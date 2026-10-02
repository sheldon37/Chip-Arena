/**
 * Development-only draft lab: the real chip-draft overlay (draft-presentation.ts markup, draft-screen.css) over the real
 * 3D draft stage (draft-stage.ts) and DraftView, driven by a local Simulation at a chosen moment, for any spec including
 * dev specs and bosses (the lab builds the snapshot directly, so no account access is involved). Not built by Vite and
 * never imported from src. With the dev server running, open /tools/draft-lab/index.html?<query> at any window size.
 *
 *  spec=<weapon spec>     the picker's spec (default pistol; siren is the boss path)
 *  n=<1-12>               number of offers (default 6)
 *  at=<s>                 seconds after the cards became ready (default 2)
 *  hover=<index>          hover that card from hoverAt seconds after ready (default none); hover2/hover2At switch later
 *  pick=<index>, after=<s>   pick that card at pickAt (default .4 s after ready) and show `after` seconds later
 *  mine=0                 another player's turn (cards disabled, the pilot scans)
 *  open=<s>               show <s> seconds after the draft turn started instead (boot-in / camera arrival)
 *  hat=<key>, color=#hex, body=#hex   cosmetics (hats are framed in)
 *  dt=<s>                 frame step (default 1/60), reduced=1 reduced motion
 */
import * as T from 'three';
import '../../src/client/style.css';
import '../../src/client/lobby.css';
import '../../src/client/match-hud.css';
import '../../src/client/chip-cards.css';
import '../../src/client/draft-screen.css';
import {Simulation,initPhysics,type Snapshot} from '../../src/shared/simulation.js';
import {eligibleChips} from '../../src/shared/content.js';
import {DraftStage} from '../../src/client/draft-stage.js';
import {DraftView} from '../../src/client/draft-view.js';
import {draftScreen} from '../../src/client/draft-presentation.js';

const q=new URLSearchParams(location.search),num=(k:string,d:number)=>q.has(k)?Number(q.get(k)):d;
(async()=>{try{
 await initPhysics();
 const spec=(q.get('spec')||'pistol') as any,n=Math.max(1,Math.min(12,num('n',6))),mine=q.get('mine')!=='0',dt=num('dt',1/60),reduced=q.get('reduced')==='1';
 const sim=new Simulation({sandbox:true});sim.addPlayer('me','Pilot','pistol');sim.addPlayer('other','Target Dummy','melee');sim.phase='fight';
 sim.debug({action:'draft-preview'},'me');
 const base=sim.snapshot(),d=base.draft!;
 const pool=eligibleChips(spec==='siren'?'siren':spec,{}),offers:string[]=[];for(const c of pool){if(offers.length>=n)break;if(!offers.includes(c.id))offers.push(c.id);}
 const me=sim['players'].get('me');me.spec=spec;me.offers=offers;
 if(q.get('color'))me.color=q.get('color');me.cosmetics={...(me.cosmetics||{}),...(q.get('hat')?{hat:q.get('hat')}:{}),...(q.get('body')?{bodyColor:q.get('body')}:{})};
 const ready=d.readyAt,local=mine?'me':'other';
 const pickIndex=q.has('pick')?num('pick',0):-1,pickAt=ready+num('pickAt',.4),after=num('after',.5);
 const end=q.has('open')?d.started+num('open',0):pickIndex>=0?pickAt+after:ready+num('at',2);
 const canvas=document.querySelector<HTMLCanvasElement>('#game')!,ui=document.querySelector<HTMLElement>('#ui')!;
 const renderer=new T.WebGLRenderer({canvas,antialias:true});renderer.setPixelRatio(1);renderer.setSize(innerWidth,innerHeight,false);
 renderer.outputColorSpace=T.SRGBColorSpace;renderer.toneMapping=T.ACESFilmicToneMapping;renderer.toneMappingExposure=.92;
 const stage=new DraftStage(),view=new DraftView(ui);let markup='',picked=false;
 const hover=(i:number)=>{const s=ui.querySelectorAll<HTMLElement>('.draft-slot')[i];if(s)s.dispatchEvent(new PointerEvent('pointerover',{bubbles:true}));};
 const hoverAt=ready+num('hoverAt',.2),hover2At=ready+num('hover2At',1e9);let hovered=-2;
 for(let t=d.started;t<=end+1e-9;t+=dt){
  if(pickIndex>=0&&!picked&&t>=pickAt){sim.time=t;sim.selectChip('me',offers[pickIndex]);picked=true;}
  sim.time=t;const s:Snapshot=sim.snapshot();
  const key=`${s.draft?.chosen}:${t>=ready}`;if(key!==markup){markup=key;ui.innerHTML=draftScreen(s,local,innerWidth,innerHeight,t);}
  const want=t>=hover2At?num('hover2',-1):t>=hoverAt&&q.has('hover')?num('hover',-1):-1;if(want!==hovered){hovered=want;if(want>=0)hover(want);}
  view.update(s,stage,local);stage.update(s,innerWidth/innerHeight,dt,1,false,reduced);
 }
 renderer.render(stage.scene,stage.camera);(window as any).labDone=true;
}catch(e){console.error(e);(window as any).labError=String((e as Error)?.stack||e);}})();
