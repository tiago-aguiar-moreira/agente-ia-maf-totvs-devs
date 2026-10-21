# Sessões persistentes no Postgres e console com Spectre.Console — plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `etapa-7` deixa o console com visual de Spectre.Console sem mudar comportamento; `etapa-8` guarda o histórico das conversas no Postgres (EF Core) e permite escolher uma sessão anterior ao abrir o console.

**Architecture:** O Console ganha um `IndicadorDePensamento` (spinner) e passa a escrever via `IAnsiConsole`. Na `etapa-8`, o cliente do Foundry vira Chat Completions; um `PostgresChatHistoryProvider` (Infra) é injetado no `AgenteDeViagens` (Agente) pelo Console, e o id da sessão viaja no `StateBag` da `AgentSession` como `string`. A camada Agente continua sem conhecer EF/Npgsql.

**Tech Stack:** .NET 10 · Microsoft.Agents.AI 1.24.0 · Spectre.Console 0.57.2 (+ Spectre.Console.Testing 0.57.2 nos testes) · Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 · Testcontainers.PostgreSql 4.16.0 · xUnit · Docker `postgres:16`.

**Spec:** `docs/superpowers/specs/2026-10-21-sessoes-persistentes-e-console-design.md` (e, para o contexto geral, `docs/superpowers/specs/2026-09-29-agente-viagens-maf-design.md`).

## Global Constraints

