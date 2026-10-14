using AgenteViagens.Testes.Fakes;
using Microsoft.Extensions.AI;

namespace AgenteViagens.Testes;

public class AgenteDeViagensTests
{
    [Fact]
    public async Task Devolve_o_texto_do_modelo()
    {
        var chat = new ChatClientFalso().ResponderTexto("Olá! Para onde vamos?");
        var agente = Montar.Agente(chat);

        var resposta = await agente.ConversarTudoAsync("oi");

        Assert.Equal("Olá! Para onde vamos?", resposta);
    }

    [Fact]
    public async Task Envia_o_system_prompt_e_a_mensagem_ao_modelo()
    {
        var chat = new ChatClientFalso().ResponderTexto("ok");
        var agente = Montar.Agente(chat);

        await agente.ConversarTudoAsync("quero viajar");

        var enviado = chat.TextoEnviadoAoModelo(0);
        Assert.Contains("agente de viagens", enviado);
        Assert.Contains("quero viajar", enviado);
    }

    [Fact]
    public async Task Executa_tool_pedida_pelo_modelo_e_devolve_a_resposta_final()
    {
        var hoteisBuscados = 0;
        var buscarHoteis = AIFunctionFactory.Create((string estado) => { hoteisBuscados++; return "Chalé das Hortênsias"; }, "buscar_hoteis");
        var chat = new ChatClientFalso()
            .ResponderChamada("c1", "buscar_hoteis", new() { ["estado"] = "RS" })
            .ResponderTexto("Encontrei o Chalé das Hortênsias.");
        var agente = Montar.Agente(chat, [buscarHoteis]);

        var resposta = await agente.ConversarTudoAsync("hotéis no RS?");

        Assert.Equal(1, hoteisBuscados);
        Assert.Equal("Encontrei o Chalé das Hortênsias.", resposta);
    }

    [Fact]
    public async Task Lembra_a_conversa_anterior_na_mesma_sessao()
    {
        var chat = new ChatClientFalso()
            .ResponderTexto("Gramado é ótima em dezembro!")
            .ResponderTexto("Sim, tem Natal Luz.");
        var agente = Montar.Agente(chat);

        await agente.ConversarTudoAsync("Quero ir a Gramado");
        await agente.ConversarTudoAsync("Tem algum evento lá?");

        var segundaChamada = chat.TextoEnviadoAoModelo(1);
        Assert.Contains("Quero ir a Gramado", segundaChamada);
        Assert.Contains("Gramado é ótima em dezembro!", segundaChamada);
    }

    private static (AIFunction Funcao, Func<int> Chamadas) ToolContada(string nome)
    {
        var chamadas = 0;
        return (AIFunctionFactory.Create(() => { chamadas++; return "feito"; }, nome), () => chamadas);
    }

    [Fact]
    public async Task Tool_sensivel_aprovada_e_executada()
    {
        var (criarReserva, chamadas) = ToolContada("criar_reserva");
        var chat = new ChatClientFalso()
            .ResponderChamada("c1", "criar_reserva", new() { ["quarto_id"] = 12 })
            .ResponderTexto("Reserva confirmada!");
        var interacao = new InteracaoFalsa(true);
        var agente = Montar.Agente(chat, [criarReserva], interacao);

        var resposta = await agente.ConversarTudoAsync("reserva o quarto 12");

        Assert.Equal(new[] { "criar_reserva" }, interacao.ConfirmacoesPedidas);
        Assert.Equal(1, chamadas());
        Assert.Contains("Reserva confirmada!", resposta);
    }

    [Fact]
    public async Task Tool_sensivel_negada_nao_e_executada()
    {
        var (criarReserva, chamadas) = ToolContada("criar_reserva");
        var chat = new ChatClientFalso()
            .ResponderChamada("c1", "criar_reserva")
            .ResponderTexto("Tudo bem, não reservei.");
        var agente = Montar.Agente(chat, [criarReserva], new InteracaoFalsa(false));

        var resposta = await agente.ConversarTudoAsync("reserva");

        Assert.Equal(0, chamadas());
        Assert.Contains("não reservei", resposta);
    }

    [Fact]
    public async Task Duas_acoes_sensiveis_na_mesma_resposta_pedem_confirmacao_separada()
    {
        var (criarReserva, reservas) = ToolContada("criar_reserva");
        var (comprarPassagem, passagens) = ToolContada("comprar_passagem");
        var chat = new ChatClientFalso()
            .Responder(new FunctionCallContent("c1", "criar_reserva", new Dictionary<string, object?>()),
                       new FunctionCallContent("c2", "comprar_passagem", new Dictionary<string, object?>()))
            .ResponderTexto("Reservei o hotel; a passagem ficou de fora.");
        var interacao = new InteracaoFalsa(true, false);
        var agente = Montar.Agente(chat, [criarReserva, comprarPassagem], interacao);

        await agente.ConversarTudoAsync("reserva hotel e compra passagem");

        Assert.Equal(new[] { "criar_reserva", "comprar_passagem" }, interacao.ConfirmacoesPedidas);
        Assert.Equal(1, reservas());
        Assert.Equal(0, passagens());
    }

    [Fact]
    public async Task Tool_de_leitura_nao_pede_confirmacao()
    {
        var (buscarHoteis, chamadas) = ToolContada("buscar_hoteis");
        var chat = new ChatClientFalso().ResponderChamada("c1", "buscar_hoteis").ResponderTexto("ok");
        var interacao = new InteracaoFalsa();
        var agente = Montar.Agente(chat, [buscarHoteis], interacao);

        await agente.ConversarTudoAsync("hotéis?");

        Assert.Empty(interacao.ConfirmacoesPedidas);
        Assert.Equal(1, chamadas());
    }

    [Fact]
    public async Task Recusa_chega_ao_modelo_como_decisao_do_usuario()
    {
        var (criarReserva, _) = ToolContada("criar_reserva");
        var chat = new ChatClientFalso()
            .ResponderChamada("c1", "criar_reserva")
            .ResponderTexto("Tudo bem, não reservei.");
        var agente = Montar.Agente(chat, [criarReserva], new InteracaoFalsa(false));

        await agente.ConversarTudoAsync("reserva");

        var resultado = chat.MensagensRecebidas[1]
            .SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single();
        Assert.Contains("usuário recusou", resultado.Result?.ToString());
    }
}
