// Build-time only: render the pinned original Siren reveal's smile (client/siren-card.ts sirenCinematic() under
// client/chip-cards.css and client/pve/pve.css, as pve/siren-reveal.ts writes it) in headless Chromium and record what
// Tests/SirenSmileChecks.cs compares with Core/NativeSirenSmile.cs: the face's path data, bounds and paint, the grin's
// transform origin and its opening (siren-grin), the stare's flicker (siren-stare), the flame row's rise (siren-rise) and
// every tongue's screen transform (siren-lick, the xMidYMax slice), the glitch tears, the drop-shadow, the reveal tray's
// spotlight entrance (pve-spot-in), the booting chip (siren-boot) and the locked card's pulse (pve-locked). Every
// animation is paused at an exact time, as the source's negative delays place it.
// Usage: node tools/export_siren_smile_fixtures.mjs <source checkout at 05950ea with node_modules> [chromium executable]
import fs from 'node:fs';import path from 'node:path';import {createRequire} from 'node:module';import {fileURLToPath,pathToFileURL} from 'node:url';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]),chromium=process.argv[3];
const pinned={'src/client/siren-card.ts':'f6a23eed8cfeb8dd8c7548b20aaf0a4fb667ee30','src/client/chip-cards.css':'983883874b47bf9c8aee4bd86bd4d1a4c1b975fd',
 'src/client/pve/pve.css':'8837ef32c07490f212a054b8efb75b641a62044b','src/client/pve/siren-reveal.ts':'9409582b4b746ff4a541f1e8755041f2073f5a7d'};
