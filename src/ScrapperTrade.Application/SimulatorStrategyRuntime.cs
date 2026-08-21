using System.Security.Cryptography;
using System.Text;
using ScrapperTrade.Domain;
using ScrapperTrade.Quant;

namespace ScrapperTrade.Application;

public enum StrategyLifecycleStatus { Research, Candidate, BacktestValidated, Shadow, Paused, Retired }
public enum StrategyPermissionActor { User, Automation }
public enum ShadowDecisionStatus { NoTrade, Rejected, ShadowApproved }

public sealed class StrategyRuntimeRegistration
{
    private StrategyRuntimeRegistration(StrategySpec spec, StrategyLifecycleStatus status, bool userEnabled) => (Spec, Status, UserEnabled) = (spec, status, userEnabled);
    public StrategySpec Spec { get; }
    public StrategyLifecycleStatus Status { get; }
    public bool UserEnabled { get; }
    public static StrategyRuntimeRegistration CreateDisabled(StrategySpec spec, StrategyLifecycleStatus status) => new(spec, status, false);

    public StrategyRuntimeRegistration SetUserEnabled(bool enabled, StrategyPermissionActor actor)
    {
        if (actor != StrategyPermissionActor.User) throw new InvalidOperationException("Only the user may change strategy runtime enablement.");
        return new(Spec, Status, enabled);
    }
}

public sealed record StrategyMarketInput(
    string Instrument,
    IReadOnlyList<Candle> Candles,
    MarketRegime Regime,
    decimal Spread,
    DateTimeOffset ObservedAt);

public sealed record ShadowStrategyDecision(
    string DecisionKey,
    Guid SignalId,
    string StrategyId,
    int StrategyVersion,
    string Instrument,
    DateTimeOffset MarketTimestamp,
    DateTimeOffset EvaluatedAt,
    ShadowDecisionStatus Status,
    string Code,
    string Reason,
    CandidateTrade? Candidate,
    decimal? SimulatedVolume,
    decimal? SimulatedRiskAmount);

public interface IShadowStrategyStateStore
{
    ShadowStrategyDecision? Find(string decisionKey);
    ShadowStrategyDecision AddOrGet(ShadowStrategyDecision decision);
    IReadOnlyList<ShadowStrategyDecision> ReadAll();
}

