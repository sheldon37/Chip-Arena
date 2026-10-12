# Ranged Q — native simulation contract

Owner request, 2026-10-12. Implemented on `codex/unity-ranged-q-20261012`, initially based on shared `1af916c`, with shared `f026b9e` integrated. This document records the simulation handoff to Claude; matching animation, effects, audio and HUD states remain presentation work. The retained Electron game is unchanged.

## Kit and input

| Weapon | Q id | Kind | Base cooldown | Cost |
| --- | --- | --- | --- | --- |
| Pistol | `fan-hammer` | `burst` | 7 s | The fired rounds only |
| Machine gun | `overdrive` | `buff` | 10 s | None |
| Rifle | Existing scope | `ads` | Unchanged | Unchanged |

Pistol and MG use the existing one-shot `AbilityQ` input edge. The host already coalesces and consumes that edge once; holding Q does not repeat it. Both no longer enter ADS, narrow FOV, lower sensitivity or receive ADS accuracy/movement modifiers. Rifle still uses held Q and its existing scope profile. Historical ADS profiles/animation fixtures remain available as source data; live native ADS access only returns the rifle profile.

The two new Qs activate immediately, outside the delayed magic `AbilityCast` path. They refuse death/disconnection, block, roll, stun/incapacity, an ongoing cast/attack/burst, reload and Q cooldown. They do not wait for the previous gun round's firing interval. Simultaneous reload/cancel takes priority. Cooldowns start once at activation and retain the existing ability-cooldown chip modifier. No mana or stamina is charged, even with cost modifiers. Existing Q-empower chips still apply to subsequent normal rounds.

## Pistol — Fan the Hammer

- Capture `min(4, Ammo)` at activation; refuse an empty magazine or a full projectile cap without spending cooldown/ammo.
- Fire immediately, then at cast time + .075 / .150 / .225 s, as remaining ammunition allows. Host fixed ticks quantize each scheduled round by at most one tick without accumulating scheduling drift. At 60 Hz, the nominal four shots occur on ticks 0/5/9/14.
- Each round consumes one ammo and follows the ordinary pistol projectile path: damage ×.8, base spread ×2.2. Base single-projectile damage is 18.4. Usual damage, extra-projectile, last-round, Q-empower, headshot, block/deflect, roll, team, chip, cover, recoil and projectile-limit rules still apply. A volley created by an extra-projectile chip still costs one round and emits one Shot event.
- Starting the final round of an empty magazine immediately starts the normal reload, with its usual duration/modifiers and events. A full four-round burst from a seven-round magazine leaves three rounds and does not reload.
- Fan adds no new movement penalty. Held LMB cannot add another round during the burst. Ordinary fire resumes after the normal firing interval following the last shot.
- Roll, block, incapacity, reload, cancel/menu, death/disconnection or an exhausted projectile cap stops the remaining burst. Unfired rounds are retained; cooldown is not refunded. No delayed burst resumes afterward.
- Each round samples current authoritative aim and emits `EventKind.Shot`, `Detail="fan"`, source = shooter, with the usual muzzle position, weapon Amount, shot counter and timestamp. No substitute AbilityHit event is emitted for the shot.

## Machine gun — Overdrive

- Active for exactly 4 s. The ordinary firing interval is divided by 1.6, composing with attack-rate chips; the ordinary fixed-tick firing scheduler is retained. Ammo and reload work normally; Q does not grant ammo or auto-fire.
- Keep the base hip-fire spread cone with no accumulated bloom (`AimShots=0` for these rounds). This means no bloom, not zero spread.
- Multiply controlled movement speed by .8, including its usual movement modifiers. Base running speed is 5.44 m/s. The host and guest acknowledgement/replay use the same active clock. Roll movement is unchanged.
- At `OverdriveUntil`, restore normal movement and begin a .8 s overheat vent. Primary gun fire is refused until `VentUntil`, even if LMB is held and the normal firing interval is ready. Afterward ordinary rate, spread behavior and untagged Shot events resume. The separate E ability retains its existing rules; the vent does not remove already-fired projectiles.
- Reloads, rolls and ordinary interruptions do not extend or erase the buff/vent clock. Death clears it; a new round starts clear. There is no early recast and no extra resource charge.
- Active rounds are ordinary `EventKind.Shot` events with `Detail="overdrive"`.

## Replication and presentation handoff

