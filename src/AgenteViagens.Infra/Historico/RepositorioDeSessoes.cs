using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace AgenteViagens.Infra.Historico;

public sealed record ResumoDeSessao(Guid Id, string Titulo, DateTimeOffset AtualizadaEm);

/// <summary>Consultas para a tela inicial: a lista de conversas e de onde a conversa escolhida parou.</summary>
public sealed class RepositorioDeSessoes(DbContextOptions<HistoricoDbContext> opcoes)
{
    public async Task<IReadOnlyList<ResumoDeSessao>> ListarAsync(Guid usuarioId, int limite, CancellationToken cancellationToken)
    {
        await using var db = new HistoricoDbContext(opcoes);
        return await db.Sessoes
            .Where(s => s.UsuarioId == usuarioId)
            .OrderByDescending(s => s.AtualizadaEm)
            .Take(limite)
            .Select(s => new ResumoDeSessao(s.Id, s.Titulo, s.AtualizadaEm))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Último texto do usuário e último texto do agente (mensagens só de tool são puladas).</summary>
    public async Task<(string? Pergunta, string? Resposta)> UltimaTrocaAsync(
        Guid sessaoId, Guid usuarioId, CancellationToken cancellationToken)
    {
        await using var db = new HistoricoDbContext(opcoes);
        var doUsuario = await db.Sessoes.AnyAsync(s => s.Id == sessaoId && s.UsuarioId == usuarioId, cancellationToken);
        if (!doUsuario)
        {
            return (null, null);
        }

        var recentes = await db.Mensagens
            .Where(m => m.SessaoId == sessaoId && (m.Papel == "user" || m.Papel == "assistant"))
            .OrderByDescending(m => m.Id)
            .Select(m => m.Conteudo)
            .Take(50)
            .ToListAsync(cancellationToken);

        var mensagens = recentes
            .Select(JsonDoHistorico.Desserializar)
            .Where(m => !string.IsNullOrWhiteSpace(m.Text))
            .ToList();

        return (mensagens.FirstOrDefault(m => m.Role == ChatRole.User)?.Text,
                mensagens.FirstOrDefault(m => m.Role == ChatRole.Assistant)?.Text);
    }
}
