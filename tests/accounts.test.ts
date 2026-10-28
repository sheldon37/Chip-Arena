import {login as legacyLogin} from '../src/relay/auth.js';
import {test} from 'node:test';import assert from 'node:assert/strict';import {pbkdf2Sync,randomBytes,scryptSync} from 'node:crypto';
import {accounts,passwordHash} from '../src/relay/accounts.js';import worker from '../src/relay/worker.js';import {LocalRoomDatabase} from '../src/relay/local.js';import {verifyPlayerToken,GAME_CLAIM_MS} from '../src/shared/account-auth.js';import {base64url} from '../src/shared/dev-auth.js';import {HostedRoom} from '../src/shared/host-room.js';import {initPhysics} from '../src/shared/simulation.js';import {cosmetics} from '../src/shared/cosmetics.js';
const now=Date.now(),password='test account password 482';let ip=0;
async function fixture(){const db=new LocalRoomDatabase(),keys=await crypto.subtle.generateKey({name:'ECDSA',namedCurve:'P-256'},true,['sign','verify']),publicKey=await crypto.subtle.exportKey('jwk',keys.publicKey),salt=randomBytes(16);const env={DB:db,DEV_SIGNING_KEY:JSON.stringify(await crypto.subtle.exportKey('jwk',keys.privateKey)),DEV_ACCOUNT_VERIFIER:JSON.stringify({username:'Sheldon',salt:salt.toString('base64'),hash:pbkdf2Sync(password,salt,100000,32,'sha256').toString('base64'),iterations:100000})};const request=(action:string,data?:any,token='',time=now,client='test-'+(++ip))=>accounts(new Request('https://arena.example/api/accounts/'+action,{method:data===undefined?'GET':'POST',headers:{'Content-Type':'application/json',Origin:'http://localhost:8788','CF-Connecting-IP':client,...(token?{Authorization:'Bearer '+token}:{})},...(data===undefined?{}:{body:JSON.stringify(data)})}),env,time);return {db,env,publicKey,request};}
test('salted account hashes match Node scrypt and never store plaintext',async()=>{const hash=await passwordHash(password),parts=hash.split('$'),hash2=await passwordHash(password);assert.notEqual(hash,hash2);assert.equal(parts[5],scryptSync(password,Buffer.from(parts[4],'base64url'),32,{N:32768,r:8,p:3,maxmem:40*1024*1024}).toString('base64url'));assert.ok(!hash.includes(password));});
test('register, persist a sanitized pilot, log in from another client, conflict safely, logout and expire',async()=>{const f=await fixture();try{const r=await f.request('register',{username:'Pilot_ONE',password,developer:true,unlocks:['weapon:leet']});assert.equal(r.status,200);const first:any=await r.json();assert.equal(first.account.username,'pilot_one');assert.equal(first.account.developer,false);assert.deepEqual(first.account.unlocks,[]);assert.ok(first.recoveryCode);const claim=await verifyPlayerToken(first.token,now,f.publicKey);assert.equal(claim.accountId,first.account.id);assert.equal(claim.developer,false);assert.equal((await verifyPlayerToken(first.token,now+GAME_CLAIM_MS+1,f.publicKey)).accountId,undefined);const forged=base64url(new TextEncoder().encode(JSON.stringify({...JSON.parse(Buffer.from(first.token.split('.')[0],'base64url').toString()),developer:true})))+'.'+first.token.split('.')[1];assert.equal((await verifyPlayerToken(forged,now,f.publicKey)).developer,false);
 const db=f.db.sql.prepare('SELECT * FROM player_accounts').get() as any;assert.notEqual(db.password,password);assert.notEqual(db.recovery,first.recoveryCode);assert.equal(f.db.sql.prepare('SELECT hash FROM player_sessions').get()!.hash===first.session,false);
 assert.equal((await f.request('register',{username:'PILOT_ONE',password})).status,409);assert.equal((await f.request('register',{username:'Sheldon',password})).status,409);
 const profile={name:'<PILOT>',class:'melee',color:'#123456',cosmetics:{lobbyPose:'chair',preferredSpec:'daggers',bodyColor:'#432123',hat:'cap',intro:'dev-ansatsuken',killEffect:'dev-bass-van',introText:'Bring the rain'}};
 const save:any=await (await f.request('profile',{revision:0,profile},first.session)).json();assert.equal(save.account.profile.name,'PILOT');assert.equal(save.account.profile.cosmetics.lobbyPose,'chair');assert.equal(save.account.profile.cosmetics.preferredSpec,'daggers');assert.equal(save.account.profile.cosmetics.intro,'neon-drop');assert.equal(save.account.profile.cosmetics.killEffect,'impact-ragdoll');assert.equal(save.account.profile.cosmetics.introText,'Bring the rain');assert.equal((await f.request('profile',{revision:0,profile},first.session)).status,409);
 const second:any=await (await f.request('login',{username:'pilot_one',password})).json();assert.deepEqual(second.account.profile,save.account.profile);assert.equal((await f.request('login',{username:'pilot_one',password:'wrong'})).status,401);
 assert.equal((await f.request('me',undefined,second.session,now+12*3600000+1)).status,401);await f.request('logout',{},first.session);assert.equal((await f.request('me',undefined,first.session)).status,401);assert.equal((await f.request('me',undefined,second.session)).status,200);
 }finally{f.db.close();}});
