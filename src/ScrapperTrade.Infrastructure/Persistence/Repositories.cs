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
            current.UpdatedAt = instrument.UpdatedAt;
        }
        await db.SaveChangesAsync(cancellationToken);
    }
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
