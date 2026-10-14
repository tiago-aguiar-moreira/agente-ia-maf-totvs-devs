using System.Net;
using System.Net.Http.Json;

namespace AgenteViagens.Infra;

public sealed record SessaoDoUsuario(string Email, string Token, DateTime ExpiraEm);

/// <summary>Login no Usuarios.Api. O JWT devolvido fica só na Infra.</summary>
public sealed class UsuariosClient(HttpClient http)
{
    public async Task<SessaoDoUsuario> LoginAsync(string email, string senha, CancellationToken cancellationToken)
    {
        HttpResponseMessage resposta;
        try
        {
            resposta = await http.PostAsJsonAsync("/usuarios/login", new { email, senha }, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new FalhaDeIntegracaoException(
                $"Usuarios.Api fora do ar em {http.BaseAddress}. Suba o servidor com `docker compose up -d`.", ex);
        }

        if (resposta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            throw new FalhaDeIntegracaoException(
                "Login recusado: confira Usuarios:Email e Usuarios:Senha nos user-secrets.");
        }

        if (!resposta.IsSuccessStatusCode)
        {
            throw new FalhaDeIntegracaoException($"Usuarios.Api respondeu {(int)resposta.StatusCode} no login.");
        }

        var corpo = await resposta.Content.ReadFromJsonAsync<RespostaDeLogin>(cancellationToken)
            ?? throw new FalhaDeIntegracaoException("Usuarios.Api devolveu um login vazio.");

        return new SessaoDoUsuario(email, corpo.Token, corpo.ExpiraEm);
    }

    private sealed record RespostaDeLogin(string Token, DateTime ExpiraEm);
}
