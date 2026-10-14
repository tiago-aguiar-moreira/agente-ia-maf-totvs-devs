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
}
