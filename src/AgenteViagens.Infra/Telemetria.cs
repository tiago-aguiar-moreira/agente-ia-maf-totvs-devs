using System.Net;
using System.Net.Http.Headers;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AgenteViagens.Infra;

/// <summary>
/// OpenTelemetry → Langfuse. Trocar de ferramenta (OpenObserve, Aspire, Azure Monitor) é trocar
/// só o exportador: o agente não muda.
/// </summary>
public static class Telemetria
{
    public static TracerProvider Iniciar(LangfuseOptions opcoes, string fonteDoAgente) =>
        Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("agente-viagens"))
            .AddSource(fonteDoAgente)
            .AddSource("*Microsoft.Extensions.AI*") // spans de chat e de execução de tools
            .AddSource("*Microsoft.Agents.AI*")
            .AddOtlpExporter(otlp =>
            {
                otlp.Endpoint = new Uri(opcoes.OtlpEndpoint.TrimEnd('/') + "/v1/traces");
                otlp.Protocol = OtlpExportProtocol.HttpProtobuf; // o Langfuse não aceita gRPC
                otlp.HttpClientFactory = () =>
                {
                    var http = new HttpClient();
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", CredencialBasic(opcoes));
                    http.DefaultRequestHeaders.Add("x-langfuse-ingestion-version", "4");
                    return http;
                };
            })
            .Build();

    /// <summary>
    /// O exportador OTLP falha em silêncio (chave errada = traces que nunca chegam). Esta chamada
    /// autenticada à API pública do Langfuse descobre isso no preflight.
    /// </summary>
    public static async Task VerificarAsync(LangfuseOptions opcoes, HttpClient http, CancellationToken cancellationToken)
    {
        var host = new Uri(opcoes.OtlpEndpoint).GetLeftPart(UriPartial.Authority);
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, $"{host}/api/public/projects");
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Basic", CredencialBasic(opcoes));

        HttpResponseMessage resposta;
        try
        {
            resposta = await http.SendAsync(requisicao, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new FalhaDeIntegracaoException($"Langfuse inacessível em {host} ({ex.Message}).", ex);
        }

        if (resposta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new FalhaDeIntegracaoException(
                "Langfuse recusou as chaves: confira Langfuse:PublicKey/SecretKey nos user-secrets e a região do Langfuse:OtlpEndpoint.");
        }

        if (!resposta.IsSuccessStatusCode)
        {
            throw new FalhaDeIntegracaoException($"Langfuse respondeu {(int)resposta.StatusCode} em {host}.");
        }
    }

    public static string CredencialBasic(LangfuseOptions opcoes) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{opcoes.PublicKey}:{opcoes.SecretKey}"));
}