public sealed class SimulatorStrategyScheduler(PortfolioRiskEngine risk, IShadowStrategyStateStore state, TimeSpan? maximumMarketAge = null)
{
    private readonly TimeSpan marketAge = maximumMarketAge ?? TimeSpan.FromSeconds(30);
    public ShadowStrategyDecision Evaluate(
        StrategyRuntimeRegistration registration,
        StrategyMarketInput market,
        SystemStateSnapshot system,
        TradingInstrument instrument,
        SymbolMetadata metadata,
        PortfolioSnapshot portfolio,
        InstrumentRiskPermissions permissions,
        string exposureGroup,
        DateTimeOffset now)
    {
        var key = DecisionKey(registration.Spec, market);
        var existing = state.Find(key);
        if (existing is not null) return existing;

        var signalId = DeterministicSignalId(key);
        ShadowStrategyDecision decision;
        var validation = StrategyValidator.Validate(registration.Spec);
        if (validation.Count > 0) decision = Reject("SPEC_INVALID", string.Join(" ", validation));
        else if (registration.Status != StrategyLifecycleStatus.Shadow) decision = Reject("LIFECYCLE_BLOCKED", $"Strategy status {registration.Status} is not shadow-eligible.");
        else if (!registration.UserEnabled) decision = Reject("USER_DISABLED", "The user has not enabled this strategy.");
        else if (!system.AllowsNewEntries || system.Mode != SystemMode.Running) decision = Reject("SYSTEM_BLOCKED", $"System mode {system.Mode} blocks new decisions.");
        else if (!instrument.CanOpenNewPosition || !string.Equals(instrument.LogicalName, market.Instrument, StringComparison.Ordinal)) decision = Reject("INSTRUMENT_BLOCKED", "The user-controlled trading universe blocks this instrument.");
        else if (market.Candles.Count == 0 || market.Candles[^1].Time != market.ObservedAt || market.Candles.Zip(market.Candles.Skip(1)).Any(pair => pair.First.Time >= pair.Second.Time)) decision = Reject("MARKET_SEQUENCE_INVALID", "Market candles must be non-empty, strictly ordered, and end at the observation timestamp.");
        else if (market.ObservedAt > now.AddSeconds(1) || now - market.ObservedAt > marketAge) decision = Reject("STALE_MARKET", "Market input is stale or future-dated.");
        else if (!registration.Spec.Regimes.Contains(market.Regime) || market.Regime is MarketRegime.Unknown or MarketRegime.Abnormal) decision = NoTrade("REGIME_MISMATCH", $"Regime {market.Regime} is not eligible.");
        else
        {
            var candidate = EvaluateCross(registration.Spec, market, signalId);
            if (candidate is null) decision = NoTrade("NO_SIGNAL", "No deterministic EMA crossover occurred.");
            else
            {
                var riskDecision = risk.Evaluate(candidate, metadata, portfolio, exposureGroup, now, permissions);
                decision = riskDecision.Approved
                    ? Build(ShadowDecisionStatus.ShadowApproved, "SHADOW_APPROVED", "Risk-approved simulator decision; broker transmission is structurally unavailable.", candidate, riskDecision.Volume, riskDecision.RiskAmount)
                    : Build(ShadowDecisionStatus.Rejected, riskDecision.Code, riskDecision.Reason, candidate, null, null);
            }
        }
        return state.AddOrGet(decision);

        ShadowStrategyDecision Reject(string code, string reason) => Build(ShadowDecisionStatus.Rejected, code, reason, null, null, null);
        ShadowStrategyDecision NoTrade(string code, string reason) => Build(ShadowDecisionStatus.NoTrade, code, reason, null, null, null);
        ShadowStrategyDecision Build(ShadowDecisionStatus status, string code, string reason, CandidateTrade? candidate, decimal? volume, decimal? riskAmount) =>
            new(key, signalId, registration.Spec.Id, registration.Spec.Version, market.Instrument, market.ObservedAt, now, status, code, reason, candidate, volume, riskAmount);
    }

    private static CandidateTrade? EvaluateCross(StrategySpec spec, StrategyMarketInput market, Guid signalId)
    {
        if (market.Candles.Count <= spec.SlowEma) return null;
        var previous = market.Candles.Take(market.Candles.Count - 1).ToArray();
        var previousFast = Indicators.Ema(previous.Select(x => x.Close), spec.FastEma);
        var previousSlow = Indicators.Ema(previous.Select(x => x.Close), spec.SlowEma);
        var currentFast = Indicators.Ema(market.Candles.Select(x => x.Close), spec.FastEma);
        var currentSlow = Indicators.Ema(market.Candles.Select(x => x.Close), spec.SlowEma);
        if (!(previousFast <= previousSlow && currentFast > currentSlow)) return null;
        var current = market.Candles[^1];
        var atr = Indicators.Atr(market.Candles, Math.Min(14, market.Candles.Count - 1));
        var entry = current.Close + market.Spread;
        var stop = entry - atr * spec.StopAtr;
        var target = entry + (entry - stop) * spec.TargetRiskMultiple;
        return new(signalId, spec.Id, spec.Version, market.Instrument, Side.Buy, entry, stop, target, market.Spread, market.ObservedAt, 1m, "Deterministic EMA crossover shadow signal.");
    }

    private static string DecisionKey(StrategySpec spec, StrategyMarketInput market) => $"{spec.Id}:{spec.Version}:{market.Instrument}:{market.ObservedAt.ToUnixTimeMilliseconds()}";

    private static Guid DeterministicSignalId(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash.AsSpan(0, 16));
    }
}
