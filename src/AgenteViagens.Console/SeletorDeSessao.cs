using AgenteViagens.Infra.Historico;
using Spectre.Console;

namespace AgenteViagens.ConsoleApp;

/// <summary>Tela inicial: continuar uma conversa salva ou começar uma nova.</summary>
public static class SeletorDeSessao
{
    private sealed record Opcao(Guid? Id, string Texto);

    public static string Rotulo(ResumoDeSessao sessao) =>
        $"{sessao.AtualizadaEm.ToLocalTime():dd/MM HH:mm} · {sessao.Titulo}";

    /// <summary>Devolve o id da conversa escolhida, ou null para uma conversa nova.</summary>
    public static async Task<Guid?> EscolherAsync(
        IAnsiConsole tela, IReadOnlyList<ResumoDeSessao> sessoes, bool entradaRedirecionada)
    {
        // Sem teclado de verdade (stdin de um pipe) não dá para mostrar a lista.
        if (entradaRedirecionada || sessoes.Count == 0)
        {
            return null;
        }

        var prompt = new SelectionPrompt<Opcao>()
            .Title("Escolha uma conversa:")
            .UseConverter(o => Markup.Escape(o.Texto))
            .AddChoices([new Opcao(null, "➕ Nova conversa"), .. sessoes.Select(s => new Opcao(s.Id, Rotulo(s)))]);

        return (await prompt.ShowAsync(tela, CancellationToken.None)).Id;
    }
}
