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
export interface StrategySpec { id:string; name:string; version:number; status:'DRAFT'|'VALIDATED'|'SHADOW'|'PAUSED'|'RETIRED'; description:string; instruments:string[]; timeframes:string[]; regimes:string[]; entry:{all:string[]}; exit:{stopLoss:string;takeProfit:string;trailing?:string}; risk:{maxRiskFraction:number;maxPositions:number}; sourceIds:string[]; updatedAt:string }
export interface BacktestRun { id:string; strategyId:string; strategyVersion:number; status:'QUEUED'|'RUNNING'|'COMPLETED'|'FAILED'; startedAt:string; completedAt?:string; inSample:boolean; costModel:string; metrics?:{trades:number;expectancy:number;profitFactor:number;maxDrawdownFraction:number;netReturnFraction:number}; equity?:{at:string;equity:number;drawdownFraction:number}[]; trades?:{id:string;symbol:string;side:string;openedAt:string;closedAt:string;pnl:number;rMultiple:number;cost:number;reason:string}[] }
export interface AutonomyStatus { mode:'OFF'|'SIMULATOR'|'SHADOW'|'DEMO'; executionEnabled:boolean; brokerOrdersAllowed:boolean; scheduler:string; lastDecisionAt?:string; strategies:{id:string;name:string;mode:string;status:string;reason?:string}[] }
export interface KnowledgeSource { id:string; title:string; kind:'DOCUMENT'|'VIDEO'|'AUDIO'|'NOTE'; status:'UPLOADED'|'PROCESSING'|'READY'|'FAILED'; fileName?:string; createdAt:string; chunks:number; citationCount:number; error?:string }
export interface KnowledgeHit { id:string; sourceId:string; sourceTitle:string; excerpt:string; locator:string; score:number }
export interface ResearchCandidate { id:string; name:string; hypothesis:string; status:'DRAFT'|'NEEDS_EVIDENCE'|'READY_FOR_REVIEW'|'APPROVED_FOR_VALIDATION'|'REJECTED'; sourceCitations:{sourceId:string;sourceTitle:string;locator:string}[]; validationSummary?:string; ambiguities:string[]; createdAt:string }
export interface AuditRecord { id:number; occurredAt:string; category:string; action:string; outcome:string; detail:string; correlationId?:string }
export interface SystemEvent { id:number; occurredAt:string; severity:'Information'|'Warning'|'Error'|'Critical'|string; eventType:string; detail:string; correlationId?:string }
export interface DiagnosticStatus { overall:'HEALTHY'|'DEGRADED'|'UNHEALTHY'|'UNKNOWN'; checkedAt:string; checks:{id:string;label:string;status:'HEALTHY'|'DEGRADED'|'UNHEALTHY'|'UNKNOWN';detail:string;lastSuccessAt?:string;recovery?:string}[] }
export interface RecoveryStatus { cleanShutdown?:boolean; reconciliationRequired:boolean; queueDepth?:number; staleCommands?:number; lastBackupAt?:string; databaseStatus:string; detail?:string }
export interface ProviderSetting { id:'MANUAL_CHATGPT'|'LOCAL'|'OPENAI'|string; name:string; enabled:boolean; configured:boolean; optional:boolean; status:string; detail?:string; model?:string }
export interface ProviderUpdate { enabled:boolean; model?:string }
const modeNames=['STOPPED','STARTING','RUNNING','PAUSED','MAINTENANCE','DEGRADED','EMERGENCY_LOCKED'] as const;
const normalizeMode=(mode:string|number):SystemMode=>typeof mode==='number'?(modeNames[mode]??'UNKNOWN'):mode.replace(/([a-z])([A-Z])/g,'$1_$2').toUpperCase();
const normalizeSystem=(value:{mode:string|number;allowsNewEntries?:boolean}):SystemStatus=>({mode:normalizeMode(value.mode),allowsNewEntries:value.allowsNewEntries??normalizeMode(value.mode)==='RUNNING'});
export class ApiError extends Error { constructor(public status: number, message: string) { super(message); this.name = 'ApiError'; } }
export class ScrapperTradeApi {
  constructor(private readonly baseUrl = '', private readonly fetcher: typeof fetch = (input,init)=>fetch(input,init)) {}
  private async request<T>(path: string, init?: RequestInit): Promise<T> {
    const controller = new AbortController(); const timer = setTimeout(() => controller.abort(), 8000);
    try {
      const response = await this.fetcher(`${this.baseUrl}${path}`, { ...init, signal: controller.signal, headers: { Accept: 'application/json', ...(init?.body && !(init.body instanceof FormData) ? {'Content-Type':'application/json'} : {}), ...init?.headers } });
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
  strategies = () => this.request<StrategySpec[]>('/api/strategies');
  strategy = (id:string) => this.request<StrategySpec>(`/api/strategies/${encodeURIComponent(id)}`);
  saveStrategy = (strategy:StrategySpec) => this.request<StrategySpec>(`/api/strategies/${encodeURIComponent(strategy.id)}`,{method:'PUT',body:JSON.stringify(strategy)});
  backtests = () => this.request<BacktestRun[]>('/api/backtests');
  backtest = (id:string) => this.request<BacktestRun>(`/api/backtests/${encodeURIComponent(id)}`);
  startBacktest = (strategyId:string) => this.request<BacktestRun>('/api/backtests',{method:'POST',body:JSON.stringify({strategyId})});
  autonomy = () => this.request<AutonomyStatus>('/api/autonomy/status');
  knowledgeSources = () => this.request<KnowledgeSource[]>('/api/knowledge/sources');
  searchKnowledge = (query:string) => this.request<KnowledgeHit[]>(`/api/knowledge/search?q=${encodeURIComponent(query)}`);
  uploadKnowledge = (file:File) => { const form=new FormData(); form.append('file',file); return this.request<KnowledgeSource>('/api/knowledge/sources',{method:'POST',body:form}); };
  researchCandidates = () => this.request<ResearchCandidate[]>('/api/research/candidates');
  approveCandidateForValidation = (id:string) => this.request<ResearchCandidate>(`/api/research/candidates/${encodeURIComponent(id)}/approve-validation`,{method:'POST'});
  audit = () => this.request<AuditRecord[]>('/api/audit');
  systemEvents = () => this.request<SystemEvent[]>('/api/system/events');
  diagnostics = () => this.request<DiagnosticStatus>('/api/health/diagnostics');
  recovery = () => this.request<RecoveryStatus>('/api/recovery/status');
  providers = () => this.request<ProviderSetting[]>('/api/settings/providers');
  updateProvider = (id:string,update:ProviderUpdate) => this.request<ProviderSetting>(`/api/settings/providers/${encodeURIComponent(id)}`,{method:'PUT',body:JSON.stringify(update)});
}
export const isExplicitSimulator = (search = window.location.search) => import.meta.env.VITE_DATA_MODE === 'simulator' || new URLSearchParams(search).get('mode') === 'simulator';
const canonical=(value:string)=>value.toUpperCase().replace(/[^A-Z0-9]/g,'').replace(/(MICRO|MINI|PRO|RAW)$/,'').replace(/[._-]?[A-Z]$/,'');
export const suggestBrokerSymbols=(logical:string,symbols:BrokerSymbol[])=>symbols.filter(x=>x.tradeAllowed).map(symbol=>({symbol,score:symbol.name.toUpperCase()===logical.toUpperCase()?100:canonical(symbol.name)===canonical(logical)?80:symbol.name.toUpperCase().includes(logical.toUpperCase())?60:0})).filter(x=>x.score>0).sort((a,b)=>b.score-a.score||a.symbol.name.localeCompare(b.symbol.name)).map(x=>x.symbol);
export const validateStrategySpec=(strategy:StrategySpec)=>{const errors:string[]=[];if(!strategy.name.trim())errors.push('Name is required.');if(!strategy.instruments.length)errors.push('At least one instrument is required.');if(!strategy.timeframes.length)errors.push('At least one timeframe is required.');if(!strategy.entry.all.length)errors.push('At least one deterministic entry rule is required.');if(!strategy.exit.stopLoss.trim())errors.push('A stop-loss rule is required.');if(!strategy.exit.takeProfit.trim())errors.push('A take-profit rule is required.');if(strategy.risk.maxRiskFraction<=0||strategy.risk.maxRiskFraction>0.02)errors.push('Strategy risk must be above 0 and no more than 2%.');if(!strategy.sourceIds.length)errors.push('At least one provenance source is required.');return errors;};
export const chartPath=(values:number[],width=600,height=160)=>{if(values.length<2)return '';const min=Math.min(...values),max=Math.max(...values),span=max-min||1;return values.map((v,i)=>`${i?'L':'M'}${(i/(values.length-1)*width).toFixed(1)},${(height-(v-min)/span*height).toFixed(1)}`).join(' ');};
export type OperationalPage='Health'|'Events'|'Audit'|'Recovery'|'Providers';
export const operationalPageFromSearch=(search:string):OperationalPage=>{const page=new URLSearchParams(search).get('page');return page==='Events'||page==='Audit'||page==='Recovery'||page==='Providers'?page:'Health';};
export const providerPayload=(enabled:boolean,model?:string):ProviderUpdate=>({enabled,...(model?.trim()?{model:model.trim()}: {})});
