import * as T from 'three';

/**
 * Voxel kit: detailed voxel parts that still count as ONE mesh of the shared unit cube.
 *
 * A part is authored as a list of boxes in metres around the part's centre (stepped bevels, layered plates, panel
 * seams, knuckles...). The list becomes the instance data of a T.InstancedMesh that uses the shared BoxGeometry cube
 * and keeps the part's name, material, position and scale. So, for every consumer, the part is exactly the old box:
 *  - one draw call and one mesh per part (rig mesh budget unchanged), shared cube geometry, shared mat() materials;
 *  - death-effects voxelBurst, ragdoll collider bounds, block-cooldown tint and the SVG compatibility renderer read the
 *    mesh as its unit cube times its scale (the SVG renderer ignores instancing and draws the plain box);
 *  - Box3.setFromObject uses the instance bounds, which stay inside the part's box unless a layout deliberately adds
 *    a proud plate.
 * Layouts are built once per key and their instance attributes are SHARED by every rig (one GPU buffer per layout).
 * Per frame there is only a material check in VoxelPart.onBeforeRender (no allocation). setVoxelDetail swaps a part back to the plain box for low detail.
 */
export const VOXEL=.025;

/** [x,y,z, width,height,depth, shade]: metres around the part centre; shade multiplies the material colour (1 = as is). */
export type VoxelBox=[number,number,number,number,number,number,number?];
export type VoxelLayout={key:string;count:number;matrix:T.InstancedBufferAttribute;color:T.InstancedBufferAttribute;
 /** Owned by one rig (wound carving, body-wounds.ts): never shared, its GPU buffers are released with releaseVoxelLayout. */
 custom?:boolean;
 /** Variants: `plain` draws in low detail (default PLAIN_LAYOUT), `ghost` under see-through materials (default plain). */
 plain?:VoxelLayout;ghost?:VoxelLayout;};

const layouts=new Map<string,VoxelLayout>(),m=new T.Matrix4(),c=new T.Color();

function pack(key:string,boxes:VoxelBox[],size:readonly number[]):VoxelLayout{
 const count=Math.max(1,boxes.length),matrix=new T.InstancedBufferAttribute(new Float32Array(count*16),16),color=new T.InstancedBufferAttribute(new Float32Array(count*3),3);
 if(!boxes.length)boxes=[[0,0,0,size[0],size[1],size[2]]];
 boxes.forEach(([x,y,z,w,h,d,shade=1],i)=>{m.makeScale(w/size[0],h/size[1],d/size[2]).setPosition(x/size[0],y/size[1],z/size[2]);m.toArray(matrix.array,i*16);c.setScalar(shade).toArray(color.array,i*3);});
 return {key,count,matrix,color};
}

/** The cached layout for a part of this size. build() runs once per key; every rig shares the result. */
export function voxelLayout(key:string,size:readonly [number,number,number],build:(out:VoxelBox[])=>void):VoxelLayout{
 const id=key+'|'+size.join(',');let layout=layouts.get(id);
 if(!layout){const out:VoxelBox[]=[];build(out);layout=pack(id,out,size);layouts.set(id,layout);}
 return layout;
}
/** The plain box: one full-size instance (low detail, and the reference for voxelBurst/ragdoll/SVG). */
export const PLAIN_LAYOUT=pack('plain',[[0,0,0,1,1,1]],[1,1,1]);
export const layoutCount=()=>layouts.size;

