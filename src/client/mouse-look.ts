/**
 * Pointer-lock mouse-look sanitising (client input only; the simulation never sees raw deltas, only the resulting yaw).
 *
 * Chromium's pointer lock on Windows (Electron included) derives movementX/Y from cursor warps unless raw input is in
 * use, and it occasionally reports a single bogus delta of hundreds of pixels: on the first move after a lock is
 * (re)acquired (the jump from where the cursor was to the lock centre), after focus changes, and as rare one-event
 * spikes mid-play. Applied as-is, one such event snaps the camera tens of degrees in one frame
 * (2026-10-03 playtest: "the camera will pull another direction randomly and instantly ... even in single player").
 *
 * The filter drops:
 * - large deltas right after a lock is acquired (the settle window, and the first event after a lock);
 * - a lone event far larger than both a fixed floor and the recent hand speed predicts. A real flick ramps up over
 *   several events, so a second large event in the same direction right after a dropped one is accepted (sustained
 *   motion); a large opposite pair (spike and its return) is dropped as a whole.
 * Normal and fast play never reaches the thresholds, so sensitivity and feel are unchanged for every mode.
 */
export const MOUSE_LOOK={
 /** ms after a lock is acquired in which any delta above `settleLimit` px is dropped. */
 settle:120,settleLimit:48,
 /** A single event is a spike only above this many px and above `ratio` × the delta the recent hand speed predicts. */
 spike:260,ratio:5,
 /** ms: hand-speed memory. The recent peak speed is held for `hold` (a frame hitch delivers one coalesced delta after a
  * long gap while the hand kept moving), then decays with this constant; the shortest/longest event gap considered. */
 hold:100,decay:80,minGap:1000/60,maxGap:250,
 /** ms within which a second large same-direction event confirms a dropped one as real motion. */
 confirm:60,
};

export class MouseLookFilter {
 private lockedAt=-Infinity;private lastAt=-Infinity;private rate=0;private fresh=true;
 private dropped?:{x:number;y:number;at:number};
 /** Rejected events since creation (diagnostics). */
 rejected=0;
 /** A pointer lock was (re)acquired at `now` (ms, same clock as the events' timeStamp). */
 lock(now:number){this.lockedAt=now;this.lastAt=now;this.rate=0;this.fresh=true;this.dropped=undefined;}
 /** True when this pointer-locked mousemove delta should turn the camera. */
 accept(dx:number,dy:number,now:number):boolean{
  if(!Number.isFinite(dx)||!Number.isFinite(dy)||!Number.isFinite(now)){this.rejected++;return false;}
  const m=Math.hypot(dx,dy),c=MOUSE_LOOK,elapsed=Math.max(0,now-this.lastAt),gap=Math.min(c.maxGap,Math.max(c.minGap,elapsed));
  const first=this.fresh;this.fresh=false;
  // The cursor-to-centre jump of a fresh lock (also when the first move only comes later).
  if((first||now-this.lockedAt<c.settle)&&m>c.settleLimit){this.lastAt=now;this.rejected++;return false;}
  const rate=this.rate*Math.exp(-Math.max(0,elapsed-c.hold)/c.decay),expected=rate*gap;
  if(m>c.spike&&m>c.ratio*expected){
   const d=this.dropped,confirmed=!!d&&now-d.at<=c.confirm&&d.x*dx+d.y*dy>0;
   if(!confirmed){this.dropped={x:dx,y:dy,at:now};this.lastAt=now;this.rate=rate;this.rejected++;return false;}
  }
  this.dropped=undefined;this.rate=Math.max(rate,m/gap);this.lastAt=now;return true;
 }
}
