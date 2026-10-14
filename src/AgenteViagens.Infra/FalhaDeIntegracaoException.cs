namespace AgenteViagens.Infra;

/// <summary>Falha numa dependência externa, com mensagem que diz o que fazer.</summary>
public sealed class FalhaDeIntegracaoException(string message, Exception? inner = null)
    : Exception(message, inner);
