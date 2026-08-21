using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ScrapperTrade.Infrastructure.Knowledge;
using ScrapperTrade.Infrastructure.StrategyGovernance;

namespace ScrapperTrade.Infrastructure.Persistence;

public sealed class ScrapperTradeDbContext(DbContextOptions<ScrapperTradeDbContext> options) : DbContext(options)
{
    public DbSet<ConfigurationEntry> Configuration => Set<ConfigurationEntry>();
    public DbSet<InstrumentRecord> Instruments => Set<InstrumentRecord>();
    public DbSet<AuditLogRecord> AuditLogs => Set<AuditLogRecord>();
    public DbSet<SystemEventRecord> SystemEvents => Set<SystemEventRecord>();
    public DbSet<BrokerSymbolMetadataRecord> BrokerSymbolMetadata => Set<BrokerSymbolMetadataRecord>();
    public DbSet<ExposureGroupRecord> ExposureGroups => Set<ExposureGroupRecord>();
    public DbSet<RiskPolicyVersionRecord> RiskPolicyVersions => Set<RiskPolicyVersionRecord>();
    public DbSet<RiskPolicyChangeRecord> RiskPolicyChanges => Set<RiskPolicyChangeRecord>();
    public DbSet<PositionRecord> Positions => Set<PositionRecord>();
    public DbSet<TradeEventRecord> TradeEvents => Set<TradeEventRecord>();
    public DbSet<KnowledgeSourceRecord> KnowledgeSources => Set<KnowledgeSourceRecord>();
    public DbSet<KnowledgeDocumentRecord> KnowledgeDocuments => Set<KnowledgeDocumentRecord>();
    public DbSet<KnowledgeChunkRecord> KnowledgeChunks => Set<KnowledgeChunkRecord>();
    public DbSet<KnowledgeTagRecord> KnowledgeTags => Set<KnowledgeTagRecord>();
    public DbSet<KnowledgeDocumentTagRecord> KnowledgeDocumentTags => Set<KnowledgeDocumentTagRecord>();
    public DbSet<KnowledgeIngestionJobRecord> KnowledgeIngestionJobs => Set<KnowledgeIngestionJobRecord>();
    public DbSet<StrategyDefinitionRecord> StrategyDefinitions => Set<StrategyDefinitionRecord>();
    public DbSet<StrategyVersionRecord> StrategyVersions => Set<StrategyVersionRecord>();
    public DbSet<BacktestRunRecord> BacktestRuns => Set<BacktestRunRecord>();
    public DbSet<BacktestMetricRecord> BacktestMetrics => Set<BacktestMetricRecord>();
    public DbSet<BacktestTradeRecord> BacktestTrades => Set<BacktestTradeRecord>();
    public DbSet<StrategyValidationRunRecord> StrategyValidationRuns => Set<StrategyValidationRunRecord>();
    public DbSet<ResearchCandidateRecord> ResearchCandidates => Set<ResearchCandidateRecord>();
    public DbSet<ResearchCandidateProvenanceRecord> ResearchCandidateProvenance => Set<ResearchCandidateProvenanceRecord>();
    public DbSet<StrategyLineageRecord> StrategyLineage => Set<StrategyLineageRecord>();
    public DbSet<ShadowComparisonRecord> ShadowComparisons => Set<ShadowComparisonRecord>();
    public DbSet<StrategyActivationRecord> StrategyActivations => Set<StrategyActivationRecord>();
    public DbSet<StrategyGovernanceAuditRecord> StrategyGovernanceAudit => Set<StrategyGovernanceAuditRecord>();

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
            entity.Property(x => x.ExposureGroupCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.TradingSessionsJson).IsRequired();
        });

        modelBuilder.Entity<BrokerSymbolMetadataRecord>(entity =>
        {
            entity.ToTable("broker_symbol_metadata"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.InstrumentId, x.BrokerSymbol }).IsUnique();
            entity.Property(x => x.BrokerSymbol).HasMaxLength(64).IsRequired();
            entity.HasOne(x => x.Instrument).WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExposureGroupRecord>(entity =>
        {
            entity.ToTable("exposure_groups"); entity.HasKey(x => x.Code);
            entity.Property(x => x.Code).HasMaxLength(64); entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<RiskPolicyVersionRecord>(entity =>
        {
            entity.ToTable("risk_policy_versions"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.Version).IsUnique();
            entity.Property(x => x.PolicyJson).IsRequired(); entity.Property(x => x.ChangedBy).HasMaxLength(100).IsRequired(); entity.Property(x => x.ChangeReason).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<RiskPolicyChangeRecord>(entity =>
        {
            entity.ToTable("risk_policy_changes"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.OccurredAt);
            entity.Property(x => x.ChangedBy).HasMaxLength(100).IsRequired(); entity.Property(x => x.ChangeReason).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<PositionRecord>(entity =>
        {
            entity.ToTable("positions"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.BrokerPositionId).IsUnique(); entity.HasIndex(x => new { x.LogicalSymbol, x.Status });
            entity.Property(x => x.BrokerPositionId).HasMaxLength(100).IsRequired(); entity.Property(x => x.LogicalSymbol).HasMaxLength(64).IsRequired(); entity.Property(x => x.BrokerSymbol).HasMaxLength(64).IsRequired(); entity.Property(x => x.Side).HasMaxLength(16).IsRequired(); entity.Property(x => x.ExposureGroupCode).HasMaxLength(64).IsRequired(); entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<TradeEventRecord>(entity =>
        {
            entity.ToTable("trade_events"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.PositionId); entity.HasIndex(x => x.SignalId); entity.HasIndex(x => x.OccurredAt);
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired(); entity.Property(x => x.DetailJson).IsRequired();
        });

        modelBuilder.Entity<KnowledgeSourceRecord>(entity =>
        {
            entity.ToTable("knowledge_sources"); entity.HasKey(x => x.Id); entity.Property(x => x.Name).HasMaxLength(300).IsRequired(); entity.Property(x => x.SourceType).HasMaxLength(50).IsRequired(); entity.Property(x => x.OriginalLocator).HasMaxLength(500);
        });
        modelBuilder.Entity<KnowledgeDocumentRecord>(entity =>
        {
            entity.ToTable("knowledge_documents"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.ContentHash).IsUnique(); entity.HasIndex(x => x.DeletedAt);
            entity.Property(x => x.Title).HasMaxLength(500).IsRequired(); entity.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired(); entity.Property(x => x.MediaType).HasMaxLength(100).IsRequired(); entity.Property(x => x.ContentHash).HasMaxLength(64).IsRequired(); entity.Property(x => x.StoredRelativePath).HasMaxLength(500).IsRequired();
            entity.HasOne(x => x.Source).WithMany(x => x.Documents).HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<KnowledgeChunkRecord>(entity =>
        {
            entity.ToTable("knowledge_chunks"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.DocumentId, x.Ordinal }).IsUnique(); entity.Property(x => x.Text).IsRequired();
            entity.HasOne(x => x.Document).WithMany(x => x.Chunks).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<KnowledgeTagRecord>(entity =>
        {
            entity.ToTable("knowledge_tags"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.Name).IsUnique(); entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
        });
        modelBuilder.Entity<KnowledgeDocumentTagRecord>(entity =>
        {
            entity.ToTable("knowledge_document_tags"); entity.HasKey(x => new { x.DocumentId, x.TagId });
            entity.HasOne(x => x.Document).WithMany(x => x.DocumentTags).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Tag).WithMany(x => x.DocumentTags).HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<KnowledgeIngestionJobRecord>(entity =>
        {
            entity.ToTable("knowledge_ingestion_jobs"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.Status); entity.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired(); entity.Property(x => x.Status).HasMaxLength(32).IsRequired(); entity.Property(x => x.ErrorCode).HasMaxLength(100); entity.Property(x => x.Detail).HasMaxLength(2000);
        });

        modelBuilder.Entity<StrategyDefinitionRecord>(entity =>
        {
            entity.ToTable("strategy_definitions"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.Key).IsUnique(); entity.Property(x => x.Key).HasMaxLength(100).IsRequired(); entity.Property(x => x.Name).HasMaxLength(200).IsRequired(); entity.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        });
        modelBuilder.Entity<StrategyVersionRecord>(entity =>
        {
            entity.ToTable("strategy_versions"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.StrategyDefinitionId, x.Version }).IsUnique(); entity.HasIndex(x => x.SpecificationHash);
            entity.Property(x => x.SpecificationJson).IsRequired(); entity.Property(x => x.SpecificationHash).HasMaxLength(64).IsRequired(); entity.Property(x => x.LifecycleStatus).HasMaxLength(32).IsRequired();
            entity.HasOne(x => x.StrategyDefinition).WithMany(x => x.Versions).HasForeignKey(x => x.StrategyDefinitionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BacktestRunRecord>(entity =>
        {
            entity.ToTable("backtest_runs"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.StrategyVersionId); entity.Property(x => x.DatasetReference).HasMaxLength(500).IsRequired(); entity.Property(x => x.CostModelJson).IsRequired(); entity.Property(x => x.Status).HasMaxLength(32).IsRequired(); entity.HasOne(x => x.StrategyVersion).WithMany().HasForeignKey(x => x.StrategyVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BacktestMetricRecord>(entity => { entity.ToTable("backtest_metrics"); entity.HasKey(x => x.BacktestRunId); entity.HasOne(x => x.BacktestRun).WithOne(x => x.Metrics).HasForeignKey<BacktestMetricRecord>(x => x.BacktestRunId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<BacktestTradeRecord>(entity =>
        {
            entity.ToTable("backtest_trades"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.BacktestRunId); entity.Property(x => x.Instrument).HasMaxLength(64).IsRequired(); entity.Property(x => x.Side).HasMaxLength(16).IsRequired(); entity.HasOne(x => x.BacktestRun).WithMany(x => x.Trades).HasForeignKey(x => x.BacktestRunId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<StrategyValidationRunRecord>(entity =>
        {
            entity.ToTable("strategy_validation_runs"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.StrategyVersionId, x.ValidationKind }); entity.Property(x => x.ValidationKind).HasMaxLength(100).IsRequired(); entity.Property(x => x.Status).HasMaxLength(32).IsRequired(); entity.Property(x => x.EvidenceJson).IsRequired(); entity.HasOne(x => x.StrategyVersion).WithMany().HasForeignKey(x => x.StrategyVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ResearchCandidateRecord>(entity =>
        {
            entity.ToTable("research_candidates"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.StrategyVersionId); entity.Property(x => x.Hypothesis).HasMaxLength(4000).IsRequired(); entity.Property(x => x.Status).HasMaxLength(32).IsRequired(); entity.Property(x => x.AmbiguitiesJson).IsRequired(); entity.HasOne(x => x.StrategyVersion).WithMany().HasForeignKey(x => x.StrategyVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ResearchCandidateProvenanceRecord>(entity =>
        {
            entity.ToTable("research_candidate_provenance"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.ResearchCandidateId); entity.Property(x => x.SourceType).HasMaxLength(100).IsRequired(); entity.Property(x => x.SourceReference).HasMaxLength(500).IsRequired(); entity.Property(x => x.Citation).HasMaxLength(1000); entity.Property(x => x.Rationale).HasMaxLength(2000).IsRequired(); entity.HasOne(x => x.ResearchCandidate).WithMany(x => x.Provenance).HasForeignKey(x => x.ResearchCandidateId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<StrategyLineageRecord>(entity =>
        {
            entity.ToTable("strategy_lineage"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.ParentVersionId, x.ChildVersionId }).IsUnique(); entity.Property(x => x.Relationship).HasMaxLength(100).IsRequired(); entity.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
            entity.HasOne<StrategyVersionRecord>().WithMany().HasForeignKey(x => x.ParentVersionId).OnDelete(DeleteBehavior.Restrict); entity.HasOne<StrategyVersionRecord>().WithMany().HasForeignKey(x => x.ChildVersionId).OnDelete(DeleteBehavior.Restrict); entity.HasOne<ResearchCandidateRecord>().WithMany().HasForeignKey(x => x.ResearchCandidateId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<ShadowComparisonRecord>(entity =>
        {
            entity.ToTable("shadow_comparisons"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.ChampionVersionId, x.ChallengerVersionId }); entity.Property(x => x.Status).HasMaxLength(32).IsRequired(); entity.Property(x => x.ChampionMetricsJson).IsRequired(); entity.Property(x => x.ChallengerMetricsJson).IsRequired(); entity.Property(x => x.DecisionEvidenceJson).IsRequired();
            entity.HasOne<StrategyVersionRecord>().WithMany().HasForeignKey(x => x.ChampionVersionId).OnDelete(DeleteBehavior.Restrict); entity.HasOne<StrategyVersionRecord>().WithMany().HasForeignKey(x => x.ChallengerVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<StrategyActivationRecord>(entity =>
        {
            entity.ToTable("strategy_activations"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.StrategyDefinitionId).IsUnique().HasFilter("DeactivatedAt IS NULL"); entity.HasIndex(x => x.UserConfirmationId).IsUnique(); entity.Property(x => x.ApprovedByUser).HasMaxLength(200).IsRequired(); entity.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
            entity.HasOne<StrategyDefinitionRecord>().WithMany().HasForeignKey(x => x.StrategyDefinitionId).OnDelete(DeleteBehavior.Restrict); entity.HasOne<StrategyVersionRecord>().WithMany().HasForeignKey(x => x.StrategyVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<StrategyGovernanceAuditRecord>(entity =>
        {
            entity.ToTable("strategy_governance_audit"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.StrategyVersionId); entity.HasIndex(x => x.ConfirmationId).IsUnique().HasFilter("ConfirmationId IS NOT NULL"); entity.Property(x => x.ActorType).HasMaxLength(32).IsRequired(); entity.Property(x => x.ActorId).HasMaxLength(200).IsRequired(); entity.Property(x => x.Action).HasMaxLength(100).IsRequired(); entity.Property(x => x.FromStatus).HasMaxLength(32).IsRequired(); entity.Property(x => x.ToStatus).HasMaxLength(32).IsRequired(); entity.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
            entity.HasOne<StrategyVersionRecord>().WithMany().HasForeignKey(x => x.StrategyVersionId).OnDelete(DeleteBehavior.Restrict);
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
