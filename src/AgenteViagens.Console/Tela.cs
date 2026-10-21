using Spectre.Console;

namespace AgenteViagens.ConsoleApp;

public static class Tela
{
    /// <summary>Caixa de boas-vindas no estilo do Claude Code.</summary>
    public static void Banner(IAnsiConsole tela, string modelo)
    {
        var conteudo = new Rows(
            new Markup("[bold]✈  Agente de Viagens[/]"),
            new Markup($"[grey]Microsoft Agent Framework · {Markup.Escape(modelo)} · MCP[/]"),
            new Markup("[grey]sair para encerrar · Ctrl+C interrompe a resposta[/]"));

        tela.Write(new Panel(conteudo).RoundedBorder().BorderColor(Color.Orange1).Padding(1, 0));
        tela.WriteLine();
    }

    public static void Ok(IAnsiConsole tela, string texto) =>
        tela.MarkupLine($"[green]✓[/] {Markup.Escape(texto)}");

    public static void Erro(IAnsiConsole tela, string texto) =>
        tela.MarkupLine($"[red]✗[/] {Markup.Escape(texto)}");
}
