// Build-time only: run the pinned original HUD code and record what it shows, for Tests/HudPlaceholderChecks.cs to replay
// through Core/NativeHudRules.cs: client/hitmarker.ts (HitmarkerTrack and the Hitmarker element's opacity, scale, classes
// and element icons, frame by frame), client/sprint-hud.ts sprintStatus, client/siren-hud.ts sirenHud, and main.ts drawHud's
// DEV fighter HUD (Ansatsuken arcade HUD and combo, Emperor ELECTRIC card and combo) and L33t .leet-status box, each run as
// the pinned statement itself; plus the cascaded style.css / match-hud.css declarations those elements draw with.
// Usage: node tools/export_hud_fixtures.mjs <source checkout at 05950ea with node_modules>
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {createHash} from 'node:crypto';import {createRequire} from 'node:module';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]);
const pinned={'src/client/main.ts':'bde1a432d79fa9cb8f3024062f87fad80ccd5253','src/client/hitmarker.ts':'76169d83040993fbfe75e6a0f1a867244a253551',
 'src/client/sprint-hud.ts':'4d0b086f43b1241409fead2bef381df5cf6a704f','src/client/siren-hud.ts':'a0cc2b3c6a5a06227595522862f652930494dd02',
 'src/shared/siren-contract.ts':'ad494dfd203d8f12cdfc69b2c73004807af28ccc','src/shared/teams.ts':'5ba513aa50878b9e58b9d90b70a121d03128374e',
 'src/shared/leet-content.ts':'0e801d175ffbe113926cae1bd9543f580eb76322','src/client/style.css':'338e2731dcd86f86b6b0c560e4f0d02d0a6564a0',
 'src/client/match-hud.css':'5b4d0b3b6e72538bad6a3f653ca4d77698c842cd'};
