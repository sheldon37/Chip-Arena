/** Only verified server claims may populate this structure. Item grants never confer DEV. */
export const GRANTABLE_CONTENT={
 'weapon:siren':'Siren DEV boss','intro:dev-siren':'Siren protocol intro',
 'weapon:emperor':'Emperor Style + Mishima Chips','intro:dev-emperor':'Emperor storm intro','weapon:leet':'L33T H4x staff + L44T Chips','weapon:ansatsuken':'Ansatsuken + arcade Chips',
 'intro:dev-matrix':'Matrix throne intro','intro:dev-ansatsuken':'Rain warrior intro',
 'hat:auditor-halo':'Auditor Halo','effect:dev-singularity':'Singularity Override','effect:dev-raging-demon':'Raging Demon','effect:dev-clown':'Clown getaway','effect:dev-clown-love':'Clown Love','effect:dev-bass-van':'Bass Van'
} as const;
export const ACCOUNT_FLAGS={playtester:'Playtester',vip:'VIP'} as const;
export interface Access {developer:boolean;unlocks:string[];accountId?:string;flags?:string[];}
export type AccessInput=boolean|Access;
export const accessOf=(a:AccessInput|undefined):Access=>typeof a==='boolean'?{developer:a,unlocks:[]}:a||{developer:false,unlocks:[]};
export const canUse=(a:boolean|Partial<Access>|undefined,key:string)=>typeof a==='boolean'?a:!!a?.developer||!!a?.unlocks?.includes(key);;
export const cleanGrants=(x:unknown):string[]=>Array.isArray(x)?[...new Set(x.filter((k):k is string=>typeof k==='string'&&Object.hasOwn(GRANTABLE_CONTENT,k)))]:[];
export const cleanFlags=(x:unknown):string[]=>Array.isArray(x)?[...new Set(x.filter((k):k is string=>typeof k==='string'&&Object.hasOwn(ACCOUNT_FLAGS,k)))]:[];
