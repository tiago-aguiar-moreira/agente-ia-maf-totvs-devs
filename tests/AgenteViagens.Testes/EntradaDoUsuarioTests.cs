using AgenteViagens.ConsoleApp;

namespace AgenteViagens.Testes;

public class EntradaDoUsuarioTests
{
    [Theory]
    [InlineData(null, TipoDeEntrada.Sair)]
    [InlineData("sair", TipoDeEntrada.Sair)]
    [InlineData("  SAIR ", TipoDeEntrada.Sair)]
    [InlineData("exit", TipoDeEntrada.Sair)]
    [InlineData("", TipoDeEntrada.Vazia)]
    [InlineData("   ", TipoDeEntrada.Vazia)]
    [InlineData("quero ir a Gramado", TipoDeEntrada.Mensagem)]
    public void Interpreta_a_linha_digitada(string? linha, TipoDeEntrada esperado) =>
        Assert.Equal(esperado, EntradaDoUsuario.Interpretar(linha));
}
