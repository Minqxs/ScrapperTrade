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
    public DateTimeOffset UpdatedAt { get; set; }
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
