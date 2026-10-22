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

    /// <summary>Onde a AgentSession guarda o id da conversa salva, para o provider de histórico achá-la.</summary>
    public const string ChaveSessaoId = "AgenteViagens.SessaoId";

    private readonly AIAgent _agente;
    private readonly IInteracaoComUsuario _interacao;
    private AgentSession? _sessao; // o histórico da conversa vive aqui

    public AgenteDeViagens(
        IChatClient chatClient, IEnumerable<AITool> tools, IInteracaoComUsuario interacao, DateOnly hoje,
        ChatHistoryProvider? historico = null)
    {
        _interacao = interacao;

        _agente = new ChatClientAgent(chatClient, new ChatClientAgentOptions
            {
                Name = "AgenteDeViagens",
                // Sem provider, o histórico fica na memória da AgentSession. Com ele, fica onde o provider
                // quiser (aqui, no Postgres) e sobrevive ao fechamento do console.
                ChatHistoryProvider = historico,
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

    /// <summary>Começa uma conversa nova ou retoma uma salva: a sessão só carrega o id dela.</summary>
    public async Task IniciarSessaoAsync(Guid sessaoId, CancellationToken cancellationToken)
    {
        _sessao = await _agente.CreateSessionAsync(cancellationToken);
        _sessao.StateBag.SetValue(ChaveSessaoId, sessaoId.ToString());

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
            try
            {
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

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                await FecharRodadaInterrompidaAsync(pedidos);
                throw;
            }

            entrada = [new(ChatRole.User, respostas)]; // continua na mesma sessão
        }
    }

    /// <summary>
    /// Ctrl+C durante a confirmação: nega tudo e responde os pedidos pendentes. Sem isso, a sessão
    /// guardaria uma chamada de tool sem resposta e as próximas mensagens seriam recusadas.
    /// </summary>
    private async Task FecharRodadaInterrompidaAsync(List<ToolApprovalRequestContent> pedidos)
    {
        List<AIContent> negacoes = [.. pedidos.Select(p => p.CreateResponse(false, "O usuário interrompeu; nada foi executado."))];
        await foreach (var _ in _agente.RunStreamingAsync([new(ChatRole.User, negacoes)], _sessao, cancellationToken: CancellationToken.None))
        {
            // a resposta do modelo a esta rodada é descartada: o usuário pediu para interromper
        }
    }
}
