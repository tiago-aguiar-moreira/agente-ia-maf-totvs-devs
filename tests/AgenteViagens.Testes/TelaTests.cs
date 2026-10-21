using AgenteViagens.ConsoleApp;
using Spectre.Console.Testing;

namespace AgenteViagens.Testes;

public class TelaTests
{
    [Fact]
    public void Banner_mostra_nome_modelo_e_atalhos()
    {
        var tela = new TestConsole();

        Tela.Banner(tela, "gpt-4.1");

        Assert.Contains("Agente de Viagens", tela.Output);
        Assert.Contains("Microsoft Agent Framework", tela.Output);
        Assert.Contains("gpt-4.1", tela.Output);
        Assert.Contains("sair", tela.Output);
        Assert.Contains("Ctrl+C", tela.Output);
    }

    [Fact]
    public void Ok_e_Erro_usam_os_simbolos_do_preflight()
    {
        var tela = new TestConsole();

        Tela.Ok(tela, "Login como [marina]");
        Tela.Erro(tela, "MCP fora do ar");

        Assert.Contains("✓ Login como [marina]", tela.Output);
        Assert.Contains("✗ MCP fora do ar", tela.Output);
    }
}
