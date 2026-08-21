using ScrapperTrade.Domain;

namespace ScrapperTrade.Quant;

public sealed record MarketState(
    MarketRegime Regime,
    decimal Confidence,
    decimal FastEma,
    decimal SlowEma,
    decimal Atr,
    decimal Rsi,
    IReadOnlyList<string> Evidence);

public static class ExtendedIndicators
{
    public static decimal Rsi(IReadOnlyList<decimal> closes, int period)
    {
        if (period < 2 || closes.Count < period + 1) throw new ArgumentException("Insufficient data or invalid RSI period.");
        decimal gains = 0, losses = 0;
        for (var index = closes.Count - period; index < closes.Count; index++)
        {
            var change = closes[index] - closes[index - 1];
            if (change > 0) gains += change; else losses -= change;
        }
        if (losses == 0) return gains == 0 ? 50 : 100;
        var relativeStrength = gains / losses;
        return 100 - 100 / (1 + relativeStrength);
    }

    public static decimal Vwap(IReadOnlyList<Candle> candles, IReadOnlyList<decimal> volumes)
    {
        if (candles.Count == 0 || candles.Count != volumes.Count || volumes.Any(x => x < 0))
            throw new ArgumentException("Candles require matching non-negative volumes.");
        var totalVolume = volumes.Sum();
        if (totalVolume <= 0) throw new ArgumentException("Positive volume is required for VWAP.");
        return candles.Select((candle, index) => ((candle.High + candle.Low + candle.Close) / 3) * volumes[index]).Sum() / totalVolume;
    }

    public static decimal Momentum(IReadOnlyList<decimal> closes, int lookback)
    {
        if (lookback < 1 || closes.Count <= lookback) throw new ArgumentException("Insufficient data or invalid lookback.");
        var prior = closes[closes.Count - 1 - lookback];
        if (prior == 0) throw new ArgumentException("Momentum base cannot be zero.");
        return closes[^1] / prior - 1;
    }
}

public sealed class DeterministicRegimeClassifier(
    int fastPeriod = 10,
    int slowPeriod = 30,
    int atrPeriod = 14,
    decimal abnormalAtrFraction = .03m,
    decimal trendSeparationFraction = .002m)
{
    public MarketState Classify(IReadOnlyList<Candle> candles)
    {
        var required = Math.Max(slowPeriod + 2, atrPeriod + 2);
        if (candles.Count < required || candles.Any(x => x.Close <= 0 || x.High < x.Low))
            return Unknown("Insufficient or invalid candle history.");
        if (!candles.Select(x => x.Time).SequenceEqual(candles.Select(x => x.Time).OrderBy(x => x)))
            return Unknown("Candles are not chronologically ordered.");

        var closes = candles.Select(x => x.Close).ToArray();
        var fast = Indicators.Ema(closes, fastPeriod);
        var slow = Indicators.Ema(closes, slowPeriod);
        var atr = Indicators.Atr(candles, atrPeriod);
        var rsi = ExtendedIndicators.Rsi(closes, atrPeriod);
        var price = closes[^1];
        var atrFraction = atr / price;
        var separation = Math.Abs(fast - slow) / price;
        var evidence = new List<string> { $"ATR/price={atrFraction:F6}", $"EMA separation={separation:F6}", $"RSI={rsi:F2}" };

        if (atrFraction >= abnormalAtrFraction)
            return new(MarketRegime.Abnormal, Clamp(atrFraction / abnormalAtrFraction), fast, slow, atr, rsi, evidence);
        if (separation < trendSeparationFraction)
            return new(MarketRegime.Ranging, Clamp(1 - separation / trendSeparationFraction), fast, slow, atr, rsi, evidence);
        var regime = fast > slow ? MarketRegime.TrendingUp : MarketRegime.TrendingDown;
        return new(regime, Clamp(separation / (trendSeparationFraction * 3)), fast, slow, atr, rsi, evidence);
    }

    private static MarketState Unknown(string reason) => new(MarketRegime.Unknown, 0, 0, 0, 0, 0, [reason]);
    private static decimal Clamp(decimal value) => Math.Clamp(value, 0, 1);
}

public static class TradingSessions
{
    public static bool ContainsUtc(DateTimeOffset timestamp, TimeOnly start, TimeOnly end)
    {
        var time = TimeOnly.FromDateTime(timestamp.UtcDateTime);
        return start <= end ? time >= start && time < end : time >= start || time < end;
    }
}
