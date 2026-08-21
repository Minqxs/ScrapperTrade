namespace ScrapperTrade.Domain;
public enum SystemMode { Stopped, Starting, Running, Paused, Maintenance, Degraded, EmergencyLocked }
public enum AccountKind { Unknown, Demo, Contest, Real }
public enum Side { Buy, Sell }
public enum TradeDirectionPermission { Disabled, Both, LongOnly, ShortOnly }
public enum MarketRegime { Unknown, TrendingUp, TrendingDown, Ranging, Breakout, HighVolatility, LowVolatility, Abnormal }
public sealed record SymbolMetadata(string Symbol, decimal TickSize, decimal TickValue, decimal ContractSize, decimal VolumeMin, decimal VolumeMax, decimal VolumeStep, decimal StopLevel = 0);
public sealed record CandidateTrade(Guid SignalId, string StrategyId, int StrategyVersion, string Instrument, Side Side, decimal Entry, decimal Stop, decimal Target, decimal Spread, DateTimeOffset MarketTimestamp, decimal Confidence = 1m, string Rationale = "");
public sealed record OpenPosition(Guid TradeId, string Instrument, Side Side, decimal Entry, decimal Stop, decimal Volume, decimal RiskAmount, string ExposureGroup, DateTimeOffset OpenedAt);
public sealed record RiskPolicy(decimal MaxRiskPerTradeFraction = .005m, decimal MaxTotalOpenRiskFraction = .02m, decimal MaxDailyLossFraction = .02m, int MaxConcurrentPositions = 5, int MaxPositionsPerSymbol = 2, decimal MaxGroupRiskFraction = .01m, decimal MinimumRewardRisk = 1.5m, decimal MaximumSpread = 5m, TimeSpan? MaximumDataAge = null, int MaxOrdersPerWindow = 3, TimeSpan? OrderFrequencyWindow = null, decimal MaxCorrelatedDirectionRiskFraction = .01m) { public TimeSpan DataAge => MaximumDataAge ?? TimeSpan.FromSeconds(30); public TimeSpan FrequencyWindow => OrderFrequencyWindow ?? TimeSpan.FromMinutes(5); }
public sealed record RecentOrder(Guid SignalId, string Instrument, DateTimeOffset SubmittedAt);
public sealed record PortfolioSnapshot(decimal Equity, decimal DailyRealisedPnl, IReadOnlyList<OpenPosition> Positions, IReadOnlyList<RecentOrder>? RecentOrders = null);
public sealed record TradingSession(DayOfWeek Day, TimeOnly StartsAtUtc, TimeOnly EndsAtUtc)
{
 public bool Contains(DateTimeOffset at){var utc=at.UtcDateTime;var time=TimeOnly.FromDateTime(utc);if(StartsAtUtc<=EndsAtUtc)return utc.DayOfWeek==Day&&time>=StartsAtUtc&&time<EndsAtUtc;var previous=(DayOfWeek)(((int)utc.DayOfWeek+6)%7);return (utc.DayOfWeek==Day&&time>=StartsAtUtc)||(previous==Day&&time<EndsAtUtc);}
}
public sealed record InstrumentRiskPermissions(bool Enabled = true, TradeDirectionPermission Direction = TradeDirectionPermission.Both, IReadOnlyList<TradingSession>? Sessions = null)
{
 public bool Allows(Side side)=>Enabled&&Direction switch { TradeDirectionPermission.Both=>true, TradeDirectionPermission.LongOnly=>side==Side.Buy, TradeDirectionPermission.ShortOnly=>side==Side.Sell, _=>false };
 public bool IsSessionOpen(DateTimeOffset at)=>Sessions is null||Sessions.Count==0||Sessions.Any(x=>x.Contains(at));
}
public sealed record RiskDecision(bool Approved, string Code, string Reason, decimal? Volume = null, decimal? RiskAmount = null);
public sealed class TradingSystemState {
 public SystemMode Mode { get; private set; } = SystemMode.Stopped; public bool AllowsNewEntries => Mode == SystemMode.Running;
 public void Start() { if (Mode is SystemMode.EmergencyLocked) throw new InvalidOperationException("User unlock required."); Mode = SystemMode.Running; }
 public void Pause()=>Mode=SystemMode.Paused; public void Stop()=>Mode=SystemMode.Stopped; public void EmergencyLock()=>Mode=SystemMode.EmergencyLocked;
 public void UserUnlockToPaused(){if(Mode!=SystemMode.EmergencyLocked)throw new InvalidOperationException("Not locked.");Mode=SystemMode.Paused;}
}
public sealed record ExecutionRequest(Guid CommandId, CandidateTrade Trade, decimal Volume, DateTimeOffset CreatedAt);
public sealed record ExecutionResult(bool Accepted, string Code, string Message, Guid? TradeId = null);
