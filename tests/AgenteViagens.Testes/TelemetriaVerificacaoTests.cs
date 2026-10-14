using System.Net;
using AgenteViagens.Infra;
using AgenteViagens.Testes.Fakes;

namespace AgenteViagens.Testes;

public class TelemetriaVerificacaoTests
{
    private static readonly LangfuseOptions Opcoes = new()
    {
        OtlpEndpoint = "https://us.cloud.langfuse.com/api/public/otel", PublicKey = "pk-lf-1", SecretKey = "sk-lf-2",
    };

    [Fact]
    public async Task Chaves_validas_consultam_a_api_publica_com_basic()
    {
        var handler = new HttpHandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Telemetria.VerificarAsync(Opcoes, new HttpClient(handler), default);

        Assert.Equal("https://us.cloud.langfuse.com/api/public/projects", handler.UltimaRequisicao!.RequestUri!.ToString());
        Assert.Equal("Basic", handler.UltimaRequisicao.Headers.Authorization!.Scheme);
    }

    [Fact]
    public async Task Chaves_invalidas_explicam_onde_corrigir()
    {
        var handler = new HttpHandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var erro = await Assert.ThrowsAsync<FalhaDeIntegracaoException>(
            () => Telemetria.VerificarAsync(Opcoes, new HttpClient(handler), default));

        Assert.Contains("Langfuse", erro.Message);
        Assert.Contains("user-secrets", erro.Message);
    }
}
