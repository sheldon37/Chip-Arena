# Weapon techniques: optional Q/E replacements for the twelve standard weapons

Owner request, 2026-11-03: "Lets add more abilities now for the weapons. Think creatively, integrate more skills that aren't too
much of a "powercreep" but still useful and fun. Ensure the animation and effects are high-quality; Make sure you evaluate
afterwards to make sure things look great."

Owner revision, 2026-11-03: techniques replace either Q or E, selected per weapon in Customize; rifle permits E only because Q is scope. Both original abilities remain the default. There is no third slot or separate technique cooldown. This revision supersedes the original key-1 proposal.

Status: playable. Astra's rules and saved choice (PRs #321 and #322), Claude's Customize picker, HUD cards and presentation. Receipts are in DEV_LOG. The sandbox preview remains optional. DEV classes (Siren, Emperor, Ansatsuken, L33T, Cyber Lord) retain their existing key-1 abilities.

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
| The effects, pure functions of the record | `Core/NativeTechniqueFx.cs` |
| Poses on the model's pack clips | `Core/NativeVisualMotion.cs` (`Technique`) |
| Sounds | `Core/NativeTechniqueSounds.cs` |
| The picker in Customize (Loadout) | `Runtime/NativeLockerUI.cs`, `Core/NativeLocker.cs` (`AddTechnique`) |
| The card sentence and the HUD card's state | `Core/NativeTechnique.cs` (`Text`, `Card`) |
| HUD card, drawing, models | `Runtime/NativeFightHud.cs`, `Runtime/NativeTechniquePresentation.cs` |
| Checks | `Tests/TechniqueChecks.cs` (`CoreTests --techniques`) |

**The picker.** Customize, Loadout: each slot's row shows that slot's own ability and, beside it, the weapon's technique, with an OR between them.
The equipped card has the bright edge and the tick (a technique's in its own accent, the colour of its effects). A click swaps, flashing the card; a
technique already in the other slot says ON Q or ON E, and picking it here moves it. A swap is part of the look: Save Pilot stores and sends it,
Revert restores it. The rifle's Q row keeps only the scope.

**The HUD.** The technique takes the card of the slot it replaced: RECAST in amber while a press would recast it, ACTIVE while it runs, then that
slot's cooldown as this pilot's chips scale it, and LOW STAMINA or LOW MANA. While the shield stands as a wall or the greatsword is thrown, the rules refuse
block and the other slot, so those cards say SHIELD OUT or BLADE OUT. A press refused for where it is aimed is below. A swapped E draws no field preview for the ability it replaced. Smoke hides a
pilot's name and health bar from anyone outside the cloud.

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
