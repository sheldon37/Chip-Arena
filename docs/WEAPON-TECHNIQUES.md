# Weapon techniques (key 1): a third ability for the twelve standard weapons

Owner request, 2026-11-03: "Lets add more abilities now for the weapons. Think creatively, integrate more skills that aren't too
much of a "powercreep" but still useful and fun. Ensure the animation and effects are high-quality; Make sure you evaluate
afterwards to make sure things look great."

Status: design and presentation by Claude (`claude/weapon-techniques`); simulation not started, for Astra (AGENTS.md role split).
Until the rules exist the techniques show only in the Dev Sandbox's "Preview Techniques" (below). DEV classes (Siren, Emperor,
Ansatsuken, L33T, Cyber Lord) are out of scope.

## Why it isn't power creep

Every weapon already has its damage on LMB, Q and E, so a technique is not more damage. The rules of the set:

- **Utility first.** A technique changes where someone stands or what they can see or do. The most any deals is 22, under a Q's
  budget (30), and most deal none.
- **Long cooldowns.** 9 to 20 s, longer than the same weapon's Q.
- **A visible cost or a counter.** Each row below names how an opponent answers it. Nothing is an untelegraphed kill.
- **Same resources and rules.** Melee pays stamina, magic pays mana, guns pay cooldown only. The cooldown and cost chips apply as to Q
  and E. A stun, knockdown or roll refuses it as it does Q and E.

## The set

Key **1** on keyboard, the existing `PadAction.Slot1` on a controller. `InputFrame.SirenAbility` already carries the press on the wire.

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
are on the presentation clock. Add the content rows (`slot: "1"`, `tools/native-content.json`), a `TechniqueReady` next to
`QReady`/`EReady` on `NativePilot`, the bot use, and the checks. Set `NativeTechniques.Source` from the snapshot and presentation takes
it from there. Drop the preview when it is done, or leave it behind the sandbox flag.

Common fields: `Kind` (the id), `Phase`, `At` (the press), `Release` (when the move leaves the pilot), `Until` (the effect's end, moved
earlier on a recast), `RecoverUntil` (the pilot's own lock), `HitAt` (-100 until the triggered moment).

| Kind | Phases | `From` | `Point` | `To` / `Dir` / `Arm` | Triggered moment |
| --- | --- | --- | --- | --- | --- |
| `riposte` | guard, counter, recovery | | the contact point | `Dir` attacker to you | `HitAt`: the parry |
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
| HUD card, drawing, models | `Runtime/NativeFightHud.cs`, `Runtime/NativeTechniquePresentation.cs` |
| Checks | `Tests/TechniqueChecks.cs` (`CoreTests --techniques`) |

To see one: the Dev Sandbox, Preview Techniques, with the weapon in hand. The preview loops the weapon's technique from just in front of
the pilot, since it does not know the world's surfaces or move anyone.

Reviewed in Unity, in slow motion from the game's own camera (Cybercity, local tool: `gallery=T,TechniquePreviewOn`, `slowmo=T,.2`, `screen=1`), all twelve,
with two rounds of fixes: the guard from a glowing panel to thin arcs, the wall from a framed rectangle to a honeycomb, the planted shield and
the thrown blade onto the pilot's own weapon model, the coin's glow down so the coin reads, the fire from washed-out to hot, the wind burst stronger. Not seen yet: any of it in
real play with the rules, in first person, in a crowd, on a guest, or on a low graphics tier in Unity (the checks cover the thinning).
