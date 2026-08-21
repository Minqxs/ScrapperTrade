using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ScrapperTrade.Application;
using ScrapperTrade.Domain;
using ScrapperTrade.Infrastructure;
using ScrapperTrade.Infrastructure.Knowledge;
using ScrapperTrade.Infrastructure.Persistence;
using ScrapperTrade.Infrastructure.StrategyGovernance;

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
builder.Services.AddSingleton(new KnowledgeFileStore(Path.Combine(Path.GetDirectoryName(databasePath)!, "knowledge", "files")));
builder.Services.AddSingleton<IShadowStrategyStateStore>(_ => new JsonShadowStrategyStateStore(Path.Combine(Path.GetDirectoryName(databasePath)!, "shadow-decisions.json")));
builder.Services.AddScoped(_ => new ScrapperTradeDbContext(PersistenceBootstrap.CreateOptions(databasePath)));
builder.Services.AddScoped<InstrumentRepository>();
builder.Services.AddScoped<EventRepository>();
builder.Services.AddScoped<ConfigurationRepository>();
builder.Services.AddScoped<TradingUniverseRepository>();
builder.Services.AddScoped<RiskPolicyRepository>();
builder.Services.AddScoped<PositionRepository>();
builder.Services.AddScoped<KnowledgeService>();
builder.Services.AddScoped<StrategyDefinitionRepository>();
builder.Services.AddScoped<BacktestEvidenceRepository>();
builder.Services.AddScoped<ResearchGovernanceRepository>();
builder.Services.AddScoped<ShadowComparisonRepository>();
builder.Services.AddScoped<UserStrategyGovernanceRepository>();

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
app.MapGet("/api/system/events", async (EventRepository events) => await events.ReadSystemEventsAsync());
app.MapGet("/api/health/diagnostics", async (ScrapperTradeDbContext db, Mt5CommonFilesHeartbeatReader heartbeat) =>
{
    var now = DateTimeOffset.UtcNow;
    var mt5 = heartbeat.Read(now);
    var databaseHealthy = await db.Database.CanConnectAsync();
    var checks = new[]
    {
        new { id = "database", label = "Local database", status = databaseHealthy ? "HEALTHY" : "UNHEALTHY", detail = databaseHealthy ? "SQLite is reachable and migrations are applied." : "SQLite is unavailable.", lastSuccessAt = databaseHealthy ? now : (DateTimeOffset?)null, recovery = databaseHealthy ? null : "Restart the host and run scripts/doctor.ps1." },
        new { id = "mt5", label = "MT5 DEMO heartbeat", status = mt5.IsPositiveDemo ? "HEALTHY" : "DEGRADED", detail = mt5.IsPositiveDemo ? "Fresh connected DEMO account verified." : "No fresh positive DEMO heartbeat; execution remains blocked.", lastSuccessAt = mt5.IsPositiveDemo ? mt5.ObservedAt : (DateTimeOffset?)null, recovery = mt5.IsPositiveDemo ? null : "Refresh or reattach the locked EA on the DEMO account." },
        new { id = "execution", label = "Execution authority", status = "DEGRADED", detail = "Broker transmission is intentionally locked during validation.", lastSuccessAt = (DateTimeOffset?)null, recovery = (string?)"Complete release validation before any explicit DEMO unlock." }
    };
    return Results.Ok(new { overall = checks.Any(x => x.status == "UNHEALTHY") ? "UNHEALTHY" : checks.Any(x => x.status == "DEGRADED") ? "DEGRADED" : "HEALTHY", checkedAt = now, checks });
});
app.MapGet("/api/recovery/status", async (ScrapperTradeDbContext db, Mt5CommonFilesExecutionSnapshotReader snapshots) =>
{
    var databaseHealthy = await db.Database.CanConnectAsync();
    var positions = snapshots.ReadPositions();
    return Results.Ok(new { cleanShutdown = (bool?)null, reconciliationRequired = positions is null, queueDepth = 0, staleCommands = 0, lastBackupAt = (DateTimeOffset?)null, databaseStatus = databaseHealthy ? "HEALTHY" : "UNHEALTHY", detail = positions is null ? "A fresh MT5 position snapshot is required before broker actions." : "Broker snapshot is available; no command was sent." });
});
app.MapGet("/api/settings/providers", async (ConfigurationRepository configuration) =>
{
    var openAiEnabled = string.Equals((await configuration.FindAsync("provider.openai.enabled"))?.Value, "true", StringComparison.OrdinalIgnoreCase);
    var openAiModel = (await configuration.FindAsync("provider.openai.model"))?.Value;
    var openAiConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")) && !string.IsNullOrWhiteSpace(openAiModel);
    return Results.Ok(new object[]
    {
        new { id = "MANUAL_CHATGPT", name = "Manual ChatGPT", enabled = true, configured = true, optional = false, status = "READY", detail = "Copy/import workflow; no API billing required.", model = (string?)null },
        new { id = "LOCAL", name = "Local provider", enabled = false, configured = false, optional = true, status = "NOT_CONFIGURED", detail = "No supported local model endpoint is configured.", model = (string?)null },
        new { id = "OPENAI", name = "OpenAI API", enabled = openAiEnabled && openAiConfigured, configured = openAiConfigured, optional = true, status = openAiConfigured ? openAiEnabled ? "READY" : "DISABLED" : "NOT_CONFIGURED", detail = openAiConfigured ? "Credential detected outside the browser and repository." : "Set a newly rotated OPENAI_API_KEY for the host process and choose a model.", model = openAiModel }
    });
});
app.MapPut("/api/settings/providers/{id}", async (string id, ProviderUpdateRequest request, ConfigurationRepository configuration) =>
{
    if (!string.Equals(id, "OPENAI", StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new { message = "Only the optional OpenAI provider is configurable in this build." });
    if (request.Model?.Length > 100) return Results.BadRequest(new { message = "Model identifier is too long." });
    if (request.Enabled && (string.IsNullOrWhiteSpace(request.Model) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))))
        return Results.Conflict(new { message = "A newly rotated host-side OPENAI_API_KEY and explicit model are required before enabling this optional provider." });
    await configuration.SetAsync("provider.openai.enabled", request.Enabled ? "true" : "false", DateTimeOffset.UtcNow);
    if (!string.IsNullOrWhiteSpace(request.Model)) await configuration.SetAsync("provider.openai.model", request.Model.Trim(), DateTimeOffset.UtcNow);
    return Results.Ok(new { id = "OPENAI", name = "OpenAI API", enabled = request.Enabled, configured = true, optional = true, status = request.Enabled ? "READY" : "DISABLED", model = request.Model?.Trim() });
});
app.MapPost("/api/positions/close-all", () => Results.Conflict(new { accepted = false, reason = "Position reconciliation and verified close workflow are not available yet." }));
app.MapPost("/api/positions/{ticket:long}/close", (long ticket) => Results.Conflict(new { accepted = false, ticket, reason = "Close requires persisted attribution and a fresh reconciled broker snapshot." }));
app.MapPost("/api/strategies/{id}/pause", (string id) => Results.StatusCode(StatusCodes.Status501NotImplemented));
app.MapGet("/api/autonomy/status", (IShadowStrategyStateStore shadow) =>
{
    var decisions = shadow.ReadAll();
    return Results.Ok(new
    {
        mode = "SHADOW",
        executionEnabled = false,
        brokerOrdersAllowed = false,
        scheduler = "deterministic-simulator-only",
        lastDecisionAt = decisions.LastOrDefault()?.EvaluatedAt,
        strategies = decisions.GroupBy(x => x.StrategyId).Select(group => new { id = group.Key, name = group.Key, mode = "SHADOW", status = group.Last().Status.ToString().ToUpperInvariant(), reason = group.Last().Reason })
    });
});

