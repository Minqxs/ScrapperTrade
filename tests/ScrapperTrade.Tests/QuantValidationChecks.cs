using ScrapperTrade.Domain;
using ScrapperTrade.Quant;

internal static class QuantValidationChecks
{
    public static IEnumerable<(string Name, Action Check)> All()
    {
        yield return ("costed backtest enters next bar", NextBarEntry);
        yield return ("costs reduce net result deterministically", CostsReduceResult);
        yield return ("chronological split applies embargo", SplitEmbargo);
        yield return ("walk-forward folds never overlap", WalkForwardNoOverlap);
        yield return ("robustness minimum oos count fails closed", InsufficientOutOfSample);
        yield return ("built-in hypotheses are valid", BuiltInsValid);
        yield return ("invalid candle chronology rejected", InvalidChronology);
    }

    private static void NextBarEntry()
    {
        var candles = CrossingCandles();
        var result = new CostedDeterministicBacktester().Run(Spec(), candles, new());
        var trade = result.Trades.First();
        Equal(true, trade.OpenedAt > trade.SignalAt);
        Equal(candles.Single(x => x.Time == trade.OpenedAt).Open + candles.Single(x => x.Time == trade.OpenedAt).Spread, trade.Entry);
    }

    private static void CostsReduceResult()
    {
        var candles = CrossingCandles();
        var engine = new CostedDeterministicBacktester();
        var free = engine.Run(Spec(), candles.Select(x => x with { Spread = 0 }).ToArray(), new()).Metrics.NetR;
        var costed = engine.Run(Spec(), candles, new(1.5m, .02m, .03m)).Metrics.NetR;
        Equal(true, costed < free);
        Equal(true, engine.Run(Spec(), candles, new(1.5m, .02m, .03m)).Trades.All(x => x.CostR >= 0));
    }

    private static void SplitEmbargo()
    {
        var candles = LongSeries(100);
        var split = ChronologicalSplitter.Split(candles, .6m, 2);
        Equal(60, split.Train.Count);
        Equal(candles[62].Time, split.Test[0].Time);
        Equal(true, split.Train[^1].Time < split.Test[0].Time);
    }

    private static void WalkForwardNoOverlap()
    {
        var result = Validator(1).Folds;
        Equal(true, result.Count > 1);
        Equal(true, result.All(fold => fold.TrainEnds < fold.TestStarts));
        Equal(true, result.Zip(result.Skip(1)).All(pair => pair.First.TestEnds < pair.Second.TestStarts));
    }

    private static void InsufficientOutOfSample()
    {
        var result = Validator(10_000);
        Equal(false, result.Passed);
        Equal("OOS_TRADE_COUNT", result.Code);
    }

    private static void BuiltInsValid()
    {
        var specs = BuiltInStrategyHypotheses.EmaTrendGrid();
        Equal(3, specs.Count);
        Equal(true, specs.All(x => StrategyValidator.Validate(x).Count == 0));
        Equal(3, specs.Select(x => (x.FastEma, x.SlowEma)).Distinct().Count());
    }

    private static void InvalidChronology()
    {
        var candles = CrossingCandles().ToArray();
        candles[2] = candles[2] with { Time = candles[1].Time };
        Throws<ArgumentException>(() => new CostedDeterministicBacktester().Run(Spec(), candles, new()));
    }

    private static RobustnessDecision Validator(int minimumTrades) => new DeterministicRobustnessValidator().Validate(
        BuiltInStrategyHypotheses.EmaTrendGrid(), LongSeries(240), new(1, .01m, .01m), 80, 40, 40,
        new(MinimumTrainTrades: 0, MinimumOutOfSampleTrades: minimumTrades, MinimumOutOfSampleExpectancyR: -100, MaximumDrawdownR: 100, MinimumPositiveGridFraction: 0));

    private static StrategySpec Spec() => new("test-cross", 1, 2, 3, 1, 1.5m, new HashSet<MarketRegime> { MarketRegime.TrendingUp });

    private static IReadOnlyList<Candle> CrossingCandles()
    {
        var at = DateTimeOffset.Parse("2024-01-01T00:00:00Z");
        var closes = Enumerable.Repeat(10m, 16).Concat([11m, 12m, 13m, 14m, 15m]).ToArray();
        return closes.Select((close, index) => new Candle(at.AddMinutes(index), index == 17 ? 12.5m : close, Math.Max(close, index == 17 ? 12.5m : close) + .6m, Math.Min(close, index == 17 ? 12.5m : close) - .6m, close, .05m)).ToArray();
    }

    private static IReadOnlyList<Candle> LongSeries(int count)
    {
        var at = DateTimeOffset.Parse("2024-01-01T00:00:00Z");
        decimal previous = 100;
        var result = new List<Candle>();
        for (var index = 0; index < count; index++)
        {
            var wave = index % 24;
            var close = wave < 8 ? 100m : wave < 16 ? 100m + (wave - 8) * .5m : 104m - (wave - 16) * .5m;
            result.Add(new(at.AddMinutes(index), previous, Math.Max(previous, close) + .4m, Math.Min(previous, close) - .4m, close, .02m));
            previous = close;
        }
        return result;
    }

    private static void Equal<T>(T expected, T actual) where T : notnull
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
