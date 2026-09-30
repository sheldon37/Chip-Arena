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
 * Nothing here runs per frame. setVoxelDetail swaps a part back to the plain box for low detail.
 */
export const VOXEL=.025;

/** [x,y,z, width,height,depth, shade]: metres around the part centre; shade multiplies the material colour (1 = as is). */
export type VoxelBox=[number,number,number,number,number,number,number?];
export type VoxelLayout={key:string;count:number;matrix:T.InstancedBufferAttribute;color:T.InstancedBufferAttribute};

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

/** Turns a mesh of the shared cube into a voxel part (same name, material, transform and geometry). */
export function voxelMesh(geometry:T.BufferGeometry,material:T.Material,layout:VoxelLayout){
 const mesh=new T.InstancedMesh(geometry,material,0);mesh.userData.voxel=layout;useLayout(mesh,layout);return mesh;
}
function useLayout(mesh:T.InstancedMesh,layout:VoxelLayout){
 if(mesh.instanceMatrix===layout.matrix)return;
 mesh.instanceMatrix=layout.matrix;mesh.instanceColor=layout.color;mesh.count=layout.count;mesh.boundingBox=null;mesh.boundingSphere=null;
}
/** Low detail (SVG compatibility, 'performance' graphics): the part becomes its plain box again. */
export function setVoxelDetail(mesh:T.Object3D,low:boolean){
 const layout=mesh.userData.voxel as VoxelLayout|undefined;
 if(layout&&mesh instanceof T.InstancedMesh)useLayout(mesh,low?PLAIN_LAYOUT:layout);
}
