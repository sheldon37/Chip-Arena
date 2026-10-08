export const RAGDOLL_STYLES={
 'ragdoll-ember':'Emberfall',
 'ragdoll-frost':'Frostbreak',
 'ragdoll-static':'Static Aftershock',
 'ragdoll-gravity':'Gravity Collapse',
 'ragdoll-pinball':'Pinball',
 'ragdoll-fireworks':'Firework Finale',
 'ragdoll-soul':'Soul Drift',
 'ragdoll-petals':'Petal Scatter',
 'dev-singularity':'Singularity Override · DEV ONLY',
} as const;
export type RagdollStyle='ragdoll'|keyof typeof RAGDOLL_STYLES;