test('DEV migration reserves the existing username; grants are audited and signed without elevating players',async()=>{await initPhysics();const f=await fixture();try{const user:any=await(await f.request('register',{username:'friend',password})).json(),dev:any=await(await f.request('login',{username:'Sheldon',password})).json();assert.equal(dev.account.developer,true);assert.ok(dev.recoveryCode);assert.equal((await legacyLogin(new Request('https://arena.example/api/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({username:'Sheldon',password})}),f.env,now)).status,409);assert.equal((await f.request('players',{query:'fr'},user.session)).status,403);const update={accountId:user.account.id,grantVersion:0,unlocks:['weapon:leet','weapon:ansatsuken','intro:dev-ansatsuken'],flags:['playtester']};assert.equal((await f.request('grant',{...update,developer:true},dev.session)).status,400);assert.equal((await f.request('grant',update,user.session)).status,403);const granted:any=await(await f.request('grant',update,dev.session)).json();assert.equal(granted.account.developer,false);assert.equal(granted.account.flags[0],'playtester');assert.equal(f.db.sql.prepare('SELECT count(*) AS n FROM account_audit').get()!.n,1);assert.equal((await f.request('grant',update,dev.session)).status,409);
 const refreshed:any=await(await f.request('me',undefined,user.session)).json(),access=await verifyPlayerToken(refreshed.token,now,f.publicKey);assert.equal(access.developer,false);assert.ok(access.unlocks.includes('weapon:leet'));assert.equal(cosmetics({intro:'dev-ansatsuken'},access).intro,'dev-ansatsuken');
 const room=new HostedRoom('ABCDEFGH',{name:'Host',class:'magic',color:'#ff0000'},true,()=>{},6,access,true);room.join({name:'Forged',class:'melee',developer:true,unlocks:['weapon:ansatsuken']});room.sim.start();assert.ok(room.sim.selectSpec('host','leet'));assert.equal(room.sim.selectSpec('guest','ansatsuken'),false);assert.ok(room.sim.leet.active(room.sim.players.get('host')));room.dispose();
 const rev=await f.request('grant',{...update,grantVersion:1,unlocks:[],flags:[]},dev.session);assert.equal(rev.status,200);const revoked:any=await(await f.request('me',undefined,user.session)).json();assert.deepEqual((await verifyPlayerToken(revoked.token,now,f.publicKey)).unlocks,[]);
 const search:any=await(await f.request('players',{query:'fr'},dev.session)).json();assert.equal(search.players.length,1);assert.ok(!JSON.stringify(search).includes('scrypt$'));assert.ok(!JSON.stringify(search).includes(user.session));
 }finally{f.db.close();}});
test('recovery rotates the one-use code, revokes all sessions and requires the new password',async()=>{const f=await fixture();try{const a:any=await(await f.request('register',{username:'recover_me',password})).json();const newPassword='a different long password';assert.equal((await f.request('recover',{username:'recover_me',password:newPassword,recoveryCode:'invalid'})).status,401);const recovered:any=await(await f.request('recover',{username:'recover_me',password:newPassword,recoveryCode:a.recoveryCode})).json();assert.ok(recovered.session);assert.notEqual(recovered.recoveryCode,a.recoveryCode);assert.equal((await f.request('me',undefined,a.session)).status,401);assert.equal((await f.request('recover',{username:'recover_me',password:newPassword,recoveryCode:a.recoveryCode})).status,401);assert.equal((await f.request('login',{username:'recover_me',password})).status,401);assert.equal((await f.request('login',{username:'recover_me',password:newPassword})).status,200);}finally{f.db.close();}});
test('public API handles CORS without cookies, rejects oversized bodies, rate limits auth and preserves room schema',async()=>{const f=await fixture();try{const pre=await worker.fetch(new Request('https://arena.example/api/accounts/login',{method:'OPTIONS',headers:{Origin:'https://some-host.trycloudflare.com'}}),f.env);assert.equal(pre.status,204);assert.equal(pre.headers.get('Access-Control-Allow-Origin'),'*');assert.equal(pre.headers.get('Access-Control-Allow-Credentials'),null);assert.equal((await f.request('register',{username:'abc',password:'x'.repeat(9000)})).status,413);let last:Response|undefined;for(let i=0;i<13;i++)last=await f.request('login',{username:'!',password:''},'',now,'same-ip');assert.equal(last!.status,429);assert.equal((await f.request('grant',{accountId:'x',developer:true})).status,401);const tables=f.db.sql.prepare("SELECT name FROM sqlite_master WHERE type='table'").all().map(x=>x.name);assert.ok(tables.includes('party_rooms')&&tables.includes('online_rooms')&&tables.includes('player_accounts'));}finally{f.db.close();}});
test('Remember me issues a 30-day revocable session; normal sessions still expire after twelve hours',async()=>{const f=await fixture();try{const data:any=await(await f.request('register',{username:'remember_me',password,remember:true})).json();assert.equal(data.expires,now+30*24*3600000);assert.equal((await f.request('me',undefined,data.session,now+29*24*3600000)).status,200);assert.equal((await f.request('me',undefined,data.session,data.expires+1)).status,401);assert.equal((await f.request('logout',{},data.session)).status,200);assert.equal((await f.request('me',undefined,data.session)).status,401);}finally{f.db.close();}});

test('short passwords register, authenticate and recover; empty and oversized passwords remain rejected',async()=>{
 const f=await fixture();try{
  for(const password of ['', 'x'.repeat(129)])assert.equal((await f.request('register',{username:'short_pass',password})).status,400);
  const registered=await f.request('register',{username:'short_pass',password:'a'});assert.equal(registered.status,200);const saved:any=await registered.json();
  assert.equal((await f.request('login',{username:'short_pass',password:'a'})).status,200);
  assert.equal((await f.request('recover',{username:'short_pass',password:'',recoveryCode:saved.recoveryCode})).status,400);
  assert.equal((await f.request('recover',{username:'short_pass',password:'b',recoveryCode:saved.recoveryCode})).status,200);
  assert.equal((await f.request('login',{username:'short_pass',password:'a'})).status,401);
  assert.equal((await f.request('login',{username:'short_pass',password:'b'})).status,200);
 }finally{f.db.close();}
});


test('native account cosmetics survive save, reload and a second login without granting DEV access',async()=>{
 const f=await fixture();try{
  const first:any=await(await f.request('register',{username:'nativepilot',password})).json();assert.equal(first.account.profileSchema,2);
  let revision=0;
  for(const intro of ['orbital-drop','grid-rez','holo-projection','blackout','skyline-zip','hero-landing','overclock']){
   const response=await f.request('profile',{revision,profile:{name:'Native',class:'melee',color:'#123456',cosmetics:{intro,overrideWeaponIntro:true,lobbyBack:'hex-field',preferredSpec:'sword',hat:'auditor-halo'}}},first.session);
   assert.equal(response.status,200);const saved:any=await response.json();revision=saved.account.revision;
   const reread:any=await(await f.request('me',undefined,first.session)).json();
   assert.equal(reread.account.profile.cosmetics.intro,intro);assert.equal(reread.account.profile.cosmetics.overrideWeaponIntro,true);assert.equal(reread.account.profile.cosmetics.lobbyBack,'hex-field');assert.equal(reread.account.profile.cosmetics.hat,'none');
  }
  const second:any=await(await f.request('login',{username:'nativepilot',password})).json();assert.equal(second.account.profile.cosmetics.intro,'overclock');assert.equal(second.account.profile.cosmetics.lobbyBack,'hex-field');
  // Unity weapon skins: a normalized per-weapon map (strict allowlist, Standard when absent or invalid), kept across reloads.
  const skinned:any=await(await f.request('profile',{revision,profile:{name:'Native',class:'melee',color:'#123456',cosmetics:{intro:'overclock',lobbyBack:'hex-field',weaponSkins:' sword : prototype ,pistol:prototype,rifle:standard,nope:prototype,shield:gold,<script>'}}},first.session)).json();
  revision=skinned.account.revision;assert.equal(skinned.account.profile.cosmetics.weaponSkins,'pistol:prototype,sword:prototype');
  const skinRead:any=await(await f.request('me',undefined,first.session)).json();assert.equal(skinRead.account.profile.cosmetics.weaponSkins,'pistol:prototype,sword:prototype');assert.equal(skinRead.account.profile.cosmetics.lobbyBack,'hex-field');
  const skinLogin:any=await(await f.request('login',{username:'nativepilot',password})).json();assert.equal(skinLogin.account.profile.cosmetics.weaponSkins,'pistol:prototype,sword:prototype');
  // The pilot body rides the same map after the weapons (Unity Core/NativeBodySkins.cs): a non-DEV account cannot keep the Sheldon Clothes.
  const bodied:any=await(await f.request('profile',{revision,profile:{cosmetics:{weaponSkins:'body:prototype,pistol:prototype,body:gold,pistol:sheldon'}}},first.session)).json();revision=bodied.account.revision;assert.equal(bodied.account.profile.cosmetics.weaponSkins,'pistol:prototype,body:prototype');
  const hooded:any=await(await f.request('profile',{revision,profile:{cosmetics:{weaponSkins:'sword:prototype,body:sheldon'}}},first.session)).json();revision=hooded.account.revision;assert.equal(hooded.account.profile.cosmetics.weaponSkins,'sword:prototype');
  const longest=['pistol','rifle','machinegun','fire','water','lightning','darkness','sword','daggers','shield','greatsword','emperor','ansatsuken','leet'].map(s=>s+':prototype').join(',');
  const all:any=await(await f.request('profile',{revision,profile:{cosmetics:{weaponSkins:longest.split(',').reverse().join(',')+',sword:prototype,siren:prototype'}}},first.session)).json();revision=all.account.revision;assert.equal(all.account.profile.cosmetics.weaponSkins,longest);
  const bow:any=await(await f.request('profile',{revision,profile:{cosmetics:{preferredSpec:'bow',weaponSkins:'bow:prototype'}}},first.session)).json();revision=bow.account.revision;assert.equal(bow.account.profile.cosmetics.preferredSpec,'bow');assert.equal((await(await f.request('me',undefined,first.session)).json() as any).account.profile.cosmetics.weaponSkins,'bow:prototype');
  for(const junk of [undefined,null,42,{sword:'prototype'},'',',,:,','sword','sword:standard','x'.repeat(5000)+',sword:prototype']){
   const r:any=await(await f.request('profile',{revision,profile:{cosmetics:{weaponSkins:junk}}},first.session)).json();revision=r.account.revision;assert.equal(r.account.profile.cosmetics.weaponSkins,'',String(junk).slice(0,40));
  }
  const bad:any=await(await f.request('profile',{revision,profile:{cosmetics:{intro:'dev-siren',lobbyBack:'<script>',overrideWeaponIntro:true}}},first.session)).json();assert.equal(bad.account.profile.cosmetics.intro,'neon-drop');assert.equal(bad.account.profile.cosmetics.lobbyBack,'tick-ring');assert.equal(bad.account.developer,false);
 }finally{f.db.close();}
});


test('native tints persist and Sheldon cosmetics require a DEV account',async()=>{
 const f=await fixture();try{
  const dev:any=await(await f.request('login',{username:'Sheldon',password})).json();
  const user:any=await(await f.request('register',{username:'colours',password})).json();
  for(const [pilot,allowed] of [[dev,true],[user,false]] as const){
   const c={preferredSpec:'cyberlord',hatColor:'#aBc123',backColor:'#123456',hat:'sheldon-head',intro:'sheldon-intro',overrideWeaponIntro:true};
   const save=await f.request('profile',{revision:0,profile:{cosmetics:c}},pilot.session);assert.equal(save.status,200);
   const read:any=await(await f.request('me',undefined,pilot.session)).json();const got=read.account.profile.cosmetics;
   assert.equal(got.preferredSpec,allowed?'cyberlord':undefined);assert.equal(got.hatColor,c.hatColor);assert.equal(got.backColor,c.backColor);assert.equal(got.hat,allowed?'sheldon-head':'none');assert.equal(got.intro,allowed?'sheldon-intro':'neon-drop');
   const bad:any=await(await f.request('profile',{revision:1,profile:{cosmetics:{hatColor:'',backColor:'<script>',hat:'sheldon-head',intro:'sheldon-intro'}}},pilot.session)).json();
   assert.equal(bad.account.profile.cosmetics.hatColor,'');assert.equal(bad.account.profile.cosmetics.backColor,'');
   // The Sheldon Clothes body skin: kept on the DEV account, Standard ('' for the body) on any other; weapon picks stay.
   const clothes:any=await(await f.request('profile',{revision:2,profile:{cosmetics:{weaponSkins:'body:sheldon,sword:prototype'}}},pilot.session)).json();
   assert.equal(clothes.account.profile.cosmetics.weaponSkins,allowed?'sword:prototype,body:sheldon':'sword:prototype');
   const reload:any=await(await f.request('me',undefined,pilot.session)).json();assert.equal(reload.account.profile.cosmetics.weaponSkins,allowed?'sword:prototype,body:sheldon':'sword:prototype');
   const cyber:any=await(await f.request('profile',{revision:3,profile:{cosmetics:{intro:'dev-cyberlord',overrideWeaponIntro:true}}},pilot.session)).json();
   assert.equal(cyber.account.profile.cosmetics.intro,allowed?'dev-cyberlord':'neon-drop');
   const cyberRead:any=await(await f.request('me',undefined,pilot.session)).json();assert.equal(cyberRead.account.profile.cosmetics.intro,allowed?'dev-cyberlord':'neon-drop');
  }
 }finally{f.db.close();}
});

test('Sheldon Idle persists only for live DEV accounts and stays outside the Electron pose catalog',async()=>{
 const {LOBBY_POSES,lobbyPose}=await import('../src/shared/lobby-presentation.js');
 assert.equal(Object.hasOwn(LOBBY_POSES,'sheldon-idle'),false);
 assert.equal(lobbyPose('sheldon-idle'),'weapon');
 const f=await fixture();try{
  const dev:any=await(await f.request('login',{username:'Sheldon',password})).json();
  const user:any=await(await f.request('register',{username:'idle_player',password})).json();
  for(const [pilot,allowed] of [[dev,true],[user,false]] as const){
   const saved:any=await(await f.request('profile',{revision:0,profile:{developer:true,cosmetics:{lobbyPose:'sheldon-idle'}}},pilot.session)).json();
   assert.equal(saved.account.profile.cosmetics.lobbyPose,allowed?'sheldon-idle':'weapon');
   const read:any=await(await f.request('me',undefined,pilot.session)).json();
   assert.equal(read.account.profile.cosmetics.lobbyPose,allowed?'sheldon-idle':'weapon');
   const login:any=await(await f.request('login',{username:allowed?'Sheldon':'idle_player',password})).json();
   assert.equal(login.account.profile.cosmetics.lobbyPose,allowed?'sheldon-idle':'weapon');
  }
  // Stored values and old sessions cannot retain DEV cosmetics after access is revoked.
  f.db.sql.prepare('UPDATE player_accounts SET developer=0,unlocks=? WHERE id=?').run(JSON.stringify(['pose:sheldon-idle']),dev.account.id);
  const revoked:any=await(await f.request('me',undefined,dev.session)).json();
  assert.equal(revoked.account.developer,false);assert.equal(revoked.account.profile.cosmetics.lobbyPose,'weapon');
  const forged:any=await(await f.request('profile',{revision:1,profile:{developer:true,cosmetics:{lobbyPose:'sheldon-idle'}}},dev.session)).json();
  assert.equal(forged.account.profile.cosmetics.lobbyPose,'weapon');
  let revision=2;
  for(const pose of [...Object.keys(LOBBY_POSES),'unknown','__proto__',null,42]){
   const r:any=await(await f.request('profile',{revision:revision++,profile:{cosmetics:{lobbyPose:pose}}},dev.session)).json();
   assert.equal(r.account.profile.cosmetics.lobbyPose,lobbyPose(pose));
  }
 }finally{f.db.close();}
});


test('Cyber Lord skin survives save, reload and login in native order without unlocking the DEV class',async()=>{
 const f=await fixture();try{
  const dev:any=await(await f.request('login',{username:'Sheldon',password})).json();
  const user:any=await(await f.request('register',{username:'cyber_skin',password})).json();
  const expected='bow:prototype,cyberlord:prototype,body:prototype';
  for(const [pilot,allowed,username] of [[dev,true,'Sheldon'],[user,false,'cyber_skin']] as const){
   const save=await f.request('profile',{revision:0,profile:{developer:true,cosmetics:{preferredSpec:'cyberlord',weaponSkins:'body:prototype,cyberlord:prototype,bow:prototype'}}},pilot.session);
   assert.equal(save.status,200);const saved:any=await save.json();
   const read:any=await(await f.request('me',undefined,pilot.session)).json();
   const login:any=await(await f.request('login',{username,password})).json();
   for(const response of [saved,read,login]){
    assert.equal(response.account.profile.cosmetics.weaponSkins,expected);
    assert.equal(response.account.profile.cosmetics.preferredSpec,allowed?'cyberlord':undefined);
    assert.equal(response.account.developer,allowed);
   }
   const access=await verifyPlayerToken(login.token,now,f.publicKey);assert.equal(access.developer,allowed);
   // Standard is represented by omission; unsupported skins never replace it.
   const reset:any=await(await f.request('profile',{revision:1,profile:{cosmetics:{weaponSkins:'cyberlord:prototype,cyberlord:standard,cyberlord:sheldon,bow:prototype'}}},pilot.session)).json();
   assert.equal(reset.account.profile.cosmetics.weaponSkins,'bow:prototype');
  }
 }finally{f.db.close();}
});

test('outfits and face gear persist; DEV clothes cannot be forged or retained after revocation',async()=>{
 const f=await fixture();try{
  const user:any=await(await f.request('register',{username:'outfits',password})).json();
  const dev:any=await(await f.request('login',{username:'Sheldon',password})).json();
  for(const [pilot,allowed] of [[user,false],[dev,true]] as const){
   const look='feet:barefoot,upper:sheldon-robe,lower:sheldon-pants,hair:wild,hands:gloves,unknown:foo';
   const saved:any=await(await f.request('profile',{revision:0,profile:{cosmetics:{look,weaponSkins:'face:visor',developer:true}}},pilot.session)).json();
   const expected=allowed?'hair:wild,upper:sheldon-robe,lower:sheldon-pants,hands:gloves,feet:barefoot':'hair:wild,hands:gloves';
   assert.equal(saved.account.profile.cosmetics.look,expected);
   const reread:any=await(await f.request('me',undefined,pilot.session)).json();
   assert.equal(reread.account.profile.cosmetics.look,expected);assert.equal(reread.account.profile.cosmetics.weaponSkins,'face:visor');
  }
  let revision=1;
  for(const [look,expected] of [[' upper:hard-jacket ,feet:work-boots,hair:wild','hair:wild,upper:hard-jacket,feet:work-boots'],['hair:wild,hair:spiky',''],['upper:tee,upper:invalid','upper:tee'],['__proto__:x,constructor:x',''],[null,''],[42,''],['x'.repeat(513),''],['','']] as const){
   const r=await f.request('profile',{revision:revision++,profile:{cosmetics:{look,weaponSkins:'face:mask,face:mask-visor'}}},user.session);assert.equal(r.status,200);const v:any=await r.json();assert.equal(v.account.profile.cosmetics.look,expected);assert.equal(v.account.profile.cosmetics.weaponSkins,'');
  }
  // Re-read sanitizes stored selections with current account authority, not the original save's status.
  f.db.sql.prepare('UPDATE player_accounts SET developer=0 WHERE id=?').run(dev.account.id);
  const revoked:any=await(await f.request('me',undefined,dev.session)).json();assert.equal(revoked.account.profile.cosmetics.look,'hair:wild,hands:gloves');
 }finally{f.db.close();}
});
