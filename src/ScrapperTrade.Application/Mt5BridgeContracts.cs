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

public enum Mt5CommandAction { Buy, Sell, Close }

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