for(const [file,blob] of Object.entries(pinned)){const bytes=fs.readFileSync(path.join(source,file));if(createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')!==blob)throw Error('Source blob mismatch: '+file);}
// The Hitmarker element is built in the constructor with document; a minimal element stand-in records what update() sets.
const element=()=>{const e={style:{},hidden:false,innerHTML:'',classes:new Set(),kids:{},setAttribute(){},append(){}};e.classList={toggle:(c,on)=>{if(on)e.classes.add(c);else e.classes.delete(c);}};e.querySelector=s=>e.kids[s]??=element();return e;};
globalThis.document={createElement:()=>element(),body:{append(){}}};
const {register}=await import(pathToFileURL(path.join(source,'node_modules/tsx/dist/esm/api/index.mjs')).href);register();
const imp=f=>import(pathToFileURL(path.join(source,f)).href);
const hm=await imp('src/client/hitmarker.ts'),sprint=await imp('src/client/sprint-hud.ts'),sirenHud=await imp('src/client/siren-hud.ts'),contract=await imp('src/shared/siren-contract.ts'),leet=await imp('src/shared/leet-content.ts');
const esbuild=createRequire(path.join(source,'package.json'))('esbuild');
const p9=v=>Number(v.toPrecision(9));
const out={hitmarker:[],sprint:[],siren:[],arcade:[],emperor:[],leet:[],css:{},contract:{slots:contract.SIREN_SLOTS,names:contract.SIREN_MOVE_NAMES,brainSeconds:leet.LEET.brainSeconds,freezeSeconds:leet.LEET.freezeSeconds}};

// 1. HitmarkerTrack + Hitmarker.update: me 'a' (red) against enemy 'b' (blue) and ally 'c' (red, teams mode).
const players=[{id:'a',team:'red'},{id:'b',team:'blue'},{id:'c',team:'red'}];
function track(name,events,times,{mode='teams',pve=false,generations}={}){
 const h=new hm.Hitmarker(),cross=h.root.querySelector('.hitmark-cross'),icons=h.root.querySelector('.hitmark-elements'),frames=[];
 for(const [i,time] of times.entries()){
  const gen=generations?generations[i]:0,state={time,generation:gen,players,options:{gameMode:mode},pve:pve?{}:undefined,events:events.filter(e=>e.id<=1000&&e.time<=time+1e-9&&(e.gen??0)===gen)};
  h.update(state,'a',true);
  frames.push({time,gen,hidden:h.root.hidden,headshot:h.root.classes.has('headshot'),dot:h.root.classes.has('dot-tick'),opacity:p9(Number(cross.style.opacity)),
   scale:p9(Number(/scale\(([^)]+)\)/.exec(cross.style.transform)[1])),elements:[...icons.innerHTML.matchAll(/class="hitmark-(\w+)"/g)].map(m=>m[1])});
 }
 out.hitmarker.push({name,mode,pve,events:events.map(({gen,...e})=>({gen:gen??0,...e})),frames});
}
const hit=(id,time,o={})=>({id,time,type:'hit',source:'a',target:'b',amount:12,...o});
track('crisp direct',[hit(1,10,{message:'direct'})],[10,10.05,10.1,10.2,10.3,10.335,10.4]);
track('chain proc is a dot',[hit(1,10,{message:'chain'})],[10,10.05,10.1,10.15,10.199,10.21]);
for(const kind of ['field','ability','blast','static','thorns','drainDash','debris','minion','blockPulse','chaosBomb','corpseParty','dashBlast','root','shrapnel','trampoline','wormhole'])
 track('proc '+kind,[hit(1,10,{message:kind})],[10.02]);
for(const kind of ['secondary','reflected','melee','siren-claw','mishima-electric','arcade-echo','leet-orb','stage'])track('crisp '+kind,[hit(1,10,{message:kind})],[10.02]);
track('crisp holds against a proc within .22',[hit(1,10,{message:'direct'}),hit(2,10.1,{message:'chain'})],[10.05,10.15,10.3,10.36]);
track('proc after .22 replaces a crisp',[hit(1,10,{message:'direct'}),hit(2,10.25,{message:'field'})],[10.2,10.26,10.4,10.46]);
track('crisp replaces a proc at once',[hit(1,10,{message:'chain'}),hit(2,10.1,{message:'direct'})],[10.05,10.12,10.3]);
track('second proc within .22 keeps the first',[hit(1,10,{message:'chain'}),hit(2,10.1,{message:'static'})],[10.15,10.205,10.3]);
track('burn ticks are dots with the flame',[hit(1,10,{message:'burn',amount:1.5}),hit(2,10.25,{message:'burn',amount:1.5}),hit(3,10.5,{message:'burn',amount:1.5})],[10.01,10.15,10.22,10.26,10.6,11.0,11.15,11.25]);
track('poison then burn keep their order',[hit(1,10,{message:'poison',amount:1.25}),hit(2,10.3,{message:'burn',amount:1.5})],[10.05,10.35,10.85,11.05]);
track('elements older than .5 are dropped on the next tick',[hit(1,10,{message:'poison',amount:1.25}),hit(2,10.6,{message:'burn',amount:1.5})],[10.65,11.25,11.35]);
track('headshot',[hit(1,10,{message:'direct',headshot:true})],[10.01,10.1]);
track('proc headshot shows no red',[hit(1,10,{message:'chain',headshot:true})],[10.01]);
track('teammate, self, other source and zero damage show nothing',[hit(1,10,{target:'c'}),hit(2,10.01,{target:'a'}),hit(3,10.02,{source:'b',target:'c'}),hit(4,10.03,{amount:0})],[10.05]);
track('free-for-all hits the same team id',[hit(1,10,{target:'c'})],[10.05],{mode:'ffa'});
track('stale events are skipped',[hit(1,9.5,{message:'direct'})],[10]);
track('PvE add hits: untagged crisp, tagged proc dot',[{id:1,time:10,type:'minionHit',source:'a',target:'add-3',amount:20},{id:2,time:10.4,type:'minionHit',source:'a',target:'add-3',amount:3,message:'field'}],[10.05,10.42,10.5],{pve:true});
track('summon and non-PvE minion hits show nothing',[{id:1,time:10,type:'minionHit',source:'a',target:'summon-1',amount:20},{id:2,time:10.05,type:'minionHit',source:'a',target:'add-1',amount:20}],[10.1],{pve:false});
track('PvE summon hit shows nothing',[{id:1,time:10,type:'minionHit',source:'a',target:'summon-1',amount:20}],[10.1],{pve:true});
track('a new generation clears',[hit(1,10,{message:'direct'}),{...hit(2,10.05,{message:'chain'}),gen:1}],[10.02,10.06,10.1],{generations:[0,1,1]});
track('time rewinds clear',[hit(1,10,{message:'direct'})],[10.05,9.9,10.06]);
out.addPrefix=(await imp('src/shared/pve/contract.ts')).ADD_PREFIX;

// 2. sprintStatus: every flag combination; the markup's tag text and exhausted class.
for(const active of [true,false])for(const hp of [100,0])for(const sprinting of [true,false])for(const exhausted of [true,false]){
 const html=sprint.sprintStatus({hp,sprinting,sprintExhausted:exhausted},active);
 out.sprint.push({active,hp,sprinting,exhausted,shown:html!=='',exhaustedClass:html.includes('exhausted'),chevron:html.includes('»'),text:html.replace(/<i[^>]*>.*?<\/i>/,'').replace(/<[^>]+>/g,'').trim()});
}

// 3. sirenHud: both modes, each toggle key, ready and cooling moves; every span's key, label and state.
const spans=html=>[...html.matchAll(/<span><kbd>([^<]*)<\/kbd> ([^<]*?)(?: <b>([^<]*)<\/b>)?<\/span>/g)].map(m=>({key:m[1],label:m[2],state:m[3]??null}));
for(const [mode,key,ready,time] of [['melee','KeyQ',{},10],['melee','KeyF',{rend:12.3,slam:10,rush:9,laugh:39.96}, 10],['ranged','KeyV',{damnation:10.05,eruption:15,rain:27.95,mark:44.5},10],['ranged','KeyQ',{mark:10.04},10]]){
 const html=sirenHud.sirenHud({spec:'siren',siren:{mode,ready}},time,key);out.siren.push({mode,key,ready,time,spans:spans(html),container:/class="([^"]+)"/.exec(html)[1]});
}
out.siren.push({mode:'melee',key:'KeyQ',ready:{},time:0,spans:spans(sirenHud.sirenHud({spec:'siren'},0,'KeyQ')),container:'arcade-hud siren-hud',noState:true});
if(sirenHud.sirenHud({spec:'fire'},0)!=='')throw Error('siren HUD drawn for another spec');

// 4. main.ts drawHud statements (the pinned lines, run as written): the arcade, Emperor and L33t HUD markup.
const lines=fs.readFileSync(path.join(source,'src/client/main.ts'),'utf8').split('\n');
const statement=prefix=>{const found=lines.filter(l=>l.startsWith(prefix));if(found.length!==1)throw Error('main.ts statement not found: '+prefix);return found[0];};
const compile=(code,args)=>{const js=esbuild.transformSync(code,{loader:'ts'}).code;return new Function(...args,js+';return html;');};
const arcade=compile(statement(" if(state.phase==='fight'&&me.spec==='ansatsuken'){"),['state','me','html']);
const emperor=compile(statement(" if(state.phase==='fight'&&me.spec==='emperor'){"),['state','me','html']);
const leetLine=statement(" if(state.phase==='fight'&&me.leet){");if(leetLine.split('(import.meta as any).env.BASE_URL').length!==2)throw Error('leet scare image path changed');
const leetHud=compile(leetLine.replace('(import.meta as any).env.BASE_URL','"/"'),['state','me','escape','html']);
const text=s=>s.replace(/<[^>]+>/g,'');
const parseArcade=html=>({specials:[...html.matchAll(/<span class="arcade-special"><kbd>([^<]*)<\/kbd> ([^<]*?) <b>([^<]*)<\/b><\/span>/g)].map(m=>({key:m[1],label:m[2],state:m[3]})),
 hud:/<div class="(arcade-hud[^"]*)"/.exec(html)?.[1]??null,
 charge:(m=>m?{text:m[1],width:Number(m[2])}:null)(/<div class="hadoken-charge"><strong>([^<]*)<\/strong><i style="width:([\d.]+)%"><\/i><\/div>/.exec(html)),
 combo:(m=>m?{cls:m[1],hits:m[2],count:m[3],small:m[4],damage:m[5],width:p9(Number(m[6]))}:null)(/<div class="(arcade-combo[^"]*)"(?: data-hits="(\d+)")?><strong>(\d+)<small>([^<]*)<\/small><\/strong><span>([^<]*)<\/span><i style="width:([-\d.e]+)%"><\/i><\/div>/.exec(html))});
