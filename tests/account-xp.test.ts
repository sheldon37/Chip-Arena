import {test} from 'node:test';
import assert from 'node:assert/strict';
import {LocalRoomDatabase} from '../src/relay/local.js';
import {confirmXp,readXp,xpCompletion,xpLevel,xpLines,XP_CAP} from '../src/relay/account-xp.js';
import {accounts} from '../src/relay/accounts.js';
const completion=(completionId='pvp_test')=>({version:1,completionId,mode:'pvp',won:true,bots:false,seconds:120,cleared:0});
function fixture(){const db=new LocalRoomDatabase();for(const id of ['one','two'])db.sql.prepare('INSERT INTO player_accounts(id,username,password,recovery,created,updated) VALUES(?,?,?,?,0,0)').run(id,id,'unused','unused');return db;}
test('reward rules, minimum participation, curve boundaries and cap',()=>{
 assert.equal(xpLevel(0),1);assert.equal(xpLevel(249),1);assert.equal(xpLevel(250),2);assert.equal(xpLevel(749),2);assert.equal(xpLevel(750),3);assert.equal(xpLevel(XP_CAP),100);
 for(let level=1;level<100;level++){const next=250*level*(level+1)/2;assert.equal(xpLevel(next-1),level);assert.equal(xpLevel(next),level+1);}
 const sum=(c:any)=>xpLines(xpCompletion(c)).reduce((n,l)=>n+l.xp,0);
 assert.equal(sum(completion()),150);assert.equal(sum({...completion(),bots:true}),75);assert.equal(sum({...completion(),mode:'pve'}),300);assert.equal(sum({...completion(),mode:'expedition',cleared:3}),900);
 for(const c of [{...completion(),seconds:59},{...completion(),version:0},{...completion(),cleared:4},{...completion(),won:1},{...completion(),mode:'sandbox'},{...completion(),completionId:'bad\n'},{...completion(),seconds:Infinity}])assert.throws(()=>xpCompletion(c));
});
test('duplicates and concurrent distinct completions credit once; conflict cannot change balance',async()=>{
 const db=fixture();try{
 const a=await confirmXp(db,'one',completion(),1);assert.equal(a.award.newTotal,150);
 const duplicates=await Promise.all(Array.from({length:8},()=>confirmXp(db,'one',completion(),2)));for(const d of duplicates)assert.deepEqual(d.award,a.award);
 await assert.rejects(confirmXp(db,'one',{...completion(),won:false},3),/different result/);assert.equal((await readXp(db,'one')).total,150);
 await Promise.all([confirmXp(db,'one',completion('pvp_second'),4),confirmXp(db,'one',completion('pvp_third'),4)]);
 assert.equal((await readXp(db,'one')).total,450);assert.equal((await readXp(db,'one')).revision,3);
 const b=await confirmXp(db,'two',completion(),5);assert.equal(b.award.previousTotal,0);assert.equal(b.summary.total,150);
 assert.equal(db.sql.prepare('SELECT revision FROM player_accounts WHERE id=?').get('one')!.revision,0);
 }finally{db.close();}
});
test('cap trims breakdown, zero awards are durable, and a failed transaction rolls back',async()=>{
 const db=fixture();try{
 db.sql.prepare('INSERT INTO account_xp(account,total,revision) VALUES(?,?,0)').run('one',XP_CAP-25);
 const a=await confirmXp(db,'one',completion(),1);assert.equal(a.award.newTotal,XP_CAP);assert.equal(a.award.lines.reduce((n,l)=>n+l.xp,0),25);assert.equal(a.award.bands.at(-1)!.next,XP_CAP);
 const b=await confirmXp(db,'one',completion('pvp_atcap'),2);assert.equal(b.award.newTotal-b.award.previousTotal,0);
 db.sql.exec("CREATE TRIGGER fail_xp BEFORE UPDATE ON account_xp BEGIN SELECT RAISE(ABORT,'test failure'); END;");
 await assert.rejects(confirmXp(db,'two',completion(),3));assert.equal(db.sql.prepare('SELECT count(*) n FROM account_xp_awards WHERE account=?').get('two')!.n,0);
 }finally{db.close();}
});
test('XP endpoints authenticate the owner, ignore forged totals, and survive lost response retry',async()=>{
 const db=fixture();try{
 const token='a'.repeat(43),hash=Buffer.from(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(token))).toString('base64url');
 db.sql.prepare('INSERT INTO player_sessions(hash,account_id,expires) VALUES(?,?,?)').run(hash,'one',100000);
 const env={DB:db,DEV_SIGNING_KEY:'unused'},request=(body?:any,auth=token)=>accounts(new Request('https://test/api/accounts/xp',{method:body?'POST':'GET',headers:{'Content-Type':'application/json',Authorization:'Bearer '+auth},...(body?{body:JSON.stringify(body)}:{})}),env,1000);
 assert.equal((await request(undefined,'bad')).status,401);
 assert.equal((await (await request()).json() as any).summary.total,0);
 const body={...completion(),account:'two',newTotal:1000000,xp:999999};assert.equal((await request(body)).status,200);
 const retry:any=await (await request(body)).json();assert.equal(retry.award.account,'one');assert.equal(retry.summary.total,150);assert.equal((await readXp(db,'two')).total,0);
 }finally{db.close();}
});
