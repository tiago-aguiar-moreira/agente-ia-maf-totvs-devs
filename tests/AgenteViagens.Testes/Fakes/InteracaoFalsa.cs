using AgenteViagens.Agente;

namespace AgenteViagens.Testes.Fakes;

public sealed class InteracaoFalsa(params bool[] respostas) : IInteracaoComUsuario
{
    private readonly Queue<bool> _respostas = new(respostas);

    public List<string> ConfirmacoesPedidas { get; } = [];

    public Task<bool> ConfirmarAcaoAsync(string tool, IDictionary<string, object?> argumentos, CancellationToken cancellationToken)
    {
        ConfirmacoesPedidas.Add(tool);
        return Task.FromResult(_respostas.Dequeue());
    }
}