for(const [phase,a,time] of [['fight',undefined,10],['fight',{hadokenCd:0,uppercutCd:0,charge:0,hits:0,comboDamage:0,lastHit:-100},10],['fight',{hadokenCd:1.04,uppercutCd:5.96,action:'charge',charge:.4,hits:3,comboDamage:41.6,lastHit:9.7},10],
 ['fight',{hadokenCd:0,uppercutCd:.05,action:'charge',charge:1,hits:1,comboDamage:9,lastHit:8.95},10],['fight',{hadokenCd:0,uppercutCd:0,charge:0,hits:7,comboDamage:120.5,lastHit:9.0},10.15],['fight',{hadokenCd:0,uppercutCd:0,charge:0,hits:7,comboDamage:120.5,lastHit:9.0},10.05],['countdown',{hadokenCd:1,uppercutCd:1,charge:0,hits:2,comboDamage:20,lastHit:9.9},10]]){
 const html=arcade({phase,time},{spec:'ansatsuken',arcade:a},'');out.arcade.push({phase,arcade:a??null,time,shown:html!=='',...parseArcade(html)});
}
for(const [phase,cooldown,a,time] of [['fight',0,undefined,10],['fight',.35,{hits:1,comboDamage:8,lastHit:9.5},10],['fight',2.04,{hits:4,comboDamage:53.5,lastHit:8.7},10],['fight',0,{hits:2,comboDamage:20,lastHit:8.6},10],['result',0,{hits:2,comboDamage:20,lastHit:9.9},10]]){
 const html=emperor({phase,time},{spec:'emperor',cooldown,emperor:a},'');out.emperor.push({phase,cooldown,emperor:a??null,time,shown:html!=='',...parseArcade(html)});
}
const named=[{id:'a',name:'Sheldon'},{id:'b',name:'Astra'}];
for(const [phase,l,time] of [['fight',{controlling:'b',controlUntil:13.26},10],['fight',{controlledBy:'b',controlUntil:12.04},10],['fight',{controlling:'z',controlUntil:9},10],['fight',{frozenUntil:13.51},10],
 ['fight',{frozenUntil:9},10],['fight',{controlledBy:'b',controlUntil:14,frozenUntil:13},10],['fight',{},10],['result',{controlling:'b',controlUntil:12},10]]){
 const html=leetHud({phase,time,players:named},{leet:l},s=>s,'');const m=/<div class="leet-status"><strong>([^<]*)<\/strong><span>([^<]*)<\/span><\/div>/.exec(html);
 out.leet.push({phase,leet:l,time,shown:!!m,title:m?.[1]??null,sub:m?.[2]??null});
}

