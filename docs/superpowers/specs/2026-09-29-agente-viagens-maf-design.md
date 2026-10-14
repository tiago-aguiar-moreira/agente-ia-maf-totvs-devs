# Agente de Viagens com Microsoft Agent Framework — Design

**Data:** 2026-09-29
**Contexto:** palestra de 45–60 min "Como criar um agente de IA em .NET com o Microsoft Agent Framework".
Breve introdução teórica em slides + apresentação comentada de código pronto (sem live coding).

## 1. Objetivo e critérios de sucesso

- Público: desenvolvedores .NET em geral (conhecem C#, não necessariamente MAF ou MCP).
- A plateia sai entendendo os blocos de um agente no MAF: criação do agente, system prompt,
  tools via MCP, sessão, aprovação humana, middleware e observabilidade.
- Uma demo ponta a ponta funcionando, conduzida por um agente de viagens em console que consome o
  servidor MCP de agência de viagens já existente.
- Código organizado em camadas, simples, com cada tópico da palestra concentrado em poucos arquivos
  curtos e legíveis no telão.

## 2. Dependências externas

| Dependência | Detalhe |
|---|---|
| Servidor MCP | `Agente.Mcp` em `S:\workspace\MVPConf2026\Como construir e consumir servidores MCP em .NET\novas-apresentacao\projeto`, Streamable HTTP stateless em `http://localhost:5400`, sobe via `docker compose up -d --build`. ~30 tools, 1 resource, 1 prompt. Lê o usuário do claim `sub` do JWT recebido em `Authorization`. **Requer a mudança de validação de JWT descrita em 6.3.** |
| Usuarios.Api | `POST http://localhost:5450/usuarios/login` `{ email, senha }` → `{ token, expiraEm }`. JWT dura 2 h. Usuário semeado: `marina.torres@example.com` / `Demo@123`. |
| Azure AI Foundry | Projeto a ser provisionado (ainda não existe). Deployment recomendado: `gpt-4.1` (ou sucessor não-reasoning mais recente do catálogo) — bom tool calling e baixa latência no palco. Autenticação via `DefaultAzureCredential` (`az login`). Orçamento ~R$ 850/mês, custo não é restrição. |
| Langfuse Cloud | Backend de traces via OTLP **HTTP** (sem gRPC) em `https://cloud.langfuse.com/api/public/otel`, Basic Auth `base64(public:secret)` + header `x-langfuse-ingestion-version: 4`. Escolhido sobre OpenObserve por renderizar nativamente as convenções GenAI (prompt, resposta, tools, tokens) sem exigir aprofundamento na ferramenta. Cloud (plano gratuito) em vez de self-host, que é pesado demais ao lado dos 7 containers do MCP. |

## 3. Escopo de recursos do MAF

Incluído:
1. Criação do agente (`IChatClient` do Foundry → `ChatClientAgent`), `RunStreamingAsync`.
2. System prompt: personalidade + guardrails (recusa fora de escopo, não inventar dados, confirmar ações com custo, resistir a "ignore suas instruções").
3. Tools via MCP (client MCP HTTP com JWT, `ListToolsAsync`, tools entregues ao agente).
4. Sessão multi-turn (`AgentSession`).
5. Aprovação humana de tools sensíveis (`ApprovalRequiredAIFunction` / `ToolApprovalRequestContent`).
6. Middleware de function calling (log das chamadas de tool).
7. Observabilidade com OpenTelemetry → Langfuse.

Fora de escopo (citados só como "próximos passos"): workflows / multi-agente, hosting como API / AG-UI / DevUI,
memória persistente / context providers, structured output, uso do resource e do prompt MCP.

## 4. Arquitetura

```
AgenteViagens.sln
├─ src/AgenteViagens.Console   → casca: view + composition root
├─ src/AgenteViagens.Agente    → lógica do agente (MAF), sem conhecer Foundry nem MCP
└─ src/AgenteViagens.Infra     → integrações externas: Foundry, MCP, login, telemetria
```

Dependências: `Console → Agente`, `Console → Infra`. Agente e Infra não se referenciam.
.NET 10 (mesma versão do servidor MCP).

### 4.1 AgenteViagens.Console
- `Program.cs`: `Host.CreateApplicationBuilder` (config, user-secrets, DI), preflight, loop de chat.
- `ConsoleInteracao.cs`: implementa `IInteracaoComUsuario` — pergunta "Confirmar? (s/n)" e imprime as linhas 🔧 de chamada de tool.
- Renderização do streaming de texto e mensagens de erro/aviso.
- Nenhuma regra de agente.

### 4.2 AgenteViagens.Agente
- `Instrucoes.cs`: system prompt (personalidade + guardrails).
- `AgenteDeViagens.cs`: recebe `IChatClient` + `IEnumerable<AITool>`, monta o `AIAgent`
  (`.AsBuilder().Use(LogToolMiddleware).UseOpenTelemetry().Build()`), cria uma `AgentSession`
  e expõe `ConversarAsync(string)`, tratando o ciclo de aprovação.
- `RegrasDeAprovacao.cs`: lista explícita dos nomes das tools sensíveis (criar/cancelar reserva de hotel,
  comprar/cancelar passagem, criar/cancelar inscrição em atividade). Essas tools são envolvidas em
  `ApprovalRequiredAIFunction`. Nomes exatos confirmados contra o `ListToolsAsync` do servidor.
- `LogToolMiddleware.cs`: function-calling middleware que notifica `IInteracaoComUsuario`.
- `IInteracaoComUsuario.cs`:
  - `Task<bool> ConfirmarAcaoAsync(string tool, IDictionary<string, object?> argumentos)`
  - `void NotificarChamadaDeTool(string tool, IDictionary<string, object?> argumentos)` (assinatura final pode incluir resultado/duração)

### 4.3 AgenteViagens.Infra
- `FoundryChatClientFactory.cs`: cria `IChatClient` a partir do projeto Foundry com `DefaultAzureCredential`.
  A API exata (obter `IChatClient` do `AIProjectClient` em vez de `AsAIAgent` direto) será validada na documentação durante o plano.
- `McpToolsFactory.cs`: `McpClient` HTTP para `Mcp:Url` com `Authorization: Bearer <JWT>`; retorna as tools.
- `UsuariosClient.cs`: login em `Usuarios.Api`, retorna token e expiração.
- `Telemetria.cs`: configura `TracerProvider` com as fontes do MAF / Microsoft.Extensions.AI e exportador OTLP HTTP para o Langfuse.
- Opções tipadas (`FoundryOptions`, `McpOptions`, `UsuariosOptions`, `LangfuseOptions`) — nenhuma camada além do Console lê `IConfiguration`.

## 5. Fluxo

### 5.1 Inicialização
1. Infra: login → JWT.
2. Infra: conecta ao MCP com JWT → tools.
3. Infra: cria `IChatClient` do Foundry; liga OpenTelemetry → Langfuse.
4. Agente: aplica instruções, envolve tools sensíveis em `ApprovalRequiredAIFunction`, aplica middleware e telemetria, cria a `AgentSession`.
5. Console imprime o preflight:
   ```
   ✓ Login como Marina Torres (token válido até HH:mm)
   ✓ MCP conectado — N tools (M exigem aprovação)
   ✓ Foundry — <deployment>
   ✓ Telemetria → Langfuse
   ```
   Falha em qualquer item encerra com mensagem acionável ("MCP offline: rode `docker compose up -d`", "Rode `az login`").

### 5.2 A cada mensagem
1. Console lê a linha e chama `AgenteDeViagens.ConversarAsync(texto)`.
2. `RunStreamingAsync(texto, session)`; texto é emitido em streaming para o Console; chamadas de tool passam pelo middleware (linha 🔧).
3. Se a resposta contiver `ToolApprovalRequestContent`, o Agente chama `ConfirmarAcaoAsync` e reenvia a resposta
   (`request.CreateResponse(aprovado)`) na mesma sessão, em loop até não haver mais pedidos.
4. Recusas por escopo vêm do próprio modelo (system prompt). A aprovação humana é a segunda linha de defesa contra prompt injection.

## 6. Autenticação

### 6.1 Credenciais

| Credencial | Quem usa | Para quê | Onde fica |
|---|---|---|---|
| Identidade Azure | Infra → Foundry | chamar o modelo | `az login` via `DefaultAzureCredential` |
| JWT do usuário | Infra → MCP | identificar quem está viajando | memória do processo (obtido no login) |
| Chaves Langfuse | Infra → Langfuse | enviar traces | user-secrets |

Azure e Langfuse são credenciais da aplicação; o JWT é do usuário final.

### 6.2 Caminho do JWT

```
Console (startup)
  1. lê Usuarios:Email/Senha (user-secrets)
  2. Infra.UsuariosClient → POST :5450/usuarios/login ← { token, expiraEm (+2h) }
  3. Infra.McpToolsFactory cria o McpClient HTTP com header fixo Authorization: Bearer <JWT>
        │  toda requisição MCP (list_tools, call_tool) leva o header
        ▼
Agente.Mcp :5400
  4. JwtBearer valida o token (assinatura HS256, iss, aud, exp) → 401 se inválido
  5. tool sensível obtém usuarioId do claim `sub` em HttpContext.User
  6. chama a API de domínio com usuarioId no corpo
```

- **O token nunca passa pelo modelo.** Ele vive no transporte HTTP do client MCP, não no prompt nem nos argumentos
  das tools. O LLM não informa "para quem" é a reserva; o servidor deriva o usuário do header. Prompt injection não
  consegue fazer o agente agir em nome de outro usuário. (Ponto de slide.)
- Só a Infra conhece o token; a camada Agente recebe `AITool`s já autenticadas; o Console só exibe "Login como … (válido até HH:mm)".
- Telemetria: traces contêm prompts, respostas e argumentos de tools, não headers HTTP. Verificar na demo que o token não aparece no Langfuse.
- A opção exata do SDK MCP .NET para headers adicionais no transporte HTTP será confirmada na documentação durante o plano.

### 6.3 Validação do token no servidor MCP (pré-requisito, repositório do servidor)

Situação atual encontrada: `UsuarioAutenticadoResolver` apenas decodifica o JWT (`ReadJwtToken`), sem validar assinatura,
assumindo um "Agente.Api" upstream que não existe neste desenho. O MCP não repassa o token às APIs de domínio
(exceto a tool de perfil) e os endpoints usados pelas tools (`/reservas`, `/passagens`, inscrições) são anônimos.
Consequência: um token forjado com qualquer `sub` faria reservas em nome de outro usuário.

Mudança no `Agente.Mcp`:
1. `Program.cs`: `AddAuthentication().AddJwtBearer(...)` com os mesmos `TokenValidationParameters` das APIs de domínio
   (chave simétrica, `ValidIssuer`, `ValidAudience`, `ValidateLifetime`) + `AddAuthorization()`; `app.MapMcp().RequireAuthorization()`.
2. `UsuarioAutenticadoResolver`: ler o claim `sub` de `HttpContext.User` (já validado) em vez de decodificar o header; remover o comentário sobre o "Agente.Api".
3. `docker-compose.yml`: repassar `Jwt__ChaveSecreta: ${JWT_KEY}`, `Jwt__Emissor` e `Jwt__Audiencia` ao container `agente-mcp`
   (mesmos valores do `Usuarios.Api`); README do servidor atualizado (user-secrets do `Agente.Mcp` para execução fora de container).

Efeito: requisição sem token, com assinatura inválida, expirada ou com emissor/audiência errados → 401 antes de qualquer tool.
O agente não muda.

### 6.4 Modelo de confiança e limite assumido

- Validação local com **HS256**: o validador recalcula o HMAC com a chave; não consulta o `Usuarios.Api` em runtime.
- Custo: a chave que valida também assina — toda aplicação que valida poderia emitir tokens. Passam a ser 6 aplicações com
  o `JWT_KEY`. Aceito por ser um sistema fechado de demo que já confia nessa chave; migrar não é tema da palestra.
- Slide: "aqui usamos chave simétrica compartilhada por simplicidade; em produção, prefira chaves assimétricas (RS256/ES256 + JWKS,
  via `options.Authority`) ou um provedor de identidade, para que só o emissor possa assinar. A spec de autorização do MCP
  prevê um fluxo OAuth 2.1 completo (Protected Resource Metadata) para cenários abertos."

