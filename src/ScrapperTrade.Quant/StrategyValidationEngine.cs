using ScrapperTrade.Domain;

namespace ScrapperTrade.Quant;

public sealed record BacktestCostModel(
    decimal SpreadMultiplier = 1m,
    decimal SlippagePerSide = 0,
    decimal RoundTripCommissionPrice = 0)
{
    public BacktestCostModel Validate()
    {
        if (SpreadMultiplier < 0 || SlippagePerSide < 0 || RoundTripCommissionPrice < 0) throw new ArgumentOutOfRangeException(nameof(SpreadMultiplier), "Trading costs cannot be negative.");
        return this;
    }
}

public sealed record CostedBacktestTrade(
    DateTimeOffset SignalAt,
    DateTimeOffset OpenedAt,
    DateTimeOffset ClosedAt,
    Side Side,
    decimal SignalPrice,
    decimal Entry,
    decimal Stop,
    decimal Target,
    decimal Exit,
    decimal GrossR,
    decimal CostR,
    decimal NetR,
    int BarsHeld);

public sealed record CostedBacktestMetrics(
    int Trades,
    int Wins,
    int Losses,
    decimal WinRate,
    decimal GrossR,
    decimal NetR,
    decimal ExpectancyR,
    decimal ProfitFactor,
    decimal MaximumDrawdownR,
    int MaximumLosingStreak,
    decimal SharpeLike,
    decimal ExposureFraction);

public sealed record CostedBacktestResult(IReadOnlyList<CostedBacktestTrade> Trades, CostedBacktestMetrics Metrics);

public sealed class CostedDeterministicBacktester
{
    public CostedBacktestResult Run(StrategySpec spec, IReadOnlyList<Candle> candles, BacktestCostModel costs)
    {
        var errors = StrategyValidator.Validate(spec);
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors), nameof(spec));
        costs.Validate();
        ValidateCandles(candles);
        var trades = new List<CostedBacktestTrade>();
        var minimum = Math.Max(spec.SlowEma, 15);
        for (var signalIndex = minimum; signalIndex < candles.Count - 1; signalIndex++)
        {
            var history = candles.Take(signalIndex + 1).ToArray();
            var previous = history[..^1];
            var crossed = Indicators.Ema(previous.Select(x => x.Close), spec.FastEma) <= Indicators.Ema(previous.Select(x => x.Close), spec.SlowEma)
                && Indicators.Ema(history.Select(x => x.Close), spec.FastEma) > Indicators.Ema(history.Select(x => x.Close), spec.SlowEma);
            if (!crossed) continue;

            var signal = candles[signalIndex];
            var entryBar = candles[signalIndex + 1]; // signals formed at close cannot fill at that same close
            var atr = Indicators.Atr(history, 14);
            var entry = entryBar.Open + entryBar.Spread * costs.SpreadMultiplier + costs.SlippagePerSide;
            var riskDistance = atr * spec.StopAtr;
            if (riskDistance <= 0) continue;
            var stop = entry - riskDistance;
            var target = entry + riskDistance * spec.TargetRiskMultiple;

            for (var exitIndex = signalIndex + 1; exitIndex < candles.Count; exitIndex++)
            {
                var bar = candles[exitIndex];
                decimal? rawExit = null;
                if (bar.Open <= stop) rawExit = bar.Open; // adverse gap is not improved to the stop
                else if (bar.Low <= stop && bar.High >= target) rawExit = stop; // conservative intrabar ambiguity
                else if (bar.Low <= stop) rawExit = stop;
                else if (bar.Open >= target || bar.High >= target) rawExit = target;
                else if (exitIndex == candles.Count - 1) rawExit = bar.Close;
                if (rawExit is null) continue;

                var exit = rawExit.Value - costs.SlippagePerSide;
                var grossR = (rawExit.Value - entryBar.Open) / riskDistance;
                var costR = (entryBar.Spread * costs.SpreadMultiplier + costs.SlippagePerSide * 2 + costs.RoundTripCommissionPrice) / riskDistance;
                var netR = (exit - entry - costs.RoundTripCommissionPrice) / riskDistance;
                trades.Add(new(signal.Time, entryBar.Time, bar.Time, Side.Buy, signal.Close, entry, stop, target, exit, grossR, costR, netR, exitIndex - signalIndex));
                signalIndex = exitIndex;
                break;
            }
        }
        return new(trades, Metrics(trades, candles.Count));
    }

    private static void ValidateCandles(IReadOnlyList<Candle> candles)
    {
        for (var index = 0; index < candles.Count; index++)
        {
            var candle = candles[index];
            if (candle.Open <= 0 || candle.High < Math.Max(candle.Open, candle.Close) || candle.Low > Math.Min(candle.Open, candle.Close) || candle.Low <= 0 || candle.Spread < 0)
                throw new ArgumentException("Candles contain invalid prices or spread.", nameof(candles));
            if (index > 0 && candles[index - 1].Time >= candle.Time) throw new ArgumentException("Candles must be strictly chronological.", nameof(candles));
        }
    }

    private static CostedBacktestMetrics Metrics(IReadOnlyList<CostedBacktestTrade> trades, int totalBars)
    {
        var wins = trades.Where(x => x.NetR > 0).ToArray();
        var losses = trades.Where(x => x.NetR <= 0).ToArray();
        var grossWin = wins.Sum(x => x.NetR);
        var grossLoss = Math.Abs(losses.Sum(x => x.NetR));
        decimal equity = 0, peak = 0, drawdown = 0;
        var losingStreak = 0;
        var maximumLosingStreak = 0;
        foreach (var trade in trades)
        {
            equity += trade.NetR;
            peak = Math.Max(peak, equity);
            drawdown = Math.Max(drawdown, peak - equity);
            losingStreak = trade.NetR <= 0 ? losingStreak + 1 : 0;
            maximumLosingStreak = Math.Max(maximumLosingStreak, losingStreak);
        }
        var expectancy = trades.Count == 0 ? 0 : trades.Average(x => x.NetR);
        var variance = trades.Count < 2 ? 0 : trades.Sum(x => (x.NetR - expectancy) * (x.NetR - expectancy)) / (trades.Count - 1);
        var sharpe = variance <= 0 ? 0 : expectancy / (decimal)Math.Sqrt((double)variance) * (decimal)Math.Sqrt(trades.Count);
        return new(trades.Count, wins.Length, losses.Length, trades.Count == 0 ? 0 : (decimal)wins.Length / trades.Count,
            trades.Sum(x => x.GrossR), trades.Sum(x => x.NetR), expectancy,
            grossLoss == 0 ? (grossWin > 0 ? decimal.MaxValue : 0) : grossWin / grossLoss,
            drawdown, maximumLosingStreak, sharpe,
            totalBars == 0 ? 0 : Math.Min(1m, (decimal)trades.Sum(x => x.BarsHeld) / totalBars));
    }
}

