export type SystemMode = 'STOPPED' | 'STARTING' | 'RUNNING' | 'PAUSED' | 'MAINTENANCE' | 'DEGRADED' | 'EMERGENCY_LOCKED' | string;
export interface HealthStatus { status: string; mode: SystemMode; demoOnly: boolean }
export interface SystemStatus { mode: SystemMode; allowsNewEntries: boolean }
export interface Mt5Status { connected: boolean; accountMode: 'DEMO'|'REAL'|'CONTEST'|'UNKNOWN'; accountType?: 'HEDGING'|'NETTING'|'UNKNOWN'; emergencyLocked: boolean; heartbeatAt?: string; terminal?: string }
export interface SetupStatus { complete: boolean; steps: { id: string; label: string; complete: boolean; detail?: string }[] }
export interface InstrumentMapping { id: string; logicalSymbol: string; brokerSymbol?: string; enabled: boolean; valid: boolean; suggestedSymbol?: string }
export interface BrokerSymbol { name:string; description:string; currencyBase:string; currencyProfit:string; digits:number; point:number; tickSize:number; tickValue:number; contractSize:number; volumeMinimum:number; volumeMaximum:number; volumeStep:number; stopsLevelPoints:number; tradeAllowed:boolean }
export interface Position { id:string; brokerTicket?:string; symbol:string; side:'BUY'|'SELL'; volume:number; entryPrice:number; currentPrice?:number; stopLoss?:number; takeProfit?:number; unrealizedPnl?:number; riskAmount?:number; strategyId?:string; openedAt:string }
export interface Order { id:string; brokerTicket?:string; symbol:string; side:'BUY'|'SELL'; type:string; volume:number; price?:number; status:string; strategyId?:string; createdAt:string }
export interface RiskPolicy { maxRiskPerTradeFraction:number; maxTotalOpenRiskFraction:number; maxDailyLossFraction:number; maxConcurrentPositions:number; maxPositionsPerSymbol:number; maxGroupRiskFraction:number; minimumRewardRisk:number; maximumSpread:number }
export interface PortfolioRisk { equity:number; openRisk:number; openRiskFraction:number; dailyRealisedPnl:number; exposureGroups:{name:string;riskAmount:number;riskFraction:number;positions:number}[] }
const modeNames=['STOPPED','STARTING','RUNNING','PAUSED','MAINTENANCE','DEGRADED','EMERGENCY_LOCKED'] as const;
const normalizeMode=(mode:string|number):SystemMode=>typeof mode==='number'?(modeNames[mode]??'UNKNOWN'):mode.replace(/([a-z])([A-Z])/g,'$1_$2').toUpperCase();
const normalizeSystem=(value:{mode:string|number;allowsNewEntries?:boolean}):SystemStatus=>({mode:normalizeMode(value.mode),allowsNewEntries:value.allowsNewEntries??normalizeMode(value.mode)==='RUNNING'});
export class ApiError extends Error { constructor(public status: number, message: string) { super(message); this.name = 'ApiError'; } }
export class ScrapperTradeApi {
  constructor(private readonly baseUrl = '', private readonly fetcher: typeof fetch = fetch) {}
  private async request<T>(path: string, init?: RequestInit): Promise<T> {
    const controller = new AbortController(); const timer = setTimeout(() => controller.abort(), 8000);
    try {
      const response = await this.fetcher(`${this.baseUrl}${path}`, { ...init, signal: controller.signal, headers: { Accept: 'application/json', ...(init?.body ? {'Content-Type':'application/json'} : {}), ...init?.headers } });
      if (!response.ok) { let reason=''; try { const body=await response.json() as {reason?:string;message?:string}; reason=body.reason??body.message??''; } catch { /* response may not be JSON */ } throw new ApiError(response.status, reason || (response.status === 404 ? 'Capability is not available in this host build.' : `Request failed (${response.status}).`)); }
      return await response.json() as T;
    } catch (error) {
      if (error instanceof ApiError) throw error;
      if (error instanceof DOMException && error.name === 'AbortError') throw new ApiError(0, 'The local host did not respond in time.');
      throw new ApiError(0, 'Cannot reach the local ScrapperTrade host.');
    } finally { clearTimeout(timer); }
  }
  health = async () => { const value=await this.request<{status:string;mode:string|number;demoOnly:boolean}>('/api/health'); return {...value,mode:normalizeMode(value.mode)}; };
  system = async () => normalizeSystem(await this.request<{mode:string|number;allowsNewEntries?:boolean}>('/api/system'));
  mt5 = () => this.request<Mt5Status>('/api/mt5/status'); setup = () => this.request<SetupStatus>('/api/setup/status');
  symbols = () => this.request<BrokerSymbol[]>('/api/mt5/symbols');
  instruments = () => this.request<InstrumentMapping[]>('/api/instruments');
  positions = () => this.request<Position[]>('/api/positions'); orders = () => this.request<Order[]>('/api/orders');
  riskPolicy = () => this.request<RiskPolicy>('/api/risk/policy'); portfolioRisk = () => this.request<PortfolioRisk>('/api/risk/portfolio');
  pause = async () => normalizeSystem(await this.request<{mode:string|number}>('/api/system/pause',{method:'POST'}));
  start = async () => normalizeSystem(await this.request<{mode:string|number}>('/api/system/start',{method:'POST'}));
  emergencyStop = async () => normalizeSystem(await this.request<{mode:string|number}>('/api/system/emergency-stop',{method:'POST'}));
  closeAll = () => this.request<{accepted:boolean}>('/api/positions/close-all',{method:'POST'});
  closePosition = (id:string) => this.request<{accepted:boolean}>(`/api/positions/${encodeURIComponent(id)}/close`,{method:'POST'});
  pauseInstrument = (id:string) => this.request<{paused:boolean}>(`/api/instruments/${encodeURIComponent(id)}/pause`,{method:'POST'});
  pauseStrategy = (id:string) => this.request<{paused:boolean}>(`/api/strategies/${encodeURIComponent(id)}/pause`,{method:'POST'});
  saveMapping = (mapping:InstrumentMapping) => this.request<InstrumentMapping>(`/api/instruments/${encodeURIComponent(mapping.id)}`,{method:'PUT',body:JSON.stringify(mapping)});
}
export const isExplicitSimulator = (search = window.location.search) => import.meta.env.VITE_DATA_MODE === 'simulator' || new URLSearchParams(search).get('mode') === 'simulator';
const canonical=(value:string)=>value.toUpperCase().replace(/[^A-Z0-9]/g,'').replace(/(MICRO|MINI|PRO|RAW)$/,'').replace(/[._-]?[A-Z]$/,'');
export const suggestBrokerSymbols=(logical:string,symbols:BrokerSymbol[])=>symbols.filter(x=>x.tradeAllowed).map(symbol=>({symbol,score:symbol.name.toUpperCase()===logical.toUpperCase()?100:canonical(symbol.name)===canonical(logical)?80:symbol.name.toUpperCase().includes(logical.toUpperCase())?60:0})).filter(x=>x.score>0).sort((a,b)=>b.score-a.score||a.symbol.name.localeCompare(b.symbol.name)).map(x=>x.symbol);
