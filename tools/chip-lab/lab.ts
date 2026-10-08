/**
 * Development-only Chip lab: the real Chip cards, draft reveal, selection spotlight and player-board chips with the
 * game's stylesheets, without playing to a draft. Not built by Vite and never imported from src. With `npm run dev`,
 * open /tools/chip-lab/index.html?<query>.
 *
 *  view=draft (default)   the draft panel; chips=<id,id,...> (default: one per ordinary tier), t=<s> since the draft
 *                         opened (0 replays the reveal), mine=0 shows another player's turn (cards disabled)
 *  view=all               every rarity, ordinary and exclusive, in one grid
 *  view=spotlight         chip=<id> as the installed pick, t=<s> since it was chosen; chips= the discarded offers
 *  view=board             the Tab board's installed chips for chips=<id,...>
 */
import '../../src/client/style.css';
import '../../src/client/lobby.css';
import '../../src/client/match-hud.css';
import '../../src/client/chip-cards.css';
import {chipCard,rarityStyle} from '../../src/client/chip-card.js';
import {selectedChip,brokenChip} from '../../src/client/draft-presentation.js';
import {chipIcon} from '../../src/client/icons.js';
import {CHIPS,RARITIES,type Rarity} from '../../src/shared/content.js';

const q=new URLSearchParams(location.search),view=q.get('view')||'draft',t=Number(q.get('t')||'6');
const byId=(id:string)=>{const c=CHIPS.find(c=>c.id===id);if(!c)throw new Error('unknown chip '+id);return c;};
const firstOf=(r:Rarity)=>CHIPS.find(c=>c.rarity===r)!;
const list=q.get('chips')?q.get('chips')!.split(',').filter(Boolean).map(byId):view==='all'?(Object.keys(RARITIES) as Rarity[]).map(firstOf):(['common','uncommon','rare','epic','legendary','mythic'] as Rarity[]).map(firstOf);
const ui=document.getElementById('ui')!;ui.dataset.phase='draft';
const panel=(title:string,body:string)=>`<main id="menus" class="menus"><section class="panel wide"><div class="panel-head"><div><div class="eyebrow">CHIP LAB / ${view.toUpperCase()}</div><h1>${title}</h1></div></div>${body}</section></main>`;
const mine=q.get('mine')!=='0';
if(view==='spotlight'){
 const c=byId(q.get('chip')||'siren-crown');
 ui.innerHTML=panel('Upgrade secured',`<div class="chip-grid draft-grid selection-locked">${list.map((x,i)=>x.id===c.id?'<div class="selected-chip-space" aria-hidden="true"></div>':brokenChip(x,0,i,t)).join('')}</div>`)+selectedChip(c,1,'Lab pilot',t);
}else if(view==='board'){
 ui.innerHTML=`<div class="player-board"><section class="board-panel"><div class="board-players"><article class="board-player own"><div class="board-chip-heading">INSTALLED UPGRADES <span>${list.length} types</span></div><div class="board-chip-grid">${list.map(c=>`<button class="board-chip rarity-${c.rarity}" style="${rarityStyle(c)}"><span class="board-chip-symbol">${chipIcon(c.id)}</span><strong>${c.name}</strong><b>×1</b></button>`).join('')}</div></article></div></section></div>`;
}else{
 ui.innerHTML=panel(mine?'Make your next move':'Lab pilot is choosing',`<p>${mine?'Choose one Chip. Everyone can see this draft.':'Watch the draft.'}</p><div class="chip-grid draft-grid">${list.map((c,index)=>chipCard(c,index===1?1:0,{index,elapsed:t,disabled:!mine})).join('')}</div>`);
}
