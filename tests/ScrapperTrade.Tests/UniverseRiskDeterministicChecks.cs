using ScrapperTrade.Application;
using ScrapperTrade.Domain;

internal static class UniverseRiskDeterministicChecks
{
    public static IEnumerable<(string, Action)> All()
    {
        yield return ("disabled instrument fails closed", Disabled);
        yield return ("direction permission rejects", Direction);
        yield return ("closed session rejects", Session);
        yield return ("overnight session admits next day", OvernightSession);
        yield return ("duplicate signal rejects", Duplicate);
        yield return ("order frequency rejects", Frequency);
        yield return ("correlated direction exposure rejects", Correlated);
        yield return ("opposite correlated direction remains independent", OppositeDirection);
    }

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-21T10:00:00Z");
    private static SymbolMetadata Metadata() => new("XAUUSD", .01m, 1m, 100m, .01m, 100m, .01m);
    private static CandidateTrade Trade(Guid? signal = null, Side side = Side.Buy) => side == Side.Buy
        ? new(signal ?? Guid.NewGuid(), "trend", 1, "XAUUSD", side, 2000, 1990, 2020, .2m, Now)
        : new(signal ?? Guid.NewGuid(), "trend", 1, "XAUUSD", side, 2000, 2010, 1980, .2m, Now);
    private static RiskDecision Evaluate(CandidateTrade trade, PortfolioSnapshot? snapshot = null, RiskPolicy? policy = null, InstrumentRiskPermissions? permissions = null) =>
        new PortfolioRiskEngine(policy ?? new()).Evaluate(trade, Metadata(), snapshot ?? new(10_000, 0, []), "METALS", Now, permissions ?? new());
    private static void Expect(string code, RiskDecision result) { if (result.Code != code) throw new Exception($"Expected {code}, got {result.Code}"); }

    private static void Disabled() => Expect("INSTRUMENT_DISABLED", Evaluate(Trade(), permissions: new(false)));
    private static void Direction() => Expect("DIRECTION_BLOCKED", Evaluate(Trade(side: Side.Sell), permissions: new(true, TradeDirectionPermission.LongOnly)));
    private static void Session() => Expect("SESSION_CLOSED", Evaluate(Trade(), permissions: new(true, Sessions: [new(DayOfWeek.Friday, new(11, 0), new(12, 0))])));
    private static void OvernightSession()
    {
        var monday = DateTimeOffset.Parse("2026-08-24T23:00:00Z");
        var tuesday = monday.AddHours(2);
        var session = new TradingSession(DayOfWeek.Monday, new(22, 0), new(2, 0));
        if (!session.Contains(tuesday)) throw new Exception("Overnight continuation should be open.");
    }
    private static void Duplicate()
    {
        var id = Guid.NewGuid();
        Expect("DUPLICATE_SIGNAL", Evaluate(Trade(id), new(10_000, 0, [], [new(id, "XAUUSD", Now.AddMinutes(-1))])));
    }
    private static void Frequency()
    {
        var orders = Enumerable.Range(0, 3).Select(i => new RecentOrder(Guid.NewGuid(), "XAUUSD", Now.AddMinutes(-i))).ToArray();
        Expect("ORDER_FREQUENCY", Evaluate(Trade(), new(10_000, 0, [], orders)));
    }
    private static void Correlated()
    {
        var existing = new OpenPosition(Guid.NewGuid(), "XAGUSD", Side.Buy, 1, .9m, .1m, 51, "METALS", Now.AddHours(-1));
        Expect("CORRELATED_EXPOSURE", Evaluate(Trade(), new(10_000, 0, [existing]), new(MaxGroupRiskFraction: .02m, MaxCorrelatedDirectionRiskFraction: .01m)));
    }
    private static void OppositeDirection()
    {
        var existing = new OpenPosition(Guid.NewGuid(), "XAGUSD", Side.Sell, 1, 1.1m, .1m, 50, "METALS", Now.AddHours(-1));
        Expect("APPROVED", Evaluate(Trade(), new(10_000, 0, [existing]), new(MaxGroupRiskFraction: .02m, MaxCorrelatedDirectionRiskFraction: .01m)));
    }
}
