using Spectre.Console;
using Spectre.Console.Rendering;

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

    /// <summary>Ao retomar uma conversa salva: de onde ela continua.</summary>
    public static void UltimaTroca(IAnsiConsole tela, string? pergunta, string? resposta)
    {
        if (pergunta is null && resposta is null)
        {
            return;
        }

        var linhas = new List<IRenderable>();
        if (pergunta is not null)
        {
            linhas.Add(new Markup($"[cyan]Você:[/] {Markup.Escape(Resumir(pergunta))}"));
        }
        if (resposta is not null)
        {
            linhas.Add(new Markup($"[green]Agente:[/] {Markup.Escape(Resumir(resposta))}"));
        }

        tela.Write(new Panel(new Rows(linhas))
            .Header("[grey] Continuando a conversa [/]")
            .RoundedBorder()
            .BorderColor(Color.Grey));
    }

    /// <summary>Respostas longas viram um trecho: a ideia é lembrar o contexto, não reler tudo.</summary>
    private static string Resumir(string texto) => texto.Length <= 280 ? texto : texto[..280] + "…";

    public static void Ok(IAnsiConsole tela, string texto) =>
        tela.MarkupLine($"[green]✓[/] {Markup.Escape(texto)}");

    public static void Erro(IAnsiConsole tela, string texto) =>
        tela.MarkupLine($"[red]✗[/] {Markup.Escape(texto)}");
}