| Field/event | Contract |
| --- | --- |
| `Actor.OverdriveAt` | Authoritative activation time |
| `Actor.OverdriveUntil` | End of the active buff, activation + 4 s |
| `Actor.VentUntil` | End of the vent, active end + .8 s |
| `NativePilot` fields | Same names/values as Actor |
| `NativePilotFrame.overdriveAt/overdriveUntil/ventUntil` | Same clocks restored on the replica pilot; snapshots also carry the Actor fields |
| `Actor.AbilityId/AbilityAt/AbilityRelease` | New ability id, activation time, immediate release at that same time |
| `AbilityCast` event | At activation, Detail `fan-hammer` or `overdrive` |
| `Shot` event | One normal shot event per round, Detail `fan` or `overdrive` as above |
| Ammo, LastShot, ShotsFired, ReloadAt/Until, QReady | Existing authoritative resource/shot/reload/cooldown paths remain in use |

Active: `OverdriveUntil > OverdriveAt && now >= OverdriveAt && now < OverdriveUntil`. Vent: `VentUntil > OverdriveUntil && now >= OverdriveUntil && now < VentUntil`. All-zero fields mean inactive, including old snapshots. New clocks are validated for finite, nonnegative and chronological values. Actor copies/recording, host/guest snapshots and late join retain the values. Render on the authoritative presentation clock and gate living-only effects by the actor's life state. Do not restart the buff or vent when a snapshot arrives.

Claude: use the per-round Shot events for the rapid pistol action; the replicated clocks for MG buff/vent pose, effects, audio and HUD. Existing cards automatically receive the new names/cooldowns; special active/vent/ammo-readiness presentation is still to be added. The input/camera gates already remove pistol/MG zoom.

Bots use the normal visibility, resource, cooldown and decision-probability checks. Fan is eligible within 16 m with ammo; Overdrive within 40 m. Their E priority and rifle scope distances remain unchanged. These are initial usage ranges, not a measured balance claim.

## Verification boundary

Managed tests cover content, scope gating, burst scheduling at 30/60/120 Hz, ammo-count boundaries, scales, buff/vent clocks, malformed clocks, copied/JSON actor state and guest movement replay. The native builder gate separately exercises the actual dispatcher, rounds, ammo, reload, interruption/cap behavior, cover/deflection, MG motion, JSON and vent firing boundaries. See DEV_LOG and the task PR for actual results. API compilation does not execute Unity/PhysX; Windows gameplay, live guests/killcam, presentation acceptance and a compiled test Update still require the existing Build-and-Publish workflow.

## Presentation (Claude, `claude/ranged-q`)

The visuals for both abilities are built on the contract above. They read only replicated actor fields, so host, guests, spectators and the killcam replay draw the same frame. Presentation only: no rule, timing, damage, cooldown, movement or wire change.

**Proposal and contract.** The proposal sent through the owner had the same rules and timings as the simulation above. It also had the same names for the Overdrive clocks (`OverdriveAt`, `OverdriveUntil`, `VentUntil`). For the fan it asked for a replicated cast time and shot count. The simulation instead stamps the cast on the pose (`AbilityId`/`AbilityAt`) and replicates the rounds through the existing ammo and shot fields. That is enough, so no extra field was added (the provisional `FanAt`/`FanShots` were dropped on merging PR #134).

### What the presentation reads (`Core/NativeGunKit.Of(actor, time)`)

| Ability | Fields | Derived |
| --- | --- | --- |
| Fan the Hammer | `Weapon` pistol, `AbilityId == "fan-hammer"`, `AbilityAt` (the cast and its first round), `LastShot`, `Ammo`, `ReloadAt`/`ReloadUntil` | Rounds fired so far: `floor((LastShot − AbilityAt)/.075 + .3) + 1`, counted only while `LastShot` is inside the four-round schedule. Volley length: the cast's magazine, `min(4, Ammo + fired)`. A reload started since the cast ends the volley. So does a round that has not arrived within .09 s of its schedule (a roll, block, stun or cancel cut the burst; .09 s covers one host tick plus a guest's 20 Hz frame). A later normal round ends the fan's presentation. |
| Overdrive | `Weapon` machine gun, `OverdriveAt` > 0, `OverdriveUntil` > `OverdriveAt`, `VentUntil` ≥ `OverdriveUntil` (finite) | Phases: spin-up (.35 s), then sustain until `OverdriveUntil`, vent until `VentUntil`, and a 1.4 s cool-down glow. Their boundaries are exactly `NativeRangedRules.Overdrive` and `Vent`. Heat rises from orange to white-hot over the active time, then turns red and cools through the vent. The barrel spin is a closed-form integral, so every peer agrees. |
| Tracers | each round's owner, `Born` and spec | A machine-gun round born inside the Overdrive window burns hotter and is drawn brighter, wider and longer (×1.5 gain, ×1.35 width, ×1.3 length). A fanned pistol round is a little warmer and wider. All other rounds keep the existing tracer. |
| HUD Q card | the kit above, `QReady`, `Ammo`, `ReloadEnd` | Fan: FANNING, then the cooldown with its dial, NO AMMO / RELOADING, READY. Overdrive: ACTIVE with time left (draining dial), VENTING with time left (hot border), then the cooldown. Icons are `hammer` and `gauge`. These replace the Locker's provisional `crosshair`/`zap`; `crosshair` was the old ADS icon and `zap` is Blink Strike's. |