- Branch de trabalho: `feature/sessoes-persistentes` a partir da `main` (`90735e5` ou posterior). Não mover nem recriar as tags `etapa-1`…`etapa-6`.
- `git diff etapa-6 etapa-7` contém **só** visual (Tasks 1–2). `git diff etapa-7 etapa-8` contém **só** persistência e a troca para Chat Completions (Tasks 3–7).
- O JWT nunca vai para o banco, para o histórico nem para o modelo.
- Um usuário nunca lista, lê ou grava a sessão de outro (`usuario_id` = claim `sub` do JWT).
- O texto do agente continua em streaming; `Ctrl+C` interrompe só a resposta; Ctrl+C no `(s/N)` nega tudo; só `s`/`sim` aprovam (`EntradaDoUsuario` não muda).
- O spinner nunca fica na tela enquanto algo é escrito ou perguntado.
- Camadas: Agente só referencia MAF/`Microsoft.Extensions.AI`; Infra tem Foundry, MCP, login, telemetria e Postgres; Console compõe.
- Senha do Postgres só em user-secrets (`Postgres:ConnectionString`); nada de segredo commitado.
- Textos, nomes e comentários em pt-BR, no estilo do código atual (comentários explicam o "porquê" para a plateia).
- Os 43 testes atuais continuam verdes; a suíte passa a exigir Docker em execução (Testcontainers).
- Commits terminam com `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Sessão retomada com aprovação pendente** (processo morto durante o `(s/N)`, ficou um `ToolApprovalRequestContent` sem resposta no banco): a próxima mensagem deve funcionar normalmente, com a ação pendente tratada como negada. → teste na Task 5.
2. **Postgres cai no meio da conversa:** a gravação falha com mensagem clara ("Não foi possível salvar a conversa no Postgres…"), não com "Falha ao falar com o modelo". → teste na Task 4.
3. **Primeira mensagem longa, com quebras de linha ou espaços nas pontas:** título numa linha só, ≤ 60 caracteres + "…". → teste na Task 4.
4. **Id de sessão de outro usuário** (ou inexistente) passado ao provider: nada é lido nem gravado; falha explícita. → teste na Task 4.
5. **Aprovação pedida enquanto o spinner gira:** o spinner para antes do painel e do prompt aparecerem. → teste na Task 1.

---

### Task 1: Spectre.Console no console — interação e indicador (início da `etapa-7`)

**Files:**
- Modify: `src/AgenteViagens.Console/AgenteViagens.Console.csproj` (pacote `Spectre.Console` 0.57.2)
- Create: `src/AgenteViagens.Console/IndicadorDePensamento.cs`
- Modify: `src/AgenteViagens.Console/ConsoleInteracao.cs`
- Modify: `tests/AgenteViagens.Testes/AgenteViagens.Testes.csproj` (pacote `Spectre.Console.Testing` 0.57.2)
- Test: `tests/AgenteViagens.Testes/ConsoleInteracaoTests.cs`

**Interfaces:**
- Produces:
  - `public interface IIndicadorDePensamento { void Iniciar(); void Parar(); }` — idempotentes; `Parar()` apaga o spinner da linha.
  - `public sealed class IndicadorDePensamento(IAnsiConsole tela) : IIndicadorDePensamento, IDisposable` — spinner próprio (frames de `Spinner.Known.Dots`, texto `pensando…` em cinza) desenhado com `\r` por um timer de ~80 ms, protegido por lock. *Não usar `AnsiConsole.Status()`*: ele não pode ser pausado para dar lugar a um prompt.
  - `public sealed class ConsoleInteracao(IAnsiConsole tela, TextReader entrada, IIndicadorDePensamento indicador) : IInteracaoComUsuario`

- [ ] **Step 1: Escrever os testes** (`TestConsole` do Spectre, `StringReader` como entrada, `IndicadorFalso` que registra a ordem das chamadas `Parar`/escrita):
  - `Confirmacao_mostra_tool_e_argumentos_num_painel` — `ConfirmarAcaoAsync("criar_reserva", {quartoId: 15}, …)` → saída contém `Ação sensível`, `criar_reserva` e `quartoId: 15`, e `Confirmar? (s/N)`.
  - `Confirmacao_aprova_so_com_s_ou_sim` — entradas `"s"`, `"sim"` → `true`; `""`, `"n"`, `"talvez"` → `false`.
  - `Spinner_para_antes_de_pedir_confirmacao` — o `IndicadorFalso` registra `Parar` antes de qualquer escrita no `TestConsole` (Review Focus 5).
  - `Notificacao_de_tool_mostra_nome_argumentos_e_duracao` — `NotificarChamadaDeTool("buscar_hoteis", {estado: RS}, 583 ms)` → saída contém `🔧 buscar_hoteis(estado: RS) · 583 ms`; `Parar` chamado antes.
- [ ] **Step 2:** `dotnet test --filter ConsoleInteracaoTests` → FAIL (construtor não existe).
- [ ] **Step 3: Implementar.** Painel de aprovação: `Panel` com borda arredondada amarela, cabeçalho `⚠ Ação sensível`, corpo `tool(args)`; depois `Confirmar? (s/N): ` e `entrada.ReadLine()` passado a `EntradaDoUsuario.Aprovou`. Linha 🔧 em cinza (`[grey]`). Escapar texto de argumentos com `Markup.Escape`. Formatação de argumentos igual à atual (`chave: valor, …`).
- [ ] **Step 4:** `dotnet test` → todos verdes.
- [ ] **Step 5: Commit** — `Console com Spectre: painel de aprovação, linhas de tool e indicador de pensamento`.

### Task 2: Banner, preflight com spinner e laço de conversa (fecha a `etapa-7`)

**Files:**
- Modify: `src/AgenteViagens.Console/Tela.cs`, `src/AgenteViagens.Console/Preflight.cs`, `src/AgenteViagens.Console/Program.cs`
- Modify: `README.md` (linha `etapa-7` na tabela de etapas)
- Test: `tests/AgenteViagens.Testes/TelaTests.cs`

**Interfaces:**
- Consumes: `IndicadorDePensamento`, `ConsoleInteracao(IAnsiConsole, TextReader, IIndicadorDePensamento)` (Task 1).
- Produces:
  - `public static class Tela` com `void Banner(IAnsiConsole tela, string modelo)`, `void Ok(IAnsiConsole tela, string texto)`, `void Erro(IAnsiConsole tela, string texto)`.
  - `Preflight.Passo` passa a receber o texto "em andamento" (ex.: `"Fazendo login…"`) e mostra spinner (`AnsiConsole.Status`, aqui permitido: não há prompt durante o preflight) até virar `✓ resumo` ou `✗ erro`.

- [ ] **Step 1: Testes** (`TestConsole`):
  - `Banner_mostra_nome_modelo_e_atalhos` — contém `Agente de Viagens`, `Microsoft Agent Framework`, o modelo passado (`gpt-4.1`), `sair` e `Ctrl+C`.
  - `Ok_e_Erro_usam_os_simbolos_do_preflight` — `✓ texto` / `✗ texto`; texto com `[colchetes]` sai literal (escape).
- [ ] **Step 2:** rodar → FAIL.
- [ ] **Step 3: Implementar.**
  - Banner no estilo do Claude Code: `Panel` de borda arredondada com `✈  Agente de Viagens`, linha `Microsoft Agent Framework · <modelo> · MCP`, linha `sair para encerrar · Ctrl+C interrompe a resposta`. O modelo vem de `foundry.Deployment`.
  - `Program`: `IndicadorDePensamento.Iniciar()` logo após a mensagem do usuário; ao receber o **primeiro trecho de texto** de uma rodada, `Parar()` e escrever o rótulo do agente (`●` verde) uma vez, depois os trechos direto (streaming); quando `ConsoleInteracao` notificar uma tool, ele mesmo para o spinner — depois da linha 🔧, `Program` religa o spinner até o próximo trecho. `finally` sempre chama `Parar()`. Prompt do usuário: `Você:` em ciano (texto igual ao de hoje).
  - Ctrl+C, `(resposta interrompida)` e mensagem de falha mantêm o comportamento atual.
- [ ] **Step 4:** `dotnet test` verde; rodar `dotnet run --project src/AgenteViagens.Console` num terminal com o ambiente no ar e conferir: banner, preflight com spinner, spinner até o 1º trecho, 🔧, `(s/N)` com `n` e com `s`, Ctrl+C durante a resposta.
- [ ] **Step 5: Commit** — `Etapa 7: banner, preflight com spinner e respostas com Spectre.Console`; README com a linha `etapa-7 | Console com Spectre.Console (visual)`; `git tag etapa-7`.

### Task 3: Id do usuário a partir do JWT do login

**Files:**
- Modify: `src/AgenteViagens.Infra/UsuariosClient.cs`
- Modify: `tests/AgenteViagens.Testes/UsuariosClientTests.cs` (tokens dos testes passam a ser JWTs bem formados)

**Interfaces:**
- Produces: `public sealed record SessaoDoUsuario(string Email, string Token, DateTime ExpiraEm, Guid UsuarioId)`.

- [ ] **Step 1: Testes:**
  - `Login_extrai_o_usuario_do_claim_sub` — token `header.{"sub":"a1b2c3d4-…"}.assinatura` (base64url, sem padding) → `UsuarioId == Guid` do `sub`.
  - `Token_sem_sub_valido_explica_o_problema` — payload sem `sub` ou com `sub` que não é Guid → `FalhaDeIntegracaoException` com `sub` na mensagem.
  - Atualizar `Login_com_sucesso_devolve_token_e_expiracao` para usar um JWT válido.
- [ ] **Step 2:** rodar → FAIL.
- [ ] **Step 3:** decodificar o payload (base64url → JSON → `sub`) sem validar assinatura — o token acabou de vir do `Usuarios.Api`, e quem valida é o servidor MCP. Comentário dizendo isso.
- [ ] **Step 4:** `dotnet test` verde.
- [ ] **Step 5: Commit** — `Login expõe o id do usuário (claim sub) para identificar o dono das sessões`.

### Task 4: Histórico no Postgres — DbContext, provider e repositório

**Files:**
- Modify: `src/AgenteViagens.Infra/AgenteViagens.Infra.csproj` (`Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3)
- Create: `src/AgenteViagens.Infra/Historico/HistoricoDbContext.cs` (entidades `SessaoSalva`, `MensagemSalva`)
- Create: `src/AgenteViagens.Infra/Historico/PostgresChatHistoryProvider.cs`
- Create: `src/AgenteViagens.Infra/Historico/RepositorioDeSessoes.cs`
- Modify: `src/AgenteViagens.Infra/Opcoes.cs` (`PostgresOptions`)
- Modify: `tests/AgenteViagens.Testes/AgenteViagens.Testes.csproj` (`Testcontainers.PostgreSql` 4.16.0)
- Create: `tests/AgenteViagens.Testes/Fakes/PostgresDeTeste.cs` (fixture xUnit com `PostgreSqlContainer` de `postgres:16`, `EnsureCreated` uma vez, cada teste com `usuarioId` próprio)
- Test: `tests/AgenteViagens.Testes/PostgresChatHistoryProviderTests.cs`, `tests/AgenteViagens.Testes/RepositorioDeSessoesTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class PostgresOptions { public string ConnectionString { get; init; } = ""; }`
  - `HistoricoDbContext(DbContextOptions<HistoricoDbContext>)` com `DbSet<SessaoSalva> Sessoes`, `DbSet<MensagemSalva> Mensagens`. Mapeamento explícito para as tabelas/colunas da spec: `sessoes(id, usuario_id [índice], titulo varchar(80), criada_em, atualizada_em)` e `mensagens(id bigint identidade, sessao_id FK cascade, papel varchar(20), conteudo jsonb, criada_em)`. `conteudo` é `string` com `HasColumnType("jsonb")`.
  - `public static class HistoricoDb { public static DbContextOptions<HistoricoDbContext> Opcoes(PostgresOptions o); public static Task<int> PrepararAsync(DbContextOptions<HistoricoDbContext> opcoes, Guid usuarioId, CancellationToken ct); }` — `EnsureCreatedAsync` + contagem de sessões do usuário; Postgres fora do ar → `FalhaDeIntegracaoException("Postgres fora do ar … Suba com `docker compose up -d` na raiz deste repositório.")`.
  - `public sealed class PostgresChatHistoryProvider(DbContextOptions<HistoricoDbContext> opcoes, Guid usuarioId, string chaveSessaoId) : ChatHistoryProvider`
    - lê o id da sessão de `session.StateBag` na chave `chaveSessaoId` (valor `string` com um Guid); sem id → `InvalidOperationException`.
    - `ProvideChatHistoryAsync`: mensagens da sessão por `id` crescente, desserializando `conteudo` com `AIJsonUtilities.DefaultOptions`; sessão inexistente → lista vazia; sessão de outro usuário → `InvalidOperationException` (Review Focus 4).
    - `StoreChatHistoryAsync`: na primeira gravação cria a linha de `sessoes` (dono = `usuarioId`, título = `Titulo(primeira mensagem de usuário)`); grava request + response em ordem com `papel` = `ChatRole.Value`; atualiza `atualizada_em`. Ignora a gravação quando `context.InvokeException` não é nulo. Falha de banco → `FalhaDeIntegracaoException("Não foi possível salvar a conversa no Postgres: …")` (Review Focus 2).
    - `StateKeys` = `[chaveSessaoId]`.
    - `public static string Titulo(string texto)` — colapsa espaços/quebras de linha em um espaço, `Trim`, corta em 60 caracteres + `…` quando passar.
  - `public sealed record ResumoDeSessao(Guid Id, string Titulo, DateTimeOffset AtualizadaEm);`
  - `public sealed class RepositorioDeSessoes(DbContextOptions<HistoricoDbContext> opcoes)`:
    - `Task<IReadOnlyList<ResumoDeSessao>> ListarAsync(Guid usuarioId, int limite, CancellationToken ct)` — só do usuário, `atualizada_em` desc.
    - `Task<(string? Pergunta, string? Resposta)> UltimaTrocaAsync(Guid sessaoId, Guid usuarioId, CancellationToken ct)` — último texto de `user` e último texto de `assistant` (mensagens só com tool call/result são puladas).

