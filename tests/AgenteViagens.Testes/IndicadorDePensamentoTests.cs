using AgenteViagens.ConsoleApp;
using Spectre.Console;

namespace AgenteViagens.Testes;

public class IndicadorDePensamentoTests
{
    [Fact]
    public async Task Spinner_redesenha_na_mesma_linha_sem_quebras()
    {
        var saida = new StringWriter();
        var tela = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes, ColorSystem = ColorSystemSupport.TrueColor, Out = new AnsiConsoleOutput(saida),
        });
        using var indicador = new IndicadorDePensamento(tela, animar: true);

        indicador.Iniciar();
        await Task.Delay(300); // alguns quadros
        indicador.Parar();

        Assert.Contains("pensando", saida.ToString());
        Assert.DoesNotContain("\n", saida.ToString());
    }

    [Fact]
    public void Sem_animacao_nao_escreve_nada()
    {
        var saida = new StringWriter();
        var tela = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(saida) });
        using var indicador = new IndicadorDePensamento(tela, animar: false);

        indicador.Iniciar();
        indicador.Parar();

        Assert.Equal("", saida.ToString());
    }
}
