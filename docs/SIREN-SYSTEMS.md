# Siren systems handoff

Status: implemented on `codex/siren-boss`; not merged or published. Base is
`cf5fa1c5548e66d1364c98f9ea053202f918ec50` on `claude/visual-revision`, followed by
Claude's shared contract `84b692ffffb01618d2397175606d83597a878e9f`.

## Integration with Claude

Claude's contract, beat map, stage layout, façade, contract tests and `SIREN.md`
are unchanged. This branch calls the façade from world/first-person/lobby rigs,
intro staging, projectiles, effects and death presentation. Ordinary melee pose
evaluation is bypassed for Siren because it assumes one of the four standard
weapon animation tables. Claude's `animateSiren` owns the pose completely.

The contract façade currently has minimal implementations; its presence is not
evidence that finished animation, warning markers, grabs, intro, stage dressing
or deaths have shipped. Merge Claude's visual branch and perform the Windows
checks below before publication. No additional placeholder visual implementation
was authored on this branch.

`MAPS`/`ROTATION_MAPS` retain the 14 public arenas. Siren is registered in
`BOSS_MAPS` and `ALL_MAPS`; `mapId`/`arenaMap` recognize it. This preserves every
existing normal-rotation consumer, while `arenaBlocks('siren')` exclusively calls
Claude's layout (no generic vertical buildings).

## Selection and authority

- The fourth selection category is `dev`, displayed as **???**, and visible only
  with verified DEV access. All current DEV weapons appear there. Weapons retain
  their original melee/magic resource class; old saves and exact item grants
  still work in their original categories. Item grants do not confer DEV status.
- `weapon:siren` and `intro:dev-siren` are registered grants. Existing account and
  room sanitization reject a forged DEV category. Bots cannot acquire Siren from
  lobby options. The authority checks the one-connected-Siren limit in saved
  loadouts, class changes, specialization commands and sandbox spec changes;
  disconnect frees the slot. The selection button displays TAKEN.
- Siren forces FFA and Ward Zero at match start/intro. Lobby return restores the
  previous map/mode. Normal matches and random rotation exclude Ward Zero.
- Intro is always last and overrides cosmetic selection. Shared contract values:
  7.2678 s duration, 6.4261 s name card (the brief's rounded 6.43 s).

## Combat

`src/shared/siren.ts` owns authority logic; `siren-content.ts` owns tuning;
`siren-motion.ts` is shared with local prediction. Input fields are optional
`sirenToggle` and `sirenAbility` (1–4). Q toggles; F or V are selectable in settings.
Rebinding to V suppresses the ordinary camera toggle for Siren. Holding a mode or
ability key does not repeat it. LMB repeats after recovery. Ansatsuken keeps 1/2.

| Mode | Input | Move | Base damage | Ability cooldown |
| --- | --- | --- | --- | --- |
| Melee | LMB | Alternating Claw Swipe | 9 | Shared 0.40 s move |
| Melee | 1 | Rending Claw, knockdown | 22 | 5 s |
| Melee | 2 | Grave Slam, launch | 16 | 7 s |
| Melee | 3 | Shadow Rush, 6 m path and fire trail | 14 per target | 6 s |
| Melee | 4 | Last Laugh, dodgeable grab/head crush | Instant kill after catch +3.2 s | 30 s |
| Ranged | LMB | Hellfire, impact patch | 10 | Shared 0.42 s move |
| Ranged | 1 | Damnation, arcing explosion/falloff | Up to 30 | 6 s |
| Ranged | 2 | Eruption, 1 s warning and launch | 28 | 5 s |
| Ranged | 3 | Black Rain, deterministic falling projectiles for 10 s | 9 per drop | 18 s |
| Ranged | 4 | Marked, 3 m/s homing projectile | 85 | 35 s |

Damage respects damage/damage-taken multipliers, collision, visibility, block,
roll/Demon Step invulnerability and existing grounded protection. Last Laugh's
successful head crush intentionally bypasses damage mitigation/extra HP; killing,
disconnecting or interrupting Siren releases the victim. Siren's capsule uses
standard gravity/locomotion; Claude supplies visual hover. Committed moves root
the player except rush/lunge; shared snapshot replay respects those movements
and freezes captured victims. Siren does not use ordinary weapon swings.

Cooldowns and shared move durations remain fixed across mode swaps. Siren's
custom kit does not promise generic weapon-chip procs (extra strikes, attack-rate
timing, multishot, etc.); those need explicit future balancing. Fire is 5 DPS while
inside a patch, not a lingering debuff, sampled every 0.25 s. Overlapping patches
from the same Siren do not multiply damage. Hellfire patches cap at 12; rush trail
segments cap at 24. Marked allows one active lock/seeker, removes destructible
props in its swept path and expires on foundation cover or target contact.
Death/spec/disconnect cleanup and per-round reset clear temporary combat effects.

## Music and effects

- The original owner MP3 is included unchanged at
  `public/music/black-siren-protocol.mp3`; credit: **Xtrr - musiccreator**. Its SHA256
  is `5b2cdc75fef5c5087f6ecff2f13ccf1e0632937195e6427536b1f3cde18f213e`.
  No audible tracking-click copy is needed for playback.
- `bossSong.startedAt` is authoritative and starts at Siren's entrance (countdown
  if skipped); it survives all rounds/sets, clears at match end/lobby, and is
  included in snapshots. Sandbox Siren starts it when combat begins. Preview
  audio uses its preview timeline without mutating authority.
