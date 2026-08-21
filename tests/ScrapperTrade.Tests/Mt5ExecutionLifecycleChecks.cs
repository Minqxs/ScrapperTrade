using ScrapperTrade.Application;
using ScrapperTrade.Infrastructure;

internal static class Mt5ExecutionLifecycleChecks
{
    public static IEnumerable<(string Name, Action Check)> All()
    {
        yield return ("mt5 locked host rejects close", LockedRejectsClose);
        yield return ("mt5 close-all cancels before closing", CloseAllOrdering);
        yield return ("mt5 execution snapshots deserialize", SnapshotReading);
        yield return ("mt5 restart reconciliation detects drift", RestartReconciliation);
        yield return ("mt5 result acknowledgement correlates", ResultCorrelation);
    }

    private static void LockedRejectsClose()
    {
        WithRoot(root =>
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);
            Write(root, "heartbeat.json", $"{{\"sequence\":1,\"time\":{now.ToUnixTimeSeconds()},\"accountMode\":\"DEMO\",\"positionMode\":\"HEDGING\",\"connected\":true,\"emergencyLocked\":true}}");
            var command = new Mt5Command(Guid.NewGuid(), now, now.AddSeconds(10), 1, Mt5CommandAction.Close, "", 0, 0, 0, 0, 42);
            Throws<InvalidOperationException>(() => new Mt5CommonFilesCommandQueue(root).Enqueue(command, now));
            Equal(false, Directory.Exists(Path.Combine(root, "ScrapperTrade", "commands")));
        });
    }

    private static void CloseAllOrdering()
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);
        var orders = new Mt5ExecutionSnapshot<Mt5BrokerOrder>(2, at, [new(20, "EURUSD", Mt5PendingOrderKind.BuyLimit, 1, 1, .9m, 1.1m, 7, "s", at)]);
        var positions = new Mt5ExecutionSnapshot<Mt5BrokerPosition>(2, at, [new(30, "XAUUSD", Mt5BrokerSide.Buy, .1m, 2000, 1990, 2020, 2001, 1, 8, "s", at)]);
        var commands = Mt5CloseAllPlanner.Create(orders, positions, at, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), 10).InSafetyOrder().ToArray();
        Equal(Mt5CommandAction.Cancel, commands[0].Action);
        Equal(Mt5CommandAction.Close, commands[1].Action);
        Equal(10L, commands[0].Sequence);
        Equal(11L, commands[1].Sequence);
    }

    private static void SnapshotReading()
    {
        WithRoot(root =>
        {
            Write(root, "positions.json", "{\"sequence\":4,\"observedAt\":\"2033-05-18T03:33:20Z\",\"items\":[{\"ticket\":42,\"symbol\":\"XAUUSD\",\"side\":\"Buy\",\"volume\":0.1,\"openPrice\":2000,\"stopLoss\":1990,\"takeProfit\":2020,\"currentPrice\":2001,\"profit\":10,\"magicNumber\":7,\"comment\":\"trade-1\",\"openedAt\":\"2033-05-18T03:30:00Z\"}]}");
            var snapshot = new Mt5CommonFilesExecutionSnapshotReader(root).ReadPositions();
            Equal(42UL, snapshot!.Items.Single().Ticket);
            Equal(Mt5BrokerSide.Buy, snapshot.Items.Single().Side);
        });
    }

    private static void RestartReconciliation()
    {
        var at = DateTimeOffset.UtcNow;
        var positions = new Mt5ExecutionSnapshot<Mt5BrokerPosition>(1, at, [new(2, "X", Mt5BrokerSide.Buy, 1, 1, 1, 1, 1, 0, 0, "", at)]);
        var orders = new Mt5ExecutionSnapshot<Mt5BrokerOrder>(1, at, [new(4, "Y", Mt5PendingOrderKind.Other, 1, 1, 1, 1, 0, "", at)]);
        var report = Mt5RestartReconciler.Reconcile([1, 2], [3, 4], positions, orders, at, TimeSpan.FromSeconds(5));
        Equal(false, report.IsConsistent);
        Equal(1UL, report.MissingTrackedPositionTickets.Single());
        Equal(3UL, report.MissingTrackedOrderTickets.Single());
    }

    private static void ResultCorrelation()
    {
        WithRoot(root =>
        {
            var requested = Guid.NewGuid();
            Write(root, $"results/{requested:D}.json", $"{{\"commandId\":\"{Guid.NewGuid():D}\",\"accepted\":false,\"reason\":\"locked\"}}");
            Throws<InvalidDataException>(() => new Mt5CommonFilesCommandQueue(root).TryReadResult(requested));
        });
    }

    private static void WithRoot(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "ScrapperTrade-ExecutionTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { action(root); }
        finally { Directory.Delete(root, true); }
    }

    private static void Write(string root, string relative, string content)
    {
        var path = Path.Combine(root, "ScrapperTrade", relative.Replace('/', Path.DirectorySeparatorChar));
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
