using AgenteViagens.Infra;
using Microsoft.Extensions.Configuration;

namespace AgenteViagens.ConsoleApp;

public static class Configuracao
{
    /// <summary>Lê uma seção e falha dizendo exatamente quais chaves faltam.</summary>
    public static T Ler<T>(IConfiguration config, string secao) where T : new()
    {
        var opcoes = config.GetSection(secao).Get<T>() ?? new T();

        var faltando = typeof(T).GetProperties()
            .Where(p => p.PropertyType == typeof(string) && string.IsNullOrWhiteSpace((string?)p.GetValue(opcoes)))
            .Select(p => $"{secao}:{p.Name}")
            .ToList();

        if (faltando.Count > 0)
        {
            throw new FalhaDeIntegracaoException(
                $"Configuração ausente: {string.Join(", ", faltando)}. " +
                "Preencha no appsettings.json ou com `dotnet user-secrets set` (veja o README).");
        }

        return opcoes;
    }
}
