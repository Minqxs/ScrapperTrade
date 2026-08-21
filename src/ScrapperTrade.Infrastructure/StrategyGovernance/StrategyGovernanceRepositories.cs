using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ScrapperTrade.Infrastructure.Persistence;

namespace ScrapperTrade.Infrastructure.StrategyGovernance;

public sealed class StrategyDefinitionRepository(ScrapperTradeDbContext db)
{
    public async Task<StrategyVersionRecord> CreateAsync(string key, string name, string description, string specificationJson, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key); ArgumentException.ThrowIfNullOrWhiteSpace(name); ArgumentException.ThrowIfNullOrWhiteSpace(specificationJson);
        var definition = new StrategyDefinitionRecord { Id = Guid.NewGuid(), Key = key.Trim().ToLowerInvariant(), Name = name.Trim(), Description = description.Trim(), CreatedAt = now };
        var version = NewVersion(definition.Id, 1, specificationJson, now); definition.Versions.Add(version); db.StrategyDefinitions.Add(definition); await db.SaveChangesAsync(cancellationToken); return version;
    }

    public async Task<StrategyVersionRecord> AddImmutableVersionAsync(Guid definitionId, string specificationJson, Guid? parentVersionId, Guid? candidateId, string reason, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(specificationJson); ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await db.StrategyDefinitions.AnyAsync(x => x.Id == definitionId, cancellationToken)) throw new KeyNotFoundException("Strategy definition not found.");
        var next = (await db.StrategyVersions.Where(x => x.StrategyDefinitionId == definitionId).MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0) + 1;
        var version = NewVersion(definitionId, next, specificationJson, now); db.StrategyVersions.Add(version);
        if (parentVersionId is Guid parent) db.StrategyLineage.Add(new StrategyLineageRecord { ParentVersionId = parent, ChildVersionId = version.Id, ResearchCandidateId = candidateId, Relationship = "DERIVED_FROM", Reason = reason, CreatedAt = now });
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return version;
    }

    private static StrategyVersionRecord NewVersion(Guid definitionId, int version, string json, DateTimeOffset now) => new() { Id = Guid.NewGuid(), StrategyDefinitionId = definitionId, Version = version, SpecificationJson = json, SpecificationHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant(), LifecycleStatus = StrategyStates.Draft, CreatedAt = now };
}

