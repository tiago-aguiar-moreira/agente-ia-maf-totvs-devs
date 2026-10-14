using System.Net;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace AgenteViagens.Infra;

/// <summary>Mantém o client MCP vivo enquanto as tools forem usadas.</summary>
public sealed class ConexaoMcp(McpClient cliente, IReadOnlyList<AITool> tools) : IAsyncDisposable
{
    public IReadOnlyList<AITool> Tools { get; } = tools;

    public ValueTask DisposeAsync() => cliente.DisposeAsync();
}

public static class McpToolsFactory
{
    public static async Task<ConexaoMcp> ConectarAsync(McpOptions opcoes, string token, CancellationToken cancellationToken)
    {
        var transporte = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(opcoes.Url),
            TransportMode = HttpTransportMode.StreamableHttp,
            // O JWT viaja no transporte HTTP — nunca no prompt nem nos argumentos das tools.
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });

        McpClient cliente;
        try
        {
            cliente = await McpClient.CreateAsync(transporte, cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new FalhaDeIntegracaoException(
                "Token rejeitado pelo MCP (401): confira JWT_KEY/emissor/audiência ou reinicie o console para novo login.", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new FalhaDeIntegracaoException(
                $"Não foi possível conectar ao MCP em {opcoes.Url} ({ex.Message}). " +
                "Rode `docker compose up -d` no projeto do servidor.", ex);
        }

        var tools = await cliente.ListToolsAsync(cancellationToken: cancellationToken);
        return new ConexaoMcp(cliente, [.. tools]);
    }
}
