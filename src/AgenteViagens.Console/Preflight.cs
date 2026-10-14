using AgenteViagens.Infra;
using Microsoft.Extensions.AI;

namespace AgenteViagens.ConsoleApp;

public sealed record Dependencias(IChatClient ChatClient);

/// <summary>Checklist de inicialização: descobre problemas antes do primeiro prompt.</summary>
public static class Preflight
{
    public static async Task<Dependencias?> ExecutarAsync(FoundryOptions foundry, CancellationToken cancellationToken = default)
    {
        try
        {
            var chatClient = await Passo(async () =>
            {
                var cliente = FoundryChatClientFactory.Criar(foundry);
                await FoundryChatClientFactory.VerificarAsync(cliente, cancellationToken);
                return (cliente, $"Foundry — {foundry.Deployment}");
            });

            return new Dependencias(chatClient);
        }
        catch
        {
            return null; // a mensagem já foi exibida pelo passo que falhou
        }
    }

    private static async Task<T> Passo<T>(Func<Task<(T Valor, string Resumo)>> acao)
    {
        try
        {
            var (valor, resumo) = await acao();
            Tela.Ok(resumo);
            return valor;
        }
        catch (Exception ex)
        {
            Tela.Erro(ex.Message);
            throw;
        }
    }
}
