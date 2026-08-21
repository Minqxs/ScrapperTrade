using Microsoft.EntityFrameworkCore;

namespace ScrapperTrade.Infrastructure.Persistence;

public sealed class ConfigurationRepository(ScrapperTradeDbContext db)
{
    public Task<ConfigurationEntry?> FindAsync(string key, CancellationToken cancellationToken = default) =>
        db.Configuration.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key, cancellationToken);

    public async Task SetAsync(string key, string value, DateTimeOffset updatedAt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var item = await db.Configuration.SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (item is null)
            db.Configuration.Add(new ConfigurationEntry { Key = key, Value = value, UpdatedAt = updatedAt });
        else
        {
            item.Value = value;
            item.UpdatedAt = updatedAt;
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class InstrumentRepository(ScrapperTradeDbContext db)
{
    public Task<List<InstrumentRecord>> ListAsync(CancellationToken cancellationToken = default) =>
        db.Instruments.AsNoTracking().OrderBy(x => x.LogicalSymbol).ToListAsync(cancellationToken);

    public async Task UpsertAsync(InstrumentRecord instrument, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instrument.LogicalSymbol);
        var symbol = instrument.LogicalSymbol.Trim().ToUpperInvariant();
        var current = await db.Instruments.SingleOrDefaultAsync(x => x.LogicalSymbol == symbol, cancellationToken);
        if (current is null)
        {
            instrument.Id = instrument.Id == Guid.Empty ? Guid.NewGuid() : instrument.Id;
            instrument.LogicalSymbol = symbol;
            db.Instruments.Add(instrument);
        }
        else
        {
            current.Enabled = instrument.Enabled;
            current.BrokerSymbol = instrument.BrokerSymbol;
            current.AllowLong = instrument.AllowLong;
            current.AllowShort = instrument.AllowShort;
            current.MaxConcurrentPositions = instrument.MaxConcurrentPositions;
            current.ExposureGroupCode = instrument.ExposureGroupCode;
            current.TradingSessionsJson = instrument.TradingSessionsJson;
            current.UpdatedAt = instrument.UpdatedAt;
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class TradingUniverseRepository(ScrapperTradeDbContext db)
{
    public async Task UpsertExposureGroupAsync(ExposureGroupRecord group, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group.Code);
        var code = group.Code.Trim().ToUpperInvariant();
        var current = await db.ExposureGroups.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (current is null) { group.Code = code; db.ExposureGroups.Add(group); }
        else { current.DisplayName = group.DisplayName; current.MaxRiskFraction = group.MaxRiskFraction; current.MaxSameDirectionRiskFraction = group.MaxSameDirectionRiskFraction; current.UpdatedAt = group.UpdatedAt; }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertBrokerMetadataAsync(BrokerSymbolMetadataRecord metadata, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata.BrokerSymbol);
        var current = await db.BrokerSymbolMetadata.SingleOrDefaultAsync(x => x.InstrumentId == metadata.InstrumentId && x.BrokerSymbol == metadata.BrokerSymbol, cancellationToken);
        if (current is null) { metadata.Id = metadata.Id == Guid.Empty ? Guid.NewGuid() : metadata.Id; db.BrokerSymbolMetadata.Add(metadata); }
        else { current.TickSize = metadata.TickSize; current.TickValue = metadata.TickValue; current.ContractSize = metadata.ContractSize; current.VolumeMin = metadata.VolumeMin; current.VolumeMax = metadata.VolumeMax; current.VolumeStep = metadata.VolumeStep; current.StopLevel = metadata.StopLevel; current.ObservedAt = metadata.ObservedAt; }
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class RiskPolicyRepository(ScrapperTradeDbContext db)
{
    public Task<RiskPolicyVersionRecord?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        db.RiskPolicyVersions.AsNoTracking().OrderByDescending(x => x.Version).FirstOrDefaultAsync(cancellationToken);

    public async Task AddVersionAsync(RiskPolicyVersionRecord version, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version.ChangedBy); ArgumentException.ThrowIfNullOrWhiteSpace(version.ChangeReason); ArgumentException.ThrowIfNullOrWhiteSpace(version.PolicyJson);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var current = await db.RiskPolicyVersions.OrderByDescending(x => x.Version).FirstOrDefaultAsync(cancellationToken);
        var expected = (current?.Version ?? 0) + 1;
        if (version.Version != expected) throw new InvalidOperationException($"Risk policy version must be {expected}.");
        db.RiskPolicyVersions.Add(version);
        db.RiskPolicyChanges.Add(new RiskPolicyChangeRecord { FromVersion = current?.Version ?? 0, ToVersion = version.Version, ChangedBy = version.ChangedBy, ChangeReason = version.ChangeReason, OccurredAt = version.EffectiveAt });
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
}

public sealed class PositionRepository(ScrapperTradeDbContext db)
{
    public Task<List<PositionRecord>> ListOpenAsync(CancellationToken cancellationToken = default) => db.Positions.AsNoTracking().Where(x => x.Status == "OPEN").OrderBy(x => x.LogicalSymbol).ThenBy(x => x.BrokerPositionId).ToListAsync(cancellationToken);
    public async Task AddAsync(PositionRecord position, CancellationToken cancellationToken = default) { if (position.Id == Guid.Empty) position.Id = Guid.NewGuid(); db.Positions.Add(position); await db.SaveChangesAsync(cancellationToken); }
    public async Task AppendEventAsync(TradeEventRecord tradeEvent, CancellationToken cancellationToken = default) { db.TradeEvents.Add(tradeEvent); await db.SaveChangesAsync(cancellationToken); }
}

public sealed class EventRepository(ScrapperTradeDbContext db)
{
    public async Task AppendAuditAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
    {
        db.AuditLogs.Add(record);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendSystemEventAsync(SystemEventRecord record, CancellationToken cancellationToken = default)
    {
        db.SystemEvents.Add(record);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<List<AuditLogRecord>> ReadAuditAsync(int limit = 100, CancellationToken cancellationToken = default) =>
        db.AuditLogs.AsNoTracking().OrderByDescending(x => x.Id)
            .Take(ValidateLimit(limit)).ToListAsync(cancellationToken);

    public Task<List<SystemEventRecord>> ReadSystemEventsAsync(int limit = 100, CancellationToken cancellationToken = default) =>
        db.SystemEvents.AsNoTracking().OrderByDescending(x => x.Id)
            .Take(ValidateLimit(limit)).ToListAsync(cancellationToken);

    private static int ValidateLimit(int limit) => limit is >= 1 and <= 1000
        ? limit
        : throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 1000.");
}
