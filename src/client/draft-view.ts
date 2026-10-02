import * as T from 'three';
import type {Snapshot} from '../shared/simulation.js';
import {DRAFT_LOOK_Z,type DraftStage} from './draft-stage.js';
import {DRAFT_GRAB,grabTimeline} from './draft-timeline.js';

/**
 * DOM side of the draft screen. Hover and focus are kept by chip id because drawMenu rebuilds the markup at readyAt and
 * at the pick. Each frame it maps card centres onto the stage's look plane, tells the stage which card the local picker
 * is on, and flies the chosen card into the pilot's projected palm. Hover is local only; other viewers see the scan.
 */
export class DraftView {
 private hover='';private points:T.Vector3[]=[];private palm={x:0,y:0};private contact='';
 private reduced=typeof matchMedia==='function'&&matchMedia('(prefers-reduced-motion: reduce)').matches;
 constructor(private root:HTMLElement,private onContact:(chip:string)=>void=()=>{}){
  const slot=(e:Event)=>(e.target as Element|null)?.closest?.<HTMLElement>('[data-slot]');
  root.addEventListener('pointerover',e=>{const s=slot(e);if(s)this.hover=s.dataset.slot!;});
  root.addEventListener('pointerout',e=>{if(slot(e)&&!(e.relatedTarget as Element|null)?.closest?.('[data-slot]'))this.hover='';});
  root.addEventListener('focusin',e=>{const s=slot(e);if(s)this.hover=s.dataset.slot!;});
  root.addEventListener('focusout',e=>{if(!(e.relatedTarget as Element|null)?.closest?.('[data-slot]'))this.hover='';});
 }
 /** Move keyboard focus across the enabled cards in reading order (arrow keys). Returns true when handled. */
 step(direction:number){
  const cards=Array.from(this.root.querySelectorAll<HTMLButtonElement>('.draft-slot>.chip:not(:disabled)'));if(!cards.length)return false;
  const at=cards.indexOf(document.activeElement as HTMLButtonElement);cards[at<0?(direction>0?0:cards.length-1):(at+direction+cards.length)%cards.length].focus();return true;
 }
 update(state:Snapshot,stage:DraftStage,local:string){
  const screen=this.root.querySelector<HTMLElement>('.draft-screen'),d=state.draft;
  if(!screen||state.phase!=='draft'||!d){stage.cards=[];stage.hover=stage.taken=-1;return;}
  screen.dataset.stage=stage.active?'on':'off';
  const head=screen.querySelector('.draft-top')?.getBoundingClientRect(),tray=screen.querySelector('.draft-tray')?.getBoundingClientRect();
  // Framing follows the layout until the pick, then holds still through the grab. The chest-up pilot fills the space
  // between the tray's bottom edge and the bottom of the screen.
  if(head&&tray&&innerHeight>0&&!d.chosen){stage.headLine=head.bottom/innerHeight;stage.trayLine=tray.top/innerHeight;stage.trayBottom=tray.bottom/innerHeight;}
  // Reads first (rects), then writes (the flying card), so one layout per frame.
  const slots=Array.from(screen.querySelectorAll<HTMLElement>('.draft-slot')),rects=slots.map(s=>s.getBoundingClientRect());
  while(this.points.length<slots.length)this.points.push(new T.Vector3());
  stage.cards=rects.map((r,i)=>stage.toWorld(r.left+r.width/2,r.top+r.height/2,this.points[i])??this.points[i].set(0,1,DRAFT_LOOK_Z));
  stage.hover=d.player===local&&!d.chosen?slots.findIndex(s=>s.dataset.slot===this.hover):-1;
  const taken=slots.findIndex(s=>s.classList.contains('taken'));stage.taken=taken;
  if(taken<0)return;
  const card=slots[taken].querySelector('.chip') as HTMLElement|null,g=grabTimeline(state.time-(d.chosenAt??state.time)),r=rects[taken];
  if(card){
   // The card flies as a fixed-position copy above the tray, so neither a scrolling row nor the slot can clip it.
   let fly=screen.querySelector<HTMLElement>(':scope>.draft-fly');
   if(!fly){fly=document.createElement('div');fly.className='draft-fly draft-tray';fly.setAttribute('aria-hidden','true');fly.append(card.cloneNode(true));screen.append(fly);card.style.visibility='hidden';}
   fly.style.cssText=`left:${r.left.toFixed(1)}px;top:${r.top.toFixed(1)}px;width:${r.width.toFixed(1)}px;height:${r.height.toFixed(1)}px`;
   if(!this.reduced&&stage.active&&stage.palmScreen(this.palm)){
    const dx=(this.palm.x-(r.left+r.width/2))*g.fly,dy=(this.palm.y-(r.top+r.height/2))*g.fly;
    fly.style.transform=`translate(${dx.toFixed(1)}px,${dy.toFixed(1)}px) scale(${(1-.88*g.fly).toFixed(3)}) rotate(${(-9*g.fly).toFixed(2)}deg)`;fly.style.opacity=(1-g.fade).toFixed(3);
   }else fly.style.opacity=(1-g.fly).toFixed(3);
  }
  // One install sound per pick when the hand closes, for every viewer (late joiners stay quiet).
  const key=`${d.player}:${d.turn}:${d.chosen}:${d.chosenAt}`;
  if(g.contact&&this.contact!==key){this.contact=key;if(g.e<DRAFT_GRAB.contact+.3)this.onContact(d.chosen!);}
 }
}
