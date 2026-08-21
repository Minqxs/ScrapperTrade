using ScrapperTrade.Domain;
using ScrapperTrade.Quant;

namespace ScrapperTrade.Application;

public enum GovernedStrategyRole { Shadow, Challenger, Champion }
public enum GovernedStrategyState { Active, AutomaticallyPaused, UserPaused, Retired }
public enum GovernanceActor { User, Automation }

public sealed record StrategyEvidence(
    int OutOfSampleTrades,
    decimal OutOfSampleExpectancyR,
    decimal OutOfSampleMaximumDrawdownR,
    bool RobustnessPassed,
    int ShadowTrades,
    decimal ShadowExpectancyR,
    DateTimeOffset ShadowStartedAt,
    DateTimeOffset EvidenceThrough);

public sealed class GovernedStrategy
{
    private GovernedStrategy(StrategySpec spec, GovernedStrategyRole role, GovernedStrategyState state, bool userEnabled, StrategyEvidence evidence) =>
        (Spec, Role, State, UserEnabled, Evidence) = (spec, role, state, userEnabled, evidence);
    public StrategySpec Spec { get; }
    public GovernedStrategyRole Role { get; }
    public GovernedStrategyState State { get; }
    public bool UserEnabled { get; }
    public StrategyEvidence Evidence { get; }

    public static GovernedStrategy CreateDisabled(StrategySpec spec, GovernedStrategyRole role, GovernedStrategyState state, StrategyEvidence evidence) => new(spec, role, state, false, evidence);
    public GovernedStrategy SetUserEnabled(bool enabled, GovernanceActor actor)
    {
        if (actor != GovernanceActor.User) throw new InvalidOperationException("Only the user may change strategy enablement.");
        return new(Spec, Role, State, enabled, Evidence);
    }
    internal GovernedStrategy AutomaticallyPause() => State == GovernedStrategyState.Active ? new(Spec, Role, GovernedStrategyState.AutomaticallyPaused, UserEnabled, Evidence) : this;
}

public sealed record StrategySelectionAssessment(string StrategyId, int Version, bool Eligible, int Rank, decimal Score, string Code, string Reason);
public sealed record StrategySelectionDecision(Guid DecisionId, string Instrument, MarketRegime Regime, DateTimeOffset At, string? SelectedStrategyId, int? SelectedVersion, IReadOnlyList<StrategySelectionAssessment> Assessments);

public interface IStrategySelectionJournal
{
    StrategySelectionDecision? Find(Guid decisionId);
    void Append(StrategySelectionDecision decision);
    IReadOnlyList<StrategySelectionDecision> ReadAll();
}

public sealed class AppendOnlyStrategySelectionJournal : IStrategySelectionJournal
{
    private readonly object gate = new();
    private readonly List<StrategySelectionDecision> decisions = [];

    public StrategySelectionDecision? Find(Guid decisionId)
    {
        lock (gate) return decisions.SingleOrDefault(x => x.DecisionId == decisionId);
    }

    public void Append(StrategySelectionDecision decision)
    {
        lock (gate)
        {
            if (decisions.Any(x => x.DecisionId == decision.DecisionId)) throw new InvalidOperationException("Selection decisions are immutable and cannot be replaced.");
            decisions.Add(Freeze(decision));
        }
    }

    public IReadOnlyList<StrategySelectionDecision> ReadAll()
    {
        lock (gate) return decisions.ToArray();
    }

    private static StrategySelectionDecision Freeze(StrategySelectionDecision decision) => decision with { Assessments = Array.AsReadOnly(decision.Assessments.ToArray()) };
}

