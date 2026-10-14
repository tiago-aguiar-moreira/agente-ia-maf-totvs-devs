using AgenteViagens.ConsoleApp;
using AgenteViagens.Infra;
using Microsoft.Extensions.Configuration;

namespace AgenteViagens.Testes;

public class ConfiguracaoTests
{
    private static IConfiguration Config(Dictionary<string, string?> valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

    [Fact]
    public void Le_a_secao_completa()
    {
        var config = Config(new()
        {
            ["Usuarios:Url"] = "http://localhost:5450",
            ["Usuarios:Email"] = "marina.torres@example.com",
            ["Usuarios:Senha"] = "Demo@123",
        });

        var opcoes = Configuracao.Ler<UsuariosOptions>(config, "Usuarios");

        Assert.Equal("marina.torres@example.com", opcoes.Email);
    }

    [Fact]
    public void Lista_as_chaves_ausentes_na_mensagem()
    {
        var config = Config(new() { ["Usuarios:Url"] = "http://localhost:5450" });

        var erro = Assert.Throws<FalhaDeIntegracaoException>(() => Configuracao.Ler<UsuariosOptions>(config, "Usuarios"));

        Assert.Contains("Usuarios:Email", erro.Message);
        Assert.Contains("Usuarios:Senha", erro.Message);
        Assert.Contains("user-secrets", erro.Message);
    }
}
