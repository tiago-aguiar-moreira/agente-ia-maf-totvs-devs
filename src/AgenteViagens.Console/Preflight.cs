using AgenteViagens.Agente;
using AgenteViagens.Infra;
using Microsoft.Extensions.AI;
using Spectre.Console;

namespace AgenteViagens.ConsoleApp;

public sealed record Dependencias(SessaoDoUsuario Sessao, ConexaoMcp Mcp, IChatClient ChatClient);

/// <summary>Checklist de inicialização: descobre problemas antes do primeiro prompt.</summary>
public static class Preflight
{
    public static async Task<Dependencias?> ExecutarAsync(
        UsuariosOptions usuariosOptions,
        McpOptions mcpOptions,
        FoundryOptions foundryOptions,
        LangfuseOptions langfuseOptions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sessao = await Passo("Fazendo login…", async () =>
            {
                var cliente = new UsuariosClient(new HttpClient { BaseAddress = new Uri(usuariosOptions.Url) });
                var s = await cliente.LoginAsync(usuariosOptions.Email, usuariosOptions.Senha, cancellationToken);
                return (s, $"Login como {s.Email} (token válido até {s.ExpiraEm.ToLocalTime():HH:mm})");
            });

            var mcp = await Passo("Conectando ao servidor MCP…", async () =>
            {
                var c = await McpToolsFactory.ConectarAsync(mcpOptions, sessao.Token, cancellationToken);

                var ausentes = RegrasDeAprovacao.SensiveisAusentes(c.Tools);
                if (ausentes.Count > 0)
                {
                    await c.DisposeAsync();
                    throw new FalhaDeIntegracaoException(
                        $"Tools sensíveis não publicadas pelo MCP: {string.Join(", ", ausentes)}. Atualize RegrasDeAprovacao.");
                }

                var comAprovacao = RegrasDeAprovacao.Aplicar(c.Tools).Count(RegrasDeAprovacao.ExigeAprovacao);
                return (c, $"MCP conectado — {c.Tools.Count} tools ({comAprovacao} exigem aprovação)");
            });

            var chatClient = await Passo("Testando o modelo no Foundry…", async () =>
            {
                var cliente = FoundryChatClientFactory.Criar(foundryOptions);
                await FoundryChatClientFactory.VerificarAsync(cliente, cancellationToken);
                return (cliente, $"Foundry — {foundryOptions.Deployment}");
            });

            await Passo("Verificando o Langfuse…", async () =>
            {
                using var http = new HttpClient();
                await Telemetria.VerificarAsync(langfuseOptions, http, cancellationToken);
                return (true, "Telemetria → Langfuse");
            });

            return new Dependencias(sessao, mcp, chatClient);
        }
        catch
        {
            return null; // a mensagem já foi exibida pelo passo que falhou
        }
    }

    /// <summary>Spinner com o que está acontecendo; termina em ✓ resumo ou ✗ erro.</summary>
    private static async Task<T> Passo<T>(string emAndamento, Func<Task<(T Valor, string Resumo)>> acao)
    {
        try
        {
            var (valor, resumo) = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(emAndamento, _ => acao());
            Tela.Ok(AnsiConsole.Console, resumo);
            return valor;
        }
        catch (Exception ex)
        {
            Tela.Erro(AnsiConsole.Console, ex.Message);
            throw;
        }
    }
}
