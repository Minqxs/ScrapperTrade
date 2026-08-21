using ScrapperTrade.Domain; using ScrapperTrade.Quant;
namespace ScrapperTrade.Application;
public interface IExecutionAdapter { ExecutionResult Execute(ExecutionRequest request); }
public interface IAuditStore { void Append(AuditEvent item); IReadOnlyList<AuditEvent> ReadAll(); }
public sealed record AuditEvent(Guid Id, DateTimeOffset At, string Type, string Code, string Detail);
public sealed class PortfolioRiskEngine {
 readonly RiskPolicy p; public PortfolioRiskEngine(RiskPolicy policy)=>p=policy;
 public RiskDecision Evaluate(CandidateTrade t,SymbolMetadata s,PortfolioSnapshot x,string group,DateTimeOffset now){
  if(x.Equity<=0)return No("EQUITY_INVALID","Positive equity is required."); if(t.Entry<=0||t.Stop<=0||t.Target<=0)return No("PRICE_INVALID","Prices must be positive.");
  if((t.Side==Side.Buy&&t.Stop>=t.Entry)||(t.Side==Side.Sell&&t.Stop<=t.Entry))return No("STOP_INVALID","Stop must be on the loss side.");
  var d=Math.Abs(t.Entry-t.Stop); if(Math.Abs(t.Target-t.Entry)/d<p.MinimumRewardRisk)return No("REWARD_RISK","Reward/risk is below policy.");
  if(t.Spread>p.MaximumSpread)return No("SPREAD","Spread exceeds policy."); if(now-t.MarketTimestamp>p.DataAge||t.MarketTimestamp>now.AddSeconds(1))return No("STALE_DATA","Market data is stale or future-dated.");
  if(x.DailyRealisedPnl<=-x.Equity*p.MaxDailyLossFraction)return No("DAILY_LOSS","Daily loss limit reached."); if(x.Positions.Count>=p.MaxConcurrentPositions)return No("POSITION_LIMIT","Portfolio position limit reached.");
  if(x.Positions.Count(z=>z.Instrument==t.Instrument)>=p.MaxPositionsPerSymbol)return No("SYMBOL_LIMIT","Symbol position limit reached."); var size=PositionSizer.Calculate(x.Equity*p.MaxRiskPerTradeFraction,t.Entry,t.Stop,s); if(!size.Success)return No("SIZING",size.Error!);
  if(x.Positions.Sum(z=>z.RiskAmount)+size.ActualRisk>x.Equity*p.MaxTotalOpenRiskFraction)return No("OPEN_RISK","Total open risk limit exceeded.");
  if(x.Positions.Where(z=>z.ExposureGroup==group).Sum(z=>z.RiskAmount)+size.ActualRisk>x.Equity*p.MaxGroupRiskFraction)return No("GROUP_RISK","Exposure group limit exceeded."); return new(true,"APPROVED","Hard risk policy passed.",size.Volume,size.ActualRisk);
 } static RiskDecision No(string c,string r)=>new(false,c,r);
}
public sealed class TradeCoordinator {
 readonly TradingSystemState s;readonly PortfolioRiskEngine r;readonly IExecutionAdapter e;readonly IAuditStore a; public TradeCoordinator(TradingSystemState state,PortfolioRiskEngine risk,IExecutionAdapter execution,IAuditStore audit)=>(s,r,e,a)=(state,risk,execution,audit);
 public ExecutionResult Submit(CandidateTrade t,SymbolMetadata m,PortfolioSnapshot p,string g,DateTimeOffset now){if(!s.AllowsNewEntries)return Record("SYSTEM_BLOCKED",$"System is {s.Mode}.");var d=r.Evaluate(t,m,p,g,now);if(!d.Approved)return Record(d.Code,d.Reason);var z=e.Execute(new(Guid.NewGuid(),t,d.Volume!.Value,now));a.Append(new(Guid.NewGuid(),now,"EXECUTION",z.Code,z.Message));return z;}
 ExecutionResult Record(string c,string m){a.Append(new(Guid.NewGuid(),DateTimeOffset.UtcNow,"REJECTION",c,m));return new(false,c,m);}
}