public sealed record ChronologicalDataSplit(IReadOnlyList<Candle> Train, IReadOnlyList<Candle> Test, int EmbargoBars);

public static class ChronologicalSplitter
{
    public static ChronologicalDataSplit Split(IReadOnlyList<Candle> candles, decimal trainFraction, int embargoBars = 1)
    {
        if (trainFraction <= 0 || trainFraction >= 1) throw new ArgumentOutOfRangeException(nameof(trainFraction));
        if (embargoBars < 1) throw new ArgumentOutOfRangeException(nameof(embargoBars), "At least one embargo bar prevents boundary leakage.");
        var boundary = (int)decimal.Floor(candles.Count * trainFraction);
        if (boundary < 16 || boundary + embargoBars >= candles.Count - 16) throw new ArgumentException("Insufficient candles for chronological train/test split.", nameof(candles));
        return new(candles.Take(boundary).ToArray(), candles.Skip(boundary + embargoBars).ToArray(), embargoBars);
    }
}

public sealed record ParameterGridResult(StrategySpec Spec, CostedBacktestMetrics Metrics);
public sealed record WalkForwardFold(DateTimeOffset TrainStarts, DateTimeOffset TrainEnds, DateTimeOffset TestStarts, DateTimeOffset TestEnds, StrategySpec Selected, CostedBacktestMetrics TrainMetrics, CostedBacktestMetrics OutOfSampleMetrics);
public sealed record RobustnessDecision(bool Passed, string Code, string Reason, IReadOnlyList<ParameterGridResult> Grid, IReadOnlyList<WalkForwardFold> Folds, CostedBacktestMetrics CombinedOutOfSample);

public sealed record RobustnessThresholds(int MinimumTrainTrades = 5, int MinimumOutOfSampleTrades = 5, decimal MinimumOutOfSampleExpectancyR = 0, decimal MaximumDrawdownR = 10, decimal MinimumPositiveGridFraction = .5m);

public sealed class DeterministicRobustnessValidator
{
    private readonly CostedDeterministicBacktester backtester = new();

