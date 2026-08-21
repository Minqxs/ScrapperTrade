export type SystemMode = 'STOPPED' | 'STARTING' | 'RUNNING' | 'PAUSED' | 'MAINTENANCE' | 'DEGRADED' | 'EMERGENCY_LOCKED' | string;
export interface HealthStatus { status: string; mode: SystemMode; demoOnly: boolean }
export interface SystemStatus { mode: SystemMode; allowsNewEntries: boolean }
export interface Mt5Status { connected: boolean; accountMode: 'DEMO'|'REAL'|'CONTEST'|'UNKNOWN'; accountType?: 'HEDGING'|'NETTING'|'UNKNOWN'; emergencyLocked: boolean; heartbeatAt?: string; terminal?: string }
export interface SetupStatus { complete: boolean; steps: { id: string; label: string; complete: boolean; detail?: string }[] }
export interface InstrumentMapping { id: string; logicalSymbol: string; brokerSymbol?: string; enabled: boolean; valid: boolean; suggestedSymbol?: string }
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
      if (!response.ok) throw new ApiError(response.status, response.status === 404 ? 'Capability is not available in this host build.' : `Request failed (${response.status}).`);
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
  instruments = () => this.request<InstrumentMapping[]>('/api/instruments');
  pause = async () => normalizeSystem(await this.request<{mode:string|number}>('/api/system/pause',{method:'POST'}));
  start = async () => normalizeSystem(await this.request<{mode:string|number}>('/api/system/start',{method:'POST'}));
  emergencyStop = async () => normalizeSystem(await this.request<{mode:string|number}>('/api/system/emergency-stop',{method:'POST'}));
  closeAll = () => this.request<{accepted:boolean}>('/api/positions/close-all',{method:'POST'});
  saveMapping = (mapping:InstrumentMapping) => this.request<InstrumentMapping>(`/api/instruments/${encodeURIComponent(mapping.id)}`,{method:'PUT',body:JSON.stringify(mapping)});
}
export const isExplicitSimulator = (search = window.location.search) => import.meta.env.VITE_DATA_MODE === 'simulator' || new URLSearchParams(search).get('mode') === 'simulator';
