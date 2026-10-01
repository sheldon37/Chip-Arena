# Siren systems handoff

Status: PR #11 merged into `claude/visual-revision` at `fd67313` on 2026-10-01;
test content `0.2.c452eefcbbac` published from that commit to the existing test
Update channel at `79e939f`. Verified follow-up implementation: `8979723`.
Original PR #8 merged at `9f010f5`; verified implementation: `1a8a5d0`. Base is
`cf5fa1c5548e66d1364c98f9ea053202f918ec50` on `claude/visual-revision`, followed by
Claude's shared contract `84b692ffffb01618d2397175606d83597a878e9f`.
PR #8 combines systems `514d784` with Claude's visual branch at `2e986f4`
(including its first-pass and polish commits), plus the integration fixes below.

Follow-up PR #11 (`codex/siren-cutscene-dev-bots`) incorporates Claude's cutscene commit
`452bbe6`, removes the dropped voice clips, adds DEV-configured bot weapons and
bot ability decisions, and raises Siren to 15,000 base HP. These changes are included
in the published test content above.

## Integration with Claude

Claude's visual branch and updated `SIREN.md` handoff are integrated into PR #8.
Its owned source files, shared contract, beat map, stage layout and visual tests
are preserved exactly as supplied at `2e986f4`. The DEV_LOG merge preserves both
agents' entries. No additional visual assets were authored by Astra.

The world/first-person/lobby rigs, intro, projectiles, effects and deaths now call
the completed first-pass façade. Siren is excluded from ordinary armor halos;
effects run after armor glow and camera positioning. Ward Zero updates after the
arena and camera, so light dimming and ceiling visibility use the current frame.
The intro skips the generic camera/glow, preserves viewport aspect and ends with
Claude's scene update. Ordinary melee pose evaluation remains bypassed for Siren.

First-person rigs rebuild only on transitions into/out of Siren. The original
integration checked for a world-rig marker absent from the first-person façade,
which would have rebuilt the arms every frame. A real renderer view-model test
now covers 120 frames plus switching back to a visible ordinary weapon.

Black Rain starts 8 m above its landing point (previously 9 m), below the storm
on the standard floor. Its vertical speed is 10 m/s (previously 11.25); the
0.8-second warning/fall time, damage, cadence and cooldown are retained. Marked
uses Claude's `markBeeps` pulse times with an 80 ms audio scheduling window,
deduplication and cancellation of interrupted locks. Per the owner's later
decision, the intro and Last Laugh are silent smiles: no voice files are fetched
or played. The obsolete intro-laugh test and event audio hooks are removed.

Claude's `452bbe6` adds the Last Laugh camera sequence for its two participants,
letterboxing, HUD/view-model hiding and mouse-look suppression. The cutscene ends
at release for Siren and continues briefly after the crush for the victim;
interruption restores the camera. Other players retain their normal view.

Combined headless checks pass for the Siren feature, but these are not an in-game
visual/GPU or audible-sync acceptance test. The owner authorized test publication
to perform the Windows checks below; those checks remain pending.

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
  room sanitization reject a forged DEV category. Bot loadouts show the ??? group
  only to verified DEV accounts, in local practice and the room owner's lobby.
  The authority derives permission from the actual configuring player, never
  request fields or another DEV guest. Each chosen bot receives only its specific
  weapon grant, without a DEV role or account identity. Random picks remain ordinary.
  The authority checks the one-connected-Siren limit in bot loadouts, saved
  loadouts, class changes, specialization commands and sandbox spec changes;
  disconnect frees the slot. The selection button displays TAKEN.
- Siren forces FFA and Ward Zero at match start/intro. Lobby return restores the
  previous map/mode. Normal matches and random rotation exclude Ward Zero.
- Intro is always last and overrides cosmetic selection. Shared contract values:
  7.2678 s duration, 6.4261 s name card (the brief's rounded 6.43 s).

## Combat

Siren has **15,000 base HP** for humans and bots. Existing HP Chips apply afterward
with their normal additive/multiplicative effects. Selection, saved loadouts,
rounds/rematches and sandbox Chip/spec changes rebuild the correct maximum.

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

All active bot difficulties can use weapon abilities through normal player inputs.
The AI chooses by range, visibility, resources and cooldowns; releases primary
during recovery so it cannot starve a special; and moves close-range casters into
their ability radius. Ordinary E abilities, Emperor electric/wavedash, Ansatsuken
Hadoken/uppercut/Tatsumaki, L33T Root Command, and Siren's mode switch/eight abilities
are supported. Dummy bots remain inactive. Costs, hit timing, hitboxes, damage and
cooldown values are unchanged; bot decision-making and approach behavior change.

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
- Voice lines were dropped by the owner; there are no laugh/You clip hooks or
  fetches. Accelerating lock beeps are synthesized. Shared event names remain
  visual timing cues, preserving the contract and snapshots.

## Verification and remaining checks

Linux Node 24.19.0 / npm 11.9.0:

- Typecheck passed. All Siren contract/gameplay/client/visual/integration/cutscene
  and DEV bot checks: **57/57 passed**.
- Final full unit run: **613/625 passed**, with exactly the **12 failure names**
  from prior integration `1a8a5d0` (598/610), originally reproduced on clean base
  `cf5fa1c` (555/567). No newly failing tests.
- Client and HTTPS relay compilation passed. Server compilation retains the four
  known Rapier/NodeNext errors; unrelated PR #2 remains separate.
- Full `npm run verify` was rerun for this integration; results are recorded in
  the newest DEV_LOG entry. The generic WebSocket suite retains its rematch
  timeout. HTTPS passed five sets, four drafts, 3,505 matching snapshots, retry,
  rematch and leave (177.6 s). Overall verification remains **not green**.
- `node --import tsx tests/siren-network.ts`: three real loopback WebSocket
  clients, DEV bot loadouts and guest refusal, 15,000 HP, forged-access and duplicate
  selection refusal, shared damage/mode/Marked seeker, song continuity,
  **84 identical snapshots**.
- Existing tests were adjusted only for the new intro count, separate Siren
  camera/song/hover paths, and authorized DEV primary fixtures. Public map tests
  keep their original 14-map semantics. Claude-owned files/tests match the imported visual branch exactly.

Update publication checks: two identical release builds; actual payload install
and all 102 file hashes; offline/corrupt-package refusal; 13/13 updater and channel
tests. Real network upgrade from `0.2.149269b37b5b` to `0.2.c452eefcbbac` passed,
including settings preservation, same-version skip and menu status. Original MP3
hash is unchanged. Source and channel SHAs are recorded in DEV_LOG.

Not performed: Windows Electron/GPU execution, two physical PCs, audible
headphone sync assessment, finished visual quality, or a new Windows seed ZIP.
The owner authorized the merge and this test-channel publication. No live feed
change or Site publication.

Windows acceptance for the combined PR: confirm ??? is DEV-only; try two
DEVs selecting Siren; check solo taps/hold and Q/1–4 in both modes, wall collisions,
Last Laugh evade/catch/kill-owner release, Marked through props/behind foundation,
warning markers before eruption/rain, first-person and lobby appearance, intro
last, preview, music mute/ducking/loop and continuous song across sets. Check the
DEV beat overlay for stable drift on both PCs. The owner's standing instruction
publishes completed updates to this test channel unless told otherwise; merge
and live-release authorization remain separate.