The per-round `Shot` events (Detail `fan` / `overdrive`) still play the normal shot cue, muzzle star and recoil for every round. The kit's own visuals use the timeline above rather than the events, because events are transient and a late joiner or the killcam would miss them.

### What it draws

- **Fan the Hammer, pose** (`Core/NativeGunKitMotion`, reached through `RangedAbilityPose.Apply`, so it applies in both the third-person GunMotion route and the first-person view model):
  - Third person: the pistol drops to a low-chest point-shooting hold, canted.
  - First person: the pistol stays in view, lower and canted to show the hammer.
  - The off hand slaps the hammer on each round's schedule, palm down across the slide. The strike moves at the support-hand speed measured from the pack's dual-pistol volleys (8.1 m/s); it follows through down and back, then rises for the next round.
  - Each round adds the pack-derived recoil kick (`tools/derive_fan_hammer.mjs` → `tools/fan-hammer-provenance.json`: the one-handed pistol shots' gun-shoulder recoil, normalised). The kicks stack and settle, on top of the rounds' own recoil.
- **Fan the Hammer, effects**: a glint on the hammer per slap, brass flicked out of the port, and a smoking barrel for 1.8 s after the volley.
- **Fan the Hammer, first-person camera**: a camera kick per round, only with the camera-shake setting (`GunKitKick`).
- **Overdrive, pose**:
  - A braced stance: hips sink, spine and chest lean in, the gun is pulled to the shoulder.
  - The hold shakes on every round, harder as the barrels heat.
  - The vent tilts the gun onto its side, muzzle up.
- **Overdrive, effects**:
  - Six barrels spinning up with motion blur.
  - A glow along the barrel, orange to white-hot, with a hot muzzle core and the shroud warming late.
  - Additive heat shimmer and thin steam off the shroud.
  - The vent: steam jets, then steam bursts from both side vents while the glow turns red and cools.
  - First person adds a faint additive heat haze along the bottom edge of the screen, and a camera buzz under the camera-shake setting.
- **Audio** (existing bank keys with built-in fallbacks):
  - per slap: `gun.dryfire` (bank only);
  - spin-up whine: `chip.delayed.charge` (fallback: `electric-hook`, raised);
  - vent hiss: `siren.vents` (fallback: `fire`).

  The generic ability-cast cue is skipped for these two ids. Otherwise their immediate release would add a duplicate gunshot to the first round.
- **ADS**: verified (`Tests/GunKitChecks`). Only the rifle has an ADS content entry. The input gate and the `UpdateCamera` gate both test `NativeRangedRules.Scope`, so pistol and machine gun never zoom, lower look sensitivity or force first person. The rifle keeps its scope. No camera edit was needed.

### Notes for Astra

- If the fan ever needs to read exactly from the first frame (for example a late joiner arriving mid-burst), an explicit replicated round count would remove the .09 s "overdue" inference. It is not needed for the present visuals.
- Bots: the content no longer has pistol or machine-gun ADS entries. `NativeBotRules.Ads` already excludes both.
- Gameplay feel to review in play, not measured here: the off hand moves at up to about 18 m/s at the .075 s cadence. The rounds' own recoil spring (`RangedPose.Recoil`) restarts on every round, so the gun snaps a little on each one.
