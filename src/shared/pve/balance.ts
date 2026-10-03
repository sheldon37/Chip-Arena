// Every PvE tunable in one place (design §10, with the owner decisions of 2026-10-02). Frozen layout; S7 may retune values.
// `h` is the party size: connected humans plus party bots (when PVE.botsCountForScaling).
import type {AddKind,DropTier,HordeModifiers,OrdinaryRarity} from './contract.js';
import type {Rarity} from '../content.js';

// §10.1 Mode and stages
export const PVE={maxParty:4, maxPartyBots:3, botsCountForScaling:true, wavesBeforeBoss:3,
 openingSeconds:10, openingEarlySeconds:3, openingMinSeconds:4,
 intermissionSeconds:12, lastIntermissionSeconds:8, waveClearRemainder:2, waveClearHold:3, waveMaxSeconds:150,
 outroVictorySeconds:4, outroDefeatSeconds:3, partyFactorPerMember:.6, bossIntroAlways:true, revealHoldsCombat:true,
 reveal:{smile:1.4, card:3.2, settle:.4}, bossIntroExtra:1.8,
 respawnOnWaveClear:true, respawnOnPhase:true, respawnHpFraction:.5, respawnClearRadius:4, reviveShield:1.5,
 soloSelfRevives:1, selfReviveDelay:5, maxRoomsPerServer:2} as const;
export const partyFactor=(h:number)=>1+PVE.partyFactorPerMember*(Math.max(1,h)-1);

// §10.2 Adds
export interface AddKindStats{hp:number;damage:number;attack:'swipe'|'lunge'|'smash';walk:number;run:number;reach:number;startRange:number;
 hit:number;cycle:number;lunge:number;impulse:number;reward:number;limbFrac:number;headFrac:number;stagger:number;smashRadius?:number}
export const ADD_KINDS:Record<AddKind,AddKindStats>={
 shambler:{hp:60,  damage:6,  attack:'swipe', walk:1.5, run:3.4, reach:1.75, startRange:1.45, hit:.34, cycle:1.05, lunge:.45, impulse:90,  reward:10, limbFrac:.35, headFrac:.4, stagger:.45},
 runner:  {hp:38,  damage:4,  attack:'lunge', walk:2.6, run:4.6, reach:2.0,  startRange:2.2,  hit:.34, cycle:.85,  lunge:.9,  impulse:60,  reward:8,  limbFrac:.35, headFrac:.4, stagger:.45},
 brute:   {hp:240, damage:15, attack:'smash', walk:1.3, run:2.4, reach:2.1,  startRange:1.7,  hit:.43, cycle:1.7,  lunge:.3,  impulse:260, reward:35, limbFrac:.45, headFrac:.5, stagger:.25, smashRadius:1.6},
};
export const GIB={headMultiplier:1.5, limbMultiplier:.9, blastTorsoShare:.6, crawlSpeed:.35, limpSpeed:.55,
 oneArmDamage:.65, biteDamage:.35, biteReach:1.2, crawlDamage:.6, crawlReach:1.3, staggerHitFraction:.25, staggerImpulse:400} as const;
export const HORDE={maxAlive:40, maxAliveRelay:28, maxEntering:16, maxCorpses:16, corpseSeconds:3.1, thinkInterval:.75,
 separation:.9, separationPush:2, stopDistance:1.1, frenzyRange:7, extDecay:6, extMax:6,
 hitEventsPerSecond:20, aoeEventInterval:.25,
 collider:{halfHeight:.43, radius:.42, mass:40, crawlHalfHeight:.12, crawlRadius:.32},
 entry:{gap:{vent:.8, door:.7, cell:.7}, closeAfter:1.2, queue:3, minPlayerDistance:5, doorWeightWaves:1.2, ventWeightPossession:1.5},
 // open/walk/climb durations live in the contract's PVE_TIMING (shared with S5 and S6)
 ventTravel:{enabled:true, minPathDistance:28, nearVent:2.0, chance:.25, cooldown:20, hidden:1.5, maxConcurrent:2},  // dive: PVE_TIMING.vent.dive
 assist:{range:20, retarget:.5, stop:1.25, reach:1.8, interval:.9, damage:{zombie:12, toy:8}, impulse:120},   // D38
 nav:{cell:1, partyRadius:.42, bossRadius:.63, breakablePoll:.5}} as const;

// §10.3 Waves and scaling
export interface WaveSpec{count:number;rate:number;maxAlive:number;mix:Partial<Record<AddKind,number>>}
export const WAVES:readonly WaveSpec[]=[
 {count:22, rate:.5, maxAlive:10, mix:{shambler:1}},
 {count:28, rate:.6, maxAlive:14, mix:{shambler:.7, runner:.3}},
 {count:34, rate:.7, maxAlive:18, mix:{shambler:.55, runner:.3, brute:.15}},
];   // S7 retune (balance harness): longer, thinner waves (design 14/18/22 at 1.2/1.5/1.8 per s cleared in ~30 s)     // budget=round(count·partyFactor(h)·mods.count); rate·(1+.35(h−1)); maxAlive+5(h−1) ≤ HORDE cap
export const MIX_BOSS:Record<1|2|3|4,Partial<Record<AddKind,number>>>={1:{shambler:.6,runner:.3,brute:.1}, 2:{shambler:.5,runner:.4,brute:.1},
 3:{shambler:.5,runner:.3,brute:.2}, 4:{shambler:.45,runner:.35,brute:.2}};
