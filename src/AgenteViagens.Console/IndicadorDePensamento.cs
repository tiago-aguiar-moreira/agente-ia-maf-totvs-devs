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
/// <param name="animar">Falso com a saída redirecionada (arquivo, pipe), onde o spinner só faria sujeira.</param>
public sealed class IndicadorDePensamento(IAnsiConsole tela, bool animar) : IIndicadorDePensamento, IDisposable
{
    private const string Texto = "pensando…";

    // Volta ao começo da linha e apaga. Vai cru (WriteAnsi): o Spectre transformaria um "\r" sozinho em quebra de linha.
    private const string LimparLinha = "\r\u001b[2K";
    private static readonly Spinner Spinner = Spinner.Known.Dots;

    private readonly Lock _trava = new();
    private Timer? _timer;
    private int _quadro;

    public void Iniciar()
    {
        lock (_trava)
        {
            if (_timer is not null || !animar)
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
            tela.WriteAnsi(LimparLinha);
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
            tela.WriteAnsi(LimparLinha);
            tela.Markup($"[grey]{Markup.Escape(quadro)} {Texto}[/]");
        }
    }

    public void Dispose() => Parar();
}
