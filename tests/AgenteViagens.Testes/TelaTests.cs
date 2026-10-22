using AgenteViagens.ConsoleApp;
using AgenteViagens.Infra;
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

    [Fact]
    public void Ultima_troca_mostra_a_pergunta_e_a_resposta()
    {
        var tela = new TestConsole();

        Tela.UltimaTroca(tela, "Hotéis em Gramado?", "Encontrei o Chalé [5 estrelas].");

        Assert.Contains("Continuando a conversa", tela.Output);
        Assert.Contains("Você: Hotéis em Gramado?", tela.Output);
        Assert.Contains("Agente: Encontrei o Chalé [5 estrelas].", tela.Output);
    }

    [Fact]
    public void Ultima_troca_vazia_nao_mostra_nada()
    {
        var tela = new TestConsole();

        Tela.UltimaTroca(tela, null, null);

        Assert.Equal("", tela.Output);
    }

    [Fact]
    public void Falha_de_integracao_na_resposta_mostra_a_causa_sem_culpar_o_modelo()
    {
        var tela = new TestConsole();

        Tela.FalhaNaResposta(tela, new FalhaDeIntegracaoException("Não foi possível salvar a conversa no Postgres: caiu."));

        Assert.Contains("Não foi possível salvar a conversa no Postgres", tela.Output);
        Assert.DoesNotContain("modelo", tela.Output);
    }

    [Fact]
    public void Outra_falha_na_resposta_continua_culpando_o_modelo()
    {
        var tela = new TestConsole();

        Tela.FalhaNaResposta(tela, new HttpRequestException("timeout"));

        Assert.Contains("Falha ao falar com o modelo", tela.Output);
        Assert.Contains("timeout", tela.Output);
    }
}
