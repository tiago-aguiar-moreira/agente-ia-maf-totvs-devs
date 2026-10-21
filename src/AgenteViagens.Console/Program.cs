using AgenteViagens.Agente;
using AgenteViagens.ConsoleApp;
using AgenteViagens.Infra;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .Build();

FoundryOptions foundry;
McpOptions mcp;
UsuariosOptions usuarios;
LangfuseOptions langfuse;
try
{
    foundry = Configuracao.Ler<FoundryOptions>(config, "Foundry");
    mcp = Configuracao.Ler<McpOptions>(config, "Mcp");
    usuarios = Configuracao.Ler<UsuariosOptions>(config, "Usuarios");
    langfuse = Configuracao.Ler<LangfuseOptions>(config, "Langfuse");
}
catch (FalhaDeIntegracaoException ex)
{
    Tela.Erro(AnsiConsole.Console, ex.Message);
    return 1;
}

Tela.Banner(AnsiConsole.Console, foundry.Deployment);

using var telemetria = Telemetria.Iniciar(langfuse, AgenteDeViagens.FonteDeTelemetria);

var dependencias = await Preflight.ExecutarAsync(usuarios, mcp, foundry, langfuse);
if (dependencias is null)
{
    return 1;
}

await using var conexaoMcp = dependencias.Mcp;

using var indicador = new IndicadorDePensamento(AnsiConsole.Console);
var agente = new AgenteDeViagens(
    dependencias.ChatClient, conexaoMcp.Tools, new ConsoleInteracao(AnsiConsole.Console, Console.In, indicador),
    DateOnly.FromDateTime(DateTime.Now));

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

while (true)
{
    AnsiConsole.Markup("\n[cyan]Você:[/] ");
    var linha = Console.ReadLine();

    switch (EntradaDoUsuario.Interpretar(linha))
    {
        case TipoDeEntrada.Sair: return 0;
        case TipoDeEntrada.Vazia: continue;
    }

    respostaAtual = new CancellationTokenSource();
    var rotuloEscrito = false;
    indicador.Iniciar();
    try
    {
        await foreach (var trecho in agente.ConversarAsync(linha!, respostaAtual.Token))
        {
            indicador.Parar(); // o texto chegou: sai o spinner, entra o streaming
            if (!rotuloEscrito)
            {
                AnsiConsole.Markup("\n[green]●[/] ");
                rotuloEscrito = true;
            }
            AnsiConsole.Write(new Text(trecho));
        }
    }
    catch (OperationCanceledException)
    {
        indicador.Parar();
        AnsiConsole.Markup("[grey] (resposta interrompida)[/]");
    }
    catch (Exception ex)
    {
        indicador.Parar();
        AnsiConsole.MarkupLine(
            $"\n[red]⚠  Falha ao falar com o modelo, tente de novo.[/] [grey]({Markup.Escape(ex.Message)})[/]");
    }
    finally
    {
        indicador.Parar();
        respostaAtual.Dispose();
        respostaAtual = null;
    }

    AnsiConsole.WriteLine();
}
