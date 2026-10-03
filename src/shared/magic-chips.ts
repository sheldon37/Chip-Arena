// Magic chips (design §8); a leaf module. 12 `spellcraft` chips (non-DEV magic) and 4 school chips (`spell-<school>`,
// mechanics in magic-abilities.ts). Effects are [stat, value, op = mul]; card lines derive from them or explicit `stats`.
import type {Chip,ChipStatLine,Rarity,Stats} from './content.js';
const good=(text:string):ChipStatLine=>({text,tone:'good'}),note=(text:string):ChipStatLine=>({text,tone:'neutral'});
/** The name is the id in title case (`rune-clock` → Rune Clock). */
const chip=(id:string,rarity:Rarity,max:number,description:string,effects:[keyof Stats,number,('add'|'set')?][],more:Partial<Chip>={}):Chip=>({id,name:id.replace(/(^|-)(.)/g,(_,s,c)=>(s&&' ')+c.toUpperCase()),rarity,max,description,tags:['spellcraft'],icon:'✦',prose:true,effects:effects.map(([stat,value,op='mul'as const])=>({stat,op,value})),...more});
const school=(id:string,rarity:Rarity,spec:string,description:string,stats:ChipStatLine[])=>chip(id,rarity,1,description,[],{tags:['spell-'+spec],mechanic:id,stats});
export const MAGIC_CHIPS:Chip[]=[
chip('rune-clock','common',4,'Etched timing runes bring your Q and E spells back sooner.',[['abilityCooldown',.9]]),
chip('spellweave','common',4,'Extra force woven into every Q and E spell.',[['abilityPower',1.1]]),
chip('wide-sigils','uncommon',3,'Broader circles: spell areas and blasts reach farther, a little thinner.',[['abilityRadius',1.15],['abilityPower',.95]]),
chip('thrifty-runes','uncommon',3,'Q and E spells cost less mana.',[['abilityCost',.85]]),
chip('swift-orbs','uncommon',3,'Your spell bolts fly faster and flatter.',[['projectileSpeed',1.18],['projectileGravity',.8]]),
chip('spellblade','rare',2,'After you cast your Q spell, your next bolts within 3 seconds (about 0.6 s of casting) hit harder.',[['qEmpower',.35,'add']]),
chip('mana-surge','rare',2,'Damage dealt by your Q and E spells flows back as mana.',[['manaSurge',.3,'add']]),
chip('blood-rune','rare',2,'Your Q and E spells heal you for part of the damage they deal.',[['abilityLeech',.2,'add']]),
chip('twin-sigil','epic',1,'Your E spell holds two charges. Each charge recharges a little slower. Cannot be combined with Spell Echo.',[['abilityCharges',1,'add'],['abilityCooldown',1.15]],{excludes:['spell-echo']}),
chip('arcane-overflow','epic',1,'Spells cast with 75% mana or more strike much harder.',[['arcaneSurge',.4,'add'],['maxMana',20,'add']]),
chip('elemental-attunement','legendary',1,'Become one with your element: stronger, faster, wider spells at a higher mana price.',[['abilityPower',1.25],['abilityCooldown',.8],['abilityRadius',1.1],['resourceCost',1.15]]),
chip('spell-echo','mythic',1,'Your E spell echoes: a second cast follows 0.8 seconds later at half power. Cannot be combined with Twin Sigil.',[['spellEcho',1,'set']],{excludes:['twin-sigil'],stats:[good('E repeats after 0.8 s'),note('Echo deals 50% damage')]}),
school('cinder-field','rare','fire','Meteor Strike leaves burning ground that lasts longer and spreads wider.',[good('+50% Meteor burn duration and radius')]),
school('undertow','rare','water','Tidal Wave drags enemies toward you instead of pushing them away.',[note('Tidal Wave pulls instead of pushes')]),
school('storm-recall','epic','lightning','Each enemy struck by Blink Strike refunds part of its cooldown.',[good('-2 s Blink cooldown per hit, up to 4 s')]),
school('abyssal-core','epic','darkness','Your Black Hole grows larger and its collapse hits harder.',[good('+25% Black Hole radius'),good('+20% collapse damage')]),
];