- [ ] **Step 1: Testes do provider** (montam `InvokingContext`/`InvokedContext` com uma `AgentSession` cujo `StateBag` tem o id):
  - `Grava_e_devolve_a_conversa_na_ordem` — 2 rodadas (texto) → `Provide` devolve as 4 mensagens na ordem, com papéis certos.
  - `Ida_e_volta_preserva_chamada_de_tool_e_aprovacao` — mensagens com `FunctionCallContent`, `FunctionResultContent`, `ToolApprovalRequestContent` e a resposta de aprovação voltam com os mesmos tipos, `CallId` e nome.
  - `Primeira_gravacao_cria_a_sessao_com_dono_e_titulo`.
  - `Titulo_fica_numa_linha_com_no_maximo_60_caracteres` — `"  Quero ir\na Gramado …(80 chars)  "` → sem `\n`, 61 caracteres terminando em `…`; texto curto fica igual (Review Focus 3).
  - `Sessao_de_outro_usuario_nao_e_lida_nem_gravada` (Review Focus 4).
  - `Falha_ao_gravar_explica_que_foi_o_Postgres` — opções apontando para porta sem servidor → `FalhaDeIntegracaoException` com `Postgres` (Review Focus 2).
  - `Rodada_com_erro_nao_e_gravada` — `InvokedContext` com `InvokeException` → nada gravado.
