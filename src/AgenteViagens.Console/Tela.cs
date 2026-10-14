namespace AgenteViagens.ConsoleApp;

public static class Tela
{
    public static void Escrever(ConsoleColor cor, string texto)
    {
        var anterior = Console.ForegroundColor;
        Console.ForegroundColor = cor;
        Console.Write(texto);
        Console.ForegroundColor = anterior;
    }

    public static void Ok(string texto)
    {
        Escrever(ConsoleColor.Green, "✓ ");
        Console.WriteLine(texto);
    }

    public static void Erro(string texto)
    {
        Escrever(ConsoleColor.Red, "✗ ");
        Console.WriteLine(texto);
    }
}