## 7. Configuração

| Onde | Chave |
|---|---|
| `appsettings.json` (Console) | `Foundry:ProjectEndpoint`, `Foundry:Deployment`, `Mcp:Url` (`http://localhost:5400`), `Usuarios:Url` (`http://localhost:5450`), `Langfuse:OtlpEndpoint` |
| user-secrets (Console) | `Usuarios:Email`, `Usuarios:Senha`, `Langfuse:PublicKey`, `Langfuse:SecretKey` |
| ambiente | `az login` |

## 8. Tratamento de erros

- Erro dentro de tool (`McpException`, ex.: sem vagas): devolvido ao modelo como resultado da tool; o modelo explica. Sem código extra.
- Falha do modelo (429, timeout, rede): capturada no loop do Console, aviso ao usuário, sessão preservada.
- Aprovação negada: fluxo normal; o modelo recebe `approved: false` e informa que não executou.
- JWT de 2 h cobre a palestra; sem renovação automática.
- MCP responde 401 (token ausente, inválido ou expirado): preflight falha com mensagem acionável ("Token rejeitado pelo MCP: confira `JWT_KEY`/emissor/audiência ou reinicie o console para novo login").

## 9. Testes

- Unitários na camada Agente: `RegrasDeAprovacao` (quais tools são envolvidas) e `LogToolMiddleware` (notifica `IInteracaoComUsuario`) com fakes.
- Ciclo de aprovação testado com um `IChatClient` fake que emite uma chamada de tool sensível.
- Integração ponta a ponta validada manualmente pelo roteiro de demo (depende de Foundry, MCP e Langfuse reais).

