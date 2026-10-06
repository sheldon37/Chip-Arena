// Double-tap W sprint (owner 2026-10-04): two fresh W presses within SPRINT.doubleTapMs latch sprint until W is
// released (or the keys are cleared). The client sends a held `sprint` flag; the shared movement rule does the rest.
import {SPRINT} from '../shared/movement.js';

export class SprintTap{
 private tapAt=-Infinity;private on=false;
 /** A fresh (non-repeat) W keydown at `now` ms. */
 press(now:number){this.on=now-this.tapAt<=SPRINT.doubleTapMs;this.tapAt=this.on?-Infinity:now;}
 /** W keyup. */
 release(){this.on=false;}
 /** Board, pause, blur, pointer-lock loss: forget the latch and any pending first tap. */
 clear(){this.on=false;this.tapAt=-Infinity;}
 /** Whether to send sprint this input sample; W no longer held ends it (covers every keys.clear()). */
 active(held:boolean){if(!held)this.on=false;return this.on;}
}