    public RobustnessDecision Validate(IReadOnlyList<StrategySpec> grid, IReadOnlyList<Candle> candles, BacktestCostModel costs, int trainBars, int testBars, int stepBars, RobustnessThresholds thresholds)
    {
        if (grid.Count < 3) throw new ArgumentException("A robustness grid requires at least three neighboring hypotheses.", nameof(grid));
        if (trainBars < 30 || testBars < 17 || stepBars < testBars) throw new ArgumentOutOfRangeException(nameof(stepBars), "Walk-forward step must prevent overlapping out-of-sample windows.");
        var gridResults = grid.Select(spec => new ParameterGridResult(spec, backtester.Run(spec, candles, costs).Metrics)).ToArray();
        var folds = new List<WalkForwardFold>();
        var allOutOfSample = new List<CostedBacktestTrade>();
        var undertrainedFold = false;
        for (var start = 0; start + trainBars + 1 + testBars <= candles.Count; start += stepBars)
        {
            var train = candles.Skip(start).Take(trainBars).ToArray();
            var test = candles.Skip(start + trainBars + 1).Take(testBars).ToArray(); // one-bar embargo
            var ranked = grid.Select(spec => (Spec: spec, Result: backtester.Run(spec, train, costs))).OrderByDescending(x => x.Result.Metrics.ExpectancyR).ThenBy(x => x.Spec.Id, StringComparer.Ordinal).ThenBy(x => x.Spec.Version).ToArray();
            var qualified = ranked.Where(x => x.Result.Metrics.Trades >= thresholds.MinimumTrainTrades).ToArray();
            if (qualified.Length == 0) undertrainedFold = true;
            var selected = qualified.FirstOrDefault();
            if (selected.Spec is null) selected = ranked[0];
            var outOfSample = backtester.Run(selected.Spec, test, costs);
            folds.Add(new(train[0].Time, train[^1].Time, test[0].Time, test[^1].Time, selected.Spec, selected.Result.Metrics, outOfSample.Metrics));
            allOutOfSample.AddRange(outOfSample.Trades);
        }
        if (folds.Count == 0) throw new ArgumentException("Insufficient candles for a walk-forward fold.", nameof(candles));
        var combined = Aggregate(allOutOfSample, folds.Count * testBars);
        var positiveGridFraction = (decimal)gridResults.Count(x => x.Metrics.ExpectancyR > 0) / gridResults.Length;
        if (undertrainedFold) return Result(false, "TRAIN_TRADE_COUNT", "At least one fold had no parameter set with enough in-sample trades.");
        if (combined.Trades < thresholds.MinimumOutOfSampleTrades) return Result(false, "OOS_TRADE_COUNT", "Out-of-sample trade count is below the minimum.");
        if (combined.ExpectancyR <= thresholds.MinimumOutOfSampleExpectancyR) return Result(false, "OOS_EXPECTANCY", "Out-of-sample expectancy did not clear the threshold.");
        if (combined.MaximumDrawdownR > thresholds.MaximumDrawdownR) return Result(false, "OOS_DRAWDOWN", "Out-of-sample drawdown exceeded the threshold.");
        if (positiveGridFraction < thresholds.MinimumPositiveGridFraction) return Result(false, "PARAMETER_INSTABILITY", "Too few neighboring parameter sets retained positive expectancy.");
        return Result(true, "ROBUSTNESS_PASSED", "Deterministic out-of-sample and parameter-grid thresholds passed.");

        RobustnessDecision Result(bool passed, string code, string reason) => new(passed, code, reason, gridResults, folds, combined);
    }

    private static CostedBacktestMetrics Aggregate(IReadOnlyList<CostedBacktestTrade> trades, int totalOutOfSampleBars)
    {
        // Reuse the public engine's metric semantics without re-running market logic.
        var wins = trades.Count(x => x.NetR > 0);
        var losses = trades.Count - wins;
        var positive = trades.Where(x => x.NetR > 0).Sum(x => x.NetR);
        var negative = Math.Abs(trades.Where(x => x.NetR <= 0).Sum(x => x.NetR));
        decimal equity = 0, peak = 0, drawdown = 0;
        var streak = 0; var maxStreak = 0;
        foreach (var trade in trades) { equity += trade.NetR; peak = Math.Max(peak, equity); drawdown = Math.Max(drawdown, peak - equity); streak = trade.NetR <= 0 ? streak + 1 : 0; maxStreak = Math.Max(maxStreak, streak); }
        var mean = trades.Count == 0 ? 0 : trades.Average(x => x.NetR);
        var variance = trades.Count < 2 ? 0 : trades.Sum(x => (x.NetR - mean) * (x.NetR - mean)) / (trades.Count - 1);
        return new(trades.Count, wins, losses, trades.Count == 0 ? 0 : (decimal)wins / trades.Count, trades.Sum(x => x.GrossR), trades.Sum(x => x.NetR), mean,
            negative == 0 ? (positive > 0 ? decimal.MaxValue : 0) : positive / negative, drawdown, maxStreak,
            variance <= 0 ? 0 : mean / (decimal)Math.Sqrt((double)variance) * (decimal)Math.Sqrt(trades.Count),
            totalOutOfSampleBars == 0 ? 0 : Math.Min(1m, (decimal)trades.Sum(x => x.BarsHeld) / totalOutOfSampleBars));
    }
}

public static class BuiltInStrategyHypotheses
{
    public static IReadOnlyList<StrategySpec> EmaTrendGrid() =>
    [
        new("ema-trend-3-8", 1, 3, 8, 1.5m, 2m, new HashSet<MarketRegime> { MarketRegime.TrendingUp, MarketRegime.Breakout }),
        new("ema-trend-4-9", 1, 4, 9, 1.5m, 2m, new HashSet<MarketRegime> { MarketRegime.TrendingUp, MarketRegime.Breakout }),
        new("ema-trend-5-10", 1, 5, 10, 1.5m, 2m, new HashSet<MarketRegime> { MarketRegime.TrendingUp, MarketRegime.Breakout })
    ];
}