public sealed class DeterministicStrategySelector(IStrategySelectionJournal journal)
{
    public StrategySelectionDecision Select(string instrument, MarketRegime regime, IReadOnlyList<GovernedStrategy> candidates, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(instrument)) throw new ArgumentException("Instrument is required.", nameof(instrument));
        var decisionId = DeterministicDecisionId(instrument, regime, now);
        var prior = journal.Find(decisionId);
        if (prior is not null) return prior;
        var evaluated = candidates.Select(candidate => Evaluate(candidate, regime, now)).ToArray();
        var ranked = evaluated.Where(x => x.Eligible)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.StrategyId, StringComparer.Ordinal)
            .ThenByDescending(x => x.Version)
            .Select((x, index) => x with { Rank = index + 1 }).ToArray();
        var rankByKey = ranked.ToDictionary(x => (x.StrategyId, x.Version));
        var assessments = evaluated.Select(x => rankByKey.GetValueOrDefault((x.StrategyId, x.Version), x)).OrderBy(x => x.Eligible ? x.Rank : int.MaxValue).ThenBy(x => x.StrategyId, StringComparer.Ordinal).ThenByDescending(x => x.Version).ToArray();
        var selected = ranked.FirstOrDefault();
        var decision = new StrategySelectionDecision(decisionId, instrument, regime, now, selected?.StrategyId, selected?.Version, Array.AsReadOnly(assessments));
        journal.Append(decision);
        return decision;
    }

    private static StrategySelectionAssessment Evaluate(GovernedStrategy candidate, MarketRegime regime, DateTimeOffset now)
    {
        var validation = StrategyValidator.Validate(candidate.Spec);
        if (validation.Count > 0) return No("SPEC_INVALID", string.Join(" ", validation));
        if (!candidate.UserEnabled) return No("USER_DISABLED", "The user has not enabled this strategy.");
        if (candidate.State != GovernedStrategyState.Active) return No("STRATEGY_PAUSED", $"Strategy state {candidate.State} is not selectable.");
        if (regime is MarketRegime.Unknown or MarketRegime.Abnormal || !candidate.Spec.Regimes.Contains(regime)) return No("REGIME_MISMATCH", $"Strategy is incompatible with regime {regime}.");
        if (!candidate.Evidence.RobustnessPassed) return No("ROBUSTNESS_REQUIRED", "Robustness validation has not passed.");
        if (candidate.Evidence.OutOfSampleTrades < 5) return No("EVIDENCE_INSUFFICIENT", "At least five out-of-sample trades are required for ranking.");
        if (candidate.Evidence.ShadowStartedAt > candidate.Evidence.EvidenceThrough) return No("EVIDENCE_INVALID", "Shadow evidence interval is invalid.");
        if (candidate.Evidence.EvidenceThrough > now) return No("FUTURE_EVIDENCE", "Future-dated performance evidence is invalid.");
        var roleWeight = candidate.Role switch { GovernedStrategyRole.Champion => 2m, GovernedStrategyRole.Challenger => 1m, _ => 0m };
        var score = candidate.Evidence.OutOfSampleExpectancyR * 100m + candidate.Evidence.ShadowExpectancyR * 25m - candidate.Evidence.OutOfSampleMaximumDrawdownR * 5m + Math.Min(candidate.Evidence.OutOfSampleTrades, 100) / 10m + roleWeight;
        return new(candidate.Spec.Id, candidate.Spec.Version, true, 0, score, "ELIGIBLE", "Regime, user permission, lifecycle, and evidence gates passed.");

        StrategySelectionAssessment No(string code, string reason) => new(candidate.Spec.Id, candidate.Spec.Version, false, 0, decimal.MinValue, code, reason);
    }

    private static Guid DeterministicDecisionId(string instrument, MarketRegime regime, DateTimeOffset now)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{instrument}:{regime}:{now.ToUnixTimeMilliseconds()}"));
        return new(bytes.AsSpan(0, 16));
    }
}

public sealed record PerformanceObservation(DateTimeOffset At, decimal NetR);
public sealed record PerformanceEnvelope(int RollingTrades = 20, int MinimumTradesBeforeEnforcement = 10, decimal MinimumExpectancyR = -.1m, decimal MaximumDrawdownR = 5m, int MaximumLosingStreak = 6)
{
    public PerformanceEnvelope Validate()
    {
        if (RollingTrades < 1 || MinimumTradesBeforeEnforcement < 1 || MinimumTradesBeforeEnforcement > RollingTrades || MaximumDrawdownR <= 0 || MaximumLosingStreak < 1) throw new ArgumentOutOfRangeException(nameof(RollingTrades));
        return this;
    }
}

public sealed record AutomaticPauseDirective(bool Pause, string Code, string Reason, int Observations, decimal ExpectancyR, decimal MaximumDrawdownR, int LosingStreak);

public sealed class RollingPerformanceMonitor
{
    public AutomaticPauseDirective Evaluate(IReadOnlyList<PerformanceObservation> observations, PerformanceEnvelope envelope, DateTimeOffset now)
    {
        envelope.Validate();
        if (observations.Zip(observations.Skip(1)).Any(x => x.First.At >= x.Second.At) || observations.Any(x => x.At > now)) return new(true, "EVIDENCE_INVALID", "Performance observations are unordered or future-dated.", observations.Count, 0, 0, 0);
        var window = observations.TakeLast(envelope.RollingTrades).ToArray();
        if (window.Length < envelope.MinimumTradesBeforeEnforcement) return new(false, "MONITORING", "Minimum evidence has not accumulated; no automatic state change is allowed.", window.Length, window.Length == 0 ? 0 : window.Average(x => x.NetR), 0, 0);
        var expectancy = window.Average(x => x.NetR);
        decimal equity = 0, peak = 0, drawdown = 0; var streak = 0; var maximumStreak = 0;
        foreach (var item in window) { equity += item.NetR; peak = Math.Max(peak, equity); drawdown = Math.Max(drawdown, peak - equity); streak = item.NetR <= 0 ? streak + 1 : 0; maximumStreak = Math.Max(maximumStreak, streak); }
        if (expectancy < envelope.MinimumExpectancyR) return Pause("EXPECTANCY_BREACH", "Rolling expectancy breached its envelope.");
        if (drawdown > envelope.MaximumDrawdownR) return Pause("DRAWDOWN_BREACH", "Rolling drawdown breached its envelope.");
        if (maximumStreak > envelope.MaximumLosingStreak) return Pause("LOSING_STREAK_BREACH", "Rolling losing streak breached its envelope.");
        return new(false, "WITHIN_ENVELOPE", "Rolling performance remains within the configured envelope.", window.Length, expectancy, drawdown, maximumStreak);
        AutomaticPauseDirective Pause(string code, string reason) => new(true, code, reason, window.Length, expectancy, drawdown, maximumStreak);
    }
}

