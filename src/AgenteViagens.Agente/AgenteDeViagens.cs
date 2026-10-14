using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgenteViagens.Agente;

/// <summary>
/// O agente de viagens. Recebe um IChatClient qualquer (Foundry, OpenAI, Ollama...) e as tools —
/// não sabe de onde vem o modelo nem que as tools vêm de um servidor MCP.
/// </summary>
public sealed class AgenteDeViagens
{
    public const string FonteDeTelemetria = "AgenteViagens";

    private readonly AIAgent _agente;
    private readonly IInteracaoComUsuario _interacao;
    private AgentSession? _sessao; // o histórico da conversa vive aqui

    public AgenteDeViagens(IChatClient chatClient, IEnumerable<AITool> tools, IInteracaoComUsuario interacao, DateOnly hoje)
    {
        _interacao = interacao;

        _agente = new ChatClientAgent(chatClient, new ChatClientAgentOptions
            {
                Name = "AgenteDeViagens",
                ChatOptions = new ChatOptions
                {
                    Instructions = Instrucoes.Montar(hoje),
                    Tools = RegrasDeAprovacao.Aplicar(tools),
                },
            })
            .AsBuilder()
            .Use(new LogToolMiddleware(interacao).InvocarAsync)
            // Prompts e respostas vão para os traces (EnableSensitiveData). O JWT não: ele está no
            // transporte HTTP do MCP, fora das mensagens.
            .UseOpenTelemetry(FonteDeTelemetria, cfg => cfg.EnableSensitiveData = true)
            .Build();
    }

    public async IAsyncEnumerable<string> ConversarAsync(
        string mensagem, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _sessao ??= await _agente.CreateSessionAsync(cancellationToken);
        List<ChatMessage> entrada = [new(ChatRole.User, mensagem)];

        while (true)
        {
            List<ToolApprovalRequestContent> pedidos = [];

            await foreach (var update in _agente.RunStreamingAsync(entrada, _sessao, cancellationToken: cancellationToken))
            {
                pedidos.AddRange(update.Contents.OfType<ToolApprovalRequestContent>());

                if (!string.IsNullOrEmpty(update.Text))
                {
                    yield return update.Text;
                }
            }

            if (pedidos.Count == 0)
            {
                yield break;
            }

            // O MAF pausou: há tools sensíveis esperando o "sim" do usuário.
            List<AIContent> respostas = [];
            foreach (var pedido in pedidos)
            {
                var chamada = (FunctionCallContent)pedido.ToolCall;
                var aprovado = await _interacao.ConfirmarAcaoAsync(
                    chamada.Name, chamada.Arguments ?? new Dictionary<string, object?>(), cancellationToken);
                respostas.Add(aprovado
                    ? pedido.CreateResponse(true)
                    // Sem o motivo, o modelo costuma achar que foi falha do sistema.
                    : pedido.CreateResponse(false, "O usuário recusou esta ação; nada foi executado."));
            }

            entrada = [new(ChatRole.User, respostas)]; // continua na mesma sessão
        }
    }
}
