using AgenteViagens.Infra;

namespace AgenteViagens.Testes;

public class McpToolsFactoryTests
{
    [Fact]
    public async Task Servidor_fora_do_ar_gera_mensagem_com_a_url()
    {
        var opcoes = new McpOptions { Url = "http://localhost:1" }; // porta sem ninguém escutando

        var erro = await Assert.ThrowsAsync<FalhaDeIntegracaoException>(
            () => McpToolsFactory.ConectarAsync(opcoes, "token", default));

        Assert.Contains("http://localhost:1", erro.Message);
    }
}
