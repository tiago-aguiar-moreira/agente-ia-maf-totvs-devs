using AgenteViagens.Agente;

namespace AgenteViagens.Testes.Fakes;

public sealed class InteracaoFalsa(params bool[] respostas) : IInteracaoComUsuario
{
    private readonly Queue<bool> _respostas = new(respostas);

    public List<string> ConfirmacoesPedidas { get; } = [];

    /// <summary>Roda enquanto a confirmação está na tela (ex.: simular um Ctrl+C).</summary>
    public Action? AoConfirmar { get; init; }

    public Task<bool> ConfirmarAcaoAsync(string tool, IDictionary<string, object?> argumentos, CancellationToken cancellationToken)
    {
        ConfirmacoesPedidas.Add(tool);
        AoConfirmar?.Invoke();
        return Task.FromResult(_respostas.Dequeue());
    }

    public List<(string Tool, IDictionary<string, object?> Argumentos)> ToolsNotificadas { get; } = [];

    public void NotificarChamadaDeTool(string tool, IDictionary<string, object?> argumentos, TimeSpan duracao) =>
        ToolsNotificadas.Add((tool, argumentos));
}
