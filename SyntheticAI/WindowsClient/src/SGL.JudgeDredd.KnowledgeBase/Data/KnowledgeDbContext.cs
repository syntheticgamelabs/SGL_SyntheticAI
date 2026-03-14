using Microsoft.EntityFrameworkCore;
using SGL.JudgeDredd.KnowledgeBase.Entities;

namespace SGL.JudgeDredd.KnowledgeBase.Data;

public class KnowledgeDbContext : DbContext
{
    public DbSet<ThreatSignatureEntity> ThreatSignatures => Set<ThreatSignatureEntity>();
    public DbSet<SolutionEntity> Solutions => Set<SolutionEntity>();
    public DbSet<FirewallTemplateEntity> FirewallTemplates => Set<FirewallTemplateEntity>();
    public DbSet<SystemPromptEntity> SystemPrompts => Set<SystemPromptEntity>();
    public DbSet<ScanHistoryEntity> ScanHistory => Set<ScanHistoryEntity>();
    public DbSet<QuarantineEntity> QuarantinedFiles => Set<QuarantineEntity>();
    public DbSet<ThreatAnalysisEntity> ThreatAnalyses => Set<ThreatAnalysisEntity>();

    private readonly string _dbPath;

    public KnowledgeDbContext(string? dbPath = null)
    {
        _dbPath = dbPath ?? Path.Combine(AppContext.BaseDirectory, "data", "sgl-sai-knowledge.db");
    }

    public KnowledgeDbContext(DbContextOptions<KnowledgeDbContext> options) : base(options)
    {
        _dbPath = Path.Combine(AppContext.BaseDirectory, "data", "sgl-sai-knowledge.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            var dir = Path.GetDirectoryName(_dbPath);
            if (dir != null) Directory.CreateDirectory(dir);
            options.UseSqlite($"Data Source={_dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ThreatSignatureEntity>(e =>
        {
            e.HasIndex(t => t.Sha256Hash).IsUnique();
            e.HasIndex(t => t.Md5Hash);
            e.HasMany(t => t.Solutions).WithOne(s => s.Threat).HasForeignKey(s => s.ThreatId);
        });

        modelBuilder.Entity<SystemPromptEntity>(e =>
        {
            e.HasIndex(p => p.ContextKey).IsUnique();
        });

        modelBuilder.Entity<FirewallTemplateEntity>(e =>
        {
            e.HasIndex(f => f.Category);
        });

        modelBuilder.Entity<ScanHistoryEntity>(e =>
        {
            e.HasIndex(s => s.SessionId);
        });

        modelBuilder.Entity<QuarantineEntity>(e =>
        {
            e.HasIndex(q => q.Sha256Hash);
            e.HasIndex(q => q.OriginalPath);
        });

        modelBuilder.Entity<ThreatAnalysisEntity>(e =>
        {
            e.HasIndex(a => a.AnalyzedAt);
        });
    }
}
