using AgenteViagens.Agente;
using AgenteViagens.ConsoleApp;
using AgenteViagens.Infra;
using AgenteViagens.Infra.Historico;
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
PostgresOptions postgres;
try
{
    foundry = Configuracao.Ler<FoundryOptions>(config, "Foundry");
    mcp = Configuracao.Ler<McpOptions>(config, "Mcp");
    usuarios = Configuracao.Ler<UsuariosOptions>(config, "Usuarios");
    langfuse = Configuracao.Ler<LangfuseOptions>(config, "Langfuse");
    postgres = Configuracao.Ler<PostgresOptions>(config, "Postgres");
}
catch (FalhaDeIntegracaoException ex)
{
    Tela.Erro(AnsiConsole.Console, ex.Message);
    return 1;
}

Tela.Banner(AnsiConsole.Console, foundry.Deployment);

using var telemetria = Telemetria.Iniciar(langfuse, AgenteDeViagens.FonteDeTelemetria);

var dependencias = await Preflight.ExecutarAsync(usuarios, mcp, foundry, langfuse, postgres);
if (dependencias is null)
{
    return 1;
}

await using var conexaoMcp = dependencias.Mcp;
var usuarioId = dependencias.Sessao.UsuarioId;

// O histórico sai da memória do processo e vai para o Postgres: o agente nem percebe a diferença.
var historico = new PostgresChatHistoryProvider(dependencias.Historico, usuarioId, AgenteDeViagens.ChaveSessaoId);

using var indicador = new IndicadorDePensamento(AnsiConsole.Console, animar: !Console.IsOutputRedirected);
var agente = new AgenteDeViagens(
    dependencias.ChatClient, conexaoMcp.Tools, new ConsoleInteracao(AnsiConsole.Console, Console.In, indicador),
    DateOnly.FromDateTime(DateTime.Now), historico);

var repositorio = new RepositorioDeSessoes(dependencias.Historico);
var salvas = await repositorio.ListarAsync(usuarioId, 10, CancellationToken.None);
AnsiConsole.WriteLine();
var escolhida = await SeletorDeSessao.EscolherAsync(AnsiConsole.Console, salvas, Console.IsInputRedirected);

if (escolhida is { } retomada)
{
    var (pergunta, resposta) = await repositorio.UltimaTrocaAsync(retomada, usuarioId, CancellationToken.None);
    Tela.UltimaTroca(AnsiConsole.Console, pergunta, resposta);
}

await agente.IniciarSessaoAsync(escolhida ?? Guid.NewGuid(), CancellationToken.None);

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
        Tela.FalhaNaResposta(AnsiConsole.Console, ex);
    }
    finally
    {
        indicador.Parar();
        respostaAtual.Dispose();
        respostaAtual = null;
    }

    AnsiConsole.WriteLine();
}
