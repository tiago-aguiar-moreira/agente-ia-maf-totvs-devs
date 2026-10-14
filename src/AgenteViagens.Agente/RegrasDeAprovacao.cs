using Microsoft.Extensions.AI;

namespace AgenteViagens.Agente;

/// <summary>
/// Quais tools só rodam com o "sim" do usuário. Segunda linha de defesa: mesmo que um prompt
/// injection convença o modelo, a ação não acontece sem aprovação humana.
/// </summary>
public static class RegrasDeAprovacao
{
    // Nomes como o servidor MCP publica (snake_case do nome do método C#).
    public static readonly IReadOnlySet<string> ToolsSensiveis = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "criar_reserva", "cancelar_reserva",
        "comprar_passagem", "cancelar_passagem",
        "criar_inscricao", "cancelar_inscricao",
    };

    public static List<AITool> Aplicar(IEnumerable<AITool> tools) =>
        tools.Select(tool => tool is AIFunction funcao && ToolsSensiveis.Contains(funcao.Name)
                ? new ApprovalRequiredAIFunction(funcao)
                : tool)
            .ToList();

    public static bool ExigeAprovacao(AITool tool) => tool is ApprovalRequiredAIFunction;

    /// <summary>Tools sensíveis que o servidor não publica (ex.: renomeadas) — sem aprovação, seriam um risco silencioso.</summary>
    public static IReadOnlyList<string> SensiveisAusentes(IEnumerable<AITool> tools)
    {
        var publicadas = tools.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return ToolsSensiveis.Where(nome => !publicadas.Contains(nome)).Order().ToList();
    }
}
