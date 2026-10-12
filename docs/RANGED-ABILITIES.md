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

## Bow presentation (Claude, `claude/bow`)

Owner (2026-10-12): "For ranged i want to add a "Bow" class." Chosen feel: "Tap or hold-to-draw" (rules in
`docs/NEW-KITS.md` section 2; Astra builds them). This is the presentation only. It reads the replicated data in one
place, `Core/NativeBowWire` (`Core/NativeBow.cs`), now wired to Astra's fields (PR #141; the provisional fixture slot is
gone).

### Field map (`NativeBowWire`)

| Presentation reads | Source |
| --- | --- |
| draw start | `Actor.BowDrawAt` (-1 while not drawing) |
| release time, draw at release | `Actor.BowReleaseAt`, `Actor.BowDraw` (not `LastShot`: every Volley and Rain arrow stamps it) |
| full draw time | `NativeBow.FullDraw` (.8 s) / `Actor.CastRate` |
| let-down (no shot) | watched: a draw start cleared with no release |
| Volley cast | `AbilityId` `volley` + `AbilityAt` (= `AbilityRelease`: the five arrows leave at once) |
| Rain cast, sky shot, target and radius | `Actor.BowRainAt`; the sky shot `RainPre` (.16 s) later; `Actor.BowRainTarget` (ground point); `NativeBowRules.RainRadius` |
| arrow kind and draw | the bullet's own `Detail` / `Draw` (host), else its Shot event: Detail `bow` / `bow-full` / `volley` / `rain`, `Draw`, `Projectile` = the bullet id |
| arrows in flight | bullets with `Spec == "bow"` (an unseen shot's draw comes from its launch speed, 45-110 m/s) |
| stuck arrows | Hit / Headshot events by a bow pilot (point, flight direction; the carrying joint is the nearest body joint); prop `Impact` events; an arrow gone from the shots with no hit near it lodged in the static world (raycast) |

### What it draws

- **Models** (`Core/NativeWeaponModels.Bow.cs`, banked by `Runtime/NativeBowPresentation.cs`): an original
  procedural recurve (riser, limbs that flex about their pockets with the pull, a string bent to the nock), at
  2058 / 934 / 480 triangles (Full / Reduced / Low). The Prototype Series bow is 228 triangles, pinned by hash.
  Arrow 180 / 132 / 82, quiver 736 / 480 / 224 (Prototype 60 / 120). The arrow line is beside the riser
  (`ArrowX`/`ArrowY`), brace at -.125, full draw at -.82. The chip modules sit about the arrow line ahead of the
  riser, with the enchant flames on the nocked arrowhead. The skin selector covers Standard and Prototype (`bow`
  is in `NativeWeaponSkins.Weapons`).
- **Poses** (`Core/NativeBowMotion.cs`): the ranged gait route with the pack's 2Hand-Bow travel loops
  (`Core/NativePackBow.Generated.cs`, derived numbers in `tools/pack-bow-provenance.json`). On top of it, a
  bladed archer's torso and a push-pull draw on the arrow line: the string hand reaches the cheek anchor at full
  draw. It covers the quick tap, the hold with a fatigue tremor after 2.5 s, the release follow-through past the
  ear, and the nock: one smooth stroke over the right shoulder to the quiver, the arrow drawn out and set on the
  string. Also the let-down, the Volley's canted five-arrow fan draw and the Rain sky shot. The aim is the
  replicated look in every camera mode. First person runs the same phases on the view rig, the arrow straight
  down the view.
- **Effects** (`Core/NativeBowFx.cs`): the arrow model with a trail that brightens with the draw, the full-draw
  glint and lock star, the string snap, Volley's fan streak, and Rain of Arrows. The Rain marker is upright light
  posts round the circle with chevrons above it: under the owner's rule nothing flat glows on the floor. Rain also
  has the sky volley, twelve arrows falling (unless the simulation's own rain arrows are seen), ground hits and
  arrows left standing 3 s, and a slow tint of frost motes. Stuck arrows stay 6 s in walls (fading over the last
  .4 s) and in pilots until respawn or the round's end. They ride the struck joint on the rig or the corpse. The
  pool holds 128 (48 on the low tier). Reduced effects and the low tier thin the particles.
- **HUD** (`Runtime/NativeBowHud.cs`, `Core/NativeBowHud`): a segmented draw meter under the crosshair (the
  quick-shot mark, brackets closing on the crosshair, a magenta full-draw lock flare, amber to red as the arm
  tires, the released fill fading). The Q / E cards use the kit icons `ability-volley` / `ability-rain`. The
  class/weapon icon is `spec-bow` (`tools/export_class_icons.mjs`). The Locker shows the role line and the
  ability icons.
- **Sounds** (existing bank keys only): the draw creak `structure.creak`, the release thwip
  `melee.daggers.throw` (pitched by the draw), the thunk `impact.wood` into props and the static world.
- **Arrowfall** (`weapon-bow`, `Core/NativeBowEntrance.cs`): the Bow's signature entrance. A native keyed
  entrance with the Bow's own arms: reach to the quiver, nock, turn side-on, draw at 42 degrees, hold, loose
  into the sky in slow motion, the split overhead, a ring of arrows landing round the pilot, the hero frame among
  them. Beat marks (match age = song time, 0.4 s grid): .4 hand at the quiver, .8 arrow on the string, 1.0 draw,
  1.6 full draw (glint), 2.0 release (the hit; slow motion to 3.04), 2.4 split, 2.8 volley lands (stragglers to
  3.0), 3.2 hero frame, 4.8 hand-off. Music key `intro.weapon-bow` (`K(...,F,4.8f,H)`). Until it is licensed,
  the built-in sting is the 150 bpm `neon-drop` bed. The account service does not know the id, so it stores
  `neon-drop`, and the client remembers the pick locally, as with the procedural entrances.

Animation impact: none on the rules. No hit timing, hitbox, damage, cooldown, movement, projectile or network
change. The presentation follows the replicated times, and every peer draws the same frame.

### Notes for Astra (fields the presentation needs)

1. The bow's actor fields: draw start, draw fraction at release (release time `LastShot`), optionally the
   let-down time, the Volley / Rain cast and release times (or the `AbilityId` stamp), and the Rain target and
   radius. Rename freely: tell me the names and `NativeBowWire.Of` follows.
2. Shot events per arrow: Detail `bow` / `bow-full` / `volley` / `rain`, `Amount` = draw fraction,
   `Projectile` = the bullet id; bullets with `Spec` `bow`.
3. Whether the Rain arrows are simulated as shots (then the presentation draws them as shots and skips its own
   fall), and how a Rain slow shows (a Hit Detail `rain`, or a slow-until field, for an exact slow tint).
4. Content: the `bow` weapon entry (class ranged, secondary `rain-of-arrows`, colour 0x7fd36b), the `volley`
   (q) and `rain-of-arrows` (e) abilities, and the weapon id the actor carries. The presentation keys on
   `SourceSpec == "bow"`.
5. Accounts: `src/relay/accounts.ts` `SKIN_WEAPONS` needs `bow`, so a Prototype pick persists on the account.
   The intro validation needs `weapon-bow` to store Arrowfall, so `NativeIntroRules.ServiceIntro` can drop its
   exception.

### Verification boundary

`Tests/BowChecks.cs` covers: model budgets, tiers and determinism (the Prototype pinned), sockets, the string and
arrow following the pull, the third- and first-person aim converging within 1 degree, the arms reaching, no hand
jump, replicated-data-only keying, the stuck-arrow lifecycle, the effects and the floor rule, the HUD, the
Arrowfall beats, the pack provenance, the runtime hooks and no per-frame allocation. Offline renders (scratch
harness, real Core and VFX code over a stub) show the pixels. Not run: the Unity Editor, Windows play, multiplayer
with the real rules (they do not exist yet).