app.MapGet("/api/knowledge/sources", async (ScrapperTradeDbContext db) =>
{
    var documents = await db.KnowledgeDocuments.AsNoTracking().Where(x => x.DeletedAt == null)
        .Select(x => new { x.Id, x.Title, x.OriginalFileName, x.IngestedAt, Chunks = x.Chunks.Count }).ToListAsync();
    return documents.OrderByDescending(x => x.IngestedAt).ThenBy(x => x.Id)
        .Select(x => new { id = x.Id.ToString(), title = x.Title, kind = "DOCUMENT", status = "READY", fileName = x.OriginalFileName, createdAt = x.IngestedAt, chunks = x.Chunks, citationCount = 0, error = (string?)null });
});
app.MapGet("/api/knowledge/search", async (string? q, KnowledgeService knowledge) =>
{
    var hits = await knowledge.SearchAsync(q ?? string.Empty);
    return Results.Ok(hits.Select(x => new { id = x.ChunkId.ToString(), sourceId = x.DocumentId.ToString(), sourceTitle = x.DocumentTitle, excerpt = x.Snippet, locator = x.Citation, score = 1m }));
});
app.MapPost("/api/knowledge/sources", async (IFormFile file, KnowledgeService knowledge) =>
{
    try
    {
        await using var stream = file.OpenReadStream();
        var result = await knowledge.IngestAsync(stream, file.FileName, Path.GetFileNameWithoutExtension(file.FileName), "Local upload", [], DateTimeOffset.UtcNow);
        return Results.Ok(new { id = result.DocumentId.ToString(), title = Path.GetFileNameWithoutExtension(file.FileName), kind = "DOCUMENT", status = "READY", fileName = file.FileName, createdAt = DateTimeOffset.UtcNow, chunks = result.ChunkCount, citationCount = 0, error = (string?)null });
    }
    catch (KnowledgeIngestionException exception)
    {
        return Results.BadRequest(new { message = exception.Message, code = exception.Code });
    }
}).DisableAntiforgery();

