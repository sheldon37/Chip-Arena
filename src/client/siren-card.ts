/**
 * Siren Chip art: the boss's blank eyes and jagged grin, black flame licking up the package, and a glitch. Original
 * vector shapes drawn for Chip Arena's Siren; no external assets. Motion lives in chip-cards.css.
 */
const EYES='<path class="siren-eye" d="M32 36 L86 48 C74 58 46 58 32 36 Z"/><path class="siren-eye" d="M168 36 L114 48 C126 58 154 58 168 36 Z"/>';
const GRIN='<path class="siren-grin" d="M22 70 C70 86 130 86 178 70 C140 116 60 116 22 70 Z"/><path class="siren-teeth" d="M28 70.9 L37 82.7 L46 76.1 L55 93.2 L64 79.8 L73 100.2 L82 82.1 L91 103.8 L100 82.8 L109 103.8 L118 82.1 L127 100.2 L136 79.8 L145 93.2 L154 76.1 L163 82.7 L172 70.9"/>';
/** The face: two slanted white eyes over a wide grin of interlocking teeth. */
export const sirenFace=(cls='siren-face')=>`<svg class="${cls}" viewBox="0 0 200 120" aria-hidden="true"><g class="siren-eyes">${EYES}</g><g class="siren-mouth">${GRIN}</g></svg>`;

const RIGHT='M-10 0 C-14 -12 -9 -22 -5 -32 C-2 -40 -3 -50 0 -60 C7 -50 10 -42 8 -32 C12 -36 13 -42 12 -47 C19 -36 19 -16 10 0 Z';
const LEFT='M10 0 C14 -12 9 -22 5 -32 C2 -40 3 -50 0 -60 C-7 -50 -10 -42 -8 -32 C-12 -36 -13 -42 -12 -47 C-19 -36 -19 -16 -10 0 Z';
/** A row of black flame tongues standing on the bottom edge of a viewBox `width` wide and `height` tall. */
export function flames(width:number,height:number,front:number[][],back:number[][],core='.84 .86'){
 const tongue=(layer:string)=>([x,s]:number[],n:number)=>{const d=n%2?LEFT:RIGHT;return `<g transform="translate(${x} ${height}) scale(${s})"><g class="siren-tongue ${layer} t${n%7}"><path class="rim" d="${d}"/>${layer==='front'?`<path class="core" transform="scale(${core})" d="${d}"/>`:''}</g></g>`;};
 return `<svg class="siren-flames" viewBox="0 0 ${width} ${height}" preserveAspectRatio="xMidYMax slice" aria-hidden="true">${back.map(tongue('back')).join('')}${front.map(tongue('front')).join('')}</svg>`;
}
const CARD_FRONT=[[10,.62],[38,.9],[66,.7],[94,1.05],[124,.78],[152,.95],[182,.68]],CARD_BACK=[[0,.5],[24,.6],[52,.48],[80,.66],[110,.55],[138,.62],[166,.5],[196,.58]];

/** Card layer: the grin watching from the fire, the flames, scanlines and the glitch tear. */
export function sirenAura(){
 return `<span class="siren-aura" aria-hidden="true">${flames(200,70,CARD_FRONT,CARD_BACK)}${sirenFace()}<span class="siren-scan"></span><span class="siren-tear"></span></span>`;
}

const STAGE_FRONT=[[8,1.1],[40,1.6],[72,1.25],[104,1.85],[136,1.35],[168,2],[200,1.5],[232,1.9],[264,1.3],[296,1.75],[328,1.2],[360,1.6],[392,1.15]],STAGE_BACK=[[0,.9],[24,1.1],[56,.85],[88,1.2],[120,.95],[152,1.25],[184,1],[216,1.3],[248,.95],[280,1.2],[312,.9],[344,1.15],[376,.95],[400,1.05]];
/** Selection reveal: the room goes black, the fire climbs, and the face opens behind the Chip. */
export function sirenCinematic(){
 return `<div class="siren-cinema" aria-hidden="true">${flames(400,160,STAGE_FRONT,STAGE_BACK,'.9 .92')}${sirenFace('siren-stare')}<span class="siren-cinema-scan"></span><span class="siren-cinema-tear tear-a"></span><span class="siren-cinema-tear tear-b"></span></div>`;
}
