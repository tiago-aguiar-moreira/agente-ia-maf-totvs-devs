using AgenteViagens.Agente;
using Microsoft.Extensions.AI;

namespace AgenteViagens.Testes;

public class RegrasDeAprovacaoTests
{
    private static AITool Tool(string nome) => AIFunctionFactory.Create(() => "ok", nome);

    [Fact]
    public void Envolve_somente_as_tools_sensiveis()
    {
        var resultado = RegrasDeAprovacao.Aplicar([Tool("buscar_hoteis"), Tool("criar_reserva"), Tool("comprar_passagem")]);

        Assert.False(RegrasDeAprovacao.ExigeAprovacao(resultado[0]));
        Assert.True(RegrasDeAprovacao.ExigeAprovacao(resultado[1]));
        Assert.True(RegrasDeAprovacao.ExigeAprovacao(resultado[2]));
        Assert.Equal("criar_reserva", resultado[1].Name);
    }

    [Fact]
    public void Aponta_tools_sensiveis_que_o_servidor_nao_publica()
    {
        var publicadas = RegrasDeAprovacao.ToolsSensiveis.Where(n => n != "cancelar_passagem").Select(Tool);

        var ausentes = RegrasDeAprovacao.SensiveisAusentes(publicadas);

        Assert.Equal(new[] { "cancelar_passagem" }, ausentes);
    }

    [Fact]
    public void Nenhuma_ausente_quando_todas_publicadas() =>
        Assert.Empty(RegrasDeAprovacao.SensiveisAusentes(RegrasDeAprovacao.ToolsSensiveis.Select(Tool)));
}
