namespace AgenteViagens.Infra;

public sealed class FoundryOptions
{
    public string ProjectEndpoint { get; init; } = "";
    public string Deployment { get; init; } = "";
}

public sealed class McpOptions
{
    public string Url { get; init; } = "";
}

public sealed class UsuariosOptions
{
    public string Url { get; init; } = "";
    public string Email { get; init; } = "";
    public string Senha { get; init; } = "";
}

public sealed class LangfuseOptions
{
    public string OtlpEndpoint { get; init; } = "";
    public string PublicKey { get; init; } = "";
    public string SecretKey { get; init; } = "";
}
