using Microsoft.EntityFrameworkCore;

namespace AgenteViagens.Infra.Historico;

/// <summary>Uma conversa. O título só serve para a pessoa reconhecer a conversa na lista.</summary>
public sealed class SessaoSalva
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public string Titulo { get; set; } = "";
    public DateTimeOffset CriadaEm { get; set; }
    public DateTimeOffset AtualizadaEm { get; set; }
}

/// <summary>Uma mensagem da conversa: a ChatMessage inteira em JSON, do jeito que volta para o modelo.</summary>
public sealed class MensagemSalva
{
    public long Id { get; set; }
    public Guid SessaoId { get; set; }
    public string Papel { get; set; } = "";
    public string Conteudo { get; set; } = "";
    public DateTimeOffset CriadaEm { get; set; }
}

public sealed class HistoricoDbContext(DbContextOptions<HistoricoDbContext> options) : DbContext(options)
{
    public DbSet<SessaoSalva> Sessoes => Set<SessaoSalva>();
    public DbSet<MensagemSalva> Mensagens => Set<MensagemSalva>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessaoSalva>(e =>
        {
            e.ToTable("sessoes");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(s => s.UsuarioId).HasColumnName("usuario_id");
            e.Property(s => s.Titulo).HasColumnName("titulo").HasMaxLength(80);
            e.Property(s => s.CriadaEm).HasColumnName("criada_em");
            e.Property(s => s.AtualizadaEm).HasColumnName("atualizada_em");
            e.HasIndex(s => s.UsuarioId);
        });

        modelBuilder.Entity<MensagemSalva>(e =>
        {
            e.ToTable("mensagens");
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(m => m.SessaoId).HasColumnName("sessao_id");
            e.Property(m => m.Papel).HasColumnName("papel").HasMaxLength(20);
            e.Property(m => m.Conteudo).HasColumnName("conteudo").HasColumnType("jsonb");
            e.Property(m => m.CriadaEm).HasColumnName("criada_em");
            e.HasOne<SessaoSalva>().WithMany().HasForeignKey(m => m.SessaoId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
