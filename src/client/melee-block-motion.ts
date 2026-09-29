import * as T from 'three';
import {braceWeapon,type CharacterRig} from './character-rig.js';
import {foot,hand} from './primary-melee-motion.js';
import type {PlayerView} from '../shared/simulation.js';

/** Defensive silhouettes keep the actual weapon between the face/chest and the threat. */
export function meleeBlockMotion(r:CharacterRig,p:PlayerView,time:number){
 const heavy=p.spec==='greatsword',shield=p.spec==='shield',daggers=p.spec==='daggers';
 const breath=Math.sin(time*2.15)*.008,still=1-T.MathUtils.clamp(r.motion/.32,0,1);
 r.body.position.y-=heavy?.13:shield?.11:.09;
 r.body.position.z-=.035;
 r.body.rotation.x+=.09;
 r.body.rotation.y=shield?-.22:daggers?0:-.18;
 r.pelvis.rotation.y=-.10;r.spine.rotation.set(-.06,.12,0);r.chest.rotation.set(-.04+breath,.10,0);
 r.head.rotation.y=-.06;r.head.rotation.x=-p.pitch*.35-.06;
 r.leftShoulder.rotation.set(-.06,-.10,.06);r.rightShoulder.rotation.set(-.06,.10,-.06);
 // Keep walking feet live; at rest settle into a staggered, bent-knee brace.
 if(p.grounded&&still>.001){
  foot(r,true,new T.Vector3(heavy?.22:.19,-.795,.14),.16,0,still);
  foot(r,false,new T.Vector3(heavy?-.22:-.19,-.795,-.12),-.28,0,still);
 }
 if(heavy){
  // Two hands on the hilt, blade raised diagonally across the upper body.
  braceWeapon(r,new T.Vector3(-.03,.16,.24),new T.Euler(-1.10,.88,-.12),1);
 }else if(shield){
  hand(r,false,new T.Vector3(-.025,.16,.30),new T.Euler(-.10,-.08,0),1);
  hand(r,true,new T.Vector3(.25,.12,.18),new T.Euler(-.9,-.2,.25),1);
 }else if(daggers){
  // Cross the two blades in front of the chest, with the wrists kept apart.
  hand(r,false,new T.Vector3(-.20,.12,.32),new T.Euler(-.78,.70,-.2),1);
  hand(r,true,new T.Vector3(.20,.15,.34),new T.Euler(-.78,-.70,.2),1);
 }else{
  hand(r,false,new T.Vector3(-.27,.14,.31),new T.Euler(-1.05,.76,-.14),1);
  hand(r,true,new T.Vector3(.27,.17,.22),new T.Euler(-.7,-.25,.25),1);
 }
}
