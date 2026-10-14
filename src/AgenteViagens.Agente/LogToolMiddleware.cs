using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgenteViagens.Agente;

/// <summary>
/// Middleware de function calling: roda em volta de CADA execução de tool. Aqui só medimos e
/// avisamos a view — mas é o mesmo ponto para auditoria, cache, bloqueio por regra de negócio...
/// </summary>
public sealed class LogToolMiddleware(IInteracaoComUsuario interacao)
{
    public async ValueTask<object?> InvocarAsync(
        AIAgent agente,
        FunctionInvocationContext contexto,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> proximo,
        CancellationToken cancellationToken)
    {
        var cronometro = Stopwatch.StartNew();
        try
        {
            return await proximo(contexto, cancellationToken);
        }
        finally
        {
            interacao.NotificarChamadaDeTool(contexto.Function.Name, contexto.Arguments, cronometro.Elapsed);
        }
    }
}
