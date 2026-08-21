namespace ScrapperTrade.Domain;

public sealed record SystemStateSnapshot(
    SystemMode Mode,
    bool AllowsNewEntries,
    long Version,
    DateTimeOffset ChangedAt,
    string Reason);

public sealed class SystemStateMachine
{
    private readonly object gate = new();
    private SystemStateSnapshot state = new(
        SystemMode.Stopped, false, 0, DateTimeOffset.UtcNow, "Initial state");

    public SystemStateSnapshot Snapshot
    {
        get { lock (gate) return state; }
    }

    public SystemStateSnapshot Start(DateTimeOffset now, string reason = "User started system") =>
        Transition(SystemMode.Running, now, reason, SystemMode.Stopped, SystemMode.Paused, SystemMode.Degraded);

    public SystemStateSnapshot Pause(DateTimeOffset now, string reason = "User paused new entries") =>
        Transition(SystemMode.Paused, now, reason, SystemMode.Running, SystemMode.Starting, SystemMode.Degraded);

    public SystemStateSnapshot Stop(DateTimeOffset now, string reason = "User stopped system") =>
        Transition(SystemMode.Stopped, now, reason, SystemMode.Running, SystemMode.Paused,
            SystemMode.Maintenance, SystemMode.Degraded);

    public SystemStateSnapshot Degrade(DateTimeOffset now, string reason) =>
        Transition(SystemMode.Degraded, now, RequireReason(reason), SystemMode.Starting, SystemMode.Running,
            SystemMode.Paused, SystemMode.Maintenance);

    public SystemStateSnapshot EmergencyLock(DateTimeOffset now, string reason) =>
        Set(SystemMode.EmergencyLocked, now, RequireReason(reason));

    public SystemStateSnapshot UserUnlockToPaused(DateTimeOffset now, string reason)
    {
        lock (gate)
        {
            if (state.Mode != SystemMode.EmergencyLocked)
                throw new InvalidOperationException("Emergency unlock is valid only while emergency locked.");
            return SetUnsafe(SystemMode.Paused, now, RequireReason(reason));
        }
    }

    private SystemStateSnapshot Transition(
        SystemMode target, DateTimeOffset now, string reason, params SystemMode[] allowedFrom)
    {
        lock (gate)
        {
            if (state.Mode == SystemMode.EmergencyLocked)
                throw new InvalidOperationException("Explicit user emergency unlock is required.");
            if (!allowedFrom.Contains(state.Mode))
                throw new InvalidOperationException($"Cannot transition from {state.Mode} to {target}.");
            return SetUnsafe(target, now, RequireReason(reason));
        }
    }

    private SystemStateSnapshot Set(SystemMode target, DateTimeOffset now, string reason)
    {
        lock (gate) return SetUnsafe(target, now, reason);
    }

    private SystemStateSnapshot SetUnsafe(SystemMode target, DateTimeOffset now, string reason)
    {
        state = new(target, target == SystemMode.Running, state.Version + 1, now, reason);
        return state;
    }

    private static string RequireReason(string reason) =>
        string.IsNullOrWhiteSpace(reason) ? throw new ArgumentException("A reason is required.") : reason.Trim();
}

public enum InstrumentAvailability { Active, Avoid, Paused }
public enum DirectionPermission { Both, LongOnly, ShortOnly }

public sealed record TradingInstrument(
    string LogicalName,
    bool UserAllowed,
    string? BrokerSymbol,
    InstrumentAvailability Availability = InstrumentAvailability.Paused,
    DirectionPermission Directions = DirectionPermission.Both,
    int MaxPositions = 1,
    decimal MaxCombinedRiskFraction = .005m,
    decimal MaximumSpread = 0,
    string ExposureGroup = "UNASSIGNED")
{
    public bool CanOpenNewPosition =>
        UserAllowed && Availability == InstrumentAvailability.Active &&
        !string.IsNullOrWhiteSpace(BrokerSymbol);

    public TradingInstrument Validate()
    {
        if (string.IsNullOrWhiteSpace(LogicalName)) throw new ArgumentException("Logical name is required.");
        if (MaxPositions < 1) throw new ArgumentOutOfRangeException(nameof(MaxPositions));
        if (MaxCombinedRiskFraction <= 0) throw new ArgumentOutOfRangeException(nameof(MaxCombinedRiskFraction));
        if (MaximumSpread < 0) throw new ArgumentOutOfRangeException(nameof(MaximumSpread));
        if (!UserAllowed && Availability == InstrumentAvailability.Active)
            throw new InvalidOperationException("A disallowed instrument cannot be active.");
        return this;
    }

    public TradingInstrument SetUserPermission(bool allowed) =>
        (this with
        {
            UserAllowed = allowed,
            Availability = allowed ? InstrumentAvailability.Paused : InstrumentAvailability.Paused
        }).Validate();

    public TradingInstrument SetAutomatedAvailability(InstrumentAvailability availability)
    {
        if (!UserAllowed && availability == InstrumentAvailability.Active)
            throw new InvalidOperationException("Automation cannot enable a user-disallowed instrument.");
        return (this with { Availability = availability }).Validate();
    }
}
