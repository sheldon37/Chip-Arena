/** Chip draft choreography shared by the 3D stage and the DOM overlay. Every time is seconds after the authoritative
 * `draft.chosenAt`, so the picker, other players, bots and timeouts all see the same grab. The whole sequence must fit
 * inside the shortest server reveal (DRAFT_REVEAL_SECONDS, 3.2 s); the details spotlight fills the rest. */
export const DRAFT_GRAB={
 /** Head snaps to the chosen chip; the others start to shatter. */ look:.14,
 /** The card is drawn out of its slot towards the reaching hand. */ fly:.10,
 /** The hand closes on the chip: the card is gone and the 3D chip sits in the palm. */ contact:.48,
 /** Chip pulled up in front of the chest; the details spotlight begins. */ details:.85,
} as const;
const smooth=(x:number)=>{x=Math.min(1,Math.max(0,x));return x*x*(3-2*x);};
/** Weights for one moment of the grab (e: seconds after chosenAt; negative while a snapshot lags is treated as 0). */
export function grabTimeline(e:number){
 e=Math.max(0,e);
 return {
  e,
  reach:smooth((e-DRAFT_GRAB.look)/(DRAFT_GRAB.contact-DRAFT_GRAB.look)),
  fly:smooth((e-DRAFT_GRAB.fly)/(DRAFT_GRAB.contact-DRAFT_GRAB.fly)),
  fade:smooth((e-(DRAFT_GRAB.contact-.08))/.08),
  contact:e>=DRAFT_GRAB.contact,
  pull:smooth((e-DRAFT_GRAB.contact)/(DRAFT_GRAB.details-DRAFT_GRAB.contact)),
  details:e>=DRAFT_GRAB.details,
 };
}
/** Offers are laid out horizontally: one row while they fit (up to six), otherwise two rows with the extra card on
 * top. `perRow` is how many compact cards fit across the screen. */
export function draftRows(count:number,perRow=6):number[]{
 const n=Math.max(0,Math.floor(count)),fit=perRow===Infinity?Infinity:Math.max(1,Math.min(6,Math.floor(perRow)));if(n<=fit)return n?[n]:[];
 const top=Math.ceil(n/2);return [top,n-top];
}
/** Compact cards that fit across a viewport: 150 px minimum, 14 px gaps, 28 px side padding. Phones and short
 * landscape windows scroll one row instead, so the pilot stays visible. */
export function draftPerRow(width:number,height=720){return width<=760||height<=480?Infinity:Math.max(2,Math.min(6,Math.floor((width-56+14)/(150+14))));}
