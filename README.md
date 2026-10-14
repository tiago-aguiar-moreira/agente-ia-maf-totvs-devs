# Agente de Viagens — Microsoft Agent Framework

Demo da palestra **"Como criar um agente de IA em .NET com o Microsoft Agent Framework"**.
Um agente de viagens em console que usa um modelo do **Azure AI Foundry** e as tools de um
**servidor MCP** de agência de viagens, com system prompt blindado, sessão, aprovação humana,
middleware e observabilidade no **Langfuse**.

## Arquitetura

```
AgenteViagens.Console  → casca: view + composition root (Program, Preflight, ConsoleInteracao)
AgenteViagens.Agente   → lógica do agente (MAF): instruções, aprovação, middleware
AgenteViagens.Infra    → integrações: Foundry, MCP (JWT), login, telemetria
```
Console → Agente e Console → Infra. Agente e Infra não se conhecem.

## Pré-requisitos

1. **.NET 10 SDK**.
2. **Servidor MCP da agência** no ar (repositório da palestra "Como construir e consumir servidores
   MCP em .NET", com validação de JWT): `docker compose up -d --build`. Expõe o MCP em
   `http://localhost:5400` e o login em `http://localhost:5450`.
3. **Azure AI Foundry** com um deployment `gpt-4.1` — veja [docs/provisionamento-foundry.md](docs/provisionamento-foundry.md) — e `az login`.
4. **Langfuse Cloud** (plano gratuito) com um par de API keys.

## Configuração

`src/AgenteViagens.Console/appsettings.json`: `Foundry:ProjectEndpoint`, `Foundry:Deployment`,
`Mcp:Url`, `Usuarios:Url`, `Langfuse:OtlpEndpoint` (`https://us.cloud.langfuse.com/api/public/otel`
para projetos na região US; `https://cloud.langfuse.com/api/public/otel` para a região EU).

Segredos:
```bash
cd src/AgenteViagens.Console
dotnet user-secrets set "Usuarios:Email" "marina.torres@example.com"
dotnet user-secrets set "Usuarios:Senha" "Demo@123"
dotnet user-secrets set "Langfuse:PublicKey" "pk-lf-..."
dotnet user-secrets set "Langfuse:SecretKey" "sk-lf-..."
```

## Rodar

```bash
dotnet test
dotnet run --project src/AgenteViagens.Console
```

O preflight confere login, MCP, Foundry e telemetria antes do primeiro prompt. Digite `sair` para
encerrar; `Ctrl+C` interrompe só a resposta em andamento.

## Etapas da palestra

| Tag | Conteúdo |
|---|---|
| `etapa-1` | Agente + system prompt + Foundry + console |
| `etapa-2` | Tools via MCP + login (JWT no transporte) |
| `etapa-3` | Sessão multi-turn |
| `etapa-4` | Aprovação humana de tools sensíveis |
| `etapa-5` | Middleware de log de tools |
| `etapa-6` | Observabilidade → Langfuse |

`git checkout etapa-N` para ver o código em cada ponto; `git diff etapa-3 etapa-4` mostra o que cada etapa acrescentou.

Roteiro da demo: [docs/roteiro-demo.md](docs/roteiro-demo.md) · Esboço dos slides: [docs/slides-esboco.md](docs/slides-esboco.md)
