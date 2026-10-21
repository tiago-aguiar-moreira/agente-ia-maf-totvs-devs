using System.Net;
using System.Text;
using AgenteViagens.Infra;
using AgenteViagens.Testes.Fakes;

namespace AgenteViagens.Testes;

public class UsuariosClientTests
{
    private static readonly Guid Marina = Guid.Parse("a1b2c3d4-0000-4000-8000-000000000001");

    private static UsuariosClient Cliente(HttpHandlerFalso handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5450") });

    /// <summary>JWT bem formado (assinatura falsa): header.payload.assinatura em base64url.</summary>
    private static string Jwt(string payloadJson)
    {
        static string B64Url(string texto) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(texto)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{B64Url("""{"alg":"HS256","typ":"JWT"}""")}.{B64Url(payloadJson)}.assinatura";
    }

    private static HttpHandlerFalso LoginDevolvendo(string token) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""{"token":"{{token}}","expiraEm":"2026-10-14T20:00:00Z"}""", Encoding.UTF8, "application/json"),
        });

    [Fact]
    public async Task Login_extrai_o_usuario_do_claim_sub()
    {
        var sessao = await Cliente(LoginDevolvendo(Jwt($$"""{"sub":"{{Marina}}","email":"m@x.com"}""")))
            .LoginAsync("m@x.com", "y", default);

        Assert.Equal(Marina, sessao.UsuarioId);
    }

    [Theory]
    [InlineData("""{"email":"m@x.com"}""")]
    [InlineData("""{"sub":"nao-e-guid"}""")]
    public async Task Token_sem_sub_valido_explica_o_problema(string payload)
    {
        var erro = await Assert.ThrowsAsync<FalhaDeIntegracaoException>(
            () => Cliente(LoginDevolvendo(Jwt(payload))).LoginAsync("m@x.com", "y", default));

        Assert.Contains("sub", erro.Message);
    }

    [Fact]
    public async Task Login_com_sucesso_devolve_token_e_expiracao()
    {
        var token = Jwt($$"""{"sub":"{{Marina}}"}""");
        var handler = LoginDevolvendo(token);

        var sessao = await Cliente(handler).LoginAsync("marina.torres@example.com", "Demo@123", default);

        Assert.Equal(token, sessao.Token);
        Assert.Equal("marina.torres@example.com", sessao.Email);
        Assert.Equal(new DateTime(2026, 10, 14, 20, 0, 0, DateTimeKind.Utc), sessao.ExpiraEm.ToUniversalTime());
        Assert.Equal("/usuarios/login", handler.UltimaRequisicao!.RequestUri!.AbsolutePath);
        Assert.Contains("\"senha\":\"Demo@123\"", handler.UltimoCorpo);
    }

    [Fact]
    public async Task Credenciais_invalidas_explicam_onde_corrigir()
    {
        var handler = new HttpHandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var erro = await Assert.ThrowsAsync<FalhaDeIntegracaoException>(
            () => Cliente(handler).LoginAsync("x@x.com", "errada", default));

        Assert.Contains("Login recusado", erro.Message);
        Assert.Contains("user-secrets", erro.Message);
    }

    [Fact]
    public async Task Api_fora_do_ar_sugere_subir_os_containers()
    {
        var handler = new HttpHandlerFalso(_ => throw new HttpRequestException("Connection refused"));

        var erro = await Assert.ThrowsAsync<FalhaDeIntegracaoException>(
            () => Cliente(handler).LoginAsync("x@x.com", "y", default));

        Assert.Contains("docker compose up -d", erro.Message);
    }
}
