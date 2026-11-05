# Weapon techniques: optional Q/E replacements for the twelve standard weapons

Owner request, 2026-11-03: "Lets add more abilities now for the weapons. Think creatively, integrate more skills that aren't too
much of a "powercreep" but still useful and fun. Ensure the animation and effects are high-quality; Make sure you evaluate
afterwards to make sure things look great."

Owner revision, 2026-11-03: techniques replace either Q or E, selected per weapon in Customize; rifle permits E only because Q is scope. Both original abilities remain the default. There is no third slot or separate technique cooldown. This revision supersedes the original key-1 proposal.

Status: playable, both sets. Astra's rules and saved choice (PRs #321, #322 and #325), Claude's Customize picker, HUD cards and presentation. Parting Line recovery is fixed on `codex/bombadier-rules`; native acceptance remains pending (see "The second set, reviewed"). Receipts are in DEV_LOG. The sandbox preview remains optional. DEV classes (Siren, Emperor, Ansatsuken, L33T, Cyber Lord) retain their existing key-1 abilities.

## Why it isn't power creep

A technique is a trade, never an extra: a weapon keeps two abilities, and swapping its technique in gives up that slot's ability. Every
weapon already has its damage on LMB, Q and E, so the trade is damage for utility. The rules of the set:

- **Utility first.** A technique changes where someone stands or what they can see or do. The most any deals is 22, under a Q's
  budget (30), and most deal none.
- **Long cooldowns.** 9 to 20 s, longer than the same weapon's Q.
- **A visible cost or a counter.** Each row below names how an opponent answers it. Nothing is an untelegraphed kill.
- **Same resources and rules.** Melee pays stamina, magic pays mana, guns pay cooldown only. The cooldown and cost chips apply as to Q
  and E. A stun, knockdown or roll refuses it as it does Q and E.

## The set

Use the selected slot's existing keyboard/controller binding: `InputFrame.AbilityQ` or `AbilityE`. The same slot performs recasts. Standard weapons ignore `SirenAbility` for techniques; DEV abilities are unchanged.

| Weapon | Technique (`id`) | What it does | Damage | Cd / cost | The counter |
| --- | --- | --- | --- | --- | --- |
| Sword | Riposte (`riposte`) | .5 s guard. A melee hit or projectile from the front in that window is negated: a melee attacker takes the counter thrust and a .35 s stun, a projectile flies back along your aim. Unused, .45 s of recovery | 22 | 12 s / 20 st | Hold your attack until the guard ends, then punish the recovery |
| Daggers | Smoke Veil (`smoke-veil`) | A pellet at your feet: a 4 m cloud for 4 s. Anyone inside is hidden from outside it (nameplates hidden, bots lose the target) | 0 | 16 s / 25 st | The cloud is huge and obvious: fire into it, or walk in |
| Shield | Bastion (`bastion`) | Plant the shield: a 2.6 m wide, 1.8 m tall wall stops projectiles from either side for everyone, for 4 s or 120 damage. Recast recalls it | 0 | 14 s / 25 st | Walk round or through it; while it stands you have no shield and cannot block |
| Greatsword | Anchor Throw (`anchor-throw`) | Hurl the blade 16 m. It sticks where it lands, or in the first enemy (18). Recast within 3 s leaps to it (.45 s), landing with a 2 m shock (10). Otherwise it flies back | 18 + 10 | 14 s / 30 st | No sword until it returns: you only punch. The blade shows where you will land |
| Pistol | Ricochet Coin (`ricochet-coin`) | Flip a coin, up for 1.4 s. A bullet that hits it (.6 m) turns to the nearest enemy in sight within 22 m at ×1.6 damage, no headshot bonus | ×1.6 of a round | 9 s / none | Needs a shot on a small moving target; out of sight, nothing happens |
| Machine gun | Sentry Drop (`sentry-drop`) | Drop a tripod gun at your feet: arms in .6 s, then fires at the nearest enemy in sight within 18 m for 6 s, 5 damage twice a second. 40 HP. Bots shoot it first | 5 a shot | 20 s / none | Laser shows its aim; break line of sight or shoot it |
| Rifle | Grapple Line (`grapple-line`) | Fire a line at a surface within 20 m and reel in at 16 m/s, at most 1.1 s. No firing while reeling. A jump or a roll lets go | 0 | 12 s / none | You fly a straight, predictable line unable to shoot |
| Bow | Gale Arrow (`gale-arrow`) | An arrow that bursts on impact, pushing everything within 5 m away, you included: shoot your feet to launch | 6 | 10 s / none | The push is the only effect: it can throw you to safety |
| Fire | Ignition Leap (`ignition-leap`) | A burst under your feet throws you 7 m up, with drift, and burns a 3 m circle | 14 + burn | 11 s / 22 mana | You fall on a predictable arc, and the flames show where you left |
| Water | Healing Spring (`healing-spring`) | A 4 m pool at the aimed floor within 14 m for 5 s: 6 HP a second to you and allies standing in it, enemies slowed ×.85 | heals 30 at most | 18 s / 30 mana | Standing still to heal in a bright pool invites a grenade; enemies can stand in it too |
| Lightning | Static Recall (`static-recall`) | Leave a crackling anchor where you stand for 4 s. Recast to bolt back to it, 12 to anyone on the line. Everyone can see the anchor | 12 | 13 s / 20 mana | Camp the anchor |
| Darkness | Shade Swap (`shade-swap`) | A slow orb (22 m/s, 18 m). On an enemy, you trade places after .25 s. Bosses and stunned targets are immune | 0 | 15 s / 24 mana | Slow and visible; a roll dodges it |

## The second set (owner 2026-11-04)

"Add additional abilities for each weapon, again same concept. Creative, good animations and effects", with more to come ("6+ more"). Same rules of the set as above: a trade, utility first, a visible counter. Each does something its
weapon cannot already do. **Rules are Astra's** (#325, the contract is below), the presentation Claude's; all twelve are offered in Customize. A technique whose content row is not live is not offered.

| Weapon | Technique (`id`) | What it does | Damage | Cd / cost | The counter |
| --- | --- | --- | --- | --- | --- |
| Sword | Parting Line (`parting-line`) | Hop back 4.5 m in .28 s. A cut hangs where you stood, 3 m wide across your facing, and bursts .7 s later: damage and a shove within 1.2 m of it | 14 | 11 s / 18 st | The line is plain to see: stop, or go round |
| Daggers | Tripwire (`tripwire`) | Throw a dagger at a surface within 14 m: a wire strings from the spot you threw from to where it stuck, armed .32 s after release, for 8 s. The first enemy through it takes damage and a .8 s snare, and it snaps. One at a time | 8 | 13 s / 20 st | It glints. Jump it, go round, or shoot its dagger (10 HP) |
| Shield | Shield Surf (`shield-surf`) | Drop on your shield and ride 2.6 s at 11 m/s along your look, steering. No attacks, no block. A jump or a recast hops off; a wall stops you | 0 | 12 s / 20 st | No shield up, and you make a lot of noise and sparks |
| Greatsword | Iron Vow (`iron-vow`) | Plant the blade and brace for 2.4 s: 40% less damage, no stagger or knockdown, no moving, no attacks. Recast ends it. It ends in a shove within 3.5 m | 0 | 14 s / 25 st | Walk away, or wait with something big |
| Pistol | Bounty Round (`bounty-round`) | One fast round, 60 m. An enemy hit is marked for 6 s, shown through walls to you and your allies. If the mark dies in that time the cooldown refunds | 6 | 14 s / none | It has to hit; a miss spends it. The marked pilot sees the mark |
| Machine gun | Recoil Burst (`recoil-burst`) | Dump what is left in the magazine (at least 1 round) to be thrown 12 m/s opposite your aim for .45 s; look down to jump. Enemies within 6 m along the aim take the damage. You land with an empty gun and reload | 10 | 10 s / none | You land empty: punish the reload |
| Rifle | Sonar Dart (`sonar-dart`) | A dart to a surface within 40 m. .5 s later it pings every 1.2 s, three pings: each shows enemies within 12 m through walls for 1.2 s to you and your allies. 10 HP | 0 | 16 s / none | It beeps and blinks. Shoot it, or leave the radius |
| Bow | Spirit Hawk (`spirit-hawk`) | A hawk of light flies your aim at 20 m/s up to 30 m and turns on the first enemy within 6 m of its line: damage and ×.8 speed for 1.5 s | 10 | 12 s / none | Slow and bright: a roll dodges the dive |
| Fire | Wildfire Run (`wildfire-run`) | 3 s at ×1.35 speed, no Q or E. Flames on your path last 3 s and burn enemies standing in them, 3 a second | 3 a second | 13 s / 25 mana | Don't follow through the fire; it draws your route for everyone |
| Water | Slipstream (`slipstream`) | A 14 m long, 2.4 m wide current on the floor along your aim, for 5 s. Anyone on it moves ×1.4 along it and ×.7 against it, enemies too | 0 | 15 s / 22 mana | Use it yourself, or make them fight against it |
| Lightning | Lightning Rod (`lightning-rod`) | Throw a rod to the floor within 12 m. It stands 5 s or 60 damage. Enemy projectiles passing within 3 m arc into it and are gone | 0 | 16 s / 24 mana | Walk round it, or break it up close: melee and blasts are not drawn |
| Darkness | Grave Walk (`grave-walk`) | Sink into your shadow for 1.5 s: nothing can hit you, you move at 7 m/s and cannot act. A recast rises early. The pool shows where you are the whole time | 0 | 17 s / 26 mana | Stand on the pool and wait for what comes up |

