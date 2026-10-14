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

    public static string CredencialBasic(LangfuseOptions opcoes) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{opcoes.PublicKey}:{opcoes.SecretKey}"));
}
