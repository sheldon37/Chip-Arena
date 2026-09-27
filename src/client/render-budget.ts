export type GraphicsMode='auto'|'high'|'performance';
/** Cap physical pixels independently of monitor DPI; adapt only after sustained slow frames. */
export class RenderBudget {
 scale=1;private elapsed=0;private frames=0;private slow=0;
 mode:GraphicsMode='auto';
 setMode(mode:GraphicsMode){this.mode=mode;this.scale=1;this.elapsed=this.frames=this.slow=0;}
 ratio(width:number,height:number,dpr:number){const pixels=this.mode==='high'?3_686_400:this.mode==='performance'?1_000_000:2_073_600;return Math.max(.4,Math.min(dpr,this.mode==='high'?1.7:1,Math.sqrt(pixels/Math.max(1,width*height)))*this.scale);}
 sample(dt:number,active:boolean){if(this.mode!=='auto'||!active||dt<=0||dt>.15){this.elapsed=this.frames=this.slow=0;return false;}this.elapsed+=dt;this.frames++;if(dt>1/45)this.slow++;if(this.elapsed<2)return false;const before=this.scale,fps=this.frames/this.elapsed;if(fps<48&&this.slow/this.frames>.25)this.scale=Math.max(.6,this.scale-.1);else if(fps>57)this.scale=Math.min(1,this.scale+.05);this.elapsed=this.frames=this.slow=0;return Math.abs(before-this.scale)>.001;}
}
