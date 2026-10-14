using System.Net;
using System.Text;
using AgenteViagens.Infra;
using AgenteViagens.Testes.Fakes;

namespace AgenteViagens.Testes;

public class UsuariosClientTests
{
    private static UsuariosClient Cliente(HttpHandlerFalso handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5450") });

    [Fact]
    public async Task Login_com_sucesso_devolve_token_e_expiracao()
    {
        var handler = new HttpHandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"token":"abc.def.ghi","expiraEm":"2026-10-14T20:00:00Z"}""", Encoding.UTF8, "application/json"),
        });

        var sessao = await Cliente(handler).LoginAsync("marina.torres@example.com", "Demo@123", default);

        Assert.Equal("abc.def.ghi", sessao.Token);
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
