namespace ScrapperTrade.Infrastructure.StrategyGovernance;

public static class StrategyStates
{
    public const string Draft = "DRAFT";
    public const string Validated = "VALIDATED";
    public const string Active = "ACTIVE";
    public const string Superseded = "SUPERSEDED";
    public const string Retired = "RETIRED";
}

public sealed class StrategyDefinitionRecord
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public List<StrategyVersionRecord> Versions { get; set; } = [];
}

public sealed class StrategyVersionRecord
{
    public Guid Id { get; set; }
    public Guid StrategyDefinitionId { get; set; }
    public StrategyDefinitionRecord StrategyDefinition { get; set; } = null!;
    public int Version { get; set; }
    public string SpecificationJson { get; set; } = string.Empty;
    public string SpecificationHash { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = StrategyStates.Draft;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class BacktestRunRecord
{
    public Guid Id { get; set; }
    public Guid StrategyVersionId { get; set; }
    public StrategyVersionRecord StrategyVersion { get; set; } = null!;
    public string DatasetReference { get; set; } = string.Empty;
    public string CostModelJson { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public bool IsOutOfSample { get; set; }
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public BacktestMetricRecord? Metrics { get; set; }
    public List<BacktestTradeRecord> Trades { get; set; } = [];
}

public sealed class BacktestMetricRecord
{
    public Guid BacktestRunId { get; set; }
    public BacktestRunRecord BacktestRun { get; set; } = null!;
    public int TradeCount { get; set; }
    public decimal ExpectancyR { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal MaximumDrawdownFraction { get; set; }
    public decimal NetReturnFraction { get; set; }
}

public sealed class BacktestTradeRecord
{
    public long Id { get; set; }
    public Guid BacktestRunId { get; set; }
    public BacktestRunRecord BacktestRun { get; set; } = null!;
    public string Instrument { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public DateTimeOffset EnteredAt { get; set; }
    public DateTimeOffset ExitedAt { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal RealisedR { get; set; }
    public decimal CostAmount { get; set; }
}

public sealed class StrategyValidationRunRecord
{
    public Guid Id { get; set; }
    public Guid StrategyVersionId { get; set; }
    public StrategyVersionRecord StrategyVersion { get; set; } = null!;
    public string ValidationKind { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public string EvidenceJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ResearchCandidateRecord
{
    public Guid Id { get; set; }
    public Guid StrategyVersionId { get; set; }
    public StrategyVersionRecord StrategyVersion { get; set; } = null!;
    public string Hypothesis { get; set; } = string.Empty;
    public string Status { get; set; } = "PROPOSED";
    public string AmbiguitiesJson { get; set; } = "[]";
    public bool HasUnresolvedAmbiguities { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<ResearchCandidateProvenanceRecord> Provenance { get; set; } = [];
}

public sealed class ResearchCandidateProvenanceRecord
{
    public long Id { get; set; }
    public Guid ResearchCandidateId { get; set; }
    public ResearchCandidateRecord ResearchCandidate { get; set; } = null!;
    public string SourceType { get; set; } = string.Empty;
    public string SourceReference { get; set; } = string.Empty;
    public string? Citation { get; set; }
    public string Rationale { get; set; } = string.Empty;
}

public sealed class StrategyLineageRecord
{
    public long Id { get; set; }
    public Guid ParentVersionId { get; set; }
    public Guid ChildVersionId { get; set; }
    public Guid? ResearchCandidateId { get; set; }
    public string Relationship { get; set; } = "DERIVED_FROM";
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ShadowComparisonRecord
{
    public Guid Id { get; set; }
    public Guid ChampionVersionId { get; set; }
    public Guid ChallengerVersionId { get; set; }
    public string Status { get; set; } = "RUNNING";
    public string ChampionMetricsJson { get; set; } = "{}";
    public string ChallengerMetricsJson { get; set; } = "{}";
    public string DecisionEvidenceJson { get; set; } = "{}";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class StrategyActivationRecord
{
    public long Id { get; set; }
    public Guid StrategyDefinitionId { get; set; }
    public Guid StrategyVersionId { get; set; }
    public Guid UserConfirmationId { get; set; }
    public string ApprovedByUser { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset ActivatedAt { get; set; }
    public DateTimeOffset? DeactivatedAt { get; set; }
}

public sealed class StrategyGovernanceAuditRecord
{
    public long Id { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Guid StrategyVersionId { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public Guid? ConfirmationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
