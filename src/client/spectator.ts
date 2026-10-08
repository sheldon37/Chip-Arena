import type {Snapshot} from '../shared/simulation.js';
/** Camera-only selection. This never changes the local identity or submits target input. */
export class Spectator {
 enabled=false;target:string|undefined;
 candidates(state:Snapshot,id:string){return state.players.filter(p=>p.id!==id&&p.connected&&p.hp>0);}
 update(state:Snapshot,id:string){const self=state.players.find(p=>p.id===id);if(!self||self.hp>0||!['fight','result'].includes(state.phase)){this.enabled=false;this.target=undefined;return undefined;}if(!this.enabled)return undefined;const alive=this.candidates(state,id);if(!alive.some(p=>p.id===this.target))this.target=(alive.find(p=>state.options.gameMode==='teams'&&p.team===self.team)||alive[0])?.id;return alive.find(p=>p.id===this.target);}
 cycle(state:Snapshot,id:string,direction=1){this.enabled=true;this.update(state,id);const alive=this.candidates(state,id);if(!this.enabled||!alive.length)return;const index=alive.findIndex(p=>p.id===this.target);this.target=alive[(index+direction+alive.length)%alive.length].id;}
}
