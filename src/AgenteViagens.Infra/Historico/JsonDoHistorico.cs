using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AgenteViagens.Infra.Historico;

/// <summary>Como as ChatMessage são gravadas e lidas da coluna jsonb.</summary>
internal static class JsonDoHistorico
{
    /// <summary>
    /// As opções do Microsoft.Extensions.AI (que conhecem chamadas de tool, aprovações etc.), aceitando
    /// o "$type" fora da primeira posição: o jsonb do Postgres reordena as chaves dos objetos.
    /// </summary>
    public static readonly JsonSerializerOptions Opcoes = new(AIJsonUtilities.DefaultOptions)
    {
        AllowOutOfOrderMetadataProperties = true,
    };

    public static string Serializar(ChatMessage mensagem) => JsonSerializer.Serialize(mensagem, Opcoes);

    public static ChatMessage Desserializar(string json) => JsonSerializer.Deserialize<ChatMessage>(json, Opcoes)!;
}
