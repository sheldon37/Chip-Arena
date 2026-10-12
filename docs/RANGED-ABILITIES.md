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
