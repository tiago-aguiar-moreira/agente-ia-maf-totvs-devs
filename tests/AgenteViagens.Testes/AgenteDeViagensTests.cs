using AgenteViagens.Testes.Fakes;

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
}
