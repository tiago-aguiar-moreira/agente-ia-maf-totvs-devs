using Spectre.Console;

namespace AgenteViagens.ConsoleApp;

/// <summary>O "pensando…" enquanto o modelo não começou a responder.</summary>
public interface IIndicadorDePensamento
{
    void Iniciar();

    /// <summary>Apaga o spinner da linha. Precisa ser chamado antes de qualquer escrita ou pergunta.</summary>
    void Parar();
}

/// <summary>
/// Spinner próprio em vez de <c>AnsiConsole.Status()</c>: o Status não pode ser pausado no meio,
/// e aqui ele precisa sumir quando aparece uma linha 🔧 ou o pedido de aprovação (s/N).
/// </summary>
public sealed class IndicadorDePensamento(IAnsiConsole tela) : IIndicadorDePensamento, IDisposable
{
    private const string Texto = "pensando…";
    private static readonly Spinner Spinner = Spinner.Known.Dots;

    private readonly Lock _trava = new();
    private Timer? _timer;
    private int _quadro;

    public void Iniciar()
    {
        lock (_trava)
        {
            // Saída redirecionada (arquivo, pipe): o \r não volta ao começo da linha, então nada de animação.
            if (_timer is not null || Console.IsOutputRedirected)
            {
                return;
            }

            _quadro = 0;
            _timer = new Timer(_ => Desenhar(), null, TimeSpan.Zero, Spinner.Interval);
        }
    }

    public void Parar()
    {
        lock (_trava)
        {
            if (_timer is null)
            {
                return;
            }

            _timer.Dispose();
            _timer = null;
            tela.Write("\r" + new string(' ', Texto.Length + 3) + "\r");
        }
    }

    private void Desenhar()
    {
        lock (_trava)
        {
            if (_timer is null)
            {
                return;
            }

            var quadro = Spinner.Frames[_quadro++ % Spinner.Frames.Count];
            tela.Markup($"\r[grey]{Markup.Escape(quadro)} {Texto}[/]");
        }
    }

    public void Dispose() => Parar();
}
