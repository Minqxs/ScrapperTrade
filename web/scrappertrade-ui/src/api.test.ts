import { describe, expect, it, vi } from 'vitest';
import { ScrapperTradeApi } from './api';
describe('ScrapperTradeApi', () => {
  it('loads typed system state', async () => { const fetcher=vi.fn().mockResolvedValue(new Response(JSON.stringify({mode:'PAUSED',allowsNewEntries:false}),{status:200})); expect(await new ScrapperTradeApi('',fetcher).system()).toEqual({mode:'PAUSED',allowsNewEntries:false}); });
  it('normalizes the current host numeric enum contract', async () => { const fetcher=vi.fn().mockResolvedValue(new Response(JSON.stringify({mode:6}),{status:200})); expect(await new ScrapperTradeApi('',fetcher).system()).toEqual({mode:'EMERGENCY_LOCKED',allowsNewEntries:false}); });
  it('does not replace missing live capabilities with simulator data', async () => { const fetcher=vi.fn().mockResolvedValue(new Response('',{status:404})); await expect(new ScrapperTradeApi('',fetcher).mt5()).rejects.toEqual(expect.objectContaining({status:404})); });
  it('posts emergency stop through the host boundary', async () => { const fetcher=vi.fn().mockResolvedValue(new Response(JSON.stringify({mode:'EMERGENCY_LOCKED',allowsNewEntries:false}),{status:200})); await new ScrapperTradeApi('',fetcher).emergencyStop(); expect(fetcher).toHaveBeenCalledWith('/api/system/emergency-stop',expect.objectContaining({method:'POST'})); });
});
