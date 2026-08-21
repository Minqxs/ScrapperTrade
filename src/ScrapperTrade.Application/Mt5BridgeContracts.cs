namespace ScrapperTrade.Application;

public enum Mt5AccountSafetyMode { Unknown, Demo, Real, Contest }
public enum Mt5PositionMode { Unknown, Hedging, Netting }

public sealed record Mt5ConnectionSnapshot(
    long Sequence,
    DateTimeOffset ObservedAt,
    DateTimeOffset ReceivedAt,
    bool Connected,
    Mt5AccountSafetyMode AccountMode,
    Mt5PositionMode PositionMode,
    bool EmergencyLocked,
    TimeSpan MaximumAge)
{
    public TimeSpan Age => ReceivedAt - ObservedAt;
    public bool IsFresh => Age >= TimeSpan.Zero && Age <= MaximumAge;
    public bool IsPositiveDemo => Connected && IsFresh && AccountMode == Mt5AccountSafetyMode.Demo;
    public bool AllowsOrderTransmission => IsPositiveDemo && !EmergencyLocked;
}

public sealed record Mt5BrokerSymbol(
    string Name,
    string Description,
    string CurrencyBase,
    string CurrencyProfit,
    int Digits,
    decimal Point,
    decimal TickSize,
    decimal TickValue,
    decimal ContractSize,
    decimal VolumeMinimum,
    decimal VolumeMaximum,
    decimal VolumeStep,
    int StopsLevelPoints,
    bool TradeAllowed);

public enum Mt5CommandAction { Buy, Sell, Close, Cancel }

public sealed record Mt5Command(
    Guid CommandId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    long Sequence,
    Mt5CommandAction Action,
    string Symbol,
    decimal Volume,
    decimal Price,
    decimal StopLoss,
    decimal TakeProfit,
    ulong Ticket = 0);

public sealed record Mt5CommandResult(
    Guid CommandId,
    bool Accepted,
    string Reason,
    DateTimeOffset? CompletedAt,
    ulong? BrokerOrder);

public enum Mt5BrokerSide { Buy, Sell }
public enum Mt5PendingOrderKind { BuyLimit, SellLimit, BuyStop, SellStop, Other }

public sealed record Mt5BrokerPosition(
    ulong Ticket,
    string Symbol,
    Mt5BrokerSide Side,
    decimal Volume,
    decimal OpenPrice,
    decimal StopLoss,
    decimal TakeProfit,
    decimal CurrentPrice,
    decimal Profit,
    long MagicNumber,
    string Comment,
    DateTimeOffset OpenedAt);

public sealed record Mt5BrokerOrder(
    ulong Ticket,
    string Symbol,
    Mt5PendingOrderKind Kind,
    decimal Volume,
    decimal Price,
    decimal StopLoss,
    decimal TakeProfit,
    long MagicNumber,
    string Comment,
    DateTimeOffset CreatedAt);

public sealed record Mt5ExecutionSnapshot<T>(long Sequence, DateTimeOffset ObservedAt, IReadOnlyList<T> Items);

public sealed record Mt5CloseAllPlan(IReadOnlyList<Mt5Command> CancelOrders, IReadOnlyList<Mt5Command> ClosePositions)
{
    public IEnumerable<Mt5Command> InSafetyOrder() => CancelOrders.Concat(ClosePositions);
}

public static class Mt5CloseAllPlanner
{
    public static Mt5CloseAllPlan Create(Mt5ExecutionSnapshot<Mt5BrokerOrder> orders, Mt5ExecutionSnapshot<Mt5BrokerPosition> positions, DateTimeOffset now, TimeSpan maximumSnapshotAge, TimeSpan lifetime, long firstSequence)
    {
        if (firstSequence <= 0) throw new ArgumentOutOfRangeException(nameof(firstSequence));
        if (orders.Sequence != positions.Sequence) throw new InvalidOperationException("Close-all requires position and order snapshots from the same EA sequence.");
        if (orders.ObservedAt > now || positions.ObservedAt > now || now - orders.ObservedAt > maximumSnapshotAge || now - positions.ObservedAt > maximumSnapshotAge)
            throw new InvalidOperationException("Close-all requires fresh, non-future broker snapshots.");
        var sequence = firstSequence;
        var cancels = orders.Items.OrderBy(x => x.Ticket).Select(order => Control(Mt5CommandAction.Cancel, order.Ticket, now, lifetime, sequence++)).ToArray();
        var closes = positions.Items.OrderBy(x => x.Ticket).Select(position => Control(Mt5CommandAction.Close, position.Ticket, now, lifetime, sequence++)).ToArray();
        return new(cancels, closes);
    }

    private static Mt5Command Control(Mt5CommandAction action, ulong ticket, DateTimeOffset now, TimeSpan lifetime, long sequence) =>
        new(Guid.NewGuid(), now, now.Add(lifetime), sequence, action, "", 0, 0, 0, 0, ticket);
}

public sealed record Mt5ReconciliationReport(
    IReadOnlyList<ulong> ConfirmedPositionTickets,
    IReadOnlyList<ulong> MissingTrackedPositionTickets,
    IReadOnlyList<ulong> UntrackedBrokerPositionTickets,
    IReadOnlyList<ulong> ConfirmedOrderTickets,
    IReadOnlyList<ulong> MissingTrackedOrderTickets,
    IReadOnlyList<ulong> UntrackedBrokerOrderTickets)
{
    public bool IsConsistent => MissingTrackedPositionTickets.Count == 0 && UntrackedBrokerPositionTickets.Count == 0 && MissingTrackedOrderTickets.Count == 0 && UntrackedBrokerOrderTickets.Count == 0;
}

public static class Mt5RestartReconciler
{
    public static Mt5ReconciliationReport Reconcile(IEnumerable<ulong> trackedPositions, IEnumerable<ulong> trackedOrders, Mt5ExecutionSnapshot<Mt5BrokerPosition> brokerPositions, Mt5ExecutionSnapshot<Mt5BrokerOrder> brokerOrders, DateTimeOffset now, TimeSpan maximumSnapshotAge)
    {
        if (brokerPositions.Sequence != brokerOrders.Sequence) throw new InvalidOperationException("Reconciliation requires position and order snapshots from the same EA sequence.");
        if (brokerPositions.ObservedAt > now || brokerOrders.ObservedAt > now || now - brokerPositions.ObservedAt > maximumSnapshotAge || now - brokerOrders.ObservedAt > maximumSnapshotAge)
            throw new InvalidOperationException("Reconciliation requires fresh, non-future broker snapshots.");
        var expectedPositions = trackedPositions.ToHashSet();
        var actualPositions = brokerPositions.Items.Select(x => x.Ticket).ToHashSet();
        var expectedOrders = trackedOrders.ToHashSet();
        var actualOrders = brokerOrders.Items.Select(x => x.Ticket).ToHashSet();
        if (actualPositions.Count != brokerPositions.Items.Count || actualOrders.Count != brokerOrders.Items.Count)
            throw new InvalidOperationException("Broker snapshots contain duplicate tickets.");
        return new(
            expectedPositions.Intersect(actualPositions).Order().ToArray(), expectedPositions.Except(actualPositions).Order().ToArray(), actualPositions.Except(expectedPositions).Order().ToArray(),
            expectedOrders.Intersect(actualOrders).Order().ToArray(), expectedOrders.Except(actualOrders).Order().ToArray(), actualOrders.Except(expectedOrders).Order().ToArray());
    }
}
