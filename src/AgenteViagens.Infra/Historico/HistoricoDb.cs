using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgenteViagens.Infra.Historico;

public static class HistoricoDb
{
    public static DbContextOptions<HistoricoDbContext> Opcoes(PostgresOptions opcoes) =>
        new DbContextOptionsBuilder<HistoricoDbContext>().UseNpgsql(opcoes.ConnectionString).Options;

    /// <summary>Cria as tabelas se não existirem (sem migrações) e conta as conversas salvas do usuário.</summary>
    public static async Task<int> PrepararAsync(
        DbContextOptions<HistoricoDbContext> opcoes, Guid usuarioId, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = new HistoricoDbContext(opcoes);
            await db.Database.EnsureCreatedAsync(cancellationToken);
            return await db.Sessoes.CountAsync(s => s.UsuarioId == usuarioId, cancellationToken);
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException { InnerException: NpgsqlException })
        {
            throw new FalhaDeIntegracaoException(
                $"Postgres fora do ar ({ex.Message}). Suba com `docker compose up -d` na raiz deste repositório.", ex);
        }
    }
}