public static class AutomaticStrategyPause
{
    public static GovernedStrategy Apply(GovernedStrategy strategy, AutomaticPauseDirective directive)
    {
        if (!directive.Pause) return strategy;
        return strategy.AutomaticallyPause();
    }
}

public sealed record PromotionEvidenceRequirements(int MinimumOutOfSampleTrades = 30, int MinimumShadowTrades = 20, TimeSpan? MinimumShadowDuration = null, decimal MinimumExpectancyAdvantageR = .05m, decimal MaximumChallengerDrawdownR = 5m)
{
    public TimeSpan ShadowDuration => MinimumShadowDuration ?? TimeSpan.FromDays(7);
}
public sealed record PromotionEligibility(bool Eligible, string Code, string Reason, string ChampionId, string ChallengerId, decimal ExpectancyAdvantageR, int ChallengerEvidenceTrades);
public sealed record UserPromotionDecision(PromotionEligibility Eligibility, bool Approved, GovernanceActor DecidedBy, DateTimeOffset DecidedAt, string Reason)
{
    public static UserPromotionDecision Decide(PromotionEligibility eligibility, bool approve, GovernanceActor actor, DateTimeOffset at, string reason)
    {
        if (actor != GovernanceActor.User) throw new InvalidOperationException("Only the user can approve or reject promotion.");
        if (approve && !eligibility.Eligible) throw new InvalidOperationException("An ineligible challenger cannot be promoted.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A user decision reason is required.", nameof(reason));
        return new(eligibility, approve, actor, at, reason.Trim());
    }
}

public static class ChampionChallengerGovernance
{
    public static PromotionEligibility Compare(GovernedStrategy champion, GovernedStrategy challenger, PromotionEvidenceRequirements requirements)
    {
        if (champion.Role != GovernedStrategyRole.Champion || challenger.Role != GovernedStrategyRole.Challenger) return No("ROLE_INVALID", "Comparison requires one champion and one challenger.");
        if (!challenger.UserEnabled || challenger.State != GovernedStrategyState.Active) return No("CHALLENGER_BLOCKED", "Challenger must be user-enabled and active for comparison.");
        if (!challenger.Evidence.RobustnessPassed) return No("ROBUSTNESS_REQUIRED", "Challenger robustness validation has not passed.");
        if (challenger.Evidence.OutOfSampleTrades < requirements.MinimumOutOfSampleTrades || challenger.Evidence.ShadowTrades < requirements.MinimumShadowTrades) return No("EVIDENCE_INSUFFICIENT", "Challenger has insufficient out-of-sample or shadow evidence.");
        if (challenger.Evidence.ShadowStartedAt > challenger.Evidence.EvidenceThrough || challenger.Evidence.EvidenceThrough - challenger.Evidence.ShadowStartedAt < requirements.ShadowDuration) return No("SHADOW_DURATION_INSUFFICIENT", "Challenger shadow evidence duration is below the minimum.");
        if (challenger.Evidence.OutOfSampleMaximumDrawdownR > requirements.MaximumChallengerDrawdownR) return No("DRAWDOWN_EXCESSIVE", "Challenger drawdown exceeds the promotion envelope.");
        var advantage = challenger.Evidence.ShadowExpectancyR - champion.Evidence.ShadowExpectancyR;
        if (advantage < requirements.MinimumExpectancyAdvantageR) return No("ADVANTAGE_INSUFFICIENT", "Challenger expectancy advantage is below the minimum.", advantage);
        return new(true, "ELIGIBLE_FOR_USER_DECISION", "Minimum comparison evidence passed; user approval is still required.", champion.Spec.Id, challenger.Spec.Id, advantage, challenger.Evidence.OutOfSampleTrades + challenger.Evidence.ShadowTrades);
        PromotionEligibility No(string code, string reason, decimal advantage = 0) => new(false, code, reason, champion.Spec.Id, challenger.Spec.Id, advantage, challenger.Evidence.OutOfSampleTrades + challenger.Evidence.ShadowTrades);
    }
}
