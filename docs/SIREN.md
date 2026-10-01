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
- **Animation:** call `animateSiren(rig, p, state, time, dt, pitch)` right after `animateCharacter` for Siren players.
- **First person:** after `poseFirstPersonView`, call `sirenFirstPerson(firstRig, self, time, dt)` when `self.spec === 'siren'`.
- **Effects:** create `SirenEffects(scene)`. Call `update(state, visualTime, players, effectsQuality, camera)` after the
  per-player loop, and `event(e, visualTime, effectsQuality)` in the event loop. Skip the default projectile mesh
  (and trails) for shots where `sirenEffects.owns(shot)` is true. Call `clear()` on match reset.
- **Intro:** in `PlayerIntros.update`, for style `dev-siren` call `sirenIntro.update(age, rig, camera, quality)` as the
  last step every frame (it takes over the intro scene's lights, background, fog and camera while active). Otherwise
  call `sirenIntro.setActive(false)`. Hide the generic intro decor for `dev-siren` as for the other DEV intros.
- **Stage:** map `siren` uses `sirenStageBlocks()` (route it in `arenaBlocks`, no vertical buildings) and the
  `SIREN_STAGE_THEME` values for its `MAPS` entry. Create `SirenStageVisuals(scene)`; `setActive(map === 'siren')` and
  `update(state, visualTime, effectsQuality, camera)` each frame.
- **Stage hazards:** `sirenStageHazards(songTime)` returns the beat-synced vents. Apply contact damage and launch the
  same way as `StageHazards` (per-hazard, per-player hit cooldown, only in the fight phase).
- **Deaths:** spread `SIREN_KILL_EFFECTS` into `KILL_EFFECTS`. Last Laugh kills use `siren-crush` with
  `gibPart: 'head'`; Siren's own death uses `siren-dissipate`.

## Music
`boss-music.ts` holds the detected beat map (the tempo drifts, so never use a BPM), sections, the intro window (bars 1–4,
7.2678 s) and the loop (bar 45 to bar 105). Song time is `sim.time - bossSong.startedAt`; apply `loopedSongTime` before
any lookup. The client player should start the buffer at the looped offset and re-seek on a bar boundary when drift
exceeds ~40 ms.
