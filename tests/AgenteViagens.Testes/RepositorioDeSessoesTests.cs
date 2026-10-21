using AgenteViagens.Infra.Historico;
using AgenteViagens.Testes.Fakes;
using Microsoft.Extensions.AI;

namespace AgenteViagens.Testes;

[Collection(ColecaoPostgres.Nome)]
public class RepositorioDeSessoesTests(PostgresDeTeste pg)
{
    private async Task<Guid> ConversarAsync(Guid usuario, string primeira, params ChatMessage[] resposta)
    {
        var id = Guid.NewGuid();
        var (agente, sessao) = await PostgresDeTeste.SessaoAsync(id);
        await new PostgresChatHistoryProvider(pg.Opcoes, usuario, PostgresDeTeste.Chave).InvokedAsync(
            new(agente, sessao, [new ChatMessage(ChatRole.User, primeira)], resposta.Length > 0 ? resposta : [new ChatMessage(ChatRole.Assistant, "ok")]));
        return id;
    }

    [Fact]
    public async Task Lista_so_as_sessoes_do_usuario_mais_recentes_primeiro_ate_o_limite()
    {
        var usuario = Guid.NewGuid();
        var ids = new List<Guid>();
        for (var i = 1; i <= 12; i++)
        {
            ids.Add(await ConversarAsync(usuario, $"conversa {i}"));
        }
        await ConversarAsync(Guid.NewGuid(), "de outra pessoa");

        var lista = await new RepositorioDeSessoes(pg.Opcoes).ListarAsync(usuario, 10, default);

        Assert.Equal(10, lista.Count);
        Assert.Equal(Enumerable.Range(3, 10).Reverse().Select(i => $"conversa {i}"), lista.Select(s => s.Titulo));
        Assert.Equal(ids[^1], lista[0].Id);
    }

    [Fact]
    public async Task Ultima_troca_pula_mensagens_de_tool()
    {
        var usuario = Guid.NewGuid();
        var id = await ConversarAsync(usuario, "Hotéis em Gramado?",
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("c1", "buscar_hoteis")]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("c1", "Chalé")]),
            new ChatMessage(ChatRole.Assistant, "Encontrei o Chalé das Hortênsias."));

        var (pergunta, resposta) = await new RepositorioDeSessoes(pg.Opcoes).UltimaTrocaAsync(id, usuario, default);

        Assert.Equal("Hotéis em Gramado?", pergunta);
        Assert.Equal("Encontrei o Chalé das Hortênsias.", resposta);
    }

    [Fact]
    public async Task Ultima_troca_de_outro_usuario_vem_vazia()
    {
        var id = await ConversarAsync(Guid.NewGuid(), "segredo");

        var (pergunta, resposta) = await new RepositorioDeSessoes(pg.Opcoes).UltimaTrocaAsync(id, Guid.NewGuid(), default);

        Assert.Null(pergunta);
        Assert.Null(resposta);
    }

    [Fact]
    public async Task Preparar_cria_as_tabelas_e_conta_as_sessoes()
    {
        var usuario = Guid.NewGuid();
        await ConversarAsync(usuario, "a");
        await ConversarAsync(usuario, "b");

        Assert.Equal(2, await HistoricoDb.PrepararAsync(pg.Opcoes, usuario, default));
    }
}
