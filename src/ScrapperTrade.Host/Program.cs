using ScrapperTrade.Application;
using ScrapperTrade.Domain;
using ScrapperTrade.Infrastructure;
using ScrapperTrade.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
var databasePath = LocalDataPath.GetDatabasePath();
var commonFilesRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MetaQuotes", "Terminal", "Common", "Files");
builder.Services.AddSingleton<TradingSystemState>();
builder.Services.AddSingleton<SystemStateMachine>();
builder.Services.AddSingleton(new RiskPolicy());
builder.Services.AddSingleton<PortfolioRiskEngine>();
builder.Services.AddSingleton<IExecutionAdapter>(_ => new DemoExecutionSimulator(AccountKind.Demo));
builder.Services.AddSingleton<IAuditStore>(_ => new JsonAuditStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScrapperTrade", "audit.json")));
builder.Services.AddSingleton<TradeCoordinator>();
builder.Services.AddSingleton(new Mt5CommonFilesHeartbeatReader(commonFilesRoot));
builder.Services.AddSingleton(new Mt5CommonFilesSymbolReader(commonFilesRoot));
builder.Services.AddSingleton(new Mt5CommonFilesExecutionSnapshotReader(commonFilesRoot));
builder.Services.AddSingleton(new Mt5CommonFilesCommandQueue(commonFilesRoot));
builder.Services.AddScoped(_ => new ScrapperTradeDbContext(PersistenceBootstrap.CreateOptions(databasePath)));
builder.Services.AddScoped<InstrumentRepository>();
builder.Services.AddScoped<EventRepository>();
builder.Services.AddScoped<ConfigurationRepository>();
builder.Services.AddScoped<TradingUniverseRepository>();
builder.Services.AddScoped<RiskPolicyRepository>();
builder.Services.AddScoped<PositionRepository>();

var app = builder.Build();
await PersistenceBootstrap.MigrateAsync(databasePath);

app.MapGet("/api/health", (SystemStateMachine system, Mt5CommonFilesHeartbeatReader heartbeat) =>
{
    var mt5 = heartbeat.Read(DateTimeOffset.UtcNow);
    return Results.Ok(new { status = mt5.IsPositiveDemo ? "healthy" : "degraded", mode = system.Snapshot.Mode, demoOnly = true });
});
app.MapGet("/api/system", (SystemStateMachine system) => system.Snapshot);
app.MapPost("/api/system/start", async (SystemStateMachine system, EventRepository events) =>
{
    var snapshot = system.Start(DateTimeOffset.UtcNow);
    await RecordSystemEvent(events, snapshot, "SYSTEM_STARTED");
    return Results.Ok(snapshot);
});
app.MapPost("/api/system/pause", async (SystemStateMachine system, EventRepository events) =>
{
    var snapshot = system.Pause(DateTimeOffset.UtcNow);
    await RecordSystemEvent(events, snapshot, "NEW_ENTRIES_PAUSED");
    return Results.Ok(snapshot);
});
app.MapPost("/api/system/emergency-stop", async (SystemStateMachine system, EventRepository events) =>
{
    var snapshot = system.EmergencyLock(DateTimeOffset.UtcNow, "User activated emergency stop");
    await RecordSystemEvent(events, snapshot, "EMERGENCY_LOCKED");
    return Results.Ok(snapshot);
});
app.MapPost("/api/system/user-unlock", async (SystemStateMachine system, EventRepository events) =>
{
    var snapshot = system.UserUnlockToPaused(DateTimeOffset.UtcNow, "User explicitly unlocked emergency state");
    await RecordSystemEvent(events, snapshot, "EMERGENCY_USER_UNLOCKED");
    return Results.Ok(snapshot);
});

app.MapGet("/api/mt5/status", (Mt5CommonFilesHeartbeatReader heartbeat) =>
{
    var snapshot = heartbeat.Read(DateTimeOffset.UtcNow);
    return Results.Ok(new { connected = snapshot.Connected && snapshot.IsFresh, accountMode = snapshot.AccountMode.ToString().ToUpperInvariant(), accountType = snapshot.PositionMode.ToString().ToUpperInvariant(), emergencyLocked = snapshot.EmergencyLocked, heartbeatAt = snapshot.ObservedAt == DateTimeOffset.MinValue ? (DateTimeOffset?)null : snapshot.ObservedAt, allowsOrderTransmission = snapshot.AllowsOrderTransmission });
});
app.MapGet("/api/mt5/symbols", (Mt5CommonFilesSymbolReader symbols) => symbols.Read());
app.MapGet("/api/positions", (Mt5CommonFilesExecutionSnapshotReader snapshots) =>
    Results.Ok(snapshots.ReadPositions()?.Items ?? []));
app.MapGet("/api/orders", (Mt5CommonFilesExecutionSnapshotReader snapshots) =>
    Results.Ok(snapshots.ReadOrders()?.Items ?? []));

app.MapGet("/api/setup/status", async (InstrumentRepository instruments, Mt5CommonFilesHeartbeatReader heartbeat) =>
{
    var mt5 = heartbeat.Read(DateTimeOffset.UtcNow);
    var mappings = await instruments.ListAsync();
    var mapped = mappings.Any(x => x.Enabled && !string.IsNullOrWhiteSpace(x.BrokerSymbol));
    var steps = new[]
    {
        new { id = "database", label = "Local database", complete = true, detail = databasePath },
        new { id = "mt5", label = "Connected DEMO account", complete = mt5.IsPositiveDemo, detail = mt5.AccountMode.ToString() },
        new { id = "emergency", label = "EA safety lock verified", complete = mt5.EmergencyLocked, detail = mt5.EmergencyLocked ? "Locked" : "Unlocked" },
        new { id = "mapping", label = "At least one enabled symbol mapping", complete = mapped, detail = mapped ? "Configured" : "Required" }
    };
    return Results.Ok(new { complete = steps.All(x => x.complete), steps });
});

app.MapGet("/api/instruments", async (InstrumentRepository instruments) =>
    (await instruments.ListAsync()).Select(x => new { id = x.Id.ToString("D"), logicalSymbol = x.LogicalSymbol, brokerSymbol = x.BrokerSymbol, enabled = x.Enabled, valid = !string.IsNullOrWhiteSpace(x.BrokerSymbol) }));
app.MapPut("/api/instruments/{id:guid}", async (Guid id, InstrumentMappingRequest request, InstrumentRepository instruments) =>
{
    if (request.Id != id || string.IsNullOrWhiteSpace(request.LogicalSymbol)) return Results.BadRequest();
    var record = new InstrumentRecord { Id = id, LogicalSymbol = request.LogicalSymbol, BrokerSymbol = string.IsNullOrWhiteSpace(request.BrokerSymbol) ? null : request.BrokerSymbol.Trim(), Enabled = request.Enabled && !string.IsNullOrWhiteSpace(request.BrokerSymbol), UpdatedAt = DateTimeOffset.UtcNow };
    await instruments.UpsertAsync(record);
    return Results.Ok(new { id = record.Id.ToString("D"), record.LogicalSymbol, record.BrokerSymbol, record.Enabled, valid = !string.IsNullOrWhiteSpace(record.BrokerSymbol) });
});
app.MapPost("/api/instruments/{id:guid}/pause", async (Guid id, InstrumentRepository instruments) =>
{
    var current = (await instruments.ListAsync()).SingleOrDefault(x => x.Id == id);
    if (current is null) return Results.NotFound();
    current.Enabled = false;
    current.UpdatedAt = DateTimeOffset.UtcNow;
    await instruments.UpsertAsync(current);
    return Results.Ok(new { id = current.Id.ToString("D"), current.LogicalSymbol, current.BrokerSymbol, enabled = false, valid = !string.IsNullOrWhiteSpace(current.BrokerSymbol) });
});

app.MapGet("/api/risk/policy", async (RiskPolicyRepository policies) =>
{
    var current = await policies.GetCurrentAsync();
    return current is null ? Results.Ok(new { configured = false, source = "conservative-demo-defaults", policy = new RiskPolicy() }) : Results.Ok(new { configured = true, source = "user-versioned", version = current.Version, policyJson = current.PolicyJson, current.EffectiveAt, current.ChangedBy, current.ChangeReason });
});
app.MapGet("/api/risk/portfolio", (Mt5CommonFilesExecutionSnapshotReader snapshots) =>
{
    var positions = snapshots.ReadPositions();
    var items = positions?.Items ?? [];
    return Results.Ok(new
    {
        snapshotAt = positions?.ObservedAt,
        positionCount = items.Count,
        symbols = items.GroupBy(x => x.Symbol).Select(group => new { symbol = group.Key, positions = group.Count(), volume = group.Sum(x => x.Volume), profit = group.Sum(x => x.Profit) }),
        totalProfit = items.Sum(x => x.Profit)
    });
});

app.MapGet("/api/audit", async (EventRepository events) => await events.ReadAuditAsync());
app.MapPost("/api/positions/close-all", () => Results.Conflict(new { accepted = false, reason = "Position reconciliation and verified close workflow are not available yet." }));
app.MapPost("/api/positions/{ticket:long}/close", (long ticket) => Results.Conflict(new { accepted = false, ticket, reason = "Close requires persisted attribution and a fresh reconciled broker snapshot." }));
app.MapPost("/api/strategies/{id}/pause", (string id) => Results.StatusCode(StatusCodes.Status501NotImplemented));
app.MapPost("/api/trades/simulate", (TradeRequest request, TradeCoordinator coordinator) =>
{
    var trade = new CandidateTrade(Guid.NewGuid(), request.StrategyId, 1, request.Symbol, request.Side, request.Entry, request.Stop, request.Target, request.Spread, DateTimeOffset.UtcNow, 1, "Manual demo simulation");
    var metadata = new SymbolMetadata(request.Symbol, request.TickSize, request.TickValue, 1, request.VolumeMin, request.VolumeMax, request.VolumeStep);
    return coordinator.Submit(trade, metadata, new(request.Equity, 0, []), request.ExposureGroup, DateTimeOffset.UtcNow);
});

app.Run();

static Task RecordSystemEvent(EventRepository events, SystemStateSnapshot snapshot, string type) => events.AppendSystemEventAsync(new SystemEventRecord { OccurredAt = snapshot.ChangedAt, Severity = type.StartsWith("EMERGENCY", StringComparison.Ordinal) ? "Critical" : "Information", EventType = type, Detail = snapshot.Reason });

public sealed record InstrumentMappingRequest(Guid Id, string LogicalSymbol, string? BrokerSymbol, bool Enabled);
public sealed record TradeRequest(string StrategyId, string Symbol, Side Side, decimal Entry, decimal Stop, decimal Target, decimal Spread, decimal Equity, decimal TickSize, decimal TickValue, decimal VolumeMin, decimal VolumeMax, decimal VolumeStep, string ExposureGroup);
public partial class Program;
