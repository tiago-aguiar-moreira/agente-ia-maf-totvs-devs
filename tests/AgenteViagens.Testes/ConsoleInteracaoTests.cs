using AgenteViagens.ConsoleApp;
using Spectre.Console.Testing;

namespace AgenteViagens.Testes;

public class ConsoleInteracaoTests
{
    private sealed class IndicadorFalso(TestConsole tela) : IIndicadorDePensamento
    {
        public List<string> Eventos { get; } = [];

        public void Iniciar() => Eventos.Add("iniciar");

        // Guarda quanto já tinha sido escrito quando o spinner parou.
        public void Parar() => Eventos.Add($"parar@{tela.Output.Length}");
    }

    private static (ConsoleInteracao Interacao, TestConsole Tela, IndicadorFalso Indicador) Montar(string entrada = "")
    {
        var tela = new TestConsole();
        var indicador = new IndicadorFalso(tela);
        return (new ConsoleInteracao(tela, new StringReader(entrada), indicador), tela, indicador);
    }

    [Fact]
    public async Task Confirmacao_mostra_tool_e_argumentos_num_painel()
    {
        var (interacao, tela, _) = Montar("n\n");

        await interacao.ConfirmarAcaoAsync("criar_reserva", new Dictionary<string, object?> { ["quartoId"] = 15 }, default);

        Assert.Contains("Ação sensível", tela.Output);
        Assert.Contains("criar_reserva", tela.Output);
        Assert.Contains("quartoId: 15", tela.Output);
        Assert.Contains("Confirmar? (s/N)", tela.Output);
    }

    [Theory]
    [InlineData("s", true)]
    [InlineData("sim", true)]
    [InlineData("", false)]
    [InlineData("n", false)]
    [InlineData("talvez", false)]
    public async Task Confirmacao_aprova_so_com_s_ou_sim(string resposta, bool esperado)
    {
        var (interacao, _, _) = Montar(resposta + "\n");

        var aprovado = await interacao.ConfirmarAcaoAsync("criar_reserva", new Dictionary<string, object?>(), default);

        Assert.Equal(esperado, aprovado);
    }

    [Fact]
    public async Task Spinner_para_antes_de_pedir_confirmacao()
    {
        var (interacao, _, indicador) = Montar("n\n");

        await interacao.ConfirmarAcaoAsync("criar_reserva", new Dictionary<string, object?>(), default);

        Assert.Equal("parar@0", indicador.Eventos.First());
    }

    [Fact]
    public void Notificacao_de_tool_mostra_nome_argumentos_e_duracao()
    {
        var (interacao, tela, indicador) = Montar();

        interacao.NotificarChamadaDeTool(
            "buscar_hoteis", new Dictionary<string, object?> { ["estado"] = "RS" }, TimeSpan.FromMilliseconds(583));

        Assert.Contains("🔧 buscar_hoteis(estado: RS) · 583 ms", tela.Output);
        Assert.Equal("parar@0", indicador.Eventos.First());
    }

    [Fact]
    public async Task Argumentos_com_colchetes_aparecem_literalmente()
    {
        var (interacao, tela, _) = Montar("n\n");

        await interacao.ConfirmarAcaoAsync("criar_reserva", new Dictionary<string, object?> { ["obs"] = "[vip]" }, default);

        Assert.Contains("obs: [vip]", tela.Output);
    }
}
