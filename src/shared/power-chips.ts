// Damage and health Chips (owner request 2026-10-03: "We need more damage chips + health chips"). Every entry is a plain
// stat Chip: its effects run through statsFor like the rest of the catalogue, so the authoritative engine consumes them
// (damage, hp, damageTaken, lifeSteal, regeneration, critical, execute, closeDamage, farDamage, backstab, ...).
// Trade-off Chips carry negative effects; the chip card prints positives in green and negatives in red (chip-stats.ts).
import type {Chip,Stats} from './content.js';
type Effect=Chip['effects'][number];
const e=(stat:keyof Stats,op:Effect['op'],value:number):Effect=>({stat,op,value});

/** Damage Chips: flat and per-class multipliers, range and finisher bonuses, crits, and glass-cannon trade-offs. */
export const DAMAGE_CHIPS:Chip[]=[
 {id:'power-cell',rarity:'common',name:'Power Cell',description:'A small, steady boost to every hit.',tags:['all'],max:6,icon:'◆',effects:[e('damage','mul',1.08)]},
 {id:'gunsmith',rarity:'common',name:'Gunsmith Tuning',description:'Tuned barrels hit harder but take longer to reload.',tags:['ranged'],max:5,icon:'◆',effects:[e('damage','mul',1.12),e('reloadSpeed','mul',1.06)]},
 {id:'arcane-lens',rarity:'common',name:'Arcane Lens',description:'Focused spells hit harder and cost a little more mana.',tags:['magic'],max:5,icon:'◆',effects:[e('damage','mul',1.12),e('resourceCost','mul',1.08)]},
 {id:'whetstone',rarity:'common',name:'Whetstone',description:'Sharpened blades cut deeper but cost a little more stamina.',tags:['melee'],max:5,icon:'◆',effects:[e('damage','mul',1.12),e('resourceCost','mul',1.06)]},
 {id:'point-blank',rarity:'uncommon',name:'Point Blank',description:'Gun hits on enemies within 5 m deal extra damage.',tags:['ranged'],max:3,icon:'⌖',effects:[e('closeDamage','add',.3)]},
 {id:'far-sight',rarity:'uncommon',name:'Far Sight',description:'Spells that land 20 m away or farther deal extra damage.',tags:['magic'],max:3,icon:'⌖',effects:[e('farDamage','add',.3)]},
 {id:'adrenaline',rarity:'uncommon',name:'Adrenaline Gland',description:'Hit harder by running your body hot.',tags:['all'],max:3,icon:'!',effects:[e('damage','mul',1.15),e('hp','mul',.92)]},
 {id:'keen-edge',rarity:'rare',name:'Keen Edge',description:'Blades find weak points: some hits critically strike for double damage, and hits from behind cut deeper.',tags:['melee'],max:2,icon:'✦',effects:[e('critical','add',.12),e('backstab','add',.2)]},
 {id:'berserker',rarity:'rare',name:'Berserker Loop',description:'Greatly increases damage, but you take more damage too.',tags:['all'],max:2,icon:'!',effects:[e('damage','mul',1.25),e('damageTaken','mul',1.15)]},
 {id:'finisher',rarity:'rare',name:'Finisher Protocol',description:'Hits on enemies below 30% health deal extra damage.',tags:['all'],max:2,icon:'×',effects:[e('execute','add',.25)]},
 {id:'overcharge',rarity:'epic',name:'Overcharged Conduit',description:'Spells hit much harder at a much higher mana cost.',tags:['magic'],max:1,icon:'ϟ',effects:[e('damage','mul',1.45),e('resourceCost','mul',1.4)]},
 {id:'titan-edge',rarity:'epic',name:'Titan Edge',description:'A massive edge: far heavier hits, slower swings and footwork.',tags:['melee'],max:1,icon:'╱',effects:[e('damage','mul',1.4),e('attackRate','mul',.85),e('speed','mul',.95)]},
 {id:'heavy-rounds',rarity:'epic',name:'Heavy Rounds',description:'Dense slugs hit much harder, fly slower and fill less of the magazine.',tags:['ranged'],max:1,icon:'●',effects:[e('damage','mul',1.35),e('projectileSpeed','mul',.85),e('ammoCapacity','mul',.8)]},
 {id:'apex',rarity:'legendary',name:'Apex Predator',description:'Hugely increases damage and adds critical strikes, at the cost of health.',tags:['all'],max:1,icon:'✦',effects:[e('damage','mul',1.5),e('critical','add',.1),e('hp','mul',.8)]},
 {id:'annihilator',rarity:'legendary',name:'Annihilator Core',description:'Massively increases damage. Slower attacks and a fragile frame.',tags:['all'],max:1,icon:'✺',effects:[e('damage','mul',1.75),e('attackRate','mul',.85),e('damageTaken','mul',1.2)]},
];

