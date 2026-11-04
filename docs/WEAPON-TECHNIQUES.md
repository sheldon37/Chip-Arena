# Weapon techniques: optional Q/E replacements for the twelve standard weapons

Owner request, 2026-11-03: "Lets add more abilities now for the weapons. Think creatively, integrate more skills that aren't too
much of a "powercreep" but still useful and fun. Ensure the animation and effects are high-quality; Make sure you evaluate
afterwards to make sure things look great."

Owner revision, 2026-11-03: techniques replace either Q or E, selected per weapon in Customize; rifle permits E only because Q is scope. Both original abilities remain the default. There is no third slot or separate technique cooldown. This revision supersedes the original key-1 proposal.

Status: playable, both sets. Astra's rules and saved choice (PRs #321, #322 and #325), Claude's Customize picker, HUD cards and presentation. One open rules bug: Parting Line (see "The second set, reviewed"). Receipts are in DEV_LOG. The sandbox preview remains optional. DEV classes (Siren, Emperor, Ansatsuken, L33T, Cyber Lord) retain their existing key-1 abilities.

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

**Open, for Astra: Parting Line snaps the pilot back on every cast.** Seen on open floor and with a pillar behind (arena clock, `body` from the log): 5.32 hopping at 3.1 m, 5.50 back at `Point+up*.9`, standing in the burst. The hop's `wall` flag sets `Phase="line"` before `Release+.28` (`NativeMoreTechniques.MoreTechniqueMotion`; most likely the arc's descent meeting the floor, not confirmed). `MoreTechniqueMotion` then returns false, `NativeTechniqueRules.Moving` is still true, and `TechniqueMotion` falls into its anchor-throw branch, whose `RecastAt` of -100 puts the destination at `Point+up*.9` at once. The fix is `k.Kind=="parting-line"&&k.Phase=="hop"&&now<k.Release+.28f` in `Moving`, which guest prediction also reads; and the arc should not count the floor as a wall, or the hop ends short. The effect itself is right; it just needs the pilot to stay out of it.

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