export const waveScale=(w:number):HordeModifiers=>({hp:1+.12*(w-1), damage:1+.06*(w-1), speed:1, attackRate:1, reward:1, count:1});
export const bossStageScale=()=>waveScale(PVE.wavesBeforeBoss);
export const IDENTITY_MODS:HordeModifiers={hp:1,damage:1,speed:1,attackRate:1,reward:1,count:1};   // sirenEmpower today
/** Field-wise product of modifier sets (identity-safe). */
export const combineMods=(...mods:Partial<HordeModifiers>[]):HordeModifiers=>mods.reduce<HordeModifiers>((a,m)=>({hp:a.hp*(m.hp??1),damage:a.damage*(m.damage??1),
 speed:a.speed*(m.speed??1),attackRate:a.attackRate*(m.attackRate??1),reward:a.reward*(m.reward??1),count:a.count*(m.count??1)}),{...IDENTITY_MODS});

// §10.4 Siren. Owner decision D35 (2026-10-02): no Last Laugh floor in phase 4 (minRecast.laugh = 0); the concurrency caps stay.
export const PVE_SIREN={hpBase:6000, hpPerExtraMember:.5, damageScale:[.2,.22,.24,.26], difficulty:['normal','hard','hard','hard'],
 thresholds:[.75,.5,.25], spawn:{x:0,z:24.2,yaw:Math.PI}, spawnClear:4,
 trickle:{alive:6, alivePerMember:4, rate:.6, ratePerMember:.4},          // 6/10/14/18 alive; .6/1.0/1.4/1.8 per s
 laughCrush:.6,                                                           // × victim max HP × damageScale(h)  (D35)
 possession:{rate:1.8, alive:1.6, hpScale:3, keepAway:[8,14], stareEvery:6, stareFor:.8, forceRevealAfter:60,
             noVentFor:10, hints:{medium:15, strong:35, pingEvery:8, pingFor:1.2, glyphRange:25}},
 beautiful:{scale:1.5, reach:1.5, growSeconds:1},                         // laugh lunge range not scaled (D35)
 play:{cooldown:0, maxRains:2, maxEruptions:4, minRecast:{laugh:0}}} as const;
export const bossHp=(h:number)=>Math.round(PVE_SIREN.hpBase*(1+PVE_SIREN.hpPerExtraMember*(Math.max(1,h)-1)));
const memberIndex=(h:number)=>Math.max(0,Math.min(3,Math.floor(Math.max(1,h))-1));
export const damageScale=(h:number)=>PVE_SIREN.damageScale[memberIndex(h)];
export const bossDifficulty=(h:number)=>PVE_SIREN.difficulty[memberIndex(h)] as 'normal'|'hard';

// §10.5 Economy, prices, drops, recruits
export const ECONOMY={startMoney:50, headBonus:5, assistShare:.4, assistMinFraction:.2, recruitKillShare:.5,
 waveClearBase:40, waveClearPerWave:20, bossThresholdBonus:100, possessedKill:150, possessedOthers:50, bossPerPercent:4} as const;
export const PRICES={chipBase:150, chipStep:25, chipMax:300, recruit:250, heal:60, healFraction:.5, healSeconds:2,
 healCooldown:12, reviveBase:100, reviveStep:50, reviveMax:300, reviveSeconds:2.5, reviveRadius:2.2,
 reviveHpFraction:.5, interactRadius:2.2, grabRadius:1.6, dispenseDelay:.6, recruitDelay:.8} as const;
export const DROP_ODDS:Record<DropTier,number>={common:38, uncommon:27, rare:18, epic:10, legendary:5, mythic:1.5, rainbow:.5};
/** §4.4: offer-tier weights per drop tier, passed as offerChips(…,{weights}) (replaces RARITIES weights; missing tiers weigh 0).
 *  The common row equals the ordinary draft weights and is also used for start and party-bot wave picks. */
export const OFFER_WEIGHTS:Record<DropTier,Partial<Record<Rarity,number>>>={
 common:   {common:42, uncommon:28, rare:17, epic:8,  legendary:4,  mythic:1},
 uncommon: {common:30, uncommon:30, rare:20, epic:11, legendary:6,  mythic:3},
 rare:     {common:18, uncommon:26, rare:26, epic:16, legendary:9,  mythic:5},
 epic:     {common:8,  uncommon:18, rare:28, epic:24, legendary:14, mythic:8},
 legendary:{common:4,  uncommon:10, rare:22, epic:28, legendary:22, mythic:14},
 mythic:   {common:0,  uncommon:4,  rare:14, epic:28, legendary:30, mythic:24},
 rainbow:  {mythic:1},
};
export const ORDINARY_TIERS:readonly OrdinaryRarity[]=['common','uncommon','rare','epic','legendary','mythic'];
export const PICKS={rainbowCount:8, maxUngrabbed:3, autoOpenOnGrab:true, autoOpenSafeRadius:6, moveWhileChoosing:false,
 botThink:[2,6]} as const;
export const RECRUIT={price:250, perPlayer:2, maxTotal:3, roles:{dps:.4,tank:.3,medic:.3}, hp:{dps:100,tank:250,medic:80},
 dpsClasses:['ranged','melee'], tankDamageTaken:.7, tauntRadius:8, tauntScore:6, leash:12, removeAfter:2.5,
 medic:{range:9, rate:6, pulse:.5, falloff:.5, capFraction:.04, underFireScale:.6, underFireSeconds:1,
        focus:'hirer', switchBelow:.35, hirerAbove:.7}} as const;
export const chipPrice=(bought:number)=>Math.min(PRICES.chipMax,PRICES.chipBase+PRICES.chipStep*bought);
export const revivePrice=(revived:number)=>Math.min(PRICES.reviveMax,PRICES.reviveBase+PRICES.reviveStep*revived);
