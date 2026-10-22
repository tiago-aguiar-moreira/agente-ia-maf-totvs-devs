using AgenteViagens.Agente;
using AgenteViagens.Infra.Historico;
using AgenteViagens.Testes.Fakes;
using Microsoft.Extensions.AI;

namespace AgenteViagens.Testes;

/// <summary>"Fechar o console e abrir de novo": dois agentes diferentes, a mesma conversa no Postgres.</summary>
[Collection(ColecaoPostgres.Nome)]
public class SessaoPersistenteTests(PostgresDeTeste pg)
{
    private readonly Guid _usuario = Guid.NewGuid();
    private readonly Guid _sessao = Guid.NewGuid();

    private async Task<AgenteDeViagens> AbrirAsync(ChatClientFalso chat, IEnumerable<AITool>? tools = null, InteracaoFalsa? interacao = null)
    {
        var historico = new PostgresChatHistoryProvider(pg.Opcoes, _usuario, AgenteDeViagens.ChaveSessaoId);
        var agente = Montar.Agente(chat, tools, interacao, historico);
        await agente.IniciarSessaoAsync(_sessao, default);
        return agente;
    }

    private static AIFunction CriarReserva(Action aoExecutar) =>
        AIFunctionFactory.Create((int quartoId) => { aoExecutar(); return "reserva 99"; }, "criar_reserva");

    [Fact]
    public async Task Outro_processo_retoma_a_conversa_pelo_id()
    {
        var antes = await AbrirAsync(new ChatClientFalso().ResponderTexto("Gramado é ótima!"));
        await antes.ConversarTudoAsync("Quero ir a Gramado");

        var chatDepois = new ChatClientFalso().ResponderTexto("Frio em dezembro.");
        var depois = await AbrirAsync(chatDepois);
        await depois.ConversarTudoAsync("E o clima?");

        var enviado = chatDepois.TextoEnviadoAoModelo(0);
        Assert.Contains("Quero ir a Gramado", enviado);
        Assert.Contains("Gramado é ótima!", enviado);
        Assert.Contains("E o clima?", enviado);
    }

    [Fact]
    public async Task Retoma_conversa_com_reserva_aprovada_e_aceita_nova_aprovacao()
    {
        var execucoesAntes = 0;
        var antes = await AbrirAsync(
            new ChatClientFalso().ResponderChamada("c1", "criar_reserva", new() { ["quartoId"] = 15 }).ResponderTexto("Reservado!"),
            [CriarReserva(() => execucoesAntes++)], new InteracaoFalsa(true));
        await antes.ConversarTudoAsync("Reserva o quarto 15");

        var execucoesDepois = 0;
        var depois = await AbrirAsync(
            new ChatClientFalso().ResponderChamada("c2", "criar_reserva", new() { ["quartoId"] = 16 }).ResponderTexto("Reservado de novo!"),
            [CriarReserva(() => execucoesDepois++)], new InteracaoFalsa(true));
        var resposta = await depois.ConversarTudoAsync("Reserva também o 16");

        Assert.Equal(1, execucoesAntes);
        Assert.Equal(1, execucoesDepois);
        Assert.Equal("Reservado de novo!", resposta);
    }

    [Fact]
    public async Task Retomar_com_aprovacao_pendente_ignora_o_pedido_e_segue()
    {
        // O console "morreu" com o (s/N) na tela: o pedido de aprovação ficou gravado sem resposta.
        var antes = await AbrirAsync(
            new ChatClientFalso().ResponderChamada("c1", "criar_reserva", new() { ["quartoId"] = 15 }),
            [CriarReserva(() => { })],
            new InteracaoFalsa(true) { AoConfirmar = () => throw new InvalidOperationException("processo encerrado") });
        await Assert.ThrowsAsync<InvalidOperationException>(() => antes.ConversarTudoAsync("Reserva o quarto 15"));

        var execucoes = 0;
        var chatDepois = new ChatClientFalso().ResponderTexto("Olá de novo!");
        var depois = await AbrirAsync(chatDepois, [CriarReserva(() => execucoes++)], new InteracaoFalsa());
        var resposta = await depois.ConversarTudoAsync("oi");

        Assert.Equal(0, execucoes);
        Assert.Equal("Olá de novo!", resposta);
    }
}