/** Box with stepped edges: every edge parallel to an axis listed in `edges` loses a b x b notch (the corner voxels). */
export function bevel(out:VoxelBox[],x:number,y:number,z:number,w:number,h:number,d:number,b=VOXEL,shade=1,edges='xyz'){
 const ex=edges.includes('x'),ey=edges.includes('y'),ez=edges.includes('z');
 if(!b||!(ex||ey||ez)){out.push([x,y,z,w,h,d,shade]);return out;}
 // Edges parallel to x are notched when no box covers both the y and the z extremes; likewise for y and z.
 if(ex&&ey&&ez){out.push([x,y,z,w,h-2*b,d-2*b,shade],[x,y,z,w-2*b,h,d-2*b,shade],[x,y,z,w-2*b,h-2*b,d,shade]);return out;}
 if(ex&&ey){out.push([x,y,z,w,h,d-2*b,shade],[x,y,z,w-2*b,h-2*b,d,shade]);return out;}
 if(ex&&ez){out.push([x,y,z,w,h-2*b,d,shade],[x,y,z,w-2*b,h,d-2*b,shade]);return out;}
 if(ey&&ez){out.push([x,y,z,w-2*b,h,d,shade],[x,y,z,w,h-2*b,d-2*b,shade]);return out;}
 if(ex){out.push([x,y,z,w,h-2*b,d,shade],[x,y,z,w,h,d-2*b,shade]);return out;}
 if(ey){out.push([x,y,z,w-2*b,h,d,shade],[x,y,z,w,h,d-2*b,shade]);return out;}
 out.push([x,y,z,w-2*b,h,d,shade],[x,y,z,w,h-2*b,d,shade]);return out;
}

/**
 * A voxel part. See-through use draws the plain box: when a consumer swaps in a transparent material (death-announcement
 * hologram, ghost kill cinematic, dagger phantom afterimages, soul ragdoll) the overlapping and interior faces of the
 * layout would all blend together into a near-opaque blob, and edge lines built from the cube would not match the sculpt.
 * Assigning `material` switches the layout at once (the renderer uploads instance buffers before onBeforeRender, so the
 * switch must happen before the frame). onBeforeRender is only a safety net for a `transparent` flag flipped later on
 * an already assigned material; that path may skip the part for one frame. Clones keep the class, and copy() re-resolves
 * the shared layout, so a clone never writes into a shared instance buffer.
 * The layout itself lives in a non-enumerable userData field: Object3D.copy JSON-copies userData, and a clone must not
 * carry a serialized copy of the instance arrays. Clones find the shared layout again through userData.voxelKey.
 */
const partMaterial=new WeakMap<T.Object3D,T.Material|T.Material[]>(),copying=new WeakSet<T.Object3D>();
export class VoxelPart extends T.InstancedMesh{
 constructor(geometry?:T.BufferGeometry,material?:T.Material,count=0){super(geometry,material,count);}
 override copy(source:this,recursive?:boolean){copying.add(this);try{super.copy(source,recursive);}finally{copying.delete(this);}syncPart(this);return this;}
 override onBeforeRender(){syncPart(this);}
}
Object.defineProperty(VoxelPart.prototype,'material',{configurable:true,enumerable:true,
 get(this:VoxelPart){return partMaterial.get(this);},
 set(this:VoxelPart,material:T.Material|T.Material[]){partMaterial.set(this,material);if(this.instanceMatrix&&!copying.has(this))syncPart(this);}});
const seeThrough=(material:T.Material|T.Material[]|undefined)=>!!material&&(Array.isArray(material)?material.some(m=>m.transparent):material.transparent);
function syncPart(mesh:T.InstancedMesh){const layout=partLayout(mesh);if(!layout)return;useLayout(mesh,mesh.userData.voxelLow?layout.plain||PLAIN_LAYOUT:seeThrough(mesh.material)?layout.ghost||layout.plain||PLAIN_LAYOUT:layout);}
function partLayout(mesh:T.Object3D):VoxelLayout|undefined{return (mesh.userData.voxel as VoxelLayout|undefined)||layouts.get(mesh.userData.voxelKey);}
function tag(mesh:T.Object3D,layout:VoxelLayout){Object.defineProperty(mesh.userData,'voxel',{value:layout,enumerable:false,configurable:true,writable:true});mesh.userData.voxelKey=layout.key;}

/** Turns a mesh of the shared cube into a voxel part (same name, material, transform and geometry). */
export function voxelMesh(geometry:T.BufferGeometry,material:T.Material,layout:VoxelLayout){
 const mesh=new VoxelPart(geometry,material,0);tag(mesh,layout);syncPart(mesh);return mesh;
}
/** A copy of a rig mesh with another material that keeps its voxel sculpt (ragdoll pieces): a VoxelPart for a voxel
 * part, else a plain T.Mesh, exactly like `new T.Mesh(node.geometry,material)`. */
