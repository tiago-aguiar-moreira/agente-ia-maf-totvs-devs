# Sessões persistentes no Postgres e console com Spectre.Console — design

Continuação de `2026-09-29-agente-viagens-maf-design.md`. Duas etapas novas na palestra
"Como criar um agente de IA em .NET com o Microsoft Agent Framework": **`etapa-7`** (visual) e
**`etapa-8`** (persistência das sessões).

## Por quê

Na explicação do código, o Tiago apresenta a `AgentSession` e diz que ela vive na memória do
processo: fechou o console, perdeu a conversa. O impacto é explicado com a analogia **cache em
memória (`IMemoryCache`) × cache distribuído (Redis)**. Em seguida ele mostra que o MAF permite
tirar o histórico do processo e guardá-lo num banco, sem mexer na lógica do agente.

A prova na tela: conversar, digitar `sair`, abrir o console de novo, escolher a sessão anterior
numa lista e continuar a conversa ("E o clima nesses dias?" → o agente sabe que é Gramado em
dezembro).

O visual novo (inspirado no Claude Code) deixa o console com cara mais caprichada. O Tiago vai
**rodar a versão completa (`etapa-8`) desde o início da demo**; as etapas continuam existindo para
organizar o código e permitir `git diff` limpo entre elas. Os slides serão revisados depois,
noutra tarefa.

## Fato técnico que molda o design

Testado em 2026-10-21 contra o Foundry deste projeto:

- Com a **Responses API** (cliente atual, `GetProjectResponsesClient()`), o histórico fica no
  Foundry e a sessão local guarda só o id `resp_...`. Ligar um `ChatHistoryProvider` nesse modo
  falha na primeira resposta: `InvalidOperationException: Only ConversationId or
  ChatHistoryProvider may be used, but not both.`
- Com **Chat Completions** (`GetProjectOpenAIClient().GetChatClient(deployment).AsIChatClient()`),
  o provider é chamado: `ProvideChatHistoryAsync` antes de cada chamada ao modelo e
  `StoreChatHistoryAsync` depois, e o modelo lembra do turno anterior.
- Um tipo de estado próprio guardado na sessão por `ProviderSessionState<T>` quebrou
  `SerializeSessionAsync` (`JsonTypeInfo metadata ... was not provided`). Quem implementar deve
  guardar na sessão só tipos que o serializador do MAF conhece, ou registrar o tipo.