app.MapGet("/api/strategies", async (ScrapperTradeDbContext db) =>
    Results.Ok((await db.StrategyDefinitions.AsNoTracking().Include(x => x.Versions).OrderBy(x => x.Key).ToListAsync())
        .Select(x => JsonSerializer.Deserialize<JsonElement>(x.Versions.OrderByDescending(v => v.Version).First().SpecificationJson)).ToArray()));
app.MapGet("/api/strategies/{id}", async (string id, ScrapperTradeDbContext db) =>
{
    var definition = await db.StrategyDefinitions.AsNoTracking().Include(x => x.Versions).SingleOrDefaultAsync(x => x.Key == id.ToLower());
    return definition is null ? Results.NotFound(new { message = $"Strategy '{id}' was not found." }) : Results.Ok(JsonSerializer.Deserialize<JsonElement>(definition.Versions.OrderByDescending(x => x.Version).First().SpecificationJson));
});
app.MapPut("/api/strategies/{id}", async (string id, JsonElement strategy, ScrapperTradeDbContext db, StrategyDefinitionRepository definitions) =>
{
    if (strategy.ValueKind != JsonValueKind.Object) return Results.BadRequest(new { message = "A constrained strategy object is required." });
    var name = strategy.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
    if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest(new { message = "Strategy name is required." });
    var json = strategy.GetRawText();
    var current = await db.StrategyDefinitions.SingleOrDefaultAsync(x => x.Key == id.ToLower());
    if (current is null) await definitions.CreateAsync(id, name, strategy.TryGetProperty("description", out var description) ? description.GetString() ?? "" : "", json, DateTimeOffset.UtcNow);
    else await definitions.AddImmutableVersionAsync(current.Id, json, await db.StrategyVersions.Where(x => x.StrategyDefinitionId == current.Id).OrderByDescending(x => x.Version).Select(x => (Guid?)x.Id).FirstAsync(), null, "User saved a new immutable strategy version.", DateTimeOffset.UtcNow);
    return Results.Ok(strategy);
});
app.MapGet("/api/backtests", async (ScrapperTradeDbContext db) => Results.Ok((await db.BacktestRuns.AsNoTracking().Include(x => x.Metrics).ToListAsync()).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToArray()));
app.MapGet("/api/backtests/{id:guid}", async (Guid id, ScrapperTradeDbContext db) =>
{
    var run = await db.BacktestRuns.AsNoTracking().Include(x => x.Metrics).Include(x => x.Trades).SingleOrDefaultAsync(x => x.Id == id);
    return run is null ? Results.NotFound(new { message = $"Backtest '{id}' was not found." }) : Results.Ok(run);
});
app.MapPost("/api/backtests", () => Results.Conflict(new { message = "A historical dataset must be selected before a persisted backtest can be queued." }));
app.MapGet("/api/research/candidates", async (ScrapperTradeDbContext db) => Results.Ok((await db.ResearchCandidates.AsNoTracking().Include(x => x.Provenance).ToListAsync()).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToArray()));
app.MapPost("/api/research/candidates/{id:guid}/approve-validation", async (Guid id, ScrapperTradeDbContext db) =>
{
    var candidate = await db.ResearchCandidates.AsNoTracking().Include(x => x.Provenance).SingleOrDefaultAsync(x => x.Id == id);
    if (candidate is null) return Results.NotFound(new { message = $"Research candidate '{id}' was not found." });
    return Results.Conflict(new { message = candidate.HasUnresolvedAmbiguities ? "Unresolved ambiguities block validation approval." : "Passed validation evidence is required; approval cannot activate or promote a strategy." });
});
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
public sealed record ProviderUpdateRequest(bool Enabled, string? Model);
public partial class Program;