export function copyVoxelPart(node:T.Mesh,material:T.Material|T.Material[]=node.material):T.Mesh{
 const layout=partLayout(node);if(!layout||!(node instanceof T.InstancedMesh))return new T.Mesh(node.geometry,material);
 const copy=new VoxelPart(node.geometry,material as T.Material,0);tag(copy,layout);if(node.userData.voxelLow)copy.userData.voxelLow=true;syncPart(copy);return copy;
}
function useLayout(mesh:T.InstancedMesh,layout:VoxelLayout){
 if(mesh.instanceMatrix===layout.matrix)return;
 mesh.instanceMatrix=layout.matrix;mesh.instanceColor=layout.color;mesh.count=layout.count;mesh.boundingBox=null;mesh.boundingSphere=null;
}
/**
 * Rig-owned layouts (wound carving, body-wounds.ts). A carved part keeps its mesh, name, material and transform; only its
 * instances change, so every consumer (recolouring, block tint, ragdoll copies, low detail) keeps working. Custom layouts
 * are never registered or shared between rigs; releaseVoxelLayout frees their GPU buffers.
 */
export type UnitBox={box:readonly number[];color:readonly number[]};
/** Boxes of a layout in the part's unit cube ([x,y,z,w,h,d]) with their RGB colour (a grey shade for sculpts). */
export function layoutBoxes(layout:VoxelLayout):{box:number[];color:number[]}[]{
 const out:{box:number[];color:number[]}[]=[],e=layout.matrix.array,col=layout.color.array;
 for(let i=0;i<layout.count;i++){const o=i*16;out.push({box:[e[o+12],e[o+13],e[o+14],e[o],e[o+5],e[o+10]],color:[col[i*3],col[i*3+1],col[i*3+2]]});}
 return out;
}
/** A rig-owned layout from unit-space boxes and RGB colours. */
export function customLayout(key:string,boxes:readonly UnitBox[],variants:{plain?:VoxelLayout;ghost?:VoxelLayout}={}):VoxelLayout{
 const count=boxes.length,matrix=new T.InstancedBufferAttribute(new Float32Array(Math.max(1,count)*16),16),color=new T.InstancedBufferAttribute(new Float32Array(Math.max(1,count)*3),3);
 boxes.forEach(({box:[x,y,z,w,h,d],color:[r,g,b]},i)=>{m.makeScale(w,h,d).setPosition(x,y,z);m.toArray(matrix.array,i*16);c.setRGB(r,g,b).toArray(color.array,i*3);});
 return {key,count,matrix,color,custom:true,...variants};
}
/** Draws nothing: a wound layer under see-through materials. Shared, never released. */
export const EMPTY_LAYOUT:VoxelLayout={...pack('empty',[],[1,1,1]),count:0};
const bases=new WeakMap<T.Object3D,VoxelLayout>();
/** The layout a voxel part draws now (custom or shared), and the shared sculpt it was built with. */
export const currentLayout=(mesh:T.Object3D)=>partLayout(mesh);
export const baseLayout=(mesh:T.Object3D)=>bases.get(mesh)||partLayout(mesh);
/** Installs a rig-owned layout on a voxel part, or restores the shared sculpt when `layout` is undefined. */
export function retagVoxelPart(mesh:T.InstancedMesh,layout?:VoxelLayout){
 const current=partLayout(mesh);if(!current)return;if(!bases.has(mesh)&&!current.custom)bases.set(mesh,current);
 const next=layout||bases.get(mesh);if(next){tag(mesh,next);syncPart(mesh);}
}
/** Frees the GPU buffers of a custom layout the mesh currently draws (renderer listener on InstancedMesh 'dispose').
 * Shared layouts are left alone; another mesh still drawing the same custom layout simply uploads it again. */
export function releaseVoxelLayout(mesh:T.Object3D){if(mesh instanceof T.InstancedMesh&&partLayout(mesh)?.custom)mesh.dispatchEvent({type:'dispose'});}

/** Low detail (SVG compatibility, 'performance' graphics): the part becomes its plain box again. */
export function setVoxelDetail(mesh:T.Object3D,low:boolean){
 const layout=partLayout(mesh);if(!layout||!(mesh instanceof T.InstancedMesh))return;
 if(low)mesh.userData.voxelLow=true;else delete mesh.userData.voxelLow;syncPart(mesh);
}
