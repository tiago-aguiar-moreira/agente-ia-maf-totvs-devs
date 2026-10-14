using AgenteViagens.Agente;

namespace AgenteViagens.ConsoleApp;

public sealed class ConsoleInteracao : IInteracaoComUsuario
{
    public Task<bool> ConfirmarAcaoAsync(string tool, IDictionary<string, object?> argumentos, CancellationToken cancellationToken)
    {
        Tela.Escrever(ConsoleColor.Yellow, $"\n⚠  Ação sensível: {tool}({Formatar(argumentos)})\n   Confirmar? (s/N): ");
        return Task.FromResult(EntradaDoUsuario.Aprovou(Console.ReadLine()));
    }

    public void NotificarChamadaDeTool(string tool, IDictionary<string, object?> argumentos, TimeSpan duracao) =>
        Tela.Escrever(ConsoleColor.DarkGray, $"\n   🔧 {tool}({Formatar(argumentos)}) · {duracao.TotalMilliseconds:N0} ms\n");

    private static string Formatar(IDictionary<string, object?> argumentos) =>
        string.Join(", ", argumentos.Select(a => $"{a.Key}: {a.Value}"));
}
