using AgenteViagens.Agente;
using AgenteViagens.ConsoleApp;
using AgenteViagens.Infra;
using Microsoft.Extensions.Configuration;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .Build();

Tela.Escrever(ConsoleColor.Cyan, "✈  Agente de Viagens — Microsoft Agent Framework\n\n");

FoundryOptions foundry;
McpOptions mcp;
UsuariosOptions usuarios;
try
{
    foundry = Configuracao.Ler<FoundryOptions>(config, "Foundry");
    mcp = Configuracao.Ler<McpOptions>(config, "Mcp");
    usuarios = Configuracao.Ler<UsuariosOptions>(config, "Usuarios");
}
catch (FalhaDeIntegracaoException ex)
{
    Tela.Erro(ex.Message);
    return 1;
}

var dependencias = await Preflight.ExecutarAsync(usuarios, mcp, foundry);
if (dependencias is null)
{
    return 1;
}

await using var conexaoMcp = dependencias.Mcp;

var agente = new AgenteDeViagens(
    dependencias.ChatClient, conexaoMcp.Tools, new ConsoleInteracao(), DateOnly.FromDateTime(DateTime.Now));

// Ctrl+C durante uma resposta interrompe só a resposta, não o programa.
CancellationTokenSource? respostaAtual = null;
Console.CancelKeyPress += (_, e) =>
{
    if (respostaAtual is { } cts)
    {
        e.Cancel = true;
        cts.Cancel();
    }
};

Console.WriteLine("\nDigite sua mensagem (ou \"sair\").");

while (true)
{
    Tela.Escrever(ConsoleColor.Cyan, "\nVocê: ");
    var linha = Console.ReadLine();

    switch (EntradaDoUsuario.Interpretar(linha))
    {
        case TipoDeEntrada.Sair: return 0;
        case TipoDeEntrada.Vazia: continue;
    }

    Tela.Escrever(ConsoleColor.Green, "Agente: ");
    respostaAtual = new CancellationTokenSource();
    try
    {
        await foreach (var trecho in agente.ConversarAsync(linha!, respostaAtual.Token))
        {
            Console.Write(trecho);
        }
    }
    catch (OperationCanceledException)
    {
        Tela.Escrever(ConsoleColor.DarkGray, " (resposta interrompida)");
    }
    catch (Exception ex)
    {
        Tela.Escrever(ConsoleColor.Red, $"\n⚠  Falha ao falar com o modelo, tente de novo. ({ex.Message})");
    }
    finally
    {
        respostaAtual.Dispose();
        respostaAtual = null;
    }

    Console.WriteLine();
}
