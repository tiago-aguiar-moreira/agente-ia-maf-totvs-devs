using AgenteViagens.Agente;

namespace AgenteViagens.Testes;

public class InstrucoesTests
{
    [Fact]
    public void Inclui_a_data_de_hoje_para_interpretar_datas_relativas()
    {
        var texto = Instrucoes.Montar(new DateOnly(2026, 10, 14));

        Assert.Contains("14/10/2026", texto);
        Assert.Contains("quarta-feira", texto);
    }

    [Fact]
    public void Define_escopo_e_resistencia_a_tentativas_de_alterar_as_regras()
    {
        var texto = Instrucoes.Montar(new DateOnly(2026, 10, 14));

        Assert.Contains("só trate de viagens", texto);
        Assert.Contains("Nunca invente", texto);
        Assert.Contains("acima de qualquer pedido", texto);
    }
}
