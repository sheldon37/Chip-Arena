import type {Database} from './worker.js';
const fail=(message:string,status=400)=>Object.assign(Error(message),{status});
export const XP_CAP=1237500;
export function xpLevel(total:number){let level=1;while(level<100&&total>=250*level*(level+1)/2)level++;return level;}
export function xpSummary(account:string,total=0,revision=0){const level=xpLevel(total),levelStart=250*(level-1)*level/2;return {version:1,account,known:true,total,level,levelStart,nextLevelAt:level===100?levelStart:levelStart+250*level,pending:0,revision};}
export function xpCompletion(input:any){
 if(input?.version!==1||typeof input.completionId!=='string'||!/^[-a-zA-Z0-9_:]{1,128}$/.test(input.completionId)||!['pvp','pve','expedition'].includes(input.mode)||typeof input.won!=='boolean'||typeof input.bots!=='boolean'||!Number.isInteger(input.seconds)||input.seconds<60||input.seconds>604800||!Number.isInteger(input.cleared)||input.cleared<0||input.cleared>3||input.mode!=='expedition'&&input.cleared!==0)throw fail('Invalid or ineligible completion.');
 return {version:1,completionId:input.completionId,mode:input.mode,won:input.won,bots:input.bots,seconds:input.seconds,cleared:input.cleared};
}
export function xpLines(c:ReturnType<typeof xpCompletion>){
 const half=(c.mode==='pvp'&&c.bots)?0.5:1;
 const lines=[{key:'completion',label:'Completion',xp:(c.mode==='pvp'?100:c.mode==='pve'?150:200)*half}];
 if(c.cleared)lines.push({key:'objectives',label:'Instances cleared',xp:c.cleared*100});
 if(c.won)lines.push({key:'victory',label:'Victory',xp:(c.mode==='pvp'?50:c.mode==='pve'?150:400)*half});
 return lines;
}
export async function readXp(db:Database,account:string){const row=await db.prepare('SELECT total,revision FROM account_xp WHERE account=?').bind(account).first();return xpSummary(account,row?.total??0,row?.revision??0);}
export async function confirmXp(db:Database,account:string,input:any,now:number){
 const c=xpCompletion(input),payload=JSON.stringify(c),lines=xpLines(c),gain=lines.reduce((n,l)=>n+l.xp,0),attempt=crypto.randomUUID();
 // D1 batch is transactional. The random insertion marker makes the increment conditional on THIS insert,
 // even when the same completion is retried concurrently or its successful HTTP response was lost.
 const result=await db.batch([
  db.prepare('INSERT INTO account_xp(account,total,revision) VALUES(?,0,0) ON CONFLICT(account) DO NOTHING').bind(account),
  db.prepare('INSERT INTO account_xp_awards(account,completion,payload,previous_total,new_total,marker,created) SELECT ?,?,?,total,MIN(?,total+?),?,? FROM account_xp WHERE account=? ON CONFLICT(account,completion) DO NOTHING').bind(account,c.completionId,payload,XP_CAP,gain,attempt,now,account),
  db.prepare('UPDATE account_xp SET total=(SELECT new_total FROM account_xp_awards WHERE account=? AND completion=?),revision=revision+1 WHERE account=? AND EXISTS(SELECT 1 FROM account_xp_awards WHERE account=? AND completion=? AND marker=?)').bind(account,c.completionId,account,account,c.completionId,attempt),
  db.prepare('SELECT * FROM account_xp_awards WHERE account=? AND completion=?').bind(account,c.completionId),
  db.prepare('SELECT total,revision FROM account_xp WHERE account=?').bind(account)
 ]);
 const row=result[3].results[0],balance=result[4].results[0];
 if(row.payload!==payload)throw fail('Completion ID already belongs to a different result.',409);
 const previousTotal=row.previous_total,newTotal=row.new_total,previousLevel=xpLevel(previousTotal),newLevel=xpLevel(newTotal),bands=[];
 let remaining=newTotal-previousTotal;for(const line of lines){line.xp=Math.min(line.xp,remaining);remaining-=line.xp;}
 for(let level=previousLevel;level<=newLevel;level++){const start=250*(level-1)*level/2;bands.push({level,start,next:level===100?start:start+250*level});}
 return {award:{version:1,completionId:c.completionId,account,mode:c.mode,status:'confirmed',revision:2,attempt:1,previousTotal,newTotal,previousLevel,newLevel,bands,lines},summary:xpSummary(account,balance.total,balance.revision)};
}
