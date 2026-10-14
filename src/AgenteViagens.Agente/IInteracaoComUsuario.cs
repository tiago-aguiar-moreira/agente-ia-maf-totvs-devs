namespace AgenteViagens.Agente;

/// <summary>
/// A "porta" entre o agente e quem conversa com ele. O agente pergunta; quem implementa decide
/// como perguntar (console, web, Teams...). O agente nunca escreve no console.
/// </summary>
public interface IInteracaoComUsuario
{
    Task<bool> ConfirmarAcaoAsync(string tool, IDictionary<string, object?> argumentos, CancellationToken cancellationToken);

    void NotificarChamadaDeTool(string tool, IDictionary<string, object?> argumentos, TimeSpan duracao);
}
