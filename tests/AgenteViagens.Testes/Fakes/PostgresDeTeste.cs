using AgenteViagens.Infra;
using AgenteViagens.Infra.Historico;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Testcontainers.PostgreSql;

namespace AgenteViagens.Testes.Fakes;

/// <summary>Um Postgres de verdade em container, compartilhado pelos testes; cada teste usa um usuário próprio.</summary>
public sealed class PostgresDeTeste : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder().WithImage("postgres:16").Build();

    public DbContextOptions<HistoricoDbContext> Opcoes { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Opcoes = HistoricoDb.Opcoes(new PostgresOptions { ConnectionString = _container.GetConnectionString() });
        await HistoricoDb.PrepararAsync(Opcoes, Guid.NewGuid(), default);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public const string Chave = "AgenteViagens.SessaoId";

    /// <summary>Uma AgentSession apontando para o id informado, como o agente fará.</summary>
    public static async Task<(AIAgent Agente, AgentSession Sessao)> SessaoAsync(Guid sessaoId)
    {
        AIAgent agente = new ChatClientAgent(new ChatClientFalso());
        var sessao = await agente.CreateSessionAsync();
        sessao.StateBag.SetValue(Chave, sessaoId.ToString());
        return (agente, sessao);
    }
}

[CollectionDefinition(Nome)]
public sealed class ColecaoPostgres : ICollectionFixture<PostgresDeTeste>
{
    public const string Nome = "Postgres";
}
