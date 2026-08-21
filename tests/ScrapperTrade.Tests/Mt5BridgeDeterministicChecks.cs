using ScrapperTrade.Application;
using ScrapperTrade.Infrastructure;

internal static class Mt5BridgeDeterministicChecks
{
    public static IEnumerable<(string Name, Action Check)> All()
    {
        yield return ("mt5 heartbeat positive demo", PositiveDemoHeartbeat);
        yield return ("mt5 heartbeat stale fails closed", StaleHeartbeat);
        yield return ("mt5 heartbeat malformed fails closed", MalformedHeartbeat);
        yield return ("mt5 command atomic and idempotent", AtomicIdempotentCommand);
        yield return ("mt5 stale command rejected host side", StaleCommand);
        yield return ("mt5 symbol metadata discovery", SymbolDiscovery);
    }

    private static void PositiveDemoHeartbeat()
    {
        WithRoot(root =>
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);
            Write(root, "ScrapperTrade/heartbeat.json", $"{{\"sequence\":7,\"time\":{now.ToUnixTimeSeconds()},\"accountMode\":\"DEMO\",\"positionMode\":\"HEDGING\",\"connected\":true,\"emergencyLocked\":true}}");
            var snapshot = new Mt5CommonFilesHeartbeatReader(root).Read(now);
            Equal(true, snapshot.IsPositiveDemo);
            Equal(false, snapshot.AllowsOrderTransmission);
            Equal(Mt5PositionMode.Hedging, snapshot.PositionMode);
        });
    }

    private static void StaleHeartbeat()
    {
        WithRoot(root =>
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);
            Write(root, "ScrapperTrade/heartbeat.json", $"{{\"sequence\":7,\"time\":{now.AddSeconds(-6).ToUnixTimeSeconds()},\"accountMode\":\"DEMO\",\"connected\":true,\"emergencyLocked\":false}}");
            var snapshot = new Mt5CommonFilesHeartbeatReader(root).Read(now);
            Equal(false, snapshot.IsPositiveDemo);
            Equal(false, snapshot.AllowsOrderTransmission);
        });
    }

    private static void MalformedHeartbeat()
    {
        WithRoot(root =>
        {
            Write(root, "ScrapperTrade/heartbeat.json", "not-json");
            var snapshot = new Mt5CommonFilesHeartbeatReader(root).Read(DateTimeOffset.UtcNow);
            Equal(Mt5AccountSafetyMode.Unknown, snapshot.AccountMode);
            Equal(true, snapshot.EmergencyLocked);
            Equal(false, snapshot.IsPositiveDemo);
        });
    }

    private static void AtomicIdempotentCommand()
    {
        WithRoot(root =>
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);
            var command = new Mt5Command(Guid.NewGuid(), now, now.AddSeconds(10), 1, Mt5CommandAction.Buy, "XAUUSD", .1m, 2000m, 1990m, 2020m);
            var queue = new Mt5CommonFilesCommandQueue(root);
            var path = queue.Enqueue(command, now);
            Equal(true, File.Exists(path));
            Equal(11, File.ReadAllText(path).Split('|').Length);
            Throws<InvalidOperationException>(() => queue.Enqueue(command, now));
        });
    }

    private static void StaleCommand()
    {
        WithRoot(root =>
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);
            var command = new Mt5Command(Guid.NewGuid(), now.AddSeconds(-20), now.AddSeconds(-1), 1, Mt5CommandAction.Close, "", 0, 0, 0, 0, 12);
            Throws<InvalidOperationException>(() => new Mt5CommonFilesCommandQueue(root).Enqueue(command, now));
        });
    }

    private static void SymbolDiscovery()
    {
        WithRoot(root =>
        {
            Write(root, "ScrapperTrade/symbols.json", "[{\"name\":\"XAUUSDm\",\"description\":\"Gold\",\"currencyBase\":\"XAU\",\"currencyProfit\":\"USD\",\"digits\":2,\"point\":0.01,\"tickSize\":0.01,\"tickValue\":1,\"contractSize\":100,\"volumeMinimum\":0.01,\"volumeMaximum\":100,\"volumeStep\":0.01,\"stopsLevelPoints\":10,\"tradeAllowed\":true}]");
            var symbols = new Mt5CommonFilesSymbolReader(root).Read();
            Equal("XAUUSDm", symbols.Single().Name);
            Equal(.01m, symbols.Single().TickSize);
        });
    }

    private static void WithRoot(Action<string> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "ScrapperTrade-Mt5Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { check(root); }
        finally { Directory.Delete(root, true); }
    }

    private static void Write(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
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
