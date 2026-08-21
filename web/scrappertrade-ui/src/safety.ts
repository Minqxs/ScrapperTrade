export const executionAllowed = (accountType: string, emergencyLocked: boolean, userPaused: boolean) =>
  accountType.toUpperCase() === 'DEMO' && !emergencyLocked && !userPaused;

export const emergencyPhraseValid = (phrase: string) => phrase === 'EMERGENCY';
