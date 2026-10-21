using AgenteViagens.Infra;
using AgenteViagens.Infra.Historico;
using AgenteViagens.Testes.Fakes;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace AgenteViagens.Testes;

[Collection(ColecaoPostgres.Nome)]
public class PostgresChatHistoryProviderTests(PostgresDeTeste pg)
{
    private readonly Guid _usuario = Guid.NewGuid();

    private PostgresChatHistoryProvider Provider(Guid? usuario = null) => new(pg.Opcoes, usuario ?? _usuario, PostgresDeTeste.Chave);

    private static async Task GravarAsync(
        PostgresChatHistoryProvider provider, AIAgent agente, AgentSession sessao, ChatMessage[] pedido, ChatMessage[] resposta) =>
        await provider.InvokedAsync(new ChatHistoryProvider.InvokedContext(agente, sessao, pedido, resposta));

    private static async Task<List<ChatMessage>> LerAsync(PostgresChatHistoryProvider provider, AIAgent agente, AgentSession sessao) =>
        [.. await provider.InvokingAsync(new ChatHistoryProvider.InvokingContext(agente, sessao, []))];

    [Fact]
    public async Task Grava_e_devolve_a_conversa_na_ordem()
    {
        var (agente, sessao) = await PostgresDeTeste.SessaoAsync(Guid.NewGuid());
        var provider = Provider();

        await GravarAsync(provider, agente, sessao, [new(ChatRole.User, "Quero ir a Gramado")], [new(ChatRole.Assistant, "Ótima escolha!")]);
        await GravarAsync(provider, agente, sessao, [new(ChatRole.User, "E o clima?")], [new(ChatRole.Assistant, "Frio em dezembro.")]);

        var historico = await LerAsync(Provider(), agente, sessao);

        Assert.Equal(["Quero ir a Gramado", "Ótima escolha!", "E o clima?", "Frio em dezembro."], historico.Select(m => m.Text));
        Assert.Equal([ChatRole.User, ChatRole.Assistant, ChatRole.User, ChatRole.Assistant], historico.Select(m => m.Role));
    }

    [Fact]
    public async Task Ida_e_volta_preserva_chamada_de_tool_e_aprovacao()
    {
        var (agente, sessao) = await PostgresDeTeste.SessaoAsync(Guid.NewGuid());
        var chamada = new FunctionCallContent("c1", "criar_reserva", new Dictionary<string, object?> { ["quartoId"] = 15 });
        var pedido = new ToolApprovalRequestContent("a1", chamada);

        await GravarAsync(Provider(), agente, sessao,
            [new(ChatRole.User, "Reserva")],
            [new(ChatRole.Assistant, [pedido])]);
        await GravarAsync(Provider(), agente, sessao,
            [new(ChatRole.User, [pedido.CreateResponse(true)])],
            [new(ChatRole.Assistant, [chamada]), new(ChatRole.Tool, [new FunctionResultContent("c1", "reserva 99")])]);

        var historico = await LerAsync(Provider(), agente, sessao);
        var conteudos = historico.SelectMany(m => m.Contents).ToList();

        var pedidoLido = Assert.Single(conteudos.OfType<ToolApprovalRequestContent>());
        Assert.Equal("criar_reserva", ((FunctionCallContent)pedidoLido.ToolCall).Name);
        Assert.True(Assert.Single(conteudos.OfType<ToolApprovalResponseContent>()).Approved);
        var chamadaLida = Assert.Single(conteudos.OfType<FunctionCallContent>());
        Assert.Equal(("c1", "criar_reserva"), (chamadaLida.CallId, chamadaLida.Name));
        Assert.Equal("c1", Assert.Single(conteudos.OfType<FunctionResultContent>()).CallId);
    }

    [Fact]
    public async Task Primeira_gravacao_cria_a_sessao_com_dono_e_titulo()
    {
        var id = Guid.NewGuid();
        var (agente, sessao) = await PostgresDeTeste.SessaoAsync(id);

        await GravarAsync(Provider(), agente, sessao, [new(ChatRole.User, "Quero ir a Gramado")], [new(ChatRole.Assistant, "Ok")]);

        await using var db = new HistoricoDbContext(pg.Opcoes);
        var salva = await db.Sessoes.SingleAsync(s => s.Id == id);
        Assert.Equal(_usuario, salva.UsuarioId);
        Assert.Equal("Quero ir a Gramado", salva.Titulo);
    }

    [Theory]
    [InlineData("Quero ir a Gramado", "Quero ir a Gramado")]
    [InlineData("  Quero ir\na Gramado  ", "Quero ir a Gramado")]
    public void Titulo_fica_numa_linha(string texto, string esperado) =>
        Assert.Equal(esperado, PostgresChatHistoryProvider.Titulo(texto));

    [Fact]
    public void Titulo_longo_e_cortado_em_60_caracteres_com_reticencias()
    {
        var titulo = PostgresChatHistoryProvider.Titulo("  Quero ir\r\na Gramado " + new string('x', 80) + "  ");

        Assert.Equal(61, titulo.Length);
        Assert.EndsWith("…", titulo);
        Assert.DoesNotContain("\n", titulo);
        Assert.StartsWith("Quero ir a Gramado x", titulo);
    }

    [Fact]
    public async Task Sessao_de_outro_usuario_nao_e_lida_nem_gravada()
    {
        var (agente, sessao) = await PostgresDeTeste.SessaoAsync(Guid.NewGuid());
        await GravarAsync(Provider(), agente, sessao, [new(ChatRole.User, "segredo")], [new(ChatRole.Assistant, "ok")]);
        var intruso = Provider(Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() => LerAsync(intruso, agente, sessao));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => GravarAsync(intruso, agente, sessao, [new(ChatRole.User, "x")], [new(ChatRole.Assistant, "y")]));

        Assert.Equal(2, (await LerAsync(Provider(), agente, sessao)).Count);
    }

    [Fact]
    public async Task Sessao_nova_comeca_sem_historico()
    {
        var (agente, sessao) = await PostgresDeTeste.SessaoAsync(Guid.NewGuid());

        Assert.Empty(await LerAsync(Provider(), agente, sessao));
    }

    [Fact]
    public async Task Falha_ao_gravar_explica_que_foi_o_Postgres()
    {
        var foraDoAr = HistoricoDb.Opcoes(new PostgresOptions
        {
            ConnectionString = "Host=localhost;Port=5999;Database=x;Username=x;Password=x;Timeout=2",
        });
        var provider = new PostgresChatHistoryProvider(foraDoAr, _usuario, PostgresDeTeste.Chave);
        var (agente, sessao) = await PostgresDeTeste.SessaoAsync(Guid.NewGuid());

        var erro = await Assert.ThrowsAsync<FalhaDeIntegracaoException>(
            () => GravarAsync(provider, agente, sessao, [new(ChatRole.User, "oi")], [new(ChatRole.Assistant, "olá")]));

        Assert.Contains("Postgres", erro.Message);
    }

    [Fact]
    public async Task Rodada_com_erro_nao_e_gravada()
    {
        var (agente, sessao) = await PostgresDeTeste.SessaoAsync(Guid.NewGuid());

        await Provider().InvokedAsync(new ChatHistoryProvider.InvokedContext(
            agente, sessao, [new ChatMessage(ChatRole.User, "oi")], new InvalidOperationException("modelo caiu")));

        Assert.Empty(await LerAsync(Provider(), agente, sessao));
    }
}