- [ ] **Step 2: Testes do repositório:**
  - `Lista_so_as_sessoes_do_usuario_mais_recentes_primeiro_ate_o_limite` — 12 sessões do usuário + 1 de outro, limite 10 → 10 itens, ordem desc, nenhum do outro.
  - `Ultima_troca_pula_mensagens_de_tool` — conversa terminando em `user → assistant(call) → tool → assistant(texto)` → devolve o último texto de cada papel.
  - `Preparar_cria_as_tabelas_e_conta_as_sessoes`.
- [ ] **Step 3:** rodar → FAIL.
- [ ] **Step 4: Implementar** conforme as interfaces.
- [ ] **Step 5:** `dotnet test` verde (Docker no ar).
- [ ] **Step 6: Commit** — `Histórico das conversas no Postgres com EF Core: provider do MAF e repositório de sessões`.

### Task 5: Agente inicia ou retoma uma sessão pelo id

**Files:**
- Modify: `src/AgenteViagens.Agente/AgenteDeViagens.cs`
- Modify: `tests/AgenteViagens.Testes/Montar.cs`
- Test: `tests/AgenteViagens.Testes/AgenteDeViagensTests.cs`, `tests/AgenteViagens.Testes/SessaoPersistenteTests.cs`

**Interfaces:**
- Consumes: `PostgresChatHistoryProvider`, `HistoricoDbContext`, fixture `PostgresDeTeste` (Task 4).
- Produces:
  - `public const string ChaveSessaoId = "AgenteViagens.SessaoId";`
  - construtor: `AgenteDeViagens(IChatClient chatClient, IEnumerable<AITool> tools, IInteracaoComUsuario interacao, DateOnly hoje, ChatHistoryProvider? historico = null)` — quando não nulo, vai em `ChatClientAgentOptions.ChatHistoryProvider`.
  - `public Task IniciarSessaoAsync(Guid sessaoId, CancellationToken ct)` — cria a `AgentSession`, grava `sessaoId.ToString()` no `StateBag` (`ChaveSessaoId`) e fecha uma aprovação pendente deixada no histórico (ver Step 1, último teste). Sem chamar este método, `ConversarAsync` continua criando uma sessão nova como hoje.

