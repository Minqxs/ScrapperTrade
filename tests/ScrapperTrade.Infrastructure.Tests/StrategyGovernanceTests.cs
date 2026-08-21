using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScrapperTrade.Infrastructure.Persistence;
using ScrapperTrade.Infrastructure.StrategyGovernance;
using Xunit;

namespace ScrapperTrade.Infrastructure.Tests;

public sealed class StrategyGovernanceTests : IAsyncLifetime
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ScrapperTrade.Governance.Tests", Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(directory, "test.db");
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() { SqliteConnection.ClearAllPools(); if (Directory.Exists(directory)) Directory.Delete(directory, true); return Task.CompletedTask; }

    [Fact]
    public async Task Migration_creates_complete_research_and_governance_schema()
    {
        await PersistenceBootstrap.MigrateAsync(DatabasePath); await using var db = CreateDb();
        var tables = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToListAsync();
        foreach (var table in new[] { "strategy_definitions", "strategy_versions", "backtest_runs", "backtest_metrics", "backtest_trades", "strategy_validation_runs", "research_candidates", "research_candidate_provenance", "strategy_lineage", "shadow_comparisons", "strategy_activations", "strategy_governance_audit" }) Assert.Contains(table, tables);
    }

    [Fact]
    public async Task Strategy_versions_are_sequential_hashed_and_lineage_linked()
    {
        await PersistenceBootstrap.MigrateAsync(DatabasePath); await using var db = CreateDb(); var repository = new StrategyDefinitionRepository(db); var now = Now();
        var first = await repository.CreateAsync("breakout", "Breakout", "Hypothesis", "{\"period\":20}", now);
        var second = await repository.AddImmutableVersionAsync(first.StrategyDefinitionId, "{\"period\":30}", first.Id, null, "Parameter challenger", now.AddMinutes(1));
        Assert.Equal(1, first.Version); Assert.Equal(2, second.Version); Assert.NotEqual(first.SpecificationHash, second.SpecificationHash);
        var lineage = await db.StrategyLineage.AsNoTracking().SingleAsync(); Assert.Equal(first.Id, lineage.ParentVersionId); Assert.Equal(second.Id, lineage.ChildVersionId);
    }

    [Fact]
    public async Task Backtest_evidence_keeps_costs_metrics_out_of_sample_and_trades_distinct()
    {
        await PersistenceBootstrap.MigrateAsync(DatabasePath); await using var db = CreateDb(); var version = await CreateVersion(db); var repository = new BacktestEvidenceRepository(db); var now = Now();
        var run = new BacktestRunRecord { StrategyVersionId = version.Id, DatasetReference = "EURUSD-M1-2024", CostModelJson = "{\"spread\":0.2,\"slippage\":0.1}", IsOutOfSample = true, PeriodStart = now.AddDays(-30), PeriodEnd = now, CreatedAt = now };
        var trades = new[] { new BacktestTradeRecord { Instrument = "EURUSD", Side = "BUY", EnteredAt = now.AddDays(-2), ExitedAt = now.AddDays(-2).AddHours(1), EntryPrice = 1.1m, ExitPrice = 1.101m, RealisedR = 1.2m, CostAmount = 2m } };
        await repository.AddCompletedRunAsync(run, new BacktestMetricRecord { TradeCount = 1, ExpectancyR = .2m, ProfitFactor = 1.1m, MaximumDrawdownFraction = .05m, NetReturnFraction = .02m }, trades);
        var saved = await db.BacktestRuns.Include(x => x.Metrics).Include(x => x.Trades).SingleAsync(); Assert.True(saved.IsOutOfSample); Assert.Equal("COMPLETED", saved.Status); Assert.Equal(2m, saved.Trades[0].CostAmount); Assert.Equal(.2m, saved.Metrics!.ExpectancyR);
    }

    [Fact]
    public async Task Research_requires_provenance_and_ambiguities_block_validation()
    {
        await PersistenceBootstrap.MigrateAsync(DatabasePath); await using var db = CreateDb(); var version = await CreateVersion(db); var research = new ResearchGovernanceRepository(db); var now = Now();
        await Assert.ThrowsAsync<ArgumentException>(() => research.AddCandidateAsync(version.Id, "No evidence", "[]", false, [], now));
        var candidate = await research.AddCandidateAsync(version.Id, "Session breakout", "[\"session timezone unclear\"]", true, [Provenance()], now);
        var validation = PassedValidation(version.Id, now);
        await Assert.ThrowsAsync<InvalidOperationException>(() => research.ApproveValidationEvidenceAsync(candidate.Id, validation, "research-loop", now));
        Assert.Equal(StrategyStates.Draft, (await db.StrategyVersions.FindAsync(version.Id))!.LifecycleStatus);
    }

    [Fact]
    public async Task Research_can_validate_but_has_no_activation_capability()
    {
        await PersistenceBootstrap.MigrateAsync(DatabasePath); await using var db = CreateDb(); var version = await CreateVersion(db); var research = new ResearchGovernanceRepository(db); var now = Now();
        var candidate = await research.AddCandidateAsync(version.Id, "Evidence-backed candidate", "[]", false, [Provenance()], now);
        await research.ApproveValidationEvidenceAsync(candidate.Id, PassedValidation(version.Id, now), "research-loop", now);
        Assert.Equal(StrategyStates.Validated, (await db.StrategyVersions.FindAsync(version.Id))!.LifecycleStatus);
        Assert.Empty(await db.StrategyActivations.ToListAsync());
        Assert.DoesNotContain(typeof(ResearchGovernanceRepository).GetMethods(), x => x.Name.Contains("Activat", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Promot", StringComparison.OrdinalIgnoreCase));
        var audit = await db.StrategyGovernanceAudit.SingleAsync(); Assert.Equal("RESEARCH", audit.ActorType); Assert.Equal(StrategyStates.Validated, audit.ToStatus);
    }

    [Fact]
    public async Task Only_explicit_user_confirmation_activates_and_audits_a_validated_version()
    {
        await PersistenceBootstrap.MigrateAsync(DatabasePath); await using var db = CreateDb(); var version = await CreateValidatedVersion(db); var users = new UserStrategyGovernanceRepository(db); var now = Now();
        await Assert.ThrowsAsync<ArgumentException>(() => users.ActivateValidatedVersionAsync(version.Id, "operator", Guid.Empty, "Approve", now));
        var confirmation = Guid.NewGuid(); await users.ActivateValidatedVersionAsync(version.Id, "operator", confirmation, "Reviewed validation evidence", now);
        Assert.Equal(StrategyStates.Active, (await db.StrategyVersions.FindAsync(version.Id))!.LifecycleStatus);
        var activation = await db.StrategyActivations.SingleAsync(); Assert.Equal(confirmation, activation.UserConfirmationId); Assert.Equal("operator", activation.ApprovedByUser);
        var audit = await db.StrategyGovernanceAudit.OrderByDescending(x => x.Id).FirstAsync(); Assert.Equal("USER", audit.ActorType); Assert.Equal(confirmation, audit.ConfirmationId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => users.RetireActiveVersionAsync(version.Id, "operator", confirmation, "Reused confirmation", now.AddDays(1)));
        await users.RetireActiveVersionAsync(version.Id, "operator", Guid.NewGuid(), "User retired strategy", now.AddDays(1));
        Assert.Equal(StrategyStates.Retired, (await db.StrategyVersions.FindAsync(version.Id))!.LifecycleStatus); Assert.Empty(await db.StrategyActivations.Where(x => x.DeactivatedAt == null).ToListAsync());
    }

    [Fact]
    public async Task Challenger_promotion_requires_completed_shadow_comparison_and_user_confirmation()
    {
        await PersistenceBootstrap.MigrateAsync(DatabasePath); await using var db = CreateDb(); var definitions = new StrategyDefinitionRepository(db); var now = Now();
        var champion = await definitions.CreateAsync("trend", "Trend", "Trend hypothesis", "{\"fast\":10}", now); await Validate(db, champion, now);
        var users = new UserStrategyGovernanceRepository(db); await users.ActivateValidatedVersionAsync(champion.Id, "operator", Guid.NewGuid(), "Initial champion", now);
        var challenger = await definitions.AddImmutableVersionAsync(champion.StrategyDefinitionId, "{\"fast\":12}", champion.Id, null, "Robustness challenger", now.AddMinutes(1)); await Validate(db, challenger, now.AddMinutes(1));
        var shadows = new ShadowComparisonRepository(db); var comparison = await shadows.StartAsync(champion.Id, challenger.Id, now.AddDays(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => users.ActivateValidatedVersionAsync(challenger.Id, "operator", Guid.NewGuid(), "Too soon", now.AddDays(1), comparison.Id));
        await shadows.CompleteAsync(comparison.Id, "{\"expectancy\":0.1}", "{\"expectancy\":0.12}", "{\"forwardDays\":30}", now.AddDays(31));
        await users.ActivateValidatedVersionAsync(challenger.Id, "operator", Guid.NewGuid(), "Promote after shadow review", now.AddDays(31), comparison.Id);
        Assert.Equal(StrategyStates.Superseded, (await db.StrategyVersions.FindAsync(champion.Id))!.LifecycleStatus); Assert.Equal(StrategyStates.Active, (await db.StrategyVersions.FindAsync(challenger.Id))!.LifecycleStatus);
        Assert.Single(await db.StrategyActivations.Where(x => x.DeactivatedAt == null).ToListAsync());
    }

    private static DateTimeOffset Now() => DateTimeOffset.Parse("2026-08-21T12:00:00Z");
    private static ResearchCandidateProvenanceRecord Provenance() => new() { SourceType = "KNOWLEDGE_CHUNK", SourceReference = "document:chunk:1", Citation = "Course / Lesson [chunk 1]", Rationale = "Rule trace" };
    private static StrategyValidationRunRecord PassedValidation(Guid versionId, DateTimeOffset now) => new() { StrategyVersionId = versionId, ValidationKind = "WALK_FORWARD_OOS", Status = "PASSED", EvidenceJson = "{\"folds\":5}", CreatedAt = now, CompletedAt = now };
    private static async Task<StrategyVersionRecord> CreateVersion(ScrapperTradeDbContext db) => await new StrategyDefinitionRepository(db).CreateAsync(Guid.NewGuid().ToString("N"), "Strategy", "Hypothesis", "{\"rule\":1}", Now());
    private static async Task Validate(ScrapperTradeDbContext db, StrategyVersionRecord version, DateTimeOffset now) { var research = new ResearchGovernanceRepository(db); var candidate = await research.AddCandidateAsync(version.Id, "Candidate", "[]", false, [Provenance()], now); await research.ApproveValidationEvidenceAsync(candidate.Id, PassedValidation(version.Id, now), "research-loop", now); }
    private static async Task<StrategyVersionRecord> CreateValidatedVersion(ScrapperTradeDbContext db) { var version = await CreateVersion(db); await Validate(db, version, Now()); return version; }
    private ScrapperTradeDbContext CreateDb() => new(PersistenceBootstrap.CreateOptions(DatabasePath));
}