- `BossTheme` uses the existing AudioContext, original detected timestamps and
  shared loop. It seeks late snapshots, corrects >40 ms drift at bar boundaries,
  respects music volume/mute/ducking, and suppresses ordinary map music. Intro
  audio excludes `dev-siren` stings so the full boss song is the only music there.
- DEV debug overlay shows song time, beat, bar.beat, section, measured drift and
  vent events. Beat-driven damage uses authority time and Claude's stage hooks.
- Optional `public/audio/siren-laugh.mp3` and `siren-you.mp3` remain owner-supplied;
  missing clips fail silently. Accelerating lock beeps are synthesized.

## Verification and remaining checks

Linux Node 24.19.0 / npm 11.9.0:

- Typecheck passed. Focused Siren/rig checks: 37/37 passed.
- Final full unit run: **587/599 passed**, with exactly the **12 failure names**
  reproduced on clean base `cf5fa1c` (555/567). No newly failing tests.
- Client and HTTPS relay compilation passed. Server compilation retains the four
  known Rapier/NodeNext errors; unrelated PR #2 remains separate.
- Full `npm run verify` was attempted. Initial integration issues found in its
  type/unit stages were fixed and those stages rerun as above. Generic WebSocket
  suite retains its recorded rematch timeout. HTTPS/in-memory SQLite integration
  passed: five sets, four drafts, 3,497 agreed snapshots, rematch, retry and leave
  (177.6 s). Overall verification is **not green**.
- `node --import tsx tests/siren-network.ts`: three real loopback WebSocket
  clients, forged-access and duplicate selection refusal, shared damage/mode/
  Marked seeker, song continuity, **80 identical snapshots**.
- Existing tests were adjusted only for the new intro count, separate Siren
  camera/song/hover paths, and authorized DEV primary fixtures. Public map tests
  keep their original 14-map semantics. Contract files/tests were not changed.

Not performed: Windows Electron/GPU execution, two physical PCs, audible
headphone sync assessment, finished visual quality, or packaging/updater tests.
There was no merge, channel upload, live feed change or Site publication.

Windows acceptance after merging both branches: confirm ??? is DEV-only; try two
DEVs selecting Siren; check solo taps/hold and Q/1–4 in both modes, wall collisions,
Last Laugh evade/catch/kill-owner release, Marked through props/behind foundation,
warning markers before eruption/rain, first-person and lobby appearance, intro
last, preview, music mute/ducking/loop and continuous song across sets. Check the
DEV beat overlay for stable drift on both PCs. Owner controls merge/publication.