// 5. The cascade of style.css then match-hud.css (main.ts import order) for the selectors these elements match, outside
// @media blocks: later rules win at equal specificity, !important wins. Values are recorded as written, with the file each
// winning declaration comes from (cssFrom), and the sprint chevron's keyframes.
const sheets=['src/client/style.css','src/client/match-hud.css'].map(f=>({file:path.basename(f),text:fs.readFileSync(path.join(source,f),'utf8').replace(/\/\*[\s\S]*?\*\//g,'')}));
function rules(text,file){const found=[];let i=0,start=0,media=0;
 for(;i<text.length;i++){const c=text[i];if(c==='{'){const head=text.slice(start,i).trim();if(head.startsWith('@')){media++;start=i+1;continue;}const end=text.indexOf('}',i);if(!media)found.push({file,selectors:head.split(',').map(s=>s.trim()),body:text.slice(i+1,end)});i=end;start=i+1;}
  else if(c==='}'){if(media)media--;start=i+1;}}
 return found;}
const all=sheets.flatMap(s=>rules(s.text,s.file));out.cssFrom={};
for(const sel of ['.arcade-hud','.arcade-special','.arcade-special b','.hadoken-charge','.hadoken-charge strong','.hadoken-charge i','.arcade-combo','.arcade-combo strong','.arcade-combo small','.arcade-combo span','.arcade-combo i',
 '.emperor-combo','.emperor-combo i','.emperor-hud .arcade-special','.siren-hud','.siren-hud>span','.siren-hud b','.leet-status','.leet-status strong','.leet-status span','.sprint-status','.sprint-status i','.sprint-status.exhausted',
 '.status-tags','.status-tags span','.healthbox .status-tags','.combat-hitmarker','.combat-hitmarker.headshot','.hitmark-elements','.hitmark-elements svg','.hitmark-fire','.hitmark-poison']){
 const props={},from={};for(const r of all)if(r.selectors.includes(sel))for(const d of r.body.split(';')){const k=d.indexOf(':');if(k<0)continue;const name=d.slice(0,k).trim(),value=d.slice(k+1).trim();if(!name)continue;
  if(props[name]?.endsWith('!important')&&!value.endsWith('!important'))continue;props[name]=value;from[name]=r.file;}
 if(!Object.keys(props).length)throw Error('no rules for '+sel);out.css[sel]=props;out.cssFrom[sel]=from;
}
out.keyframes={'sprint-chevron':/@keyframes sprint-chevron\{(.*?\})\}/.exec(sheets[0].text)[1]};
// The marker's svg strokes and icons (viewBox units), as written in hitmarker.ts.
const src=fs.readFileSync(path.join(source,'src/client/hitmarker.ts'),'utf8');
out.svg={cross:/<path d="(m34[^"]+)"/.exec(src)[1],tips:/hitmark-tips" d="([^"]+)"/.exec(src)[1],stroke:Number(/stroke-width="(\d+)" stroke-linecap/.exec(src)[1]),flame:/const flame='<path d="([^"]+)"/.exec(src)[1],poison:/const poison='<path d="([^"]+)"/.exec(src)[1],iconStroke:Number(/stroke-width="([\d.]+)" stroke-linejoin/.exec(src)[1])};

fs.writeFileSync(path.join(root,'Tests/hud-placeholder-parity.json'),JSON.stringify({source:pinned,...out})+'\n');
console.log('Exported',out.hitmarker.length,'hitmarker tracks,',out.sprint.length,'sprint tags,',out.siren.length,'Siren HUDs,',out.arcade.length+out.emperor.length,'fighter HUDs,',out.leet.length,'L33t boxes,',Object.keys(out.css).length,'style rules');