public sealed class BacktestEvidenceRepository(ScrapperTradeDbContext db)
{
    public async Task AddCompletedRunAsync(BacktestRunRecord run, BacktestMetricRecord metrics, IReadOnlyCollection<BacktestTradeRecord> trades, CancellationToken cancellationToken = default)
    {
        if (run.PeriodEnd <= run.PeriodStart) throw new ArgumentException("Backtest period must be positive.");
        if (metrics.TradeCount != trades.Count) throw new ArgumentException("Trade count must match persisted trades.");
        run.Id = run.Id == Guid.Empty ? Guid.NewGuid() : run.Id; run.Status = "COMPLETED"; metrics.BacktestRunId = run.Id; run.Metrics = metrics;
        foreach (var trade in trades) { trade.BacktestRunId = run.Id; run.Trades.Add(trade); }
        db.BacktestRuns.Add(run); await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ResearchGovernanceRepository(ScrapperTradeDbContext db)
{
    public async Task<ResearchCandidateRecord> AddCandidateAsync(Guid versionId, string hypothesis, string ambiguitiesJson, bool hasUnresolvedAmbiguities, IReadOnlyCollection<ResearchCandidateProvenanceRecord> provenance, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hypothesis); if (provenance.Count == 0) throw new ArgumentException("Research candidates require provenance.", nameof(provenance));
        var candidate = new ResearchCandidateRecord { Id = Guid.NewGuid(), StrategyVersionId = versionId, Hypothesis = hypothesis, AmbiguitiesJson = ambiguitiesJson, HasUnresolvedAmbiguities = hasUnresolvedAmbiguities, Status = hasUnresolvedAmbiguities ? "AMBIGUOUS" : "PROPOSED", CreatedAt = now };
        foreach (var item in provenance) { item.ResearchCandidateId = candidate.Id; candidate.Provenance.Add(item); }
        db.ResearchCandidates.Add(candidate); await db.SaveChangesAsync(cancellationToken); return candidate;
    }

    public async Task ApproveValidationEvidenceAsync(Guid candidateId, StrategyValidationRunRecord validation, string researchActorId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(researchActorId);
        var candidate = await db.ResearchCandidates.Include(x => x.StrategyVersion).SingleOrDefaultAsync(x => x.Id == candidateId, cancellationToken) ?? throw new KeyNotFoundException("Research candidate not found.");
        if (candidate.HasUnresolvedAmbiguities) throw new InvalidOperationException("Unresolved ambiguities block validation approval.");
        if (!string.Equals(validation.Status, "PASSED", StringComparison.Ordinal)) throw new InvalidOperationException("Only passed validation evidence can be approved.");
        if (validation.StrategyVersionId != candidate.StrategyVersionId) throw new InvalidOperationException("Validation evidence belongs to another strategy version.");
        var from = candidate.StrategyVersion.LifecycleStatus; candidate.Status = "VALIDATION_APPROVED"; candidate.StrategyVersion.LifecycleStatus = StrategyStates.Validated;
        validation.Id = validation.Id == Guid.Empty ? Guid.NewGuid() : validation.Id; validation.CompletedAt ??= now; db.StrategyValidationRuns.Add(validation);
        db.StrategyGovernanceAudit.Add(new StrategyGovernanceAuditRecord { ActorType = "RESEARCH", ActorId = researchActorId, Action = "VALIDATION_APPROVED", StrategyVersionId = candidate.StrategyVersionId, FromStatus = from, ToStatus = StrategyStates.Validated, Reason = "Passed validation evidence recorded; execution remains inactive.", OccurredAt = now });
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ShadowComparisonRepository(ScrapperTradeDbContext db)
{
    public async Task<ShadowComparisonRecord> StartAsync(Guid championVersionId, Guid challengerVersionId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (championVersionId == challengerVersionId) throw new ArgumentException("Champion and challenger must differ.");
        var versions = await db.StrategyVersions.Where(x => x.Id == championVersionId || x.Id == challengerVersionId).ToDictionaryAsync(x => x.Id, cancellationToken);
        if (!versions.TryGetValue(championVersionId, out var champion) || champion.LifecycleStatus != StrategyStates.Active) throw new InvalidOperationException("Champion must be active.");
        if (!versions.TryGetValue(challengerVersionId, out var challenger) || challenger.LifecycleStatus != StrategyStates.Validated) throw new InvalidOperationException("Challenger must be validated.");
        var comparison = new ShadowComparisonRecord { Id = Guid.NewGuid(), ChampionVersionId = championVersionId, ChallengerVersionId = challengerVersionId, Status = "RUNNING", StartedAt = now };
        db.ShadowComparisons.Add(comparison); await db.SaveChangesAsync(cancellationToken); return comparison;
    }

    public async Task CompleteAsync(Guid comparisonId, string championMetricsJson, string challengerMetricsJson, string decisionEvidenceJson, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var comparison = await db.ShadowComparisons.SingleOrDefaultAsync(x => x.Id == comparisonId, cancellationToken) ?? throw new KeyNotFoundException("Shadow comparison not found.");
        if (comparison.Status != "RUNNING") throw new InvalidOperationException("Shadow comparison is not running.");
        comparison.Status = "COMPLETED"; comparison.ChampionMetricsJson = championMetricsJson; comparison.ChallengerMetricsJson = challengerMetricsJson; comparison.DecisionEvidenceJson = decisionEvidenceJson; comparison.CompletedAt = now; await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class UserStrategyGovernanceRepository(ScrapperTradeDbContext db)
{
    public async Task ActivateValidatedVersionAsync(Guid versionId, string userId, Guid confirmationId, string reason, DateTimeOffset now, Guid? completedShadowComparisonId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId); ArgumentException.ThrowIfNullOrWhiteSpace(reason); if (confirmationId == Guid.Empty) throw new ArgumentException("Explicit user confirmation is required.", nameof(confirmationId));
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await db.StrategyGovernanceAudit.AnyAsync(x => x.ConfirmationId == confirmationId, cancellationToken)) throw new InvalidOperationException("User confirmation was already consumed.");
        var version = await db.StrategyVersions.SingleOrDefaultAsync(x => x.Id == versionId, cancellationToken) ?? throw new KeyNotFoundException("Strategy version not found.");
        if (version.LifecycleStatus != StrategyStates.Validated) throw new InvalidOperationException("Only a validated version can be activated by the user.");
        if (completedShadowComparisonId is Guid comparisonId)
        {
            var comparison = await db.ShadowComparisons.SingleOrDefaultAsync(x => x.Id == comparisonId, cancellationToken);
            if (comparison is null || comparison.Status != "COMPLETED" || comparison.ChallengerVersionId != versionId) throw new InvalidOperationException("A completed matching shadow comparison is required.");
        }
        var previous = await db.StrategyActivations.SingleOrDefaultAsync(x => x.StrategyDefinitionId == version.StrategyDefinitionId && x.DeactivatedAt == null, cancellationToken);
        if (previous is not null)
        {
            previous.DeactivatedAt = now; var oldVersion = await db.StrategyVersions.SingleAsync(x => x.Id == previous.StrategyVersionId, cancellationToken); oldVersion.LifecycleStatus = StrategyStates.Superseded;
        }
        var from = version.LifecycleStatus; version.LifecycleStatus = StrategyStates.Active;
        db.StrategyActivations.Add(new StrategyActivationRecord { StrategyDefinitionId = version.StrategyDefinitionId, StrategyVersionId = version.Id, UserConfirmationId = confirmationId, ApprovedByUser = userId, Reason = reason, ActivatedAt = now });
        db.StrategyGovernanceAudit.Add(new StrategyGovernanceAuditRecord { ActorType = "USER", ActorId = userId, Action = previous is null ? "ACTIVATED" : "CHALLENGER_PROMOTED", StrategyVersionId = version.Id, FromStatus = from, ToStatus = StrategyStates.Active, Reason = reason, ConfirmationId = confirmationId, OccurredAt = now });
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }

    public async Task RetireActiveVersionAsync(Guid versionId, string userId, Guid confirmationId, string reason, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId); ArgumentException.ThrowIfNullOrWhiteSpace(reason); if (confirmationId == Guid.Empty) throw new ArgumentException("Explicit user confirmation is required.", nameof(confirmationId));
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var version = await db.StrategyVersions.SingleOrDefaultAsync(x => x.Id == versionId, cancellationToken) ?? throw new KeyNotFoundException("Strategy version not found.");
        if (version.LifecycleStatus != StrategyStates.Active) throw new InvalidOperationException("Only an active version can be retired.");
        var activation = await db.StrategyActivations.SingleAsync(x => x.StrategyVersionId == versionId && x.DeactivatedAt == null, cancellationToken);
        if (await db.StrategyGovernanceAudit.AnyAsync(x => x.ConfirmationId == confirmationId, cancellationToken)) throw new InvalidOperationException("User confirmation was already consumed.");
        activation.DeactivatedAt = now; version.LifecycleStatus = StrategyStates.Retired;
        db.StrategyGovernanceAudit.Add(new StrategyGovernanceAuditRecord { ActorType = "USER", ActorId = userId, Action = "RETIRED", StrategyVersionId = version.Id, FromStatus = StrategyStates.Active, ToStatus = StrategyStates.Retired, Reason = reason, ConfirmationId = confirmationId, OccurredAt = now });
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
}
