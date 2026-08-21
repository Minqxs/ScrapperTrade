using ScrapperTrade.Application;
using ScrapperTrade.Domain;
using ScrapperTrade.Infrastructure;
using ScrapperTrade.Quant;

internal static class SimulatorStrategyRuntimeChecks
{
    public static IEnumerable<(string Name, Action Check)> All()
    {
        yield return ("strategy enablement is user owned", UserOwnsEnablement);
        yield return ("strategy paused system fails closed", PausedFailsClosed);
        yield return ("strategy shadow decision risk approved", ShadowApproved);
        yield return ("strategy decision idempotent", Idempotent);
        yield return ("strategy state survives restart", RestartSafe);
        yield return ("strategy invalid spec rejected", InvalidSpec);
        yield return ("strategy stale no-signal input rejected", StaleInput);
        yield return ("strategy candidate lifecycle cannot schedule", CandidateBlocked);
    }

    private static void UserOwnsEnablement()
    {
        var registration = StrategyRuntimeRegistration.CreateDisabled(Spec(), StrategyLifecycleStatus.Shadow);
        Throws<InvalidOperationException>(() => registration.SetUserEnabled(true, StrategyPermissionActor.Automation));
        Equal(true, registration.SetUserEnabled(true, StrategyPermissionActor.User).UserEnabled);
    }

    private static void PausedFailsClosed()
    {
        WithStore((_, store) =>
        {
            var result = Scheduler(store).Evaluate(Enabled(), Market(), new(SystemMode.Paused, false, 2, Now(), "paused"), Instrument(), Metadata(), Portfolio(), new(), "METALS", Now());
            Equal(ShadowDecisionStatus.Rejected, result.Status);
            Equal("SYSTEM_BLOCKED", result.Code);
        });
    }

    private static void ShadowApproved()
    {
        WithStore((_, store) =>
        {
            var result = Evaluate(store);
            Equal(ShadowDecisionStatus.ShadowApproved, result.Status);
            Equal("SHADOW_APPROVED", result.Code);
            Equal(true, result.SimulatedVolume > 0);
        });
    }

    private static void Idempotent()
    {
        WithStore((_, store) =>
        {
            var first = Evaluate(store);
            var second = Evaluate(store);
            Equal(first.SignalId, second.SignalId);
            Equal(1, store.ReadAll().Count);
        });
    }

    private static void RestartSafe()
    {
        WithStore((path, store) =>
        {
            var first = Evaluate(store);
            var restarted = new JsonShadowStrategyStateStore(path);
            var second = Evaluate(restarted);
            Equal(first.SignalId, second.SignalId);
            Equal(first.DecisionKey, second.DecisionKey);
            Equal(1, restarted.ReadAll().Count);
        });
    }

    private static void InvalidSpec()
    {
        WithStore((_, store) =>
        {
            var invalid = StrategyRuntimeRegistration.CreateDisabled(new("", 0, 1, 1, 0, 0, new HashSet<MarketRegime>()), StrategyLifecycleStatus.Shadow).SetUserEnabled(true, StrategyPermissionActor.User);
            var result = Scheduler(store).Evaluate(invalid, Market(), Running(), Instrument(), Metadata(), Portfolio(), new(), "METALS", Now());
            Equal("SPEC_INVALID", result.Code);
            Equal(null, result.Candidate);
        });
    }

    private static void StaleInput()
    {
        WithStore((_, store) =>
        {
            var stale = Market() with { ObservedAt = Now().AddMinutes(-2), Candles = Market().Candles.Select((c, i) => c with { Time = Now().AddMinutes(-14 + i) }).ToArray() };
            var result = Scheduler(store).Evaluate(Enabled(), stale, Running(), Instrument(), Metadata(), Portfolio(), new(), "METALS", Now());
            Equal("STALE_MARKET", result.Code);
        });
    }

    private static void CandidateBlocked()
    {
        WithStore((_, store) =>
        {
            var candidate = StrategyRuntimeRegistration.CreateDisabled(Spec(), StrategyLifecycleStatus.Candidate).SetUserEnabled(true, StrategyPermissionActor.User);
            var result = Scheduler(store).Evaluate(candidate, Market(), Running(), Instrument(), Metadata(), Portfolio(), new(), "METALS", Now());
            Equal("LIFECYCLE_BLOCKED", result.Code);
        });
    }

    private static ShadowStrategyDecision Evaluate(IShadowStrategyStateStore store) =>
        Scheduler(store).Evaluate(Enabled(), Market(), Running(), Instrument(), Metadata(), Portfolio(), new(), "METALS", Now());

    private static SimulatorStrategyScheduler Scheduler(IShadowStrategyStateStore store) => new(new PortfolioRiskEngine(new()), store);
    private static StrategySpec Spec() => new("ema-shadow", 1, 2, 3, 1, 2, new HashSet<MarketRegime> { MarketRegime.TrendingUp });
    private static StrategyRuntimeRegistration Enabled() => StrategyRuntimeRegistration.CreateDisabled(Spec(), StrategyLifecycleStatus.Shadow).SetUserEnabled(true, StrategyPermissionActor.User);
    private static DateTimeOffset Now() => DateTimeOffset.Parse("2030-01-01T00:12:00Z");
    private static SystemStateSnapshot Running() => new(SystemMode.Running, true, 1, Now(), "user start");
    private static TradingInstrument Instrument() => new("XAUUSD", true, "XAUUSD", InstrumentAvailability.Active, MaximumSpread: 1, ExposureGroup: "METALS");
    private static SymbolMetadata Metadata() => new("XAUUSD", .01m, 1m, 100m, .01m, 100m, .01m);
    private static PortfolioSnapshot Portfolio() => new(10_000m, 0, []);

    private static StrategyMarketInput Market()
    {
        var start = Now().AddMinutes(-12);
        var candles = Enumerable.Range(0, 12).Select(index => new Candle(start.AddMinutes(index), 10, 10.1m, 9.9m, 10)).ToList();
        candles.Add(new(Now(), 10, 11.1m, 9.9m, 11));
        return new("XAUUSD", candles, MarketRegime.TrendingUp, .02m, Now());
    }

    private static void WithStore(Action<string, JsonShadowStrategyStateStore> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "ScrapperTrade-StrategyTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "shadow-state.json");
        try { check(path, new(path)); }
        finally { Directory.Delete(root, true); }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}");
    }
}