### Rules contract for the second set (Astra)

**More than one technique per weapon.** `NativeTechniqueLoadout` holds one per weapon (`sword:q`). It needs the technique's id: canonical `sword:q=riposte,sword:e=parting-line`, in fixed weapon then slot order. An old `sword:q` still reads as the weapon's first
technique. A slot holds at most one technique, and a technique at most one slot (picking it moves it). Rifle Q stays the scope. Proposed API: `In(map,spec,slot)` returns the id or "" and `With(map,spec,slot,id)` ("" restores the slot's own ability). Please update the account sanitizer the same way.
`NativeTechniques.Equipped(map,spec,slot)` and `Equip(map,spec,slot,id)` are the one place the presentation reads and writes this: change their bodies to your `In` and `With`. Both slots can now hold techniques, so `TechniqueRefusal` needs to know which: `TechniqueRefusal(p,slot,in aim)`, and `TechniqueAimCheck` in `Runtime/NativeFightHud.cs` should ask per slot (say so in your PR and Claude will follow, or change it). `Content.Ability(spec,"technique")` becomes a lookup by id.
`NativeTechniques.More` holds the twelve new rows; move them into `All` and drop `For` when your rules are in, or tell me to. The Locker offers a technique once its content row is `live`.

Records, on the same `NativeTechnique` (one new field: `Path`). Everything is in `Core/NativeTechnique.cs`'s preview `Write`, which is the worked example of each. Fill `TechniqueRefusal` for the new ones where a press can be refused (Tripwire and Sonar Dart: no surface).

| Kind | Phases | `From` | `Point` | `To` / `Dir` / `Arm` / `Path` | Triggered moment |
| --- | --- | --- | --- | --- | --- |
| `parting-line` | hop, line, burst | where it hopped from | the line's centre on the floor | `Dir` the facing (the line lies across it); `Arm` the burst | |
| `tripwire` | throw, armed, snap | the near anchor | the far anchor | `Arm` armed; `To` where it snapped | `HitAt`: it snapped; `Until` .85 s later, or the wire's end |
| `shield-surf` | mount, ride, hop | | | `Dir` the heading, kept current | `RecastAt`: the hop off, `Until` and `RecoverUntil` .25 s later |
| `iron-vow` | plant, brace, release | | the blade's spot on the floor | `Dir` the facing; `To` the last hit's point | `HitAt`: the last hit absorbed |
| `bounty-round` | shot, marked | hand at release | where the round ended | `To` the mark's place when hit; `Target` the marked id | `HitAt`: it hit; `Until` the mark's end, or the round's |
| `recoil-burst` | brace, blast | hand at release | | `Dir` the aim, 3D | |
| `sonar-dart` | fire, ping | hand at release | the dart | `Dir` the surface's normal; `Arm` the first ping (then every 1.2 s) | `HitAt`: the dart destroyed, `Until` the same |
| `spirit-hawk` | draw, flight, dive | hand at release | where the flight ended or the dive began | `Arm` when it got there; `To` the strike, kept current | `HitAt`: it struck; `Until` .3 s after, or Arm + .9 if it found no one |
| `wildfire-run` | ignite, run | | | `Arm` the run's end; `Until` the last flame's end; `Path` a floor point every `PathStep` from Release | |
| `slipstream` | cast, stream | the near end on the floor | | `To` the far end on the floor | |
| `lightning-rod` | throw, rod, zap | hand at release | the rod's foot on the floor | `Arm` when it landed; `To` the last shot drawn | `HitAt`: the last shot drawn |
| `grave-walk` | sink, walk, rise | where it sank | | `Until` when it starts to rise; `RecoverUntil` risen, .34 s later | `Release`: fully under. Untouchable from Release to `Until` |

`NativeTechniques.WeaponAway` now includes Shield Surf and Iron Vow, so `TechniqueAway` refuses Q/E and the shield's block during them. Bounty Round's mark and Sonar Dart's reveal are presentation (both read the record and live positions), so the rules only need to keep the record right.

### Second-set implementation and presentation handoff (Astra, 2026-11-04)

Rules on `codex/more-technique-rules`, based on shared `6bfd311f`, with `claude/more-techniques` through `d759e612` merged. All 24 definitions now live in `NativeTechniques.All`; `Of(spec)` enumerates however many that weapon has. `More` is removed. `For(spec)` remains only as a compatibility/legacy-first selector; new code must use `Of`, `Find` or `Equipped`.

- `NativeTechniqueLoadout.In(map,spec,slot)` returns an id or empty. `With(map,spec,slot,id)` replaces/restores only that slot and moves an id already used in the other slot. `NativeTechniques.Equipped` and `Equip` delegate to these (Equip's null restores the slot). Canonical weapon order is `Weapons`, then Q/E. Legacy `weapon:slot` selects its first definition. Defaults remain empty. The 2,048-character wire limit accommodates every slot; unknown/foreign ids and rifle Q are rejected. Account sanitizer mirrors this; source updated, live account service **not deployed**.
- Content resolution is by id; both slots use their own input and QReady/EReady deadline. Only one active record per actor: a second technique cannot overwrite an active effect. Recasts act only through the slot holding that record's id. Bots retain defaults.
- `TechniqueRefusal(p,slot,in aim)` is the slot-aware public query; the old `(p,in aim)` overload remains compatible. Tripwire (14 m) and Sonar Dart (40 m) share their cast's read-only world-surface/hub check and return `NoSurface`. All queries use passed aim. No new record fields beyond the agreed `Path`; path arrays are deep-copied and validated (32 finite points maximum).
- Final unspecified rule choices: Parting Line uses a .55 m hop arc; Tripwire is a .65 m contact tube, .8 s slow to x.4, with a .25 m dagger shot target; Iron Vow and Parting Line shoves are 5 m/s; Bounty Round travels at 120 m/s; Recoil Burst's 6 m cone requires aim dot >=.65, travels for .45 s then starts an ordinary reload (also after Bottomless, so it cannot leave an unusable empty gun); Hawk's dive travels at 20 m/s with a .9 s timeout, respects walls/rolls; wildfire uses 24 fixed .125 s samples, .75 damage per .25 s, no overlapping-patch stacking, and expires with the last sample at Release+5.875. Slipstream affects normal pilot and minion locomotion, does not stack with another current, and leaves committed enemy attacks alone. Rod redirects hostile shots only, requires sight to the rod, loses their damage from its 60 HP, and can also be damaged directly by melee/blasts. Death refunds Bounty's owning slot only while its mark is live; despawning a living minion does not refund.

**Claude follow-up (presentation files preserved):** `d759e612`'s picker now reads/writes the correct ids through the adapters, and its HUD resolves row numbers by id. `TechniqueAimCheck` still asks one shared reason: call `TechniqueRefusal(p,slot,in pending)` independently per equipped slot and give each card only its own reason. When calling `NativeTechniques.Card`, pass the live record only if its `Kind==td.Id`, otherwise the other slot falsely says ACTIVE/RECAST. `NativeTechniqueFx.HawkAt` currently starts a dive only when `HitAt>-50`; live authority cannot know a future hit. During `Phase=="dive"`, `Arm` is the dive start and `Point`/`To` are its start/current target; `HitAt` stays -100 until actual collision. Render that live phase without inventing a future hit. These require no record-field changes. Visuals/poses/sounds/models were merged unchanged; the only added presentation-side asset is the missing `.meta` for Claude's new FX source.

Native builder fixtures cover every new technique, per-slot routing, refusal, refund, movement and damage/counters; codec fixtures cover Path and isolated copies. They are compiled here, not executed in Unity/PhysX. Managed/account/source and compilation receipts are recorded in the task PR and DEV_LOG.

## The third set (owner 2026-11-04)

"Lets give essentially each weapon (non-dev) 3 more new abilities. Of course, same concept as we did for others (Balanced, fun, great animation and effects)." Twelve weapons, thirty-six techniques; the DEV kits, the Bombadier included, get none. The rules of the set are the same: a trade for a slot, utility first, a visible counter, at most 22 damage, 9 to 20 s. Each does something its weapon cannot already do, and the rifle's sit on E as its others do.

**Status:** design and rows by Claude (`Core/NativeTechniquesThird.cs`, generated with this table); rules are Astra's, presentation Claude's. `NativeTechniques.Third` is apart from `All` until the rules are in; `Of` and `Find` read both, and the Locker offers one only once its content row is live, so none reaches a player before it works.

| Weapon | Technique (`id`) | What it does | Damage | Cd / cost | The counter |
| --- | --- | --- | --- | --- | --- |
| Sword | Disarm (`disarm`) | Lunge 2.2 m in .22 s with a flick. The first enemy in a 1.4 m capsule takes the damage and cannot use Q or E for 2.5 s (a press is refused, as a stun refuses it). A miss leaves .5 s of recovery | 8 | 11 s / 18 st | Short reach, a roll dodges it; a miss leaves you open |
| Sword | Iaido (`iaido`) | Press to sheathe: up to 1.5 s, walking x.5, no attack or block. Press again (or at 1.5 s) to draw: you cross 5 m in .12 s, and everyone on the 1.2 m wide line takes 8 to 22 by how long you held | 8 to 22 | 13 s / 24 st | The stance and its growing glint are plain to see: step off the line. Cover stops the dash |
| Sword | Challenge (`challenge`) | Pick the enemy nearest your aim within 14 m in sight. For 6 s you and they deal +20% to each other, and you move x1.15 toward them. Two seconds without sight of them ends it. Bosses cannot be challenged | 0 | 16 s / 15 st | It cuts both ways: hit back, or break line of sight |
| Daggers | Mirror Double (`mirror-double`) | You are unseen for 1 s (still hittable, nameplate hidden). A double runs along your look at run speed for 2.5 s, or until one hit or a wall pops it. Bots go for it first. It cannot attack | 0 | 16 s / 25 st | It never attacks or turns; you shimmer while unseen, and splash finds you |
| Daggers | Envenom (`envenom`) | A .4 s coat. The next 3 hits within 6 s each poison for 8 over 4 s (refreshing, not stacking) and slow x.85 for 2 s | 8 poison a hit | 12 s / 15 st | The green drip warns of it: keep away until it runs out |
| Daggers | Chain Hook (`chain-hook`) | A hook at 40 m/s, up to 12 m. You stand still while it flies. The first enemy takes the damage and is hauled to 2 m in front of you over .35 s. A boss or a Hulk hauls you to it instead. Cover stops it | 6 | 13 s / 22 st | A straight line: a roll dodges it, and a miss leaves you standing |
| Shield | War Cry (`war-cry`) | A .3 s bang. Enemies within 8 m in sight deal -25% to anyone but you for 4 s, and you take -15% for 4 s. Bots turn on you | 0 | 15 s / 20 st | Shoot the one shouting, which is the point, or get out of the 8 m before it lands |
| Shield | Ground Slam (`ground-slam`) | A leap 3 m up, at most 5 m along your aim, .5 s in the air, drawing its landing circle. A 4 m ring on landing: damage and .4 s knocked up. No block in the air | 12 | 12 s / 22 st | The circle shows where: roll out, or jump |
| Shield | Lodestone (`lodestone`) | A 2.5 s stance, walking x.6, no block. Enemy shots within 7 m in front bend to the shield and stick, at most 8. A recast or the end releases them in a 6 m cone: 3 damage each, at most 24 | 3 a stored shot | 14 s / 25 st | Stop shooting, or come round behind it; melee goes straight through |
| Greatsword | Sunder (`sunder`) | A .55 s overhead, a 3 m cone. Everyone hit takes the damage, and +20% from everyone for 4 s, shown as cracks of light | 14 | 12 s / 24 st | Slow and plain: a roll is enough |
| Greatsword | Reaping Sweep (`reaping-sweep`) | A .25 s wind-up, then a 300 degree sweep at shin height. Enemies within 4 m on the floor take the damage, are dragged to 1.6 m and tripped for .5 s. Anyone in the air is missed | 10 | 13 s / 24 st | Jump over it |
| Greatsword | Blade Drag (`blade-drag`) | Up to 2 s at x1.2 speed, turning at most 90 degrees a second, no block. A recast or the end rips up a 2.6 m cone: 8 damage plus 6 a second dragged, at most 20, launching 5 m | 8 to 20 | 13 s / 22 st | Loud and bright, and it turns badly: sidestep |
| Pistol | Flash Round (`flash-round`) | It bursts on first contact or at 30 m. Within 6 m in sight, anyone facing it (within 70 degrees of their view) is whited out for .9 s; bots and adds lose their target for 1.2 s | 0 | 12 s / none | Look away. It blinds you too |
| Pistol | Gun Spin (`gun-spin`) | A .45 s twirl with no firing. Then the magazine is full and you fire +25% faster for 4 s | 0 | 12 s / none | You cannot shoot through the twirl: that is your moment |
| Pistol | Slide Shot (`slide-shot`) | 6 m along your movement (or look) in .55 s at crouch height, firing freely, with spread x.5. .2 s to rise. A wall stops it | 0 | 9 s / none | A straight line with no dodge at its end |
| Machine gun | Bipod (`bipod`) | A .5 s set. For up to 5 s you cannot move, with spread x.3, +30% fire rate and no rounds used. A recast packs up in .3 s | 0 | 15 s / none | You stand still: flank it, or use anything with splash |
| Machine gun | Ammo Crate (`ammo-crate`) | A crate at your feet: 8 s, 40 HP. You and each ally may take from it once: an instant reload and +20% attack speed for 5 s | 0 | 20 s / none | Shoot it: enemies can see it through nothing, but it is big and it stays put |
| Machine gun | Flak Burst (`flak-burst`) | It bursts at the aimed distance, at most 40 m: a 5 m cloud for 3 s. Enemy shots entering it are destroyed. Enemies in it take 2 a second and move x.9 | 2 a second | 14 s / none | Walk round it; melee ignores it |
| Rifle | Breacher Round (`breacher-round`) | A 1 s charge at walk x.4, with a laser everyone can see. The round goes through enemies and up to 1.2 m of walls in total, up to 80 m | 22 | 14 s / none | The laser tells you where: step aside |
| Rifle | Ghillie (`ghillie`) | A .6 s settle. While you stay under .5 m/s, for at most 8 s, you are all but invisible beyond 6 m, nameplate hidden. Firing or taking damage ends it; that first shot deals +20% | 0 | 16 s / none | A shimmer shows up close, and splash finds you |
| Rifle | Bolas Round (`bolas-round`) | 45 m/s, up to 40 m. An enemy hit takes the damage and is rooted for 1.2 s: they can still aim and shoot. Bosses are slowed x.6 instead | 4 | 12 s / none | Slow and spinning: a roll dodges it, and a Block breaks it |
| Bow | Bramble Arrow (`bramble-arrow`) | On first contact a 5 m wide, 1 m tall hedge grows across the arrow's line for 5 s. Enemies in it move x.6 and take 3 a second | 3 a second | 13 s / none | Go round, or jump it |
| Bow | Vault Shot (`vault-shot`) | A 5 m back flip in .45 s. At its top, one arrow along your aim, no draw | 14 | 10 s / none | The arc is predictable, and you land where it ends |
| Bow | Tether Arrow (`tether-arrow`) | An arrow; the enemy hit takes the damage and is tied to where they stood: for 3 s they cannot go past 5 m and are pulled back. A dodge roll snaps it for 6 more. Bosses are immune | 5, then 6 if snapped | 14 s / none | A roll snaps it; the line shows exactly where they can go |
| Fire | Flame Cloak (`flame-cloak`) | For 4 s, shots on you deal x.75, and a melee attacker takes 6 and burns for 5 a second for 2 s | 6 + burn | 16 s / 26 mana | Keep your distance and wait it out |
| Fire | Dragon Breath (`dragon-breath`) | A .25 s inhale, then 1.6 s of a 7 m, 30 degree cone: 10 a second and burn, walking x.5 and turning at most 120 degrees a second. A recast stops it | up to 16 + burn | 13 s / 24 mana | Step out and flank it |
| Fire | Combust (`combust`) | A .2 s snap. Every enemy burning within 14 m in sight loses the burn: its remaining damage at once plus 8 in 2 m, at most 22 each. Refused when no one is burning | up to 22 | 9 s / 16 mana | Water and time put a burn out: no burn, no blast |
| Water | Bubble Trap (`bubble-trap`) | 18 m/s, up to 20 m. The enemy hit floats 1 m up for 1.4 s, unable to act. Damage on them is halved and pops it. Bosses are slowed x.6 instead | 0 | 14 s / 24 mana | Slow: a roll dodges it. Anyone hitting the bubble frees them |
| Water | Flash Freeze (`flash-freeze`) | A 6 m sheet for 4 s. Enemies on it move x.85 and slide (a quarter of their grip); you move x1.2 | 0 | 14 s / 22 mana | Jump off it, or stay away: it does not follow you |
| Water | Hydro Jet (`hydro-jet`) | For up to 1.5 s, 9 m/s opposite your look (look down to rise); a 6 m stream pushes enemies 6 m/s. No damage, no attacks. A recast or a jump lets go | 0 | 12 s / 20 mana | You fly straight and cannot attack |
| Lightning | Magnetize (`magnetize`) | The two enemies nearest your aim within 16 m in sight. After .5 s they pull together for 1 s; on touching, 10 each and a .4 s stun. One alone is pulled to the point you aimed at. Cover stops the pull; bosses are immune | 10 each | 13 s / 24 mana | Roll out of the pull, or put a wall between you |
| Lightning | Thunderclap (`thunderclap`) | A .3 s clap. Within 5 m in sight: 10 damage, a .5 s stun, and any channel ends | 10 | 12 s / 26 mana | The raised hands telegraph it: back off |
| Lightning | Overload (`overload`) | For 4 s, +25% attack speed and +15% speed, crackling for all to see. It ends in 8 damage to you (never lethal). A recast ends it early; under 1 s, no shock | 8 to you | 14 s / 18 mana | Kite it: it runs out and hurts them |
| Darkness | Soul Siphon (`soul-siphon`) | For up to 2 s on the enemy nearest your aim within 10 m in sight: 8 a second, healing you the same, walking x.6. It breaks at 12 m or out of sight. A recast stops it | up to 16 | 13 s / 22 mana | Run, or get behind cover |
| Darkness | Dread (`dread`) | A .35 s wind-up, then a 6 m, 90 degree cone. Enemies hit stumble away from you for .9 s, unable to attack or steer. Bosses are slowed x.6 instead | 0 | 15 s / 26 mana | The wind-up is your warning: roll, or come from the side |
| Darkness | Shadow Puppet (`shadow-puppet`) | A puppet where you stand, for 4 s, 30 HP. It copies your left-click toward your aim from where it stands at 40% damage, triggering no chips. Bots may target it | 40% of a shot | 16 s / 28 mana | It stands still and dies quickly |

### First rules batch (Astra, 2026-11-05)

`NativeThirdTechniqueContent.Ready` enables **Gun Spin, Thunderclap and Overload** by live content ID. Gun Spin: .45 s locked twirl, full magazine, +25% rate for 4 s. Thunderclap: .3 s windup, 10 damage / 5 m / .5 s stun and channel cancellation (Sonic Boom: 7 m / .7 s). Overload: .1 s ignition, +25% rate / +15% speed for 4 s, 8 nonlethal self damage; recast under one second avoids the shock. These preserve the proposed cooldown/cost. No other third-set technique has a live row yet; `Third` intentionally remains separate from `All` until all 36 work.

`Actor.TechniqueMarks` supplies the requested marks (`NativeTechniqueMark { Kind, By, At, Until }`, max 6, oldest replaced), with deep copies and frame/death/corpse validation. Null is accepted for legacy records. `TechniqueRefusal` supplies Disarmed for Q/E, including defaults; disarm/hush refuse new presses without cancelling primary fire/draw. NoTarget target queries are in place for the still-gated Challenge, Combust and Soul Siphon. The first three live techniques do not create victim marks. Chip state/event names and the rollout list are in CHIP-EXPANSION.md.

### Second rules batch (Astra, 2026-11-05)

Adds **Bipod, Envenom, Dragon Breath, Combust and Soul Siphon**, bringing live third-set rows to 8/36. `Third` stays separate until all handlers exist. The account sanitizer accepts these five IDs; the service itself is not deployed here.

- Bipod uses the proposed .5 s set / 5 s stance / .3 s pack-up. It roots movement, fires at zero ammo without spending rounds, applies x.3 spread / x1.3 rate, and permits a slot recast. Belt Feed extends it to 7 s and heals 3/s. Block/roll/stun interrupt; jumping is refused while planted.
- Envenom uses .4 s coat / 6 s window / three successful direct hits. Each refreshes 2 poison/s for 4 s and x.85 speed for 2 s on pilots and adds. A lethal hit spends a charge but does not poison a corpse.
- Dragon Breath uses .25 s inhale / 1.6 s channel / 7 m / 30-degree full cone / 10 damage/s, sampled every .1 s with x.5 walk and a 120-degree/s aim limit. Its unspecified burn is set to 1.5/s for 2 s, refreshed, not stacked. Deep Lungs uses 2.4 s / 40 degrees.
- Combust refuses with no burning target before payment. After .2 s, burns in sight within 14 m are consumed. A target's remaining burn plus 8 bursts in 2 m, capped at 22 base damage; overlapping detonations use the strongest hit, never multiply that cap. Path carries up to six blast points as contracted; all eligible burns still resolve.
- Soul Siphon uses .1 s reach / 2 s channel / 8 damage/s in .1 s ticks, healing actual damage only; x.6 walk, 10 m acquisition / 12 m tether, cover/death ends it. Soul Feast uses 14 m acquisition / 16 m tether (preserving 2 m escape slack), and transfers 10/s of the victim's stamina for melee or mana otherwise, bounded by the victim's balance and caster's cap. Adds without these resources provide none.
- Recasts stop the two spell channels; stun, roll, knockdown or block cancels them. Channel motion cannot fall through to Anchor Throw's teleport. Release state, owning slot, damage scale and next tick are replicated alongside the existing technique record to avoid restarting ticks after authority restoration. Record phases/points follow the staged contract; no presentation changed.

### Third rules batch (Astra, 2026-11-05; task branch)

`codex/expansion-rules-3`, based on shared `a6a454a4`, adds one technique for each of the six priority weapons: **Disarm, Ground Slam, Reaping Sweep, Bolas Round, Vault Shot, Hydro Jet**. This is **14/36** implemented rows, not the completed set. `Third` remains separate from `All`. Remaining priority techniques: Iaido, Challenge, War Cry, Lodestone, Sunder, Blade Drag, Breacher Round, Ghillie, Bramble Arrow, Tether Arrow, Bubble Trap and Flash Freeze. Finish those before the remaining daggers/pistol/machinegun/darkness/fire/lightning rows.

- Disarm: .01 s start, 2.2 m/.22 s collision-limited lunge, .7 m contact radius, 8 damage to the first valid contact. Pilots receive the existing `Actor.TechniqueMarks` disarm for 2.5 s; primary attacks remain legal. Hit recovery .12 s; miss recovery .5 s. Adds have no Q/E slot to refuse.
- Ground Slam: .1 s start; .5 s parabolic leap, 3 m apex, up to 5 m horizontal travel. No attacks/block in the leap. Grounded victims in 4 m take 12, a 4.6 m/s upward impulse and .4 s pilot stun. Side/ceiling collision cancels instead of exploding in midair; a floor contact resolves at the actual landing. Aftershock is 6 damage/6 m after .5 s, once, without knock-up.
- Reaping Sweep: .25 s windup, 300-degree/4 m grounded check, 10 damage and .5 s trip. Pull uses the existing collision-resolved knockback, aimed toward the 1.6 m inner radius and capped at 8 m/s; it does not teleport a victim to an exact radius. Pilot trip marks replicated.
- Bolas Round: .1 s start, 45 m/s swept orb, 40 m limit, 4 damage, 1.2 s bound. Pilots can aim/fire; Block removes the mark and root. Movement constraints run after summon/PvE movement as well. Bosses use x.6 slow. Bolas/arrow flight resumes from the record's replicated Point rather than a local cached position.
- Vault Shot: .01 s start, 5 m backward/.45 s parabolic vault, 1.5 m apex. At .225 s it fires a single 14-damage arrow at 45 m/s with 18 m/s² gravity and 40 m forward range. `Path[0]` stores its launch position; `Point` its current/contact point, `To` remains the landing. A collision stops the vault; the launched arrow retains its own clock. This is the one extra record-field use Claude needs for this batch.
- Hydro Jet: .1 s start, up to 1.5 s at 9 m/s opposite current aim, including upward travel when looking down. No attacks; recast/jump ends it. Its visible 6 m stream pushes susceptible enemies by collision-checked .1 m steps (6 m/s); bosses retain their displacement immunity. Cover and sanctuary boundaries stop movement.

No presentation, poses, effects, cards or sounds changed. Native Unity regression cases are added but require actual Editor execution; semantic compilation does not establish PhysX/playtest acceptance. Account sanitizer source accepts all six; no account-service deployment.

### Rules contract for the third set (Astra)

Same record and same pattern as the second set: `Core/NativeTechnique.cs`, content rows by id with `slot:"technique"`, the slot's own input and deadline, one active record per actor, recasts through the slot holding it, bots keep their defaults. Move `Third` into `All` when your rules are in, and tell me.

**Fields.** All times on the presentation clock, as before. Unlisted fields are unused. New phases are already in `Valid()`.

| Kind | Phases | Fields | Triggered moment |
| --- | --- | --- | --- |
| `disarm` | lunge, flick, recovery | `From` the start; `Point` the contact; `Dir` the lunge; `Target` who | `HitAt`: it caught someone; `Until` the disarm's end, or `Release` + .5 on a miss |
| `iaido` | sheathe, draw, recovery | `From` the start; `To` the end; `Dir` the line; `Arm` the charge 0 to 1, set at the draw | `RecastAt`: the draw; `HitAt`: the first cut; `Until` the draw + .3 |
| `challenge` | point, duel | `Target` who; `Until` the end, moved up if it breaks | `NoTarget` refusal when no one is in reach |
| `mirror-double` | cast, run, pop | `From` the start; `To` the double now; `Dir` its heading; `Arm` when you reappear | `HitAt`: it popped, `Until` .3 s later |
| `envenom` | coat, coated | `Arm` the hits left, 3 to 0; `Until` the end | `HitAt`: the last hit; `To` where |
| `chain-hook` | throw, flight, yank | `From` the hand; `Point` the hook now; `To` where it pulls to; `Target` who; `Arm` when it turned back | `HitAt`: it caught; `Until` the yank's end |
| `war-cry` | bang, rally | `Point` the centre; `Until` the end | Each enemy carries a mark (below) |
| `ground-slam` | leap, slam | `From` the start; `Point` the landing; `Arm` the landing's time | `HitAt`: the landing |
| `lodestone` | raise, draw, release | `Dir` the shield; `Arm` shots held; `To` the last caught | `HitAt`: the last caught; `RecastAt`: the release, `Until` .3 s later |
| `sunder` | raise, smash | `Point` the blade's landing; `Dir` the facing | `HitAt`: it struck. Each victim carries a mark |
| `reaping-sweep` | wind, sweep | `Point` the centre on the floor; `Dir` the sweep's start | `HitAt`: it struck |
| `blade-drag` | drag, rise, recovery | `Path` a floor point every `PathStep` from `At`; `Arm` the charge 0 to 1, set at the rise; `Dir` the heading now | `RecastAt`: the rise; `HitAt`: it struck |
| `flash-round` | shot, flash | `From` the hand; `Point` the burst; `Arm` the burst's time | Each victim carries a mark |
| `gun-spin` | spin, hot | `Arm` the twirl's end; `Until` the end |  |
| `slide-shot` | slide, rise | `From` the start; `To` the end; `Dir` the slide | `Until` the slide's end |
| `bipod` | set, planted, packup | `Point` the feet; `Dir` the gun's aim, kept current | `RecastAt`: the pack-up, `Until` .3 s later |
| `ammo-crate` | drop, open | `Point` the crate; `Arm` its health 1 to 0; `To` the last to take from it | `HitAt`: the last pick-up |
| `flak-burst` | fire, cloud | `From` the hand; `Point` the cloud; `Arm` the burst's time; `To` the last shot shredded | `HitAt`: the last shot shredded |
| `breacher-round` | charge, fire | `From` the muzzle; `To` the round's end; `Dir` the line; `Path` where it entered and left walls | `HitAt`: the first enemy |
| `ghillie` | settle, hidden | `Point` where; `Arm` when fully faded; `Until` the end, moved up when broken |  |
| `bolas-round` | fire, flight, bound | `From` the muzzle; `Point` where it ended; `Target` who | `HitAt`: it hit; `Until` the root's end |
| `bramble-arrow` | draw, flight, thorns | `From` the arrow; `Point` the hedge's middle on the floor; `Dir` its line; `Arm` when it landed |  |
| `vault-shot` | vault, shot | `From` the start; `To` the landing; `Arm` the release; `Point` where the arrow ended | `HitAt`: it hit |
| `tether-arrow` | draw, flight, tether | `From` the arrow; `Point` the anchor; `Target` who | `HitAt`: it hit; `RecastAt`: it snapped; `Until` the end |
| `flame-cloak` | ignite, cloak | `Arm` the ignition's end; `To` the last contact | `HitAt`: the last contact |
| `dragon-breath` | inhale, breath | `Dir` the breath, kept current; `Arm` the breath's start | `RecastAt`: stopped early |
| `combust` | snap, blow | `Path` the blasts, at most 6; `Arm` when they go off | `NoTarget` refusal when no one burns |
| `bubble-trap` | cast, flight, bubble, pop | `From` the hand; `Point` the orb; `Target` who | `HitAt`: it caught; `RecastAt`: it popped; `Until` the end |
| `flash-freeze` | stamp, ice | `Point` the centre on the floor |  |
| `hydro-jet` | charge, jet | `Dir` the stream, kept current | `RecastAt`: let go |
| `magnetize` | cast, pull, clash | `Point` the meeting place; `Target` the first; `Path` where each one is now; `Arm` when the pull starts | `HitAt`: the clash |
| `thunderclap` | raise, clap | `Point` the centre; `Arm` the clap's time | `HitAt`: the clap |
| `overload` | surge, live, shock | `Arm` the surge's end | `RecastAt`: ended early; `HitAt`: the shock |
| `soul-siphon` | reach, drain | `Target` who; `To` the beam's end now | `RecastAt`: stopped; `NoTarget` refusal |
| `dread` | gather, scream | `Point` you; `Dir` the cone | `HitAt`: the scream. Each victim carries a mark |
| `shadow-puppet` | cast, puppet, pop | `Point` where; `Dir` its aim, kept current; `Arm` its health 1 to 0 | `HitAt`: its last shot, `To` the shot's end |

**New: marks on the victim.** Several of these leave something on someone else, sometimes on many at once, and one `Target` cannot say who. Please add a small replicated list on the actor: `NativeTechniqueMark{string Kind;int By;float At,Until;}`, at most 6, the oldest replaced, in snapshots, late join and the killcam, checked by `Valid()`. `Kind` is the technique's or chip's id. Set one for: `disarm`, `challenge` (on both duellists), `war-cry`, `sunder`, `flash-round` (only on those it dazzled), `bolas-round`, `tether-arrow`, `bubble-trap`, `magnetize`, `dread`, `reaping-sweep` (the trip), and the chips listed in docs/CHIP-EXPANSION.md. The presentation draws them and the whiteout from the list and adds no rule. Tell me the accessor's name.

**New refusals.** `TechniqueRefusal` also answers `NativeTechniques.NoTarget` for Challenge, Combust and Soul Siphon, and a pilot under `disarm` or `wild-hush` is refused Q and E as a stun refuses them, answering `NativeTechniques.Disarmed` so the card can say so. The constants and the card lines are in.

**Movement** belongs to the rules, as before: Disarm's lunge, Iaido's dash, Chain Hook's haul, Ground Slam's leap, Blade Drag's run, Slide Shot, Vault Shot, Hydro Jet, Reaping Sweep's drag, Magnetize's pull, Dread's stumble, the bubble's float and Tether's pull. Nothing in the presentation moves a pilot.

**Choices left to you.** Exact capsule sizes and the shove strengths; whether a stun, knockdown or roll cancels each channel (the default is that they do, as with Q and E). If a number here does not work in play, change it and note it: this table is a proposal, your numbers win.

**Doubles and puppets.** Mirror Double, Shadow Puppet and the Evil Twin chip are minions, by the path Toy Soldier uses, so bots can see them. Ammo Crate is a thing with health, as Sentry Drop is.

## Simulation contract (for Astra)

Presentation reads one authority-owned record per actor, `NativeTechnique` (`Core/NativeTechnique.cs`, already in the tree). Replicate
it the way `Actor.MeleeKit` is: `Actor.Technique`, copied in snapshots, late join and the killcam, validated with `Valid()`. All times
are on the presentation clock. Content rows use `slot: "technique"` as a catalog entry, never an input slot; `QReady`/`EReady` track the chosen slot. Bots retain the default abilities. Set `NativeTechniques.Source` from the snapshot and presentation takes
it from there. Drop the preview when it is done, or leave it behind the sandbox flag.

Common fields: `Kind` (the id), `Phase`, `At` (the press), `Release` (when the move leaves the pilot), `Until` (the effect's end, moved
earlier on a recast), `RecoverUntil` (the pilot's own lock), `HitAt` (-100 until the triggered moment).

| Kind | Phases | `From` | `Point` | `To` / `Dir` / `Arm` | Triggered moment |
| --- | --- | --- | --- | --- | --- |
| `riposte` | guard, counter, recovery | | the contact point | `Dir` the aim until a parry, then attacker to you | `HitAt`: the parry |
| `smoke-veil` | throw, cloud | hand at release | the cloud's centre on the floor | | |
| `bastion` | plant, wall, recall | | the shield's foot on the floor | `Dir` the wall's facing; `Arm` its health 1 to 0 | `HitAt`: the last stopped shot, `To` where |
| `anchor-throw` | throw, flight, stuck, leap, return | hand at release | where the blade stuck | `Arm` when it stuck; `Dir` its flight | `RecastAt`: the leap |
| `ricochet-coin` | flick, air | hand at release | the coin at the hit | `Dir` the flick's flat aim; `To` who it turned to | `HitAt`: the bullet met it |
| `sentry-drop` | drop, armed | | the tripod's feet | `Dir` its aim; `To` its target; `Arm` armed time | Shots as `Shot` events, `Detail="sentry"` |
| `grapple-line` | fire, reel | hand at release | the anchor | `Arm` when the hook landed | |
| `gale-arrow` | draw, flight, burst | arrow at release | the burst | `Arm` when it burst | |
| `ignition-leap` | crouch, flight | | where it left the floor | `Dir` the drift | |
| `healing-spring` | cast, pool | hand | the pool's centre on the floor | | |
| `static-recall` | set, armed, bolt | where the recast left | the anchor | | `RecastAt`: the bolt |
| `shade-swap` | cast, flight, swap | where the orb left | the swap's other end | `Dir` the flight; `Target` the swapped | `HitAt`: the orb landed |

Movement belongs to the rules: the preview does not move the pilot, so Anchor Throw's leap, Grapple Line, Ignition Leap, Static
Recall and Shade Swap are seen in the sandbox with their effects only until then. The presentation adds no field to the wire and no
rule, hit timing, damage, cooldown or movement.

## Presentation (Claude)

| Piece | File |
| --- | --- |
| The record, the table, the preview loop | `Core/NativeTechnique.cs` |
| The effects, pure functions of the record | `Core/NativeTechniqueFx.cs`, `Core/NativeTechniqueFxSecond.cs` |
| Poses on the model's pack clips | `Core/NativeVisualMotion.cs` (`Technique`) |
| Sounds | `Core/NativeTechniqueSounds.cs` |
| The picker in Customize (Loadout) | `Runtime/NativeLockerUI.cs`, `Core/NativeLocker.cs` (`AddTechniques`) |
| The card sentence and the HUD card's state | `Core/NativeTechnique.cs` (`Text`, `Card`) |
| HUD card, through-walls markers, drawing, models | `Runtime/NativeFightHud.cs`, `Runtime/NativeCombatHud.cs`, `Runtime/NativeTechniquePresentation.cs` |
| Checks | `Tests/TechniqueChecks.cs` (`CoreTests --techniques`) |

**The picker.** Customize, Loadout, Abilities: two slot tiles, Q and E, each with its key and what the slot holds now. Clicking a tile chooses the slot; under the tiles a library shows everything that slot can hold: its own ability and each of the weapon's techniques.
The equipped card has the bright edge and the tick (a technique's in its own accent, the colour of its effects). A click swaps with a short pop; a
technique already in the other slot says ON Q or ON E, and picking it here moves it. A swap is part of the look: Save Pilot stores and sends it,
Revert restores it. The rifle's Q tile says FIXED: it can still be opened to read the scope, and nothing there can be picked.

Built for more abilities: the library has no fixed count. A few options share the full row; past that the cards keep `LkAbilityMin` (224 px) and wrap onto new rows, and the page scrolls. A card grows to fit up to five lines of its text. Seen with 2, 3 and 8+ options on a sized-down Game view; a very narrow window has only been worked out, not captured (about two columns at 1280 wide).

**Adding an ability.** Nothing in the picker or the HUD names one:

1. `Core/NativeTechnique.cs`: the id, a `Def` row in `All`, and its case in the preview's `Write` (the record's worked example). `Of(spec)` then offers it.
2. The effect as a pure function of the record, in a partial of `NativeTechniqueFx`, with its case in the dispatch; its moments in `Core/NativeTechniqueSounds.cs`; its pose in `Core/NativeVisualMotion.cs`. A clip new to `TechClips` needs the motion bank rebaked and the Drive zip repacked before the builder sees it.
3. Rules (Astra): the handler and a content row by id with `slot:"technique"`. The Locker offers it once the row is live.
4. `Tests/TechniqueChecks.cs` loops over `All`, so its effect, card and text are checked with no new test. Add a pin for whatever it does differently.

**The HUD.** The technique takes the card of the slot it replaced: RECAST in amber while a press would recast it, ACTIVE while it runs, then that
slot's cooldown as this pilot's chips scale it, and LOW STAMINA or LOW MANA. While the shield stands as a wall or the greatsword is thrown, the rules refuse
block and the other slot, so those cards say SHIELD OUT or BLADE OUT. A press refused for where it is aimed is below. A swapped E draws no field preview for the ability it replaced. Smoke hides a
pilot's name and health bar from anyone outside the cloud.

Each card asks only about its own slot: its own refusal, and the live record only when the record's `Kind` is its own id, so the other slot never claims ACTIVE. Bounty Round's marked target and, during each ping, the enemies within Sonar Dart's reach get a round icon with the distance, through walls, for the owner's side only (pilots and minions). Grave Walk hides the walker's name and bar, and closes the walker's own view in with a dark veil that follows the depth (`GraveDepth`).

To see one: the Dev Sandbox, Preview Techniques, with the weapon in hand. The preview loops the weapon's technique from just in front of
the pilot, since it does not know the world's surfaces or move anyone.

Reviewed in Unity, in slow motion from the game's own camera (Cybercity, local tool: `gallery=T,TechniquePreviewOn`, `slowmo=T,.2`, `screen=1`), all twelve,
with two rounds of fixes: the guard from a glowing panel to thin arcs, the wall from a framed rectangle to a honeycomb, the planted shield and
the thrown blade onto the pilot's own weapon model, the coin's glow down so the coin reads, the fire from washed-out to hot, the wind burst stronger.

Reviewed again with the rules (2026-11-03, the same tool: `swaps=`, a real Q or E press at an arena time, full speed and slow motion): all twelve fire from the swapped
key and the old ability stays away, with Anchor Throw's leap and landing, Grapple Line up a wall, Static Recall's bolt back, Shade Swap on a bot and the sentry firing at one; in first
person Anchor Throw, Ignition Leap, Smoke Veil and Riposte; the picker by real clicks through the Game view (a swap, a move from E to Q, Save Pilot lighting up). Two
fixes came of it: the guard's arcs drew behind the pilot (`Dir` is the aim until a parry) and now face where the pilot faces, also dimly in first person; Ignition Leap's wings read as a stripe across the
screen and are now shorter and swept back into a V. Not seen yet: Riposte's actual parry, a shot on the coin, Static Recall's damage on the line, a crowd, a guest or replay, a low
graphics tier in Unity (the checks cover the thinning), a Windows build. 

**The second set, reviewed (2026-11-04).** First the previews, in slow motion from the game's camera: all twelve in third person, and Shield Surf, Iron Vow, Recoil Burst, Slipstream and Grave Walk in first person. Then Astra's rules (`swaps=`, a real Q or E press on the arena clock, slow motion): all twelve fire from the swapped key with the right record and no exceptions, and their effects land where the rules put them.
Fixes from it: Shield Surf's sparks moved to the board's sides, since behind the pilot is off-frame from the low camera. Slipstream rewritten: flat strips went white, now water on the floor with a tint and thin lines. Wildfire's burning line down from neon. A bigger hawk, which now also dives on the live record (`HitAt` is unknown until it strikes). Grave Walk's pool without its plate, as dark smoke raised off the floor with a violet rim. Recoil Burst's glare down with shock rings added. Sonar's ring at dart height removed, since it went edge-on as a line across the screen. A thicker wire, a stronger mark, and Iron Vow's blade seated in the hand.

**Fixed on `codex/bombadier-rules` (2026-11-04), native acceptance pending: Parting Line snapped the pilot back on every cast.** Seen on open floor and with a pillar behind (arena clock, `body` from the log): 5.32 hopping at 3.1 m, 5.50 back at `Point+up*.9`, standing in the burst. The hop's `wall` flag sets `Phase="line"` before `Release+.28` (`NativeMoreTechniques.MoreTechniqueMotion`; most likely the arc's descent meeting the floor, not confirmed). `MoreTechniqueMotion` then returns false, `NativeTechniqueRules.Moving` is still true, and `TechniqueMotion` falls into its anchor-throw branch, whose `RecastAt` of -100 puts the destination at `Point+up*.9` at once. The fix is `k.Kind=="parting-line"&&k.Phase=="hop"&&now<k.Release+.28f` in `Moving`, which guest prediction also reads; and the arc should not count the floor as a wall, or the hop ends short. The effect itself is right; it just needs the pilot to stay out of it.

Not seen: a shot actually drawn into a real rod, a real mark's refund, Wildfire and Slipstream on a moving enemy, a real wire waiting armed (it snapped on the bot at once; seen armed in the preview), a guest or replay, a crowd, a low graphics tier in Unity, a Windows build.

**A refused press.** A press the rules refuse for where it is aimed must not feel like a dead key (Grapple Line with no surface in 20 m, Static Recall with something on the line: a low
counter is enough). While that holds the card says NO SURFACE, BLOCKED or NO FLOOR instead of READY or RECAST, without flaring each time the aim comes back; a press then puts the reason under the crosshair
for a second, shakes the card and plays `ui.error`. A press on cooldown stays quiet: the card's count says it. Built and seen on the PC against a temporary copy of the check below.

**Implemented by Astra (2026-11-04): `NativeArena.TechniqueRefusal(NativePilot p,in InputFrame aim)`** (`Runtime/NativeWeaponTechniques.cs`) shares the original geometric checks with `TryTechnique`. The public HUD method and casting use the same private overload; casting receives its resolved point to avoid repeating the queries. No change to what is refused:

- `NativeTechniques.NoSurface`: Grapple Line, nothing within 20 m along `aim`'s look, or the hit crosses the hub.
- `NativeTechniques.Blocked`: Static Recall inside its recast window, when `TechniqueDestination` or `RoomFor` fails or the capsule cast hits something on the line. Outside the window, null.
- `NativeTechniques.NoFloor`: Healing Spring, no walkable floor at the aimed point within 14.5 m, or the hub. `AimedAbilityPoint` reads `p.Input`, so it needs the passed `aim`.
- null for everything else: cooldown, cost, a stun and the like already show on the card.

The HUD passes `pending` (the look about to be sent) once a frame for the local pilot. The query uses the supplied yaw, pitch and AbilityTarget through explicit-aim overloads of AimedAbilityPoint/AuthorityAim; it never swaps p.Input, casts, spends resources, changes cooldowns, moves bodies or emits events. Other abilities retain wrappers using p.Input. It uses the existing nonalloc physics query buffers (growing only on saturation), and ordinary techniques do no physics queries. Guest sanctuary checks read the already-loaded collision area, because HubOn is authority-only; host sanctuary predicates are unchanged. Native tests cover pending vs stale aim, confirmed body depth, replica immutability/sanctuary, surface range, missing/out-of-range floor, blocked/occupied/out-of-bounds recall, recast expiry and refusal without state/cost changes.


## Simulation implementation and presentation handoff (Astra, 2026-11-03)

- All twelve standard weapons have authority technique catalog rows. The host consumes the selected Q/E edge; no new input packet. Costs use `NativeAbilityRules.Cost`, cooldowns use `NativeAbilityRules.Cooldown`, and the selected slot's `QReady` or `EReady` is its replicated absolute deadline. `TechniqueReady` was removed. Recasts do not charge twice or restart cooldowns. No new mechanic grants damage invulnerability.
- `Actor.Technique` is copied by `ViewCopy`, carried by the frame/late-join/replay path, presence-tagged for Unity JsonUtility, and checked by `Valid()`. Live render and replay select `NativeTechniqueSnapshotSource`. No presentation files were edited; the existing sandbox preview can still override the source for its own render.
- Authoritative movement: Anchor Throw sweeps its 16 m flight at 26 m/s, accepts one recast for three seconds after sticking, then uses a .45 s collision-limited leap. Grapple flies its hook at 60 m/s and reels at 16 m/s for at most 1.1 s, with jump/roll release. Ignition uses `sqrt(2*g*7)` upward velocity and normal steering/gravity. Static Recall refuses obstructed/occupied destinations and damages each line target once. Shade's orb travels 22 m/s for at most 18 m; after .25 s both destinations and the target's immunity are rechecked before an atomic exchange. These moves respect the current area and sanctuary boundary.
- Runtime choices where the table left a number open: Ignition burn is 3 DPS for two seconds before ability scaling; Gale's push impulse is 650, including self/allies and movable props (damage stays on normal hostile/friendly-fire rules). Weapon-away primary punches have 1.4 m reach and 6 base damage; Q/E and shield blocking cannot use the absent weapon. Reeling forbids shooting. Stuns, knockdowns, rolls and control/carry states refuse new casts; owner death/disconnect ends deployed effects. Active techniques delay cross-area transfers until their short lifetime ends, preserving area ownership.
- Coin contract correction: `CoinAir` is **1.4 s** as specified (was 1.1 in the preview helper); `CoinLift` is **6.1 m/s** so the existing parabola stays above the floor for that interval. No field meaning changed. A swept .6 m hit consumes one coin, turns the round toward the nearest visible enemy within 22 m, multiplies its damage by 1.6 and disables headshot bonuses. With no visible enemy it continues without amplification.
- Bastion and sentry use shot-only intersections, so actors can walk through them. Standard bullet, ability-shot and melee-projectile paths respect nearest collision ordering. Bastion intercepts both teams; sentries take hostile/friendly-fire projectile, primary melee and blast damage. The sentry deals fixed 5 damage per round at .5 s intervals, arms after .6 s and expires after six armed seconds. Bots prioritize visible hostile sentries but retain their default Q/E abilities. Smoke prevents target acquisition from outside the cloud; entering the cloud restores targeting. Healing Spring ticks 1.5 HP every .25 s (30 maximum per pool) and slows enemies to .85.

**Claude follow-ups, done (2026-11-03) except where item 3 says:**

1. `Runtime/NativeCombatHud.cs`, pilot health/nameplate loop: skip a pilot when `arena.TechniqueSmokeHidden(arena.ViewPilot.Body.position, p.Body.position)` is true. The query also works on replicas because it reads replicated technique records. The simulation's bot concealment is active; human nameplate concealment awaits this hook.
2. `Runtime/NativeFightHud.cs`: remove the standard-weapon key-1 card. Resolve each Q/E card with `NativeTechniqueLoadout.Ability(content, p.Seat.weaponTechniques, p.Seat.spec, slot)`, then use that slot's `p.QReady` / `p.EReady` and `NativeAbilityRules.Cooldown(row,p.Stats)`. A technique on E uses the single deadline (no extra-charge display); recasts use the same key without spending again. Rifle Q always remains scope. Icons can use `NativeTechniques.Find(row.id)?.Icon` for technique rows.

### Customize and saved-loadout contract (Claude)

- **Field:** `string weaponTechniques` on `ConversionPreferences`, `NativeCosmetics` (inside the existing seat `NativeProfile.cosmetics`), `NativeSeat`, and `NativeSeatFrame`.
- **Format:** canonical comma-separated `weapon:slot` entries; e.g. `rifle:e,sword:q`. Empty/null means both existing Q and E. One replacement at most per weapon. Slots are lower-case `q` / `e`; rifle accepts only `e`. Standard weapon IDs only; no DEV IDs.
- **Read picker:** `NativeTechniqueLoadout.Slot(prefs.weaponTechniques, spec)` returns `""`, `"q"`, or `"e"`.
- **Write picker:** `prefs.weaponTechniques = NativeTechniqueLoadout.With(prefs.weaponTechniques, spec, slot)`; use `""` to restore both defaults. Hook this field into Customize's `LockerLook` baseline, dirty comparison and discard restore, then existing `Save()`/profile send on Apply. These picker/dirty-state presentation changes belong to Claude.
- **Read actual ability:** `NativeTechniqueLoadout.Ability(content, map, spec, "q"/"e")` returns the technique catalog row only for the selected slot; otherwise the unchanged original row. Its catalog `row.slot` is `"technique"`; the HUD/input slot comes from the query, not that property.
- **Validation:** `Allowed(spec,slot)` for the picker; `Normalize(map)` bounds and allowlists stored/profile input, de-duplicates in fixed weapon order. Oversized incoming host profiles are refused; frame `Valid` rejects noncanonical/unknown choices. Choices can change only through the existing lobby profile path, never during combat.
- **Cooldown/cost:** no `TechniqueReady`, no key 1, no additional charge pool. The selected slot keeps the technique's own table cooldown and cost, with the existing cost/cooldown chip multipliers. Recasts never reset the deadline or pay again. The unselected ability remains intact. Bots always use defaults.
- **Persistence:** local preferences and account profile save/load wired. The account-service sanitizer source also retains the field; deployment of that service change is required for cross-device persistence. No live service deployment is claimed by this source PR.
- `Actor.Technique` and all fields in the animation contract above are unchanged. The Dev Sandbox preview remains available independently of loadout.

3. Inspect first-person, guest and replay movement/poses in Unity, especially the collision-shortened leap/grapple and Shade's delayed exchange. The implementation adds real movement and hit timing; the prior preview review does not validate that integration. Done for third and first person on the host (see the review above); guest and replay still open.

Regression gates: `CoreTests --technique-rules` (simulation contracts), `CoreTests --techniques` (existing presentation plus rule checks), full CoreTests; `NativeArena.ValidateTechniques` and the extended native network gate run in the Unity builder. Semantic compilation in a managed environment does not execute those native PhysicsScene/JsonUtility gates.

### Batches 4–5 rules checkpoint (2026-11-05, draft PR #338)

Task branch now implements all three third-set rows on sword, shield, greatsword, rifle, bow and water: **26/36 overall**. Adds Iaido, Challenge, War Cry, Sunder, Ghillie, Flash Freeze, Lodestone, Blade Drag, Breacher Round, Bramble Arrow, Tether Arrow and Bubble Trap. Ten other-weapon rows remain gated; Third stays separate from All. DEV_LOG records tuning, collision decisions and unrun native tests. These are source changes, not a released build.

Minion victim marks are `NativeMinion.TechniqueMarks`, wire `NativeMinionFrame.techniqueMarks`; pilot/replay marks remain `Actor.TechniqueMarks`. Both use the bounded mark contract. No presentation implementation is included.
