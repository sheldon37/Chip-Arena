export const TEAMS={red:{name:'Red',color:'#ff6575'},blue:{name:'Blue',color:'#69a6ff'},green:{name:'Green',color:'#7ed78a'},yellow:{name:'Yellow',color:'#f5db70'},purple:{name:'Purple',color:'#ba94f5'},orange:{name:'Orange',color:'#ffa565'},cyan:{name:'Cyan',color:'#65dce0'},pink:{name:'Pink',color:'#f18dcc'}} as const;
export type TeamId=keyof typeof TEAMS;
export type GameMode='ffa'|'teams';
export const teamId=(value:unknown):TeamId|undefined=>typeof value==='string'&&Object.hasOwn(TEAMS,value)?value as TeamId:undefined;
export const gameMode=(value:unknown):GameMode=>value==='teams'?'teams':'ffa';
export function sameTeam(a:{id:string;team?:TeamId}|undefined,b:{id:string;team?:TeamId}|undefined,mode:unknown){return !!a&&!!b&&mode==='teams'&&!!a.team&&a.team===b.team;}
export function wonWith(p:{id:string;team?:TeamId}|undefined,winner:{id:string;team?:TeamId}|undefined,mode:unknown){return !!p&&!!winner&&(p.id===winner.id||sameTeam(p,winner,mode));}
