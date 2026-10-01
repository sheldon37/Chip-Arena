# Siren DEV boss: contract and wiring

Owner request (2026-10-01): a playable DEV boss (`siren`, one per match) with its own intro, stage ("Ward Zero") and
boss theme ("Black Siren Protocol"). The theme starts at Siren's intro (always the last intro) and plays through the
whole match. The work is split so two branches merge cleanly:

| Branch | Owner | Scope |
| --- | --- | --- |
| `codex/siren-boss` | Astra | Simulation, input, networking, music sync, HUD, DEV overlay, audio wiring, tests, and all edits to existing files |
| `claude/visual-revision-siren` | Claude | Model, animation, VFX, intro scene, stage design (`src/client/siren/**`, `tools/siren-lab/**`) |
| `claude/siren-contract` | Claude | The shared contract both branches start from (this commit) |

Contract files (Claude owns them; request changes through DEV_LOG rather than editing):
`src/shared/siren-contract.ts`, `src/shared/boss-music.ts`, `src/shared/siren-stage-layout.ts`,
`src/client/siren/index.ts` (the visual façade), `tests/siren-contract.test.ts`, this document.

## Snapshot fields the systems add
Use exactly these names; the visuals read them through the accessors in `siren-contract.ts`.

- `PlayerView.siren?: SirenPlayerState`: mode, `modeAt`, the current `move` (absolute times `at`/`hitAt`/`endAt`/`until`, `seq`, `side`, `dir`, `aim`, `target`, `caught`/`caughtAt`/`crushAt`), and `ready` times for the HUD.
- `Snapshot.siren?: SirenWorldView`: fire `patches`, `eruptions`, `rains`, `marks`, `grabs`.
- `Snapshot.projectiles[i].siren?: SirenShot` (`hellfire` | `damnation` | `drop` | `seeker`; drops carry `land`/`landAt`, seekers carry `target`).
- `Snapshot.bossSong?: BossSongState`: set the tick the Siren intro starts, kept across rounds and sets.

Write the absolute times the gameplay actually uses into `SirenMoveState`. `SIREN_TIMING` holds the base values,
and the animation follows whatever you write. Spawn projectiles at `sirenPoint(pos, yaw, SIREN_SPAWN.*)` so they leave the
hands the animation shows. Emit the events listed on `SirenEventType`.

## Client wiring (Astra)
- **Rig:** when `p.spec === 'siren'`, build the normal rig and call `sirenify(rig)`. Rebuild the rig when a spec
  changes to or from `siren` (add the spec to the rig cache key). `buildWeapon(..., 'siren')` should build nothing.
  Leave Siren players out of the `ArmorGlow` actors (the black body must not get coloured halos); `SirenEffects`
  hides any halo that slips through, but only if it runs after `ArmorGlow`.
- **Animation:** call `animateSiren(rig, p, state, time, dt, pitch)` right after `animateCharacter` for Siren players.
  It writes every joint (floating stance, all ten moves, hit flinch, guard); knockdowns and juggles keep the shared motion.
- **First person:** call `sirenFirstPerson(firstRig, self, state.time, dt)` every frame after `poseFirstPersonView`.
  For a non-Siren player it restores the normal arms, so it is safe to call unconditionally.
- **Effects:** create `SirenEffects(scene)`. Call `update(state, visualTime, players, effectsQuality, camera)` after the
  per-player loop, and `event(e, visualTime, effectsQuality)` in the event loop. Skip the default projectile mesh
  (and trails) for shots where `sirenEffects.owns(shot)` is true. Call `clear()` on match reset.
- **Intro:** in `PlayerIntros.update`, for style `dev-siren` set `camera.aspect`, then call
  `sirenIntro.update(age, rig, camera, quality)` as the last step every frame (it takes over the intro scene's lights,
  background, fog and camera while active). Otherwise call `sirenIntro.setActive(false)`. Skip the generic intro decor,
  `introCamera` and the intro `ArmorGlow` for `dev-siren`.
- **Stage:** map `siren` uses `sirenStageBlocks()` (route it in `arenaBlocks`, no vertical buildings) and the
  `SIREN_STAGE_THEME` values for its `MAPS` entry. Create `SirenStageVisuals(scene)`; `setActive(map === 'siren')` and
  `update(state, visualTime, effectsQuality, camera)` each frame (the camera lets it hide the ceiling for overview shots).
  While active it dims the arena's night lights and restores them on `setActive(false)`.
- **Black Rain:** Ward Zero has a ceiling grid at about 9 m; spawn the drops around 8 m (the storm is drawn just
  under the ceiling) and give each drop its `land`/`landAt` so its red floor marker is right.
