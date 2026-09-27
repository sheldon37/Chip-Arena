export const HATS={none:'No hat',cap:'Ball cap',beanie:'Beanie',top:'Top hat',wizard:'Wizard hat',crown:'Crown',cone:'Traffic cone',antenna:'Antenna',halo:'Halo',cowboy:'Cowboy hat'} as const;
export const KILL_STYLES={'voxel-burst':'Voxel breakup',confetti:'Confetti cannon',ascension:'Beam me up',shrink:'Pocket-sized',launch:'Team Rocket',ghost:'Ghost escape','dev-clown':'Clown getaway · DEV ONLY','dev-clown-love':'Clown Love · DEV ONLY'} as const;
export const developerKillEffect=(id:string)=>id.startsWith('dev-');
export interface Cosmetics {bodyColor:string;hat:keyof typeof HATS;killEffect:keyof typeof KILL_STYLES;}
export const DEFAULT_COSMETICS:Cosmetics={bodyColor:'#e5e7eb',hat:'none',killEffect:'voxel-burst'};
export function cosmetics(raw:any,dev=false):Cosmetics{return {bodyColor:typeof raw?.bodyColor==='string'&&/^#[0-9a-f]{6}$/i.test(raw.bodyColor)?raw.bodyColor:DEFAULT_COSMETICS.bodyColor,hat:raw&&Object.hasOwn(HATS,raw.hat)?raw.hat:'none',killEffect:raw&&Object.hasOwn(KILL_STYLES,raw.killEffect)&&(dev||!developerKillEffect(raw.killEffect))?raw.killEffect:'voxel-burst'};}
