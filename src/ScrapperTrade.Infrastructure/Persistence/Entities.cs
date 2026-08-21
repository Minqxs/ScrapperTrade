namespace ScrapperTrade.Infrastructure.Persistence;

public sealed class ConfigurationEntry
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class InstrumentRecord
{
    public Guid Id { get; set; }
    public string LogicalSymbol { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? BrokerSymbol { get; set; }
    public bool AllowLong { get; set; } = true;
    public bool AllowShort { get; set; } = true;
    public int MaxConcurrentPositions { get; set; } = 1;
    public string ExposureGroupCode { get; set; } = "DEFAULT";
    public string TradingSessionsJson { get; set; } = "[]";
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class BrokerSymbolMetadataRecord
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public InstrumentRecord Instrument { get; set; } = null!;
    public string BrokerSymbol { get; set; } = string.Empty;
    public decimal TickSize { get; set; }
    public decimal TickValue { get; set; }
    public decimal ContractSize { get; set; }
    public decimal VolumeMin { get; set; }
    public decimal VolumeMax { get; set; }
    public decimal VolumeStep { get; set; }
    public decimal StopLevel { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
}

public sealed class ExposureGroupRecord
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal MaxRiskFraction { get; set; }
    public decimal MaxSameDirectionRiskFraction { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RiskPolicyVersionRecord
{
    public long Id { get; set; }
    public int Version { get; set; }
    public string PolicyJson { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
    public DateTimeOffset EffectiveAt { get; set; }
}

public sealed class RiskPolicyChangeRecord
{
    public long Id { get; set; }
    public int FromVersion { get; set; }
    public int ToVersion { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class PositionRecord
{
    public Guid Id { get; set; }
    public string BrokerPositionId { get; set; } = string.Empty;
    public string LogicalSymbol { get; set; } = string.Empty;
    public string BrokerSymbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public decimal EntryPrice { get; set; }
    public decimal StopPrice { get; set; }
    public decimal Volume { get; set; }
    public decimal RiskAmount { get; set; }
    public string ExposureGroupCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}

public sealed class TradeEventRecord
{
    public long Id { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? SignalId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string DetailJson { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class AuditLogRecord
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
}

public sealed class SystemEventRecord
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
}
