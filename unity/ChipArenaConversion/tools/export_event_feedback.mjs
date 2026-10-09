// Event feedback parity fixtures (presentation only). Runs the pinned original modules themselves, never the native port:
// client/event-vfx.ts EventVfx.event (the camera-shake flag), client/combat-numbers.ts CombatNumberTrack and numberLabel,
// and client/audio.ts SoundLimiter and ArenaAudio.update (its event branch, with play/cue/arcadeTone recorded and the
// music/boss/siren/slender/intro players stubbed). Each scenario also states the Unity events or routing context that
// stand for those source events, so Tests/EventFeedbackChecks.cs can run the Core rules on them and compare.
// Usage: node tools/export_event_feedback.mjs <pinned source> <temporary output>  (link a node_modules containing three
// r180 into the temporary output). Writes Tests/event-feedback-parity.json.
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {stripTypeScriptTypes} from 'node:module';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]),temp=path.resolve(process.argv[3]);
const inventory=JSON.parse(fs.readFileSync(path.join(root,'tools/conversion-inventory.json')));
fs.mkdirSync(temp,{recursive:true});fs.writeFileSync(path.join(temp,'package.json'),'{"type":"module"}');
for(const f of inventory.files.filter(f=>f.path.startsWith('src/')&&f.path.endsWith('.ts'))){const input=path.join(source,f.path);if(!fs.existsSync(input))continue;const b=fs.readFileSync(input);if(createHash('sha1').update(`blob ${b.length}\0`).update(b).digest('hex')!==f.gitBlob)throw Error('Source mismatch '+f.path);const out=path.join(temp,f.path.replace(/\.ts$/,'.js'));fs.mkdirSync(path.dirname(out),{recursive:true});let code=b.toString().replace(/import\s*\{([^}]+)\}\s*from\s*(['"])([^'"]+)\2/g,(whole,names,quote,module)=>{if(!module.startsWith('.'))return whole;let target=path.resolve(path.dirname(input),module.replace(/\.js$/,'.ts'));if(!fs.existsSync(target))return whole;let text=fs.readFileSync(target,'utf8'),kept=names.split(',').filter(n=>!new RegExp('(?:export\\s+)?(?:interface|type)\\s+'+n.trim().split(/\s+as\s+/)[0]+'\\b').test(text));return kept.length?'import {'+kept.join(',')+'} from '+quote+module+quote:'import '+quote+module+quote;});fs.writeFileSync(out,stripTypeScriptTypes(code,{mode:'transform'}));}
const load=async p=>import(pathToFileURL(path.join(temp,'src',p+'.js')));
const T=await import(pathToFileURL(path.join(temp,'node_modules/three/build/three.module.js')));
const {EventVfx}=await load('client/event-vfx'),{CombatNumberTrack,numberLabel}=await load('client/combat-numbers'),{ArenaAudio,SoundLimiter,HIT_TRIM}=await load('client/audio');
const P=(x,y,z)=>({x,y,z});

// ---- numberLabel ----
const amounts=[.001,.0049,.005,.0151,.025,.05,.0500001,.07,.095,.0995,.1,.15,.25,.35,.45,.55,.65,.95,.96,.994,.995,.999,1,1.4,1.5,2.5,3.5,12.49,12.5,99.5,250,1234.5];
const labels=amounts.flatMap(amount=>[false,true].map(heal=>({amount,heal,label:numberLabel({amount,heal})})));

// ---- SoundLimiter (ArenaAudio.cue: gate(key, now, gain*HIT_TRIM, gain)) ----
const limiter=[];{const l=new SoundLimiter();
 const plays=[['hit',0,.9],['hit',.01,.9],['hit',.03,.9],['hit',.061,.9],['hit',.2,.9],['hit',.21,.9],['hit',.22,.9],['hit',.5,.9],['hit',1.5,.9],
  ['add',0,.55],['add',.05,.55],['add',.08,.55],['add',.09,.55],['proc',0,.32],['proc',.1,.32],['tick',0,.24],['tick',.05,.24],['tick',.099,.24],['tick',.1,.24],['tick',.3,.24],
  ['hurt',0,.6],['hurt',.07,.6],['hurt',.081,.6],['impact',0,.22],['impact',.02,.22],['impact',.04,.22],['impact',.06,.22],['impact',.08,.22],['explosion',0,.65],['explosion',.06,.65],['explosion',.071,.65],
  ['arc',0,.5],['arc',.05,.5],['arc',.091,.5],['blast-stone',0,.5],['blast-stone',.05,.5],['blast-stone',.061,.5],['hit',3,.9],['hit',3.02,.9],['hit',3.04,.9],['hit',3.06,.9],['hit',3.08,.9],['hit',3.1,.9]];
 for(const [key,now,gain] of plays)limiter.push({key,now,gain,out:l.gate(key,now,gain*HIT_TRIM,gain)});}

// ---- Camera shake flag (event-vfx.ts:29) for each source event type, and the Unity kind that carries it ----
const vfx=new EventVfx(new T.Scene(),()=>({quality:1,compat:false,trails:false})),shakeState={time:5,players:[{id:'me',pos:P(0,1,0),spec:'pistol'}],events:[]};
const shakeCases=[['hit','direct','Hit'],['hit','secondary','Headshot'],['hit','burn','BurnTick'],['hit','poison','PoisonTick'],['hit','stage-saw','Hit'],['impact',undefined,'Impact'],['break',undefined,'Break'],['blockHit','projectile','Block'],['collapse',undefined,'Collapse'],
 ['heal',undefined,'Heal'],['critical',undefined,'Critical'],['explosion','shrapnel','Explosion'],['blockPulse',undefined,'BlockPulse'],['vacuum',undefined,'Vacuum'],['arc',undefined,'Arc'],['freeze',undefined,'Freeze'],['teleport',undefined,'Teleport'],
 ['catch',undefined,'Catch'],['split',undefined,'Split'],['ricochet',undefined,'Ricochet'],['ceiling-hit',undefined,'CeilingHit'],['stage-saw-hit','SAW','HazardHit'],['summon',undefined,'Summon'],['install','kinetic','Install'],['field','repulsion','Field'],
 ['pve-gib',undefined,'PveGib'],['pve-revive',undefined,'PveRevive'],['pve-buy','chip','PveBuy'],['pve-cash','kill','PveMoney'],['pve-phase',undefined,'PvePhase'],['death',undefined,'Death'],['shot',undefined,'Shot'],['reloadStart',undefined,'ReloadStart']];
const shake=[];for(const [type,message,kind] of shakeCases)for(const [target,unityTarget] of [['me',1],['foe',2],[undefined,0]])
 shake.push({type,message:message??null,target:target??null,kind,unityTarget,amount:5,shake:!!vfx.event({id:1,time:5,type,pos:P(0,1,0),target,message,source:'foe',amount:5},shakeState,'me').shake});

// ---- CombatNumberTrack ----
// Source ids map to Unity seat ids (me 1, foe 2, ally 3, boss 900) and add ids (add-1 -> 10001). Hits keep the source's
// 'hit' message: burn/poison ticks become BurnTick/PoisonTick, a headshot flag becomes Headshot, minionHit becomes Hit on
// the add, heal stays Heal.
const ids={me:1,foe:2,ally:3,boss:900,'add-1':10001,'add-2':10002,p4:4,p5:5,p6:6,p7:7,p8:8};
const players=[{id:'me',pos:P(0,1,0),hp:100},{id:'foe',pos:P(4,1.2,-3),hp:100},{id:'ally',pos:P(-2,1,5),hp:100},{id:'boss',pos:P(8,2.5,8),hp:100},...[4,5,6,7,8].map(n=>({id:'p'+n,pos:P(n*1.5-9,1,n-11),hp:100}))];
let serial=0;const ev=(type,time,source,target,amount,extra={})=>({id:++serial,time,type,pos:extra.pos??P(1,.5,2),source,target,amount,...extra});
const unityEvent=e=>({id:e.id,time:e.time,kind:e.type==='heal'?'Heal':e.type==='minionHit'?'Hit':e.message==='burn'?'BurnTick':e.message==='poison'?'PoisonTick':e.headshot?'Headshot':'Hit',source:ids[e.source]??0,target:ids[e.target]??0,amount:e.amount,pos:e.pos});
const numbers=[];
function numberScenario(name,me,pve,frames){
 const track=new CombatNumberTrack(),out=[];
 for(const f of frames){const state={time:f.time,generation:f.generation??0,events:f.events,players,pve:pve?{}:undefined};const list=track.update(state,me);
  out.push({time:f.time,generation:f.generation??0,events:f.events.map(unityEvent),numbers:list.map(n=>({id:n.id,target:ids[n.target],heal:n.heal,amount:n.amount,at:n.at,lane:n.lane,pos:n.pos,label:numberLabel(n)}))});}
 numbers.push({name,local:ids[me],pve,players:players.map(p=>({id:ids[p.id],pos:p.pos})),frames:out});
}
{serial=0;const a=[ev('hit',1,'me','foe',12,{message:'direct'}),ev('hit',1.05,'me','foe',3.5,{message:'direct',headshot:true}),ev('hit',1.1,'ally','foe',2.25,{message:'secondary'}),ev('heal',1.12,'foe','foe',4),ev('hit',1.2,'me','foe',7,{message:'direct'})];
 const b=[ev('hit',1.25,'foe','me',.5,{message:'burn'}),ev('hit',1.5,'foe','me',.5,{message:'burn'}),ev('hit',1.5,'foe','me',.25,{message:'poison'}),ev('heal',1.6,'me','me',.02),ev('heal',1.65,'me','me',.03),ev('hit',1.75,'foe','me',.5,{message:'burn'})];
 const c=[ev('hit',2,'foe','boss',.06,{message:'chain'}),ev('hit',2.1,'foe','ally',30,{message:'direct'}),ev('hit',.5,'foe','ally',9,{message:'direct'})];
 numberScenario('merge-lanes-heal-ticks','me',false,[{time:1,events:[a[0]]},{time:1.15,events:a.slice(0,3)},{time:1.3,events:[...a,...b.slice(0,1)]},{time:1.8,events:[...a,...b]},{time:2.1,events:[...b,...c]},{time:2.45,events:[...c]},{time:3.4,events:[]}]);}
{serial=100;const a=[ev('minionHit',1,'me','add-1',3,{pos:P(5,0,5)}),ev('minionHit',1.04,'me','add-1',.04,{pos:P(5,0,5)}),ev('minionHit',1.05,'ally','add-1',8,{pos:P(5,0,5)}),ev('minionHit',1.3,'me','add-2',.5,{pos:P(-3,0,1)}),ev('hit',1.3,'boss','me',20,{message:'siren'})];
 numberScenario('pve-add-hits','me',true,[{time:1.3,events:a},{time:2.2,events:a}]);
 serial=200;numberScenario('no-add-hits-outside-pve','me',false,[{time:1.3,events:[ev('minionHit',1,'me','add-1',3,{pos:P(5,0,5)}),ev('hit',1.2,'me','foe',2,{message:'direct'})]}]);}
{serial=300;const burst=[];for(let i=0;i<70;i++)burst.push(ev('hit',1+i*.0113,'me',['foe','ally','boss','me'][i%4],1+i,{message:'direct'}));for(let i=0;i<70;i++)burst.push(ev('heal',1+i*.00291,'me',['foe','ally','boss','me'][i%4],.1));
 // Times stay off the .16 s merge edge: Unity's float clock and the source's doubles disagree exactly on it.
 // Nine targets hit .1719 s apart each: every hit is its own number, so more than 64 are alive and the cap applies.
 const targets=['foe','ally','boss','me','p4','p5','p6','p7','p8'],spread=[];for(let i=0;i<120;i++)spread.push(ev('hit',1.3+i*.0191,'foe',targets[i%9],1+i%7,{message:'direct'}));
 numberScenario('cap-and-expiry','me',false,[{time:1.2,events:burst},{time:2.3,events:[...burst,...spread.slice(0,60)]},{time:3.6,events:spread},{time:3.7,events:spread},{time:5,events:spread}]);}
{serial=400;const first=[ev('hit',4,'me','foe',5,{message:'direct'}),ev('hit',4.05,'me','foe',5,{message:'direct'})];serial=0;const restart=[ev('hit',1,'foe','me',9,{message:'direct'})];
 numberScenario('generation-and-rewind','me',false,[{time:4.1,events:first},{time:4.2,generation:1,events:first},{time:1.05,generation:1,events:restart},{time:1.1,generation:1,events:restart}]);}

// ---- ArenaAudio event routing ----
const sounds=[];
function soundScenario(name,events,unity,{pve=false,mode='ffa',extraPlayers=[]}={}){
 const a=new ArenaAudio(),noop=new Proxy({},{get:(t,k)=>k==='cueVolume'||k==='playbackVolume'?1:()=>{}});a.music=a.boss=a.siren=a.slender=a.intros=noop;const calls=[];
 a.play=(n,pos,gain=1,rate=0)=>calls.push({fn:'play',name:n,key:null,gain,positional:!!pos,rate});
 a.cue=(key,n,pos,gain,rate=0)=>calls.push({fn:'cue',name:n,key,gain,positional:!!pos,rate});
 a.arcadeTone=(hit,move,superMove)=>calls.push({fn:'tone',name:'arcade-tone',key:null,gain:1,positional:false,rate:0,hit,move,superMove:!!superMove});a.clownLaugh=()=>calls.push({fn:'laugh'});
 const team=mode==='teams',ps=[{id:'me',pos:P(0,1,0),hp:100,spec:'pistol',team:team?'red':undefined},{id:'foe',pos:P(4,1,-3),hp:100,spec:'fire',team:team?'blue':undefined},{id:'ally',pos:P(-2,1,5),hp:100,spec:'shield',team:team?'red':undefined},{id:'ans',pos:P(3,1,3),hp:100,spec:'ansatsuken'},...extraPlayers];
 const state={time:5,tick:300,phase:'fight',generation:1,setNumber:1,round:1,remaining:0,options:{map:'foundry',gameMode:mode},players:ps,events:[],pve:pve?{entries:[],party:{}}:undefined};
 a.update(state,'me',0);calls.length=0;// the first update only announces the fight phase ('go')
 let id=10;state.events=events.map(e=>({id:++id,time:5,pos:P(1,1,1),...e}));a.update(state,'me',0);
 sounds.push({name,unity,expected:calls});
}
const U=(Kind,o={})=>({Kind,...o});
for(const spec of ['pistol','fire','leet','emperor','ansatsuken','siren','greatsword'])soundScenario('shot-'+spec,[{type:'shot',source:'foe',spec}],[U('Shot',{SourceSpec:spec})]);
soundScenario('block',[{type:'blockHit',source:'ally',target:'foe',message:'melee'}],[U('Block',{SourceSpec:'shield'})]);
soundScenario('block-ansatsuken',[{type:'arcade-parry',source:'ans',target:'foe',spec:'ansatsuken'},{type:'blockHit',source:'ans',target:'foe',spec:'ansatsuken'}],[U('Block',{SourceSpec:'ansatsuken'})]);
soundScenario('reload-start-me',[{type:'reloadStart',source:'me'}],[U('ReloadStart',{SourceMe:true})]);
soundScenario('reload-end-me',[{type:'reloadEnd',source:'me'}],[U('ReloadEnd',{SourceMe:true})]);
soundScenario('reload-other',[{type:'reloadStart',source:'foe'}],[U('ReloadStart')]);
soundScenario('hit-me',[{type:'hit',source:'foe',target:'me',amount:10,message:'direct'}],[U('Hit',{TargetMe:true,TargetPilot:true,Amount:10})]);
soundScenario('self-hit',[{type:'hit',source:'me',target:'me',amount:4,message:'blast'}],[U('Hit',{TargetMe:true,SourceMe:true,TargetPilot:true,Ally:true,Amount:4})]);
soundScenario('hit-by-me',[{type:'hit',source:'me',target:'foe',amount:10,message:'direct'}],[U('Hit',{SourceMe:true,TargetPilot:true,Amount:10})]);
soundScenario('headshot-by-me',[{type:'hit',source:'me',target:'foe',amount:30,message:'direct',headshot:true}],[U('Headshot',{SourceMe:true,TargetPilot:true,Amount:30})]);
soundScenario('hit-ally-teams',[{type:'hit',source:'me',target:'ally',amount:10,message:'direct'}],[U('Hit',{SourceMe:true,TargetPilot:true,Ally:true,Amount:10})],{mode:'teams'});
soundScenario('hit-enemy-teams',[{type:'hit',source:'me',target:'foe',amount:10,message:'direct'}],[U('Hit',{SourceMe:true,TargetPilot:true,Amount:10})],{mode:'teams'});
soundScenario('hit-others',[{type:'hit',source:'foe',target:'ally',amount:10,message:'direct'}],[U('Hit',{TargetPilot:true,Amount:10})]);
soundScenario('burn-tick',[{type:'hit',source:'me',target:'foe',amount:1,message:'burn'}],[U('BurnTick',{SourceMe:true,TargetPilot:true,Amount:1})]);
soundScenario('poison-tick-me',[{type:'hit',source:'foe',target:'me',amount:.5,message:'poison'}],[U('PoisonTick',{TargetMe:true,TargetPilot:true,Amount:.5})]);
soundScenario('add-hit-pve',[{type:'minionHit',source:'me',target:'add-1',amount:5}],[U('Hit',{SourceMe:true,Pve:true,Amount:5})],{pve:true});
soundScenario('add-hit-other-pve',[{type:'minionHit',source:'ally',target:'add-1',amount:5}],[U('Hit',{Pve:true,Amount:5})],{pve:true});
soundScenario('minion-hit-pvp',[{type:'minionHit',source:'me',target:'zombie-1',amount:5}],[U('Hit',{SourceMe:true,Amount:5})]);
for(const material of ['wood','metal','stone'])soundScenario('impact-'+material,[{type:'impact',source:'foe',material}],[U('Impact',{Material:material})]);
soundScenario('impact-player',[{type:'impact',source:'foe',target:'me'}],[U('Impact')]);
for(const material of ['wood','metal','stone'])soundScenario('break-'+material,[{type:'break',source:'foe',material}],[U('Break',{Material:material})]);
soundScenario('death',[{type:'death',source:'foe',target:'ally'}],[U('Death')]);
soundScenario('explosion-chip',[{type:'explosion',source:'foe',message:'shrapnel',radius:1.8}],[U('Explosion',{Detail:'chip',Radius:1.8})]);
soundScenario('explosion-weapon',[{type:'explosion',source:'foe',radius:1.5}],[U('Explosion',{Detail:'chip',Radius:1.5})]);
soundScenario('explosion-ability-frag',[{type:'explosion',source:'foe',message:'frag',radius:3}],[U('Explosion',{Detail:'ability',Radius:3})]);
soundScenario('collapse',[{type:'collapse',source:'foe',radius:3}],[U('Collapse')]);
soundScenario('block-pulse-supernova',[{type:'blockPulse',source:'ally',radius:3,mods:{supernova:1}}],[U('BlockPulse',{Supernova:true,Radius:3}),U('Explosion',{Detail:'chip',Radius:3,Shadowed:true})]);
soundScenario('block-pulse',[{type:'blockPulse',source:'ally',radius:3,mods:{'block-pulse':2}}],[U('BlockPulse',{Radius:3}),U('Explosion',{Detail:'chip',Radius:3,Shadowed:true})]);
soundScenario('arc-ball-lightning',[{type:'arc',source:'foe',from:P(0,2,0)}],[U('Arc',{Radius:.15})]);
soundScenario('leet-lock',[{type:'leet-lock',source:'foe',from:P(0,2,0)}],[U('Arc',{Radius:.25})]);
soundScenario('teleport',[{type:'teleport',source:'foe',from:P(0,1,0)}],[U('Teleport'),U('Arc',{Radius:1,Shadowed:true})]);
soundScenario('blink-strike',[{type:'teleport',source:'foe',from:P(0,1,0),message:'blink-strike'},{type:'explosion',source:'foe',message:'blink-strike',radius:3}],[U('Teleport',{BlinkStrike:true}),U('Arc',{Radius:1,Shadowed:true}),U('Arc',{Radius:1,Shadowed:true}),U('Explosion',{Detail:'ability',Radius:3})]);
soundScenario('panic-button',[{type:'field',source:'foe',message:'repulsion',radius:6},{type:'nova',source:'foe',radius:6,message:'panic-button'}],[U('Field',{Detail:'panic-button',Radius:6})]);
soundScenario('field',[{type:'field',source:'foe',message:'time',radius:5}],[U('Field',{Detail:'freezer-block',Radius:5})]);
soundScenario('ceiling-hit',[{type:'ceiling-hit',source:'foe',message:'ABSORB'}],[U('CeilingHit')]);
soundScenario('stage-saw',[{type:'hit',target:'me',amount:22,message:'stage-saw'},{type:'stage-saw-hit',target:'me',amount:22,message:'SAW'}],[U('HazardHit',{TargetMe:true,TargetPilot:true,Amount:22,Saw:true}),U('Hit',{TargetMe:true,TargetPilot:true,Amount:22})]);
soundScenario('stage-lava',[{type:'hit',target:'foe',amount:8,message:'stage-lava'},{type:'stage-lava-hit',target:'foe',amount:8,message:'LAVA'}],[U('HazardHit',{TargetPilot:true,Amount:8}),U('Hit',{TargetPilot:true,Amount:8})]);
soundScenario('install-mishima',[{type:'install',source:'me',message:'mishima-armor'}],[U('Install',{SourceMe:true,Detail:'mishima-armor'})]);
soundScenario('install-arcade',[{type:'install',source:'me',message:'arcade-focus'}],[U('Install',{SourceMe:true,Detail:'arcade-focus'})]);
soundScenario('install-chip',[{type:'install',source:'me',message:'kinetic'}],[U('Install',{SourceMe:true,Detail:'kinetic'})],{pve:true});
soundScenario('pve-buy-me',[{type:'pve-buy',target:'me',message:'chip',amount:200}],[U('PveBuy',{SourceMe:true,Detail:'chip',Amount:200})],{pve:true});
soundScenario('pve-buy-other',[{type:'pve-buy',target:'ally',message:'heal',amount:75}],[U('PveBuy',{Detail:'med',Amount:75})],{pve:true});
soundScenario('pve-gib',[{type:'pve-gib',source:'me',target:'add-1',amount:32}],[U('PveGib',{SourceMe:true,Amount:32})],{pve:true});
soundScenario('pve-phase',[{type:'pve-phase',amount:1,message:'siren-reveal'}],[U('PvePhase',{Detail:'reveal'})],{pve:true});
soundScenario('pve-intro',[],[U('PvePhase',{Detail:'intro'})],{pve:true});
soundScenario('pve-revive-me',[{type:'pve-revive',target:'me'}],[U('PveRevive',{TargetMe:true,TargetPilot:true})],{pve:true});
soundScenario('pve-revive-other',[{type:'pve-revive',target:'ally'}],[U('PveRevive',{TargetPilot:true})],{pve:true});
soundScenario('pve-cash',[{type:'pve-cash',target:'me',amount:100,message:'kill'}],[U('PveMoney',{TargetMe:true,TargetPilot:true,Amount:100,Detail:'kill'})],{pve:true});
for(const [type,kind,extra] of [['vacuum','Vacuum',{radius:5}],['freeze','Freeze',{target:'foe'}],['critical','Critical',{}],['catch','Catch',{}],['split','Split',{}],['ricochet','Ricochet',{}],['summon','Summon',{message:'zombie'}],['heal','Heal',{target:'me',amount:5}]])
 soundScenario('silent-'+type,[{type,source:'foe',...extra}],[U(kind,{Amount:extra.amount??0})]);

fs.writeFileSync(path.join(root,'Tests/event-feedback-parity.json'),JSON.stringify({source:'05950ea',hitTrim:HIT_TRIM,labels,limiter,shake,numbers,sounds}));
console.log('labels',labels.length,'limiter',limiter.length,'shake',shake.length,'number scenarios',numbers.length,'sound scenarios',sounds.length);