- [ ] **Step 1: Testes:**
  - `Sem_provider_continua_lembrando_na_memoria` — o teste atual `Lembra_a_conversa_anterior_na_mesma_sessao` continua verde sem mudança.
  - (`SessaoPersistenteTests`, Postgres) `Outro_processo_retoma_a_conversa_pelo_id` — agente A (com provider) conversa "Quero ir a Gramado"; agente B **novo**, mesmo `ChatClientFalso`-tipo e provider novo, `IniciarSessaoAsync(mesmoId)`, pergunta "E o clima?" → a mensagem enviada ao modelo na chamada de B contém "Quero ir a Gramado".
  - `Retoma_conversa_com_reserva_aprovada_e_aceita_nova_aprovacao` — A: chamada `criar_reserva` aprovada (`InteracaoFalsa(true)`) + resposta; B retoma, nova chamada `criar_reserva` aprovada → tool executada 1× em B, sem exceção.
  - `Retomar_com_aprovacao_pendente_nega_e_segue` — histórico gravado termina num `ToolApprovalRequestContent` sem resposta (A foi "morto" antes do `(s/N)`); B retoma e envia "oi" → sem exceção, tool **não** executada, resposta normal (Review Focus 1).
- [ ] **Step 2:** rodar → FAIL.
- [ ] **Step 3: Implementar.** Para a aprovação pendente: ao iniciar a sessão, ler o histórico pelo provider (`ChatHistoryProvider` exposto por `_agente.GetService<ChatHistoryProvider>()` ou guardado no campo) e, se a última mensagem do assistente tiver `ToolApprovalRequestContent` sem resposta posterior, reutilizar `FecharRodadaInterrompidaAsync` com esses pedidos.
- [ ] **Step 4:** `dotnet test` verde.
- [ ] **Step 5: Commit** — `Agente inicia ou retoma uma sessão persistida pelo id`.

### Task 6: Console — Chat Completions, Postgres no preflight e escolha da sessão (fecha a `etapa-8`)

