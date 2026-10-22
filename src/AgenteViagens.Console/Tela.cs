using AgenteViagens.Infra;
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
    private static string Resumir(string texto)
    {
        if (texto.Length <= 280)
        {
            return texto;
        }

        var corte = char.IsHighSurrogate(texto[279]) ? 279 : 280; // não corta um emoji ao meio
        return texto[..corte] + "…";
    }

    /// <summary>
    /// Erro no meio de uma resposta. Falha de integração (ex.: Postgres fora do ar) já diz a causa e
    /// como resolver; o resto, por eliminação, é com o modelo.
    /// </summary>
    public static void FalhaNaResposta(IAnsiConsole tela, Exception erro)
    {
        if (erro is FalhaDeIntegracaoException)
        {
            tela.MarkupLine($"\n[red]⚠  {Markup.Escape(erro.Message)}[/]");
            return;
        }

        tela.MarkupLine($"\n[red]⚠  Falha ao falar com o modelo, tente de novo.[/] [grey]({Markup.Escape(erro.Message)})[/]");
    }

    public static void Ok(IAnsiConsole tela, string texto) =>
        tela.MarkupLine($"[green]✓[/] {Markup.Escape(texto)}");

    public static void Erro(IAnsiConsole tela, string texto) =>
        tela.MarkupLine($"[red]✗[/] {Markup.Escape(texto)}");
}