Portanto a `etapa-8` troca o cliente do Foundry para Chat Completions, **no mesmo diff do
provider**, porque a troca faz parte da explicação ("para o histórico ser do cliente, a API não
pode guardar estado"). As etapas 1 a 7 continuam com a Responses API.

## Metas

1. **`etapa-7` — visual com Spectre.Console**, sem mudar comportamento:
   - banner de boas-vindas;
   - preflight como checklist (✓/✗) com spinner enquanto cada passo roda;
   - spinner ("pensando…") enquanto o agente não começou a responder;
   - painéis/cores para a resposta do agente, as linhas 🔧 das tools e o pedido de aprovação `(s/N)`.
2. **`etapa-8` — sessões persistentes no Postgres:**
   - Postgres em container, num `docker-compose.yml` **deste repositório** (só o Postgres);
   - um `ChatHistoryProvider` próprio que grava e lê as mensagens no Postgres;
   - cada sessão pertence ao usuário logado (o `sub` do JWT);
   - ao iniciar, depois do preflight, uma lista para escolher **"Nova conversa"** ou uma das
     sessões anteriores do usuário;
   - ao retomar, a conversa continua com o contexto anterior;
   - novo passo no preflight: `✓ Postgres — N sessões salvas`.
3. Tags `etapa-7` e `etapa-8`; README, roteiro da demo e provisionamento atualizados.

## Não-metas

- Não reescrever as etapas 1 a 6 nem mover as tags existentes.
- Não revisar os slides agora (tarefa posterior). Só registrar no esboço que existem as etapas 7 e 8.
- Não apagar, renomear ou pesquisar sessões pelo console.
- Não usar reducer / limite de mensagens.
- Não agrupar traces por sessão no Langfuse.
- Não usar framework de migrações; o schema é criado na inicialização (`CREATE TABLE IF NOT EXISTS`).
- Não adicionar Postgres ao repositório do servidor MCP.

## Anti-metas (falharia mesmo funcionando)

- **O JWT nunca vai para o banco** nem para o histórico. A regra "o token nunca passa pelo modelo"
  continua valendo.
- Um usuário **nunca** vê nem retoma a sessão de outro.
- A demo não pode ganhar passos manuais frágeis: subir o Postgres é um `docker compose up -d`, e
  o preflight acusa claramente se ele estiver fora.
- O visual não pode atrapalhar a demo: o texto do agente continua aparecendo **em streaming**
  (trecho a trecho); `Ctrl+C` continua interrompendo só a resposta; Ctrl+C no `(s/N)` continua
  negando tudo; o spinner nunca fica por cima de um prompt.
- As camadas continuam como estão: **Agente** só conhece abstrações do MAF/`Microsoft.Extensions.AI`;
  **Infra** tem Foundry, MCP, login, telemetria e agora Postgres; **Console** compõe tudo.

## Restrições

- .NET 10, MAF `Microsoft.Agents.AI` 1.24.0, `Microsoft.Agents.AI.Foundry` 1.24.0-preview.261006.1
  (já em uso).
- Imagem `postgres:16` (já está no cache local do Docker).
- Os 43 testes atuais continuam passando; código novo vem com teste (TDD, como no resto do projeto).
- Segredos (senha do Postgres) em user-secrets, como o resto; nada de senha real commitada.
- Comentários e textos em pt-BR, no estilo do código atual (classes curtas, comentários que
  explicam o "porquê" para a plateia).

## Como fica — decisões já tomadas

### `etapa-7`

- Pacote `Spectre.Console` no projeto Console. `Tela` e `ConsoleInteracao` passam a usá-lo;
  `IInteracaoComUsuario` (porta da camada Agente) não muda.
- Regras de entrada (`EntradaDoUsuario`: `sair`, vazio, `s`/`sim`) não mudam.

### `etapa-8`

- **Cliente:** `FoundryChatClientFactory.Criar` passa a usar Chat Completions.
- **Banco:** `docker-compose.yml` na raiz com o serviço `postgres` (porta 5432, volume nomeado).
  Configuração `Postgres:ConnectionString` (com a senha) em user-secrets.
- **Acesso a dados:** `Npgsql` direto, sem EF Core — menos conceitos na tela, e o SQL do provider
  fica visível no slide. *(Decisão do Claude.)*
- **Schema** *(decisão do Claude)*:
  - `sessoes (id uuid PK, usuario_id uuid, titulo text, criada_em timestamptz, atualizada_em timestamptz)`
  - `mensagens (id bigserial PK, sessao_id uuid FK → sessoes, conteudo jsonb, criada_em timestamptz)`
  - `conteudo` é o `ChatMessage` serializado com `AIJsonUtilities.DefaultOptions`, para que
    chamadas e resultados de tools e pedidos/respostas de aprovação façam o caminho de ida e volta.
- **Provider:** `PostgresChatHistoryProvider : ChatHistoryProvider` em `AgenteViagens.Infra`.
  `ProvideChatHistoryAsync` lê as mensagens da sessão em ordem; `StoreChatHistoryAsync` grava
  as novas (request + response) e atualiza `atualizada_em`.
- **Dono da sessão:** `usuario_id` = claim `sub` do JWT obtido no login.
- **Título:** a primeira mensagem do usuário, cortada em ~60 caracteres. *(Decisão do Claude.)*
- **Lista ao iniciar:** `SelectionPrompt` do Spectre com "➕ Nova conversa" primeiro e depois até
  10 sessões do usuário, mais recentes primeiro, no formato `dd/MM HH:mm · título`.
- **Ao retomar:** mostrar a última troca (última mensagem do usuário e última resposta do agente)
  antes do prompt, para a plateia ver de onde a conversa continua. *(Decisão do Claude.)*
- **Entrada por pipe:** os prompts do Spectre exigem terminal interativo. Com stdin redirecionado
  (ensaios automatizados), o console não mostra a lista e começa uma conversa nova; as perguntas
  continuam lidas linha a linha como hoje. *(Decisão do Claude.)*
- **Agente:** `AgenteDeViagens` recebe o `ChatHistoryProvider` pela composição (o Console injeta
  o do Postgres) e ganha um jeito de começar uma sessão nova ou retomar uma existente pelo id.
  A camada Agente não referencia Npgsql.

## O que fica para quem implementar

- Como o id da sessão do Postgres fica preso na `AgentSession` (StateBag, estado do provider com
  tipo serializável, ou o próprio provider resolvendo pelo id) — respeitando o achado de
  serialização acima.
- Como obter o `sub`: ler o payload do JWT do login ou usar dado que o `Usuarios.Api` já devolva.
- Layout exato do banner, cores e painéis; como combinar spinner + streaming (ex.: spinner até o
  primeiro trecho chegar ou até a primeira linha 🔧, depois texto direto).
- Estratégia de teste do provider (Testcontainers ou o Postgres do compose); deve cobrir ida e
  volta de uma conversa com chamada de tool e com aprovação negada e aprovada.
- Ajustes no `roteiro-demo.md`: nova seção da persistência (sair, reabrir, escolher a sessão,
  perguntar do clima), checklist T-30 com `docker compose up -d` deste repo, e corrigir a
  instrução de "lista de reservas limpa" (o seed já cria reservas para a Marina).

## Critério de pronto

- `git diff etapa-6 etapa-7` só tem visual; `git diff etapa-7 etapa-8` só tem persistência (e a
  troca para Chat Completions).
- Ensaio real: conversa sobre Gramado → `sair` → reabrir → escolher a sessão → "E o clima nesses
  dias?" responde sobre Gramado em 12–14/12 sem perguntar de novo.
- Uma sessão retomada com reserva aprovada no histórico continua funcionando (nova pergunta e
  nova aprovação).
- Com o Postgres parado, o preflight mostra `✗ Postgres ...` com a instrução de subir o container.
- Suíte de testes verde.
