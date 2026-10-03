// Magic chips (design §8), spread after POWER_CHIPS in content.ts. Leaf module (type imports only).
// 12 ability chips (tag `spellcraft`: non-DEV magic) and 4 school chips (tag `spell-<school>`) whose mechanics live in
// magic-abilities.ts. Cards print `description` as written (`prose`) plus lines derived from `effects` or explicit `stats`.
import type {Chip,ChipStatLine,Rarity,Stats} from './content.js';
const line=(tone:ChipStatLine['tone'])=>(text:string):ChipStatLine=>({text,tone}),good=line('good'),bad=line('bad'),note=line('neutral');
/** The name is the id in title case (`rune-clock` → Rune Clock). */
const chip=(id:string,rarity:Rarity,max:number,description:string,effects:[keyof Stats,'add'|'mul'|'set',number][],more:Partial<Chip>={}):Chip=>({id,name:id.replace(/(^|-)(.)/g,(_,s,c)=>(s&&' ')+c.toUpperCase()),rarity,max,description,tags:['spellcraft'],icon:'✦',prose:true,effects:effects.map(([stat,op,value])=>({stat,op,value})),...more});
const school=(id:string,rarity:Rarity,spec:string,description:string,stats:ChipStatLine[])=>chip(id,rarity,1,description,[],{tags:['spell-'+spec],mechanic:id,stats});
export const MAGIC_CHIPS:Chip[]=[
chip('rune-clock','common',4,'Etched timing runes bring your Q and E spells back sooner.',[['abilityCooldown','mul',.9]]),
chip('spellweave','common',4,'Extra force woven into every Q and E spell.',[['abilityPower','mul',1.1]]),
chip('wide-sigils','uncommon',3,'Broader circles: spell areas and blasts reach farther, a little thinner.',[['abilityRadius','mul',1.15],['abilityPower','mul',.95]]),
chip('thrifty-runes','uncommon',3,'Q and E spells cost less mana.',[['abilityCost','mul',.85]]),
chip('swift-orbs','uncommon',3,'Your spell bolts fly faster and flatter.',[['projectileSpeed','mul',1.18],['projectileGravity','mul',.8]]),
chip('spellblade','rare',2,'After you cast your Q spell, your next bolts within 3 seconds (about 0.6 s of casting) hit harder.',[['qEmpower','add',.35]]),
chip('mana-surge','rare',2,'Damage dealt by your Q and E spells flows back as mana.',[['manaSurge','add',.3]]),
chip('blood-rune','rare',2,'Your Q and E spells heal you for part of the damage they deal.',[['abilityLeech','add',.2]]),
chip('twin-sigil','epic',1,'Your E spell holds two charges. Each charge recharges a little slower.',[['abilityCharges','add',1],['abilityCooldown','mul',1.15]],{excludes:['spell-echo'],stats:[good('+1 E charge'),bad('+15% Ability cooldown'),note('Cannot be combined with Spell Echo')]}),
chip('arcane-overflow','epic',1,'Spells cast with 75% mana or more strike much harder.',[['arcaneSurge','add',.4],['maxMana','add',20]]),
chip('elemental-attunement','legendary',1,'Become one with your element: stronger, faster, wider spells at a higher mana price.',[['abilityPower','mul',1.25],['abilityCooldown','mul',.8],['abilityRadius','mul',1.1],['resourceCost','mul',1.15]]),
chip('spell-echo','mythic',1,'Your E spell echoes: a second cast follows 0.8 seconds later at half power.',[['spellEcho','set',1]],{excludes:['twin-sigil'],stats:[good('E repeats after 0.8 s'),note('Echo deals 50% damage'),note('Cannot be combined with Twin Sigil')]}),
school('cinder-field','rare','fire','Meteor Strike leaves burning ground that lasts longer and spreads wider.',[good('+50% Meteor burn duration and radius')]),
school('undertow','rare','water','Tidal Wave drags enemies toward you instead of pushing them away.',[note('Tidal Wave pulls instead of pushes')]),
school('storm-recall','epic','lightning','Each enemy struck by Blink Strike refunds part of its cooldown.',[good('-2 s Blink cooldown per hit, up to 4 s')]),
school('abyssal-core','epic','darkness','Your Black Hole grows larger and its collapse hits harder.',[good('+25% Black Hole radius'),good('+20% collapse damage')]),
];
