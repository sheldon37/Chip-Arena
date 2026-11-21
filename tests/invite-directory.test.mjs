// Run with Node 24: node --test tests/invite-directory.test.mjs
// Exercises the shipped social handler against SQLite without accounts or a live service.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {stripTypeScriptTypes} from 'node:module';
import {DatabaseSync} from 'node:sqlite';
const source=readFileSync(new URL('../src/relay/account-social.ts',import.meta.url),'utf8');
const {accountSocial}=await import('data:text/javascript;base64,'+Buffer.from(stripTypeScriptTypes(source)).toString('base64'));

test('Unity invitations use the registered UN directory key through send, inbox and acceptance',async()=>{
 const sql=new DatabaseSync(':memory:');
 try{
  sql.exec('PRAGMA foreign_keys=ON; CREATE TABLE player_accounts(id TEXT PRIMARY KEY,username TEXT); CREATE TABLE online_rooms(code TEXT PRIMARY KEY,expires INTEGER);');
  sql.exec(readFileSync(new URL('../drizzle/0006_player_invites.sql',import.meta.url),'utf8'));
  sql.exec("INSERT INTO player_accounts VALUES('host','host'),('guest','guest'),('other','other'); INSERT INTO online_rooms VALUES('UN7KMR4P',999999);");
  const db={
   prepare(query){
    return {bind(...args){
     const statement=sql.prepare(query);
     return {
      first:async()=>statement.get(...args),
      run:async()=>({meta:{changes:Number(statement.run(...args).changes)}}),
      execute:async()=>/^SELECT/i.test(query)?{results:statement.all(...args)}:{meta:{changes:Number(statement.run(...args).changes)}}
     };
    }};
   },
   async batch(statements){return Promise.all(statements.map(s=>s.execute()));}
  };
  await accountSocial(db,'host','social',{},1000);await accountSocial(db,'guest','social',{},1000);
  // The old client sends the visible code, which is not the registered directory row.
  await assert.rejects(accountSocial(db,'host','invite',{target:'guest',code:'7KMR4P'},1000),e=>e.status===409&&e.message==='The room is no longer available.');
  assert.deepEqual((await accountSocial(db,'guest','social',{},1000)).invites,[]);
  assert.deepEqual(await accountSocial(db,'host','invite',{target:'guest',code:'UN7KMR4P'},1000),{ok:true});
  const inbox=await accountSocial(db,'guest','social',{},1000);
  assert.equal(inbox.invites.length,1);assert.equal(inbox.invites[0].username,'host');
  assert.deepEqual((await accountSocial(db,'other','social',{},1000)).invites,[]);
  const accepted=await accountSocial(db,'guest','invite-answer',{id:inbox.invites[0].id,accept:true},1000);
  assert.deepEqual(accepted,{ok:true,code:'UN7KMR4P'});
  assert.equal(sql.prepare('SELECT code FROM online_rooms WHERE code=?').get(accepted.code).code,'UN7KMR4P');
  await assert.rejects(accountSocial(db,'guest','invite-answer',{id:inbox.invites[0].id,accept:true},1000),e=>e.status===409);
 }finally{sql.close();}
});