**Files:**
- Create: `docker-compose.yml` (raiz): serviço `postgres` (`postgres:16`, porta `5432:5432`, volume `agente-viagens-pg`, `POSTGRES_DB=agente_viagens`, usuário/senha via `${POSTGRES_PASSWORD}` com `.env` ignorado pelo git) + `.env.example`; `.gitignore` com `.env`.
- Modify: `src/AgenteViagens.Infra/FoundryChatClientFactory.cs` (Chat Completions: `GetProjectOpenAIClient().GetChatClient(deployment).AsIChatClient()`, comentário explicando o porquê)
- Modify: `src/AgenteViagens.Console/Preflight.cs` (passo `Postgres — N sessões salvas`; `Dependencias` ganha `DbContextOptions<HistoricoDbContext> Historico`)
- Create: `src/AgenteViagens.Console/SeletorDeSessao.cs`
- Modify: `src/AgenteViagens.Console/Program.cs`
- Test: `tests/AgenteViagens.Testes/SeletorDeSessaoTests.cs`, `tests/AgenteViagens.Testes/ConfiguracaoTests.cs`

**Interfaces:**
- Consumes: Tasks 3–5.
- Produces:
  - `public static class SeletorDeSessao`:
    - `public static string Rotulo(ResumoDeSessao s)` → `dd/MM HH:mm · título` (hora local).
    - `public static Task<Guid?> EscolherAsync(IAnsiConsole tela, IReadOnlyList<ResumoDeSessao> sessoes, bool entradaRedirecionada)` → `null` = nova conversa. `entradaRedirecionada` ou lista vazia → `null` sem perguntar. Senão `SelectionPrompt` com `➕ Nova conversa` primeiro e as sessões.
  - Em `Program`: `PostgresOptions` lido de `Postgres`; após o preflight, `ListarAsync(usuarioId, 10)` → `EscolherAsync(…, Console.IsInputRedirected)`; nova conversa → `Guid.NewGuid()`; retomada → mostrar a última troca (painel cinza `Você: …` / `Agente: …`) antes do primeiro prompt; `agente.IniciarSessaoAsync(id)`.

- [ ] **Step 1: Testes:**
  - `Rotulo_mostra_data_hora_e_titulo`.
  - `Entrada_redirecionada_comeca_conversa_nova_sem_perguntar` e `Sem_sessoes_comeca_conversa_nova`.
  - `Seleciona_sessao_existente` — `TestConsole` com teclas (seta para baixo + Enter) → devolve o id da 1ª sessão.
  - `ConfiguracaoTests`: `Postgres:ConnectionString` ausente → mensagem cita a chave.
- [ ] **Step 2:** rodar → FAIL.
- [ ] **Step 3: Implementar.** Mensagem de falha do preflight para o Postgres fora do ar vem de `HistoricoDb.PrepararAsync` (Task 4).
- [ ] **Step 4:** `dotnet test` verde. Subir `docker compose up -d`, gravar `Postgres:ConnectionString` nos user-secrets e fazer o ensaio real por pipe (fallback) e, se possível, num terminal:
  1. conversa "Quero ir a Gramado de 12 a 14 de dezembro com minha esposa. Que hotéis vocês têm?" → `sair`;
  2. reabrir → a sessão aparece na lista com esse título → escolher → aparece a última troca → "E como vai estar o clima nesses dias?" responde sobre Gramado em 12–14/12 sem perguntar o destino;
  3. `docker compose stop postgres` → preflight `✗ Postgres fora do ar …`.
  Conferir também `SELECT papel, conteudo FROM mensagens` (via `docker compose exec postgres psql`) e que nenhum `conteudo` contém `Bearer` ou `eyJ`.
- [ ] **Step 5: Commit** — `Etapa 8: sessões persistentes no Postgres e escolha da conversa ao iniciar`.

### Task 7: Documentação e tag `etapa-8`

**Files:**
- Modify: `README.md` (pré-requisito Docker/Postgres deste repo, `Postgres:ConnectionString` nos user-secrets, linha `etapa-8 | Sessões persistentes no Postgres (EF Core)`, testes exigem Docker)
- Modify: `docs/roteiro-demo.md` (T-30: `docker compose up -d` também neste repo; nova seção "Sessões persistentes" com o passo a passo do ensaio da Task 6; trocar "para a lista começar limpa" por "cancelar só a reserva criada no ensaio — o seed já cria reservas para a Marina"; "Se algo falhar": `✗ Postgres` → `docker compose up -d` na raiz deste repo)
- Modify: `docs/slides-esboco.md` (registrar as etapas 7 e 8 no fechamento; revisão dos slides fica para depois, conforme a spec)

- [ ] **Step 1:** atualizar os três arquivos.
- [ ] **Step 2:** `dotnet test` verde.
- [ ] **Step 3: Commit** — `Docs: Postgres, sessões persistentes e etapas 7 e 8`; `git tag etapa-8` neste commit.
