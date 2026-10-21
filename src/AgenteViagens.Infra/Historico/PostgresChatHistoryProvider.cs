using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace AgenteViagens.Infra.Historico;

/// <summary>
/// Histórico da conversa fora do processo: o MAF chama <see cref="ProvideChatHistoryAsync"/> antes de
/// cada chamada ao modelo e <see cref="StoreChatHistoryAsync"/> depois. A sessão (AgentSession) só
/// carrega o id da conversa; as mensagens vivem no Postgres e sobrevivem ao fechamento do console.
/// </summary>
public sealed partial class PostgresChatHistoryProvider(
    DbContextOptions<HistoricoDbContext> opcoes, Guid usuarioId, string chaveSessaoId) : ChatHistoryProvider
{
    private const int TamanhoDoTitulo = 60;

    public override IReadOnlyList<string> StateKeys => [chaveSessaoId];

    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(
        InvokingContext context, CancellationToken cancellationToken = default)
    {
        var sessaoId = SessaoId(context.Session);

        return await Executar(async db =>
        {
            await GarantirDono(db, sessaoId, cancellationToken);

            var conteudos = await db.Mensagens
                .Where(m => m.SessaoId == sessaoId)
                .OrderBy(m => m.Id)
                .Select(m => m.Conteudo)
                .ToListAsync(cancellationToken);

            return conteudos.Select(JsonDoHistorico.Desserializar).ToList();
        });
    }

    protected override async ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        if (context.InvokeException is not null)
        {
            return; // rodada que falhou não entra no histórico
        }

        var sessaoId = SessaoId(context.Session);
        var novas = context.RequestMessages.Concat(context.ResponseMessages ?? []).ToList();
        var agora = DateTimeOffset.UtcNow;

        await Executar(async db =>
        {
            var sessao = await GarantirDono(db, sessaoId, cancellationToken);
            if (sessao is null)
            {
                var primeira = novas.FirstOrDefault(m => m.Role == ChatRole.User && !string.IsNullOrWhiteSpace(m.Text));
                sessao = new SessaoSalva
                {
                    Id = sessaoId, UsuarioId = usuarioId, Titulo = Titulo(primeira?.Text ?? "Nova conversa"), CriadaEm = agora,
                };
                db.Sessoes.Add(sessao);
            }

            sessao.AtualizadaEm = agora;
            db.Mensagens.AddRange(novas.Select(m => new MensagemSalva
            {
                SessaoId = sessaoId,
                Papel = m.Role.Value,
                Conteudo = JsonDoHistorico.Serializar(m),
                CriadaEm = agora,
            }));

            await db.SaveChangesAsync(cancellationToken);
            return true;
        });
    }

    /// <summary>A primeira mensagem do usuário numa linha só, cortada em 60 caracteres.</summary>
    public static string Titulo(string texto)
    {
        var linha = EspacosEmBranco().Replace(texto, " ").Trim();
        return linha.Length <= TamanhoDoTitulo ? linha : linha[..TamanhoDoTitulo] + "…";
    }

    private Guid SessaoId(AgentSession? sessao) =>
        sessao is not null && sessao.StateBag.TryGetValue<string>(chaveSessaoId, out var valor) && Guid.TryParse(valor, out var id)
            ? id
            : throw new InvalidOperationException("A sessão não tem o id da conversa salva. Chame IniciarSessaoAsync antes.");

    /// <summary>Devolve a sessão (ou null se ainda não existe) e garante que ela é do usuário logado.</summary>
    private async Task<SessaoSalva?> GarantirDono(HistoricoDbContext db, Guid sessaoId, CancellationToken cancellationToken)
    {
        var sessao = await db.Sessoes.SingleOrDefaultAsync(s => s.Id == sessaoId, cancellationToken);
        if (sessao is not null && sessao.UsuarioId != usuarioId)
        {
            throw new InvalidOperationException("Esta conversa pertence a outro usuário.");
        }
        return sessao;
    }

    private async Task<T> Executar<T>(Func<HistoricoDbContext, Task<T>> acao)
    {
        try
        {
            await using var db = new HistoricoDbContext(opcoes);
            return await acao(db);
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or DbUpdateException
                                       or InvalidOperationException { InnerException: Npgsql.NpgsqlException })
        {
            throw new FalhaDeIntegracaoException($"Não foi possível salvar a conversa no Postgres: {ex.Message}", ex);
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspacosEmBranco();
}
