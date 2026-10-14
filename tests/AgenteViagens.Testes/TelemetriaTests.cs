using System.Text;
using AgenteViagens.Infra;

namespace AgenteViagens.Testes;

public class TelemetriaTests
{
    [Fact]
    public void Credencial_basic_e_base64_de_public_dois_pontos_secret()
    {
        var opcoes = new LangfuseOptions { PublicKey = "pk-lf-1", SecretKey = "sk-lf-2" };

        var credencial = Telemetria.CredencialBasic(opcoes);

        Assert.Equal("pk-lf-1:sk-lf-2", Encoding.UTF8.GetString(Convert.FromBase64String(credencial)));
    }
}
