import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import {TEAMS,type TeamId} from '../shared/teams.js';
const box=new T.BoxGeometry(1,1,1),materials=new Map<TeamId,T.MeshBasicMaterial>();
/** Unlit team bands remain readable in darkness and when cosmetic effects are disabled. */
export function teamArmor(r:CharacterRig,team?:TeamId){
 if(r.group.userData.identityTeam===team)return;
 r.group.traverse(o=>{if(o.name==='team-identity')o.visible=false;});
 r.group.userData.identityTeam=team;if(!team)return;
 let material=materials.get(team);if(!material){material=new T.MeshBasicMaterial({color:TEAMS[team].color,toneMapped:false});materials.set(team,material);}
 const band=(parent:T.Object3D,pos:number[],size:number[])=>{let m=parent.children.find(o=>o.name==='team-identity') as T.Mesh|undefined;if(!m){m=new T.Mesh(box,material);m.name='team-identity';parent.add(m);}m.visible=true;m.material=material;m.position.fromArray(pos);m.scale.fromArray(size);};
 band(r.chest,[0,.22,.165],[.45,.09,.02]);band(r.spine,[0,.12,-.17],[.36,.16,.03]);
 band(r.leftArm,[0,-.12,0],[.17,.075,.19]);band(r.rightArm,[0,-.12,0],[.17,.075,.19]);
}