/** Health Chips: flat and percent maximum health, regeneration, life steal, damage reduction and tank trade-offs. */
export const HEALTH_CHIPS:Chip[]=[
 {id:'stout',rarity:'common',name:'Stout Plating',description:'Bolted-on plating adds health and a little weight.',tags:['all'],max:8,icon:'+',effects:[e('hp','add',35),e('speed','mul',.97)]},
 {id:'mender',rarity:'common',name:'Mender Cell',description:'Slowly recover health after 3 s without taking damage.',tags:['all'],max:4,icon:'+',effects:[e('regeneration','add',1.5),e('hp','add',5)]},
 {id:'iron-will',rarity:'common',name:'Iron Will',description:'Shrug off a little of every hit.',tags:['all'],max:5,icon:'▣',effects:[e('damageTaken','mul',.95)]},
 {id:'heart-cell',rarity:'uncommon',name:'Heart Cell',description:'A second pump adds a share of your maximum health.',tags:['all'],max:4,icon:'♥',effects:[e('hp','mul',1.12)]},
 {id:'hardened',rarity:'uncommon',name:'Hardened Shell',description:'Take less damage, hit slightly softer.',tags:['all'],max:3,icon:'▣',effects:[e('damageTaken','mul',.92),e('damage','mul',.96)]},
 {id:'vampiric-rounds',rarity:'uncommon',name:'Vampiric Rounds',description:'Gun hits steal health.',tags:['ranged'],max:2,icon:'♥',effects:[e('lifeSteal','add',.08)]},
 {id:'nanite-mesh',rarity:'rare',name:'Nanite Mesh',description:'Nanites add health and knit wounds after 3 s without taking damage.',tags:['all'],max:3,icon:'+',effects:[e('regeneration','add',3),e('hp','add',10)]},
 {id:'warden',rarity:'rare',name:'Warden Core',description:'Front-line bulk for blade fighters.',tags:['melee'],max:2,icon:'▣',effects:[e('hp','add',45),e('speed','mul',.94)]},
 {id:'arcane-ward',rarity:'rare',name:'Arcane Ward',description:'A standing ward adds health and mana.',tags:['magic'],max:2,icon:'◈',effects:[e('hp','add',30),e('maxMana','add',20)]},
 {id:'giant-blood',rarity:'rare',name:'Giant Blood',description:'Adds a large amount of health but slows you down.',tags:['all'],max:2,icon:'♥',effects:[e('hp','mul',1.25),e('speed','mul',.93)]},
 {id:'blood-pact',rarity:'epic',name:'Blood Pact',description:'Every hit steals health; your frame is weaker.',tags:['all'],max:1,icon:'♥',effects:[e('lifeSteal','add',.2),e('hp','mul',.85)]},
 {id:'troll-blood',rarity:'epic',name:'Troll Blood',description:'Fast regeneration after 3 s without taking damage, plus extra health.',tags:['all'],max:1,icon:'+',effects:[e('regeneration','add',6),e('hp','add',25),e('attackRate','mul',.94)]},
 {id:'colossus-heart',rarity:'legendary',name:'Colossus Heart',description:'Adds a huge amount of health.',tags:['all'],max:1,icon:'♥',effects:[e('hp','mul',2),e('speed','mul',.88),e('damage','mul',.88)]},
 {id:'phoenix-core',rarity:'legendary',name:'Phoenix Core',description:'Burning life: much more health, rapid regeneration and life steal.',tags:['all'],max:1,icon:'☄',effects:[e('hp','mul',1.4),e('regeneration','add',6),e('lifeSteal','add',.1),e('damage','mul',.9)]},
];

export const POWER_CHIPS:Chip[]=[...DAMAGE_CHIPS,...HEALTH_CHIPS];
