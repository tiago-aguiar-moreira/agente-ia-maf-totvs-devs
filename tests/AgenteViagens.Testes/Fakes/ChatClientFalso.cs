using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace AgenteViagens.Testes.Fakes;

/// <summary>
/// Modelo "de mentira": devolve respostas enfileiradas e registra tudo que recebeu.
/// Se o agente chamar o modelo mais vezes do que o teste previu, a fila vazia lança exceção.
/// </summary>
public sealed class ChatClientFalso : IChatClient
{
    private readonly Queue<ChatResponse> _respostas = new();

    public List<List<ChatMessage>> MensagensRecebidas { get; } = [];
    public List<ChatOptions?> OpcoesRecebidas { get; } = [];

    public ChatClientFalso Responder(params AIContent[] conteudos)
    {
        _respostas.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, [.. conteudos])));
        return this;
    }

    public ChatClientFalso ResponderTexto(string texto) => Responder(new TextContent(texto));

    public ChatClientFalso ResponderChamada(string callId, string tool, Dictionary<string, object?>? argumentos = null) =>
        Responder(new FunctionCallContent(callId, tool, argumentos ?? []));

    /// <summary>Tudo o que chegou ao modelo (instruções + textos de todas as mensagens), para asserts.</summary>
    public string TextoEnviadoAoModelo(int chamada) =>
        string.Join("\n",
            new[] { OpcoesRecebidas[chamada]?.Instructions ?? "" }
                .Concat(MensagensRecebidas[chamada].Select(m => m.Text)));

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); // como um client real
        MensagensRecebidas.Add(messages.ToList());
        OpcoesRecebidas.Add(options);
        return Task.FromResult(_respostas.Dequeue());
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var resposta = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in resposta.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
