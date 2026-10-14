using AgenteViagens.Agente;
using AgenteViagens.Testes.Fakes;
using Microsoft.Extensions.AI;

namespace AgenteViagens.Testes;

public static class Montar
{
    public static readonly DateOnly Hoje = new(2026, 10, 14);

    public static AgenteDeViagens Agente(
        ChatClientFalso chat, IEnumerable<AITool>? tools = null, InteracaoFalsa? interacao = null) =>
        new(chat, tools ?? [], interacao ?? new InteracaoFalsa(), Hoje);

    public static async Task<string> ConversarTudoAsync(
        this AgenteDeViagens agente, string mensagem, CancellationToken cancellationToken = default)
    {
        var texto = "";
        await foreach (var trecho in agente.ConversarAsync(mensagem, cancellationToken))
        {
            texto += trecho;
        }
        return texto;
    }
}
