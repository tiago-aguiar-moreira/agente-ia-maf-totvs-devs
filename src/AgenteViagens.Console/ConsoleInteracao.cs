using AgenteViagens.Agente;
using Spectre.Console;

namespace AgenteViagens.ConsoleApp;

public sealed class ConsoleInteracao(IAnsiConsole tela, TextReader entrada, IIndicadorDePensamento indicador)
    : IInteracaoComUsuario
{
    public Task<bool> ConfirmarAcaoAsync(string tool, IDictionary<string, object?> argumentos, CancellationToken cancellationToken)
    {
        indicador.Parar(); // o spinner nunca fica por cima de uma pergunta

        tela.WriteLine();
        tela.Write(new Panel(new Text($"{tool}({Formatar(argumentos)})"))
            .Header("[yellow] ⚠ Ação sensível [/]")
            .RoundedBorder()
            .BorderColor(Color.Yellow));
        tela.Markup("[yellow]Confirmar? (s/N):[/] ");

        return Task.FromResult(EntradaDoUsuario.Aprovou(entrada.ReadLine()));
    }

    public void NotificarChamadaDeTool(string tool, IDictionary<string, object?> argumentos, TimeSpan duracao)
    {
        indicador.Parar();
        tela.MarkupLine($"\n[grey]   🔧 {Markup.Escape($"{tool}({Formatar(argumentos)})")} · {duracao.TotalMilliseconds:N0} ms[/]");
    }

    private static string Formatar(IDictionary<string, object?> argumentos) =>
        string.Join(", ", argumentos.Select(a => $"{a.Key}: {a.Value}"));
}
