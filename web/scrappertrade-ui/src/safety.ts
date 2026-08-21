export const executionAllowed = (accountType: string, emergencyLocked: boolean, userPaused: boolean) =>
  accountType.toUpperCase() === 'DEMO' && !emergencyLocked && !userPaused;

export const emergencyPhraseValid = (phrase: string) => phrase === 'EMERGENCY';

export type DestructiveAction = 'close' | 'emergency';
export const confirmationPhrase = (action: DestructiveAction) => action === 'emergency' ? 'EMERGENCY' : 'CLOSE ALL';
export const destructivePhraseValid = (action: DestructiveAction, phrase: string) => phrase === confirmationPhrase(action);
