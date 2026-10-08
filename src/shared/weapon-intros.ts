import type {SpecId} from './config.js';
export const WEAPON_INTROS={
 'weapon-sword':{spec:'sword',name:'Flash Draw',hit:1.8,color:0xb6fff0},
 'weapon-daggers':{spec:'daggers',name:'Twin Phantom',hit:2.12,color:0x87ffd5},
 'weapon-greatsword':{spec:'greatsword',name:'Colossus Oath',hit:1.8,color:0xffcb87},
 'weapon-shield':{spec:'shield',name:'Bastion Drop',hit:1.45,color:0x8cceff},
 'weapon-pistol':{spec:'pistol',name:'Deadeye',hit:2.12,color:0xffd99f},
 'weapon-rifle':{spec:'rifle',name:'Longwatch',hit:1.8,color:0xace1ff},
 'weapon-machinegun':{spec:'machinegun',name:'Belt Rattle',hit:2.12,color:0xffb867},
 'weapon-fire':{spec:'fire',name:'Cinder Crown',hit:1.8,color:0xff6c32},
 'weapon-water':{spec:'water',name:'Tidal Serpent',hit:2.12,color:0x63e2ff},
 'weapon-lightning':{spec:'lightning',name:'Thunderstep',hit:1.45,color:0xc6bcff},
 'weapon-darkness':{spec:'darkness',name:'Eventide Gate',hit:2.12,color:0xaf6eff},
} as const;
export type WeaponIntro=keyof typeof WEAPON_INTROS;
export const isWeaponIntro=(style:string):style is WeaponIntro=>Object.hasOwn(WEAPON_INTROS,style);
export const weaponIntroFor=(spec:SpecId):WeaponIntro|undefined=>Object.entries(WEAPON_INTROS).find(([,value])=>value.spec===spec)?.[0] as WeaponIntro|undefined;
