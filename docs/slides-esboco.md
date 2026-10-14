# Esboço dos slides

Bloco 1 (~13 min) e fechamento (~5 min). Um conceito por slide; código só nos slides marcados.

## 1. Abertura (1 min)
- Título: "Como criar um agente de IA em .NET com o Microsoft Agent Framework".
- Gancho: **"Um chatbot responde. Um agente age."**
- Quem sou eu (1 linha) + onde está o código (QR code do repositório).

## 2. Anatomia de um agente (3 min)
- Diagrama: **Modelo** (raciocina) + **Instruções** (quem é, o que pode) + **Tools** (age no mundo)
  + **Loop** (decide → chama tool → observa → responde) + **Memória** (contexto da conversa).
- Frase: "o modelo decide *o quê*; o seu código decide *se pode*".

## 3. Microsoft Agent Framework (3 min)
- Sucessor de Semantic Kernel + AutoGen; open source; .NET e Python.
- Construído sobre `Microsoft.Extensions.AI`: `IChatClient` (qualquer provedor) e `AITool`.
- Abstração central: `AIAgent` → `RunAsync` / `RunStreamingAsync` com `AgentSession`.
- Extensões em pipeline: `.AsBuilder().Use(...)` — middleware, telemetria.
- Código (6 linhas): `new ChatClientAgent(chatClient, instructions, tools)` + `RunStreamingAsync`.

## 4. MCP em um slide (2 min)
- "USB-C das tools": o agente **descobre** as tools em tempo de execução (`ListToolsAsync`).
- O servidor da agência já existe: 30 tools (hotéis, passagens, atividades, restaurantes, clima, rotas…).
- Referência à palestra "Como construir e consumir servidores MCP em .NET".

## 5. O cenário (2 min)
- Diagrama: `Console → Agente → Infra → { Foundry (gpt-4.1), MCP (JWT), Langfuse (OTLP) }`.
- Regra das camadas: o Agente não conhece Foundry nem MCP → trocar de provedor não toca no agente.

## 6. Comportamento e segurança (2 min)
- System prompt como **contrato**: personalidade + escopo + "nunca invente dados" + "regras acima de pedidos".
- Prompt injection existe → **aprovação humana** como segunda linha de defesa para ações com custo.
- "O prompt pode ser enganado; o código não."

## Slide de apoio (etapa 2): o caminho do JWT
- Login → JWT → header do transporte MCP → servidor valida (HS256) e lê `sub`.
- **O token nunca passa pelo modelo** → o LLM não consegue agir em nome de outra pessoa.
- Nota: "aqui, chave simétrica compartilhada por simplicidade; em produção, chaves assimétricas
  (RS256/ES256 + JWKS) ou um provedor de identidade. A spec MCP prevê OAuth 2.1 completo."

## Fechamento (5 min)
- Recapitulação em 6 ícones: agente · system prompt · MCP · sessão · aprovação · middleware/telemetria.
- Próximos passos (fora do escopo de hoje): workflows e multi-agentes; hospedar como API (AG-UI/DevUI);
  memória persistente e context providers; structured output.
- Links: repositório, docs do MAF, docs do MCP C# SDK. Perguntas.