for(const [file,blob] of Object.entries(pinned)){const bytes=fs.readFileSync(path.join(source,file));if(createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')!==blob)throw Error('Source blob mismatch: '+file);}
const {register}=await import(pathToFileURL(path.join(source,'node_modules/tsx/dist/esm/api/index.mjs')).href);register();
const card=await import(pathToFileURL(path.join(source,'src/client/siren-card.ts')).href);
const css=fs.readFileSync(path.join(source,'src/client/chip-cards.css'),'utf8')+'\n'+fs.readFileSync(path.join(source,'src/client/pve/pve.css'),'utf8');
const req=createRequire(path.join(source,'node_modules/'));let pw;try{pw=req('playwright');}catch{pw=createRequire('/opt/node-tools/node_modules/')('playwright');}
const browser=await pw.chromium.launch(chromium?{executablePath:chromium}:{});const page=await browser.newPage({viewport:{width:1600,height:900}});
// The smile exactly as siren-reveal.ts writes it at the window's start (delay 0, duration 1.40 s), the tray's spotlight,
// a booting Siren chip and a locked card.
const smile=`<div class="pve-reveal" data-window="smile"><div class="siren-selection pve-smile" style="--selection-duration:1.40s;--selection-delay:0.000s">${card.sirenCinematic()}</div></div>`;
const extras=`<div id="extras" style="position:fixed;left:0;top:0;width:600px"><div class="pve-reveal-spotlight" style="--reveal-age:0s"><strong>Find me...</strong><p>x</p></div>
<button class="chip circuit-chip rarity-siren draft-reveal" style="--reveal-delay:0s;width:228px;height:236px"></button><div class="pve-locked-card"><span>???</span><small>3 / 4</small></div></div>`;
await page.setContent(`<!doctype html><html><head><meta charset="utf-8"><style>${css}</style><style>body{margin:0;background:#000}</style></head><body>${smile}${extras}</body></html>`);
const p9=v=>Number(Number(v).toPrecision(9));
const out=await page.evaluate(()=>{
 const q=s=>document.querySelector(s),qa=s=>[...document.querySelectorAll(s)],cs=e=>getComputedStyle(e);
 const anims=document.getAnimations();for(const a of anims)a.pause();
 const at=t=>{for(const a of anims)a.currentTime=t*1000;};
 const m=e=>{const t=cs(e).transform;if(t==='none')return [1,0,0,1,0,0];return t.replace(/matrix\(|\)/g,'').split(',').map(Number);};
 const box=e=>{const b=e.getBBox();return [b.x,b.y,b.width,b.height];};const rect=e=>{const b=e.getBoundingClientRect();return [b.x,b.y,b.width,b.height];};
 const ctm=e=>{const c=e.getScreenCTM();return [c.a,c.b,c.c,c.d,c.e,c.f];};
 const stare=q('.siren-stare'),mouth=q('.siren-stare .siren-mouth'),flames=q('.siren-cinema>.siren-flames'),tears=qa('.siren-cinema-tear'),eyes=qa('.siren-stare .siren-eye');
 const face={eyes:eyes.map(e=>e.getAttribute('d')),grin:q('.siren-stare .siren-grin').getAttribute('d'),teeth:q('.siren-stare .siren-teeth').getAttribute('d'),viewBox:stare.getAttribute('viewBox'),
  eyeBoxes:eyes.map(box),grinBox:box(q('.siren-stare .siren-grin')),teethBox:box(q('.siren-stare .siren-teeth')),mouthOrigin:cs(mouth).transformOrigin,mouthBox:box(mouth),
  eyeFill:cs(eyes[0]).fill,grinFill:cs(q('.siren-stare .siren-grin')).fill,teethStroke:cs(q('.siren-stare .siren-teeth')).stroke,teethWidth:cs(q('.siren-stare .siren-teeth')).strokeWidth,
  teethJoin:cs(q('.siren-stare .siren-teeth')).strokeLinejoin,teethCap:cs(q('.siren-stare .siren-teeth')).strokeLinecap,teethMiter:cs(q('.siren-stare .siren-teeth')).strokeMiterlimit,teethFill:cs(q('.siren-stare .siren-teeth')).fill,
  filter:cs(stare).filter,stareRect:rect(stare)};
 const tongues=qa('.siren-cinema .siren-tongue').map(g=>{const rim=g.querySelector('.rim'),core=g.querySelector('.core'),place=g.parentNode.getAttribute('transform');
  return {cls:g.getAttribute('class'),place,d:rim.getAttribute('d'),box:box(rim),origin:cs(g).transformOrigin,rimFill:cs(rim).fill,rimStroke:cs(rim).stroke,rimStrokeOpacity:cs(rim).strokeOpacity,rimStrokeWidth:cs(rim).strokeWidth,
   vectorEffect:cs(rim).vectorEffect,core:core?{fill:cs(core).fill,transform:core.getAttribute('transform')}:null};});
 const flameInfo={viewBox:flames.getAttribute('viewBox'),aspect:flames.getAttribute('preserveAspectRatio'),overflow:cs(flames).overflow};
 const scan=cs(q('.siren-cinema-scan')).backgroundImage,tear={background:cs(tears[0]).backgroundImage,height:cs(tears[0]).height,tops:tears.map(t=>cs(t).top)};
 const smile=[];
 for(let i=0;i<=140;i++){const t=i/100;at(t);const mm=m(mouth),fm=m(flames);
  smile.push({t,stare:+cs(stare).opacity,mouthScale:mm[0],mouthOpacity:+cs(mouth).opacity,rise:fm[5],tears:tears.map(e=>({opacity:+cs(e).opacity,dx:m(e)[4]})),flameRect:rect(flames)});}
 const licks=[];for(const t of [0,.31,.77,1.13,1.39,2.6,4.05]){at(t);licks.push({t,lick:qa('.siren-cinema .siren-tongue').map(g=>m(g)),ctm:qa('.siren-cinema .siren-tongue .rim').map(ctm)});}
 const spot=q('#extras .pve-reveal-spotlight'),boot=q('#extras .draft-reveal'),locked=q('#extras .pve-locked-card span');
 const spotIn=[],sirenBoot=[],pulse=[];
 for(let i=0;i<=70;i++){const t=i/100;at(t);const s=m(spot);spotIn.push({t,opacity:+cs(spot).opacity,sx:s[0],sy:s[3],tx:s[4]});}
 for(let i=0;i<=125;i+=1){const t=i/100;at(t);const s=m(boot);sirenBoot.push({t,opacity:+cs(boot).opacity,sx:s[0],sy:s[3],tx:s[4]});}
 for(let i=0;i<=44;i++){const t=i/20;at(t);pulse.push({t,opacity:+cs(locked).opacity,color:cs(locked).color});}
 return {face,tongues,flameInfo,scan,tear,smile,licks,spotIn,sirenBoot,pulse,viewport:[innerWidth,innerHeight]};
});
await browser.close();
const round=v=>Array.isArray(v)?v.map(round):v&&typeof v==='object'?Object.fromEntries(Object.entries(v).map(([k,x])=>[k,round(x)])):typeof v==='number'?p9(v):v;
fs.writeFileSync(path.join(root,'Tests/siren-smile-parity.json'),JSON.stringify({source:'05950eaef80ff2483a9c44d898cad98be1a76bbc',...round(out)})+'\n');
console.log('wrote Tests/siren-smile-parity.json',out.smile.length,'smile samples,',out.tongues.length,'tongues');
