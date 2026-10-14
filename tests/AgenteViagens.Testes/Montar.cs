using AgenteViagens.Agente;
using AgenteViagens.Testes.Fakes;
using Microsoft.Extensions.AI;

namespace AgenteViagens.Testes;

public static class Montar
{
    public static readonly DateOnly Hoje = new(2026, 10, 14);

    public static AgenteDeViagens Agente(ChatClientFalso chat, IEnumerable<AITool>? tools = null) =>
        new(chat, tools ?? [], Hoje);

    public static async Task<string> ConversarTudoAsync(this AgenteDeViagens agente, string mensagem)
    {
        var texto = "";
        await foreach (var trecho in agente.ConversarAsync(mensagem))
        {
            texto += trecho;
        }
        return texto;
    }
}
