using AgenteViagens.Agente;

namespace AgenteViagens.ConsoleApp;

public sealed class ConsoleInteracao : IInteracaoComUsuario
{
    public Task<bool> ConfirmarAcaoAsync(string tool, IDictionary<string, object?> argumentos, CancellationToken cancellationToken)
    {
        Tela.Escrever(ConsoleColor.Yellow, $"\n⚠  Ação sensível: {tool}({Formatar(argumentos)})\n   Confirmar? (s/N): ");
        return Task.FromResult(EntradaDoUsuario.Aprovou(Console.ReadLine()));
    }

    private static string Formatar(IDictionary<string, object?> argumentos) =>
        string.Join(", ", argumentos.Select(a => $"{a.Key}: {a.Value}"));
}