## 10. Organização em etapas (git tags)

Projeto final único; histórico com tags que compilam e rodam:

| Tag | Conteúdo |
|---|---|
| `etapa-1` | Agente + system prompt + Foundry + loop do console |
| `etapa-2` | Tools via MCP + login |
| `etapa-3` | Sessão multi-turn |
| `etapa-4` | Aprovação humana |
| `etapa-5` | Middleware de log de tools |
| `etapa-6` | Observabilidade → Langfuse |

## 11. Roteiro da palestra (~50 min)

**Bloco 1 — Slides (~13 min):** abertura ("chatbot responde, agente age"); anatomia de um agente; MAF
(sucessor de Semantic Kernel + AutoGen, sobre `Microsoft.Extensions.AI`, `AIAgent`); MCP em um slide;
cenário e arquitetura; system prompt como contrato + aprovação humana como segunda linha de defesa.

**Bloco 2 — Código comentado + demo intercalada no app final (~32 min):**

| Etapa | Arquivos | Demo | Tempo |
|---|---|---|---|
| 1 | `Instrucoes.cs`, `AgenteDeViagens.cs`, `FoundryChatClientFactory.cs` | saudação com personalidade; pedido fora de escopo recusado | 7 min |
| 2 | `McpToolsFactory.cs`, `UsuariosClient.cs` (+ 1 slide: caminho do JWT, "o token nunca passa pelo modelo", HS256 × JWKS) | busca de hotéis em Gramado com 🔧 visíveis | 6 min |
| 3 | `AgenteDeViagens.cs` (sessão), `Program.cs` | follow-ups de clima e restaurante sem repetir contexto | 3 min |
| 4 | `RegrasDeAprovacao.cs`, ciclo de aprovação, `ConsoleInteracao.cs` | reserva negada; prompt injection ainda pede confirmação; reserva aprovada | 7 min |
| 5 | `LogToolMiddleware.cs` | "revelação" da origem das linhas 🔧 | 3 min |
| 6 | `Telemetria.cs` | trace da conversa no Langfuse | 6 min |

**Bloco 3 — Fechamento (~5 min) + Q&A:** recapitulação e próximos passos (workflows/multi-agente, hosting, memória persistente).

## 12. Entregáveis

- **Pré-requisito no repositório do servidor MCP:** validação de JWT no `Agente.Mcp` (seção 6.3), em commit próprio, antes da etapa 2.
- Solução com as 3 camadas + projeto de testes, tags `etapa-1` … `etapa-6`.
- `README.md`: pré-requisitos (MCP no ar, `az login`, user-secrets, Langfuse) e como rodar.
- `docs/roteiro-demo.md`: prompts prontos por momento de demo e ordem dos arquivos a abrir.
- `docs/provisionamento-foundry.md`: criação do projeto Foundry e do deployment.
- `docs/slides-esboco.md`: esboço de conteúdo por slide do Bloco 1 e do fechamento.
- Lembrete (fora do repo, responsabilidade do palestrante): gravar vídeo da demo completa como plano B.
