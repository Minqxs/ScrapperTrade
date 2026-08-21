using ScrapperTrade.Domain;
using ScrapperTrade.Quant;

public static class MarketIntelligenceChecks
{
    public static IEnumerable<(string, Action)> All()
    {
        yield return ("rsi deterministic bounds", Rsi);
        yield return ("vwap weighted typical price", Vwap);
        yield return ("regime insufficient is unknown", Unknown);
        yield return ("regime trend direction", Trend);
        yield return ("overnight session deterministic", OvernightSession);
        foreach (var check in SimulatorStrategyRuntimeChecks.All()) yield return check;
    }

    private static void Rsi()
    {
        var value = ExtendedIndicators.Rsi([1, 2, 3, 2, 4], 4);
        if (value <= 50 || value > 100) throw new Exception($"Unexpected RSI {value}.");
    }

    private static void Vwap()
    {
        var at = DateTimeOffset.Parse("2024-01-01T00:00:00Z");
        var candles = new[] { new Candle(at, 9, 12, 9, 9), new Candle(at.AddMinutes(1), 18, 21, 18, 18) };
        var value = ExtendedIndicators.Vwap(candles, [1, 3]);
        if (value != 16.75m) throw new Exception($"Expected 16.75, got {value}.");
    }

    private static void Unknown()
    {
        var result = new DeterministicRegimeClassifier().Classify([]);
        if (result.Regime != MarketRegime.Unknown || result.Confidence != 0) throw new Exception("Unknown must fail closed.");
    }

    private static void Trend()
    {
        var at = DateTimeOffset.Parse("2024-01-01T00:00:00Z");
        var candles = Enumerable.Range(0, 40).Select(index =>
        {
            var close = 100m + index;
            return new Candle(at.AddMinutes(index), close - 1, close + .5m, close - .5m, close);
        }).ToArray();
        var result = new DeterministicRegimeClassifier().Classify(candles);
        if (result.Regime != MarketRegime.TrendingUp || result.Confidence <= 0) throw new Exception($"Unexpected {result.Regime}.");
    }

    private static void OvernightSession()
    {
        var timestamp = DateTimeOffset.Parse("2024-01-01T23:30:00Z");
        if (!TradingSessions.ContainsUtc(timestamp, new(22, 0), new(2, 0))) throw new Exception("Overnight session missed.");
        if (TradingSessions.ContainsUtc(timestamp, new(8, 0), new(17, 0))) throw new Exception("Day session false positive.");
    }
}
