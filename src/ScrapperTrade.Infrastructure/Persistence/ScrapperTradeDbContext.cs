using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ScrapperTrade.Infrastructure.Persistence;

public sealed class ScrapperTradeDbContext(DbContextOptions<ScrapperTradeDbContext> options) : DbContext(options)
{
    public DbSet<ConfigurationEntry> Configuration => Set<ConfigurationEntry>();
    public DbSet<InstrumentRecord> Instruments => Set<InstrumentRecord>();
    public DbSet<AuditLogRecord> AuditLogs => Set<AuditLogRecord>();
    public DbSet<SystemEventRecord> SystemEvents => Set<SystemEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConfigurationEntry>(entity =>
        {
            entity.ToTable("configuration");
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasMaxLength(200);
            entity.Property(x => x.Value).IsRequired();
        });

        modelBuilder.Entity<InstrumentRecord>(entity =>
        {
            entity.ToTable("instruments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.LogicalSymbol).IsUnique();
            entity.Property(x => x.LogicalSymbol).HasMaxLength(64).IsRequired();
            entity.Property(x => x.BrokerSymbol).HasMaxLength(64);
        });

        modelBuilder.Entity<AuditLogRecord>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => x.CorrelationId);
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Outcome).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Detail).IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
        });

        modelBuilder.Entity<SystemEventRecord>(entity =>
        {
            entity.ToTable("system_events");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => x.EventType);
            entity.Property(x => x.Severity).HasMaxLength(32).IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Detail).IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
        });
    }
}

internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ScrapperTradeDbContext>
{
    public ScrapperTradeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ScrapperTradeDbContext>()
            .UseSqlite("Data Source=scrappertrade-design.db")
            .Options;
        return new ScrapperTradeDbContext(options);
    }
}
