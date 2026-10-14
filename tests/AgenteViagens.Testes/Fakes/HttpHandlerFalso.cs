namespace AgenteViagens.Testes.Fakes;

public sealed class HttpHandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public HttpRequestMessage? UltimaRequisicao { get; private set; }
    public string? UltimoCorpo { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        UltimaRequisicao = request;
        UltimoCorpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return responder(request);
    }
}
