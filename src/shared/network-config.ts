/** Prototype network tuning, independent of weapon and Block cooldowns. */
export const NETWORK={maxPendingInputs:180,maxInputBatch:60,inputQueueLimit:90,correctionDecay:18,hardSnapDistance:2.5,interpolationMinMs:75,interpolationMaxMs:350,extrapolationMaxMs:80} as const;
