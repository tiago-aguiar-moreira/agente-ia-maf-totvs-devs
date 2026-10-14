using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgenteViagens.Agente;

/// <summary>
/// O agente de viagens. Recebe um IChatClient qualquer (Foundry, OpenAI, Ollama...) — não sabe
/// de onde vem o modelo.
/// </summary>
public sealed class AgenteDeViagens
{
    public const string FonteDeTelemetria = "AgenteViagens";

    private readonly AIAgent _agente;

    public AgenteDeViagens(IChatClient chatClient, IEnumerable<AITool> tools, DateOnly hoje)
    {
        _agente = new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = "AgenteDeViagens",
            ChatOptions = new ChatOptions
            {
                Instructions = Instrucoes.Montar(hoje),
                Tools = [.. tools], // as tools do MCP são AIFunctions: o MAF as entrega ao modelo e executa as chamadas
            },
        });
    }

    public async IAsyncEnumerable<string> ConversarAsync(
        string mensagem, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in _agente.RunStreamingAsync(mensagem, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }
}
