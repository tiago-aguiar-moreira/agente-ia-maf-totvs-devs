using AgenteViagens.ConsoleApp;
using AgenteViagens.Infra.Historico;
using Spectre.Console.Testing;

namespace AgenteViagens.Testes;

public class SeletorDeSessaoTests
{
    private static readonly ResumoDeSessao Gramado =
        new(Guid.NewGuid(), "Quero ir a Gramado", new DateTimeOffset(2026, 10, 21, 22, 42, 0, TimeSpan.Zero));

    private static readonly ResumoDeSessao Recife =
        new(Guid.NewGuid(), "Passeios em Recife", new DateTimeOffset(2026, 10, 20, 18, 10, 0, TimeSpan.Zero));

    [Fact]
    public void Rotulo_mostra_data_hora_e_titulo()
    {
        var local = Gramado.AtualizadaEm.ToLocalTime();

        Assert.Equal($"{local:dd/MM HH:mm} · Quero ir a Gramado", SeletorDeSessao.Rotulo(Gramado));
    }

    [Fact]
    public async Task Entrada_redirecionada_comeca_conversa_nova_sem_perguntar()
    {
        var tela = new TestConsole();

        Assert.Null(await SeletorDeSessao.EscolherAsync(tela, [Gramado, Recife], entradaRedirecionada: true));
        Assert.DoesNotContain("Nova conversa", tela.Output);
    }

    [Fact]
    public async Task Sem_sessoes_comeca_conversa_nova()
    {
        Assert.Null(await SeletorDeSessao.EscolherAsync(new TestConsole(), [], entradaRedirecionada: false));
    }

    [Fact]
    public async Task Nova_conversa_e_a_primeira_opcao()
    {
        var tela = new TestConsole().Interactive();
        tela.Input.PushKey(ConsoleKey.Enter);

        Assert.Null(await SeletorDeSessao.EscolherAsync(tela, [Gramado, Recife], entradaRedirecionada: false));
    }

    [Fact]
    public async Task Seleciona_sessao_existente()
    {
        var tela = new TestConsole().Interactive();
        tela.Input.PushKey(ConsoleKey.DownArrow);
        tela.Input.PushKey(ConsoleKey.Enter);

        Assert.Equal(Gramado.Id, await SeletorDeSessao.EscolherAsync(tela, [Gramado, Recife], entradaRedirecionada: false));
    }
}
