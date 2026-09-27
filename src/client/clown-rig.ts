import {createCharacterRig,rigBox} from './character-rig.js';
export function createClownRig(){
 const clown=createCharacterRig('#e94967',0xf3d777);clown.group.scale.setScalar(1.2);
 rigBox(clown.head,'red-nose',0,.14,.22,.13,.12,.14,0xff354f);rigBox(clown.head,'left-eye',-.08,.23,.165,.055,.07,.025,0x181820);rigBox(clown.head,'right-eye',.08,.23,.165,.055,.07,.025,0x181820);rigBox(clown.head,'grin',0,.06,.18,.2,.045,.03,0xfbfcff);
 for(const side of [-1,1])rigBox(clown.head,'hair',side*.22,.25,0,.17,.3,.3,0xfc445e);
 rigBox(clown.head,'hat',0,.47,0,.25,.24,.23,0x734aea);rigBox(clown.leftFoot,'shoe',0,-.01,.16,.28,.16,.43,0xff385b);rigBox(clown.rightFoot,'shoe',0,-.01,.16,.28,.16,.43,0xff385b);
 return clown;
}
