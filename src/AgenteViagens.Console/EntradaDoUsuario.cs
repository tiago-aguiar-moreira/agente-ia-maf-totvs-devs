namespace AgenteViagens.ConsoleApp;

public enum TipoDeEntrada { Mensagem, Vazia, Sair }

public static class EntradaDoUsuario
{
    public static TipoDeEntrada Interpretar(string? linha) => linha?.Trim().ToLowerInvariant() switch
    {
        null => TipoDeEntrada.Sair, // stdin fechado (Ctrl+Z)
        "sair" or "exit" => TipoDeEntrada.Sair,
        "" => TipoDeEntrada.Vazia,
        _ => TipoDeEntrada.Mensagem,
    };

    /// <summary>Só "s" ou "sim" aprovam. Enter vazio ou qualquer outra coisa = NÃO.</summary>
    public static bool Aprovou(string? resposta) => resposta?.Trim().ToLowerInvariant() is "s" or "sim";
}
