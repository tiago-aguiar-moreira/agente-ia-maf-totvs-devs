using AgenteViagens.Infra;
using Microsoft.Extensions.AI;

namespace AgenteViagens.ConsoleApp;

public sealed record Dependencias(SessaoDoUsuario Sessao, ConexaoMcp Mcp, IChatClient ChatClient);

/// <summary>Checklist de inicialização: descobre problemas antes do primeiro prompt.</summary>
public static class Preflight
{
    public static async Task<Dependencias?> ExecutarAsync(
        UsuariosOptions usuarios, McpOptions mcp, FoundryOptions foundry, CancellationToken cancellationToken = default)
    {
        try
        {
            var sessao = await Passo(async () =>
            {
                var cliente = new UsuariosClient(new HttpClient { BaseAddress = new Uri(usuarios.Url) });
                var s = await cliente.LoginAsync(usuarios.Email, usuarios.Senha, cancellationToken);
                return (s, $"Login como {s.Email} (token válido até {s.ExpiraEm.ToLocalTime():HH:mm})");
            });

            var conexao = await Passo(async () =>
            {
                var c = await McpToolsFactory.ConectarAsync(mcp, sessao.Token, cancellationToken);
                return (c, $"MCP conectado — {c.Tools.Count} tools");
            });

            var chatClient = await Passo(async () =>
            {
                var cliente = FoundryChatClientFactory.Criar(foundry);
                await FoundryChatClientFactory.VerificarAsync(cliente, cancellationToken);
                return (cliente, $"Foundry — {foundry.Deployment}");
            });

            return new Dependencias(sessao, conexao, chatClient);
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
