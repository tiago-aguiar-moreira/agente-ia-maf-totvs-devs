using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AgenteViagens.Infra;

public sealed record SessaoDoUsuario(string Email, string Token, DateTime ExpiraEm, Guid UsuarioId);

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

        return new SessaoDoUsuario(email, corpo.Token, corpo.ExpiraEm, UsuarioDoToken(corpo.Token));
    }

    /// <summary>
    /// Lê o claim <c>sub</c> (id do usuário) do payload do JWT, sem validar a assinatura: o token
    /// acabou de chegar do Usuarios.Api, e quem valida é o servidor MCP. Só serve para saber de quem
    /// são as conversas salvas.
    /// </summary>
    private static Guid UsuarioDoToken(string token)
    {
        try
        {
            var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            if (json.RootElement.TryGetProperty("sub", out var sub) && Guid.TryParse(sub.GetString(), out var id))
            {
                return id;
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or FormatException or JsonException or InvalidOperationException)
        {
        }

        throw new FalhaDeIntegracaoException("O token do Usuarios.Api não tem um claim sub com o id do usuário.");
    }

    private sealed record RespostaDeLogin(string Token, DateTime ExpiraEm);
}
