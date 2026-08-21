import { describe, expect, it } from 'vitest';
import { confirmationPhrase, destructivePhraseValid, emergencyPhraseValid, executionAllowed } from './safety';
describe('control-centre safety gates', () => {
  it.each(['REAL','CONTEST','UNKNOWN','DISCONNECTED'])('rejects %s execution', account => expect(executionAllowed(account,false,false)).toBe(false));
  it('requires demo, unlocked, and unpaused state', () => {
    expect(executionAllowed('DEMO',false,false)).toBe(true);
    expect(executionAllowed('DEMO',true,false)).toBe(false);
    expect(executionAllowed('DEMO',false,true)).toBe(false);
  });
  it('requires the exact emergency phrase', () => { expect(emergencyPhraseValid('emergency')).toBe(false); expect(emergencyPhraseValid('EMERGENCY')).toBe(true); });
  it('uses an action-specific close-all phrase', () => {
    expect(confirmationPhrase('close')).toBe('CLOSE ALL');
    expect(destructivePhraseValid('close','EMERGENCY')).toBe(false);
    expect(destructivePhraseValid('close','CLOSE ALL')).toBe(true);
    expect(destructivePhraseValid('close-position','CLOSE ALL')).toBe(false);
    expect(destructivePhraseValid('close-position','CLOSE POSITION')).toBe(true);
  });
});
