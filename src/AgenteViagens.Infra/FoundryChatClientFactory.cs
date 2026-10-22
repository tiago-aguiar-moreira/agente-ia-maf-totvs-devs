using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.AI;

namespace AgenteViagens.Infra;

public static class FoundryChatClientFactory
{
    /// <summary>
    /// IChatClient do projeto Foundry. Autenticação pela identidade do `az login` — sem API key.
    /// Chat Completions, e não a Responses API: a Responses guarda a conversa no servidor, e aí o
    /// histórico não pode ser nosso (o MAF recusa um ChatHistoryProvider junto com estado no servidor).
    /// Com Chat Completions o modelo não guarda nada, e quem guarda é o Postgres.
    /// </summary>
    public static IChatClient Criar(FoundryOptions opcoes) =>
        new AIProjectClient(new Uri(opcoes.ProjectEndpoint), new DefaultAzureCredential())
            .GetProjectOpenAIClient()
            .GetChatClient(opcoes.Deployment)
            .AsIChatClient();

    /// <summary>Uma chamada mínima ao modelo para descobrir problemas antes de subir ao palco.</summary>
    public static async Task VerificarAsync(IChatClient chatClient, CancellationToken cancellationToken)
    {
        try
        {
            await chatClient.GetResponseAsync("Responda apenas: OK", cancellationToken: cancellationToken);
        }
        catch (AuthenticationFailedException ex)
        {
            throw new FalhaDeIntegracaoException("Sem credencial Azure válida. Rode `az login` e tente de novo.", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new FalhaDeIntegracaoException($"Foundry indisponível: {ex.Message}", ex);
        }
    }
}
