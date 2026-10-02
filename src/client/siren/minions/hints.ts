import * as T from 'three';
import type {SirenMinion} from './model.js';

/**
 * Possession hints (PvE "Find me...", design D13 and §8.3). No eyes: the owner's face rule stays (blank by default, about
 * a quarter cracked, no burned-in eyes), so the hints live on the crown and above the head.
 *  - Level 1 (0-15 s): a violet glow pulses out of the head every ~4 s: from the crack on cracked heads, from a thin
 *    seam that splits the crown on blank heads.
 *  - Level 2 (15-35 s): plus a floating, translucent Siren grin (the boss card's grin only, never its eyes) above the head,
 *    shown within 25 m. The black-flame trail is emitted by the horde view (it owns the flame field).
 *  - Level 3 (35 s+): as level 2, brighter (the whisper and screen-edge arrow belong to the HUD).
 * Everything is a pure function of (level, time); the meshes are built once per minion, on first use, and hidden at 0.
 */
export const HINT_GLYPH_RANGE=25;
export const HINT_LEVELS=[0,15,35] as const;
/** Hint level from seconds since possessedAt (0 none). */
export const hintLevel=(age:number)=>!(age>=0)?0:age<HINT_LEVELS[1]?1:age<HINT_LEVELS[2]?2:3;
const VIOLET=0x9d84ff;
interface Hint {seam:T.Mesh;halo:T.Sprite;glyph:T.Sprite}
const hints=new WeakMap<SirenMinion,Hint>();
let haloTex:T.Texture|null|undefined,glyphTex:T.Texture|null|undefined;
const canvas=(w:number,h:number)=>typeof document!=='undefined'?Object.assign(document.createElement('canvas'),{width:w,height:h}):null;
/** A soft violet glow (canvas radial gradient, built once; null without a DOM). */
function halo(){if(haloTex!==undefined)return haloTex;const c=canvas(64,64);if(!c)return haloTex=null;const g=c.getContext('2d')!,r=g.createRadialGradient(32,32,0,32,32,32);
 r.addColorStop(0,'rgba(220,205,255,1)');r.addColorStop(.35,'rgba(157,132,255,.55)');r.addColorStop(1,'rgba(157,132,255,0)');g.fillStyle=r;g.fillRect(0,0,64,64);
 haloTex=new T.CanvasTexture(c);haloTex.colorSpace=T.SRGBColorSpace;return haloTex;}
/** The Siren grin (siren-card.ts GRIN path: lips and interlocking teeth, no eyes), drawn once into a canvas texture. */
const GRIN_PATH='M22 70 C70 86 130 86 178 70 C140 116 60 116 22 70 Z',TEETH_PATH='M28 70.9 L37 82.7 L46 76.1 L55 93.2 L64 79.8 L73 100.2 L82 82.1 L91 103.8 L100 82.8 L109 103.8 L118 82.1 L127 100.2 L136 79.8 L145 93.2 L154 76.1 L163 82.7 L172 70.9';
function glyph(){if(glyphTex!==undefined)return glyphTex;const c=canvas(256,96);if(!c||typeof Path2D==='undefined')return glyphTex=null;const g=c.getContext('2d')!;
 g.translate(28,-52);g.scale(1,1);g.shadowColor='rgba(157,132,255,.9)';g.shadowBlur=14;g.fillStyle='rgba(10,8,14,.92)';g.fill(new Path2D(GRIN_PATH));
 g.shadowBlur=6;g.strokeStyle='rgba(244,240,255,.95)';g.lineWidth=3.2;g.lineJoin='miter';g.stroke(new Path2D(TEETH_PATH));g.strokeStyle='rgba(200,185,255,.8)';g.lineWidth=2;g.stroke(new Path2D(GRIN_PATH));
 glyphTex=new T.CanvasTexture(c);glyphTex.colorSpace=T.SRGBColorSpace;return glyphTex;}
const additive=(color:number)=>new T.MeshBasicMaterial({color,transparent:true,opacity:0,blending:T.AdditiveBlending,depthWrite:false,toneMapped:false});
function build(m:SirenMinion):Hint{
 const cracked=m.look.face==='cracked',seam=new T.Mesh(new T.BoxGeometry(cracked?.07:.24,.014,cracked?.2:.026),additive(VIOLET));
 seam.name='siren-minion-hint-seam';seam.position.set(cracked?-.03:0,.365,cracked?.03:.0);seam.castShadow=false;seam.receiveShadow=false;
 const halo_=new T.Sprite(new T.SpriteMaterial({map:halo(),color:VIOLET,transparent:true,opacity:0,blending:T.AdditiveBlending,depthWrite:false,toneMapped:false}));
 halo_.name='siren-minion-hint-halo';halo_.position.set(0,.38,0);halo_.scale.setScalar(.7);
 const glyph_=new T.Sprite(new T.SpriteMaterial({map:glyph(),color:0xffffff,transparent:true,opacity:0,depthWrite:false,toneMapped:false}));
 glyph_.name='siren-minion-hint-grin';glyph_.position.set(0,.95,0);glyph_.scale.set(.78,.29,1);
 m.rig.head.add(seam,halo_,glyph_);const h={seam,halo:halo_,glyph:glyph_};hints.set(m,h);return h;
}
/**
 * Shows the possession hints for `level` (0 hides them) at authority time `time`. `cameraDistance` limits the grin glyph
 * to HINT_GLYPH_RANGE. Pure in (level, time): no state besides the lazily built meshes.
 */
export function possessHint(m:SirenMinion,level:number,time:number,cameraDistance=0){
 let h=hints.get(m);if(!level){if(h){h.seam.visible=h.halo.visible=h.glyph.visible=false;}return;}
 h??=build(m);
 // A pulse every ~4 s (a quick swell, a slow fade), hashed per minion so two possessed adds never beat together.
 const k=((time+m.look.phase*4)%4+4)%4,pulse=k<1.4?Math.sin(Math.PI*k/1.4)**2:0,boost=level>=3?1.35:1;
 const seamOn=.25+.75*pulse;(h.seam.material as T.MeshBasicMaterial).opacity=Math.min(1,seamOn*boost);h.seam.visible=true;
 h.halo.visible=pulse>.02;h.halo.material.opacity=.8*pulse*boost;h.halo.scale.setScalar(.45+.5*pulse);
 const g=level>=2&&cameraDistance<=HINT_GLYPH_RANGE;h.glyph.visible=g;
 if(g){h.glyph.material.opacity=Math.min(1,(.42+.12*Math.sin(time*2.3))*boost);h.glyph.position.y=.95+.05*Math.sin(time*1.7);}
}
/** The hint meshes (tests and budgets): built only for a minion that was ever possessed. */
export const hintObjects=(m:SirenMinion):T.Object3D[]=>{const h=hints.get(m);return h?[h.seam,h.halo,h.glyph]:[];};
