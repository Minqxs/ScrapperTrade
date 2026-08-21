using ScrapperTrade.Application;
using ScrapperTrade.Domain;
using ScrapperTrade.Quant;

internal static class StrategyGovernanceChecks
{
    public static IEnumerable<(string Name, Action Check)> All()
    {
        yield return ("governance regime ranking journals rejections", RegimeRanking);
        yield return ("governance selection is idempotent", SelectionIdempotent);
        yield return ("governance disabled strategy rejected", DisabledRejected);
        yield return ("governance envelope breach pauses only", EnvelopePauseOnly);
        yield return ("governance insufficient observations do not pause", InsufficientMonitoring);
        yield return ("governance invalid evidence pauses closed", InvalidEvidence);
        yield return ("governance promotion requires user", PromotionRequiresUser);
        yield return ("governance challenger evidence enforced", ChallengerEvidence);
    }

    private static void RegimeRanking()
    {
        var journal = new AppendOnlyStrategySelectionJournal();
        var selector = new DeterministicStrategySelector(journal);
        var lower = Strategy("lower", GovernedStrategyRole.Champion, Evidence(.1m, .05m, 2));
        var higher = Strategy("higher", GovernedStrategyRole.Challenger, Evidence(.3m, .2m, 1));
        var wrongRegime = Strategy("range", GovernedStrategyRole.Shadow, Evidence(.8m, .8m, 1), MarketRegime.Ranging);
        var result = selector.Select("XAUUSD", MarketRegime.TrendingUp, [lower, wrongRegime, higher], Now());
        Equal("higher", result.SelectedStrategyId);
        Equal("REGIME_MISMATCH", result.Assessments.Single(x => x.StrategyId == "range").Code);
        Equal(1, journal.ReadAll().Count);
    }

    private static void SelectionIdempotent()
    {
        var journal = new AppendOnlyStrategySelectionJournal();
        var selector = new DeterministicStrategySelector(journal);
        var first = selector.Select("XAUUSD", MarketRegime.TrendingUp, [Strategy("one", GovernedStrategyRole.Shadow, Evidence(.1m, .1m, 1))], Now());
        var replay = selector.Select("XAUUSD", MarketRegime.TrendingUp, [], Now());
        Equal(first.DecisionId, replay.DecisionId);
        Equal("one", replay.SelectedStrategyId);
        Equal(1, journal.ReadAll().Count);
    }

    private static void DisabledRejected()
    {
        var strategy = GovernedStrategy.CreateDisabled(Spec("off"), GovernedStrategyRole.Challenger, GovernedStrategyState.Active, Evidence(.3m, .3m, 1));
        Throws<InvalidOperationException>(() => strategy.SetUserEnabled(true, GovernanceActor.Automation));
        var result = new DeterministicStrategySelector(new AppendOnlyStrategySelectionJournal()).Select("XAUUSD", MarketRegime.TrendingUp, [strategy], Now());
        Equal(null, result.SelectedStrategyId);
        Equal("USER_DISABLED", result.Assessments.Single().Code);
    }

    private static void EnvelopePauseOnly()
    {
        var observations = Enumerable.Range(0, 10).Select(index => new PerformanceObservation(Now().AddMinutes(index - 10), -.2m)).ToArray();
        var directive = new RollingPerformanceMonitor().Evaluate(observations, new(), Now());
        Equal(true, directive.Pause);
        Equal("EXPECTANCY_BREACH", directive.Code);
        var paused = AutomaticStrategyPause.Apply(Strategy("weak", GovernedStrategyRole.Champion, Evidence(0, 0, 0)), directive);
        Equal(GovernedStrategyState.AutomaticallyPaused, paused.State);
        Equal(true, paused.UserEnabled);
        Equal(GovernedStrategyState.AutomaticallyPaused, AutomaticStrategyPause.Apply(paused, directive).State);
    }

    private static void InsufficientMonitoring()
    {
        var directive = new RollingPerformanceMonitor().Evaluate([new(Now().AddMinutes(-1), -10)], new(), Now());
        Equal(false, directive.Pause);
        Equal("MONITORING", directive.Code);
    }

    private static void InvalidEvidence()
    {
        var directive = new RollingPerformanceMonitor().Evaluate([new(Now(), 1), new(Now().AddMinutes(-1), 1)], new(), Now());
        Equal(true, directive.Pause);
        Equal("EVIDENCE_INVALID", directive.Code);
    }

    private static void PromotionRequiresUser()
    {
        var champion = Strategy("champion", GovernedStrategyRole.Champion, Evidence(.1m, .1m, 2));
        var challenger = Strategy("challenger", GovernedStrategyRole.Challenger, new(40, .2m, 3, true, 25, .3m, Now().AddDays(-8), Now()));
        var eligibility = ChampionChallengerGovernance.Compare(champion, challenger, new());
        Equal(true, eligibility.Eligible);
        Throws<InvalidOperationException>(() => UserPromotionDecision.Decide(eligibility, true, GovernanceActor.Automation, Now(), "automated"));
        var decision = UserPromotionDecision.Decide(eligibility, true, GovernanceActor.User, Now(), "reviewed evidence");
        Equal(true, decision.Approved);
        Equal(GovernanceActor.User, decision.DecidedBy);
    }

    private static void ChallengerEvidence()
    {
        var champion = Strategy("champion", GovernedStrategyRole.Champion, Evidence(.1m, .1m, 2));
        var challenger = Strategy("challenger", GovernedStrategyRole.Challenger, new(2, 1, 1, true, 2, 1, Now().AddDays(-8), Now()));
        var result = ChampionChallengerGovernance.Compare(champion, challenger, new());
        Equal(false, result.Eligible);
        Equal("EVIDENCE_INSUFFICIENT", result.Code);
        Throws<InvalidOperationException>(() => UserPromotionDecision.Decide(result, true, GovernanceActor.User, Now(), "not enough"));
    }

    private static DateTimeOffset Now() => DateTimeOffset.Parse("2030-01-01T12:00:00Z");
    private static StrategyEvidence Evidence(decimal outOfSample, decimal shadow, decimal drawdown) => new(40, outOfSample, drawdown, true, 25, shadow, Now().AddDays(-8), Now().AddMinutes(-1));
    private static StrategySpec Spec(string id, params MarketRegime[] regimes) => new(id, 1, 3, 8, 1.5m, 2, new HashSet<MarketRegime>(regimes.Length == 0 ? [MarketRegime.TrendingUp] : regimes));
    private static GovernedStrategy Strategy(string id, GovernedStrategyRole role, StrategyEvidence evidence, params MarketRegime[] regimes) =>
        GovernedStrategy.CreateDisabled(Spec(id, regimes), role, GovernedStrategyState.Active, evidence).SetUserEnabled(true, GovernanceActor.User);

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