- **Stage hazards:** `sirenStageHazards(songTime)` returns the beat-synced vents (positions also in `SIREN_VENTS`).
  Apply contact damage and launch the same way as `StageHazards` (per-hazard, per-player hit cooldown, only in the
  fight phase).
- **Deaths:** spread `SIREN_KILL_EFFECTS` into `KILL_EFFECTS`. Last Laugh kills use `siren-crush` with
  `gibPart: 'head'`; Siren's own death uses `siren-dissipate`.
- **Audio:** `markBeeps(lockAt, throwAt)` (exported by the façade) returns the lock-on beep times; play the beeps at
  those times so they land on the crosshair pulses. Owner decision (2026-10-01): no voice lines and no laughter. The
  Siren smiles silently (intro `smile` cue, Last Laugh windup); `siren-laugh` and `siren-mark` are timing cues only.
- **Last Laugh cutscene:** `SirenCutscene` (`renderer.sirenCutscene`). Each frame the renderer calls
  `check(state, me)` before choosing the camera; while it returns true, `update(state, camera, players, occlusion)`
  owns the camera (after the cosmetic preview, before spectator and gameplay cameras), the first-person view model and
  overhead health bars are hidden, `main.ts` sets `#ui[data-cutscene="1"]` (the CSS fades the HUD and combat numbers)
  and mouse look is ignored so the facing can't change under a directed camera. Only the held player and the Siren's
  player get it; everyone else keeps playing. Camera and presentation only: no gameplay, timing or network change.

## What the visuals do
- **Apparition:** the standard skeleton re-skinned as living black flame: near-black body with a pale burning rim,
  flame tongues on the crown, shoulders and back, legs that thin into flickering wisps, a hover of 0.24 m. Big blank
  white eyes that narrow into a slanted, angry shape when it attacks or smiles, and a jagged white grin of interlocking
  teeth that parts in its silent smile. Long hooked black claws in melee mode; in ranged mode the claws retract and the
  palms ignite into black-fire orbs.
- **Motion:** a hunched floating stance with the head cocked hard to one side, psychotic head twitches, a lean into
  movement instead of steps. Every move follows the absolute times in `SirenMoveState`.
- **Effects:** three-streak claw slashes that leave small flames burning in place, the Grave Slam shockwave and floor
  cracks, the Shadow Rush flame trail, fireballs with violet-lit black coronas, ten-second fire patches, the Damnation
  charge and explosion, eruption warnings (red) and black-flame geysers, the Black Rain storm with a red marker under
  every drop, the Marked crosshair and its glowing seeker, the Last Laugh hold (victim lifted at the claw tips,
  struggling and charring) and the head crush, and the Siren's own death (it burns away from the feet up; the eyes and
  grin go last).
- **Last Laugh cutscene** (letterboxed, from the catch): the lift in low profile; a close-up (the victim sees the
  grin from below, the Siren's player sees its own grin from above, past the victim's head); the grip as an eye-level
  two-shot that creeps in and trembles; the crush from high on the front right with a jolt. The victim's cutscene
  continues 1.75 s past the crush as their death camera (the body drops, the camera cranes up and back); the Siren
  player's ends on `releaseAt`, when it can move again. A hold broken before the crush cuts straight back. Every shot
  stays on the Siren's right so screen direction never flips; the camera eases after the Siren's facing.
- **Intro and stage:** see `SIREN_INTRO` and `siren-stage-layout.ts`; Ward Zero's lights, beacons, painted grins,
  giant eyes and containment circle follow the song's beats and sections.

## Siren lab
`tools/siren-lab` renders the real Siren code as frame strips (dev only, not built). With `npm run dev`, open
`/tools/siren-lab/index.html?move=rend&frames=6&span=1` (rig), `?kind=intro&aspect=1.778`, `?kind=stage&views=back,cells`,
`?kind=fp&mode=ranged`, `?move=idle&stage=1&song=141` (the Siren inside Ward Zero at song time 141 s), or
`?move=laugh&stage=1&cutscene=victim&times=3.4,4.9,5.6,7.1&aspect=1.778` (the grab cutscene as that participant). The header of
`lab.ts` lists every parameter.

## Music
`boss-music.ts` holds the detected beat map (the tempo drifts, so never use a BPM), sections, the intro window (bars 1–4,
7.2678 s) and the loop (bar 45 to bar 105). Song time is `sim.time - bossSong.startedAt`; apply `loopedSongTime` before
any lookup. The client player should start the buffer at the looped offset and re-seek on a bar boundary when drift
exceeds ~40 ms.
