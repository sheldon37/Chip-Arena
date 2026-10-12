# Chip Arena — development log

## 2026-10-12 — Claude — fluid melee swings: retimed sweeps, follow-through, real recovery, turn blend, crescent sync (owner: "it doesnt feel smooth swinging right now")
- Branch `claude/melee-feel`, base `19c00c7`; commit 2 of 2 (follows the torso commit `1903c08`), on the branch only (not pushed, merged or released). Details and numbers in `docs/MELEE-FEEL.md` (P2–P8).
- `Core/MeleeMotion.cs` `MeleeSwing` (shared by the third-person rigs, the first-person view model in `Runtime/CapturedAnimation.cs` and the slash crescent): each captured cut plays its authored sweep linearly across the hit window (sword mean tip speed in the window 18–28 → 51–61 m/s, greatsword 19/31 → 37/63, peaks now inside it); the windup keeps the cut's pace keys up to the sweep (sword cut B half even); the daggers start after their wind-back (windup compression ×4.7–5.7 → ×2.5–3.0).
- Follow-through (`MeleeCapture.FollowThrough`): the clip carries on past the sweep and eases to rest while the live pose settles into the fitted guard (cubic), instead of slerping from the frozen contact pose (sword leaving the window 0.07–0.4 → 12–29 m/s). Real recovery (`MeleeMotion.RecoverySeconds`): `CommitUntil` (the authority's AttackEnd) instead of `Recovery(spec)`; held shields and greatswords no longer stand at the guard (7 → 2–3 frames below 1 m/s at the next swing).
- Turn blend: a changed commit yaw eases over 0.1 s (a 40° turn snapped in one frame, a 66 m/s tip pop; now at most 17° per frame). Hit-stop: lost time caught up at most 1.3× (the greatsword cut B pop at recovery start: 106–118 m/s, a ×2.6 spike → none). Transitions into, out of and between the captured swings carry the last frame's joint velocity (no held frame); the DEV fighters keep the source's transitions.
- Slash crescent (`NativeSlashFx.Frame`): the head follows the posed blade of the cut being played (within 5° while it crosses the arc), in the blade's direction. Finding for Astra (S0): `NativeMeleeRules.Angle` sweeps mirrored in the native frame (the source's yaw grows to the left), so the hitbox runs opposite to the captured blades; the crescent used to show that direction.
- Tests: `Tests/MeleeMotionChecks.cs` deviation table adds "swing" (combo, Echo Strike/Hurricane, fast-rate, reach variants) and "torso+swing" (run-attack-block); 24 scenarios on the Unity-owned `Tests/melee-motion-pinned.json`, the 5 hit-reaction scenarios still on the original within 0.34 mm. `Tests/MeleeFeelChecks.cs` adds the shared swing timing (continuous, monotone, sweep across the window, leaving it at the window's rate) and held chains with whiffs and hits (window speed bounds, peak inside, no one-frame pop, no dead stop, no guard idle, the 40° turn, crescent on the blade). `Tests/WeaponFxChecks.cs` checks the crescent against the visible blade.
- Animation impact: presentation only. No change to hit timing (the window, `HitAt/HitUntil`), hitboxes, damage, cooldowns, movement, stamina or network data; the authoritative step is unchanged and still drives the footwork.
- Checks: `unity-check.sh` runtime+editor OK; `check_relay.py` exit 0, 370 PASS, no FAIL; Python unittest 86 OK (1 skipped); `package_source.py` passed (1,262 files); `git diff --check 19c00c7 HEAD` clean. Offline before/after strips (held chains per weapon, frame-by-frame hit windows, a 40° turn, hits) and tip-speed charts reviewed.
- Not run: Unity Editor, Windows play, multiplayer guests and the killcam on a live match (all read the same replicated fields: `AttackAt`, `SwingAt/SwingUntil`, `CommitAt/CommitUntil`, `CommitYaw`, `VisualDuration`). Risks: owner taste on the sweep speeds and the greatsword's chop stopping on the floor; the daggers' 90 ms windup still repositions both blades quickly (37–73 m/s); the crescent opens against the hitbox order until S0 lands. Next: Astra's S0–S5 and the Q/E kit; Claude's P6 chain entry after S2.

## 2026-10-12 — Claude — melee feel evaluation and steady locomotion torso (owner: "improve the "fluidity" … notice how the body rocks when moving")
- Branch `claude/melee-feel`, base `19c00c7`; commit 1 of 2, implemented on the branch only (not pushed, merged or released).
- New `docs/MELEE-FEEL.md` (linked from `docs/MELEE-ABILITIES.md`): the three findings with numbers and `file:line` (swings peak before the hit window and stop dead after it, no buffer/chain/turning; melee cannot close the gap and the Q/E kit is not started; the captured walk/run torso rocks over the procedural pelvis), prioritised recommendations tagged PRESENTATION/SIMULATION, the exact simulation numbers for Astra and the presentation contract.
- Torso (`Core/MeleeMotion.cs` `Ready`/`SteadyTorso`, `CapturedClip.CycleMean`, read-only `MeleeGait.Back`): each captured walk/run keeps its cycle-mean carry posture and 35 %/25 % of its stride swing on the spine, chest, neck and head; the spine counters 75 % of the gait's pelvis twist and 80 % of its roll (exact inverse order); the run hunch lifts 0.26 rad (+0.22 when backpedalling) with the neck keeping the gaze. Sword run chest twist 52→10°, roll 20→3°, nod 23→6°, head sway 6.0→2.3 cm, mean chest pitch 49→37°; every weight follows the existing carry/run blends (chest-rate steps through ramps 410–556→138–152 °/s).
- Tests: `Tests/MeleeMotionChecks.cs` gains an owner-deviation table; its 14 torso scenarios (walk/run/strafe timelines, four weapons, two reach variants) compare with the Unity-owned `Tests/melee-motion-pinned.json` (`CHIP_PIN_MELEE=1`) and keep their lower body within 0.17 mm of the original; the other 15 stay on the original within 1.73 mm. New `Tests/MeleeFeelChecks.cs`: torso bounds for 4 weapons × walk/run/sprint/strafe/backpedal/backpedal walk/lock-on circle (owner targets for the sword/daggers run), the lifted hunch, and continuity through ramps and runs into and out of a swing (fails on the base).
- Animation impact: poses only. No change to hit timing, hitboxes, damage, cooldowns, movement, stamina or networking.
- Checks: `unity-check.sh` runtime+editor OK; `check_relay.py` exit 0, 368 PASS, no FAIL; Python unittest 86 OK (1 skipped); `package_source.py` passed (1,262 files); `git diff --check` clean. Offline before/after gait strips (side and front) reviewed for every melee weapon at walk, run, strafe, backpedal and lock-on circle.
- Handoff to Astra (details and numbers in MELEE-FEEL.md): S1 LMB buffer (edge bit, hit window to end of recovery, 0.4 s expiry), S2 chain points (sword 0.50, daggers 0.32, shield 0.60, greatsword 0.95 s), S3 windup turning (720/540/450/300 °/s, locked at HitAt), S4 steps 0.9/0.8/0.75/1.0 m with magnetism (opener 2.0–2.4 m, chained 1.2–1.6 m), S5 combo reset, then the Q/E kit (G1) and melee bot approach (G8) before any tuning knob; S7 guest swing prediction. Owner decisions: backpedal penalty, charged heavy.
- Not run: Unity Editor, Windows play, multiplayer, killcam on a live match. Risks: owner taste on how much stride swing to keep (the greatsword run keeps 7.6° of roll from its authored carry); the offline renders approximate Unity lighting.

## 2026-10-12 — Claude — pack pilot motion for the sword and greatsword entrances (owner: "Sword and greatsword intro animations need to change (The player animations)")
- Branch `claude/intro-melee`, base `512348d`; implemented on the branch only (not pushed, not merged, not released). Owner scope update the same day: every weapon signature but the daggers follows on this branch in later commits.
- New exporter `unity/ChipArenaConversion/tools/export_pack_intros.mjs <pack>/Animations` (ENTRANCES table; retarget of `scripts/mocap/mixamo-retarget.mjs` through `explosive-pack.mjs`): sequences pack clips onto the 4.8 s entrance clock with monotone timing keys, cross-fades, upper-body layering, root continuity, a leap height curve, foot-contact detection on the voxel rig's leg FK, the hero facing over the stage origin; writes `Core/NativePackIntros.Generated.cs` (60 Hz, 64-channel tracks; derived curves only, no FBX/.meta) and `tools/pack-intros-provenance.json` (input and track SHA-256). Byte-identical on rerun.
- Flash Draw (hit 1.8): Relax-Unsheath-R-Hips 0-1.2 s (calm stand) -> Sword-Attack-R2 from 0.75 (windup slowed into a cross-body coil 1.45-1.64, backhand cut source .3156 pinned to 1.8, follow-through to 2.2) -> Armed-Boost1 .2-.75 over 2.1-2.85 (overhead flourish) -> Boost1 .04-.12 over 2.75-4.8 (blade raised in salute, body raised 5 cm, breathing from 3.2). Colossus Oath (hit 1.8): 2Hand-Sword Idle-Alert1 0-0.8 (blade up in oath) -> Jump 0.65-1.62 (crouch, flight lifted .42 m, blade raised overhead) -> Attack2 .52-.62 over 1.62-1.8 (chop bites the floor on the hit), recovery to 2.7 -> 2Hand-Sword Idle from 2.6 (blade shouldered).
- Runtime `Core/NativeEntrancePack.cs` (`NativeWeaponEntrance.Pack`): pure function of the presentation clock; arms through the rig's IK with the gameplay `MeleeClearance`, a planned forearm/hand clearance (`NativeIntroMotion.BodyPush`, new public wrapper) and the greatsword support hand pulled onto its grip; planted feet on their landing spots (`NativeEntrancePose.PlantFoot`, new), the body sinking at most 6 cm where a lunge outreaches the voxel leg, the heel rising over deep crouches, free feet kept out of the floor, the blade tip above it. Allocation-free per frame after a first-use plan.
- Camera/effects: Flash Draw's coil shot now a medium follow on the chest, the anticipation from the sword side, the cut orbit round the front; Colossus Oath's opening is a low front orbit on the oath, the leap shot looks higher, the hero frame a little wider; its floor-scrape sparks now fly from the hit while the tip scrapes (there is no drag-in). Timing, name card, hit marks, blasts and every other entrance unchanged; Twin Phantom and the other signatures keep their baked clip (pinned fingerprint and bit-for-bit checks).
- Animation impact: presentation only. No hit timing, hitbox, damage, cooldown, movement or networking change; the match still sees only the style and its 4.8 s phase.
- Checks: `unity-check.sh` runtime+editor OK; `check_relay.py` exit 0, 359 PASS, no FAIL (new `PackIntroChecks`: provenance hashes, deterministic decode, timing, no foot slide (0.0 mm), soles above the floor, shin corners at most 3.8 cm into the floor top for at most .08 s, blade tip above the floor, support hand on the grip, limb/blade body contact at most 2.2 cm, the cut moving fast and the slam on the floor at 1.8, purity, zero allocation, daggers unchanged); Python unittest 82 OK (1 skipped); `package_source.py` passed (1,258 files); `git diff --check` clean. Offline WebGL before/after sheets (camera and fixed side view) reviewed.
- Not run: Unity Editor import, Windows play, multiplayer/late-join on real clients. Offline sheets approximate Unity lighting. Risks: the very fast backhand (3-4 frames at 60 Hz) relies on the crescent and trail to read; owner taste review of the new choreography and shots.

## 2026-10-12 — Claude — entrance music for every non-DEV entrance; owner picks for sword and greatsword
- Branch `claude/intro-music` (base `512348d`), follows `72de0b4`; on the branch only (not pushed, merged or released). Code unchanged since `72de0b4`; this step is music in the private licensed-assets repository (commit `d85129f`, no remote) plus credits.
- Owner picks after listening (2026-10-12, verbatim: "greatsword 3 and sword 2"): `intro.weapon-sword` now plays `encryption` (UGMC Encryption TRAILER) first and `intro.weapon-greatsword` plays `revolution` (UGMC Revolution) first; the other candidates stay installed as alternates. Neither was our first-ranked candidate. What the picks share was used as the style for the rest: cinematic trailer or title scores already moving before the hit (no dead stop or silence), a section step-up of a few dB on the director's mark with no time stretch, and a real resolve on the 3.2 s card (a held stab with a cymbal, or the piece's own final chord ringing out).
- The nine other weapon signatures (marks: daggers, pistol, machinegun, water and darkness 2.12 s; shield and lightning 1.45 s; rifle and fire 1.8 s) and the seven procedural entrances (orbital drop 1.2/1.6, grid rez 0.8/2.4, holo 1.6, blackout 0.4/1.2/2.0/2.4/2.8, skyline 0.8/1.4/1.75, hero landing 1.2, overclock 0.8/1.6) each get three ranked edits: Ultimate Game Music Collection, plus Badlands Music Pack 3 for the pistol, with Universal Sound FX and Fantasy Game Sound Effects hit layers. Procedural stings use 150/75 bpm material on the entrances' 0.4 s grid; Blackout follows its staging with spotlight slams. All 54 are installed (defaults first). EffectsAudio is 10.91 MB of the 12 MB budget; dropping the alternates frees about 3.3 MB. Loudness is within 0.6 LU of each replaced sting. Owner previews and `INDEX.md` are in the session scratchpad; credits are in `docs/ASSET-CREDITS.md`.
- Checks: `unity-check.sh` OK; `check_relay.py` 357 PASS, no FAIL, both with LocalAssets absent and with the new zip installed (the intro check sees 18 of 18 entrances, 54 clips). Python: 86 tests OK, 1 skipped. `package_source.py` passed and adds no audio. `install_local_assets.py --check` on the rebuilt zip OK (191 keys, 459 clips, 10.91 MB). `git diff --check` clean.
- Not run: Unity import and in-game listening. The new candidates were chosen by analysis, not by ear. Animation impact: none. If the parallel sword/greatsword rework moves a hit mark, re-cut by editing `intro_inputs.json` and rebuilding. Next: the owner listens and reorders defaults; then install the zip in the builder.

## 2026-10-12 — Claude — new entrance music from the licensed music packs (owner: "can we change the music on those? Those are also outdated lol", then "yes do music for all of them too")
- Branch `claude/intro-music`, base `512348d`; implemented on the branch only (not pushed, not merged, not released). Presentation/audio only; the sword/greatsword animation rework runs in parallel on another branch (no `NativeEntrance*` file touched).
- Code: the effects bank's key contract gains 18 `intro.<style>` keys (every non-DEV entrance: 2D, 4.8 s cap, high priority; 191 keys). `NativeSoundBank.IntroClip(style)` picks the entry's first clip (later clips are alternates; reorder the manifest list to switch) with its gain trim; `NativeAudio.Entrance` (also the Locker/Customize preview) plays it on the existing intro voice with the same volume, intro-age seek (`introVoice.time=age`, late joiners) and late-join guard, else today's built-in sting exactly as before. DEV entrances have no key (the Siren keeps its boss song). Only the playing intro clip preloads. Same LocalAssets mechanism, installer and 12 MB budget as the effects bank; `install_local_assets.py` needed no logic change.
- Checks added: managed `IntroMusicChecks` (key per entrance, clip choice with bank absent/empty/filled/broken, built-in sting fallback present, runtime routing scans, LocalAssets clip lengths/format) and Python `test_intro_music.py` (contract, installs with alternates, rejects over-length/48 kHz/positional/DEV/unlisted). `EffectsAudioChecks` and `test_effects_audio.py` updated for 191 keys.
- Music (private licensed-assets repo, never in this repo): `tools/build_intro_audio.py` renders `tools/intro_inputs.json` (edit decision lists; accents cut on the director's marks: sword/greatsword hit 1.8 s, card 3.2 s; 150 ms tail fade; loudness matched to the built-in sting; Ogg Vorbis q5 stereo). This commit: sword (Ultimate Game Music Collection Tense Combat / Encryption TRAILER / Long Range Combat) and greatsword (Phoenix Rising / Boss Battle 4 / Revolution), 3 installed per entrance, defaults first. Credits: `docs/ASSET-CREDITS.md`.
- Checks: `unity-check.sh` runtime+editor build OK; `check_relay.py` 357 PASS, no FAIL (also 357 with the private bank installed); Python 86 tests OK (1 skipped); `package_source.py` passed with no LocalAssets or audio added; `install_local_assets.py --check` on the rebuilt zip OK (175 keys, 411 clips, 6.47 MB).
- Not run: Unity Editor import of the Ogg clips, in-game listening on Windows, multiplayer late-join timing by ear. The candidates were chosen by onset/energy analysis, not by ear. Risk: if the animation rework moves a hit mark, re-cut the stings (change `hit` in `intro_inputs.json`, rebuild). Next: the remaining nine weapon and seven procedural entrances in follow-up commits; owner listens to `previews/` and reorders defaults if wanted.

## 2026-10-12 — Astra — guest Siren/PvP screens and Ward Zero minion music
- Owner explicitly approved pushing the hover update (`f288493`, verified on both shared refs), then reported guest Siren reveal/intro and PvP draft failures and supplied Ward Zero (1).mp3 for minion waves only. Branch `codex/unity-guest-screens-20261011`, based on `f288493`, fast-forwarded to shared/cloud `b320737` to retain the concurrent VFX build-gate repair.
- Siren reveal/intro freezes simulation time, but the guest ordering gate discarded every capture with the same timestamp. Frames now carry a monotonically increasing capture sequence, independent of simulation time. Guests accept fresh held-clock state while rejecting duplicate/reordered captures, regressed time and old generations/epochs. Sequence-zero frames retain legacy timestamp ordering; host and guests should all update for the fix.
- JsonUtility turns null strings into empty strings. Restore the null sentinel for unchosen PvP chips and absent PvE cinematic/outcome/notice state; the match application also normalizes an empty choice. This prevents a guest draft from being treated as already chosen and avoids looking up an empty chip id. Native gates exercise framed reveal-to-intro/song progression, complete personal chip offers, choose-later/reopen, late-join state and a real PvP draft round trip.
- Replaced the existing native minion-wave resource with the owner's exact Ward Zero MP3 (4,897,730 bytes, SHA-256 `421a6f277da35fd22f7e7b212104d6d6673fce96042a1916f51e281a60c7640e`), retaining its resource key, GUID and importer. Asset receipt and credits updated. Boss song, timing/beat map and Electron music are unchanged; no LocalAssets input needed.
- Verification: managed Core/Tests and relay compile with .NET 8 Roslyn; `check_relay.py` passes with 346 PASS lines, including 284 held-clock captures, stale/duplicate rejection and empty/real/next-turn PvP choices. Python suite: 82 tests, one skipped. Complete Core/Runtime plus gameplay-validation code compiles against genuine UnityEngine assemblies from the verified native player; the network Editor fixture type-checks with a compile-only preview-scene API stub. Source packaging and MP3 full decode pass; boss audio hash unchanged; diff whitespace clean.
- Unrun: Unity Editor execution, native JsonUtility/PhysX gates, two-client rendered gameplay/listening and a new Windows player build. No changes to combat, animation timing, hitboxes, damage, cooldowns or movement. Source push/integration is authorized; no compiled update published by this task. Next: existing local Build-and-Publish, require all native gates, then update host and guests and verify both draft modes plus Siren reveal/intro in a room.

## 2026-10-12 — Claude — directed match entrances (owner 2026-10-11: "rework the entrances (Not the DEV ones) ... Remove Neon drop, vector slide, signal slide, arc salute, and pulse wave. Add more entrances too")
- Branch `claude/intros`, base `096bb80`; implemented on the branch only (not pushed, not merged, not released). Presentation work; the account/network migration pieces below need Astra's review.
- Removed the five generic entrances (`neon-drop`, `vector-slide`, `signal-spin`, `arc-salute`, `pulse-wave`) from the Locker, menus, rules and tests. `NativeIntroRules.Migrate` turns a saved pref, account profile or received pick the client cannot stage into `Default` (`hero-landing`) with the override off, so the pilot plays its weapon signature; `Entrance(style, spec)` stages the pilot's signature for any retired/unknown replicated style; guest seats are normalized on receipt (`NativeNetworkFrame`, host receipt in `NativeNetwork`). The account service still validates intros against the Electron list, so a procedural pick is stored as `neon-drop` and reconciled locally through `pendingIntros` (`NativePendingEffect`, like pending effects). Retired clips stay unreferenced in the pinned baked banks (no asset regeneration). Editor `NativeNetworkBuildChecks` keeps `pulse-wave` as a wire fixture on purpose.
- New engine-free director (`Core/NativeEntrance*.cs`): every non-DEV entrance is a pure function of its presentation clock — pose, a multi-shot camera (cuts, whip pans, polar blends round the subject, follow shots on a smoothed Catmull-Rom anchor, guarded off the floor, the pilot and solid props), a slow-motion beat after the hit (identity before the hit and from hit+hold+catch-up, which ends before 3.2 s), staged set, lights and closed-form effects drawn through `NativeVfxDraw` (quality thins them; the entrance blast sim replays from 0 at 60 Hz). Runtime: `NativeIntros.DirectedIntro`, `NativeLighting.Entrance`, a per-bone reveal for `SourceCharacter`, and `Resources/NativeEntranceGhost.shader` (one-layer afterimage/hologram).
- The 11 weapon signatures keep their baked clips and sting hits and get 6–7 shot camera plans (establishing orbit or crane, detail/over-the-shoulder, low anticipation, a cut or whip on the hit into a slowed orbit, a dolly-out, the hero frame at 3.2 s) and new staging/effects. Seven new procedural entrances for any pilot: Orbital Drop, Grid Rez, Holo Projection, Blackout, Skyline Zip, Hero Landing, Overclock (names, Locker text and icons in `NativeLocker`; they reuse the retired stings' 150 bpm beds).
- Owner rules kept: no glowing disc or ring lies on the floor; Thunderstep's bolt and shock are violet, never blue/cyan; 4.8 s phase and 3.2 s name card unchanged. Animation impact: presentation only — no hit timing, hitbox, damage, cooldown, movement or network packet change.
- Checks: `unity-check.sh` runtime+editor build OK; `check_relay.py` exit 0, 346 PASS lines, no FAIL (new `EntranceDirectorChecks`: timing, purity, camera floor/body/prop clearance and smoothness, framing, effect budgets, owner rules, zero allocation); Python unittest 82 OK (1 skipped); `package_source.py` passed (1,219 files); `git diff --check 096bb80 HEAD` clean. Offline WebGL contact sheets (before/after) reviewed for all 18 entrances.
- Not run: Unity Editor import, the ghost shader's Unity/FXC compile, Windows play, multiplayer with older clients, GPU cost. The offline sheets approximate Unity lighting.
- Risks/next: Astra to review the account `pendingIntros` reconciliation and the seat normalization; the pilot model's own emission keeps it faintly visible in Blackout's dark beats; owner build review of camera taste.

## 2026-10-12 — Astra — repair VFX build gate tier ordering
- Branch `codex/unity-vfx-build-check-20261012`, base shared/cloud `f288493`. Owner's Windows build reached `CheckVfx` and failed the soft-depth assertion.
- The fixture submitted Low=true (correctly releasing depth), assigned Low=false without submitting, then incorrectly expected normal-tier depth. Assert depth immediately after the normal submission, release immediately after the low submission, and restoration after a fresh normal submission. All existing checks remain; runtime rendering and gameplay are unchanged.
- Verification: freshly compiled managed/relay assemblies pass 345 PASS lines through `check_relay.py`; source package validation passes (1,234 files); `git diff --check` passes. Used .NET 8 direct Roslyn compilation because SDK process discovery fails in this container.
- Unity Editor/Windows build and real soft-particle rendering are unrun here (Unity references/editor unavailable). This fixes the observed fixture ordering error; later build gates are not claimed to pass. Shared source integration is intended for the existing local Build-and-Publish flow; no compiled update published by this task. Next: rebuild with the local builder and require all native validation before publishing.

## 2026-10-11 — Astra — consistent menu hover and one-shot chip highlights
- Owner requested one menu highlight sound and a better, non-continuous chip highlight sound. Branch `codex/unity-hover-audio-20261011`, base shared/cloud `0bdcaa3`. Direct owner audio request; this narrowly changes hover feedback and its trigger, leaving Claude's other audio/visual work intact.
- Root cause: `LayoutTray` creates a new object every GUI pass; `DrawTray` compared its reference to the previous tray and replayed `ui.draft.hover` every repaint. `NativeHoverFocus` now tracks the actual slot/chip and resets with the existing draft-turn reset. Mouse and keyboard highlights cue once; disabled/spectator trays stay silent. Card hit regions use `SilentButton`, so their custom focus/pick cues cannot also emit a generic menu hover as cards finish appearing or relayout.
- Menu highlights use one fixed 40 ms soft tick; chips use one fixed 70 ms digital tap. Both are original built-in PCM at fixed pitch, with faded ends and a shared 90 ms minimum interval to prevent overlap while sweeping options. Cached clips reuse the existing voices, respect effects volume/mute and are released at teardown. No LocalAssets input or licensed files needed; other bank variations, click/pick cues and gameplay remain unchanged.
- Checks: Core/Tests and relay compile with .NET 8 Roslyn (warnings as errors); existing `check_relay.py` with those exact assemblies passes, 345 PASS lines, including a 10,000-repaint hover regression, slot/offer/re-entry/reset/disabled cases, sample determinism, finite amplitude, silent endpoints and duration/overlap bounds. Source packaging passes (1,234 files); diff whitespace check clean. Normal SDK/MSBuild invocation is unavailable in this container because process discovery fails, so the documented direct compiler path was used.
- Not run: Unity Editor/runtime compilation, Windows build or listening acceptance. No combat, animation timing, hitbox, damage, cooldown, movement or networking changes. Implemented on task branch; no shared merge or compiled test-channel release yet. Next: owner-controlled integration, then existing local Build-and-Publish and listening check; shared/cloud source must include this branch before the builder can fetch it.

## 2026-10-11 — Claude — Effects toon pass (second effects round, after the owner's Asset Store pack study)
- Branch `claude/fx-toon`, base `096bb80`. Implemented on the branch only (one commit, not pushed, not merged, not released). Visual-only: no gameplay, network data, events, hit timing, hitboxes, damage, cooldowns or movement change.
- Look: the study's recommended Epic Toon FX style, rebuilt in our own code and textures (no pack texture, mesh or shader code). Core `NativeVfxTextures.ToonSheet` (1024x512, deterministic, pinned hash): 16 cartoon puffs (unions of overlapping circles, a hard self-shadow and cast shadows, lobe outlines, a blob-shaped erosion field), an 8-frame round flash flipbook, 4 crescents, spike, lens, star and droplet sprites, all signed-distance encoded.
- `NativeVfx.shader` modes 7-11: toon smoke, toon fire (white-hot, flame, orange, then slate, HDR while hot), toon water (pale and blue tones, lit rim, oriented along its velocity), flash and segment sprites; 2-3 hard tone bands with a one-pixel edge (fwidth outside the branches) and an erosion that eats puffs into blobs. Toon puffs meet the floor nearly crisply (no glowing foot ring). `NativeVfxLit.shader`: Quake's slam wave, an upright open cylinder lit only at its silhouette.
- `NativeVfxDraw`: Smoke, Cloud, Fireball and Flame are toon by default; new roles Flash, Lick, Spike, Water, Droplet, Mist (kept soft: crisp-edged faint mist read as grey glass), Blob, Halo (a big glow sorted behind, so Void's black shapes stay black). The draw list carries each particle's spawn colours (stride 25). Bomb fuse effect (spike sparks, falling sparks, flickering glow, rising puffs; a pure function of the bomb's age, faster in the last second). Low tier skips a third of the toon puffs and draws the rest at .9 size (still no depth texture). Public API unchanged: `Smoke()` keeps its signature and now draws the toon puff; `SoftSmoke()` keeps the base layer's soft puff for anyone who needs it (weapon/block agents: please check your Smoke calls in a render).
- Per style (`NativeBlastSim.Blast`, sizes scale with the radius, 1 at 3 m): Fire/Mine/Meteor a flash, toon fire puffs from a ground circle and up a cone (#FFCE00 to slate #3E4556, over in ~1.5 s), crescent licks, hot debris, braking sparks (Meteor: a high-gravity spark fountain); Kinetic a white poof, snaps and metal debris; Frost ice spikes, crystals, faint mist, snow that lingers ~2 s; Toxic green flashes, dark green puffs, acid blobs on arcs leaving puff trails, a cloud that keeps coming for ~1.3 s; Void black ink and orbs over a violet halo, inward streaks, ink threads, a lilac ring sinking into navy; Shock violet only (palette retinted), a strobing glow and arcs living at most .15 s; Quake a dust ring of ground-hugging puffs, a rising core and the slam wave; Nova a gold flash and radial licks; Implosion an inrush, then the pop at .25 s. Smoke ends slate, never near-black.
- Owner rules kept: no flat ground disc or ring (checked for every look and tint, and the slam wave is upright); no blue/cyan lightning: the sky bolt and ground arcs still draw only for the violet Ball Lightning and red Orbital Mark tints (`Lightning` now requires a tint; the plain palette is violet but keeps the owner's cut), the new short crackle never draws for a blue/cyan look (Blink Strike). Field emitters keep the source's Shock palette.
- Water: Geyser Wall (`NativeBlastLook.Geyser` on its Frost tint) erupts as a 3-4 m column of stretched toon water puffs and droplets that tops out in ~.6 s and falls back, with spray shed from its top, a base crown and mist; Tidal Wave (`NativeBlastLook.Tide`) pops as water. Floor splash: the match emits no event when a shot hits the floor, so `NativeBlastPresentation.Globes` watches the replicated shot list; a water shot gone this frame is tested from its last seen state (`NativeWaterSplash.Landing`: touching the raycast floor, or its ballistic arc reaches it within .15 s, covering a guest's 20 Hz gap) and splashes (`NativeBlastSim.Splash`), unless a hit, impact, block or ricochet event lies near its last path. Cosmetic and deterministic in what it reads; a guest's splash can sit up to one snapshot of flight from the host's.
- Parity: Electron parity is retired for blasts only (owner-requested overhaul). `tools/export_blast_fixtures.mjs` now records impacts, fields, lines and paddles (unchanged paths); re-run against the pinned source it reproduces the old fixture byte for byte, and the new `Tests/blast-parity.json` equals the old minus its 62 blast runs (26 runs kept). New Unity-owned `Tests/blast-pinned.json` (68 runs, same frame format plus spawn counts; regenerate with `CHIP_PIN_BLASTS=1`). New `Tests/ToonFxChecks.cs`: spawn and duration budgets per look, composition reads, front/slam timing to the radius, splash and landing rules, no disc.
- Budgets (r 3 / r 6, quality 1): Fire 120/175 particles, Meteor 156/227, Toxic 199/310 (trail puffs are short-lived; peak alive 112), others 71-184 / 103-274; still three draws a frame (billboards, lit, shells); the slam wave adds 80 shell vertices.
- Checks: `unity-check.sh` runtime+editor build OK; `check_relay.py` 341 PASS, no FAIL; Python 82 OK (1 skipped); `package_source.py` passed; `git diff --check 096bb80` clean. HLSL: the requested `fxaudit/harness-test/hlsl/check.sh` cannot parse these shaders as they stand (no UnityLightingCommon stub or tex2Dlod; `096bb80` fails identically); with the base round's stub declarations inlined into scratch copies it passes all variants, as does the base round's extended check. Before/after/after+bloom renders of 20 scenarios through the offline WebGL harness (real Core sim and real NativeVfxDraw, shader SHARED blocks as GLSL), plus low-tier sheets.
- Not run: Unity Editor import/FXC compile and the Editor `CheckVfx` build check, Windows play, multiplayer (the guest-side splash timing in particular), GPU cost on either tier, D3D11 soft particles.
- Risks: the toon look replaces the soft smoke everywhere `Smoke()` is called (melee slam dust looked right in renders; other agents' calls are not rendered here); the analytic AA and erosion are tuned in WebGL, not on D3D11; the floor splash depends on the replicated shot list and may miss a shot that lands between two guest snapshots more than .15 s of flight from its last state.
- Next: owner build and review; tune sizes and timing per style in the owner's camera.

## 2026-10-11 — Claude — Effects overhaul, weapons milestone (muzzle, tracers, impacts, trails, slashes, afterimages, hit flash, charge)
- Branch `claude/fx-weapons`, base `096bb80`. Implemented on the branch only: not pushed, not merged, not released. Visual-only work on the Unity game; rebuilt procedurally from the studied Epic Toon FX / Sci-Fi / Trails FX references (no pack file used).
- New: `Core/NativeWeaponFx.cs` (pure rules: seeds, timing, shapes, colours), `Runtime/NativeWeaponFxDraw.cs` (one premultiplied dynamic mesh per camera, plus the shared geometry builders), `Runtime/NativeWeaponFxPresentation.cs` (arena hooks), `Resources/NativeWeaponFx.shader` (8 procedural modes, soft particles on the match camera's depth texture) and `Resources/NativeWeaponGhost.shader` (additive fresnel ghost); both shaders are in the `NativeBuildChecks` gate.
  - Muzzle: the source glow cone is replaced by an ETFX StandardMuzzle-style star on the same socket: 3 additive petals, a side flame card along the barrel, a core glow, 3-6 sparks, a light smoke wisp; 60-90 ms per gun, seeded by `LastShot`. First person draws on the view-model camera (layer 10), sized with the view weapon. A light pulse takes only a projectile spill light the nearest projectiles left free (`NativeCombatGlow.Pulse`; still seven lights).
  - Bullets: thin HDR tracer streaks, clipped to the distance flown. Surface `Impact` events add 6 curved shards, 8 sparks cooling white/#FF7900/#DF3C00/#180700 (bouncing off floors) and a quick glow on top of the base layer's .12 s contact flash; seeded by the event id.
  - Energy (magic bolts; ball lightning, flame lance, shadow shackles): a white-hot core in front of the existing model, a school glow, world-space sprites every .16 m flown and a .1 s HDR ribbon in the halo colour. Models, C10 halos and trails are kept.
  - Melee: standard strikes (sword, daggers, greatsword, shield) draw an ETFX SwordSlashThick-style crescent (bright edge, dark underlay, pointed head, fast fade) instead of the swept ribbon. The head follows `NativeMeleeRules.Angle` over the replicated window (`SwingAt`/`SwingUntil`), with the rules' `Arc` (via `NativeMeleeRules.Begin`) and `Reverse`, from `CommitYaw` and `Combo+1`. The plane is pitched up and rolled so the chase camera and the first-person eye see its face. Daggers get twin arcs, the greatsword a wide heavy one, the shield a short fat bash. Fists, the Siren and released specials keep the ribbon.
  - Rolls: afterimages snapshot the posed pilot (and held weapon) every .6 m during `RollAt` + `VisualRollDuration` (= RollEnd - RollAt), additive fresnel ghosts in the accent colour fading over .4 s (24 slots; 10 and 1.1 m with Reduced effects). Not on the first-person self or the Ansatsuken (Demon Step).
  - Hit flash: Hit/Headshot events on a pilot flash its body and hat 85 ms white decaying through red (headshot 100 ms, brighter, redder). The code rides the per-instance `_AccentTint.w` (unused; 1 = none) into `NativeSourceLit`'s `StageTone` after the armour grade. No new instanced array and no batching change; the armour light is untouched.
  - Charge: `NativeSpellCharge`'s orb gains converging streaks (90/s) and shrinking rings in the school colour on the rig's camera.
  - Quality: Reduced effects and low graphics cut counts (sparks, shards, wisps, sprites, streaks, copies); the low tier draws without soft particles. The trails setting turns off tracers, energy sprites and ribbons, crescents (as it did the ribbon) and afterimages.
- Cosmetic only: no gameplay, hit timing, hitboxes, damage, cooldowns, movement, animation timing or network change. Everything reads replicated actors, shots, events and seat stats, so host, guests and the killcam draw it alike.
- Fixtures: none regenerated. `weapon-presentation-parity.json` still pins the source flash (`NativeWeaponDressing.MuzzleFlash`, unchanged) as the reference for when a shot flashes. A comment there records the owner-requested star. The sweep fixtures still pin `NativeMeleeSweeps`, still drawn for fists and specials. The new Unity-owned `Tests/WeaponFxChecks.cs` has 460 checks, including pinned hash values.
- Checks: `unity-check.sh` runtime+editor build OK; `check_relay.py` 341 PASS, 0 FAIL (baseline 340 + the new weapon-effects line); Python unittest 82 OK (1 skipped); `package_source.py` passed; `git diff --check 096bb80 HEAD` clean. glslang HLSL front end OK for both new shaders and the hit-flash fragment of `NativeSourceLit`. The offline WebGL2 harness compiled the shared shader blocks as GLSL and rendered before/after sheets for each gun, impacts, 4 schools, 6 slashes (third and first person), a roll, the hit flash and 4 charges (scratch, not in the repo).
- Not run: Unity import/FXC compile (the `NativeSourceLit` surface shader edit is only fragment-checked), the Editor build checks, Windows play, multiplayer, D3D11 soft-particle depth, GPU cost. The harness pilots are CapturedRig boxes, not the source meshes, and its first-person rows use a world eye camera, not the view model.
- Risks: the 3rd-person crescent is pitched above the swing (it can read above the head); additive copies stack where ghosts overlap (no stencil); muzzle pulses only light when a projectile light is free; `NativeSourceLit` is shared by all source models (w = 1 everywhere else).
- Next: owner build and review of the looks and costs; tune the `NativeSlashFx.Look`/`NativeMuzzleFx.Look` tables in Core.

## 2026-10-11 — Claude — portrait backdrops for lobby icons (owner: "change the effect behind player for lobby icons ... additional back-options")
- Branch `claude/lobby-back`, base `411c696`; local commit only (not pushed, not merged, not released).
- Twelve selectable backdrops behind the pilot portrait on the lobby and PvE results cards, the Play/Create/Join pilot card and the Locker turntable: Tick Ring (default, the old frame unchanged), Clean Glow, Sunburst, Hex Field, Circuit, Equalizer, Warp, Embers, Orbitals, Glitch, Crown Halo, Neon Diamond. All tinted by the accent (and body colour), kept inside the stage, drawn before the portrait (never over the face). Reduced effects slows motion to a fifth and holds the glitch still. The emblem fallback is unchanged.
- New `Core/NativePortraitBacks.cs` (keys, labels, card text, icons, `Normalize`/`Index`/`Default`, pure per-style maths and texel functions), `Runtime/NativePortraitBackUI.cs` (one drawer per style, clip-safe: only unrotated, unscaled draws on every path; rays, streaks and outlines are textures baked per angle and pixel size, cached per kit scale; no per-frame allocation) and `Tests/PortraitBackChecks.cs`. `PortraitStage` takes the backdrop key: the pilot card uses prefs, the Locker stage the hovered or equipped one, lobby and results cards the seat's replicated field (your own card prefs).
- Locker Appearance tab: a BACKDROP section under IDLE POSE ("The effect behind your pilot in lobbies"); each card plays its style live behind a voxel bust, hovering tries it on the stage (TRYING ON), a click sets `prefs.lobbyBack`; it is in `LockerLook`, dirty tracking and Revert, and Save Pilot sends it. Fits at 1280x720, 1920x1080 and 3440x1440 (the tab already scrolled).
- DTO fields touched (flagged for Astra), mirroring `lobbyPose`: `ConversionPreferences.lobbyBack` (default `tick-ring`), `NativeCosmetics.lobbyBack` (no initializer, so a profile without it changes nothing), `NativeSeat.lobbyBack` (default), `NativeSeatFrame.lobbyBack` (frame validation caps it at 32 like the pose; build, and apply with `Normalize`), `LobbySeat`, `Profile()`, `SanitizedSeat` (`Normalize`), account load (adopts it only when present). Absent or unknown keys and bots show the Tick Ring; older clients ignore the field. No protocol version change.
- Handoff to Astra: account-side persistence needs a `lobbyBack` cosmetics field in the account service (it whitelists cosmetics and drops unknown fields; Electron/backend not changed here). Until then the backdrop persists in local prefs and room messages, and an account load never resets it. Merge note: `NativeCosmetics` in `NativeAccount.cs` gained the field on its long line, which the Entrance work also edits; keep both on conflict. Prefs and seat fields are on their own lines.
- Presentation only: no simulation, hit timing, hitbox, damage, cooldown or movement change; each seat frame carries one more short cosmetic string.
- Checks: `unity-check.sh` runtime+editor build OK; `check_relay.py` 341 PASS (base 340 + `PASS Portrait backdrops`), 0 FAIL; Python 82 OK (1 skipped); `package_source.py` passed (1,207 files); `git diff --check 411c696 HEAD` clean. The Editor network build check gained prefs/ready round trips, the old-save default, seat-frame carry and normalisation and the over-long rejection (compiled, not executed). Offline IMGUI renders (scratch harness): every backdrop at lobby-card and Locker-stage size at several times, the Locker tab at three resolutions with hover try-on, a lobby and PvE results with mixed backdrops, the pilot card.
- Not run: Unity Editor import/play, Windows build, the real 3D portrait behind each style, draw-call cost with eight lobby cards, a room with an older client.

## 2026-10-11 — Astra — owner-supplied Brickfall Run stage music
- Owner asked to add the uploaded track and send it through the existing shared Update source. Task `codex/unity-brickfall-music-20261011`, base shared `411c696` (rim-light fix retained).
- Added the exact owner-supplied `Brickfall Run.mp3` (embedded artist: Xtrr - musiccreator), 199.944 s / 48 kHz stereo / 4,687,082 bytes, to the existing `brickfall-demolition-row` resource slot. It is a native music resource, separate from the unchanged 83-clip source bank and optional licensed LocalAssets effects. No sound ZIP or manual copy is needed for this song. Map data, general music rotation, stage pause/resume/volume controls, all simulation/animation timing and networking are unchanged.
- Added the asset-lock receipt, streaming/stereo Unity importer metadata, provenance in ASSET-CREDITS, a real Unity build gate for the map's resolved full-length stereo clip, and `Music-Credits.txt` inside the compiled Windows update package. Kept the complete original MP3 without remastering or a seamless-loop edit; this deliberately preserves the supplied recording instead of doing the proposal's suggested Ogg/mastering conversion.
- Verified the installed audio is byte-identical to the upload (SHA-256 `3093119a80cdac42bf46a3cbb44b5da2ec6920ec5580ef153f01ea3bc6417d25`), ffmpeg decodes the entire recording, source structure/asset hashes pass (1,207 files), five existing structure-audio checks pass, and diff whitespace is clean. Full Unity import, Windows playback/listening and compiled publication remain pending. Source integration/native CI receipts go in the PR; the owner uses the existing local Build-and-Publish while Unity Cloud build requests fail HTTP 500.

## 2026-10-11 — Claude — new block effect (owner: "add a new block effect")
- Branch `claude/fx-block`, base `096bb80`; one local commit (not pushed, not merged, not released). Presentation only; design in `docs/MELEE-ABILITIES.md` "The new block effect".
- New Core `NativeBlockFx` (forms, shapes, neon colours, timing curves, clash/deflect heuristic, seeded sparks, CPU mesh `NativeBlockMesh`), Runtime `NativeBlockFxPresentation` (+ `NativeBlockFxDraw` component) and `Resources/NativeBlockFx.shader` (GLSL-safe SHARED block, registered in the NativeBuildChecks gate).
- Guard: while the replicated block window is open (guests open it at BlockUntil - BlockDuration(stats)), a ward in the weapon colour in front of the weapon: sword slim arc, greatsword tall wide arc, daggers twin arcs, shield hex dome, other classes a chest ward; animated scanlines/cells. First person: a small arch over the view weapon on the layer-10 view camera.
- Clash: guard flash and cracks (held .2 s past a late block), HDR spark spray along the blade, star and needles, upright ring facing the attacker (.18 s); the blocker's camera gets a 50 ms kick (camera shake setting, x.4 with Reduced effects; never Time.timeScale). Deflect: a streak along the reflected shot's velocity (else back toward the attacker) and a ping.
- Until Astra adds the Block Detail: Target (attacker) melee-class within reach + 1.25 m, or seatless (PvE add) = clash, else deflect; Detail `clash`/`deflect` overrides, `guard` draws no hit. Known misread: a blocked summon strike.
- Removed the cyan block bursts: native `NativeBlastPresentation` skips Block (the NativeImpactCues parity plan is unchanged) and the prototype lab's HitBurst no longer includes Block. Hooks: one call in `ArenaPresentation.Render`, one hitch line in `ConversionGame`. Reduced effects thins sprays; the low tier halves segments and sparks.
- No change to hit timing, hitboxes, damage, cooldowns, movement or networking.
- Checks: `unity-check.sh` OK; `check_relay.py` 341 PASS (base 340; new `PASS Block fx`), no FAIL; Python 82 OK (1 skipped); `package_source.py` passed; `git diff --check` clean; glslang HLSL OK. Offline WebGL2 before/after sheets per melee weapon, third and first person (scratch only).
- Not run: Unity Editor import/FXC, Windows play, multiplayer, D3D11 look and GPU cost.
- Next: owner build review. Astra: Block Detail `guard`/`clash`/`deflect` with attacker id and contact point.

## 2026-10-11 — Claude — rim light no longer keeps the chip-draft angle
- Owner report: after the first chip draft the arena's rim light stayed at the draft stage's angle. Branch `claude/rim-fix`, base `096bb80`.
- Cause: `NativeLighting.Draft` turned the rim to (15,35,0) and also wrote that into `rimHome`, the direction `Apply` restores for looks without a rim aim, so every later match kept the draft angle. Now only `Create` writes `rimHome`; the draft still turns the rim for its own stage and `EndIntro`/`Apply` put it back on (14,7,12), as Electron does. Looks with a `RimAim` (Brickfall) were never affected.
- Affected: lighting only (no gameplay, timing, networking or saves). Checks: Unity source check OK; 340 managed/relay PASS, no FAIL; 82 Python tests OK (1 skip); source package built; diff check clean. Not run: Windows build and an in-game look.

## 2026-10-11 — Astra — automatic Cloudflare rooms (owner-selected free option)
- Owner selected Cloudflare instead of Render and requested the test Update release. Branch `codex/unity-cloudflare-rooms-20261011`, base PR #118 `883a937` (shared sound-bank base `9667065`). Supersedes the earlier shared-service deployment plan; no Render service or paid plan is needed or provisioned.
- Default Online Create launches the existing pinned cloudflared 2026.9.3 connector automatically, pointing TCP-over-WebSocket at a loopback-only native listener. Guests look up six-character codes through the existing Site directory using an internal `UN` prefix, preserving its eight-character API and all Electron/account records. The authority stays in the host Unity process. No Site deployment, schema migration, router setup, account-service changes or simulation/animation changes.
- Connector binary is official and SHA-256 verified before launch; isolated config ignores named-tunnel environment settings. Bounded startup/join, cancellation on leaving, room shutdown and a Windows kill-on-close job prevent orphan connectors. Directory registration retries collisions with independent 256-bit lease credentials, renews while open and expires after leaving; transient directory failures keep current players connected. Origins are restricted to HTTPS root trycloudflare.com hosts; bounded replies, no redirects or arbitrary endpoints.
- Windows player packaging stages the verified connector and Apache license inside `app/runtime/`; normal Update installs both. Source packages exclude the binary. Removed the unused `native/online.json` bootstrap. Default rooms are private/code-only; optional managed-relay listing remains accessible with explicit `--ca-relay` on both clients. LAN is retained; unattended Cloudflare hosting uses `--ca-server --ca-online`.
- Actual local checks so far: 336 managed/Core plus real local relay PASS lines; 82 Python checks (one skip); Core/runtime and pure connector packager compile against Unity 6000.3.24f1 API references; 1,181-file source validation. Full Unity Editor/Windows gameplay has not run here. This container blocks direct tunnel egress (`api.trycloudflare.com` DNS/network unreachable), so a dedicated Windows CI internet check stages the shipping connector and tests public lookup, two native guests, ordered frames, a 130 KB snapshot, collision refusal and disconnect cleanup. Windows CI run 38176955364 passed the actual public tunnel test: shipping Windows connector staging, six-character directory lookup, two native guests, collision refusal, ordered traffic, 130 KB snapshot and host/guest cleanup. Native source and Windows updater/publisher/launcher CI run 38176955384 also passed.
- Follow-up: shared-head live check 38177379269 exposed an asynchronous Windows connector file lock during fixture cleanup, masking the original connection error. Wait briefly for the killed child to exit, handle Windows access-denied cleanup retries, preserve any original error and log each live connection stage. First live check 38176955364 had passed. Diagnostic run 38177691312 then confirmed host registration succeeds but the three fast guest retries give up after 2.4 seconds. Joining now has a cancellable 40-second routing window with two-second retry spacing and retains the underlying error for diagnostics; verify this follow-up in Windows CI before advancing the builder again. Automatic publication 38177498699 failed requesting Unity (HTTP 500); no compiled player was published.
- Integrated the shared effects/bloom work through `af13686` while preserving both agents' entries. Merged-tree verification: 340 managed/Core plus real local relay PASS lines, 82 Python tests (one skip), Core/runtime Unity API reference compilation and 1,202-file source validation passed. Compiled release remains pending until a validated Windows Build-and-Publish run. Source publication is not a compiled game release; the existing native test feed is the only intended destination. Quick Tunnels have no uptime guarantee; a tunnel failure ends its room.

## 2026-10-11 — Astra — simple online rooms and six-character codes
- Owner requested one Create/Join flow across networks, host-PC simulation, simpler codes and test Update publication. Task branch `codex/unity-simple-online-rooms-20261011`, initial base `c442b86`; includes the latest shared sound-bank work through `9667065`.
- Online is the initial connection mode; new room codes are six unambiguous base32 characters with no CA prefix (30 bits, collision reservation retries, unchanged 256-bit control credentials). Normalized lowercase/spaces/hyphens and legacy eight-character/CA codes work. LAN uses the short code too; online and LAN copy/paste invite links are supported. Public room listing remains opt-in. Menus reuse Claude's layout with only functional labels/controls changed.
- Normal clients resolve a bounded, HTTPS configuration at the existing test channel's `native/online.json`. No relay URL is shown to players; missing/invalid configuration fails honestly without making a LAN-only room. Dedicated `--ca-relay` stays available. Host/join/list operations are cancellable on leaving the menu. All game rules, physics authority, account verification, native protocol 6, source/content compatibility and saved profile data are preserved.
- Relay has a minimal Docker image, PORT support, capability health response and a restricted build context containing no licensed/game assets. Its shared code generator negotiates short codes; older clients still receive eight characters. `Relay/DEPLOYMENT.md` records exact service settings and the live two-client `--relay-smoke` check required before publishing the real endpoint configuration.
- Actual checks after merging shared `9667065`: full managed/Core plus real local WebSocket relay passed (335 PASS lines); native Runtime/Core compilation against Unity 6000.3.24f1 API references passed with the existing unused Card.Sub warning; Python source/publisher tests passed (82 run, 1 skipped); 1,171-file source structure checks passed. Added meaningful code normalization, collision exhaustion/no-overwrite, invite restrictions, secure configuration, missing-service and cancellation checks. .NET CLI startup is restricted in this environment; compiled the same project inputs directly with the installed .NET 8 Roslyn/reference assemblies and executed the real assemblies.
- Deployment/release pending: Render installation is confirmed, but its service tools are not exposed in this running session. No Render service or paid plan has been created. No public configuration is written until a real endpoint passes the live smoke check. No Windows Unity build/gameplay acceptance or compiled Update publication claimed. Keep this on the task branch until the public relay is ready; then integrate current shared heads and run the existing validated Windows build/publication path. Never publish placeholder hosts or a stale player as this change.

## 2026-10-11 — Claude — Effects overhaul, base layer (owner: "cube placeholders", "that pancake ring effect is still there")
- Branch `claude/fx-base-layer`, base `2bcb646` (Brickfall integration merge). Commits: Core `f72d153` (particle roles, seeds, shock records, smoke colour, atlas/noise/meshes, bomb pose; exporter `owner.fx` cuts, `blast-parity.json` regenerated), Runtime `edb2804`, and the review-fix commit after them. Not pushed, not merged, not released.
- Runtime: new shared renderer `Runtime/NativeVfxDraw.cs` draws every pooled particle as its Core role's primitive instead of a flat cube:
  - glows/streaks/smoke/fireballs: one premultiplied billboard mesh, sorted back to front, `Resources/NativeVfx.shader`;
  - rubble, crystals and the modelled chip bomb: one lit mesh;
  - shock fronts: one additive fresnel shell per `NativeBlastSim.Shocks` record (`Resources/NativeVfxLit.shader`).
  - Smoke is lit by the ambient, the moon and the 3 blast flashes (the `_ArenaFlash*` globals) and erodes with the Core atlas; the atlas is built once on a worker thread.
- Hooks: blasts, burst sparks, field cores, melee hit sparks and slam dust (`NativeMeleeFlash`) and the impact contact flash (`NativeCombatGlow`) go through the renderer; ArenaPresentation begins it once per RenderGlow and submits it at the end (also on the no-combat-glow path). The 3 flash lights, the block flash feed and the 2 impact spill lights are unchanged.
- No pancakes: the field ground glow disc (`NativeFieldRules.GlowDisc`) and the combat glow's flat surface disc under every shot (impact-glows.ts) are no longer drawn; a hit gets a .12 s camera-facing contact flash. Chip bomb, implosion bomb and mine draw as the lit bomb model on the Core pulse, standing on their floor. The time and repulsion domes are unchanged.
- Review fixes (same day): bloom removed from this branch (the single bloom is `claude/fx-bloom`'s `NativeBloom`, which now also has a hue-keeping display shoulder); Bolt/Line segments join only within a strand (new `StrandAt`/`StartSizeAt` draw-list floats, `NativeBlastPool.Linked`); fireballs keep their hue and billow from their start size; thin rim-only shock shells, front faces from outside, no Quake front; smoke albedo lifted to a minimum luminance, Quake smoke is light dust; `HotDebris` role (fire-family debris glows and cools) and bigger chunks; wider, brighter crystals that settle flat on the floor; Void's core is a black orb with an HDR rim; toxic clouds glow a little; flames brighter. Billboards moved to the Transparent queue with real bounds; soft particles against the camera depth texture on the normal tiers; the low tier gets no depth request, .8-size puffs and no shell noise read.
- Owner cuts recorded for parity: Quake smoke end colour and the bomb fuse-spark start height (`tools/export_blast_fixtures.mjs` FX_CUTS, `blast-parity.json` regenerated, `owner.fx.cuts` 8); melee/dust and bomb-on-floor are presentation-only (comments in `export_impact_cues.mjs`, `export_field_fixtures.mjs`).
- Cosmetic only: no gameplay, events, network data, hit timing, hitboxes, damage, cooldowns or movement change. Sim counts, lifetimes, velocities and random draws are unchanged; only roles, strands and the fuse-spark start position moved.
- Checks (review-fix commit): `unity-check.sh` runtime+editor build OK; `check_relay.py` 322 PASS, no FAIL (new strand, bomb-floor, smoke and renderer text checks); Python unittest 75 OK (1 skipped); `package_source.py` passed; `git diff --check 2bcb646 HEAD` clean. glslang HLSL front end OK for NativeVfx and NativeVfxLit (fog and gamma variants); the SHARED blocks compile as GLSL in the offline WebGL2 harness (scratch, not in the repo), which re-rendered 11 scenarios.
- Not run: Unity Editor import/FXC compile, the Editor `CheckVfx` build check (now also checks the depth-texture tier request), Windows play, multiplayer, D3D11 soft-particle depth, GPU cost on either tier.
- Known limits: transparent draws in later queues (combat glow halos +10, beacon, repair, kill effects) are not dimmed by smoke in front of them; the billboard batch sorts as one unit against the domes.
- Next: owner build and review. Later milestones cover flame shapes, kill/death effects and the Siren/Brickfall kits.

## 2026-10-11 — Claude — Effects overhaul milestone 1: match-camera bloom
- Branch `claude/fx-bloom` from `2bcb646`; implemented on the branch only (not merged, not published). Bloom part of the owner's effects overhaul; the particle-role renderer and the field glow-disc removal are a parallel branch.
- New `Runtime/NativeBloom.cs`, `Resources/NativeBloom.shader` and `Core/NativeBloomRules.cs`. This is a post effect on the match camera: OnRenderImage -> Graphics.Blit only, no command buffer.
  - The chain: a 13-tap prefilter to half size, 6 levels down to 1/64, accumulating 9-tap tent ups and an additive composite. All passes are linear HDR on ARGBHalf temporaries, released in the same frame.
  - Parameters: threshold 1 with a .5 soft knee (exactly 0 at or below linear 1), firefly clamp 10, intensity .4.
  - Second-texture v flips when the source is stored top-down. The per-pixel maths sits in a GLSL-compatible SHARED BEGIN/END block.
- The match camera sets `allowHDR=true` explicitly (the Unity default it already relied on). The bloom is attached when the camera is created, so it is always the first image effect.
  - The damage screen's image effect is added later and runs after it: behind it on the same camera, or on the view-model camera, which renders later.
  - IMGUI draws after all cameras, so the HUD and menus never bloom. The portrait, victim-screen and view-model cameras do not bloom.
- Quality: ApplySettings disables the bloom on the low graphics tier and scales it x.6 with Reduced effects. It has no saved preference of its own. The shader is registered in NativeBuildChecks.
- No gameplay, network, hit timing, hitbox, cooldown or movement change.
- Checks:
  - `unity-check.sh`: runtime+editor build OK.
  - `check_relay.py`: 320 PASS, 0 FAIL, including the new `Tests/BloomChecks.cs`.
  - Python: 75 tests OK, 1 skipped.
  - `package_source.py`: passed.
  - `git diff --check`: clean.
  - The shader passes an HLSL front-end check (glslang, all 4 passes). Its shared block compiles as GLSL ES 3.00 and was run through the full chain on a synthetic HDR frame: grid lines at 1.0 gain 5e-5.
- Not run: Unity import/compile of the shader, D3D11 orientation with MSAA, real-scene look and cost, Windows build.
- Review fix (same day, second commit): this is now the only bloom; `claude/fx-base-layer` dropped its own `NativeBloomImage` so the two branches merge without an asset or shader-name clash. The composite ends in a hue-keeping display shoulder (`BloomShoulder` / `NativeBloomRules.Shoulder`): unchanged up to 0.9, eased to at most 1 with the hue kept, blending toward white only from a brightest channel of 2 (full at 8.25), so HDR fireball puffs keep their colour and spark cores still read white-hot. Only while the bloom runs; the low tier keeps the plain clip. New BloomChecks cover it (CPU mirror and shader text).
  - Checks: `unity-check.sh` build OK; `check_relay.py` 320 PASS, 0 FAIL; Python 75 OK (1 skipped); `package_source.py` passed; `git diff --check 2bcb646 HEAD` clean; glslang HLSL front end OK (4 passes, fog/gamma variants); the SHARED block compiled as GLSL in the offline effects harness (renders with fx-base's review fix).
  - Not run: Unity import/FXC compile, D3D11 orientation with MSAA, real-scene look and cost, Windows build.
- Next: tune Intensity/Knee in NativeBloomRules against the new HDR effect values in an owner build.

## 2026-10-11 — Claude — Unity effects sound bank (licensed clips kept out of the repo)
- Owner approved the four Asset Store sound packs (2026-10-11). The repo stays public, so the clips stay out of git. Branch `claude/fx-audio`, base `1adfce5`; not pushed by this task. Presentation and audio only.
- New Core `NativeSoundBank` (key contract of 173 keys with space, priority and hard length caps; strict manifest reader; seeded non-repeating bags; per-key dedup), `NativeSoundCues`/`NativeSoundPlan` (bank lookup beside the unchanged `NativeEventSounds.Route`: blast style via `NativeBlastSim.StyleOf`, material, weapon, kind, covers and the per-style blast recipe) and `NativeVoicePool` (24 shared voices, 8 gated, 4 reserved from low priority, high steals low, 3 loops). Runtime `NativeEffectsAudio` loads `Resources/EffectsAudio/manifest`, which only the git-ignored `Assets/LocalAssets` provides. Without it every cue plays today's clip and routing, and every new hook stays silent.
- Hooks: routed events, roll/swing/cast/fighter/countdown/go/result/PvE cues, entries, machine deny, death broadcast, Brickfall's 11 layers plus 3 borrowed clips as override keys (plans, delays and the 6-voice ledger unchanged), `U.Button`/`U.Btn` click/back/confirm/error/hover, loadout/draft/notice, headshot dink, crit, kill confirm, third-party weapon impacts, shock zap, freeze/shatter, the silent kinds, kill-style accents, field born cues, mine arm, fuse beeps, 10 loops (7 field kinds, singularity, ball lightning, fuse hiss), jump/land/footsteps and the scope. Not hooked: dry fire, fly-by, ground/water impacts, pickups, Siren mark impact and vents, settings toggles (no event or state to hang them on).
- `tools/install_local_assets.py` (folder or zip; verifies the manifest, clips, metas, caps and the 12 MB budget) is for Astra to wire into the owner's builder. Gameplay, timing, hitboxes, damage, cooldowns, movement and networking unchanged. NativeBuildChecks' 25 AudioSources and 83 source clips untouched.
- Checks with LocalAssets absent: Unity 6000.3.24f1 reference compile of runtime and editor OK. Managed + real relay exit 0 with 333 PASS (base 323, +10 effects-audio checks; bank plan equals the parity route over 102 scenarios and 18,564 swept contexts), no FAIL. Python 82 tests OK (1 skipped). Source package 1,158 files; `git diff --check` clean. With the private bank installed from its zip (405 clips, 5.95 MB with metas): the same checks pass, and the LocalAssets check confirms the manifest covers all 165 named keys and every clip has a .meta, fits its cap, is 44.1 kHz and is mono when positional; the source package is byte-identical. Not run: Unity Editor import of Ogg clips, Windows audio output, the in-game mix and the voice load. Next: the owner listens to the bank and tunes the manifest gains; Astra wires the installer into the builder.

## 2026-10-11 — Claude — Customize becomes the Locker (presentation)
- Owner request: a clean, stylized Customize for kill effects, appearance, weapon abilities and (future) weapon skins. Branch `claude/customize-locker`, base `1adfce5`; local commit only (not pushed, not merged, not released).
- New `Runtime/NativeLockerUI.cs` + `Core/NativeLocker.cs`: full-screen Locker with a category rail (Appearance, Kill Effects, Entrance, Loadout, Weapon Skins), the live portrait on a turntable (drag to turn, idle sway; `PortraitLook.Yaw` is a render input outside `Same()`, so turning never re-derives the slot) and item cards with hover try-on (colours, hat, pose, weapon). Kill effects get animated rect emblems; Entrance folds the override toggle into a "Weapon signature" card; Loadout shows class, weapon and Q/E slots read from content `abilities` (melee E from `secondary`; empty melee Q says it is coming); Weapon Skins is drawing only.
- Saving is unchanged: same prefs fields, `SavePilot`, the room `profile` message and `LobbyBroadcast`; every list goes through `NativeCosmeticRules.Allowed`. New: Save is disabled until something changes and the Locker stays open after saving; Revert restores the look on entry; Escape/close with changes asks (Keep editing / Discard / Save & leave). In a room it returns to the lobby. Old `CustomizeBody`/`ColourField`/`LobbyPosePicker` removed.
- Hand-off to Astra: ability loadout and weapon skin fields (profile, account, network seat) and host validation. Hooks: `LockerEquippedAbility` / `LockerAbilityPicks` (currently first option, not selectable). A kill-effect preview on a dummy was not built (death effects are arena physics presentation, not portrait meshes); the extension point is noted in the kill cards.
- No impact on hit timing, hitboxes, damage, cooldowns, movement or networking.
- Checks: Unity 6000.3.24f1 API compile OK; managed + real relay exit 0, 324 PASS lines (new `PASS Locker: 235 checks`); Python 75 OK / 1 skipped; source packaging 1154 files; `git diff --check` clean. Offline IMGUI renders of every tab at 1280x720, 1920x1080, 3440x1440 and UI scale 1.5 (placeholder silhouette portrait, hats not drawn by the stub).
- Not run: Unity Editor/Windows play, the real turntable portrait, drag/scroll feel. Next: owner review on Windows; Astra's loadout fields.

## 2026-10-11 — Claude — Melee Q/E ability kit proposal (handoff to Astra)
- Owner request: overhaul the melee abilities so melee has Q and E like magic and ranged (creative but balanced, DEV classes excluded), plus a new block effect. Branch `claude/sleepy-ritchie-g8ruhv`, base `8ec9b04`. Docs only.
- New `docs/MELEE-ABILITIES.md`: a Q and an E for each of sword, daggers, shield and greatsword, with numbers, counterplay, shared rules, the presentation contract (ability entries, replicated state, event details, block `guard`/`clash`/`deflect` details, melee Q input) and acceptance.
  - Q is a gap closer or a way through fire: Flash Step, Shadow Step, Bulwark Charge, Earthsplitter. ABILITIES.md records melee bots losing about 100 % of cover duels.
  - E evolves today's specials: Rising Dragon with the Skyfall recast, Kunai Mark with the teleport recast, Aegis Ricochet, Wolf Knight with super armour.
- Split: Astra owns the simulation, bots, replication, input and tests. Claude owns animation (Explosive pack clips), VFX on the new effects renderer, the block effect, sound (owner-approved pack audio) and the HUD/ability cards.
- Checks: documentation only; none run. Not merged or released. Next: owner review, then Astra implements the simulation side against the contract.

## 2026-10-11 — Astra — supported ground for isolated DEV combat validation
- Owner's new log from shared `ec0c0583d88715a87a7ac41b6aa24b47ab1c40b3` passes **all real Unity locomotion gates**, including every authored ramp variant/Brickfall flight and host/guest wall/ceiling/ledge checks. It also passes account, Siren PvP audio, codec and JsonUtility/replay checks, then fails `Native DEV combat: held E resumes after hook` before player packaging.
- Branch `codex/unity-combat-fixture-floor-20261011`, base `ec0c058`. The combat fixture teleports pilots to (1000,10,1000/1006), outside map geometry, and previously assigned Grounded=true by hand. GroundMotion correctly refreshed support at the new position, found none, and made the held-E ground-only wave unavailable after hook recovery. This is a fixture mismatch with the ground motor, not a reason to relax the ability's grounded restriction.
- Give the isolated editor fixture its own PropLayer floor, reset its cached support, synchronize transforms and assert that both pilots are grounded through the real probe before each case. Destroy the floor in finally. Keep all 11 local and 11 remote Q timing cases, held-E recovery, momentum/drag, explicit airborne refusal and the remaining combat/serialization assertions. Recovery failures now include grounded/kind/ready/time. No production combat, motion, animation, account or protocol change.
- Verification: exact editor-only fixture body plus Core/Runtime compile against Unity 6000.3.24f1 API references (only existing Card.Sub warning/contextual-field info); managed/Core + real relay exit 0 with 323 PASS lines; 1,149-file source validation and whitespace pass. A separate PhysX 5.11 query reproduction finds no support at both original positions and finds the new floor at a 0.01999996 m gap. That experiment and API compilation are **not Unity native execution**. The new real-Unity setup assertion and full Windows packaging still require the owner's Build-and-Publish run.
- Push to the authorized shared Unity/builder heads; exact source/CI receipts in the task PR. Last automatic publisher for ec0c058 (`38167047126`) failed its Unity build request with HTTP 500. Do not bypass gates or publish an old player as this repair. Owner also requested easier diagnostics: the separate builder v3 task (`codex/unity-builder-diagnostics-20261011`, stacked on PR #110) prints causal errors, opens a report and maintains fixed log filenames.

## 2026-10-11 — Astra — refused stair clearance must constrain movement
- Owner's `Pasted text(20261011-185644).txt` (SHA-256 `d78fa07bf0e27996b765da4eda09a61d925dcf260a67670ec0a2fa9748a056c7`) on `c877db5` reaches the low-ceiling gate and fails `Native locomotion: stepped through ceiling`. The previous match-lifetime crash is gone; control reached this assertion after the preceding authored ramp/Brickfall traversal, idle, jump and tall-wall checks. No Windows player was packaged by the failed run.
- Branch `codex/unity-stair-clearance-20261011`, base `c877db539fde96ffed27983cebb9a90e31cb3388`. Root cause: a rounded capsule/tread-edge hit was mistaken for a walkable ramp, and any refused step still left full horizontal input active. The dynamic solver could squeeze the driven capsule through a step/ceiling gap shorter than its height. A separate PhysX 5.11 WASM reproduction of the motor and exact fixture crossed the blocked 0.5 m front edge and continued through it without a successful StepUp.
- Require a matching actual walkable face before treating a capsule contact as a ramp. When a static/kinematic obstruction refuses the complete step clearance, both authority and prediction constrain horizontal travel to the hit minus skin, then retain only sideways/outward motion. Successful steps restore full intended travel. Dynamic-prop pushes stay with the solver. Capsule dimensions, slope limit, movement stats, airborne/combat impulses, protocol and animation timing are unchanged.
- The real Unity gate retains every existing acceptance and now checks wall/low-ceiling refusal on every tick for both host and guest, walking and sprinting. A passable overhead step must still be climbed. It logs completed traversal/clearance phases; failures include the role, speed, tick and position offset. No thresholds were weakened.
- Actual verification: managed/Core and real relay exit 0 with 323 PASS lines; new regressions cover rounded-edge classification, refused walk/sprint approach, sideways/backward escape and all 228 authored ramp normals. Unity 6000.3.24f1 API reference compilation passes (existing Card.Sub warning/contextual-field info only); 1,149-file source validation and whitespace pass. Independent PhysX WASM trials pass 124/124: 25 authored ramp variants, three exact Brickfall flights, wall, blocked ceiling and passable ceiling, each host/guest and walk/sprint. Blocked host stops at z=0.3042 m and guest z=0.2842 m before the 0.5 m step face. These use a port of the query/motor logic with upstream PhysX 5.11, **not Unity's native runtime**, and are supporting evidence rather than a Windows acceptance claim.
- Actual Unity Editor/native gate and Windows packaging must still run through the owner's existing Build-and-Publish.cmd after fetching the updated shared source. Prior publisher `38165059973` failed its Unity build request with HTTP 500; no automatic retries or gate bypasses. Source integration/CI/publication receipts go in the task PR so receipt-only commits do not launch more builds.

## 2026-10-11 — Astra — stair build gate fixture lifetime
- Owner's uploaded `Pasted text(20261011-183440).txt` (SHA-256 `2fc73a126f6beea7a1ca9053905c4d46c2f3267e00715aa02e7c447bf39ecd80`) reports a real Unity 6000.3.24f1 failure on `07f9d6e`: `MissingReferenceException` reading the pilot Rigidbody in `NativeLocomotionValidation.cs:69`. Import/compilation and earlier asset checks passed; this run stopped before Windows packaging/publication.
- Task `codex/unity-stair-gate-lifetime-20261011`, base `07f9d6eae4c2cd459ae259420fdeed9af26ce7e5`. The isolated locomotion fixture has one living seat. `Match.Tick` therefore resolved a winner immediately, then called `ResetRound` after five seconds, destroying the arena and the pilot still held by the traversal loop. This was a validation-harness lifecycle bug.
- Hold round resolution only while the isolated native locomotion gate runs, restoring the prior callback in `finally`. The match clock, real StepPilot/PhysX, all ramp/tread/clearance assertions and normal gameplay match rules remain active/unchanged. Each tick now verifies that the fixture still has its original pilot and Fight phase, so an unexpected lifecycle transition fails at its cause.
- Managed regression reproduces the original one-seat reset, verifies 180,000 held ticks (50 simulated minutes, beyond the suite's worst-case tick budget) preserve the phase while time advances, and confirms normal round resolution resumes when the hold is removed. Runtime/Core reference compilation against Unity 6000.3.24f1 passes with the existing Card.Sub warning/contextual-field info; 1,149-file source validation and whitespace checks pass. Full managed/core and real relay checks exit 0 with 322 PASS lines, including the new lifetime regression.
- Unity Editor/PhysX and Windows build are unavailable here; the repaired native gate still needs actual execution. No new compiled player is claimed. Previous automatic publisher `38163278125` failed to request the stairs build with HTTP 500. Push this repair to the shared builder source, then use the existing owner `Build-and-Publish.cmd` to run native validation and publish only its successful player to the test Update feed. No gate bypass, cloud retry loop, gameplay or animation changes.

## 2026-10-11 — Astra — shared stair/ramp movement repair
- Owner reports sliding down stairs on every map and requested a fix plus publication. Task `codex/unity-stairs-fix-20261011`, base `b48b0751f5b28470a6938fac504b8ad542c641eb` (includes the owner-build spawn repair). Targets the shared Unity source and `codex/unity-cloud-current` for the existing builder/publisher.
- Root cause: the frictionless Rigidbody motor drove horizontally into ramps, retained downward velocity, and AfterPhysics converted **all** solver velocity changes (including stationary stair/wall braking) into next-tick knockback. An analytic contact regression reproduces the downhill feedback on a 48° ramp; the authored content has 228 stair ramps across the 14 old maps, steepest 48.01°. Brickfall has separate physical treads.
- Shared ground motor: verified walkable surface normals, slope-following velocity, grounded gravity/idle support, bounded 0.35 m step-up with up/forward/down capsule clearance, and 50° slope limit. The existing 0.42 m radius / 1.70 m capsule, horizontal walk/sprint speeds and airborne jump/gravity values are retained. Ground support releases for jump, launch, swimming and vertical impulses; leaving a ledge resumes gravity. Teleport abilities refresh the ground probe. Only an approaching dynamic prop can contribute collision knockback; multiple contacts are bounded by the actual solver response. Damage/debris rules and attack animation timing are unchanged.
- Guest prediction shares the same surface/step queries and refreshes support on ledges/landings, including reconciliation. No new wire fields or protocol change. All maps retained; Brickfall remains manually selectable, outside random rotation.
- Actual checks: full managed Core/Tests + real local relay run exits 0 (321 PASS lines), including all 228 authored ramp normals, uphill/downhill/walk/sprint/idle, analytic negative feedback regression, static vs moving contact impulse, jump/launch/swimming and prediction ledge/landing checks. Python 75 collected: 74 pass / 1 skipped. Runtime/Core compile against Unity 6000.3.24f1 reference assemblies with only the existing Card.Sub warning/contextual-field info. Source packaging passes 1,149 files; whitespace clean.
- Added a real Unity build gate that drives StepPilot/PhysX and guest capsule queries on every old map's distinct authored ramp grades/widths, three actual Brickfall flights, walking/sprinting/idle, jump, descent, low ceiling, tall wall and ledge cases. **Not executed here**: Unity Editor/PhysX/native gate, Windows build or owner multiplayer playtest. The local reference compile and analytic tests are not a Windows acceptance claim.
- Publication requires the validated Windows player. At task start the automatic publisher's latest run `38161124408` failed requesting a Unity build (HTTP 500), after native CI passed on b48b075. Keep the existing build gates; do not publish an old player under the new source revision. The owner's existing `Build-and-Publish.cmd` fetches the updated cloud-source branch, executes the Unity gates and publishes a successful validated build to the same test Update feed.

## 2026-10-11 — Astra — owner build failure: legacy map inline defaults
- Task branch `codex/unity-spawn-build-fix-20261011`, base `2bcb646a523e00b72ded2f4f1be013bd18b25b11`. Owner's `build(5).log` (SHA-256 `74864c81212682df3def166c6fb18d1761b8b92eff0bdb3fa5f58b31ea1e01a2`) compiled/imported the project and passed asset checks, then exited 1 in `NativeMapRules.Spawn` line 45 during the first map's build validation. No player/feed was produced by that run.
- Cause: checking only `m.spawns == null` did not handle JsonUtility-created empty inline objects for absent legacy fields. The managed System.Text.Json loader leaves these fields null, so previous managed checks missed the native failure. Empty bounds/navigation objects would also incorrectly enable the supplemental paths.
- Normalize GameContent legacy maps' supplemental fields at the shared merge/load boundary. They retain global spawns, original yaw and player/bot bounds, and legacy navigation; authored UnityMaps keep strict validation and their own data. No map removal, Brickfall geometry, content revision, protocol, presentation, combat timing, hitbox or balance change.
- Regression first reproduced the same NullReferenceException on unchanged production code. After the fix: all 15 legacy maps / 240 FFA and team spawn selections plus yaw, player/bot bounds, null/empty inline variants and malformed authored-spawn refusal pass. Full managed/core + real local relay suite exits 0 with 320 PASS lines; Core/Tests and relay compile with warnings as errors. Core/Runtime compile against genuine Unity API references (existing Card.Sub warning/contextual-field info only). Source package structure check passes 1,140 files; diff whitespace passes.
- Added an actual JsonUtility legacy-map gate before the existing Unity physics checks. This new Editor gate and the full Windows/Unity build remain unrun here. Release target is the builder's `codex/unity-cloud-current`; owner reruns `Build-and-Publish.cmd` to fetch the repair and exercise the native gates. No compiled game update is published by this source repair; keep all gates enabled and inspect any subsequent build failure separately.

## 2026-10-11 — Astra — Brickfall simulation/presentation connected for owner build
- Owner explicitly requested connecting the work and pushing the update for a local build. Task branch `codex/unity-brickfall-integration-20261011`, shared base `beaacc7332f9aefddd0ce046923523870b9c0f33`; merges PR #109 simulation `8c0094060181156ec7578676d4ea1f2faca57d93`. Both agents' DEV_LOG entries and Claude's replicated possession timestamp are preserved. Shared/build destinations: `claude/sleepy-ritchie-g8ruhv` and `codex/unity-cloud-current` (fast-forward, no force).
- Connected StructureStress/Fall/Land to Claude's VFX, ambience/dust hooks and audio; reconstructed active warnings from replicated schedules for join/reconnect/replay. Authored crush + sweep bounds cover the hazard; all ten queued warnings retain their actual time until fall. Increased the fixed record capacity to avoid evicting concurrent warnings, keeping existing draw/puff budgets.
- Dressing and paper cover use AllBlocks, including static scenery and rubble. Fragment looks inherit the replicated parent. Actual map ID aliases reuse Claude's material palette; structural damage stages read authored hp/optional stages. Member lamps and damage share the four-second arm rule. Block sag follows its own segment, so overlapping warnings cannot move unrelated static blocks.
- COLLAPSE HUD/hologram cause survives death replication. Collapse attribution uses the simulation's six-second fallback without the legacy death recorder reassigning a seven-second attacker. Existing normal death text/credit remains unchanged. No combat animation timing or hitbox changes from this wiring.
- Verification: managed Core/Tests compile with warnings as errors; full local relay + managed run exits 0, 319 PASS lines (including integration regressions, all ten warnings, segment isolation, material IDs, damage stages, compatibility and real local LAN/relay). Python: 75 collected, 74 pass / 1 skipped. `package_source.py --check`: 1,140 source files, passed. Core + Runtime compile against genuine Unity 6000.3.24f1 managed references and netstandard2.1, with only existing Card.Sub warning / contextual-field info. Diff whitespace clean.
- Not run: Unity Editor/import/shader execution, native JsonUtility/PhysX/build gates (packet bytes, capsule ejection, replica/render fixtures), Windows build, two Windows clients, G1/G2/G3 visual/play acceptance. Compilation against references is not those checks. Protocol 6 requires everyone to update together. All 14 old rotation maps plus Ward Zero remain; Brickfall is manually selectable, `rotation:false`; Wrecker remains deferred. New architectural kit geometry/polish stays with Claude.
- Source update only. No new player archive or test feed is published by this integration. Latest observed existing feed at task start: channel `20b03b8`, build `94469f03347c59a6a4e419d3a4eda5ae4eeceebd3e3805379adc284c7e1eaa25`. Owner's builder should fetch the updated source, run its Unity gates, then test Brickfall before publishing through the existing workflow.

## 2026-10-11 — Claude — Brickfall kits integrated; lighting review fixes
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62): merged `claude/brickfall-integration` `0d6b85e` (base `9808ab8`; builder, review, fix). Presentation only.
- One dust coat:
  - `Core/NativeDustCoat` is the round's only coat. Breaks are deduplicated by time and xyz; structure landings come through the new `ArenaPresentation.StructureHeard`, which also feeds the pigeons and drift.
  - It is read at the drawn arena's field clock (so the killcam shows the coat of its moment), clears on each round rebuild, and is published as `_ArenaDustCoat` by NativeArenaDressing only.
  - The block shader reads it. Bands, tags and lamps keep a tenth.
- Collapse telegraph on blocks: `NativeStructureFx.StressHolding` (a per-frame early-out, so old maps pay nothing) and `TelegraphAt`. Windows strobe at 8 Hz, then go black until the segment lands; members sag up to 8 cm and lean up to 1°.
- Band lamps: amber while locked (it mirrors §3.9's 4 s arm delay until Astra's Core function exists), red-strobing at S2 or under a telegraph, otherwise dark.
- Lighting review fixes:
  - Intro and Draft save, set and restore the fog state and pin 2 cascades over 65 m; the draft uses a soft key at shadow strength 1. Old maps are unchanged; an offline draft pixel diff shows 0 changed channels.
  - The look-to-applied mapping is now pure Core (`NativeMapLooks.Applied`), called by both `NativeLighting.Apply` and MapLookChecks. The frozen oracle is still bit-identical (3,857 values).
- Tests are now self-contained to `unity/ChipArenaConversion`: `tools/export_look_references.py` writes `Tests/look-references.json` from the Brickfall draft layout and two Electron files, with source-lock blobs. The sparse-CI python run now executes every scraps test.
- Checks on merged head `654e89d`: `unity-check.sh` runtime+editor build OK; Python tests OK (also in a unity/-only copy); `package_source.py` passed; `check_relay.py` 311 PASS; `git diff --check 6235a82 HEAD` clean.
- Not run: Unity import of the changed NativeBlockLit.shader, a Windows build, a playtest.
- Waiting on Astra:
  - the Brickfall grey-box (UnityMaps.json and its block ids);
  - `NativeStructureRules`, so the telegraph uses segment membership and the real crush volume;
  - the `EventKind.Structure*` events (hook lines are marked in NativeStructureFxPresentation/NativeStructureAudio);
  - the static block list for dressing;
  - the arm-delay Core function for the lamp.
- Release: pushed to PR #62 and `codex/unity-cloud-current`; publication waits on native CI and UBA.

## 2026-10-11 — Claude — Brickfall lighting presets (MapLooks) and the dressed block renderer
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62): merged `claude/brickfall-blocks` `bc76111`, which contains `claude/brickfall-lighting` `3c2ff7e` (both reviewed; blocks fixed).
  - Integration commit: the backdrop light-layer check now covers point lights only (the dressed maps' ForcePixel rim is directional).
  - Presentation only.
- Lighting:
  - New `Resources/MapLooks.json` + `Core/NativeMapLooks.cs` (Claude-owned, never in the network frame): key and rim lights, trilight ambient, fog, cascades with a low-tier fallback, sky, 8 practical lights.
  - `NativeLighting.Apply` reads it. The 14 other old maps are bit-identical (MapLookChecks: 3,857 values against a frozen oracle and fixture).
  - Cinder Crossing (lavaworks) now gets the forge sky, as in Electron's night-world.ts.
  - `NativeNightSky.shader` gains sun/moon, halo, star and cloud properties whose defaults reproduce today's sky.
  - Brickfall's "golden-demolition" look follows §3.10 (dormant until a map with id brickfall exists).
- Blocks:
  - `Core/NativeBlockLooks.cs`: look selection by id, glob or material; damage stages; 5 bevelled unit meshes.
  - `Resources/NativeBlockLit.shader`: procedural brick/concrete/rivet/plank/corrugation/tar/asphalt/cobble/window/neon looks; the chevron band and DANGER tag fitted to each member; wear, grime, dust and soot; cracks and spalls by health; its own 8 practical-light arrays plus rim and 3 blast flashes.
  - `Runtime/NativeArenaDressing.cs`: instanced; at most 5 main-pass draws at the prop cap.
  - Opt-in per map via MapLooks. Only Brickfall opts in, so old maps are unchanged. The dev preview is the `--ca-dress-blocks` command-line flag.
  - Brickfall's blockLooks use the draft layout's id stems (documented in MapLooks). Kit parts (windows, sills, cornices and so on) are presentation dressing, not sim blocks.
- Known, for later:
  - Dressing of Astra's future `static` blocks needs their list.
  - After the first chip draft the arena rim stays at the draft's angle (pre-existing; restoring Electron's (14,7,12) would change every old map's in-match look, so it is the owner's call).
  - The two lighting review minors are open: fog state in Intro/Draft; the parity check mirrors Apply rather than calling it.
- Checks on merged head: `unity-check.sh` runtime+editor build OK; Python tests OK (also in a unity/-only copy); `package_source.py` passed; `check_relay.py` 309 PASS; `git diff --check 6235a82 HEAD` clean.
- Not run: Unity import of NativeBlockLit/NativeNightSky (SM 3.5 limits), a Windows build, a playtest.
- Release: pushed to PR #62 and `codex/unity-cloud-current`; publication waits on native CI and UBA.

## 2026-10-11 — Claude — Siren possession hint levels 2 and 3 (on Astra's replicated PossessedAt)
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62): merged `claude/possession-hints` `efb8fdf` (base `7c39c47`; builder, review, fix). Presentation only; it reads Astra's `NativePveAdd.PossessedAt` (51d47d0) and changes no sim or network data.
- `Core/NativeMinionHint.cs`:
  - Level by possession age, as in `src/client/siren/minions/hints.ts`: under 15 s is 1, under 35 s is 2, then 3.
  - Level 2: the floating Siren grin over the possessed add, within 25 m of the camera. `hints.ts`'s canvas is baked once into one premultiplied texture (built off the main thread before first use) and drawn as one quad through the new `Resources/NativeMinionGrin.shader`. That shader is added to the `NativeBuildChecks` shader gate. The grin fades as one image like the source sprite; mean difference from Chromium's canvas is .0039.
  - Black-flame trail: the gore drip flame at 12/s (3.6/s under reduced effects) while the add walks. It starts at level 2, as the source does (horde-view.ts `level>=2`), not level 3 as earlier handoff notes said.
  - Level 3: a 1.35 brightness boost on the seam, halo and grin.
  - Deterministic from PossessedAt and the presentation clock (host, guests, killcam). No per-frame allocations.
- New `Tests/MinionHintChecks.cs` with source fixtures (`tools/export_minion_hint_fixtures.mjs`, `Tests/minion-hint-parity.json`).
- Also on the branch before this merge: `a52c85c` makes `test_brickfall_scraps` skip its `docs/` blockout comparison under the native CI's sparse checkout, which had failed native-source on `7c39c47`. Native CI passed on `a52c85c`. Tests must only read files under `unity/ChipArenaConversion`.
- Checks on merged head `ece3047`: `unity-check.sh` runtime+editor build OK; Python tests OK (1 skipped; also OK in a unity/-only copy); `package_source.py` passed; `check_relay.py` 292 PASS; `git diff --check 6235a82 HEAD` clean.
- Not run: Unity Editor (the new shader's import), a Windows build, live PvE with guests.
- Release: pushed to PR #62 and `codex/unity-cloud-current`. Publication waits on native CI and UBA.

## 2026-10-11 — Claude — Brickfall presentation: backdrop, atmosphere and destruction effects kit
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62). Merged two branches, both from `ce3c506`, each built, reviewed and fixed:
  - `claude/brickfall-scenery` `4aff19d`;
  - `claude/brickfall-destruction-fx` `30d498c`.
  - Integration commit `e3222f0` shares the 120 dust-billboard cap between the two kits.
  - Presentation only: no sim, network, save or Astra-owned file changed.
- Backdrop (dormant unless the map id is "brickfall"):
  - Built once from fixed rules into 5 static sector meshes (`Core/NativeSceneryBuilder.cs`, `NativeBrickfallScenery.cs`). Everything is beyond x ±34 / z ±30, on layer 28 (not the proposal's layer 0, so arena point lights skip it), with no colliders or shadows.
  - Contents:
    - a near ring of 1920s walk-ups with window grids, cornices, fire escapes and water towers;
    - six half-demolished blocks with slab edges, rebar and wallpaper;
    - a far row and a skyline with a warm rim;
    - a brick viaduct with a train every 45 s;
    - two 45 m cranes with blinking aviation lights;
    - the MERCER PLAZA billboard; birds; two smoke columns.
  - Every motion follows match time, so host, guests and the killcam agree. It is also drawn behind the menu.
- Atmosphere (`NativeBrickfallAtmosphere.cs`, `NativeBrickfallFx.cs`):
  - sun shafts, dust motes, paper scraps kept out of solid cover, flapping tarps;
  - 14 pigeons that scatter from explosions within 18 m and return after 25 s;
  - collapse dust drifting east, thinned near the camera;
  - an `_ArenaDustCoat` shader value (0–1) that the block shader can read.
  - Scales with reduced effects; a new `LowGraphics` flag (graphics=performance) halves counts and drops the shafts. At most 9 draws; no allocations per frame.
  - `ArenaPresentation.BrickfallStructure(...)` is ready for the structure events.
- Destruction kit (`NativeStructureFx`):
  - **Visible on today's maps:** Break events now play material bursts (masonry, concrete/stone, timber/wood, metal with a clank flash, glass with glints) instead of the generic 18-spark burst. Gibs settle on the floor they died on (a kill-effect floor raycast, §5.5).
  - API for StressFrame / Fall / Land:
    - the stress telegraph is never reduced: red crush footprint, grit columns, crack trickles, window/lamp strobe and sag values for the block shader;
    - fall streamers and snapping cables, at the physical ≤6.9 m/s² for pancakes;
    - the landing dust wall: 24–40 billboards growing 4→14 m in 1.6 s, opacity .5 / .2 / 0 at 1.5 / 3 / 7 s, at most 120 map-wide;
    - a shock skirt, 40–120 debris voxels fading from fresh to dusty over 10 s, smoke wisps for 15 s;
    - a 256-slot decal ring (scorch only from real explosions) that survives the killcam;
    - camera shake by distance (≤25 m, ≤.3 m, only when camera shake is on).
  - Deterministic from hash(mapId, segment, round index).
  - Hook lines for Astra's `EventKind.Structure*` are marked and commented out in `Runtime/NativeStructureFxPresentation.cs` and `NativeStructureAudio.cs`.
  - Audio: 11 deterministic procedural WAV stand-ins (724 KB) in `Resources/StructureAudio`, made by `tools/synth_structure_audio.py` with asset-lock receipts. They sit outside the 83-clip source-audio gate, under a 6-voice rubble cap that skips layers rather than whole cues. They can be replaced with real recordings later.
  - Dev preview: Sandbox rows (developer accounts, offline) play a stand-in hotel quadrant collapse at the aim point.
- Renders: offline emulation only. They are under Claude's scratchpad (`renders-brickfall-scenery`, `renders-brickfall-destruction`) and not committed.
- Checks on merged head `e3222f0`: `unity-check.sh` runtime+editor build OK; Python tests OK (1 skipped); `package_source.py` passed; `check_relay.py` 291 PASS, 35 managed-core checks; `git diff --check 6235a82 HEAD` clean.
- Not run: Unity Editor, a Windows build, real PhysX raycasts, audio playback, a playtest.
- Owner acceptance:
  - breaking blocks on a current map (new material bursts) and gibs on raised floors;
  - the dev Sandbox collapse preview.
- Pending:
  - Brickfall lighting (`claude/brickfall-lighting` `3c2ff7e`, review approved, two minor follow-ups) and the block renderer: in progress.
  - Wiring the structure events once Astra's grey-box and segment system land.
- Release: pushed to PR #62 and fast-forwarded to `codex/unity-cloud-current`. Publication waits on native CI and UBA for that exact head.

## 2026-10-11 — Claude — Chip selection close-up + Holo Scan hover; impact area disc removed (owner requests)
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62). Merged two branches, both presentation only:
  - `claude/draft-closeup` `3becf46` (base `40009f1`; framing builder, three hover prototypes plus a fourth spec, a judge, hover builder, two review lenses, fix);
  - `claude/impact-no-pancake` `972dbac` (base `ce3c506`; builder, review, fix).
- Chip selection, framing ("pilot zoomed in a lot more, solar plexus and up"), `Core/NativeDraftStage.cs` / `NativeDraftPlant.cs` / Runtime draft files:
  - The camera is solved so the head top sits at the headroom line and a rig-derived solar plexus sits on the tray's top line. Same 24° lens; about 2.8 m instead of 3.7 m at 16:9; tilt 8°. The head is 1.29–1.40× larger.
  - Crowns take at most 0.2 m of room (tall hats and halo flames can cross the top edge). The Siren's BossTop drops to .04. Hatted pilots and the Siren are now at least 1.25× larger than before.
  - The display table is removed. Hands rest in the lap inside a lower shade that is opaque below the plexus, so legs and lap never show.
  - The gaze is read through the turn, so the face stays toward the lens (FaceCamera ≥ .7 on every card).
  - Pick: the hand rises in front of the chest to catch the card, then plants the chip above the plexus line. Same timeline: contact .62 s; install cue still at 1.14 s. The Siren drafter catches and holds.
  - DraftStageChecks covers 11 screens (16:9 to 32:9, 4:3, 5:4, UI scales) plus pick-in-frame (132 picks) and face-toward-camera.
- Chip selection, hover ("stylize the highlighting"): Holo Scan+ won the judge (30.5/35 against lift-and-glint, circuit-energize and socket-lock), with grafts from the others. New `Core/NativeHoloScan.cs`; drawing in `NativeChipArt`/`NativeChipCard`.
  - The highlight:
    - a tier-coloured trace runs both ways round the border in 0.32 s, then a light circles it every 3.2 s;
    - the pins light as the trace passes them;
    - corner brackets snap in outside the card, keylined to stay visible over the pilot's torso;
    - one scan line passes under the text; the die flares; a tier glow breathes behind the card;
    - the card lifts 10 px, and the other cards dim by up to 15%.
  - Keyboard focus gets the same treatment. When the pointer is elsewhere, the focus card keeps faint still brackets.
  - Per-tier touches: gold tail for Legendary, foil ring for Mythic, red current for Mishima, a bracket blink for L44T, a glitch jolt for Siren, a softer glow for Common.
  - The pick clamps the brackets with a ring. Reduced effects keeps only the still parts.
  - New DraftHoverChecks.
- Impact "floating pancake" removed:
  - `NativeBlastSim` drew a filled flat disc (Ring) at an assumed floor of py−.95 for every blast and ground impact, so wall hits left a disc and debris floating mid-air.
  - Removed every Ring and the ground sweep in Impact. Their voxels went into the shell, so spawn counts still equal the source's.
  - The presentation raycasts the real floor (WorldMask, upward faces only). Near the ground (≤1.2 m) the shell is a dome; higher up it is a full sphere around the hit. Debris, fireball and sparks land on the real floor.
  - Chip-field arrivals and the Orbital Mark lock use the real floor too. A field's ground glow fades when the field floats more than 1.2 m above its floor.
  - Owner cut recorded as `owner.disc` in `tools/export_blast_fixtures.mjs` (15 cuts in total); `blast-parity.json` regenerated (67 of 88 runs changed; the rest byte-identical). BlastChecks NoAreaDisc covers it.
  - Gameplay radius and damage unchanged.
- Checks on merged head `c972cd7`: `unity-check.sh` runtime+editor build OK; Python 59 OK (1 skipped); `package_source.py` passed; `check_relay.py` 289 PASS, 35 managed-core checks; `git diff --check 6235a82 HEAD` clean.
- Not run: Unity Editor/IMGUI rendering, a real PhysX raycast, a Windows build or a playtest.
- Owner acceptance:
  - chip selection at your resolution: framing, a hat or halo, the Siren pick, and the hover feel at 720p;
  - a rocket into a wall and a ground blast.
- Known: a chip field's own ambient emitters (acid rain motes, wildfire flames) and the time/repulsion domes still sit at the field height minus .95 m, as in the source. Moving them would separate the look from the gameplay sphere; owner call.
- Release: pushed to PR #62 and fast-forwarded to `codex/unity-cloud-current`. Publication waits on native CI and UBA for that exact head.
## 2026-10-11 — Astra — Brickfall simulation implementation (PR #109)
- Branch `codex/brickfall-simulation-20261011`, exact base `ce3c506`; H0 was published first as `9f673411`. Implemented the owner's A/B/C simulation scope. Handoff and schema deltas: `docs/arenas/BRICKFALL-SIMULATION-CONTRACT.md`. This is an unaccepted G1/G2 candidate, not a passed playtest. PR: https://github.com/sheldon37/Chip-Arena/pull/109.
- Loader/network: independent schema-1 JsonUtility `UnityMaps.json`; runtime SHA-256 `contentRevision` in hello/welcome/frame, protocol 6, mismatch refusal. The generated content revision is `17926c4efe8cb1aa59525e4311fdaee045b93c9bccfd0728d23cae8f7c3621bf`. Exporter, GameContent, relay and LAN discovery are unchanged. All 14 old arenas plus Ward Zero remain; Brickfall is selectable with `rotation:false`, and Random uses that flag.
- Grey-box: 64×56 district, bounds-driven clamps preserving old defaults, eight FFA spawns and two four-seat team tables (only exactly two teams of <=4; otherwise FFA by seat), sandbox spawn safety, pilot-only upward exclusions. 507 blocks: 363 static + 144 props; 26 carried blocks are locally posed, leaving 118 initial wire props. Ten segments / 106 rubble boxes / 2,364 nav nodes / 3,983 links. Static blocks and rubble stay outside the prop cap/frame; cover queries, PvE rebake, guest deletion and killcam restore see the split.
- Collapse: explosion-only members, demolition <=1.5, closest-box role blast distance and shared LOS; weighted transitive supports, four-second arm, >=0.8-second telegraph, three-fall queue; authored pancake/hinge/peel/topple poses from immutable definitions and t0. Pancake riders only; 45 crush plus 15 rider landing and segment debris share min(60, .6 maxHP) after multipliers. Self-crush enabled, team FF honored; failed-support damage expires at 10 s, victim-damager fallback at 6 s. Max 80 fragments, segment debris 25 hit/40 group, 2.5 s rest freeze/20 s expiry; rubble swap with bounded 3 m capsule-exit search. Round reset rebuilds the map. Segment events and reversible network/replay state are implemented; fragments continue using replicated transforms.
- Bot/nav and Claude handoff: routes use real hotel core doors and scissor stairs, tenement switchbacks and bridge portals; state-gated links plus whole-path fall-sweep avoidance. Capsule checks required wider second-floor bridge portals, northern stair-house side exits and tenement landings. Rubble was lowered along hotel wing seams and the southwest stair foot; bridge rubble is 1.2 m. Prefix families remain as H0; added `ashby-stair-landing-*` / `bray-stair-landing-*`. Bridge hinge is always the authored tenement end, a deliberate deterministic contract choice. No looks, lighting, shaders, scenery, menus or presentation routing changed. Claude should use `AllBlocks`, `parent`, segment state and the documented events; actual warnings/audio must be integrated before G2.
- Actual checks: Core and all Runtime C# compiled against genuine Unity player reference assemblies with Roslyn/.NET 8.0.413, including the `ARENA_VALIDATE` runtime gates (three pre-existing CS0649 warnings and one existing contextual-keyword info). Full managed suite passed, including 35 named managed-core checks plus source/presentation/network suites. Brickfall checks cover arm/cascade/order/queue, invalid states, team fallback, revision/protocol rejection, structural filters, final caps, 10 s / 6 s credit boundaries, 24,000 pure pose samples with all eight corners inside sweep volumes, and sampled pancake acceleration 6.432 m/s². Pure pose comparisons are not a real multiplayer result.
- Actual geometry/Python checks: 64 tests, 63 pass and one Windows-only updater test skipped. All 1,024 segment combinations connect all low walkable ground cells and 16 spawn entries in the capsule-width 1 m raster; this is not a PhysX reachability proof. 28 FFA + 16 cross-team LOS pairs ×243 jitter/eye variants pass; minimum fall-sweep clearance 3.082 m. Hotel opening check passes 729 north/south +81 west/east rays (only the 81 arcade rays pass through). No unrelated intact prop intersects a crush box. Authored walk-link capsule clearance and graph access to hotel floors/both bridges pass. Jump apices from real chip stats: 1.571, 2.187, 6.283, 9.817, 13.403, 32.722 m; every protected top is excluded. Source package structure/receipts: 1,067 files pass; `git diff --check` passes.
- Unrun gates/limits: no Unity Editor, native PhysX execution, rendered player, Windows build or two Windows clients available here. Added Unity gates for actual JsonUtility start/80-fragment packet sizes with eight pilots, self/team damage through `Damage`, capsule-safe rubble ejection, actual guest transforms, between-frame killcam poses and rubble rewind. These gates compile as runtime source where applicable but have NOT executed; Editor-only replica gate is not covered by the player-reference compile. Actual packet byte counts, peak body count, latency/jitter behavior, pilot carry/crush/ejection, bot traversal/stuck behavior and A* cost, 22-degree rubble walkability, explosive-kit pier timings, full old-client joins, Windows multiplayer/FPS and G1/G2/G3 playtests remain unrun. The ejection search reports invalid geometry instead of teleporting farther than 3 m; its PhysX gate must pass before accepting this map.
- Release: draft task PR only. No shared/cloud merge, UBA build, production or test Update publication. Compatibility is implemented on the task branch, not yet shipped. After integration and verified Unity build, any test publication must use only the existing test Update channel. Wrecker, tank and tenement-floor collapse remain deferred; old-map removal waits for G3.

## 2026-10-11 — Astra — Brickfall H0 simulation contract
- Branch `codex/brickfall-simulation-20261011`, exact requested base `ce3c506`. Read proposal §5 and current integration/runtime; PR #105 is a separate, incompatible foundation and is not merged here.
- Contract: `docs/arenas/BRICKFALL-SIMULATION-CONTRACT.md`. Adds explicit pose/sweep/rubble/nav authoring, defines t0 as fall-start time, static and locally posed block collections, reversible replay restore, shared post-multiplier crush/debris allowance and deterministic credit. Protocol 6 plus runtime content hash; relay/LAN unchanged. Prefix table is the handoff to Claude before wiring presentation.
- Owner decisions retained: self-crush, friendly-fire team rule, 6 s fallback, ten M1 segments, rebuild every round, rotation false, all old maps retained; tank/tenement-floor/Wrecker deferred.
- H0 docs only; no simulation or Unity/Windows tests run in this commit. Implementation and acceptance results follow. No shared-branch merge or Update release.

## 2026-10-11 — Claude — Arena rework: Brickfall approved as arena 1 (owner decision)
- Owner direction: replace every stage except Ward Zero with unique maps in the current box-built style at a much higher quality bar, with buildings that collapse when exploded. Split: Claude does presentation (layout look, scenery, lighting, atmosphere, identity, destruction visuals); Astra does collision, structural sim, spawns, bots and multiplayer. First milestone: one arena, playtested before the old maps go.
- Design workflow (read-only):
  - three readers mapped the stage, destruction, presentation and audio pipelines;
  - four concepts were designed and two judges (gameplay/multiplayer and identity/production) scored them;
  - a synthesis was written, then fact-checked against `40009f1` (33 corrections, all applied).
- Owner picked **Brickfall** and accepted Claude's recommendations for everything else:
  - self-crush yes; team crush follows friendly fire;
  - the Wrecker is deferred until after G2;
  - the map rebuilds every round;
  - `rotation:false` until G3;
  - no game-wide tone-mapping change now;
  - kill credit goes to the last damager within 6 s;
  - old music stays.
  - The Udio licence is still the owner's to confirm per track.
- Committed `docs/arenas/ARENA_PROPOSAL.md` (spec, quality bar, destruction contract §5, work split §6, gates §7, ready-to-paste Astra prompt §6.3) and `docs/arenas/scripts/` (2D blockout and spawn line-of-sight checks, section drawings; both print to stdout). PROJECT_STATE updated with the next step.
- Why the current maps feel the same:
  - all 14 embed the same 184-block tower set and share one 64×56 footprint and one 8-point global spawn table (`NativeArena.cs:85`);
  - there are 4 flat materials;
  - only 7 blocks have supports, and they never cascade.
- Next step:
  - Astra (§6.1 steps 1–3): UnityMaps loader with a content revision, static split, per-map spawns, all five clamps from map bounds, then the Brickfall grey-box with a minimal nav graph, for the G1 playtest.
  - Claude (in parallel): MapLooks lighting presets and the dusk sky (old maps identical), then the looks, dressing, backdrop, destruction VFX and audio.
- Docs only: no code, gameplay, network or save change. Nothing was run in Unity or on Windows. The layout numbers come from a 2D script.

## 2026-10-11 — Astra — Simple portable player folder
- Owner requested a distribution that extracts to one `Chip Arena` folder with a clearly named `Chip Arena.exe`. Task `codex/unity-simple-package-20261011`, cloud base `507f257` (includes #102 camera/timing fixes).
- New player download: `Chip-Arena-Windows.zip`, containing `Chip Arena/Chip Arena.exe`, its required `.config`, `native-install.txt`, a short player README, and `app/` with the complete Unity player. Only one EXE at the folder's top level. Build receipts, logs and feed payload parts remain in the separate automation delivery. Both local builds and cloud exports produce the clean ZIP; share this ZIP rather than the raw UBA/technical delivery archive.
- Launcher compiler emits the friendly name. Runtime/updater accept either friendly or legacy launcher; internal player EXE/data names, bootstrap v1, `app` transaction/backup layout, settings path, accounts and update feed remain stable. Existing installations update normally but keep their launcher/folder name; no user-directory migration or duplicate shortcut creation.
- Actual packaging now uses `NativeStarterPackage` (also compiled in managed tests). Regression coverage: single ZIP root and launch EXE, extraction/move with spaces, excluding stale root launchers/diagnostics, missing launcher refusal, friendly and legacy installation validation. Windows integration runs both names through normal launch from another working directory, update/restart and startup-failure rollback.
- Cloud archive validator accepts both the legacy flat seed and the new nested seed with exact inventory and byte-for-byte player validation. Companion main branch `codex/native-simple-package-validator` updates the publisher's independent copy; activate it before integrating this game package change. Technical seed filename retained for the existing delivery contract; its contents are the same clean ZIP.
- Local Python/source checks: 62 tests (61 passed, 1 skipped), source packaging passed; publisher companion 74 tests (73 passed, 1 skipped). Diff whitespace clean. Hosted native/Windows checks and real UBA compile/packaging remain required; no locally built Windows game or owner gameplay acceptance claimed. No new visual assets, combat, network or save changes.

## 2026-10-11 — Astra — Owner-approved knockdown and ADS camera continuity
- Owner approved both camera changes and publication through the existing Update button, including PR #102's possession timestamp. Continued on `codex/unity-possession-time-20261011` from `51d47d0` (same tree as local `ab34c88`), cloud base `40009f1`.
- `ConversionGame.UpdateCamera`: a living pilot keeps the selected first-person view during knockdown. Existing `ArenaPresentation.Render` hides the viewed pilot's world body regardless of recovery state; the separate hands/weapon view remains. ADS camera/FOV/scope follows held Q for weapons with ADS, including stagger/recovery. Releasing Q, death or menus ends ADS. Reload/roll/block no longer switch the camera mode either; the simulation still gates ADS benefits and shooting independently in `NativeArena.StepPilot`.
- No changes to simulation, shot availability, accuracy bonuses, damage, stamina, recovery duration, replicated input, animation assets or saves. Cinematics/replay retain their existing camera overrides.
- Verification: inspected the input, camera, own-body suppression, view-model/scope and authority firing paths. Existing Python/source/package checks rerun for the combined patch; native CI and the actual UBA build must pass on the final cloud commit before publication. Windows visual/playtest acceptance remains unrun here.
- Merge and test-channel publication are now explicitly authorized by the owner. Claude's level-2/3 possession visuals remain a separate handoff; the timing data is included here.

## 2026-10-11 — Astra — Replicate Siren minion possession start for Claude's hint visuals
- Task branch `codex/unity-possession-time-20261011`, based on cloud `40009f11f75926a3418f3b595e87a9277ae8e0b4`. Host already records `NativePveAdd.PossessedAt`; the snapshot now carries `NativeMinionFrame.possessedAt`, validates it as finite/nonnegative, and restores it into the guest add. Network/replay consumers can use `add.Pve.Possessed` and `arena.Time - add.Pve.PossessedAt`. This is match time, not wall-clock time. No movement, damage, cooldown, possession selection, save, or camera rule change.
- Claude handoff: the timing field is ready on this task branch for the level-2 floating grin and level-3 black-flame trail/visual boost. No hint visuals implemented here. The additive JSON field defaults to zero for older frames; mixed-version peers do not gain accurate possession age from an old host. No protocol bump.
- Added an actual-Unity PvE build gate exercising authority capture, real network encode/decode, replay clone, guest application after 36 seconds, repeated observation without resetting the start, state clearing, omitted-field default, and nonfinite/negative rejection.
- Local verification: Python suite 59 collected, 58 passed / 1 skipped; source packaging passed (1050 files); diff whitespace check passed. No local .NET SDK or Unity Editor available, so the managed checks run in native CI; the new runtime gate requires UBA/Editor execution. Windows visual and live multiplayer acceptance remain unrun.
- Owner decision pending, as explicitly marked in the request: retain first person during knockdown with own body hidden; retain ADS camera while Q is held during stagger while simulation still blocks shots. Both remain unchanged.
- Release at handoff: `40009f1` cloud native CI passed (run 38107942516); publisher 38108008594 was in progress ensuring its exact Unity build. This patch is implemented on a task branch, not merged or released. Repository AGENTS.md leaves merges to the owner; integrate this branch after approval, then require native CI and a real UBA build of the new cloud head before its test-channel publication.

## 2026-10-11 — Claude — Damage-screen camera flip fix and Siren minion effects (owner request)
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62): merged `claude/damage-screen-flip` `daf6048` and `claude/siren-minion-fx` `0c5871b`, both based on `497966f`. Each branch had a builder, two review lenses and a fix pass. Visual only: no simulation, networking, replicated-data or save change.
- Camera flip ("camera seems to flip when taking damage"):
  - Cause: the damage screen copied `CameraTarget` into a temporary texture from a command buffer at `AfterImageEffects`, then blitted it back. On D3D11 with a back-buffer or MSAA-resolved target, that copy lands top-down without Unity flagging it, so the frame drew upside down while the effect was up. Unity staff describe this round trip as flipping in forward rendering (won't-fix).
  - Fix: new `Runtime/NativeDamageScreenImage.cs` draws through `OnRenderImage` + `Graphics.Blit`, the manual's guaranteed-orientation path. It is enabled only on the last screen camera while the effect is active, so idle frames cost nothing. The shader is unchanged apart from comments and a saturated frame tap, which keeps the tuned look on HDR sources. The `Frame`/`Release` API is unchanged.
  - Guard: new `Tests/test_damage_screen_orientation.py`; it fails 8 assertions against the old file.
  - The first-person melee kick was ruled out: it is NaN-guarded, soft-limited (2.6/1.2/2.8°, 3 cm) and rebuilt from the look angles every frame.
- Other damage-time camera changes, Astra-owned and unchanged; owner to confirm which one they saw:
  - (a) A knockdown switches first person to third person for the Down window: `ConversionGame.cs:193` `!arena.Downed(p)`, intentional per e268aa8. Electron keeps first person and hides the player's own body.
  - (b) A melee stagger cancels ADS: the camera `aim` includes `!Incapacitated(p)` (`ConversionGame.cs:192`), mirroring the simulation's ADS gate. A third-person player who is aiming drops out of the first-person aim view, and the zoom snaps out.
- Siren minions ("make cracked flames animate, other improvements"):
  - The exporter baked the glow marker into colour, so cracks were static blown-out white and the tongues were static boxes. `tools/export_models.mjs` now keeps the real glow colours plus emission and a flicker code. It splits the tongues into two shared pieces and writes `Core/NativeMinionFx.Generated.cs`. Only the minion models changed; all others are byte-identical. The bank went from 6,647,451 to 6,606,479 bytes.
  - `NativeSourceLit.shader`: each glow cell flickers on its own, plus a per-minion power drop. The drop is computed on the CPU so the haze and the crack light fade out together.
  - New `Core/NativeMinionFx.cs` + `Runtime/NativeMinionFxPresentation.cs`:
    - swaying, flaring crack tongues that shed tip flames;
    - a breathing violet haze over cracked heads;
    - the level-1 possession hint;
    - vent emergence (pool, rim flames, sparks);
    - death char with ember flicker, then a feet-first burn-away with flames, ash and scorch (the rig now lives 3.1 s instead of 2.3 s).
  - Effect budgets are ranked (possessed and dying adds first, then vent climbers, nearest first) and distance-banded (full to 24 m, half to 40 m). Reduced effects halves everything. No new draw calls; no steady-state allocations.
  - Two additions not in the source, open to owner tuning: tip-shed flames and the head haze.
  - New `Tests/MinionFxChecks.cs` with source fixtures (`tools/export_minion_fx_fixtures.mjs`, `Tests/minion-fx-parity.json`).
- Handoff to Astra: possession hint levels 2 and 3 (the floating grin within 25 m, the black-flame trail, the 1.35 boost) need the possession start time in the replicated minion frame (`add.Pve.PossessedAt` or `Pve.PossessionAt`). Claude adds the visuals once it is replicated.
- Checks on merged head: `unity-check.sh` runtime+editor build OK; Python tests 59 OK (1 skipped); `package_source.py` passed; `check_relay.py` 281 PASS, 35 managed-core checks; `git diff --check 6235a82 HEAD` clean. Shader syntax checked only with glslang's HLSL front end.
- Not run: Unity Editor import, a real D3D11/Windows build and gameplay.
- Owner acceptance:
  - take damage in first and third person, with anti-aliasing on and off;
  - watch minion cracks and tongues, emergence and deaths.
- Risks:
  - the first-person source assumes the view-model camera's image effect receives the stacked frame; fallback: the match camera;
  - NativeUiBlur uses the same command-buffer `CameraTarget` copy (at AfterEverything), so panel backdrops may also be upside down on D3D (unverified).
- Release: pushed to PR #62 and fast-forwarded to `codex/unity-cloud-current`; publication waits on native CI and the UBA build of that exact head.

## 2026-10-10 — Astra — Fix real Unity UBA #32 pre-export parity blocker
- Failed actual Unity cloud build #32 (source `c0426cb`) has an Editor PreExport failure: `Follow-up parity: ability ceiling absorbs upward projectile` (NativeFollowupValidation.cs:19). This stale Editor assertion requires a `CeilingHit` from a rocket that should be absorbed silently (the original ability-system.ts has none); the intentional removal of that event was already merged in Astra's earlier ceiling parity batch. GitHub managed/source CI did not execute this Editor-only assertion, so it was green while the real UBA failed.
- Corrected NativeFollowupValidation to require projectile absorption **without a new CeilingHit**; preserves unrelated existing events. New Python regression guards this Editor/runtime contract in both CI source and the Windows package tests. No runtime/gameplay, shader, networking or save change.
- Branch `codex/unity-uba-ceiling-validation-20261010` from cloud `f804d2b`; merge only after both native CI jobs pass. A fresh UBA build of the merged head is still required; a passing source check alone is not publication verification. This also enables the first real compile of Claude's new shaders on the current batch.

## 2026-10-10 — Astra — Last Laugh crush authority and Windows update handoff
- Owner request: fix the Siren's arm snapping to idle at the timed crush, correct the victim's facing on the drop, and distinguish the actual crush from unrelated mid-hold kills; publish the resulting Unity batch through the existing native test Update button.
- Task branch `codex/siren-crush-release-20261010` from verified cloud head `d4cabb9455a0e749fb8665d428cc08c1db36bc92` (Claude PR #62). Simulation change: mark each grabbed victim as crushed once; detach the victim before applying the crush damage so the death handler does not cancel the holder; retain the holder's `Caught`/grab action until the existing `m.Until` (0.6 s after crush). Cancellation now zeros captive velocity only while the holder actually has them.
- Explicit `sirenCrush` authority flag on `Damage -> RecordDeath`: only lethal damage from that timed crush selects `siren-crush`. Bystander/DoT deaths while held retain their usual kill effects. The death yaw is the Siren holder's yaw + 180° as drawn; root position remains the authority's held body anchor, with the presentation's existing 0.08 s pose carry (exact rendered claw transform is not available inside simulation).
- Source event timestamps, original damage values, stun, cooldown, `CrushAt`, `Until`, physical pin, ragdoll release and PvE repair code are otherwise unchanged. Network/save schemas are unchanged; snapshot `SourceAction` continues to report `grab` until the move finishes. New managed regression checks cover the 0.6 s action interval, wrapped facing, and actual-crush-only death style.
- `tools/export_cinematics.mjs` inspection: Claude's staged/dressed Siren bake and head track are accounted for by existing export parity checks; existing dependency on three.js object-id order persists. No changes to exported models or shader assets in this task.
- Verification/release status at implementation: source patch committed on task branch; native CI and real Windows UBA/D3D shader compile still need their exact new-head receipts. Existing `main` publisher workflow on completion of cloud native CI is the authorized native-test path. Do not publish a stale build, alter the Electron feed, or treat a green source job as a compiled Unity player. Owner Windows physics/visual/host-guest acceptance remains open.

## 2026-10-11 — Claude — Siren redesign ("Fallen Couturiere") and a realistic entrance smile (owner request)
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62): merged `claude/siren-detail` `0a87241` (base `0e8a3ff`) on top of `7f5e288`.
  - Three rendered design concepts were judged. The winner is couture-horror, with grafts: faithful-plus's sculpted mask face, finer fangs and flame twist; eldritch's animated cheeks and distance fade.
  - A model builder, then a smile builder, then review from two angles and fixes: all six findings were real and fixed. Visual only.
- Model:
  - 258 detail boxes on the existing Siren bones, generated deterministically by `tools/siren_design.mjs` and ported in `Core/NativeSirenDetail.cs` (matches the JS to 4.3e-9).
  - Pieces: a trimmed pleated collar, pauldrons, a bodice V and dorsal ridge, the gown (peplum, skirt, slit panels), an animated train with ash hem steps, gauntlets and claw rings, a broken nimbus arc, the sculpted mask face, animated cheeks, a finer 13/12 fang grin, talons ×1.15 and a stratified flame twist.
  - Glow lines breathe and fade with distance.
  - `NativeSirenLook` Count is now 136 source + 260 detail.
  - Every draw path shows it (arena, killcam, entrance bake, portraits, draft seat, PvE boss), and the Siren death bake is dressed too. First person is unchanged.
  - Cost: +258 boxes, about +4 solid draws, +1 glow draw and 4 shadow casters per camera. No new shader.
- Smile:
  - Before, the source simply stretched the grin sideways and the old bake held the eyes at full anger.
  - `Core/NativeSirenSmileFace.cs` (mirrored by `tools/siren_smile.mjs`) and `Runtime/NativeSirenFace.cs` now draw the face natively on the baked head track:
    - the blank stare is restored;
    - at the smile cue (beat 14, unchanged) a closed fang seam flickers in, the left corner leads into a smirk, and the 25 fangs bare from the centre with a bite;
    - the jaw drops and settles, the eyes narrow from below as the cheeks rise, and the halos and fangs flare;
    - a silent laugh lands on song beats 15 and 16, and the held grin strains with a 4.1 Hz quiver.
  - Cue times, durations, the intro clock and the music sync are unchanged.
- Tooling Astra should know about:
  - `tools/export_cinematics.mjs` (exporter tooling) was changed: it hides the source eyes and grin in the dev-siren bake, records the head track, bakes the body layer, and re-bakes the dressed Siren death.
  - Clips were spliced so only the stage-dev-siren clip and the death-siren-dissipate clip changed. A re-export elsewhere differs only by last-digit float noise.
  - There is an older exporter fragility: bakes depend on the three.js object-id sequence. It was worked around, not fixed.
  - `SceneMotion.json` is now about 15 MB, and the asset-lock receipts are updated.
  - Please review these exporter edits.
- Checks on the merged head:
  - unity-check: runtime+editor build OK;
  - Python: 52 OK;
  - package_source: passed, 1038 files;
  - check_relay: exit 0, 280 PASS, including Siren look parity, Siren detail and Siren smile face.
  - The new Editor build check uses public members only.
- Not run: the Unity Editor, a Windows build or in-game look, multiplayer.
- Known risks:
  - the arena's bank grin still looks striped in 3/4 view;
  - couture plates read a little lighter than the ink body because of the glow seams;
  - the gown changes the silhouette, on purpose.
- Status: implemented and merged on the branch; pushed to the cloud branch; not released by Claude.

## 2026-10-11 — Claude — Damage screen effect (owner request)
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62): merged `claude/damage-screen-fx` `fd1a789` (base `d6a5dac`) on top of `c0426cb`.
  - Three rendered concepts were judged. The winner is the cinematic "wound bloom", with grafts from the tactical concept and one from the visceral one.
  - Then built, reviewed from two angles and fixed. All five findings were real and fixed, including two photosensitivity issues: the heartbeat now peaks at 2.7 swells/s and flashes are capped at about 3/s. Visual only.
- Files:
  - `Core/NativeDamageScreen.cs`: pure rules, no allocations after construction.
  - `Core/NativeDamageScreenChecks.cs`: 14 checks, also run by the Editor build checks (public members only).
  - `Resources/NativeDamageScreen.shader`: target 3.5, tex2Dlod only, listed in `NativeBuildChecks`.
  - `Runtime/NativeDamageScreenPresentation.cs`: one CommandBuffer at AfterImageEffects, attached only while active.
- What it shows:
  - **Wound bloom:** a smoky crimson bloom at the screen edge facing the attacker, scaled by damage over max HP (melee ×1.2), merged per side and live re-aimed.
  - **Threat ring:** per-attacker arcs on a crosshair ring with ▲/▼ elevation carets; headshot rails and diamond; a melee shock arc; a dashed ring for hazards or hits with no source. In third person the ring is wider and dimmed over the pilot.
  - **States:** burn flames and poison drips; a low-HP heartbeat (reach capped at about 22% of the edge); a heal wavefront; an impact flash.
  - **HP lag chip** on the health bar.
- Where it hides: menus, previews, every killcam replay, intro, draft, PvE picks and holds, the Siren reveal and cutscenes. It fades out when you die.
- New setting: "Damage screen effect" Off / Subtle / Full (default Full) under Effects. It is a new preference with a default, so it is compatible with existing settings files.
- Checks on the merged head:
  - unity-check: runtime+editor build OK;
  - Python: 52 OK;
  - package_source: passed, 1024 files;
  - check_relay: exit 0, 278 PASS.
  - `git diff --check` is clean.
- Not run: a real FXC SM3.5 compile, the Windows player, live multiplayer. Offline renders used the shader's shared block compiled as GLSL over arena frames.
- Optional for Astra: put the attacker or blast origin in `ArenaEvent.From` on Hit/Headshot. `DamageSource` marks where it plugs in. The arcs currently locate attackers by pilot, or the nearest summon or add within 3.5 m.
- Status: implemented and merged on the branch; pushed to the cloud branch; not released by Claude.

## 2026-10-10 — Claude — Hit reactions driven by the owner's Explosive pack clips (owner suggestion)
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62): merged `claude/hit-reactions-pack` `7c4ca32` (base `2815906`) on top of `7d04b80`. Built, reviewed from two angles, fixed: three low findings, fixed in docs and checks.
- New `tools/export_pack_reactions.mjs`, which reuses scripts/mocap's retarget code and writes only under unity/. It produces `Core/NativePackClips.Generated.cs` (40 retargeted pose tracks: 5 sets × 8 slots) and `tools/pack-reactions-provenance.json` (SHA-256 per track). The output is byte-identical on re-run.
- No pack FBX, pack .meta or raw data is committed: the same policy as the melee mocap clips.
- `Core/NativePackReact.cs` selects and layers the clips:
  - GetHit F1/F2 (heavy)/L1/R1 per victim style: Armed for sword and daggers (daggers reverse-grip), 2Hand-Sword for the greatsword (hands kept on the hilt), Armed body plus the shield arm on guard for the shield, Unarmed for the fighters;
  - Block-GetHit for melee blocks;
  - the Stunned loop for long stuns.
- Layering:
  - light hits move the upper body and hips;
  - while stunned, the full body takes the clip with the feet planted;
  - everything is exactly 0 at `StunUntil`;
  - clips are retimed to the reaction window, with a soft trunk-bend limit and blade clearance.
- The procedural layer stays as fallback and modulation:
  - hits from behind (the pack's B1 read wrong);
  - gun and magic victims;
  - short-stun daze;
  - non-melee hits.
- `MeleeMotion` hooks act only when a hit layer is bound, so the source parity checks are unchanged.
- Checks on the merged head:
  - unity-check: runtime+editor build OK;
  - Python: 52 OK;
  - package_source: passed, 1016 files;
  - check_relay: exit 0, 264 PASS, including the pack-reaction checks (provenance, selection, blocks, stun end, blade clearance, no allocation).
  - No FBX in the diff.
- Risks: the heavy greatsword-on-greatsword hit from the left buckles hard, and headshot snaps on sword pilots are subtler. It still needs an in-game look on Windows.
- Status: implemented and merged on the branch; pushed to the cloud branch; not released by Claude.

## 2026-10-10 — Claude — Pilot armor lighting: trims light the armor around them (owner request)
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62): merged `claude/armor-lighting` `eaeeb7e` (base `2815906`) on top of `d6a5dac`. Three rendered approaches were judged; the winner is shader-side spill, with grafts from the glow-lights and bloom proposals. Then built, reviewed from two angles and fixed: all four findings were real and fixed. Visual only.
- New `Resources/NativeArmorLight.cginc`, `Core/NativeArmorLight.cs` and `Runtime/NativeArmorLights.cs`; small hunks in `NativeSourceLit.shader`, `NativeSourceModels`, `NativeSourcePresentation`, `NativeCombatGlow`, `NativeKillModels` and `NativeBuildChecks`.
- What the armor lights now do:
  - Each emitter face has a soft core-to-edge profile with a 1.2 cm contact bleed.
  - Wrapped-Lambert spill: caps light the upper arms, the band lights the hips, cuffs light the forearm and the held gun, kneecaps light the shins.
  - The shell self-glow drops to .80. The sternum is the hot core (x1.15). Hue-keeping roll-off: pale accents no longer wash out.
  - Spill on stage-lit draws is x.6, for intros.
  - Halos fade by facing and are clamped to 4.5-70 px.
  - The two pooled floor lights are hue-true, kept by the nearest pilots with hysteresis and a 0.15 s fade, range 2.4 m.
  - Breathing pulse ±6%, off with reduced effects.
- Corpses: boxes crossing severable joints skip spill, so a detached limb never keeps a neighbour's glow.
- Rollback values for the owner: ArmorHaloScale 1, ArmorSpillIntensity 8, ArmorSpillRange 3.6.
- Siren ink, the death hologram and portraits are unchanged.
- Checks on the merged head:
  - unity-check: runtime+editor build OK;
  - Python: 52 OK;
  - package_source: passed, 1009 files;
  - check_relay: exit 0, 254 PASS, including "Armor lights (777 checks)".
  - `git diff --check` is clean.
- Not run: a real D3D11 compile of the changed `NativeSourceLit` and the new cginc (the cloud build is the first true compile), GPU timing with 8 pilots, Windows play. The offline renderer emulates Unity shading.
- Status: implemented and merged on the branch; pushed to the cloud branch; not released by Claude.

## 2026-10-10 — Claude — AoE blue/cyan lightning removed; Siren grab death ragdolls on drop (owner requests)
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62). Merged `claude/remove-aoe-lightning` `bfab574` and `claude/siren-grab-ragdoll` `822d815` on top of `2815906`. Each was built, reviewed from two angles and fixed. Presentation only.
- AoE lightning (owner override of source parity):
  - The blue/cyan lightning came from `NativeBlastSim`: the Shock style's 9 m sky bolt, its side sparks and the ground arcs, plus the Storm In A Jar field's repeated strikes.
  - It showed on Blink Strike (cyan), Thunder Step, Static and Storm In A Jar.
  - New rule: no bolts or arcs for blue/cyan shock looks. Ball Lightning (violet) and Orbital Mark (red) keep theirs. The core, shock front, sweep, sparks and light stay, and so do the storm clouds and glow.
  - `export_blast_fixtures.mjs` applies the same cuts to the pinned source (`owner.cuts=4`); only 7 of 88 parity runs changed. 208 new checks.
  - Risk: the blue shock front and stretched sparks remain on those abilities. If the owner still reads them as electric, recolour or drop them next.
- Grab ragdoll:
  - Before: the `death-siren-crush` clip left the body about 1 m in the air, and kept PvE bodies froze mid-air.
  - Now (new `Core/NativeSirenDrop.cs` and `Runtime/NativeSirenDropPresentation.cs`): after a 0.1 s limp beat, or the source's 0.45 s hang if the Siren still holds, the exact drawn pose plus its motion goes to a `NativeCorpse` physics ragdoll. It has a seeded backward tumble and a knee give, so it falls and settles on its back on the real floor.
  - The weapon drops loose, the gibs land on the floor, flames come off the ragdoll, and the body shrinks into flame from 3.2 to 4.4 s.
  - Host, guests and killcam are deterministic from replicated data.
  - `NativeCorpse` gains a posed-matrices constructor.
  - In Astra's `NativeInteractionPresentation.StartRevival`, the crush-clip block became `SirenDropPose(...)`, so kept bodies revive as they lie. The vent-splat hook is kept.
- Checks on the merged head:
  - unity-check: runtime+editor build OK;
  - Python: 52 OK;
  - package_source: passed, 1002 files;
  - check_relay: exit 0, 253 PASS, including "Siren crush drop" and the no-blue-lightning checks.
  - `git diff --check` is clean.
- Not run: real PhysX (the renders used a Rapier stand-in with PhysX-like joint limits), Windows play, live host/guest/killcam.
- For Astra:
  1. `StepSirenWorld` calls `CancelSiren` at `CrushAt`, so the Siren's arm snaps to idle at the crush. The source keeps the laugh move until crush + 0.6 s. Keeping `SourceAction` 'grab' until `Until` makes the presentation use the source's 0.45 s hang automatically.
  2. For siren-crush deaths `death.yaw` is the victim's own yaw from the catch, while the hold faces holder yaw + 180, so the body spins at the crush. Record the held facing (and if possible the drawn claw point) in the death record.
  3. `RecordDeath` uses siren-crush whenever `HeldBy` is set, including a bystander kill mid-hold. The source uses it only for the actual crush. Please confirm which is intended.
  4. Optional: add a floor-height assertion to the siren-crush revive validation (it now steps the drop body).
- Status: implemented and merged on the branch; pushed to the cloud branch; not released by Claude.

## 2026-10-10 — Claude — Melee hit reactions and the visible stun (owner request)
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62): merged `claude/melee-hit-reactions` `a684b49` on top of Astra's `0e8a3ff`, which carries the authoritative melee hit stun. It was built, reviewed from two angles and fixed: all four findings were real and fixed.
- New `Core/NativeHitReact.cs` plus `Runtime/NativeHitPresentation.cs`, applied as a per-rig layer on all four third-person routes:
  - directional accents: front lean-back, back lurch, side bend and twist;
  - weight by attacker weapon: daggers 0.6, sword 1, shield 1.15, greatsword 1.4, Siren 1.35, fighters by damage;
  - chained hits alternate, headshots snap the head;
  - a melee block shoves the guard;
  - a dazed stun pose (guard dropped, head lolls, sway) while `StunUntil` is in the future, ending exactly at the stun end;
  - gun and magic victims get recoil and a catch step (they were frozen before);
  - light hits move only the upper body, so running continues.
- First-person melee hit kick on the camera only, after the aim point is taken. It is off with screen shake off, scaled with reduced effects, and soft-limited.
- One presentation-only hunk in `NativeArena.Damage`: `MeleeHitReaction(...,direct||melee)`, so the Siren claws record a `Reaction`. `MeleeHitReaction` only writes the presentation `Reaction`; it never stuns. No change to stun lengths, damage, hitboxes, timing, movement or network format.
- Checks on the merged head:
  - unity-check: runtime+editor build OK;
  - Python: 52 OK;
  - package_source: passed, 997 files;
  - check_relay: exit 0, 252 PASS, including 25 new hit-reaction lines.
  The offline renders (22 before/after scenarios) are in the scratchpad.
- Not run: the Unity Editor, Windows play, live host/guest/killcam.
- Next: a follow-up pass driving these reactions from the owner's Explosive pack clips (GetHit F/B/L/R, Block-GetHit, Stunned), with this procedural layer kept as the fallback.
- Status: implemented and merged on the branch; pushed to the cloud branch; not released by Claude.

## 2026-10-10 — Claude — Early-port placeholders, batch 5: world, stage, summon and pickup effects; Astra handoff
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62), on top of `0433c87`. Merged `claude/placeholders-5a` `dd2ab53` and `claude/placeholders-5b` `1a4fb9a`. Each half was built, reviewed from two angles (source fidelity, Unity build safety) and fixed. Presentation only.
- B5a:
  - The pilot cube stand-ins are deleted: the Admin Pause cage, the possession tether and the shield ring. The source draws none of them in 3D.
  - Siren-stage and Ward Zero vents follow `stage.ts` (`Core/NativeStageVentFx.cs`, `Runtime/NativeStageVentPresentation.cs`):
    - the square warn-red rim that turns white while erupting;
    - the open-vent flicker and the lid rattle;
    - warning wisps, burst, jets and lid smoke, emitted into the shared flame pool.
    The orange cube rings are gone.
  - The Siren Marked seeker carries its pooled point light (`Core/NativeSeekerLight.cs`, cap 4).
- B5b:
  - Summon break-up follows the source: jitter, the rig hides at 0.75 s, three gib-coloured bursts, and the minionHit puff (`Core/NativeSummonBreak.cs`).
  - Ceiling-field hit holograms (`Core/NativeCeilingField.cs`, new `Resources/NativeCeilingField.shader`).
  - PvE pickups (`Core/NativePickupFx.cs`, new `Resources/NativePickupGlow.shader`): the glow sprite, the YOURS label on your own chips and the pulsing tier beam for epic and above. The old IMGUI YOURS label is now only a fallback.
  - Both new shaders are listed in `NativeBuildChecks`.
- Fix to batch 4: the match camera is not tagged MainCamera, so `Camera.main` is null in matches. The SPLAT! pop now faces the last `RenderGlow` camera (`WorldEye`), and the pickup glow faces the camera in its shader.
- Checks on the merged head:
  - unity-check: runtime+editor build OK;
  - Python: 52 OK;
  - package_source: passed, 992 files;
  - check_relay: exit 0, 224 PASS. New `StageVentChecks` runs 102,444 checks and `SummonCeilingPickupChecks` 1,665, both against pinned-source fixtures.
  - `git diff --check` is clean.
- Not run: a real D3D11 compile of the two new shaders (glslang HLSL syntax check only), the Windows font-atlas readback for YOURS (it falls back to the flat label), and live play.
- Handoff to Astra (also given to the owner):
  1. Melee hit stun (owner request "small hit stuns when hit by melee"; simulation is Astra's):
     - On a clean standard-melee or fighter Hit/Headshot, set `StunEnd = max(StunEnd, now + d)` by attacker weight. Suggested d: daggers ~0.12 s, sword ~0.18, shield ~0.20, greatsword ~0.28, fighter light ~0.10 and heavy ~0.22.
     - Exclude blocked hits, burn/poison ticks and ranged/magic hits. Bosses immune or reduced.
     - Anti-stunlock: no new stun while `StaggerImmuneUntil > now`; set `StaggerImmuneUntil = StunEnd + ~0.5 s`.
     - `stunEnd` is already replicated. Claude's hit-reaction work (in progress) plays a stun pose whenever `StunEnd > time`.
  2. Ceiling holograms:
     - set `Color`/`Detail` on `CeilingHit` (bounce 0x75ddff, absorb 0xb5a3ff; `NativeProjectiles.cs:79`);
     - emit `CeilingHit` for melee shots (`NativeMeleeSpecials.cs:185`);
     - drop or tag the ability-shot `CeilingHit` (`NativeAbilities.cs:213`), which the source never emits.
  3. The optional presentation-data follow-ups from batches 1–4 are listed in their entries below.
- Status: implemented and merged on the branch; pushed to the cloud branch for the test channel; not released by Claude.

## 2026-10-10 — Astra — correct the interaction build fixture

- Follow-up `codex/unity-interaction-build-fix`, base `d9ebe850` / PR #94.
  Unity #24 failed with CS0117: the Editor assembly cannot initialize the runtime's
  internal `NativeSeat.AuthorityBoss` field. Move that fixture-only assignment and
  PvE setup into the existing runtime-assembly `UNITY_EDITOR` validation method.
  Keep the field internal. Also advance the fixture clock in supported .25s ticks
  instead of a rejected 2s `NativeMatch.Tick` call.
- Production grab/revival behavior is unchanged. No authority, hitbox, damage,
  movement, cooldown, audio or visual timeline edits in this follow-up.
- Native CI passed for #94, including 33 original grab samples and repair timing.
  Actual Unity compile/interaction checks and publication of this correction are
  pending. Failed #24 was not published; last verified feed remains Unity #23.
- Automation-only PR #95 now retains failed Unity logs privately; its 71-test
  Linux/Windows suite passed. Log retrieved from run `38078057168`.

## 2026-10-10 — Claude — Early-port placeholders, batch 4: sky death broadcast and vent fall/splat (owner request)
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62), on top of `e8bb235`; implementation commit `fcab36f`. I finished this from the partial `claude/placeholders-4` work. Presentation only. No change to simulation, death timing, `landAt`, revive rules, network frames or saves.
- Sky broadcast (`Core/NativeBroadcastFx.cs`, `NativeBroadcastEdges.Generated.cs`, `Runtime/NativeDeathFxDraw.cs`) replaces the first port's cube rocket, cube stars and beam frame with the source `death-announcements.ts` look:
  - a line-trail rocket;
  - point-sprite stars;
  - the flat 0x62dcff portrait hologram with its 24° edge lines;
  - drifting scanlines and the corner/tick frame;
  - the victim / verb / killer nameplate.
  Only the newest three launched broadcasts show. The new `Resources/NativeDeathFx.shader` is listed in `NativeBuildChecks`.
- Vent deaths (`Core/NativeVentDeath.cs`, `Runtime/NativeVentDeathPresentation.cs`) draw `vent-fall.ts`'s scripted victim instead of the sliding corpse:
  - fall: the carried last pose, flail, drop and spin;
  - splat: the fall, squash, wobble and held pose, the floor dust puff and the SPLAT! pop.
- One-line hook in Astra's `NativeInteractionPresentation.StartRevival`: a kept vent-splat body is rebuilt from the body as drawn. The partial batch's own revive-hologram port (`NativeRegen`) is dropped, because Astra's `b1f908d` owns the revive.
- Checks:
  - unity-check: runtime+editor build OK;
  - Python: 52 OK (1 skipped);
  - package_source: passed, 966 files;
  - check_relay: exit 0, 222 PASS. The new `DeathFxChecks` runs 32,618 checks against pinned-source frames for the broadcast, vent fall and splat, plus runtime-hook scans.
  - `git diff --check` is clean.
  - The offline sky-broadcast sheet matches the source.
- Not run: no subagent review (usage limit), no offline render of the vent fall/splat runtime draw, no real D3D11 compile of the new shader (the cloud build's `NativeBuildChecks` gates it), no Windows play.
- Remaining: B5 (world/stage items) is research only and waits.
- Status: implemented on the branch; pushed to the cloud branch for the test channel; not released by Claude.

## 2026-10-10 — Claude — Early-port placeholders, batches 1–3: impact VFX, rig dressing, HUD/menus (owner request)
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62), merged on top of the cloud head `d9ebe85` (Astra's victim pose, revive hologram and intro audio clock). Batches built on `claude/placeholders-1` `9db1d32`, `-2` `8f95076` and `-3` `cf01e49`. Presentation only. No change to simulation, rules, hit timing, hitboxes, damage, cooldowns, movement, network frames or saves.
- B1, impacts: the native-arena ParticleSystem hit burst is replaced by the source's event-vfx voxel sparks for hit, block, break, impact and collapse (`Core/NativeImpactCues.cs`). Also ported from the source:
  - proc impacts (critical, catch, split, ricochet);
  - saw, lava and spike-rise hazard sparks;
  - green L33t bursts and streaks;
  - Ball Lightning arc lines (the cube bars are gone);
  - the Pong rally paddle and streak;
  - the grey rocket dud.
  The flat Siren halos are removed. Standard melee hits get the weight-scaled spark flash, and slams get the source's dust and stones (`Runtime/NativeMeleeFlash.cs`). No new shaders.
- B2, rig dressing:
  - body chips, i.e. the source `bodyModifiers` (`Core/NativeBodyChips.cs`);
  - the right-hand spell charge orb, the Ansatsuken charge orb and the element hand motes (`Core/NativeSpellCharge.cs`);
  - block-cooldown dimming, plus the Ansatsuken parry flash drawn as a ghost overlay, an approximation of the source tint (`Core/NativeBlockTint.cs`);
  - the Siren's weapon socket stays hidden, as in the source (`NativeWeaponDressing.SocketShown`);
  - the source first-person base for the fighters' fists.
  Arena and first-person rigs only. Intro, viewer and draft rigs stay bare.
- B3, HUD and menus (`Core/NativeHudRules.cs`):
  - the proc/tick hitmarker and the crisp X with corner tips;
  - SPRINT DEPLETED and the SPRINTING tag;
  - the L33t CONTROLLING / POSSESSED BY / ADMIN PAUSE box;
  - the DEV fighter cards, charge bar and combo meters;
  - the Siren mode/move HUD.
  The home Catalogue link is now developer-only. The Sandbox gains Restore, Arena Reset, Test Block, the Draft/Rarities/Knockout/My Death previews and a debris launcher, all wired to the existing calls.
- Merge: kept both sides in `NativeSourcePresentation` (Astra's `NativeSirenVictimPose` weapon hide and `ClearRevives`, plus the rig dressing) and in `HiddenSweep` (Astra's `posed` reuse, plus the hand motes).
- Checks on the merged head:
  - unity-check: runtime+editor build OK;
  - Python: 52 OK (1 skipped);
  - package_source: passed, 951 files;
  - check_relay: exit 0, 221 PASS, with new impact-cue, rig-dressing and HUD parity fixtures generated from the pinned source;
  - `git diff --check` is clean.
  The planned two-lens subagent reviews did not run because of the usage limit. I reviewed the three diffs myself.
- Not run: the Unity Editor, a real D3D11 compile (no new shaders), a Windows player, live host/guest/killcam play, and the parry-flash look under real lighting.
- Astra follow-ups (optional, presentation data only):
  - Impact `From` and colour on every Impact; Hit events at the contact point;
  - `slowed` / `meleeRebound` / missing `leet-*` events, the rocket-dud Detail, and lightning-chain / Blink Strike Arc events;
  - guest replication of `SprintExhausted`, the L33t `ControlUntil` and the Siren cooldowns;
  - hit elements on Hit events;
  - the source Sandbox debris modes and the one-chip-per-rarity preview;
  - an exact parry time and dash clock.
- Not done: B4 has only partial work (the death-broadcast firework is ported; the vent fall/splat is unwired), and its revive hologram is superseded by Astra's `b1f908d`. B5 (world/stage items) is research only. Both wait.
- Status: implemented and merged on the branch; pushed to the cloud branch for the test channel; not released by Claude.

## 2026-10-10 — Codex/Astra — Siren victim animation and PvE holographic repair
- Task `codex/unity-grab-revive`, base cloud `efb4fa4017e68b12dbfda2db166e52f397913539`; owner reports the grabbed player remains idle and requests the Electron holographic revival back. Restores the existing source presentations (`siren/fx.ts` and `pve/downed.ts`) in Unity.
- Replicate the actual victim's holder/catch/crush/release fields. Pose all rigs before drawing, attach the struggling victim to this frame's claw, suppress the held weapon, and use explicit identity for guest flames/cameras. Held guests no longer predict locomotion. The visual pose does not move the authoritative collider.
- Replace the placeholder revive ring/fade with the cyan scan, wound sealing, bounded reconstruction voxels and a 0.8 s scan / 0.6 s stand from the kept body. A distant pad materialises the live body. Handles intact, severed and Siren-crushed bodies, newest-frame health ahead of the render clock, interruption, replay reset and reduced-effects budgets. Adds a dedicated additive scanline shader; existing holograms and Siren intro flicker remain unchanged.
- Local: source-extracted fixtures generated from 33 Electron victim poses; `git diff --check` clean. Adds managed pose/timeline checks and real Unity gates for host/guest codec, release/prediction, intact/gibbed/crushed/pad repair, finite rig transforms and unchanged health/colliders. Native CI, activated Unity shader/rig/PhysX checks and publication are pending at this commit; Windows visual acceptance is not claimed.
- No changes to hit timing, hitboxes, damage, cooldowns, authoritative movement, revive cost/health/shield or saves. Network payload additions are presentation fields with zero defaults; existing protocol retained. Release results will be recorded on the PR to avoid a docs-only cloud rebuild.

## 2026-10-10 — Codex/Astra — Siren intro audio clock repair
- Task `codex/unity-siren-music-sync`, base cloud `7b2f035f319d7ae029effbb3298a689234e5ce13`; owner reports entrance/music drift and assigns audio/timing plumbing. Claude's `NativeSirenIntroLight` flicker timeline and all visual assets remain byte-for-byte unchanged.
- Use the music source's PCM sample position for both drift correction and the intro clock. The old streamed/compressed `AudioSource.time` can report packet positions seconds apart, causing repeated seeks. Wait for background loading, cue the current age, allow seeks to settle, and resume paused sources with `UnPause`. Identify new previews/matches explicitly so the same song restarts only for a new song epoch.
- Replace the playback/intro 0.5 s proximity test with replicated origin ownership: first PvP Siren and held PvE intro follow their heard song; later PvP Sirens retain their own entrance age while the original song continues. Customize now updates audio before rendering and uses the same clock. Explicitly reset the live clock for each entrance.
- Added offline checks for 0.8 s cold loading, PCM versus packet readbacks, correction settling/restarts and clock ownership across the old cutoff. Added Unity gates for first/second PvP Siren origins through the real codec and PvE hold/start/finish plus actual guest frame application.
- Local checks: `git diff --check` clean; cold-load fixture arithmetic checked (11.6 ms maximum simulated error). Local .NET/Unity are unavailable; hosted native CI and the real Unity build gates are pending at this commit. No Windows listening/playtest result claimed. No changes to authoritative phase duration, hit timing, hitboxes, damage, cooldowns, movement, network protocol or beat table. Release verification will be recorded on the task PR.

## 2026-10-10 — Codex/Astra — exact-head CI for publication
- Task `codex/unity-publish-ci`, base shared/cloud `ff81074951cd94ad09e6974ae6a842646e6860c1`; owner requested final visual-batch build/publication and pipeline repair.
- Remove the native workflow's push path filter so docs-only Unity/Claude branch heads run both native CI jobs. Keep the existing branch filters, job names, permissions and exact-head publication gate. No inherited/parent CI acceptance and no fallback to older game source.
- Run #44 (`38058241777`) waited 15 minutes on UBA #17 `sentToBuilder`, then intentionally deferred; logs still targeted `64084ad`, so branch movement was not the cause. Main automation's separate `codex/unity-publish-outcomes` repair adds explicit deferred/published outcomes and skips redundant preparation after a wait.
- Validation: YAML structure/diff review passed. Both hosted native jobs passed on `0cc2ecd` (run `38064992931`); scope/whitespace passed. Legacy verify retains the unchanged Electron TypeScript/unit/websocket failures. Main publisher repair PR #89 passed hosted Linux/Windows tests and merged as `0cdb005`.
- This documentation-only follow-up exercises the repaired trigger. The final cloud head must still pass its own CI, real Unity shader/build gates and public delivery verification. No gameplay, shader, animation, networking, account or save changes here. Claude's placeholder batch is still absent from shared `ff81074`; no final visual-batch release claimed.

Read recent entries and relevant open PRs before starting work. Keep entries concise,
newest first; use Git diffs and tests as implementation evidence. Entries on task
branches are not automatically visible in other checkouts. Check GitHub for current
merge/release status; an entry records status at the time it was written.

For every completed task or unfinished handoff, record:
- Date, agent, task, branch, base commit, and PR or implementation commit when known.
- Changes and rationale; relevant files/systems and compatibility impacts.
- Actual checks with passed/failed/not-run outcomes and environment limitations.
- Remaining issues, overlapping work/dependencies, and the exact next step.
- Status: implemented on branch / merged / released (release version only if verified).
For animation/combat work explicitly state whether hit timing, hitboxes, damage,
cooldowns, movement rules, or network behavior changed. Never invent a commit,
PR, test result, release, or earlier log history. Git history identifies the commit
containing an entry; no self-referential commit hash is required.

## 2026-10-10 — Claude — Siren model: black flames restored, rimmed finish (owner playtest)
- Branch `claude/siren-model-upgrade` (`8f940db`, base `ff81074`). The review found nothing. Merged. Presentation only.
- Lavender cubes: `tools/export_cinematics.mjs` copied a flame cube's colour only from a material colour slot. The Siren flame material has none (the colour lives on each cube), so 420+ flame cubes in stage-dev-siren, death-siren-crush and death-siren-dissipate baked as the 0xaea4c7 fallback.
  - Re-exported with per-cube Linear colours. Only those three clips' flame columns were spliced in; asset-lock was updated.
- Flat black body: the rim in NativeSourceLit/NativePortrait used a fixed world axis, not the surface normal, so a front view had no outline.
- Per-piece source rim and streak strengths (claws, tongues and wisps, body), slow breathing on the eye halos and grin glow, and a faint pale-violet edge on the in-arena flame cubes. Reduced effects stays flat.
- SirenLookChecks pins the flame palette, no fallback colour, the rim expressions and the per-piece strengths.
- Checks on the merged tree: compile OK; check_relay 211 PASS; Python OK; package OK; `git diff --check` clean. Not run: Unity/D3D11 shader compile, Windows, multiplayer.
- Intro sync with the music (owner report): handed to Astra at the owner's request (NativeIntroClock / NativeAudio.SongPlayback; see the owner relay).

## 2026-10-10 — Claude — Intro NaN/boxes, Siren intro cell lights, Siren smile, seated chip draft, gun/Siren runs (owner playtest)
- Merged into `claude/sleepy-ritchie-g8ruhv` after builders, two-lens reviews and fixes (one run was interrupted by a usage limit and resumed). Presentation only: no change to hit timing, hitboxes, damage, cooldowns, movement, networking or saves.
  - `claude/intro-nan-fix` (`5d17801`)
  - `claude/siren-intro-backdrop` (`e9f26f0`)
  - `claude/siren-smile-reveal` (`d2e71f7`)
  - `claude/chip-seat-table` (`212e29f`)
  - `claude/run-animations` (`7e4f2c6`, no findings)
- NaN bone spam (owner's dev console: localPosition NaN on elbows, wrists, hips, knees, ankles):
  - Cause: the gameplay pilot rig. The Siren's weapon has windup 0, so `CapturedClip.Retime` divided by a zero-length knot span. The NaN clip time poisoned every joint and the pose history.
  - Fix: zero spans get no slope. `NativePoseHistory`, Blend and Sample reject non-finite input. The new `Runtime/NativePoseWriter.cs` refuses non-finite poses, keeps the last good pose and logs once.
  - A fuzz of every pose route went from 128,551 errors to 0.
- Intro boxes and smoke:
  - The flashing boxes were stage voxels blended toward empty placeholder rows in the cinematic bank. That blend is now held: 2,741 bad frames before, 0 after, across 20 entrances.
  - Gun smoke draws unlit and see-through: a new `_Unlit` property on NativeSourceTransparent, set by the intro smoke material.
- Weapon intros: new layered pass, `Core/NativeIntroMotion.cs`, on arms, chest and head only. Legs, hips, root, timing, name-card times and cameras are as baked.
  - Deadeye cups the pistol and Longwatch braces the fore-end; Belt Rattle and the greatsword keep their two-hand brace.
  - Pistol and rifle shots kick back and settle.
  - Body clearance is planned per clip.
  - Remaining: the greatsword sits about 17 cm inside the body in the slam, and the anchored cuts sink 4-6 cm.
- Siren intro cell:
  - Proven by a Unity-forward emulation: the instanced entrance stage never received Unity's point lights, so the cell was black.
  - The intro draws now evaluate the stage's point lights in NativeSourceLit/NativeSourceTransparent (up to 8, three's falloff), with ACES at .92 on lit stage parts.
  - `Core/NativeSirenIntroLight.cs` ports the intro.ts beat timeline, driven by the black-siren-protocol beat table. `Runtime/NativeSirenCell.cs` makes the bulb glow and halo follow it.
  - Guests draw live entrances on a smooth `NativeIntroClock` locked to the song.
  - The Customize preview plays the song and resumes the menu track.
- Siren smile reveal:
  - The old grin was a rounded bar. `Core/NativeSirenSmile.cs` ports pve-smile from the source: flames, flicker, almond eyes, a toothed grin opening from a slit, glow, scanlines and glitch streaks.
  - The rest of the reveal is fixed too: backdrop, layout, title drop, booting card, locked cards.
  - Textures are rasterised on a background thread; a leak on resize was fixed.
- Chip draft:
  - The pilot is framed chest-up, seated behind a lit table, with the designed 2D cards laid along its display. Forearms rest on the table, the hover lift and reach go across it, and the plant arcs from and to the rest.
  - The framing allows for the worn hat's measured height (wizard hat).
  - Known gap: two-row Siren picks float the first row over the chest.
- Runs:
  - Third-person guns take a new `PoseRoute.Gun` (`Core/GunMotion.cs`): the source gait plus the stances.ts gun stances, with the RangedPose hold layered on. The torso is on the aim while raised; running lowers the carry.
  - The Siren gets `Core/NativeSirenDrift.cs`.
  - Pose transitions are shared (`Core/NativePoseTransition.cs`).
  - The pack's converted runs are melee carries only; tried on guns and rejected.
- Checks on the merged tree (`0c0bb78`): Unity runtime+editor compile OK; check_relay 211 PASS, exit 0; Python 52 OK (1 skip); package_source OK.
  - Not run: Unity Editor/GPU, real shader compile (glslang HLSL check only), D3D11 player, Windows, multiplayer, on-screen check.
- Placeholder sweep: 50 distinct items, triaged into 5 fix batches. The batch agents did not build anything (an owner question reached them mid-task); they are being rerun.
  - Astra list from the triage: merge PR #88 (Admin Pause event, Impact events on adds/summons, Pong rally fields); Impact From and colour; slowed/meleeRebound events; L33t event origins; Arc events for the lightning chain, Blink Strike and shield catch; replicate SprintExhausted, DEV fighter HUD/charge, Siren cooldowns, parry/dash timing, juggle timing, and the Siren grab victim and crush time.
## 2026-10-10 — Codex/Astra — Emperor wave cancel and Claude's simulation/data follow-ups
- Task branch `codex/unity-combat-parity`, current shared/cloud base `b51583ea0a0a1a4681940732c36b3fd92fb884e1`; retains Claude's wavedash animation and the allied-bot repair. Owner requested the listed fixes; no unrelated backlog included.
- Emperor Q and jab can replace a wave without bypassing attack recovery or incapacitation. Held E resumes after the attack. Waves require ground contact, reset jab-chain memory as in the source, and keep their 9 m/s external push to the original 0.18 s deadline through a cancel, then use 3.1/s drag; Electric Hook adds its existing 6 m/s grounded step. Fixed the independent bot ability gate that otherwise suppressed mid-wave Q. Ansatsuken confirmed-cancel rules retained.
- Replicate authoritative `Actor.SourceModeAt` and `Shot.SirenTarget`, validate them, and feed existing Siren look/mark consumers for guests and replay (including first observation after the swap/throw). Specials no longer consume swipe/hellfire hand alternation. Leet E respects primary readiness and Admin Pause emits `Leet/leet-freeze`. Summon static/Corpse Party explosions have exact effect tags; contact hits on summons/adds emit Impact without adding DoT/blast contact effects. Pong redirects include target, rally detail and bounce origin. Intro selection normalizes unknown styles to neon-drop.
- Added genuine Unity authority/JsonUtility build checks: 11 Q tick offsets for local and remote one-shot input, held-E recovery, jab, bot decision, airborne gate, push/drag, cooldown/stun and Ansatsuken guards; Siren fields/side/serialization validation; Leet cooldown/event; summon impacts and explosion tags; Pong payloads. Existing add-contact gate now asserts impact/DoT separation. Managed tests cover drag, intro fallback and late-observation/replay mode timing.
- Local whitespace/diff review passes. Partial-checkout Python suite: 35 collected, 32 passed, 1 Windows-only skip, 2 errors from absent unchanged SourceModels/SourceMotion banks. No local .NET or Unity Editor is installed. Complete-tree native CI and real Unity gates are pending; no runtime pass or publication is claimed yet.
- Gameplay impact: Emperor cancel/movement/grounding and Leet readiness change as above; weapon damage, hitboxes and authored attack timing/cooldowns otherwise unchanged. Native snapshot adds fields; no account/profile save changes or new visual assets. Existing appearance algorithms are unchanged apart from consuming authoritative timing/target data. Windows rendered gameplay and live host/guest playtest remain unrun. Next: exact-head native CI, then verified existing test Update publication under standing owner authorization; source PR stays reviewable without an automatic shared merge.

## 2026-10-10 — Claude — Emperor wavedash legs and cancel blend (owner playtest); cancel fix for Astra
- Branch `claude/emperor-wavedash` (`c8c5454`, base `530321e`). Two-lens review found nothing blocking (one low note about Unity's simulation stopping the body on a cancel, below). Merged. Presentation only.
- Legs:
  - The original places the wave's feet over the gait (emperorLayers editFoot/applyFighterLegs, dashTime .18). The baked `emperor-wave` clip has no leg tracks, so Unity's gait ran a 9 m/s stride under the crouch with the feet dragging flat.
  - Now `NativeWaveDash` keeps the dash window and `NativeFighterPose.Feet` plants the lead and rear marks as in fighter-motion.ts, with the Electric Hook's rear heel lift.
  - Chained waves no longer twitch, and `MeleeMotion.FighterFinish` runs the original's pose transitions for both fighters, so a wave cancelled into Q blends over .055 s.
  - New `tools/export_fighter_motion.mjs`, `Tests/fighter-motion-parity.json`, `Tests/WavedashChecks.cs`. Legs within 2.3 mm of the original, against 70 cm before.
- **For Astra (cancel, simulation):** in `Runtime/NativeFighters.cs` `NativeArena.StartFighter`, `if((Time<p.Ready||f.Kind!=null)&&!cancel)return false;` rejects Q while `f.Kind=="wave"` (0.18 s). Q is a one-tick GetKeyDown, so the press is lost (1/11 taps start the hook; the original gets 11/11).
  - Suggested change: `bool cancel=emperor?kind=="electric"&&f.Kind=="wave":kind!="combo"&&Has(p,"arcade-cancel")&&f.Kind=="combo"&&f.LastHit>=f.At&&Time-f.LastHit<.5f;` (the original's emperor.start ignores the wave).
  - Optional: let a jab cancel too (`f.Kind=="wave"`).
  - Movement parity: in the original the 9 m/s wave push carries through a cancel, plus the hook's 6 m/s step, and the wave requires being grounded. Unity zeroes MoveVelocity on StartFighter.
  - Harness: scratchpad `wavedash/cancel/run.sh`.
- Checks on the merged tree: Unity runtime+editor compile OK; check_relay 179 PASS, exit 0; Python OK (1 skip); package_source OK. Not run: Unity Editor/GPU, Windows, multiplayer.
- Also still in progress after the usage-limit interruption: chip seat (chest-up table), intro NaN/artifact fix, Siren intro lights (review fixes), Siren smile reveal and gun/Siren runs (reviews), placeholder sweep.
## 2026-10-10 — Astra — allied bots attack in Siren boss mode
- Task branch `codex/unity-boss-allied-bots`, base shared/cloud `530321ec2b5e3b0ee13678db1517166d6f2674ae`. Owner reports allied bots moving without attacking.
- The arena evaluated PvP AI before PvE AI. The first pass consumed the bot reaction window on Siren; the second could select a minion but retain aim at Siren. Route through one mode-specific brain before observing a target.
- PvE combat now bypasses inherited PvP stationary/dummy suppression, which previously stopped attacks while the PvE leash still moved allies. This follows the source's independent `src/shared/pve/recruit-ai.ts` fighter brain. Medics remain non-attacking; PvP practice settings still apply in PvP. Party bots and combat recruits use their existing weapons, abilities and cooldowns.
- Added an actual Unity/PhysX Editor gate to the existing PvE acceptance run: gun, magic and melee bot inputs must damage minions with Siren hidden and visible; aim must follow the selected minion with Siren on the opposite side; inherited practice settings, combat recruits, medics, unexposed/dead targets, visible boss targeting and PvP dummy preservation are checked.
- Local diff/whitespace review passed. Python run: 49 passed, 1 Windows-only skip, 2 visual-bank failures because the connector could not retrieve the latest large SourceModels bank into this partial checkout. No visual bank is changed by this patch; GitHub will test the complete remote tree. No local Unity/.NET runtime is installed. Real Unity gate, hosted checks and publication pending; do not treat the local run as release approval.
- Gameplay impact: bot decision/aim and PvE attack suppression fixed; weapon hit timing, hitboxes, damage, cooldowns, player movement, network schema, account/profile saves and presentation unchanged. Implemented on task branch, not yet merged or released. Next: complete-tree native CI, real Unity build and verified existing test Update publication under owner authorization.

## 2026-10-10 — Claude — Siren Marked crosshair and the animated, upgraded Auditor halo (owner requests)
- Base `claude/sleepy-ritchie-g8ruhv` at `4bdd90b`. Both branches were reviewed, then merged. Presentation only: no change to hit timing, hitboxes, damage, cooldowns, movement, networking or saves.
  - `claude/siren-target-telegraph` (`cb11846`). Review found the crosshair lingering 0.5 s after the seeker hit; fixed.
  - `claude/auditor-halo` (`608a39b`). Review found the flames burying the arcs from above (fixed by slimming them into hooked horns) and red speckle at 12 m (fixed by a detail fade). One sort-order limitation was accepted.
- Siren Marked (fourth ranged move):
  - The source's warn-red lock-on crosshair was never ported, only a near-invisible cube-ring stand-in, now removed.
  - New `NativeSirenMark` and `NativeSirenMarkTracker` port fx.ts updateMarks/markBeeps: ring, inner square and swinging ticks, shrinking 1.6 to .85, a flash and 12% swell per beep, drawn through walls on a depth-ignoring overlay layer.
  - The NativeSirenFx shader gets a `_ZTest` property (default LEqual).
  - Draws on host, guests and killcam from the mark records plus seeker bullets.
  - Unity addition: the marked player's own first-person view holds the crosshair 1 m ahead (the source shows nothing there). It ignores reducedEffects because it is a warning.
  - Fixture: `Tests/siren-mark-parity.json` from `tools/export_siren_mark_fixtures.mjs`.
- Auditor halo:
  - The arena, killcam, intro, draft, corpse and death-scene hats used the static bank model, so the halo never animated. Only portraits had an animated copy.
  - New `Core/NativeAuditorHalo.cs`, `Resources/NativeAuditorHalo.shader` (registered in NativeBuildChecks) and `Runtime/NativeAuditorHaloPresentation.cs` now draw it everywhere it is worn, portraits included; the portrait-only copy is removed. First person stays hidden.
  - Look: the source's bob, spin, tilt and scale, with three tapered arcs (hot core, travelling pulse, breathing gaps) and soft glow shells under black shader flames (hooked horns, ember rims, licking tongues). Plus crackle across narrow gaps, rising embers, a faint haze and a small red vertex light in the arena.
  - Reduced effects falls back to the source's flat flames; a missing shader falls back to the static model.
  - About 4 draws per halo.
- Checks on the merged tree: Unity runtime+editor compile OK; check_relay 170 PASS, exit 0; Python OK (1 skip); package_source OK.
  - GitHub Actions jobs were not starting (no runner, 3 s failures) until the owner upgraded the account. After the re-run, native-source and native-updater-windows passed on `4b3e178`.
  - Not run: a Unity compile of the new halo shader (glslang HLSL check only), D3D11 player, Windows build, hosted multiplayer, on-screen check.
- For Astra (optional): keep the mark record while its seeker lives, and add `sirenTarget` to the replicated Shot. That would make the post-throw crosshair exact on guests and the killcam.

## 2026-10-10 — Claude — Siren claw timing, weapon intros, random explosions, chip stage gaze, L33t H4x paths (owner playtest)
- Base `claude/sleepy-ritchie-g8ruhv` at `7149fdc`. Branches, each reviewed then merged:
  - `claude/siren-claw-timing` (`331759a`, no findings)
  - `claude/fix-weapon-intros` (`9ed791d`)
  - `claude/fix-random-explosions` (`34729f1`; review found a Siren add death puff, fixed)
  - `claude/chip-stage-gaze` (`c6a416a`; review found a wrong-card gaze between cards and the missing hover arm lift, both fixed)
  - `claude/leet-fixes-b` (`54e3db0`, checks only)
  - Check fix `eb229a9`.
- Siren claws:
  - The swap starts at the authoritative snapshot time of the flag flip, not the render frame. Guests were about 0.1 s early.
  - The idle stance now blends with the claws and orbs over .35 s with motion.ts's .5 s shudder.
  - The sword floor guard no longer bends the right claw in Slam and Rush.
  - Fixture: swap and swipe timelines against every source joint.
- Weapon intros:
  - The intro bake (`tools/export_motion.mjs`) now arms the rig like player-intros.ts, so braceWeapon poses the free arm. Rebaked left-arm tracks for intro-weapon-greatsword, rifle and machinegun; other clips byte-identical; asset-lock updated. If SourceMotion.json conflicts, rebake it, don't merge it.
  - The glow voxels in the stage blasts fade by alpha instead of turning into opaque dark cubes. These were the artifacts.
  - Gauntlets sit on the hand socket, and Belt Rattle shows its muzzle flash.
  - Risk: 8 NaN alphas in the stage blob shadow, also present in the source.
- Explosions:
  - Removed the first-port AbilityEffects stand-ins: fire on severed add limbs (the gun-kill fireball), darkness on summons, water on revives, fire on pong bounces, and Tidal Wave/Shackles wall blasts. These calls in sim files only fed visuals.
  - NativeBlastPresentation draws a blast only with its matching Explosion/Vacuum event, styled from the event tag (`NativeBlastCues.ExplosionTag`).
  - The static discharge on adds is a shock arc with the lightning cue.
  - Removed the native death particle burst and the white puff on minion deaths.
- Chip stage:
  - The pilot is framed behind and above the tray, bowing toward the cards. The head follows the highlighted card on a critically damped spring (about 0.4 s); the hover arm lift uses the source springs.
  - The plant timeline is about 1.3x slower (plant and install cue at 1.14 s), and the pick runs on a smooth clock (`Core/NativeDraftStage.cs`).
  - Cards and the wrist plant are unchanged.
- L33t H4x:
  - No path in the current code plays the sword set. The owner's build likely predated 7149fdc.
  - E (Root Command) exploding at the pilot is source-faithful (`leet.ts` explode 'root'). Kept pending the owner's call.
  - New `Tests/LeetPathChecks.cs`.
- Checks on the merged tree:
  - Passed: Unity runtime+editor compile OK; check_relay 168 PASS, exit 0; Python OK (1 skip); package_source OK.
  - Not run: Unity Editor/GPU, D3D11 player, Windows build, hosted multiplayer, on-screen check.
- For Astra:
  - Optional `Actor.SourceModeAt` (Siren ModeAt) for exact guest/killcam swaps.
  - Siren Side should alternate only for swipe/hellfire.
  - `effect:"static"`/`"corpseParty"` on NativeSummons ChipBlasts.
  - Pong event Target/From.
  - Impact events on adds and summons.
  - A Leet event for Admin Pause ('leet-freeze').
  - StepLeetPilot ready check.
- Claude follow-ups:
  - The source's green leet-* bursts and the green Leet beams are not ported.
  - Summon break-up timing and colours.

## 2026-10-10 — Claude — Animated Siren look and L33t H4x magic gait (owner request)
- Base `claude/sleepy-ritchie-g8ruhv` at `a6de3d5`. Branches `claude/siren-look` (`e53aac9`) and `claude/leet-gait` (`90b4248`). Each was reviewed (Siren: one low DEV_LOG note, handled here; leet: no findings), then merged. Presentation only: no change to hit timing, hitboxes, damage, cooldowns, movement, networking or saves.
- Siren:
  - Export: `tools/export_models.mjs` now keeps the Siren's animated pieces separate: eye, grin rows, claw groups, and one shared flame box for the 112 tongue steps and 12 wisps. Palm orbs, halos and the flame pool are no longer baked. Only the `siren` model in SourceModels.json changed (19,049 to 4,817 vertices); asset-lock and provenance are updated.
  - Face tracks: `tools/export_siren_look.mjs` writes `Resources/SirenLook.json`, the per-clip mouth and squint targets.
  - Runtime: `Core/NativeSirenLook.cs` ports applySirenLook and the face/lean part of finishLook. Claws shorten to .32 in ranged mode; palm orbs and halos grow in, pulse and flare; eye halos glow at .42; the eyes widen, squint and cut their brows; the tongues flicker and the wisps sway; the Damnation ball swells. It is driven only by replicated Actor fields.
  - `Runtime/NativeSirenLookPresentation.cs` draws it for the third-person and PvE boss Siren, host, guests and killcam. The drafter and portraits get the idle look with eye halos. No new shaders; first-person claws unchanged.
  - The bank now requires the named Siren pieces, and NativeBuildChecks checks them.
  - Offline render, source vs port, for a melee frame (rend) and a ranged frame (hellfire): same triangle count, mean difference .06 to .18 of 255.
- L33t H4x: in the source it is a plain magic staff pilot (MAGIC stance, default magicLayer branches, .18 s release, no clips of its own).
  - New `MagicPose.School` (5 = leet; replicated MagicVisual stays 0) and `Core/NativePoseRoute.cs` send it through the magic gait in third person and the magic cast and staff view in first person, so no sword idle, run, cut, block or stagger plays.
  - The magic gait now shows the juggle and ports the finishPose blends for all five magic pilots.
  - Allocation fixes: cast-phrase keys and the roll tables.
  - `tools/export_magic_motion.mjs` adds three leet timelines, matching the source within .08 mm; `Tests/LeetMotionChecks.cs`; and `ValidateLeet` in the Unity build's ValidatePlayback.
- Checks on the merged tree: Unity runtime+editor compile OK; check_relay 160 PASS, exit 0; Python OK (1 skip); package_source OK.
  - Not run: Unity Editor/GPU, D3D11 player, Windows build, hosted multiplayer, on-screen check.
- Known gaps:
  - The Siren claw/orb blend starts when the replicated SourceRanged flag flips, not at the source's modeAt. Astra, optional: replicate NativeSirenState.ModeAt.
  - The dev-siren intro and the Siren death cinematics stay baked scenes.
  - The shadow pool's airborne fade is not ported.
  - A hit-stunned magic pilot keeps the staff carry under the reaction (the source skips magicLayer there).

## 2026-10-09 — Claude — Owner playtest fixes (chip tray, DEV fighters, lightning, intros, music, accent glow, magic walk)
- Base `claude/sleepy-ritchie-g8ruhv` at `ece4c11`. Fix branches were each reviewed, then merged: `claude/fix-accent-glow-b`, `claude/fix-magic-walk`, `claude/fix-siren-leet-intro`, `claude/fix-intro-music-d`, `claude/fix-emperor-anim-d`, `claude/fix-chip-select-d`. Lightning follow-up commit `25054a6`. All presentation or audio only.
- Chip selection: the table is reverted and the tray draft is restored (603ca76 screen and presentation, card sizes and motion), with a wrist plant at real size: chip at most 14 cm, smaller than the 14.5 cm palm; plant at .88 s; install cue at the plant.
  - Card art ported for the first time (`Runtime/NativeChipArt.cs`): per-tier metals, traces, spreader, die and line icon, gradient names, and kit art for Emperor, Ansatsuken, L44T and Siren.
  - The Tone light-layer remap now follows the sRGB curve. The owner screenshot shows the player blends IMGUI in sRGB, so Tone was not why the cards looked plain.
- DEV fighters: own pose path (`FighterPose`, Emperor/Karate stances) so running no longer uses the sword run. Emperor Shift roll shows. The juggle pose (`NativeJugglePose`) and the knockdown now show on all rigs; the juggle reads the replicated JuggleUntil, so it works on host, guests and the killcam.
- Lightning (owner: none on basic attacks): removed the first-port swing and echo blast in `NativeFighters.Fx(...,electric?1:3)`, which drew the Shock blast on every non-electric fighter hit (Ansatsuken moves included). The Emperor jab's .65 impact and the bolt on a jab's juggle hit are dropped. Kept: Electric Hook, Twin echo, storm SHOCK, bound/wall splat and Siren launch bolts. The first edit removes calls in Astra's sim file; they only fed the visual ability-effect list, so hits, damage and timing are unchanged.
- Intros:
  - Lighting: lights are converted for the Linear pin, fixing the Siren cell's blow-out (bulb about 30x too bright since build #7). All 20 entrances are darker and closer to the source.
  - The intro rig holds only a weapon intro's own weapon or the Emperor gauntlets, which fixes the L33T Throne double staff and the Rain Warrior fists.
  - A style with no entrance stage falls back to neon-drop instead of a black intro.
- Music: PvP stage music is silent outside countdown, fight and result, and the PvE intro dips to .35 (`NativeStageMusic`, 28-phase fixture). A paused rotation track no longer reshuffles every frame. Menu music is held under the intro preview.
- Already pushed: accent glow (accent tint on trims and halos, own team band hidden for the local pilot) and the magic walk (the torso follows the magic gait instead of being reset every frame).
- Checks on the merged tree: Unity runtime+editor compile OK; check_relay 155 PASS, exit 0; Python 48 OK (1 skip); package_source OK.
  - Not run: Unity Editor/GPU, Windows build, hosted multiplayer, a listening test, the card art per-frame IMGUI cost (about 270 draws per card), and on-screen confirmation of the Siren cell lighting.
- For Astra:
  - `NativeIntroRules.Style` can return an unknown `weapon-siren` for a Siren seat whose access check fails, including an expired 15-minute token. It should return neon-drop. Source gates dev-* intros on canUse.
  - Replicate the juggle start and screw-spin end (JuggleAt, JuggleSpinUntil).
  - Port the stepJuggle extension (`if(Time>=j.Until)j.Until=Time+.2f`).
- Open:
  - The arena Siren is a static export (no claw retract, palm orbs, eye halos or face animation). The owner has not said which model problem they meant.
  - L33t H4x (MagicVisual 0) still walks on the sword set.

## 2026-10-09 — Claude — C23 CSS effects and the chip install cue at the plant
- Owner direction (2026-10-09): Claude pushes verified completed game batches directly to `codex/unity-cloud-current` (Unity auto-build and automatic test-channel publication); no per-batch confirmation. The previous batch (`9f16ca2`) was pushed there by Astra at the owner's request.
- Branch `claude/wip-c23` (`54d4de3`, review found nothing), merged into `claude/sleepy-ritchie-g8ruhv`. Presentation and audio timing only.
- Backdrop blur: new `Runtime/NativeUiBlur.cs` + `Resources/NativeUiBlur.shader` (13-tap separable Gaussian). A command buffer on the last screen camera (AfterEverything) copies the finished 3D frame at quarter and eighth size only while a panel asked for it in the last 30 frames, then releases it; `NativeUi.Backdrop` draws it under rounded panels (U.Panel, the lobby shell, the death card), falling back to the plain tint. Text `filter: blur` is drawn as soft kit copies (intro lines and name, boss intro, count tick, result word). Clip-path: `U.Polygon` (lightning/Emperor class band) and exact Siren split bands (chip card name, Siren reveal, PvE banners, boss title letters). CSS 3D card turns: none remain after the chip table.
- Install cue (`Core/NativeInstallCue.cs`): PvP draft plays once at the plant (ChosenAt + 0.9 s); the Siren pick holds the player's own Install event until the follow-through's plant (or its early end). DraftTableChecks covers the windows.
- Checks on the merged tree: Unity runtime+editor compile OK; check_relay 143 PASS; Python 45 OK (1 skip); package_source OK (846 files).
- Not run: the blur capture and shader on a GPU (HDR/MSAA, orientation, frame cost), real IMGUI band clipping, Windows build, listening test.

## 2026-10-09 14:52 EDT — Astra — account setup released through Update
- Game PR #81 plus test-isolation PR #83 are merged at shared/cloud
  `603ca76b69b2256d58b24abc4ac23166306e1fd4`. UBA #8 built that exact commit;
  native CI `37974507246` and real Unity 6000.3.24f1 / Linear compile, Editor and
  delivery gates passed. Account URL hidden, default service/migration installed,
  full profile/API/grant contracts retained. No account database changes or resets.
- Initial Unity auto-build did not queue after the push. Main automation PR #82
  (`f6137be`) adds a pinned, CI-gated build-start fallback using the existing target.
  Ten added failure/duplicate/branch/request tests pass; full suite 65 pass / one
  skip locally and both hosted platforms pass. Actual API call requested #8 at
  18:38:17 UTC; success observed at 18:50:37. No configuration or access changes.
- Released native version `80b8edb903c75a79175839ef4a26090526c794d03f1cc1b4a1a1b092bba130a8`,
  revision `639271684416413583`, payload SHA256
  `0226ccd91ad9eb3120607d18bb294646c440d58b55c1982a0fbfeb87f71cdb15`.
  Feed push `e19ece73917f3320a0f38a7a1804e2c5ae51955f`; first run accurately reported
  a public-cache delay. Verification-only attempt 2 of `37974643037` succeeded;
  every public payload part and manifest passed archive/hash validation at 18:52:39
  UTC, receipt `b4462d36e4cc63625a48487b9ad6eb5a1de5184f`, verified=true.
- Private diagnostic logs: Actions run 37974643037 / artifact
  native-cloud-publication-37974643037. Publisher validated complete provider and
  delivery logs; no compiler/shader failure accepted. The returned artifact's
  temporary download was blocked (403) in this runtime, so no separate local log
  inspection is claimed. Existing Electron Checks still reproduces 12 unit failures
  and the documented chaos/leet/standard-melee TypeScript errors; native CI passes.
- Sheldon account already has DEV and its existing credential matches. No passwords,
  tokens, recovery codes or user records committed or exposed in Connection report.
  All requested APIs/profile fields and Reload saved pilot remain. Windows sign-in,
  DEV menu and cross-device save/load need owner runtime verification after Update.
  No gameplay, hit timing, hitbox, cooldown, movement or animation changes.
## 2026-10-09 — Claude — Follow-ups on Astra's data, C16 cinematic cameras, C12 DEV fighter and Siren effects
- Lean pass (one builder, one reviewer, a fix only when needed). Branches merged into `claude/sleepy-ritchie-g8ruhv`: `claude/wip-followups-b` (`e1ac132`, includes the extra room UI fixes `e16b570`), `claude/wip-c16` (`b98295a`), `claude/wip-c12-b` (`4e5b2be` Siren, `321d6ce` DEV fighters, review fix `ddbc7bb`). Presentation only: no simulation, rules, damage, timing, hitbox, movement, networking or save change.
- Follow-ups: event sounds now route by Astra's Detail tags (BLAST_CUE layers per ability fx, static arc cue, Leet nuke at 1.0, proc and debris cues by damage kind, break material, blink-strike teleport, ball-lightning/leet-lock arcs, saw hazard); 98 source scenarios in EventFeedbackChecks. Real zone Born, ChipField.Effect, per-punch fighter sweeps from LastAttack, directed gore normals; cached CA- room code display; stale "needs Astra" notes removed. The held-chip size fix on this branch is superseded by the chip table.
- C16: `Core/NativeCinematicRules.cs` (Raging Demon victim screen and Siren Last Laugh cutscene paths/timings, fixture-checked) and `Runtime/NativeCinematicCamera.cs`, called first in ConversionGame.UpdateCamera, shown only to the involved pilots.
- C12: `Core/NativeFlameField.cs`, `NativeSirenFx.cs` (siren/flames.ts, fx.ts, motion.ts emitFlames, first-person.ts; 48,809 fixture checks) and `NativeDevFx.cs` (emperor-effects.ts, matrix-effects.ts, hadouken-visual.ts, demon-afterimages.ts; 91,476 checks); runtime `NativeFxDraw.cs`, `NativeDevEffects.cs`, `NativeDevFighterEffects.cs`. Replaces C9's Siren flame/eruption stand-ins and adds the C17 black-flame puffs and stump drips. Review fix: Emperor echo/storm-trail impact sizes.
- Checks on the merged tree: Unity runtime+editor compile OK; check_relay 142 PASS; Python 45 tests OK (1 Windows-only skip); package_source OK (840 files).
- Not run: Unity Editor/GPU, Windows build, live host/guest/killcam, listening tests.
- Needs Astra: static discharge from hits on summons should pass effect:"static" (NativeSummons.cs:44); accept/format CA- codes with the source's inner dash ("CA-ABCD-2345"); replicate the Siren grab victim's seat (and crush time) so a grabbed guest gets the Last Laugh cutscene and the burn lands on the right pilot; typed Siren and Emperor events (A21) with positions/radius; replicate NativeSirenState.Reach (and optionally ModeAt); a Slender death record/event at the body centre; optional NativeJuggle.Hits next to JuggleUntil.

## 2026-10-09 — Claude — Chip selection at a table (owner redesign) and the Linear UI blend fix
- Owner, after native build #7: the chips had lost their polish, the selection was not smooth, and the chips were bigger than the pilot; asked for the pilot seated at a table with the chips laid out, seen from above, planting the chosen chip into the wrist. Answers: both the PvP draft and the Siren picks; a high tilted view (~65°); real-size chips with lift + side card.
- Branch `claude/wip-chip-table` (`fee2c20`, review fix `1c098af`), merged into `claude/sleepy-ritchie-g8ruhv`. Presentation only: draft/pick rules, timers, messages, networking and saves unchanged.
- New `Core/NativeDraftTable.cs` (layout by grade, 65° framing for 16:9 and 21:9, seated chair pose with IK lean/look/reach, lift-tilt-glow hover, grab-carry-plant-pulse pick on the replicated pick age) and `Runtime/NativeDraftFollow.cs` (the Siren pick's 1.7 s follow-through, skippable by any key/click and cut short when attacked, dying, leaving or holding). `NativeDraftPresentation` draws the table with the existing cube/ring batches (no new shaders); `NativeDraftScreen` drops the tray/shatter/fly-to-palm/spotlight for table hover/click and the full chip card beside the table (INSTALLED state after the plant). Chips: largest lifted chip 75% of a palm; 41-49 px at 720p, 67-73 px at 1080p, 89-98 px at 3440x1440.
- Linear UI fix (`Runtime/NativeUi.cs`, kit-wide, alpha only): build #7 pinned Linear, so IMGUI blends translucent layers in linear light and the card's 3% tints, glows, halos and shadows came out far stronger/hazier than CSS. `NativeUi.Tone` remaps alpha (light layers a^2.2, dark layers 1-(1-a)^2.2, mixed by lightness, ramp textures likewise) only when the colour space is Linear and sRGB write is on. Offline linear-blend emulation of the card: mean error vs the sRGB reference 8.98 -> 3.72. Assumes Unity linearises IMGUI vertex colours; confirm on a Windows build.
- Checks on the merged tree: Unity runtime+editor compile OK; check_relay 137 PASS (new DraftTable layout/framing/motion checks incl. 0-allocation Step); 24+21 Python tests OK; package_source OK (811 files).
- Not run: Unity GPU rendering of the table and of the Linear blend fix, a hosted PvP draft with guests, a live Siren pick, Windows build. Known: the routed install cue still plays at the click (NativeAudio), not at the plant.

## 2026-10-09 — Astra — connect existing accounts and preserve pilot contracts
- Task `codex/unity-account-contract`, PR #81, base shared/cloud `27a7833`. Owner
  chose efficient implementation, asked to hide the service URL and explicitly
  authorized publishing through Update. Reuse existing hosted accounts and D1.
- Authorized owner administration inspection confirms Sheldon already has DEV and
  a saved pilot; supplied password matches the stored verifier. No account reset,
  database mutation or backend deployment needed. Secrets stayed in memory only.
- NativeAccount default service plus one-time blank-URL preference migration;
  preserve custom URLs, other pilot settings and later explicit offline choice.
  Account URL field removed as requested. Configure and all existing account APIs,
  signed DEV/unlocks/flags, profile fields, DPAPI sessions and reload remain intact.
  No-service status is `Signed out / offline`; Connection report unchanged.
- Added real JsonUtility Editor gates for migration, all profile fields across
  save/reload/lobby payloads, grant/flag data and offline account calls. Local Python
  suite 44 passed / one Windows-only skip; diff checks passed. Hosted native CI and
  real Unity build pending; no local Unity/.NET. Windows login not run: this runtime
  received Cloudflare 403/1010 on an unauthenticated service read, with no workaround
  or credential submission. Database inspection/password verification is not login.
- Implemented on task branch; publication pending exact-head gates. Current release
  remains UBA #7 / 3686f40e / 27a7833. Detailed menu/API handoff in
  UNITY-LOGIC-HANDOFF.md. No gameplay, timing, hitbox, movement or animation changes.

## 2026-10-09 — Astra — UBA #6 replica projectile tag repair
- Task `codex/unity-shot-tag-repair`, base cloud `8ebe73214dc16b7010542ca47b9da5f94af21453`. Uploaded UBA #6 confirms that exact source, successful C# compilation and `NATIVE_COLOR_SPACE: Linear`. PreExport then stops at NativeNetworkBuildChecks:44, "Late reflected hadouken took the current owner's appearance". No shader error is reported; remaining Editor gates, final player shader compilation and delivery are not accepted.
- The preceding payload assertions passed (Hadoken, original owner, Arcade Red mods, burn and poison), and the tracker is fresh. The appearance builder's Siren-tag early return is therefore the failure path: an absent optional string arrives as empty through JsonUtility and the non-null check misclassifies the ordinary shot. Normalize empty SirenKind/LeetKind to null at ApplyFrame; keep nonempty values. This corrects guest/replay projectile identity without changing authority combat, timing, hitboxes, damage, cooldowns or movement.
- Extended the real JsonUtility/ApplyFrame Editor gate with explicit empty tags, a false-Siren assertion, actual appearance values on failure, and nonempty special-tag preservation/routing. Existing reflected-shot assertions remain. This Editor regression requires Unity and has not run locally.
- Local Python suite: 45 collected, 44 pass / one Windows-only skip; diff whitespace check passes. Hosted native source/helper checks pending; these do not execute the Unity Editor gate. Next: after native CI, advance the same cloud target branch and inspect a real UBA retry. No player package or feed publication from #6; current published native player remains #4.
- Claude handoff: fix is in replication, not a visual redesign. Shader review remains pending the cloud gate completing. API authentication is verified; first automated delivery/publication still awaits a passing game build.

## 2026-10-09 — Astra — UBA #5 compiler repair and service-account access
- Task branch `codex/unity-cloud-compile`, base/current cloud source `c50993ca3d069dbea3b646d6dbc31199fa83af20`. Owner-uploaded UBA #5 log confirms the intended branch, exact commit and Unity 6000.3.24f1; compilation stops on two CS0103 errors in `NativeGameplayValidation.cs` lines 98–99.
- Corrected Astra's Siren contact validation calls from assignment expressions `effect=...` to C# named arguments `effect:...`. Both declarations contain the named parameter; the blast checks and all gameplay rules remain intact. No animation, hit timing, hitbox, damage, cooldown, movement, networking or presentation changes.
- Local checks: 45 Python tests collected, 44 passed / one Windows-only skip; source packaging validates 806 files; diff whitespace check passes. Local Unity/.NET are unavailable. Hosted native CI and a fresh real UBA build are still required; the earlier source checks did not compile this Unity-only validation file. UBA #5 reports no shader errors, but its C# failure prevents acceptance of the shaders/Editor gates and delivery.
- Publication setup: automation-only PR #70 is merged on main (`4babc69`). Unity service account now has project-scoped Automation User, the role explicitly required by Unity's V2 migration guide. GitHub check run 37949375457 attempt 2 successfully reads the Unity API and passes exact-commit native CI, then waits for a successful eligible build. Feed-token write access, first automated publication and the enable variable remain unverified. Last published player remains UBA #4; build #5 was not published.
- Next: after native CI passes, fast-forward the existing cloud branch to this repair and let its owner-enabled Auto-build run; inspect the new log, all package gates and validated delivery before test publication. Keep the target on `codex/unity-cloud-current`. Claude can pick up this repair with the next shared integration. No raw logs, keys or delivery binaries committed.

## 2026-10-09 — Astra — Siren minion hits, visual data contracts and Linear cloud build
- Task branch `codex/unity-siren-data`, draft PR #69, implementation `694f5bd`, base shared `5b97d9a` (PR #62). That shared merge already contains requested `1969af8` and cloud `8f7cb32`; Astra's pool-equality / seeded-reset light check and both DEV_LOG histories remain intact. `codex/unity-cloud-current` is the same candidate for the next UBA build.
- Root cause of unhittable Siren minions: hiding the authority boss sets its seat disconnected; all minion hit/target tests reused that owner's eligibility. PvE adds now remain hostile to connected party/recruit pilots while the hidden boss stays untargetable. Applied to projectile sweeps, melee, abilities, fields, summons and bots. Entry exposure, vent travel and cinematic protection remain in HitPveAdd. Added pure rules checks and a real Unity projectile/contact regression gate.
- Required data: lobby pose in seats/lobby/match frames (sanitized; bots default weapon); original bullet flags/chip stacks, Hadoken and sword-beam mods; melee burn/poison; ability fuse; zone Id/Born and attachments, with a fresh identity at meteor-to-fire; chip field Effect; guest/replay DoT/slow/mark state; PvE revived count; directed add hit Normal; fighter LastAttack/JuggleUntil. Frame validation bounds the added payloads. New Editor round-trip checks cover late reflected shots, statuses and field/fuse identity.
- Events now carry ability-fx/chip-source explosion tags, damage kinds, post-damage hazard kind, Break material/SurfaceId and blink-strike teleport detail. Arc is emitted only for ball-lightning and leet-lock with its source/type; cosmetic teleport/control/hop/summon beams remain draw effects. Exact saw/arc types are consumed without changing audio mix.
- Rooms/settings: host RequestTeam for any lobby seat; CA- online-code display/input over existing raw native relay IDs; clear imported room state on join/close; peer metrics identify seats/names; synchronize actual fullscreen state so settings do not undo Alt+Enter. Modal hover checks cover lobby selects/folds. Corpse inherited velocity is capped at 24 m/s and detached subtrees get the source momentum kick in world coordinates.
- `PrototypeBuild.ConfigureRendering`, cloud export validation and ProjectSettings pin **Linear**; cloud receipt includes colourSpace. Added NativeBlast/NativePortrait/NativePortraitResolve to shader validation alongside the field/kill/status/beacon shaders. ACES exposure/tone mapping is not part of this batch.
- Local checks: Python 45 tests passed (one Windows-only skip); source package validates 806 files; git diff whitespace check passed. No local .NET or Unity Editor installed. Native CI [37940400473](https://github.com/sheldon37/Chip-Arena/actions/runs/37940400473) passed both jobs at `694f5bd`: managed parity and real relay, source packaging, 51 updater checks on each OS, Windows publisher/delivery suites and real launcher process/rollback checks. No failed native checks. New Unity Editor gates, shader compilation, GPU visuals, real clients and Alt+Enter are NOT RUN. Source checks cannot substitute for UBA.
- No new build or Update publication yet. UBA target `chip-arena` uses Unity 6000.3.24f1, Windows x64 Mono, project `unity/ChipArenaConversion`, post script `tools/cloud_post_build.sh`; use the existing free Windows Micro target. This session has no callable UBA trigger/artifact API. Next: build the cloud branch, inspect shader errors/failed checks and all delivery markers, validate the matching delivery ZIP, then publish under the standing test-channel instruction. Last published/downloadable player remains UBA #4 (`659a376e`).
- Still open: owner choice on retaining fields after their owner leaves (current policy preserved); optional gibPart/hitPoint and absorbed-contact/PvE events; C6b routing, C8 mix, C13 tools, C19 ACES and baseline Electron CI. C12/C16 stay with Claude. Gameplay timing, hitboxes, damage formulas, cooldowns and movement unchanged; hit eligibility and replicated presentation payloads changed.

## 2026-10-09 — Claude — Merge Astra's cloud build tooling into the shared Unity branch
- Owner asked to get Claude's 2026-10-09 batch onto the Update button. Merged `codex/unity-cloud-current` (`8f7cb32`, PR #68: Unity Build Automation hooks, cloud packaging, native publisher, the PR #67 build-check fixes) into `claude/sleepy-ritchie-g8ruhv`, so a cloud build of this branch now contains both. Conflicts: `NativeBuildChecks` presentation check (both sides counted the 7 combat + 3 blast lights; kept Astra's pool-equality version; today's work adds no Light/AudioSource components) and DEV_LOG (both kept).
- Checks on the merged tree: Unity runtime+editor compile OK; check_relay 134 PASS; Python 45 tests OK (1 Windows-only skip, includes the cloud/publisher suites); package_source OK (806 files); UpdaterTests 51 PASS.
- Not run: a real Unity/cloud build of this head (first real compile of today's shaders and Editor checks), publication. Next: the owner points the UBA `chip-arena` target at this branch (or Astra fast-forwards `codex/unity-cloud-current` to it) and runs one build; validate the delivery, then publish to the native test feed only with the owner's OK.

## 2026-10-09 — Claude — C18 ragdoll kill-effect visuals
- Branch `claude/wip-c18` (`9354e03`, review fixes `d20ef8a`, merges up to `ca59386`), merged into `claude/sleepy-ritchie-g8ruhv`. Presentation only; corpse physics and forces unchanged.
- `Core/NativeKillEffectRules.cs` and `NativeKillBoxes.Generated.cs` port client/ragdoll-vfx.ts, gibber-vfx.ts, body-trauma.ts and voxelBurst: the nine RAGDOLL_STYLES point swarms (16 additive points, 6 reduced), three rings and the Singularity core, the part collapse, Soul Drift's translucent ghost, Frostbreak's ice recolour, voxel-burst's 120 cubes (48 reduced), the Gibber's 150-drop fountain (55 reduced), woundCap stump caps, crush fragments and loose hat, blood sprays and splats, under the source's 280-piece budget. `Runtime/NativeKillEffectPresentation.cs`/`NativeKillModels.cs` draw them with three new Resources shaders (NativeKillPoints: camera-facing quads sized like three's attenuated points; NativeKillBody; NativeKillGhost), replacing the generic cube clouds and 3x3 caps.
- Review: 9 findings (2 medium: the dropped weapon ignored the style forces, collapse and sink; kill effects stepped at 20 Hz on guests and in the killcam), all fixed; the weapon still lies apart from the hand, and corpse physics (and the swarm centre that follows the pelvis) stays on arena.Time.
- Checks on the merged tree: Unity runtime+editor compile OK; check_relay 134 PASS (kill effects: 45,706 checks against the pinned source; fixture regenerates byte-identical); 24 Python tests OK; package_source OK (796 files). fxref source/port renders of 15 cases x full/reduced x 6 times: mean pixel difference 0.00-0.05.
- Not run: Unity play mode, the three shaders on a GPU, NativeBuildChecks, guests/killcam live, Windows build.
- Deviations: Unity's corpse detaches a severed or crushed part in every style, so caps and blood follow it in every style (the source only for impact-ragdoll and the Gibber); additive points blend in linear light and there is no tone mapping (C19); Frostbreak recolours only body/accent meshes with shade.
- Needs Astra (corpse physics): `NativeCorpse` gives every part `ClampMagnitude(death.velocity, 100)`; the source caps at 24 m/s and adds min(10, wound.momentum/part mass) to the detached subtree (`NativeKillEffectRules.DetachedVelocity` has the formula and a check). Optional: `gibPart`/`hitPoint` on `NativeDeath` so the Gibber cuts the source's part.

## 2026-10-09 — Claude — C14 live 3D pilot portraits and lobby poses (owner request)
- Owner, 2026-10-09: "I liked doing the pilots posing for their lobby icons, why did we get rid of that completely? (Still can improve it, I do like the animations you did but just want to show identities essentially)". Unity never had them; the 2D emblem was a documented stand-in.
- Branch `claude/wip-c14` (`93c5e19`, review fixes `208897b`, base merge `55e8bf7`), merged into `claude/sleepy-ritchie-g8ruhv`. Menus/presentation only.
- `Core/NativeLobbyPoses.cs` ports lobby-poses.ts and the pose helpers (weapon, chair, sleep, crossed) with breath/sway idle; `Core/NativePortraitRules.cs` the menu-portraits.ts framing (hat-top aware) and texture sizing. Checked against `tools/export_lobby_poses.mjs` fixtures: 270 poses, worst joint 0.0003 mm, 120 framings.
- `Runtime/NativePortraits.cs` renders each visible portrait once per frame with one disabled orthographic camera on layer 30 (removed from the match camera mask) into pooled 4x MSAA RenderTextures at physical size (cap 1280), its own lights only, then `Resources/NativePortraitResolve.shader` resolves to straight alpha with edge dilation; `Resources/NativePortrait.shader` draws the pilot. Shows the pilot's own model, colours, hat or animated Auditor halo, weapon in hand, podium, chair and sleep marks.
- `Runtime/NativePortraitUI.cs` places them in the pilot card (Customize LIVE PREVIEW, Play, Boss Test, Create, Join), the lobby cards (host, guests, bots, PvE party) and the PvE/Boss results cards; the card entrance, accent glow and ticked ring stay as the frame (ticks now turn smoothly), and the emblem remains the fallback. Customize gains the IDLE ANIMATION picker bound to `prefs.lobbyPose` (saved and sent like the other cosmetics; no new fields). Hooks: one line each in NativeHomeMenu.PilotCard, NativeLobbyUI.LobbyCard, NativeProfileUI.CustomizeBody.
- Review: 5 findings (1 medium: the pilot card dropped its INTRO row at 1080p/4K), all fixed.
- Checks on the merged tree: Unity runtime+editor compile OK; check_relay 133 PASS; 24 Python tests OK; package_source OK (779 files). fxref source/port renders of all four poses over 14 weapons, hats and the halo: mean pixel difference 0.5-1.6. Offline menu renders (portrait stubbed) at 1280x720 (scale .8), 1920x1080, 2560x1440, 3440x1440 and 4K.
- Not run: Unity Editor/Play mode, the two portrait shaders' compile and look on a GPU, real IMGUI, a lobby with real guests, Windows build.
- Deviations: the Emperor/Ansatsuken standing pose uses their baked idle without the source's gait ground pass (legs differ up to ~1.2 rad at the knee); the hat top is re-measured per pose; on short cards the Headwear/Kill effect/Intro rows give way to the portrait.
- Needs Astra: other pilots' lobby pose is not replicated, so their portraits use the 'weapon' pose. Add `lobbyPose` to `NativeSeat` (Core/NativeMatch.cs) and `NativeSeatFrame` (Runtime/NativeNetworkFrame.cs), set it on join from `profile.cosmetics.lobbyPose` (NativeNetwork.cs, next to hat/bodyColor; host seat from prefs; bots 'weapon'), and copy it in `LobbySeat` (NativeLobbyLogic.cs) and the match frames. `NativePortraitUI.CardLook` then reads it (one line).

## 2026-10-09 — Claude — Session UI on the PR #66 contracts (C20 connection report, end-of-match flow, fullscreen, reload pilot)
- Branch `claude/wip-ui-session` (build `8a0704a`, review fixes `9fe11b1`, base merges up to `3e577b3`), merged into `claude/sleepy-ritchie-g8ruhv`. Menus only.
- New `Runtime/NativeConnectionReport.cs`: the source's Connection report dialog with Copy (from Settings, the pause menu and the lobby's Advanced Options), built from `ConnectionMetrics`/`ConnectionReportJson` (guest) and `PeerMetrics` (host), no tokens; a guest's last sample is kept after a host loss. The lobby connection pill appends ' · N MS'.
- Match end and PvE results: rematch/retry votes with counts (`VoteRematch`, `RematchVotes`, `RematchRequired`), the cancellable three-second return with the source's #lobby-return banner (`RequestLobbyReturn`, `CancelLobbyReturn`, `LobbyReturnSeconds`), guest waiting states, `HostLost` (keeps the resume token so the seat can be reclaimed), Return to Main Menu (`ReturnMainMenu`). Settings: Full screen toggle (reads the real window mode, writes `SetFullscreen`). Account: Reload saved pilot (`ReloadSavedPilot`).
- Review: 12 findings (3 medium: pause menu ran off screen at Interface scale 1.2-1.5; host-lost match end discarded the resume token), all fixed. The pause menu now shrinks to fit; the modal blocks hover/clicks underneath (lobby SelectFields and fold rows still show hover under the dialog; clicks are blocked — small follow-up in NativeLobbyUI/NativeRoomOptionsUI).
- Checks on the merged tree: Unity runtime+editor compile OK; check_relay 132 PASS; 24 Python tests OK; package_source OK (764 files). Offline renders of Settings, Account, Pause (host, guest, offline, sandbox, lost; 1280x720 at scale, 1080p), the report over Settings/Pause/Lobby, match end and PvE results (votes, countdown, host lost) compared with the source. The banner covering the top notice matches the source's CSS (z-index 1600 over .notice at 76 px).
- Not run: Unity play mode, a real Alt+Enter, a live two-client host loss and reclaim, Windows build.
- Needs Astra: a seat id/name on each `PeerMetrics` entry so the host's report labels guests; keep `prefs.fullscreen` in sync with Alt+Enter/OS changes (ApplySettings re-applies the saved value); optional host `server` block (loop p95, tick, buffered bytes, dropped snapshots) in the report.

## 2026-10-09 — Claude — Event feedback for the PR #66 event kinds (C22 shake, combat numbers, event sounds)
- Branch `claude/wip-ui-events` (build `0132d04`, review fixes `08f1114`, base merge `44224b9`), merged into `claude/sleepy-ritchie-g8ruhv`. Presentation and audio only.
- New `Core/NativeEventFeedback.cs` (pure, checked against `tools/export_event_feedback.mjs` fixtures from the pinned source): `NativeShakeRules` (event-vfx.ts/renderer.ts: .12 shake on Hit/Headshot over .05, Impact, Break, Block, Collapse aimed at the view pilot, never Burn/PoisonTick, ignored after .4 s, random world-X jitter, frame-capped decay); `NativeCombatNumbers` (CombatNumberTrack: heals, burn/poison ticks, .16 s merge window, 3 lanes, 1.15 s life, JS rounding, U+2212 minus, PvE hits on adds); `NativeSoundLimiter` and `NativeEventSounds` (ArenaAudio.update routing for every EventKind, per-play rate, falloff, pan); `NativeSpikeRises` (stage-hazards.ts spike-rise cue).
- Wiring: ConversionGame shake region (the killing hit now shakes the death cam; decay runs while paused or spectating, but the camera only moves in play), NativeCombatHud numbers, NativeAudio event routing (add deaths silent like the source, DEV kill songs at rate 1, listener at the local pilot, Slender heartbeat voices reused without LINQ), PvP draft installs routed from Match.Chosen. No consumer treats an unknown EventKind as a hit.
- Review: 8 findings (1 medium: add deaths played the pilot death cue), all fixed.
- Checks on the merged tree: Unity runtime+editor compile OK; check_relay 132 PASS (75 sound scenarios, 25 spike rises tick by tick); 24 Python tests OK; package_source OK (762 files).
- Not run / not heard: Unity play mode, the mix by ear, shake feel, guests, PvE, Windows build.
- Deviations: no shake in the killcam replay or under the pause menu; burn/poison/proc ticks show no dot hitmarker (NativeFightHud); every blast plays the default explosion (see Astra items); clown laugh and the scheduled Clown Love/Bass Van songs remain C8.
- Needs Astra: Explosion Detail = the source explosion tag (ability fx or chip kind) at NativeChips.cs ChipBlast / NativeAbilities.cs AbilityBlast (also for C6b blast styles); Arc events with Source and a Detail naming the source event type (teleport, leet-lock, leet-control, slender hop, minionShot) or Arc only where the source emits one; Teleport Detail 'blink-strike'; Hit/Headshot Detail = damage kind (direct, secondary, reflected, ability, blast, chain, field, thorns, debris, stage-*); HazardHit Detail = hazard kind (and emitted after damage resolves); Break SurfaceId/material; optional Impact for absorbed standard-melee contacts and PvE pve-grab/pve-found events.

## 2026-10-09 — Claude — C10 projectile and ability-shot models (merged into PR #62's branch)
- Branch `claude/wip-c10` (port, base merge `7dcbfff`, review fixes `34726b9`), merged into `claude/sleepy-ritchie-g8ruhv` after C9. Presentation only.
- `Core/NativeShotScene.cs`, `NativeShotVisuals.cs`, `NativeShotGeometry.cs`, `NativeBurstPool.cs` and `NativeShotTracker.cs` port the source projectile path (renderer.ts shot loop and abilityShot, chip-effects.ts, burst-pool.ts, event-vfx.ts, projectile-trails.ts, projectile-glow.ts): per-spec bullet meshes, the seven ability-shot models with frag spin and fuse blink, element tint and puffs, fire/water sparks, lightning/darkness streaks, chip modules on projectiles, and trail/halo colour, width and light rules. `Runtime/NativeShotPresentation.cs` draws them as batched dynamic meshes (unlit NativeBlast shader); `NativeBurstPool` is shared for the later event-burst routing (C6b).
- Review fixes: a shot keeps its first-seen look (a reflected hadouken stays red); guests draw shots at the snapshot render clock, mixed between frames like client/snapshots.ts, with at most 80 ms velocity carry past the newest frame; colours follow `QualitySettings.activeColorSpace` (linear vs gamma); the tidal wave sorts at its own position.
- Merge: C9 and C10 share one `ArenaPresentation.FieldClock` (identical bodies); `RenderGlow` collects fields, renders blasts, telegraphs, then shots and the glow.
- Checks on the merged tree: Unity runtime+editor compile OK; check_relay 125 PASS (ShotChecks: 31 source runs/217 frames; tracker: 487 guest renders, 1,120 in-buffer positions match the pinned SnapshotBuffer to 1e-9); 24 Python tests OK; package_source OK (757 files).
- Not run: Unity import, GPU rendering in the build's colour space (the project does not pin one; Astra/owner), the Editor identity check, a joined match with reflected shots, frame-time cost.
- Needs Astra: ability-shot fuse in the `ability` effect frame (frag blink on guests/replays); per-bullet visual flags/chip stacks in `Snapshot.Shots`; Burn/Poison in the `melee` effect frame; optionally `Mods['sword-beam']` and Hadoken mods for data parity; pin the project colour space in build tooling.
- No simulation, damage, timing, hitbox, movement or protocol change.

## 2026-10-09 — Claude — C9 field telegraphs and ground effects (merged into PR #62's branch)
- Branch `claude/wip-c9` (work `6b594d0`, base merge `dd83c64`, review fixes `b61fcaa`), merged into `claude/sleepy-ritchie-g8ruhv`. Presentation only.
- `Core/NativeFieldRules.cs` ports client/chaos-visuals.ts: edgeless ground glow (alpha 0 at the gameplay radius), black-hole/bomb/mine/implosion cores, time/repulsion domes, the ally/enemy ability tint behind `NativeFieldRules.TintAbilityFields = true` (owner decision: false matches the shipped Electron look), Siren floor telegraphs (scorch, eruption warning ring and fill) and the ability aim preview (frag, meteor, geyser, singularity). `Runtime/NativeFieldPresentation.cs` draws them with the new `Resources/NativeFieldGlow.shader`; the C6 field emitters share its keys and birth times. Unity-only rings, cubes, storm beam and zone halos are removed (C7 status rings kept).
- Review fixes: Siren warning ring now draws over its fill; guests animate on the continuous replica clock (`ArenaPresentation.FieldClock`) instead of 20 Hz steps; exact zone births for Singularity, meteor fire and Orbital Mark (no second arrival burst after a killcam); aim preview only when E is ready/affordable or casting, read from the replicated pose; the eruption stand-in column starts at the burst.
- Checks on the merged tree: Unity runtime+editor compile OK; check_relay 123 PASS (new FieldChecks: 30 source runs, 324 frames, 120 tint rows, 54 Siren frames, 269 preview motes, zone-birth and preview-gate cases; fixture regenerates byte-identical); 24 Python tests OK; package_source OK. fxref source/port renders: mean pixel difference 0-0.06 except the domes (0.6/1.6, no ACES tone mapping yet, C19).
- Not run: Unity import, GPU rendering in the build's colour space, a hosted or joined match.
- Needs Astra: `Id` and `Born` on `NativeAbilityZone` (set in NativeAbilities.cs/NativeAbilityMinions.cs, sent in the `zone` effect frame), a new Id/Born when meteor strike turns into burning ground, `NativeChipField.Effect` in the `chip` effect frame (guests now recover it from the 1 s Field event), and whether replicas should keep drawing fields whose owner left (the source does).
- No simulation, damage, timing, hitbox, movement or protocol change.

## 2026-10-09 — Claude — Room UI on the PR #66 contracts (C1 join/invite, C3 options, C2 E12 team pickers)
- Branch `claude/wip-ui-room` (build `dc17893`, review fixes `bafaa28`, base merge `517eeb2`), merged into `claude/sleepy-ritchie-g8ruhv` (PR #62). Menus only.
- New `Runtime/NativeRoomOptionsUI.cs` holds the source's option rows (SelectField selects, checkboxes, notes, sections, the draft fold). Play vs Bots, Create Room and the lobby's essentials box and Advanced Options follow main.ts matchOptions/roomAdvanced/botDraftOption/botLoadouts: BOT DIFFICULTY, Random arena with its description, Randomize map after each set, rounds/sets, Skip player intros, per-bot weapon and team, the teams setup with Start blocked when everyone is on one team.
- Host changes go through `RoomOptions`/`SetRoomOptions`, your own team through `ChangeTeam`; guests see the host's rules read-only. Bot cards come only from `LobbySeats`.
- Join takes a code or a pasted invite link (`JoinInvite`, then `JoinRoom`). The lobby invite card shows the code, the LAN line (`LanAddresses` + port, refreshed every 5 s), Copy Code and Copy Invite Link (`InviteUrl`); Advanced Options ends with the INVITE LINK field.
- Two-lens review: 12 findings (2 medium: a pasted link broke the Join form; Play/Create/Boss threw while a guest join was pending), all fixed.
- Checks on the merged integration tree: Unity runtime+editor compile OK; check_relay 122 PASS; 24 Python tests OK; package_source OK (729 files). Offline renders of Play, Create, Join, host/guest/teams/PvE lobbies at 1280x720 (UI 1.5), 1920x1080, 2560x1440 and 3440x1440 compared with the source renders.
- Not run: Unity import/play mode, real IMGUI input (clicks, text focus, the team list taking clicks and the wheel), Windows build, a LAN invite round trip.
- Needs Astra: a host-side per-seat team call (e.g. `RequestTeam(seatId, team)`; `ChangeTeam` moves only the caller, so the host cannot set a guest's team as the source can); clear `receivedOptions`/`lobbySeats` (and the copied `prefs.pve`) in `CloseNetwork`/`JoinRoom` (the UI ignores stale ones for now); online CA- directory codes (A8).
- No simulation, rules, protocol, save or account change.
## 2026-10-08 23:14 EDT — Codex/Astra — owner reports cloud-built update worked

- Owner reports “looks like it worked” after being asked to update the existing native installation to cloud build #4. Record this as the first **owner-reported successful cloud-to-installed Update**: compiled source `51841828e142c551b521ca6698d7487390cd4880`, version `659a376e`, revision `639271111007548341`, published channel `1d4b4f62762698f0c3e9f01d114dcbc29574f1df`. Actual cloud build/delivery and public-download checks are recorded below.
- This completes the initial cloud-build -> native test feed -> installed Update workflow on the owner's report. It is not independent installed-identity/settings-retention, full Windows gameplay, multiplayer or rollback verification. Future ordinary releases can reuse the same installation/feed without a local-PC Unity build; triggering, artifact retrieval and publication remain separate steps, not automatic.
- Handoff branch `codex/unity-cloud-current`, PR #68, previous head `89f5090`. Updated project/cloud/native/A13 notes. Documentation-only diff check passes; no rebuild, code tests, new feed publication, merge, gameplay/visual changes or Site action in this follow-up.

## 2026-10-08 23:07 EDT — Codex/Astra — cloud build #4 published to native test feed and public download verified

- Owner explicitly approved publication at 23:02 EDT. Published verified UBA `chip-arena` build **4**, source **`51841828e142c551b521ca6698d7487390cd4880`**, version **`659a376e5185f5ec9282d698eeda3622661a14fc70ad1eb79d8dac09142e0651`**, revision **`639271111007548341`**. Channel commit **`1d4b4f62762698f0c3e9f01d114dcbc29574f1df`**, parent `c5251fbb0113bf9544a37989318336dae0df1840`. Feed: https://raw.githubusercontent.com/sheldon37/chip-arena-test-updates/main/native/windows-x64/latest.json .
- Uploaded 12 <=8 MiB parts via authenticated GitHub Git-data operations, checking each returned Git blob hash against local bytes. Created the tree from the current channel parent, verified all previous native objects and every unrelated root entry are preserved, then moved main with an expected-parent check and force=false. Existing Electron `feed` subtree remains `59d8f200fdc4ee22014a92561031cbfd1b2d7d72`; README/PUBLISHED are unchanged. No source PR merge, Claude-branch push, Site/live-feed publication or credentials committed.
- Full payload remains **94,040,253 bytes**, **229 files**, **191,590,466 unpacked bytes**, SHA-256 **`e369e8870c77eaf744cb8980e87b28d7ba4ad900caaf8ea6122e98c274a55c6a`**. Resplitting the original three cloud parts into 12 changes only part boundaries/metadata, not the archive, game or build identity.
- Actual public verification at 23:07 EDT: fetched the normal main-branch raw manifest with no-cache headers and every published part, verified each size/SHA-256, then ran the existing publisher validator over the downloaded feed. Full archive SHA-256, ZIP CRCs, safe unique inventory and embedded identity pass. Public manifest exactly matches the approved staged candidate; GitHub ref confirms channel `1d4b4f6`. The build is now available to older native installations through Update.
- Source handoff branch `codex/unity-cloud-current`, PR #68, previous documentation head `99a5344`. Updated cloud/native handoffs, PROJECT_STATE and A12/A13 with this release receipt; documentation-only diff check passes, no redundant code suites rerun. Build #4's 86 Unity checks/complete delivery and implementation `82ddcfb`'s native CI `37874907141` remain the actual build/test evidence.
- Owner next: launch the existing native installation, choose Update, let it install/restart and confirm the new game opens. This particular old-to-cloud upgrade, retained settings and Windows gameplay remain unrun here; the earlier owner's successful upgrade concerned previous `83e6a566`. No new starter install or owner-PC Unity build is required for this ordinary update. Future candidates can use the proven cloud build -> validated native feed -> installed Update path; dashboard triggering/artifact retrieval and publication are still separate actions, not an automatic release pipeline. No gameplay/visual/animation timing/hitbox/damage/cooldown/movement/network changes in this publication task.

## 2026-10-08 22:57 EDT — Codex/Astra — first complete cloud delivery verified, ready for owner publication decision

- Branch `codex/unity-cloud-current`, PR #68; inspected remote and build #4 source `51841828e142c551b521ca6698d7487390cd4880`, shared base `2604b2a`, path repair `82ddcfb`. UBA target `chip-arena` / build 4 / Unity 6000.3.24f1 / Windows x64 Mono passes 86 checks, both export hooks, all four package markers and exit 0. Runtime diagnostic confirms Cygwin Python and the correct project root; no additional code fix was needed.
- Uploaded ZIP now contains the complete delivery and sidecar. Actual verification: outer/inner ZIP CRCs, sidecar SHA-256, exact source/target/build/editor receipt, seed/update/log hashes, native manifest part and full archive checksums, safe unique inventory and embedded identity. The complete starter player matches the feed byte-for-byte; all 228 raw exported player files also match after EXE/Data renaming, plus the verified added identity. Launcher is present (89,088 bytes). This verifies actual cloud delivery, not Windows gameplay.
- Candidate version `659a376e5185f5ec9282d698eeda3622661a14fc70ad1eb79d8dac09142e0651`, revision `639271111007548341`. Payload 94,040,253 bytes / 229 files / 191,590,466 unpacked bytes, SHA-256 `e369e8870c77eaf744cb8980e87b28d7ba4ad900caaf8ea6122e98c274a55c6a`. Delivery SHA-256 `c4220e78629b9f544fb210d3aa8ca93ddbfff554bcfdd7c685c72be0914e46a6`; starter `4d1180d5cc489c120fa5e18cb959e9eb481999090b3ad9b98637fe1bc803a1c1`; embedded Editor log `87ff061ea0206d557d43a10be8f1e1d40e071c0b9793e5955a6781c10a97d3c4`. Full upload log `28d2c5090f2cfed4b8d15f1e7f5a5808daa67e8077d2f0c4c253db062cd3b8fe`, outer ZIP `8e32eef1f99ae32296dc77b7cb0e1f558f06c34a4616378fdf36df1e53c8353f`.
- Prepared a local publication copy with 12 parts <=8 MiB for the GitHub connector limit, and revalidated the unchanged payload hash/identity. Original delivery remains intact. Read public manifest and channel ref: still `83e6a566`, revision `639271006350008324`, channel `c5251fbb0113bf9544a37989318336dae0df1840`. The new version/revision is eligible for installed clients after publication. No channel write performed.
- Claude: cloud/native handoffs, PROJECT_STATE and A12/A13 now record packaging acceptance PASS. No rebuild/settings change needed for this candidate. Next is owner A13 approval for its native test-feed publication, followed by Windows startup/gameplay and actual old-to-cloud update checks. Older feed's successful owner-reported upgrade and CI launcher tests do not prove this particular game build's acceptance. Automatic triggering/publication still unconfigured; owner dashboard supplied artifacts.
- Documentation-only receipt update, diff check passes; no redundant source suites rerun. Existing CI `37874907141` passes on implementation `82ddcfb` as recorded below. No gameplay/visual/animation timing/hitbox/damage/cooldown/movement/network changes, raw logs/binaries/secrets committed, merge, new feed/Site publication or paid upgrade.

## 2026-10-08 22:25 EDT — Codex/Astra — build #3 receipt lookup failure and packaging repair

- Branch `codex/unity-cloud-current`, PR #68, parent/build #3 source `c2baa210251f441c4f1d8d97e7da04dc0ed54977`, shared base `2604b2a`. Uploaded log confirms 86 PASS lines, both export hooks, three Editor packaging markers and exit 0. It now executes `tools/cloud_post_build.sh`, verifying the dashboard correction.
- Python then fails looking for `BUILD_PATH/Builds/cloud-build-receipt.json` rather than the Unity project output. UBA nevertheless reports SUCCESS; ZIP is only 228 raw-player files / 191,590,212 unpacked bytes (CRC pass). No delivery marker/launcher/starter/feed/receipt. Log SHA-256 `1b814d846477261078ddaba28d304affc654078683d2773462bf29ceeed4d9f7`; ZIP `180d3c9d4b50788ac00777781d94ed10f6b3ec22ac6d1508db548d223626a052`.
- Fixed wrapper to select native Windows vs POSIX paths for the actual Python interpreter, explicitly pass the Unity root, and propagate conversion/Python failures. Python no longer infers its project from `__file__`, converts Unity's native Editor-log path under Cygwin, and logs runtime/root for diagnosis. The incorrect lookup is proven; the Cygwin interpreter explanation is inferred because build #3 did not log its Python platform. Feed validation and no-publication behavior are unchanged. No gameplay, visuals, animation timing, hitboxes, damage, cooldowns, movement or network changes.
- Regression-before-fix: three new tests fail (explicit-project CLI, native Windows log path under emulated Cygwin, real shell entrypoint under POSIX Python). After repair, scoped publisher/delivery unittest suite collects 27: 26 pass, one Windows command skip. Shell test produces a real fixture delivery from an unrelated working directory with spaces and checks missing-receipt nonzero exit/no delivery. Bash syntax/diff checks pass. These do not claim a real Cygwin/Unity run. Local scoped checkout lacks full source and dotnet/Unity. GitHub native CI [37874907141](https://github.com/sheldon37/Chip-Arena/actions/runs/37874907141) passes on repair `82ddcfbb03b774805fb088d505ed1e7e7d2e326e`: Linux check_relay.py, full Python suite (45 collected, 44 pass / one Windows-only skip), package_source.py (737 files), cloud staging and 51 UpdaterTests; Windows all 16 publisher tests, cloud delivery 11 collected / 10 pass / one POSIX-shell-fixture skip, staging/51 updater checks, shipping launcher compilation and real install/restart/acknowledgement, startup rollback and interrupted-swap recovery. This verifies source/helpers, not the repaired script on UBA or delivery collection.
- Claude: updated cloud/native handoffs, PROJECT_STATE and A12/A13 with the failed delivery receipt. Keep dashboard `tools/cloud_post_build.sh` and both export methods. After CI, run the new branch head and require all four markers plus validated delivery/sidecar/receipt/starter/feed. Provider SUCCESS is insufficient. Windows playtest/update acceptance still open. Implemented on branch, not merged or released; native feed remains `83e6a566` at `c5251fb`. No raw log/binary/credential committed, no paid upgrade or Site change.

## 2026-10-08 21:41 EDT — Codex/Astra — first cloud build inspected; delivery path correction

- Branch `codex/unity-cloud-current`, PR #68, inspected remote/source head `8fd2f78d84695f4f6fc0fafe2967e0fda5b707da`, shared base `2604b2a`. Owner uploaded UBA build #2 log and ZIP; target ID is now verified as `chip-arena` for the recorded Chip Arena project.
- Actual cloud evidence: Unity 6000.3.24f1 / Windows x64; both configured export methods ran; 86 PASS lines, 15 maps / 120 spawn floors, all three Editor packaging markers, Unity exit 0 and provider SUCCESS. This validates cloud compilation/export and launcher packaging on the newer C11 candidate. It does not establish rendered gameplay or updater acceptance.
- Delivery blocker: log line 9803 reports the configured `unity/ChipArenaConversion/tools/cloud_post_build.sh` was not found and was skipped. No delivery marker. ZIP inventory is 228 raw-player files, 191,590,212 unpacked bytes, all CRCs pass; launcher/starter/feed/receipt/delivery are absent. SHA-256 log `4411960258badaaa37e1240fe4e97040ebd96a0cba0e76c5dde34dba7c507bc0`; ZIP `c50614c48fae8aaea3a7ccb12bf99522f03af0129a268a2ec41d1af8293a5729`.
- Corrected dashboard/handoff instruction to project-relative `tools/cloud_post_build.sh`. Local path lookup reproduces that the old path is absent under the configured project and the corrected one exists. The diagnosis is inferred from that layout and the provider warning; Unity's generic docs say repository-relative, so the next actual run must verify this target's behavior. No source-code repair or new automated regression test is claimed for this external configuration change.
- Updated cloud/native handoffs, PROJECT_STATE and A12/A13 status; documentation-only diff check passes. Existing source/helper CI remains `37866917576` on `8a99e96`; no code suites rerun. Full uploaded archive CRC/inventory inspection and targeted log review actually ran. No raw provider log, binary or credential is committed.
- Next: owner changes that one Post-build script field, saves and reruns the same branch; inspect all four markers and the delivery/sidecar/receipt/starter/feed before accepting it. Unity dashboard access is unavailable in this session. Actual post-build Python/path behavior, delivery collection and Windows gameplay/update acceptance remain open. No merge, feed/Site publication, paid upgrade or gameplay/presentation/network change; current native feed remains `83e6a566` at `c5251fb`.

## 2026-10-08 21:03 EDT — Codex/Astra — cloud target setup and automatic Mono clarification

- Branch `codex/unity-cloud-current`, PR #68, previous documentation head `60d2be8`, unchanged tested source `8a99e96`. Owner reports dashboard settings complete except a missing Scripting backend selector. The uploaded PDF verifies branch/subfolder/editor/Windows builder; advanced hooks, Micro tier and scheduling are owner-reported.
- Read `NativeCloudBuild.cs`: PreExport sets `NamedBuildTarget.Standalone` to `ScriptingImplementation.Mono2x` and enables development; PostExport rejects a backend mismatch. A manual Mono selector is unnecessary. Corrected the dashboard handoff so Claude/the owner do not search for an optional field.
- Documentation-only update; diff check passes. Existing CI receipt remains unchanged, no tests rerun. Next: first clean Windows cloud build, full log and delivery archive validation. No cloud build result, new release, merge, paid upgrade or Unity API access claimed.

## 2026-10-08 evening EDT — Codex/Astra — refresh cloud build candidate for owner setup

- Branch `codex/unity-cloud-current`, based on current shared Unity head `2604b2ae587b0af3a5378cd52bf10dba93143515`. Shared advanced from the owner's tested `5900583` to Claude's C11 weapon presentation while account setup was underway; all those changes are retained. No shared branch was changed or merged by this task.
- Reapplied PR #63 publisher and PR #64 cloud hooks/delivery tooling to this base. Carried all four PR #67 v3 startup/glow/headshot/rescue repairs (`9903a66`), preserving their regression checks and the owner's successful Windows-build/update receipts below. The older cloud branch is not the build candidate. No additional gameplay, presentation design, combat timing/hitbox/damage/cooldown/movement/network changes.
- Owner created the Chip Arena Unity Cloud project and is choosing Windows desktop 64-bit in Target setup. Observed organization `2474084689228`, project `06275870-8c41-42b2-b21a-f75ff362f3dc`; target ID still unknown. Free dashboard allocation includes 200 Windows Micro minutes monthly. Keep automatic builds off initially and use the included Micro builder; no paid upgrade/spend authorized. Source credential goes directly into Unity; no credential in this repository.
- Actual local checks: publisher/delivery unittest suite collected 24 cases, 23 pass and one Windows command test skips on Linux; Bash syntax and diff whitespace pass. This is a scoped source checkout with no dotnet/activated Unity Editor, so full check_relay.py, source unittest/package_source.py and UpdaterTests are delegated to the candidate's GitHub CI; results follow after push. Prior CI receipts below apply only to their recorded old heads.
- GitHub native CI [37866917576](https://github.com/sheldon37/Chip-Arena/actions/runs/37866917576) passes on implementation `8a99e962554887f758f4edc7132199aceb40f38d` (draft PR #68): Linux check_relay.py, full Python suite (42 collected, 41 pass, one Windows-only skip), package_source.py (737 files), cloud staging and 51 UpdaterTests; Windows all 16 publisher and eight cloud-delivery tests, staging/51 updater checks, shipping launcher compilation and real fixture-player install/restart/acknowledgement, rollback and interrupted-swap recovery. These are source/helper checks, not an actual Unity build. The follow-up commit records results in documentation only.
- Next: select this branch in the owner's UBA target, project subfolder `unity/ChipArenaConversion`, editor 6000.3.24f1, Windows x64 Mono/Development, the pre/post-export methods and post-build script in `UNITY-CLOUD-BUILD-HANDOFF.md`. Run one clean cloud build after source CI passes; inspect all log gates, full source SHA, receipt and delivered starter/feed hashes. Real UBA compilation/provider paths/artifact collection and Windows gameplay of the newer C11 source remain unverified. No working Unity API client is claimed.
- Native test feed remains the owner-approved `83e6a566` candidate at channel `c5251fb`; no new publication, source PR merge, Claude-branch push, force push or Site change. DEV_LOG, PROJECT_STATE, native/cloud handoffs and A12/A13 preserve the distinction between local-build evidence, owner-reported updater success, source CI and actual cloud acceptance.

## 2026-10-08 20:21 EDT — Codex/Astra — owner reports older installation updated successfully

- Owner reports: “Looks like my older build successfully updated.” This is the first owner-reported successful Unity-to-Unity update through the published native test feed, following channel commit `c5251fb` (version `83e6a566`, revision `639271006350008324`). Combined with the prior verified public download, native update delivery is working in this observed case. No new client log or installed identity was supplied; do not imply independent verification of settings retention, rollback or all acceptance cases.
- Future ordinary game releases can reuse this installation and feed: update source, produce a newer Windows Unity build, validate and publish the approved native candidate, then use Update in installed clients. Builds still run on the owner's Windows PC; cloud-build PR #64 remains open/unmerged and its Unity service setup is not active. Publisher PR #63 and build-repair PR #67 also remain open/unmerged. Future source checkouts must retain the v3 build repairs until integrated; publishing a compiled candidate does not merge source branches.
- Handoff branch `codex/unity-startup-build-check`, PR #67, previous docs `23ad201`; documentation only. Record the owner result in A13/project status and the publisher PR. No rebuild, new feed publication, source merge or gameplay/presentation change in this follow-up. Remaining follow-ups are installed-identity/retention/repeat-update/rollback checks and the broader multiplayer/service/resolution acceptance; they do not negate the reported successful update.

## 2026-10-08 20:17 EDT — Codex/Astra — native test feed published and download verified

- Owner reported the candidate looked good and explicitly authorized publication at 20:10 EDT. Published approved version **`83e6a566052ac3167eb076abb0803e71a32337226674fa9446710bdb533f39e4`**, revision **`639271006350008324`**, to `sheldon37/chip-arena-test-updates` main in channel commit **`c5251fbb0113bf9544a37989318336dae0df1840`** (parent `5ddab5b`). Public feed: https://raw.githubusercontent.com/sheldon37/chip-arena-test-updates/main/native/windows-x64/latest.json .
- Publication used authenticated GitHub Git-data operations with a checked expected parent and non-force ref update. Only `native/windows-x64` was added. Verified the existing Electron `feed` subtree remains `59d8f200fdc4ee22014a92561031cbfd1b2d7d72`, with root README/PUBLISHED blobs unchanged. No Site publication, source-PR merge or Claude-branch push.
- The connector rejected the original 32 MiB part request because its request-body limit is 16 MiB. Split the same archive into **12 parts of at most 8 MiB**; revalidated before publication. The assembled **94,017,590-byte / 228-file** player archive is byte-identical to the approved upload, SHA-256 **`5b4db8faeca1715bff41d660cdbf93b909effffb5f27abd351d1501ce33f3d72`**. Build identity, player files and source are unchanged; only delivery boundaries and per-part manifest metadata differ.
- Actual post-publication verification: the public manifest matches the staged manifest; downloaded **every part from the public main-branch URL**, verified all sizes/SHA-256, reassembled and ran the actual publisher validator. Full archive SHA-256, ZIP CRC, safe unique paths, inventory and embedded build identity all pass. This proves feed availability/integrity, not that the owner's Windows launcher has installed it.
- Source provenance remains the owner's reported shared `5900583` archive plus PR #67 v3 code `9903a66`. Successful Windows log has 86 PASS lines and both packaging markers. PR #67 / `codex/unity-startup-build-check` and publisher PR #63 remain open/unmerged; previous handoff `451ac05`. This follow-up changes documentation only. No animation, hit timing, hitbox, damage, cooldown, movement, rendering or network behavior changed.
- Next: owner opens the **older baseline installation** (version `a947a4c2`, revision `639270982812880966`) through its root launcher and uses Update. Confirm download/install/restart, installed `app/native-build.json` equals the published version/revision, settings are retained, Play vs Bots still works, and another update check reports current. Actual Unity-to-Unity install/restart, retention and rollback remain unverified here. A13 is now feed-published / end-to-end acceptance pending; broader multiplayer/service/resolution gates remain.

## 2026-10-08 20:04 EDT — Codex/Astra — newer native package validated, publication pending

- Continuing PR #67 / `codex/unity-startup-build-check`, code `9903a66`, previous documentation `7c948ac`, shared base `5900583`. Owner supplied `NativeUpdateFeed(1).zip` and `native-build(1).json` following successful `build(4).log`. Validated using PR #63's actual `publish_native_update.py` (`validate`, `validate_build_log`, and upgrade/publication rule comparison); no publication function was invoked.
- Candidate source version **`83e6a566052ac3167eb076abb0803e71a32337226674fa9446710bdb533f39e4`**, revision **`639271006350008324`** (2026-10-08 19:57:15 EDT), bootstrap 1 / native-test / windows-x64. All three part sizes/SHA-256 checks, assembled size/SHA-256, safe unique regular-file paths, embedded identity, CRC/inventory and separate uploaded identity match. **228 files, 94,017,590 payload bytes (89.66 MiB), 191,544,639 unpacked bytes**; zero unreferenced files in the outer feed ZIP.
- Payload SHA-256 `5b4db8faeca1715bff41d660cdbf93b909effffb5f27abd351d1501ce33f3d72`; uploaded feed ZIP SHA-256 `0afe7a6bc817a1109cf1ff5caa157dce79c6a2acbb786fa533005514e20e9755`. Both packaging markers in the previously recorded log pass the publisher's log/identity check. This confirms artifact consistency, not an independently reproduced source-to-Git fingerprint; source provenance remains owner-reported shared archive plus v3 patch.
- Upgrade eligibility **passes** against the earlier supplied baseline: `a947a4c267ff468891fe70cfd9dd28401e6bcbe87e411d655d4cdab782f99184`, revision `639270982812880966`. Candidate has a different version and a greater revision, matching NativeUpdateRules.IsNewer. This checks eligibility; it is not a real download/install/restart result.
- Read-only GitHub check still returns 404 for `native/windows-x64/latest.json`; PRs #63 and #67 remain open/unmerged. Next: owner confirms the new separately extracted starter launches and Play vs Bots works, and gives the explicitly required OK for **this native test candidate** before publication. Preserve the old install for the real update test; then check restart, installed identity, retained settings and repeat-update behavior. No feed/Site publication, merge or Claude-branch push. This handoff changes documentation only.

## 2026-10-08 19:58 EDT — Codex/Astra — Windows build and native packaging succeeded

- Owner's `build(4).log` confirms Unity **6000.3.24f1** completed Build-Windows.cmd: **86 PASS lines**, `Build Finished, Result: Success.`, both `NATIVE_UPDATE_FEED_READY` and `UNITY_TEST_BUILD_READY`, and final **exit 0**. Source provenance is the owner's reported fresh shared `5900583` archive plus cumulative v3 patch (PR #67 code `9903a66`); the log itself does not embed the Git SHA. Candidate manifest/build identity and payload hashes still need to be supplied and checked.
- The new four-case grounded/airborne headshot gate passes. Real JsonUtility/replay/replica, combat/follow-up, presentation startup/reset, glow admission/lifetimes, melee/E-special/ranged/magic checks, PvE economy/revival, all **15 maps / 120 spawn floors**, core/LAN checks and captured animation validation also pass in the owner's Editor run. No C# compiler error, shader error, exception or kinematic velocity warning was found in this log. These are build-gate results, not a rendered gameplay or online-session acceptance claim.
- Outputs are under the owner's `D:\Chip Arena New\unity\ChipArenaConversion\Builds`: `NativeUpdateFeed` and `Chip-Arena-Native-Conversion-TEST-Windows.zip`. Feed marker explicitly says **prepared only; no publication**. Log receipt: 273,429 bytes, SHA-256 `70251692ac31e806ba46022e76e431bd12d4e5d9300abf433a79135a7a36239c`.
- Branch `codex/unity-startup-build-check`, draft PR #67, base `5900583`; this follow-up changes documentation only. A12 now records successful Windows build/packaging with playtest acceptance still open. No new code or rebuild is requested for this documentation commit; v3 remains the current patch. No hit timing, animation, damage, hitbox, cooldown, movement, rendering, network or updater behavior changed.
- Next: owner uploads a ZIP of the **new** NativeUpdateFeed plus `Builds/Windows/app/native-build.json`; validate referenced parts, complete archive, embedded identity and upgrade eligibility against the earlier baseline. Extract the new starter ZIP separately and confirm launch plus Play vs Bots; retain the old install for the later update test. Host/join, account/service flows, rendered/resolution checks and an actual Unity-to-Unity update remain unrun. Publication/merge remains owner-gated; nothing merged, pushed to Claude's branch, or published to a feed/Site.

## 2026-10-08 19:50 EDT — Codex/Astra — Windows headshot gate and v2 verification

- PR #67 / `codex/unity-startup-build-check`, parent `7678323`, shared base `5900583`. Owner's `build(3).log` from Unity 6000.3.24f1 confirms the corrected 200-impact glow admission/lifetime gate **passes**, as do presentation startup/reset, real JsonUtility, combat/follow-up, melee contacts and E specials. The kinematic warning remains absent. Build exits 1 at NativeGameplayValidation's fatal-headshot assertion; neither packaging marker is present.
- Confirmed stale contract: that assertion requires `StunEnd > Time` after a fatal projectile, while A10 intentionally removed generic hit stun from Damage and ValidateParityCombat already rejects it. RecordDeath chooses grounded high-speed bullet headshot collapse independently of StunEnd. Do not restore gameplay stun to satisfy the old test. Replace the combined assertion with four grounded/airborne × no-stun/pre-existing-stun cases, separately checking death/alive state, collapse selection and the exact unchanged stun deadline. Diagnostic messages identify the case and actual value. Only the Editor-guarded test changes.
- Checks actually run: supplied Windows log inspection; reviewed Damage, Wound, NativeBodyDamage, RecordDeath and the existing no-stun regression; diff whitespace; cumulative four-file ZIP CRC and exact byte comparison. The new four-case gate has **not run in Unity** here (no activated Editor or C# compiler). Remaining map/PvE/build/launcher gates, completed new player, rendered gameplay and actual update acceptance remain unverified.
- Claude/owner handoff: cumulative `Unity-Build-Startup-Fix-v3.zip` retains the first three patched files and adds `Runtime/NativeGameplayValidation.cs`. Close Unity, overlay its Assets folder onto the owner's fresh `unity/ChipArenaConversion`, replace four files and rerun Build-Windows.cmd. Keep the older starter as the updater baseline. No production combat, animation selection, hit timing, damage, hitboxes, cooldowns, motion, rendering, network or updater changes in this follow-up. No merge, Claude-branch push or native-feed/Site publication.

## 2026-10-08 19:39 EDT — Codex/Astra — next Windows gate: glow fixture event budget

- Follow-up on PR #67 / `codex/unity-startup-build-check`, initial code `7f8689e`, shared base `5900583`. Owner's next Unity 6000.3.24f1 log (`Pasted text(20261008-233923).txt`) confirms the new seeded presentation startup/reset check passes, the kinematic velocity warning is gone, and real JsonUtility plus combat/follow-up checks pass. Build then exits 1 in CheckGlow before either packaging marker. The initial patch is now exercised in real Unity; the newer glow correction below is not yet.
- Confirmed fixture mismatch: it adds 200 Impact events with one timestamp, but NativeEventBuffer.PerTickEffects accepts only 32 transient notifications at that timestamp. The renderer therefore cannot receive the 200 impacts the old assertion expects. Keep production budgets and VFX unchanged. Emit the fixture's 200 impacts across seven 60 Hz ticks, assert all 200 were admitted, then require all 200 simultaneously live. Verify trail expiry, all marks just before the oldest expires, 168 after only the first tick expires, and zero after the newest expires. Failure messages now report actual mark/trace/admission counts.
- Checks run: source budget/constructor inspection; float32 admission/lifetime schedule model reproduces the old 32-of-200 fixture and verifies the corrected 200 -> 200 -> 168 -> 0 schedule; diff whitespace; patch ZIP CRC and exact byte comparison to all three source files. No C# compiler or activated Editor here. The corrected real-Unity fixture still needs the owner's next Build-Windows.cmd invocation; remaining map/physics/build/launcher gates and actual update acceptance are not claimed passed.
- Handoff: same PR, cumulative three-file patch (only NativeBuildChecks changes in this follow-up; retain the previous placement fix/regression). No production simulation, event budget, rendering, animation, hitbox, damage, timing, network or updater behavior changed by this follow-up. No merge or publication. Rebuild the fresh source with this patch, inspect the next log, and retain the old starter as the update baseline.

## 2026-10-08 — Codex/Astra — Windows build log: presentation pool check and PvE placement warning

- Branch `codex/unity-startup-build-check`, based directly on `59005836032bd8ba5705d749c226ad61c406e69b` on `claude/sleepy-ritchie-g8ruhv` (PR #62). Owner's `build(2).log` from Unity 6000.3.24f1 exits 1 in NativeBuildChecks.CheckPresentation before player packaging; no new starter/feed produced. This entry supersedes the earlier claim that real JsonUtility/PhysX checks have never run: this supplied log records their PASS messages, including the network/replay serialization and combat/follow-up paths. It does not prove rendered gameplay or a completed current-head build.
- Confirmed failure: ArenaPresentation.Initialize(false) creates NativeCombatGlow's seven lights plus NativeBlastPresentation's three (NativeBlastSim.LightCapacity), but the older startup assertion counted all children and required exactly seven. Repair the build check to validate both pools independently, report actual counts, seed every light and particles before two resets, and reject active/leaked/replaced components. Preserve the exact 25 audio sources and one particle system checks. Claude's VFX rendering and pool capacities are unchanged.
- The same log warns that PlacePveBoss writes linearVelocity on a kinematic body. Clear logical motion for all bodies and only write Rigidbody velocity when dynamic, for both the boss and displaced party members. Extend the real-Unity follow-up check to cover kinematic/dynamic rescue, HP preservation, push distance, cleared motion, preserved physics mode and warning/error-free placement, restoring fixture modes afterward.
- Checks actually run here: source inventory reconciliation (7 + 3 explains the original failing assertion), review against the owner's exact Unity log, diff whitespace and patch archive byte comparison. No Unity Editor/license or C# compiler is available in this authoring workspace; the changed real-Unity checks still require the owner's next Build-Windows.cmd run. Do not claim this patch passed a Windows build yet.
- Earlier baseline evidence: the owner's older-source build succeeded and its uploaded feed validates (version `a947a4c267ff468891fe70cfd9dd28401e6bcbe87e411d655d4cdab782f99184`, 228 files, 3 referenced parts, 93,772,778 bytes). The older build is only an update baseline; its source commit is unknown. Current failed build is not publishable. Publisher PR #63 records that validation.
- Impact/handoff: test/build tooling plus physics-mode-safe boss placement only; no change to animation timing, hitboxes, damage, cooldowns, normal dynamic rescue movement, networking, UI/VFX design or update feed. No merge, Claude-branch push or publication. Next: apply this task branch's three changed C# files to the owner's fresh 5900583 source, rebuild, inspect the new log and both packaging markers; then validate/test the resulting new package before owner-authorized native publication.

## 2026-10-08 — Codex — Unity cloud build packaging setup
- Owner requested Unity Build Automation and a Claude handoff. Task branch `codex/unity-cloud-build`, based on PR #63 at `5331741da76c5ec317cf06006e79ffbd2558dcd0`; incorporates Claude's documentation-only correction `e36253af63824c0b2d8178c2ecd2a5858bac0134` without losing either handoff.
- Added pre/post-export hooks to validate the native project, reuse the exported Windows player and existing launcher/feed/seed packager, and record exact Git/cloud-build provenance. Post-build delivery checks current-job receipts, actual log gates and every starter-player byte against the validated update payload; includes only current objects. No feed credentials or automatic publication.
- Local validation: eight real-archive delivery tests and 15 publisher tests pass; Windows cmd test skipped on Linux. Shell syntax and diff checks pass. C# staging fixtures are wired into existing Linux/Windows updater CI, and Windows delivery tests added. Actual CI result will be recorded after push.
- Pushed as draft PR #64 stacked on #63. Native CI `37784636310` passes at code `905ab8da171c1f9cb5976d83c2cda002faf556f3`: Linux source/managed/relay, 42 Python tests (one Windows-only skip), 623-file source package; Linux/Windows C# staging and 51 updater checks; Windows 16 publisher and eight delivery tests, shipping launcher compilation and real fixture restart/rollback. These are helper/source checks, not a Unity cloud build.
- Account setup remains blocked: connector discovery found no Unity connection and browser security policy rejected Unity Cloud navigation. No account configuration, credential creation, billing, cloud Unity run, native acceptance, merge or publication occurred. See `docs/UNITY-CLOUD-BUILD-HANDOFF.md` for precise dashboard values, artifact layout, next activation steps and acceptance boundaries. No working cloud API connection is claimed.
- Packaging/developer tooling only: no gameplay, animation/hit timing/hitbox/damage/cooldown/movement/network, save, menu or runtime updater changes. Real Unity hook compilation, Windows cloud export and artifact collection remain unrun. Next: configure the owner's approved Unity project/target and run one pinned clean candidate, then native acceptance before publication.

## 2026-10-08 — Codex — native update publication setup
- Task branch `codex/unity-native-publish`, remote base `d8046635907cf799134a1b99d2483438c4bd70dd` on Claude's `claude/sleepy-ritchie-g8ruhv` / PR #62. Read `UNITY-NATIVE-UPDATE-HANDOFF.md`; GitHub confirms `native/windows-x64/latest.json` does not exist. A prepared local package does not reach the in-game button until published.
- Added `Publish-Native.cmd` and read-only `--status`: verify local payload hashes/archive, Windows success markers and embedded identity; compare current version/revision; stage a preview; require typed confirmation for that build; recheck channel; commit/push only the native path; report the public feed's actual served status. The script's existing explicit `--publish` interface remains. Clean behind-only channel checkouts fast-forward; dirty, ahead/divergent and wrong-repo checkouts stop without resetting work.
- Build output now explains the separate installed baseline and local-only feed. Source ZIP includes the new command. Native CI covers both agent branch families when native paths change, with a real Windows command-entrypoint test.
- Local checks: 15 publisher tests pass; Windows cmd.exe test skipped on Linux. Tests exercise real ZIP validation and full commit/push against disposable local bare Git remotes, cancellation/EOF, same/older versions, failed/inconsistent build logs, channel advancement during confirmation, clean fast-forward, dirty/ahead refusal and preservation of Electron files. No production or test feed used by those fixtures.
- Scope is developer publication tooling, documentation and CI. No gameplay, animation/hit timing/hitbox/damage/cooldown/movement/network, save format, updater service or Claude menu changes. The local checkout materializes the relevant files at the remote base; the GitHub commit applies only this scoped diff atop the full Claude tree.
- CI follow-up: native workflow `37781110655` passes on code commit `375bd2525d85c971430ccad2b9c0e7c68b6283ff`: Linux source/managed/relay/package checks, all 16 publisher tests on Windows, 51 updater transaction checks, shipping launcher compilation (mixed separators/spaces/icon), real fixture-player install/restart and startup/interruption rollback. The first Windows run exposed ZipInfo name normalization; publisher validation now checks raw archive names and fixtures retain malicious backslashes/NULs. PR #63 contains this scoped work.
- Remaining: actual compiled Unity download/install/restart, rollback, settings/account retention and room/match/cancel acceptance. A source ZIP is not a player package; obtain the owner's completed `Builds/NativeUpdateFeed` and build log, or have the owner run the command on the build PC. Native publication still requires the owner's acceptance/authorization as documented in the handoff. Implemented on branch; not merged or published.

## 2026-10-09 — Claude — C11 review fix: reach-stretched blades and the captured-melee floor guard
- Branch `claude/wip-c11` (local, not pushed), on the C11 commit `a3cd5af` (base `81939f9`, PR #62 head). Presentation only.
- Review finding: the renderer stretched melee weapons by stats.reach, but the captured-melee floor guard, blade clearance and support grip measured the unstretched blade. Daggers with one Extended Reach chip put their tips 2–6 cm into the floor in idle, walk and combo; at reach 2.0 daggers went up to 65 cm in. The source scales `r.weapon` before the melee layers (character-animation.ts:57), so its guard, `bladeEnd` and support grip read the stretched sockets.
- Fix: `MeleeView.Reach` → `MeleeRig.WeaponReach`. The floor guard tip, the clearance blade end and pommel, the support grip (also the hit-reaction two-hand test) and the recovery-cache key all use it. The presentation sets it per actor from the renderer's `WeaponReach` before posing. `NativeWeaponDressing.ReachScale` also stretches the fighters' fists only while stunned, rolling or knocked down: their layers reset the fists to 1 (emperor-animation.ts:45, ansatsuken-animation.ts:44; checked with the pinned source).
- Fixture: `tools/export_melee_motion.mjs` adds 7 reach scenarios (daggers 1.2 walk/combo, daggers/sword/greatsword combo at 2.0, greatsword hits 2.0, shield block 2.0). The 22 existing scenarios are byte-identical. With reach ignored, six new scenarios differ by 14–50 cm. The shield has no clearance and stays far from the floor, so it does not change. Each of the floor-guard, clearance and support-grip changes is needed for parity.
- Checks: check_relay OK (captured melee parity 1196 frames, the original 872 plus 324 reach frames, within 1.73 mm; new ReachScale contract checks, mutation caught); Unity runtime+editor compile OK; 24 Python tests OK; package_source OK. A probe over every timeline at reach 1, 1.2, 1.35 and 2.0 finds no grounded frame with a stretched tip under the floor (lowest -0.780, the guard plane). Airborne frames are not guarded, as in the source.
- Not run: Unity import, GPU rendering, a hosted or joined match, the Windows player.
- No change to hit timing, hitboxes, damage, cooldowns, movement or networking.
- Needs Astra (low priority, dev-only weapons): `Actor.JuggleUntil`, set from `NativePilot.Juggle.Until` in `NativeArena.Snapshot` and replicated with the snapshot. Without it, a juggled Emperor/Ansatsuken's fists stop stretching when the .45 s stun ends; the source keeps them stretched for the whole juggle.
- Risks: the first-person fallback pose keeps its fixed-length blade floor clamp; the source does not guard first person at all.
- Correction to the C11 entry's allocation claim (review, low): in first person the hidden local rig is now posed every frame (`HiddenSweep`), and the captured-melee pose allocates a little per call (the overlay closure in `CapturedAnimation.MeleePose`, 64-536 B in `MeleeMotion.Sample`), the same cost as any other visible pilot. Posing it only during the sweep window is a possible later saving.
- Status: implemented on branch; not merged; not released.

## 2026-10-08 — Claude — C11 weapon presentation in Unity (plus the C7 muzzle flash)
- Branch `claude/wip-c11` (local, not pushed), base `claude/sleepy-ritchie-g8ruhv` `81939f9` (PR #62 head). Presentation only.
- `Core/NativeWeaponDressing.cs` is a pure port of the pinned source (05950ea). It covers the client/melee-trails.ts sweep: the window, ribbon identity, 3/1 sub-samples slerped about the grip, the 16-sweep and 48-sample caps, the per-weapon life (x.7 reduced) and the glow formula. It also covers weapon-visuals.ts chip modules (all 17 kinds, the orb bob and coil spin), fire/poison enchants, the muzzle flash and three r180's four primitives.
- `Runtime/NativeWeaponPresentation.cs` draws them through the source-model batches (`Runtime/NativeSourcePrimitives.cs`: NativeSourceLit glow/lit pieces; glow pieces cast no shadow). The ribbons use one dynamic NativeGlow mesh. Melee weapons stretch along +z by stats.reach, blade, modules, enchant and tip together.
- Hooks: SourceCharacter (weapon bone + dressing), the Render loop, ResetPresentation/OnDestroy, the thrown-weapon loop, and the loadout arena set before Render (ConversionGame, killcam replay).
- First person: modules at half size, hidden past half ADS. Enchant on both view daggers; flash on the view gun. The hidden body rig is posed, not drawn, so the pilot's own world sweep shows as in the source. Guests draw from replicated actors, seat chips and recomputed stats.
- Settings: sweeps follow Projectile trails and Reduced effects only, as in the source; the Unity-only Combat glow setting does not hide them.
- No change to hit timing, hitboxes, damage, cooldowns, movement, networking or the event model; meshes never define reach or hit volumes (weapon-visuals.ts:20).
- Slide recoil not added: the source never moves the pistol slide (its voxel part is unnamed, so ranged-motion.ts:222 finds nothing; checked with tsx). The visible gun kick is the hand recoil RangedPose already ports.
- Checks: Unity runtime+editor compile OK; check_relay OK (new WeaponPresentationChecks against `tools/export_weapon_fixtures.mjs` -> `Tests/weapon-presentation-parity.json`; 7 deliberate Core mutations caught); 24 Python tests OK; package_source OK. Voxel renders of modules, enchants, flashes and ribbons through the fxref harness match the source data.
- Not run: Unity import, GPU rendering, a hosted or joined match, the Windows player, performance.
- Needs Astra: a per-hit fighter timestamp on Actor (e.g. `Actor.LastAttack = p.LastAttack` in NativeArena.Snapshot, replicated with the snapshot) so every Emperor/Ansatsuken punch sweeps; until then only the first punch of an action does.
- Risks: the reach-stretched blades were not known to the captured-melee floor guard and clearance (fixed in the 2026-10-09 entry above). Glow pieces read a little brighter than the source without ACES (C19). The Siren stays bare: the source would draw modules and flames on its empty weapon socket.
- Status: implemented on branch; not merged; not released.

## 2026-10-08 — Claude — C7 first-person sway, bob and recoil (implemented on branch)
- Branch `claude/wip-c7-fp-sway`, rebased onto `a0ca6b9` (`claude/sleepy-ritchie-g8ruhv` with PR #66 and the C6 blast merge); first written on `3da391d`. It finishes the uncommitted partial work left in the `claude/wip-c7-fp` worktree, which this session could not commit to. No PR yet.
- Ported the view layer of `client/first-person-view.ts:48-73` to `Core/NativeViewSway.cs` (pure, no UnityEngine). It covers look-sway springs from aim rate (ω 14, ζ .8; ±.03/±.025 m, ±.05 rad roll), stride bob from the first-person rig's gait (`motion-quality.ts` locomotion: blend 13/s, cycle length, 3.7 Hz cap, planted actions), the landing kick (fall speed/9 × .9 into ω 16, ζ .55, × .05 m) and gun recoil (exp(-20·age); rifle 14/s and × 1.4, MG × .6; ADS kick profile). It also covers the reload dip/roll, run-low carry, rocket kick, mark flick and ADS damping to 30 %, with the source clock (.05 clamp, fresh after .35 s or a rewind).
- `Runtime/NativeViewSwayPresentation.cs` applies the layer to the view root after the existing ADS sight alignment, so the sight is measured on the steady pose as in the source. The only edit to an existing runtime file is one hook line in `NativeViewPresentation.RenderFirstPerson`. Inputs are the presented pilot (host state, or the replicated snapshot on a guest) and the local camera look; it runs on the presentation clock and allocates nothing per frame.
- Unchanged: simulation, hit timing, hitboxes, damage, cooldowns, movement, muzzle/aim rays, the network protocol and the third-person view. Reduced effects does not gate the layer; the source does not gate it either.
- Checks: `tools/export_first_person.mjs` runs the pinned source poseFirstPersonView on 8 scripted timelines (30-240 Hz, gaps, rewind, pause, pilot change). The managed check reproduces all 2,567 frames: worst 0.006 mm / 0.00003 rad. It also checks the Unity frame mapping (400 poses), camera look, input mapping, and 4,820 staff view models under a 9 rad/s flick with sprint bob, landing and Q/E (0 crosshair intersections; 0 of 2,976 at the clamp envelope). The existing first-person checks still pass. Real-Unity compile, `check_relay.py`, unittest and `package_source.py` pass. No Unity playtest was run.
- Review fix (guest path): on `3da391d` a guest rendered on the staircase visual clock, which only moves when a 20 Hz host frame arrives, so most frames held. Each held frame overwrote the stored look, so look sway was 3.5-4.8 mm instead of 12 mm, and the bob jumped about 12 mm every 50 ms. This entry first implied that the guest path matched the host; it did not. After the rebase the layer runs on `arena.ReplicaRenderTime` (PR #66's `NativeSnapshotClock`). `NativeViewSway.Sample` now also keeps the stored look and ground contact until the clock advances, so a turn or a landing on a held frame counts on the next step.
- New managed checks: held frames are no-ops and pass their turn and landing on to the next step (60 frames; the check fails on the old code). A modelled guest (20 Hz host frames, 30 ms jitter, the snapshot clock advanced by frame time) matches the host at 60/144/240 Hz: 12.0 mm look sway, the host's bob step (5.2/2.2/1.3 mm per frame) and no held frames. With a 250 ms stall of host frames and a host clock 5 % slower than wall time (200 held frames), look sway stays at 12.0-12.1 mm. Re-run after the rebase: real-Unity runtime and Editor compile, `check_relay.py` (111 PASS lines, 35 managed-core checks), 24 unittest tests and `package_source.py` (691 files) pass. No Unity playtest or two-client run.
- Risks/next: the guest feel depends on `ConversionGame.LateUpdate` keeping `arena.ReplicaRenderTime` as the guest render time. That clock still holds at the newest host frame when frames are late or the host's match clock runs slower than wall time. For those frames the view model freezes with the world, and the turn arrives on the next step, clamped to the 3 cm sway limit, so a long stall loses part of it. A slower presentation clock also raises look sway in proportion (13.3 mm at 0.9× in a scratch harness), because the camera turns in wall time; the host behaves the same. On guests, recoil and the landing kick start one host frame late (20 Hz snapshots, as the source's authoritative snapshot). Not changed: Unity still places fighters (ansatsuken/emperor) and Leet at the melee view offset, while the source uses (0, -.48, -.2) and the staff offset. Next: an owner playtest of the first-person feel at 60 and 144 Hz, hip-fire and ADS.

## 2026-10-08 — Claude — C4 class cards and weapon cards in the Unity menus
- Built on `claude/wip-c4` (base `ea8981b`) and merged into `claude/sleepy-ritchie-g8ruhv` (PR #62). Parity item C4 in `docs/UNITY-PARITY-GAPS.md`.
- The weapon name grid is replaced by the source's YOUR CLASS row (Ranged / Magic / Melee, `???` · DEV CLASSES for developers) and the class's weapons as the source's specialization chips: icon well, name, verbatim description with the aim-higher hint, mono stat line (DMG · /SEC · cost · range), gold pins, 5 px hover lift. Play vs Bots and Test Boss use the stacked class card, the room lobby (host, guest, PvE) the lobby card, Customize its compact card plus compact weapon cards. `Runtime/NativeLoadoutUI.cs` reserves each picker's slot with GetRect and draws the cards through the bodies' deferred kit replay (`U.Defer`, merged from `662c9d3`); `Core/NativeLoadoutText.cs` holds the text, grouping and stat formatting (invariant culture).
- UI state only: the class filters the cards; a pick sets `prefs.weapon` with the old side effect (host rebroadcast). No save, network, rules, hit timing, hitbox, damage, cooldown or movement change. Adaptations: weapon cards in the menus (the source picks them in its in-match phase, A18 / owner decision 5), a selected look borrowed from the active class card, 25 px between card rows, a 60 px lobby class card so the row fits under the roster.
- Checks: Unity 6000.3.24f1 Runtime+Editor compile (warnings as errors) OK; managed core checks 35 passed incl. new `Tests/LoadoutChecks.cs` (15 stat lines, comma locale, hints, class lists for guest/developer/grant); 24 Python tests OK; package_source OK. The first renders used the superseded push-time clip model; after the merge the cards were re-rendered with the current offline renderer (Unity's draw-time clip model) at 1920×1080, 1280×720, interface scale 1.5 and 3440×1440, scrolled and unscrolled, for Play, Boss, lobby host/guest/PvE and Customize. The review also removed per-frame allocations in the kit (tracked text, wrap and texture cache keys, closures). Not run: Unity import/play, real IMGUI input, Windows build, multiplayer.
- Next: owner check in Unity; Astra items from the C4 spec (A18 phase and seat class, Siren uniqueness, PvE Siren rule, unready on loadout change). Create/Join class rows and the pilot card's DEV class label were left out (networking file / C2 overlap).
## 2026-10-08 — Codex/Astra — Unity parity logic handoff, PR #66

- Branch `codex/unity-parity-logic-current`, target `claude/sleepy-ritchie-g8ruhv` (PR #62), requested base `b828acb`. Earlier code checkpoints `718cd4f`, `d80d974`, `c9888fc`; this handoff also includes the regression-checked ordinary-bot/Siren intro ordering and 12-second disconnect grace. Claude drawing changes through `4207641` preserved. PR #65 conflicted after Claude advanced; replacement PR #66 is directly based on `4207641` with the identical reviewed tree in `9921c78`, avoiding a merge/force push. PR #66 remains a draft pending Unity acceptance. Implemented on branch; not merged or released.
- A1–A6 networking/replay/auth blockers and A9–A11 lobby/combat/AI logic repaired. A14–A17/A19–A21/A23/A27–A30 logic is implemented; A8 has LAN/code/invite support, A22 has signed sandbox APIs and A24 has bot tie/pick-source corrections. Gap entries now distinguish code fixes, partial UI handoffs and owner/environment gates. See `docs/UNITY-LOGIC-HANDOFF.md` for exact APIs and retained differences; issue #60 receives one consolidated fix comment.
- Behavior impact: host-authoritative damage/headshots/block/parry/roll and Q/E timing corrected; prediction/interpolation and collision momentum changed; fresh seeds, overtime, bot decisions, revive/horde rules, Chip procs and rematch/retry voting changed. Ordinary bots no longer add intro delays; human Siren enters last. Losing a connection disables its body; a 12-second grace allows resuming health, after which it stays dead until a new round, and absent opposing sides end the match. The 90-second seat lease can reclaim the seat/results but does not undo a completed forfeit. Protocol **5** requires matching current native clients/authority; no Electron interop is implied.
- Actual checks: **100 PASS lines** in CoreTests, including real UDP/TCP; `check_relay.py` passes real WebSocket tests; Python unittest **24/24**; UpdaterTests **51/51**; Runtime API compilation against genuine Unity **6000.3.24f1** DLLs passes with C# 9 (0618/0649 existing warnings suppressed, remaining warnings errors). Local SDK process inspection blocks MSBuild, so local C# runs use SDK Roslyn + .NET host. GitHub native CI **37811222667 on c9888fc passed both normal-project source tests and Windows launcher/process fixtures**. `package_source.py` validates 670 source files and creates the ZIP. No secret or binary added to the branch.
- A12 build attempt: `cmd.exe /c Build-Windows.cmd` exits **127, cmd.exe not found** in the Linux authoring host. No activated Unity Editor/license exists here. New genuine JsonUtility/PhysX tests are wired into NativeBuildChecks but **not run**; Mono verification, actual headless/client Unity execution, multiplayer frame-rate/load play, live account grants, real-player updater acknowledgment and rendered four-resolution tests remain. No `UNITY_TEST_BUILD_READY` claim.
- Claude hooks: `LobbySeats` includes cosmetics/bots/difficulty; `GuestReady`, `ChangeTeam`, `RoomOptions`/`SetRoomOptions`; `LanAddresses`, `RoomCode`, `InviteUrl`/`JoinInvite`; `ReloadSavedPilot`; vote/host-loss/return timer fields; new EventKinds including radius/origin/detail and separate DoT/heal events; measured `ConnectionMetrics`/`PeerMetrics`/JSON report; sandbox and fullscreen methods. Timed specialization state is intentionally absent until owner decision 5. Hand-off table describes each contract and event payload.
- Remaining decisions: A7 endpoints/relay hosting, A8 internet directory, A13 release PR merges/feed publication, A18 specialization, A24 Siren intro duration, A25 dedicated topology, A26 migration/cutover and A22 visual-test scope. No merge, force push, Claude-branch push, feed or Site publication. Next: Claude can wire remaining presentation controls from the handoff, then an activated Windows Unity build of the PR head must establish acceptance before owner-approved integration/publication.

## 2026-10-08 — Codex/Astra — Unity parity logic checkpoint (work in progress)

- Branch: `codex/unity-parity-logic`. Started from `b828acb` on PR #62; integrated Claude's visual updates through `662c9d3` without changing the drawing design. Local implementation checkpoint `92cf245`; remote PR/commit recorded below when pushed. Not merged or released.
- First priority pass: explicit optional-object presence for real JsonUtility (network and replay), protocol 4; background socket loops; input acknowledgement/prediction and buffered replica transforms; bounded presentation events/snapshots, shared compression, lobby broadcast throttling, epoch fencing, relay error/retry handling; managed P-256 verification with catch-all trust boundaries.
- Lobby/combat pass: `GuestReady(bool)` includes loadout, team defaults/changes, bots and options/cosmetics in lobby state, automatic ready start; held melee, head-volume tests, ability/block/reload/roll rules, authority aim and persistent pull; bot difficulty/reaction/kit decisions, LAN discovery/invite data. Fresh draft seeds, overtime, random arena/per-set option and source setup defaults are included. Remaining A-items are still being worked through; the gap list will be finalized at handoff.
- Claude integration API: `LobbySeats`, `RoomOptions`, `SetRoomOptions`, `ChangeTeam`, `GuestReady`, `LanAddresses`, `RoomCode`, `InviteUrl`, `JoinInvite`; seat `difficulty`, cosmetics and `bot`; options include per-bot loadout/team/difficulty and intro/random-map toggles. Wired Ready and real bot records into the latest lobby with small data/action edits only. Profile reload, specialization decision, expanded events and connection metrics follow later in this task.
- Checked at the first logic checkpoint: genuine Unity 6000.3.24f1 player API compilation of Core+Runtime using C# 9; managed regressions including RFC 6979 plus 24 independent P-256 signatures, paused synchronization context, prediction, event/order budgets, headshots, teams/bot rules, real UDP/TCP discovery; `check_relay.py` with precompiled Roslyn assemblies passed; Python unittest 24/24; UpdaterTests 51/51; `package_source.py` passed (642 files). Recheck after integrating later commits. Roslyn is invoked directly because this container's .NET SDK driver cannot read process metadata.
- Not run: Unity Editor/JsonUtility/PhysX build-gate execution, Windows Mono gameplay, or `Build-Windows.cmd` (Linux container has no cmd.exe, Unity Editor or activated Unity license). New real-JsonUtility/physics regression gates are source only until that run; API compilation is not a Unity build or playtest. No `UNITY_TEST_BUILD_READY` receipt exists here.
- Owner gates preserved: A7 endpoints/relay hosting, A13 publication/merges, A18 specialization choice, A24 canonical Siren intro duration, A25 server topology, A26 migration/cutover, and A22 visual sandbox scope. No feed/Site publication, merge, Claude-branch push, or secrets.

## 2026-10-08 — Claude — C6 blast review fixes
- Branch `claude/wip-c6-fixes` (local, not pushed), base `claude/wip-c6` `4c4f8ea`. Presentation only; fixes four review findings on the C6 port.
- One cue per source event: a record whose key repeats in the same frame is skipped, and `NativeBlastFrame.Collapse` drops a teleport landing where an earlier one landed, and a blast or dud at the spot of a larger one with the same tag. This removes Blink Strike's second void blast, origin impact and streak (ChipBlink and Blink both record it), the second shock blast of a ball-lightning wall hit and the grey dud after an armed rocket hits a summon. Untagged chip blasts that share a spot are kept. Model: Blink Strike 307 -> 236 voxels and 3 -> 2 lights; ball lightning 305 -> 197 and 2 -> 1.
- Step: the blasts now advance with the unscaled frame time capped at 0.05 s (`NativeBlastFrame.Step`, as the source main loop) and stop only when the match is paused (offline menu, `RenderGlow(..., menu&&!Online)`). The arena clock still drives record ages, field emitters and rewinds. Guest model (20 Hz snapshots, 144 Hz render): 401/720 frames with dt=0 and 40 ms steps before; 0/720 and 6.9 ms after. The reported online-menu pile-up did not reproduce in a model of the real clocks. `PresentationClock` falls back to `arena.Time` once the paused render clock falls behind, so the old step ran at 60 Hz (2521 of 4320 frames stalled) and peaked at the same 316 voxels. The frame step removes those stalls.
- Siren: records of the Siren's own attacks (`NativeBlastFrame.AtSiren`: element 4 under a Siren pilot, on its shots or fields, or on a Siren-stage vent) are no longer implosion blasts. The source draws them with siren/fx.ts, not ChipBlasts. They keep the old flat halos (`NativeBlastPresentation.Halos` -> `NativeCombatGlow`) until that port (new parity item C25) and the A21 owner field land.
- No change to hit timing, hitboxes, damage, cooldowns, movement, networking or the event model.
- Checks: Unity runtime+editor compile OK; check_relay OK (BlastChecks adds step, collapse and Siren-site checks); 24 Python tests OK; package_source OK. fxref harness for the affected styles (fire, shock, void, implosion, blink-strike and ball-lightning tints, dud and teleport impacts): 29 numeric runs equal and 10 rendered sheets with 0 pixel diff.
- Not run: Unity import, GPU rendering, an online session, a Siren match in the player.
- Status: implemented on branch; not merged; not released.

## 2026-10-08 — Claude — C6 volumetric chip blasts in Unity
- Branch `claude/wip-c6` (local, not pushed), base `claude/sleepy-ritchie-g8ruhv` `662c9d3` (PR #62 head). Presentation only.
- `Core/NativeBlastSim.cs` is a pure port of client/chip-blasts.ts (05950ea): 11 styles, palettes and tints, the 0.3 s front, 900/700 doubling pools, the 16-voxel minimum, quality and compat scaling, impact(), field emitters, line, paddle, three pooled lights, the source's seeded LCG and Float32 storage. `Runtime/NativeBlastPresentation.cs` draws it with one dynamic mesh per pool from reused buffers (new unlit `Resources/NativeBlast.shader`: opaque, and additive with no depth write) and three point lights (three.js intensity x .35, as NativeCombatGlow). Clock: the render clock, so a menu pause freezes it (superseded by the review fixes above: frame time, paused only offline). Reduced effects = 0.3, as the source.
- Driven on host and guests from replicated data: every non-beam ability/chip effect except the Siren's own (see the review fixes above; this replaces the two 0.22 s halos in NativeCombatGlow, Leet nuke included), blink teleports (void blast, origin impact, streak), Vacuum, ability zones and chip fields (emitters, arrival and lock impacts) and surface impacts (water, kinetic and demolition procs). Style: the source tag of a zone, field, ability shot or teleport that ends at the effect; otherwise the owner's school (fire, frost, shock, implosion; no school = fire). The host reads ChipField.Effect; guests have only the kind until it is replicated.
- No change to hit timing, hitboxes, damage, cooldowns, movement, networking or the event model. Coordinates go in as Unity positions; the converted maps keep the source's numbers, so the in-game layout is mirrored in Z just as the maps are.
- Checks: Unity runtime+editor compile OK; 24 Python tests OK; package_source OK; check_relay OK (new BlastChecks: 88 source runs, 481 frames from `tools/export_blast_fixtures.mjs` -> `Tests/blast-parity.json`, plus determinism, front at 0.3 s, capacity, minimum voxels, quality, cues and winding). fxref harness: 97 numeric runs equal voxel for voxel; 43 rendered comparisons (11 styles x r 1.2/3/6, tints, impacts, fields) have 0 pixel diff.
- Not run: Unity import and shader compile, rendering on a GPU, the Windows player, an online session, performance.
- Needs Astra (A21): carry the source event type and blast message, plus the owner, on ability/chip effects and NativeEffectFrame; replicate ChipField.Effect, zone ids and lockAt; add the missing events (freeze, panic-button nova, summon, critical/catch/split/meleeRebound/slowed/ricochet, arcs, impacts on players, slash). Then switch NativeBlastCues to the tags.
- Status: implemented on branch; not merged; not released.

## 2026-10-08 — Claude — Unity C7 status visuals and team bands
- Branch `claude/wip-c7-status` from `claude/sleepy-ritchie-g8ruhv` `4207641` (PR #62 head). Not pushed, merged or released.
- Ported client/status-effects.ts (burn flames + embers, poison bubbles, slow droplets, shackle cuffs, Orbital Mark reticle; world rigs, the first-person hands rig and up to 16 PvE adds) and client/team-identity.ts bands (in-match, draft stage, entrances; PvE party blue / Siren purple as pve/contract.ts). Motion and shapes are pure Core (`NativeStatusVisuals`); `NativeStatusPresentation` draws them with a new unlit fogged shader (`NativeStatusUnlit`), allocation-free per frame, quality .3 under Reduced effects.
- Checks: Runtime+Editor compile against Unity 6000.3.24f1; check_relay (all managed checks, plus the new status parity check: 53 source StatusRig samples, geometry, materials and 36 bands from `tools/export_status_fixtures.mjs`); 24 Python tests; package check. Not run: Unity Editor, shader compile, rendering, Windows build, multiplayer.
- No simulation, damage, timing, hitbox, cooldown, movement or protocol change. Shackles are read from the slow factor (.4 is only Shadow Shackles in the pinned content).
- For Astra: replicate pilot DoTs (fire/poison until), SlowEnd/SlowFactor (or a slow kind), the Orbital Mark zone's target and LockAt, and add DoTs/SlowUntil, so guests and killcam replays show these statuses.
- Next: owner build and look at burn/poison/slow/shackle/mark and team bands in a host match at 1080p (reduced effects on and off).

## 2026-10-08 — Claude — C17 Ward Zero gore and revive beacons in Unity
- Branch `claude/wip-c17` (local, not pushed), base `claude/sleepy-ritchie-g8ruhv` `4207641` (PR #62 head). Presentation only.
- Gore (client/pve/gore.ts): `Core/NativeGoreRules.cs` ports gib() (hash-seeded throw, CHUNK sizes, spin), chunkAt() (arc, landing at size.y/4, .35 s skid), update() (spin stop, last-.6 s shrink, 12-chunk cap, rewind drop, slot colours) and the BurstPool ink with hashed randoms. `Runtime/NativeGorePresentation.cs` throws one chunk per severed part when the replicated `NativePveAdd.Lost` (or a fresh corpse's `lost`) gains bits, from the posed rig joint, timed and aimed by the severing Hit/Headshot event on the add; directed hits splash 6-10 ink drops. Instanced NativeLit cubes, no shadows; Reduced effects = quality .3. The body keeps hiding the lost limb as before.
- Beacons (beacons.ts, downed.ts): `Core/NativeBeaconRules.cs` (state, clock, ring arc, column/ring geometry, motes map and HELP bubble pixels); `Runtime/NativeBeaconPresentation.cs` draws column + ring for each downed, connected party member (not in a vent shaft, run not ended) in one additive mesh with the new `Resources/NativeBeacon.shader`; `Runtime/NativeBeaconHud.cs` draws the label (depth via raycast) and the HELP bubble (not for the downed player) with the kit at the projected point, sized by view depth like a sprite. Hooks: NativeSourcePresentation (SourceWorld end, ClearSourceActors), ConversionGame (EffectsQuality, HUD call), NativeBuildChecks shader list.
- Review fix: every Hit event on an add splashed ink, so a chip field (a Hit every 1/60 s), ability zone or burn/poison tick turned an add into a fountain of 600 drops/s that filled the 192-drop pool; a field tick later in the step also stole a bullet sever's time and aim. `Core/NativeGoreHits.cs` now applies the source rule (horde.ts:354, horde-view.ts:211): only a hit with a horizontal direction (ArenaEvent.Normal) splashes, once per hit time; a sever takes the newest directed hit within .1 s of the add's newest hit, and a tick-only sever rises with no direction (gore.ts zero dir). Unity Hit events carry no direction yet, so until an arena shows a directed hit, each add splashes at most every .25 s (a field add keeps at most 30 live drops) and a sever keeps the newest-hit aim. `Tests/GoreHitChecks.cs` pins both modes.
- No change to simulation, revive rules or timing, hit timing, hitboxes, damage, cooldowns, movement or the network protocol.
- Checks: Unity runtime+editor compile OK; 24 Python tests OK; package_source OK; check_relay OK (new GoreBeaconChecks from `tools/export_gore_beacon_fixtures.mjs`, GoreHitChecks). Scratch renders of source vs port (fxref chunk frames; three.js beacon scenes with the shader replica and HUD layout) match; the bubble shape matches the Chromium canvas to 0.27/255 mean alpha.
- Not run: Unity import and shader compile, a GPU, the Windows player, a hosted/joined Ward Zero match, performance.
- Not ported: the black-flame FlameField puffs and stump drips (siren/flames.ts, with C12).
- Needs Astra (hit direction): in `NativePveHorde.HitPveAdd`, set `Normal=Vec(dir)` on the emitted Hit event when `hypot(dir.x,dir.z)>1e-6` (horde.ts:354), else leave it zero; e.g. an optional normal argument on `NativeArena.Emit`. Normal is already a serialized ArenaEvent field in `snapshot.Events`, so the wire format does not change, and guests get it with the events. The presentation switches to the source rule on the first directed hit with no Claude change. Gap until then: field, zone and DoT ticks splash ink every .25 s per add, and a bullet sever on an add in a field or burning can fly along add minus field owner.
- Needs Astra: replicate `NativePveRow.Revived` (add `revived` to NativePveRowFrame, set in the pve frame rows and ApplyPve) so a guest's label shows the real revive price (now $150). Optional: a replicated pve-gib event per severed part from NativePveHorde.HitPveAdd (time, gibs.ts joint, hit direction, limb) to replace the inferred time and direction.
- Status: implemented on branch; not merged; not released.

## 2026-10-08 — Claude — Unity vs Electron parity gap list (handoff)
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62), audited at `eb8f7f7` against the original `05950ea`. Docs only; no code changed.
- Added `docs/UNITY-PARITY-GAPS.md`: a read-only code audit of screens, gameplay, networking, presentation and trackers, with each P1 re-checked by a refuting reviewer. Items are split by the role split: A1–A31 for Astra (logic) and C1–C24 for Claude (visuals), plus 12 owner decisions and a not-verified list.
- Main result: content is at parity in code, but real sessions are blocked. Guests drop on the first match frame (A1, #1/#3), with transport/frozen-view/frame-size defects behind it (A2–A4). Sign-in fails under Mono ECDsa (A6). No zero-setup online hosting or code/invite join (A7/A8). The killcam throws outside PvE (A5). Combat rules and bot AI differ (A10/A11). None of the 17 critical/high findings in #60 is fixed in code.
- Checks: none run; nothing was built or played in Unity. I spot-checked the cited code for A5 (JsonUtility round trip), A10 (0.25 s stun), A14 (intros forced on), A15 (fixed draft seed 12345) and A16 (Enemy predicate).
- Next: Astra starts at A1/A5 (shared JsonUtility root), then A2–A4 and A6. Claude's independent items: C2, C4–C6 (existing blast events), C9–C12. Owner: the hosting and endpoint decisions (decisions 1–2) gate A7/A8/C1.

## 2026-10-08 — Claude — native main menu rebuilt from the source home screen
- Branch `claude/sleepy-ritchie-g8ruhv` (PR #62, draft, base `codex/unity-updater`), on top of the entry below. Commits `de94ebe` (icon set + rasteriser), `4fbd54c` (menu), then the review-fix commit that adds this entry. Owner request: restore the normal menu UI, made more appealing.
- Home screen (`NativeHomeMenu.cs`) follows the live source rules (main.ts drawMenu; style.css/lobby.css/pve.css after all override layers): brandmark + PROTOTYPE, eyebrow, CHIP / ARENA. (clamp 68–104 px), tagline, Play vs Bots / Create Room / Join Room, flex-sized Customize / Settings / account / DEV Menu tiles, Developer Sandbox · Catalogue · Quit links, the Boss Test pill with the source skull, footer, entrance stagger. Extras: per-letter title entrance and light sweep, voxel motes, primary sheen, keys 1–4. Fits short canvases (large interface scale).
- Icons: `tools/export_icons.mjs` exports the pinned Lucide set + icon-map (ISC text included) to `Icons.json`; `NativeIcons` rasterises paths/arcs/shapes and gradient fills per drawn pixel size. All 101 icons compared with Chromium at 48 px: mean IoU 0.968, min 0.944 (six icons were wrong before compact arc flags were parsed).
- Panels wrap the existing forms unchanged in behaviour: Play vs Bots (also the sandbox setup), Create / Join Room, room lobby (Customize and Settings open over it), Boss Test, Customize with a live pilot card, Settings, Account, DEV Menu, Catalogue. Pause menu: Resume, Settings, Sandbox Tools, Restart (offline), Return to Lobby (host), Return to Main Menu; a guest whose host dropped is told how to reclaim the seat.
- Updater: `InitializeUpdater()` unchanged; `UpdatePanel(right, top)` is the card opened by the home Update Game pill (state line under it, forced open while downloading/verifying/preparing/restarting); `CanInstall` gate unchanged (Escape on the main menu no longer clears the menu flag). README note added. `NativeUpdater` and packaging untouched.
- Review (6 agents, each finding verified): fixed a host alone in a Versus room with 0 bots reaching the permanent error screen (StartMatch now checks seats/sides before touching the arena and reports in the menu), rejected joins (lobby only after welcome; host error kept), relay joins failing on a hidden port, pending connections not cancelled on leaving Create/Join, lost-room messages, Settings saved on every exit, stale update-card hit tests, capsule texture churn on hover, Boss party-bot weapon toggle shown.
- Also: kit text no longer multiplied by the skin label colour (#e6e7ec), so overlay text is its exact colour. No gameplay, hit timing, hitboxes, damage, cooldowns, movement or network protocol changes; host lobby weapon changes now rebroadcast the roster.
- Checks: Runtime+Editor compile (warnings as errors) and strict unused-field pass; 93 managed checks; 24 Python tests (new icon-data test); 51 updater checks; relay; package. Offline renders of the real drawing code (scratch harness with a recording UnityEngine stand-in, painted in Chromium; not committed) at 1920×1080, 3440×1440 and 1280×720 at 1.5 scale. Not run: Unity import/render, GUILayout panel contents, `Build-Windows.cmd`, multiplayer rooms, an actual in-game update.
- Update button follow-up: the native feed (`chip-arena-test-updates/native/windows-x64/latest.json`) is 404 — nothing has been published, so the button correctly finds nothing. The badge now says "No update published yet" (Idle/404) instead of "Update check unavailable", the card chip reads NOT PUBLISHED, and opening the card re-checks. An update is offered only for a build with different hashed source (Assets/Packages/ProjectSettings/Updater) packaged later than the installed starter; publishing stays an owner step (`tools/publish_native_update.py --publish`, Updater/README.md).
- Handoff for Astra on making the update button deliver builds (owner request): `docs/UNITY-NATIVE-UPDATE-HANDOFF.md`, cross-checked against the code, git history and the live feed.
- Next: owner rebuilds with `Build-Windows.cmd`, clicks through every screen at 1080p and 3440×1440, hosts and joins a room with a second player, and checks the pause menu and update card. Not merged or released.

## 2026-10-08 — Claude — Unity melee runtime, presentation polish and updater integration
- Branch `claude/sleepy-ritchie-g8ruhv`, based on repair `c2cb7fd` (merged at `7c7f6c2`) with Astra's `codex/unity-updater` merged through `6235a82` at `d185be4`. Implementation `1c0f257`–`9df7212`. Isolated conversion work only; not merged or released.
- Melee runtime (`1c0f257`, `106b8c4`): source gait, footwork, hip seating, blade clearance, blending/recovery and hit/parry reactions for the 34 captured clips; parity 872 frames, worst 1.73 mm vs transpiled source. Presentation only: hit timing, hitboxes, damage, cooldowns, movement and networking unchanged. First-person staff placement/jitter fixes retained.
- Stage: hazards drawn from exported source parts and animated on the shared hazard clock (also on replicas); Ward Zero baked from its first source frame (crowd off the centre, lamps lit, glow intensities, beams) with song-mood fan/doors/chandeliers/clocks/beams; lavaworks smoke colour; menu backdrop orbit; scenery on the render clock.
- Overlays (source CSS numbers, authoritative clocks): intro with Siren title (name time 6.4261), countdown, round result + set score, match end, killcam frame + LIVE handoff, spectator label, death line, PvE banners/outro/results, Siren chip reveal. Result strings follow the source; draw no longer plays the lose sting.
- Chip selection: source chip card (exported description/stat lines), PvP draft header/pills/timer/nameplate, per-tier entrances, hover/focus/keys, shatter, palm fly, spotlight; Siren personal pick over the 3D stage with keys 1–9, alarm and vignette (fight keeps simulating). Draft rules, timers, individual PvE picks and vulnerability unchanged; turn order and turn start are derived client-side (no protocol change).
- HUD/menus: source fight HUD layout, pixel-centred crosshair and hit marker, PvE HUD, menu skin, one UI canvas (3440×1440 → 2150×900) and an Interface scale setting. Updater: `NativeUpdateUI.cs` restyled as a menu card (check/availability, progress, cancel, error/retry, Update and restart gated by `CanInstall`/`CanCancel`/`Busy`); `InitializeUpdater()` unchanged, `UpdatePanel()` still called from the menu; `NativeUpdater`, rules, launcher and packaging untouched.
- Checks on the combined tree: Runtime+Editor compile against Unity 6000.3.24f1 DLLs (warnings as errors, plus a strict unused-field pass), 93 managed checks, 20 Python tests, 51 updater transaction checks, relay check, package check — all pass. A Python export check that still expected 133 models was updated to 139 (Ward beams). Not run: Unity import/render, in-motion animation and UI, `Build-Windows.cmd`/`UNITY_TEST_BUILD_READY`, starter ZIP, `check_updater_windows.py`, and an actual in-game update.
- For Astra (issue #60, not changed here): PvP Siren intro phase 9.0678 s vs source 7.2678 s; bots queued in intros; `skipBotDrafts` default on (source off); bot draft tie-break; no return-to-lobby countdown; y>19 ceiling bounce; PvE pick source label not replicated (heuristic).
- Next: owner rebuilds with `Build-Windows.cmd` on this branch, requires `UNITY_TEST_BUILD_READY`, installs the starter ZIP once, playtests melee/overlays/draft/HUD at 1080p and 3440×1440, then tests a newer compiled build through the update card before any native feed publication.

## 2026-10-08 — Codex — native launcher packaging path repair
- Continue `codex/unity-updater` / PR #61 from `bfb4079156ef021eae74e88e331a3d2cca130323`. Owner's next build.log confirms Editor/player compilation, native gates (including all 15 maps/120 spawn floors, presentation lifecycle, gameplay and animation playback) and Unity player build Success. Launcher packaging then fails CS1504 looking for its four source files at the project root; no final starter/feed marker was emitted.
- Move launcher compilation to `NativeLauncherCompiler`, shared by Editor packaging and a Windows regression driver. Normalize absolute paths, preflight every source/config input, set the project working directory and use a UTF-8 csc response file; diagnostics name the retained response file. Existing ZIP enum alias remains. No gameplay, animation, damage, timing, hitbox, cooldown, movement or networking changes.
- Windows gate now invokes the exact compiler implementation with mixed separators, spaces, an icon and real sources, then tests the resulting install/restart/rollback/recovery launcher. Previously CI compiled the launcher separately and missed the Editor's argument construction. CI 37716445819 at `3036366` passes both native jobs, including this exact packaging regression, missing-input rejection, 51 transaction/archive checks, real Windows install/restart/rollback/recovery, managed/relay checks and all 18 Python tests; local source/package/diff checks pass. The complete Unity packaging run and actual in-game update still need owner verification.
- Refresh the cumulative Editor patch and full source package. Next: close Unity, extract the patch at the project root (both Editor source files plus the new .meta), rerun Build-Windows.cmd and require UNITY_TEST_BUILD_READY. No merge, native feed publication, account mutation or production change.

## 2026-10-08 — Codex — updater Editor compression-name compile fix
- Continue `codex/unity-updater` / PR #61 from `ba936a86b54bfcac7aee93680148196e78ab59ea`. Owner's Unity 6000.3.24f1 Windows build log (01:55 UTC) reports three CS0104 errors in `NativeUpdateBuild.cs`: both UnityEngine and System.IO.Compression define CompressionLevel. The Editor-target runtime assembly compiled; the Editor assembly failed before any native build gates or player build ran.
- Add an explicit alias to System.IO.Compression.CompressionLevel for all three ZIP packaging calls. No runtime, animation, gameplay or network changes. Refresh the full source package and provide the single-file build patch. The shared Windows launcher tests did not compile the Unity Editor packager, which is why preceding CI missed this error.
- Local package/source checks pass; existing branch CI runs for this correction. Actual corrected Unity Editor compilation and Windows player build remain unrun here. Next: replace `Assets/ChipArena/Editor/NativeUpdateBuild.cs` in the owner's project and rerun `Build-Windows.cmd`. No merge or native feed publication.

## 2026-10-08 — Codex — native Unity update button and rollback launcher
- Isolated `codex/unity-updater`, based on repair `c2cb7fda9c7a0e04f856dcfdc381d9eef9fac4a3` (PR #59); implementation checkpoints `4be166e` and `5d657a1`. No animation, combat timing, hitbox, damage, cooldown, movement or gameplay-network changes. Claude's melee/visual branch remains independent.
- Added a menu update service with bounded HTTPS downloads, progress/cancel/error states, SHA-256 and version/product checks. A .NET Framework 4.8 launcher stages and swaps `app/`, waits for the requesting player, verifies startup, and rolls back or recovers interrupted directory swaps. Preferences/account data stay outside the replaced folder; stable top-level EXE preserves shortcuts.
- Windows packaging now builds the launcher and starter ZIP plus chunked native feed. `tools/publish_native_update.py` validates and stages by default; explicit `--publish` writes only `native/windows-x64/` in the existing test-channel repository. No feed, service or account publication was performed. UI contract and Claude handoff: `unity/ChipArenaConversion/Updater/README.md`, presentation adapter `NativeUpdateUI.cs`, two `ConversionGame.cs` hooks.
- Passed at `5d657a1`: CI 37712635437, existing managed/source/real relay checks, all 18 Python tests, 51 updater transaction/archive checks on Linux and Windows, shipping launcher compilation with the Windows .NET Framework compiler, and actual-process Windows install/restart and failed-start rollback tests. The first Windows run exposed short-path vs full-path executable identity mismatch; fixed and retested. Added an additional real-launcher recovery test for interruption with no `app/` folder; final branch CI verifies that change.
- Not run here: Unity import/player/Editor compilation of the new adapter and packaging code, activated Windows Unity build, actual game download/restart and rendered UI. CI fixture players do not prove Unity runtime acceptance. Next: integrate the scoped branch with Claude's work, run `Build-Windows.cmd`, install the starter once, test a newer compiled build through the button and publish the accepted native feed. A source archive cannot update installed players. Implemented on branch; not merged or released.

## 2026-10-07 — Codex — presentation component startup repair
- Continue isolated `codex/unity-build-fix` from `a46043a126b77407af6cd762a2dfe50eb78db87a`, draft PR #59. Owner's 20:27 screenshot shows the match-setup UI with an `ArenaPresentation` constructor/field-initializer `CreateImpl` error followed by null references. No successful build log or rendered match is supplied.
- Move the Ward `MaterialPropertyBlock` allocation from the component field initializer into explicit `Initialize`, after `AddComponent`. Inspect related component/presentation initializers. No combat, hit timing, hitboxes, damage, cooldown, movement or networking changes; material properties are unchanged.
- Close the native build coverage gap: instantiate/initialize/reset/tear down the actual presentation component twice, verify audio/particle/light pools and reject logged Unity errors. Reuse the existing play/edit-aware object release helper for the initialization sample and material cleanup. This native gate is unrun here.
- Actual local checks: 12 asset/package tests, source receipts/GUIDs and diff checks pass. Conversion CI runs on the repair branch. The preceding `a46043a` conversion CI passed all managed/source/relay/package checks; those tests did not instantiate Unity components. Rebuilt Windows player, native lifecycle gate and match start remain owner verification. Refreshed full source ZIP; no merge or updater/feed/service publication.

## 2026-10-07 — Codex — zero-cost ability Windows gate repair
- Continue isolated `codex/unity-build-fix` from `1e96f3b53305ee3dbdb3dd0a3ec2e5cecd1c8866`, draft PR #59 targeting `codex/unity-conversion`. Owner's second Windows log confirms script compilation and the previous texture fix, asset imports, glow, native melee and E-special gates pass; the next gate fails on `frag-grenade`.
- Source grenade, rocket and orbital mark cost zero mana. Correct the validator's blanket mana-debit and empty-pool-refusal assumptions. Assert exact configured debit/cooldown/charge use, no spend before release, zero-mana free casts and paid-cast refusal without state changes. Add exact-cost/zero-mana checks to all 44 managed source ability fixtures. No runtime rules, source content, hit timing, hitboxes, damage, cooldowns, movement or networking changed.
- Actual checks: 12 local Python tests, source package/asset receipts/GUID checks and diff checks pass. Managed checks run in branch CI; corrected native gate, remaining map/PvE gates, rendered gameplay and Windows EXE are unrun here because Unity is unavailable.
- Deliver refreshed complete source ZIP. Next: owner reruns `Build-Windows.cmd`, confirms `UNITY_TEST_BUILD_READY`, then playtests the isolated native build. Implemented on repair branch only; no merge, updater/feed release, live account mutation or service deployment.

## 2026-10-07 — Codex — Windows texture upload and account callback repair
- Isolated `codex/unity-build-fix`, based on conversion candidate `a8fa79611d84f171e5c79899b379231570b8337d`. Owner supplied the failing Windows build log. No open PR targeted the conversion branch when checked.
- The activated Windows Editor compiled scripts and passed the source combat/presentation checks, then failed because the source model loader supplied mip-0 bytes to `LoadRawTextureData` on a mipmapped texture. It also rejected `NativeAccount.Start(string, string, bool)` as an invalid Unity callback.
- Upload the validated base image with `SetPixelData<byte>(..., 0)` and regenerate mipmaps with `Apply`; preserve RGBA pixels and texture repetition. Rename the account helper and all four callers to `BeginRequest`. No gameplay, hit timing, hitboxes, cooldown, movement, account protocol or network changes.
- Add native build gates for real texture pixel round trips, mip-chain retention, truncated input and callback signature; these are unrun here. Local 12 Python asset/package tests, package receipts/GUID validation, call-site review and diff checks pass. Native C# compilation and the Windows Editor/player are unavailable in this workspace; source/relay CI runs on this repair branch.
- Deliver the complete corrected source ZIP for a fresh Windows build. Next: run `Build-Windows.cmd`, confirm the native texture gate and `UNITY_TEST_BUILD_READY`, then playtest. Implementation on branch only; no merge, updater publication, service deployment or live account mutation.

## 2026-10-07 — Codex — full native conversion candidate for owner acceptance
- Separate `codex/unity-conversion`; parent `a787a77cdb61e4ea1528e19faf2a3bb0e87efbb8`, pinned original `05950ea`. Completed the remaining implementation pass and prepared the isolated Windows builder/source handoff; no merge or release.
- Added original voxel wound carving and bone anchors, exact source headshot-collapse curves/release velocities, native sprays/gibs, snow/water/smoke, source Slender and summon models/poses, victim static/scare/audio, station/pickup motion, combat HUD, death-announcement holograms/audio and synchronized 3D drafts. Model bank: 133; cosmetic scenes: 31; motion clips: 165; audio files: 83.
- Fixed fatal-hit stagger incorrectly suppressing headshot kneel; collapsed body release uses native PhysX. Combat damage/cooldowns/hitboxes are unchanged by this pass. Native protocol advances to 3 for wounds, machine/summon state and draft choice time; older native clients cannot join. Reviewed original source: automatic player-host migration is absent there too; corrected prior remaining-work notes.
- Passed: genuine Unity Runtime/Windows/Editor C# compilation with warnings as errors; managed/source regressions, 84 carving cases/64 hashes, eight collapse scenarios, 79 summon/station/draft samples; eleven asset/package tests and real TCP/WebSocket relay checks. Initial Editor compile exposed private authority-flag access and was repaired inside runtime validation, without making the flag public.
- Added native gun/magic/Q/E/reflection/headshot and PvE economy/revive acceptance gates. These compile but were NOT run here. Activated Unity import, shaders, native gameplay/PhysX, rendered first-person and lighting, live accounts/remote rooms/headless execution, and EXE build are unrun. Native material/UI/audio/cosmetic-physics adapters are explicitly documented; pixel identity is not claimed.
- Next step: owner extracts source, closes the project in Editor, runs `Build-Windows.cmd`, checks `UNITY_TEST_BUILD_READY`, then tests the separate `ChipArena-Native-Conversion-TEST.exe`. Full folder ZIP and voxel CA icon are retained. No updater/feed publication, service deployment, account mutation or production change.

## 2026-10-07 — Codex — source cinematic and menu checkpoint (work continuing)
- Separate `codex/unity-conversion`, parent `adaba58532d52b7ea63f339b0f85e220b012d1b2`, source `05950ea`. The complete-conversion task remains active.
- Replaced temporary entrance/death props with offline samples of the original 20 entrance stages and 11 scripted deaths: original clown/love/van, confetti/ascension/shrink/launch/ghost, Raging Demon and Siren crush/dissipate. Added original light/fog cues, per-instance motion/colour/opacity, actual dropped weapon models and death-pose blending. Fixed zero-scale anchor inversion and vertex alpha. Source model bank now has 124 models.
- Ported ragdoll style forces/freeze/impulses and kept-body fallback, added native dropped weapon bodies. Added appearance/entrance previews in an unsimulated isolated scene, player build inspection, 1–12 draft choices, selection timer/both-side options, scrolled cards/reveal, and sandbox sort/speed. Replicated fighter audio timing, added source Raging Demon hit cues and fixed relay disposal after cancellation.
- Verification: four scene asset tests (31 clips/124 models, finite tracks, noncollapsed meshes, original clown weapon drop, Siren blackout, dynamic demon/flames) pass; actual Unity Runtime/Windows/Editor C# compilation passes. Asset/package receipts and existing managed/transport gates remain required before push. Earlier checkpoint CI `37588486683` passed .NET build and relay integration.
- These changes affect presentation, menu choices and cosmetic physics; combat hitboxes/damage/cooldowns are unchanged. No activated Unity import, shader/rendered play, native body physics acceptance or Windows EXE was run. Full living-wound carving, remaining environment/Siren presentation, migration and broader native gameplay gates remain work. No merge, production release, live account mutation or service deployment.

## 2026-10-07 — Codex — Ward, controls and internet-room checkpoint (work continuing)
- Separate `codex/unity-conversion`, parent `000af4d8349646a42f2f97aec7da03678448053c`, pinned source `05950ea`. Owner's complete-conversion request remains active; this is an intermediate durable checkpoint.
- Ported weighted/FIFO horde entrances, source climb curves and vent travel, Ward moving meshes and pit/fall/revive timing. Added six dive tracks, procedural night-sky shader port, settings, melee lock-on, spectator controls and replay in a separate unsimulated physics scene. Added local DEV practice tools, grant-management UI and account token refresh without live account mutations.
- Added a standalone .NET WebSocket relay with optional discovery/private rooms, bounded pipes and one-use pairing tickets, plus headless Unity authority source. Original Electron networking/services remain separate. Player-host migration is still absent.
- Actual checks: original entry fixtures (241 climbs / 6 timelines / 96 weights), managed/source regressions, real two-client relay and TCP tests, real Unity API Runtime/Windows-player/Editor C# compilation, four package tests and source receipts pass. New collision/animation entry timings affect authority and replication; cinematic camera/replay changes are presentation-only.
- Not run: activated Unity import, shader compilation, rendered/PhysX acceptance, headless Unity runtime, live accounts, deployed relay and Windows EXE. Remaining cinematic/body/environment fidelity and native gates are recorded in the conversion status. No merge, updater publication, Site release or live service deployment.

## 2026-10-07 — Codex — broad native conversion checkpoint (work continuing)
- Isolated `codex/unity-conversion`, parent `e268aa85c160795d1860b082188cb5e12176bdd4`;
  gameplay/assets pinned to `05950eaef80ff2483a9c44d898cad98be1a76bbc`. Owner requested the
  complete conversion before the next playtest; this is a durable intermediate checkpoint, not acceptance.
- Added native projectile modifiers and the full Chip draft, summons, DEV fighters/Leet/Siren,
  cooperative waves/economy/recruits/boss phases, debris damage, wound classification and isolated
  PhysX cosmetic bodies, source model/texture export, 159 source motion clips including entrances,
  the 83 original audio files, account/profile requests with signed grants and Windows DPAPI,
  and conversion-only native LAN rooms with host authority, input/state replication and reconnects.
- Fixed source-parity issues found during integration: double blast falloff on adds, horde limb
  shares, sustained-fire release, starting-overlap ability contacts and spawn bot-count truncation.
  New combat systems change hit timing, damage/procs, movement, knockdowns and native network state.
  Existing first-person base-pose isolation and native staff sockets are retained.
- Passed locally: managed regression/animation/parity suite; 24 PvE scale cases, six complete source
  schedules, 72 body-wound cases, 36 horde hit splits, 80 deterministic PRNG values, 1,001 music
  positions; debris contact caps; strict frame/UTF-8/decompression checks; two real loopback clients,
  ordered delivery, disconnect/reconnect and malformed-protocol rejection. Four dependency tests pass.
  Runtime, Windows player and Editor C# compile against actual Unity 6000.3.24f1 assemblies.
- Native Editor/import/shader/PhysX/rendered tests and Windows EXE build remain unrun. New asset
  import gates run in the Windows builder. Four large original MP3s are reused by Git blob SHA
  during this GitData push; the source-package gate in CI must check all 83 files and receipts.
- Remaining work includes vent-pit/entry presentation, cinematic/environment detail and remaining
  source presentation adaptations, internet relay/discovery/host migration, admin/account UI parity,
  and expanded native integration gates. LAN/account functionality has not been tested live.
  Some cinematic/rig/physics effects are native approximations, not proven visual parity.
- Branch only; not merged, released, sent to the Electron updater, or published to the Site.

## 2026-10-07 — Codex — native melee E specials and recovery
- Separate `codex/unity-conversion`, base `34b0375c1b4817a5c72cd789a0ce8415cfeef377`; source remains `05950ea`.
  Checked branch/source heads, shared policy/state/log and open PRs; no overlapping conversion writer found.
- Ported Sword launcher, ballistic Dagger throw/reflection, returning Shield and Greatsword leap/direct strike/real-landing
  slam. Native scene sphere sweeps include starting overlaps; shield retrieval cannot damage or refund resources. Special
  stamina/cooldowns, one shared slam target ledger, camera aim convergence, owner cleanup and total projectile cap are included.
- Added standard-melee knockdown, one 0.22 s pickup window, two early airborne follow-ups, protected ground recovery and
  latched directional roll away. Shield primary now launches. HUD and same-weapon bots exercise the new moves.
- Added source special envelopes, adapted native joints/grips/recovery poses, visible thrown weapons, glow and voxel slam dust.
  First-person Greatsword arms stay braced; knockdown temporarily shows third person and restores the saved preference.
  Base-pose history remains separate from procedural layers to preserve the first-person feedback fix.
- Authority impact: new E hit timing/damage/cooldowns and launch/recovery movement/protection replace missing behavior;
  base weapon values and collider dimensions remain unchanged. Native head detection and recovery planting remain adaptations.
  No new network/service behavior; DEV juggle, PvE revive, physical ragdolls and special Chip handlers remain pending.
- Passed: nine-blob source fixtures (96 start gates, 324 strike filters, 216 recovery samples, 36 ballistic steps, 24 ceiling
  cases, 108 special envelopes), 2,400 finite/repeatable native poses, inherited managed/animation/parity checks, real Unity
  6000.3.24f1 Runtime/Editor/Windows-conditional compilation, GUID/dependency checks and four package regressions.
- Added Windows gates for actual E/contact/overlap/reflection/return/recovery handlers, special repeat-frame playback and
  thrown-shield glow lifecycle. These gates compile but are NOT executed here; activated Editor, rendered gameplay/PhysX
  acceptance and the Windows EXE build remain unrun. The deliverable is a separate source ZIP with the existing Windows builder/CA icon.
- Status: isolated unmerged checkpoint; no updater publication. Next: owner tests the four E moves, cover, blocking, pickup
  and roll recovery in first/third person; continue remaining combat modifiers/Chip handlers and production presentation.

## 2026-10-07 — Codex — native melee primaries, riposte and impulses
- Separate `codex/unity-conversion`, base `fc87b9510de9fd49eb5b67863be99fd0b06655a7`; original source remains `05950ea`.
  Checked current branch/source, shared policy/state/log and open PRs; no overlapping conversion writer found.
- Replaced the one-frame melee cone with source active sweeps, per-target contact ledgers and native scene-aware line of sight.
  Ported attack footwork, human timed-block riposte/rebound, source stagger/escape rules and early/late human primary roll-cancel.
  Blocked or unfunded counter attempts retain protection. Captured block timing now uses the actual block start.
- Corrected shared native external impulses: velocity decay, capped horizontal speed, source counter-steering and one-shot lift,
  replacing the previous extra dt*3.1 scaling. Added same-weapon bots and riposte feedback for focused owner tests.
- Authority impacts: actual contact timing, footwork, interruption/stagger, roll refund and knockback behavior change toward source.
  Base weapon damage/cost/cooldown values, collider dimensions and inherited wire protocol are unchanged. Native online play remains absent.
- Passed: independently exported six-blob source fixtures (64 swings / 448 timing samples / 5,376 contacts / 90 block cases / nine
  external-motion cases), contact ledger/boundary checks and inherited motion/ability/content/animation/managed checks. Runtime,
  Editor and Windows-conditional Unity 6000.3.24f1 compilation, package structure and four package regressions pass.
- Added native preview-scene contact/defense/impulse gates to the Windows builder; compiled but NOT executed here. No activated
  Editor, GPU/PhysX gameplay or Windows EXE test ran. Full source controller/collision response and visual fidelity are not established.
- Source Shield E is now a throw; the inactive old held-guard branch was not introduced. Melee E moves, Shield knockdown,
  launch/juggle/recovery, special Chips and the broader conversion remain pending. Shield retains a grounded stagger in this checkpoint.
- Status: isolated unmerged source checkpoint; no updater publication. Next: owner melee/knockback acceptance, then E specials/recovery.

## 2026-10-07 — Codex — first-person stability and aiming conversion
- Separate `codex/unity-conversion`, base `6d23fdfb1fe1aaa76da2e07e05a851c59d747a64`; original reference `05950ea` remains unchanged.
  Owner reported vibrating first person and requested continued conversion. Read current branch/source heads, shared policy/state/log and open PRs.
- Reproduced pose feedback: five renders at one timestamp moved the casting wrist 3.43 cm in rig space. Store only the captured
  base layer in history before procedural casting/recoil/IK/torso settling. Add a continuous, bounded visual clock with pause/reset handling.
- Move camera/aim-ray/render work to LateUpdate; camera and copied render snapshots read interpolated Rigidbody transforms.
  Simulation, hitboxes, muzzles, damage, hit timings, cooldowns, movement solver and network protocol are unchanged. Aim targets now
  come from the final rendered camera. No manual interpolation call is made on Unity's automatically interpolated default scene.
- Continued presentation port: source ADS spring/offsets/FOV counter-scale, smoothly aligned sights, circular rifle scope/reticle,
  and no floating hand-glow while the scoped model is hidden. The source HUD/CSS blobs are pinned; source sway/bob remains pending.
- Passed: 960 repeated-frame joint checks, 1,787 render-clock samples at 30–240 Hz, ADS/scope/reset checks, existing 5,808 staff projections,
  1,728 source phrase / 44 ability cases, eight native rule groups, 35 inherited managed checks and 34 clips / 3,384 poses.
  Runtime/Editor/Windows-conditional compilation against 6000.3.24f1, four package regressions and GUID/package validation pass.
- Added unrun Windows gates for actual AnimationClip repeat-frame stability and render-snapshot authority isolation. Native Unity
  execution, camera interpolation/frame pacing, scope/lighting visuals, PhysX gameplay and the EXE build were NOT run here.
- Status: unmerged separate source checkpoint, no updater publication. Next: owner builds/tests idle/casting/movement/guard,
  pause/resume/V, rifle Q/reload/roll and alternate display rates. Continue remaining combat/Chip and visual conversion after feedback.

## 2026-10-07 — Codex — native staff framing and combat glow
- Continued the separate `codex/unity-conversion` from remote `8f2cd601088c71f26686b2b5f0a9573ad4d86500`; original reference remains `05950ea`.
  Reviewed Claude’s 2026-10-04 log and first-person staff tests/source after the owner reported the old staff issue and missing bullet glow.
- Ported staff 0.85 scale, 20-degree outward roll, torso settling, source shaft/head proportions and elemental silhouettes.
  Added damped guard blending/chest-space orientation, with native-only view lift and guard-transition dip to keep the reticle clear.
- Added batched additive projectile/armor halos and trails, surface-attached impact marks with full 0.85 s lifetime, seven reused
  spill lights, soft field/explosion fills, and saved Full/Low/Off controls. Native events retain a full second instead of dropping
  live bursts by count. The retained lab protocol and original game/project are unchanged.
- This pass changes presentation/event metadata only: no hit timing, hitboxes, damage, cooldowns, movement or authoritative muzzle
  change; no network/service/account/save migration. Staff geometry is visual. Native ability shot IDs now share the primary ID source.
- Passed locally: 5,808 native staff mesh projections (zero crosshair intersections), guard/clock/glow/trail regressions, 1,728 original
  magic phrase cases, 44 ability-rule cases, eight prior native rule groups, 35 inherited managed checks and 34 clips / 3,384 poses.
  Five additional source blobs verified. Runtime/Editor/Windows-conditional C# against Unity 6000.3.24f1, four package tests and GUID/package checks pass.
- Added Windows build gates for NativeGlow shader errors and native effect lifecycle (200 impacts, primary/ability traces, seven
  lights, expiry/reset). These native gates, actual shader rendering, PhysX gameplay and EXE build were NOT run in this Linux workspace.
- Status: separate conversion branch/source checkpoint; no merge or updater publication. The accepted lab and original game are untouched.
  Next: owner builds in activated 6000.3.24f1, checks staff primary/Q/E/guard and glow settings near cover; then continue combat/Chip mechanics
  and remaining scene/presentation parity. Exact pending scope and visual adaptations remain in CONVERSION-STATUS.md / VALIDATION.md.

## 2026-10-07 — Codex — native first-person, magic poses, lighting and standard abilities
- Continued `codex/unity-conversion` from remote `0ce87bc7e4a58bc0150aef3daf3ef11d2a8e149b`; shared source remains `05950ea`.
  Owner requested continued conversion and restoration of the first-person toggle; prior magic/lighting feedback is included.
- Restored saved V/menu first-person selection, source camera dimensions and dedicated forearm/weapon pass, with ADS sight
  alignment. Magic now carries/guards with the left-hand staff and casts with source element-specific primary/Q/E phrases.
  Added source-based gun ability gestures, moon/rim scene lights, fog/shadows and wood/metal voxel trim.
- Implemented eleven standard gun/magic Q/E handlers, mana/cooldowns, release spending, buffering/cancellation,
  hold-to-preview ground targeting, bounded swept shots, grenade fuse/bounce, rocket arming, orbital tracking, pierce,
  slow/heal, blink clearance, timed fire/geyser/orb/black-hole fields and basic effects. The 89-entry stat draft stays filtered.
- Presentation leaves authoritative hitboxes/muzzles and melee timings alone. Native ability combat is newly implemented;
  damage/status/impulse integration needs Windows comparison. Existing primary casts now record their animation timestamp
  and cancel on accepted defensive interruption. No native multiplayer or production service/save change is included.
- Passed: 1,728 original-source magic samples, 44 ability-rule cases, input/gating/camera/staff checks, eight previous native
  rule groups, 35 inherited managed checks and 34 clips / 3,384 poses. Eleven reference blobs match the source tree.
  Runtime/Editor/Windows-conditional C# compilation against actual 6000.3.24f1 assemblies, four package regressions and
  source/GUID checks pass. Reproducible fixture exporter and exact remaining scope are committed with the project.
- Not run: native Editor/shader import/lighting, first-person visual playtest, ability PhysX contacts, Windows build/EXE.
  Next: build the new full source package in 6000.3.24f1, test V/ADS/reload and all Q/E against targets/cover, then continue
  melee/contact parity, special Chips, DEV/PvE and production presentation. Full conversion is still incomplete.
- Status: separate conversion checkpoint, not merged or released through the updater; accepted lab/shared game preserved.

## 2026-10-07 — Codex — repair native conversion Edit-mode build gate
- Continued isolated `codex/unity-conversion` from `f811738`; remote head and open PRs verified, no overlapping conversion writer found.
- Owner's 6000.3.24f1 Windows log confirms all eight native rule groups pass, then stops at the build check's
  Play-mode-only `SceneManager.CreateScene` call. No map simulation or EXE success was observed.
- Replaced scene setup/cleanup with public Editor preview-scene APIs; require a valid non-default PhysicsScene,
  confirm a falling probe advances, retain all map/capsule/spawn checks, and close the preview even if disposal throws.
  Per-map results identify the next failing layout. Gameplay, animation timing, hitboxes, damage, cooldowns and networking are unchanged.
- Validation: Editor C# compilation against actual 6000.3.24f1 APIs and source/package checks; native execution of the repair
  remains unrun here. Updated VALIDATION with the owner's evidence, exact cause, API references and remaining acceptance.
- Next: extract the corrected full project into its own `ChipArenaConversion` folder and rerun Build-Windows.cmd.
  The observed path was named `ChipArenaPrototype`; that name does not cause this exception, but keep the conversion
  project separate from the accepted lab. Full conversion remains incomplete; no merge or updater publication.

## 2026-10-07 — Codex — native Unity conversion foundation (incomplete full port)
- Owner accepted the ranged lab and requested full conversion, kept separate; explicitly selected native Unity/C#
  including gameplay/physics. New branch `codex/unity-conversion` starts at `f1207e65b9ba3db39239b7996c8b516fa58bd775`.
  Current shared game source verified unchanged at `05950ea`. No overlapping conversion PR was found.
- Created independent `unity/ChipArenaConversion`, product/save/executable identity and source CI. Accepted
  `unity/ChipArenaPrototype`, shared game, service endpoints and updater feeds remain separate.
- Added checked source export: 108 exact blob receipts; 15 weapons, 236 Chips, 14 Q/E definitions, 15 maps / 3,451
  collision blocks, source-derived fixtures. Native C# stats, eligibility/drafts, roll profiles, hazard clocks and
  round/set/draft flow. A wider hashed source/media inventory and explicit CONVERSION-STATUS track the remaining port.
- Native runtime milestone: Unity rigid bodies/capsules, rotated map colliders, dynamic props/support release,
  basic 11-standard-weapon primaries, elemental effects, hazards, local FFA/teams/bots and 89 implemented stat Chips.
  This is a new gameplay authority; timing/contact/physics/network parity with the production game is NOT established.
  No Node/Rapier authority is embedded. DEV weapons, Q/E handlers, 147 other Chip entries, Siren/PvE, production
  presentation, new multiplayer, accounts and cosmetics remain unfinished and are not represented as completed.
- Checks: 60 source parity cases / 4,620 stats plus offer/eligibility/roll equality; 20 hazard schedules; native
  match regressions including final win/rematch/skipped-bot awards; inherited 35 managed lab tests / captured and ranged poses;
  Unity API compilations and package checks. Source export reproduces identical content/fixture hashes.
  Native lava volume checks prevent duplicate channel damage (an intentional source difference); the original game is unchanged.
  The inherited tests do not exercise the new PhysX authority. No native Editor/GPU/Windows playtest ran here.
- Added a build-time native gate for all 15 maps, 120 spawn-floor checks and capsule settling, alongside the prior
  shader/clip/audio/icon checks. Implemented but unexecuted in this workspace. Next: activated Windows build/playtest
  of movement/ramps/collision/dynamic supports/round reset before relying on this physics base; then follow the
  remaining conversion sequence. Full conversion is in progress, not complete or released. No merge/publication.

## 2026-10-07 — Codex — isolated Unity ranged weapons test
- Owner accepted the animation test and requested ranged weapons. Continued `codex/unity-prototype`
  from `7d3126d4797f0848a281e73a2d744f64aeafce54`; shared source is still `05950ea`.
- Added Pistol/Rifle/Machine Gun primary and Q-aim constants, ammo/reload/switch rules, gravity/zeroing,
  swept body/head hits, muzzle obstruction, models, procedural shoulder/recoil/reload poses, animated magazines,
  flashes/tracers and six byte-identical original WAVs. Source receipts are in tools/ranged-provenance.json.
- Added weapon controls/HUD, shoulder camera, hit receipts, mixed gun bots, stationary targets and gun viewer sets.
  RANGED.md identifies preserved values and simplified rules/geometry; E specials and Chip modifiers are omitted.
- New gun hit volumes/damage, fire/reload cooldowns and ADS movement rules affect the Unity authority only.
  The bounded LAN protocol changes from CAP1 to CAP2 for loadout/aim/ammo/reload/projectile state; both players
  must update. Existing Sword combat regressions pass; no production game, feed or account-system edits.
- Checks passed: 35 managed combat/network regressions (including real TCP gun fire/reload and 192 ranged bots),
  909 gun pose samples, unchanged 34 clips / 3,384 captured pose checks, four package regressions and source checks.
  Runtime, Editor and Windows-conditional scripts compile against real Unity 6000.3.24f1 APIs with warnings as errors.
  Exact original sound blob hashes verified; the Windows builder now checks AudioClip imports as well.
- Not run here: native Unity import/graphics/audio, Windows EXE build, camera/pose visual acceptance and two-window
  LAN gameplay. Next: apply the source patch, rebuild with Build-Windows.cmd, and run RANGED.md's gunplay steps.
- Status: isolated branch source update; patch/full source deliverables. No merge, release or feed publication.

## 2026-10-07 — Codex — enhanced isolated Unity player-animation test
- Continued `codex/unity-prototype` from `14e9ba9dff63a7cd70a2ed8a4ddd28db5f180f4f` at owner request.
  Reused exact committed animation data from shared source `05950eaef80ff2483a9c44d898cad98be1a76bbc`;
  no overlapping Unity PR/writer found. Original Electron game and update feeds remain separate.
- Imported 34 encoded Explosive-derived clips with strict source-blob checks/provenance. Added source rig-v2
  proportions/plain-box body, chest-space arm IK conversion, native Unity legacy AnimationClip baking/sampling,
  sword idle/movement/cuts/block/stagger, travel-aware feet, adapted roll and blade/shell floor guards.
- Added a close-up four-weapon viewer (orbit, clip selection, pause, quarter/half speed). Daggers/greatsword/shield
  and block-impact captures are viewer-only. Original test poses remain selectable; HUD/CSV identify animation
  mode and pose CPU time. New geometry is also heavier, so this is not an isolated animation-only cost comparison.
- Combat hit timing, hitboxes, damage, cooldowns, stamina, movement authority and network protocol are unchanged.
  Cut contact marks map to the existing sword window. No root-motion authority or character colliders were added.
- Actual checks: 34 clips / 3,384 sampled poses (normalization, bone lengths, floor constraints, loop/timing,
  wrist/reflection/roll, corrupt data), all 22 combat/LAN regressions, four package regressions and source validation pass.
  Runtime/Editor/Windows-conditional scripts compile against actual Unity 6000.3.24f1 APIs with warnings as errors.
  Offline pose geometry reviewed for four states in all four sets; this is not a Unity render/playtest.
- Added a native 102-pose curve-binding validation gate to the Editor checks and Windows builder; not executed here.
  The owner confirmed the prior visibility repair, but this enhanced version still needs activated Windows import,
  build, visual acceptance and host/guest checks. Full production foot plants, blade/body clearance, recovery wrist
  limiter, armor sculpture/materials, cosmetics, maps and the ability catalog remain outside the slice (ANIMATIONS.md).
- Deliverable: source patch/full source archive on this isolated branch; no Windows EXE built here, no merge or feed publication.

## 2026-10-06 — Codex — Unity invisible characters/projectiles follow-up
- Owner reports both characters and projectiles invisible. Continued `codex/unity-prototype` from
  `e21fe19b1b05ef8469b553c5b2444ece2d92e3b9`. No screenshot/runtime log was supplied; Editor versus
  player context is unconfirmed. Both types share the runtime GPU-instanced voxel draw path.
- Found missing build-time retention for runtime-only instanced materials. Unity documents that
  scene-based stripping can remove these shader variants. Prepare and an IPreprocessBuildWithReport
  guard now set/read-check GraphicsSettings Instancing Variants=Keep All; shader target is explicit 3.5.
  This is the leading hypothesis, not a reproduced native/GPU diagnosis.
- Added F9/Escape-menu compatibility rendering using individual draws and non-instancing materials,
  with automatic fallback after instanced drawing exceptions and renderer/device logging. HUD/CSV
  identify the drawing path; manual switches cancel active captures. High-count compatibility mode
  costs more draw calls, so benchmark comparisons must use the same renderer mode.
- Checks: runtime, Editor and Windows-conditional C# compile against the official 6000.3.24f1 managed
  APIs, warnings as errors. Package regressions and source/patch ZIP checks pass. Shader compilation,
  native callback execution, variant retention and both visual paths remain unrun in this workspace.
- Visual/build changes only; combat timing, animation poses, hitboxes, damage, movement and network
  behavior are unchanged. CA icon and the prior package fix remain. No shared game or feed changes.
- Handoff: apply the four-file visibility patch to the existing prototype, rebuild with 6000.3.24f1,
  check actors/bolts, then F9 if needed. If still failing, obtain Player.log (or Editor Console) and
  a screenshot. Status: isolated test branch only; no claim of visually verified repair or release.

## 2026-10-06 — Codex — Repair Unity package-resolution blocker
- Owner supplied the first Windows batch-build log. Continued `codex/unity-prototype` from
  `1b091e5795444b6a72ff2796138e3af1a783c3ed`. The log runs 6000.3.24f1, resolves a Personal
  entitlement, then exits 1 on invalid package dependencies before the project build method runs.
- Removed the author's invalid `com.unity.modules.inputlegacy` manifest entry. Other failing names
  in the owner's local log (physicscore2d, tetgen, timelinefoundation) were not in the committed
  manifest; their local origin is unverified. No license or Editor-path fix is inferred from this log.
- Extracted the pinned official Editor's built-in package metadata. All six remaining dependencies
  exist at 1.0.0 and declare no transitive package dependencies. Added a versioned module catalog
  and package checks that validate actual names/versions rather than only a naming prefix.
  Four regression tests pass, including rejection of all four names in the observed failure.
  Source structure and ZIP integrity checks pass; branch CI includes the package regressions.
- Recovery: fresh source extraction, open/import with 6000.3.24f1, close Editor, then build; do not
  carry an old Library or package lock across versions. README records an alternative in-place repair.
  Gameplay, animation, timing, damage, hitboxes, movement, networking and the CA icon are unchanged.
- Status: fixed on isolated Unity branch, not merged or game-feed released. Successful Unity import,
  shaders, Windows player build/icon embedding and gameplay remain unverified. Owner must rerun the
  corrected source in the activated Editor and share the next build log if it fails.

## 2026-10-06 — Codex — Unity Windows EXE icon and build handoff
- Owner requests a Windows EXE and stylized voxel CA launcher icon. Continued isolated branch
  `codex/unity-prototype` from `3739884e1d2ecbb235f7497696638ec2bfb0a9ca`; no shared-game or feed changes.
- Added original generated transparent RGBA CA icon, importer metadata and generation provenance.
  Editor preparation fills/read-checks default and standalone icon slots using Unity's supported API.
  Windows packaging now rejects missing UnityPlayer.dll/game data; the command clears stale logs,
  explains activation/support requirements and opens the player folder only after build success.
- Validation: updated Editor helper compiles with C# 9 against actual Unity 6000.3.24f1 managed APIs,
  warnings as errors. PNG decoding/alpha and source structure/package integrity checks pass.
  No gameplay, animation, hit timing, hitboxes, movement, damage or network code changed.
- Blocker: no activated Unity Editor in this authoring environment. No EXE was produced; Windows
  player compilation, icon embedding/Explorer appearance, CMD execution and gameplay remain unrun.
  Next: activate the pinned Editor via Hub on Windows, install Windows Build Support, open/import
  the project, close it, and run Build-Windows.cmd (or use the Editor's Build Windows TEST ZIP menu).
- Status: source and icon implemented on the Unity test branch; not merged, no game-feed release.

## 2026-10-06 — Codex — Isolated Unity engine-evaluation prototype
- Owner requests a separate Unity test branch and delivery. Branch `codex/unity-prototype`, shared base
  `05950eaef80ff2483a9c44d898cad98be1a76bbc` (tree `1984514e15f96473fda5d56fde20c0ccc1401a0f`).
  The source overlay was prepared in an isolated directory and is added to that exact remote base tree.
- Added self-contained `unity/ChipArenaPrototype`, pinned to Unity 6.3 LTS 6000.3.24f1. Includes one arena,
  source-grounded movement/sword/block/roll constants, simplified combat, procedural instanced voxel poses,
  energy bolts, breakable cover, decorative PhysX debris, 0–192-bot workloads, frame-time/CSV measurements,
  and a distinct 2-player trusted-LAN host-authoritative test protocol (no prediction or production integration).
- Existing gameplay, hitboxes, timing, animation, saves, launcher and network protocols are untouched.
  Prototype behavior differs deliberately; README and PROTOTYPE-SCOPE list omissions. Purchased animation
  assets and full Siren/Chip/round systems are not included. Built-in render pipeline; native pack/rig evaluation is next-stage.
- Validation: 22 real managed-core checks pass, including 192 bots/600 ticks and a real TCP loopback
  input/authority/snapshot/disconnect exchange. Core compiles with Roslyn from .NET SDK 8.0.425.
  Runtime, separate Editor assembly, and standalone-Windows conditional runtime all compile against the
  official Unity 6000.3.24f1/.NET Standard 2.1 assemblies using C# 9, warnings as errors. No Unity stubs.
  Fixed the build helper's CompressionLevel ambiguity found by the real API check.
- Unity Editor import/play, shader compilation, visual/PhysX acceptance, two actual Unity windows and a
  compiled Windows player remain NOT RUN: this environment has no activated Editor. Managed API compilation
  is not a Unity build. No FPS uplift or production-parity claim. See project VALIDATION.md.
- Added local Editor menu/Windows command to validate and build a separate TEST ZIP. Isolated branch-only CI
  checks the core and packages a private **source** artifact; it uses read-only contents and no Unity credentials.
  No existing workflow is altered. Status: implemented on test branch; not merged or published to any game feed.
  Next: owner opens the source project in Unity Hub, runs the supplied Editor checks, Play and Windows build,
  then evaluates equal-load performance and one licensed native animation pack before broader migration.

## 2026-10-06 — Codex — Clear surviving enemies at wave completion
- Branch `codex/wave-end-cleanup`, shared base `9028309412355e3cb978609d4776970c050900bf`, exact tree
  `e885503d2a52068ab0d4c4896784566da13c1ab7`. Owner requests cleanup excluding bosses and publication to Update.
  Recovered an isolated checkout from the local source after its old parent Git metadata expired; every tracked blob,
  file mode and the resulting tree match the current remote before edits. No other open task overlaps this fix.
- Every normal, timed-out or debug-skipped wave now invokes horde-only cleanup before intermission rewards/respawns.
  Active/exposed entering adds die through existing cleanup handling; pending/unexposed arrivals and the spawn budget
  are cancelled. Bosses, humans, party bots, recruits and friendly summons are outside that registry and are untouched.
  Boss-phase transitions retain their existing horde behavior. Wave bonuses, respawns, timings, hitboxes, ordinary damage,
  cooldowns, movement, animations, saves and network formats are unchanged; deaths/snapshots now reflect the cleanup.
- Checks (Linux, Node 24.19.0/npm 11.9.0): client types pass; 49 affected director/horde/flow/boss/possession/render tests
  pass. Real-Rapier coverage verifies active/exposed/queued cleanup, surviving boss body/HP and friendly summon, no delayed
  spawns during intermission, and reuse of the same entry next wave. Natural/timeout/debug clears are covered.
  Extended PvE multiplayer passes: both clients observe live adds going from 2 to 0 at wave clear, 853 matching shared
  ticks, reconnect and victory; raw snapshot p95 34,708 B. All 10 updater channel/menu-status fixtures pass.
- Full verify units: 1307 total, 1293 pass, 13 fail, one skipped: the 12 known baseline names plus the eight-client ping
  timing check under full-suite load. That check passes alone (80 identical snapshots, ping p95 6.61 ms). An incidental
  standalone party-relay rerun hit its previously observed 403-versus-200 flake; it passes in this full verify run.
  Full `npm run verify` completes with those unit failures, four known server TypeScript errors and the existing
  WebSocket rematch timeout; HTTPS integration passes (3418 matching snapshots, rematch and guest leave).
- Two complete client/relay release builds produce byte-identical manifests and payloads for candidate
  `0.2.e337f51c003d`, game build `49ca456f76647ac42ef59970`. Both actual updater payloads pass installation,
  all 103 game-file hashes (107 including launcher), launcher activation, same-version skip, settings preservation,
  offline/corrupt refusal and source-privacy checks. Schema 1: 24,708,289 B; schema 2: 20,578,756 B.
  Existing TEST-only launcher wrappers are preserved, extracted from the previous verified pack with matching hashes.
  `git diff --check` passes.
- Release: owner authorized merge/publication. PR #57 merged at `a0167bece51e21f465b1cdf1fbbc0e0bd948820d`;
  its complete tree `c6882b17ec7f04ee80f5b07ce14f6da747c70227` matches the tested checkout exactly.
  TEST channel `5ddab5b780e87eb9f8d9ab3e6d309e2f784f1125` publishes `0.2.e337f51c003d`
  (successful run `37526867343`). Public Git blob hashes of both manifests and payloads match the verified local files.
  The temporary assembly branch deleted itself; no publishing workflow/chunks remain on channel main.
- Actual network updater checks pass for both schemas from `0.2.b18d54478b46` to `0.2.e337f51c003d`: all 103 installed
  game-file hashes match, settings survive, schema-2 launcher activation succeeds, and a second update skips. The actual
  menu status reports version `0.2.e337f51c003d` and build `49ca456f76647ac42ef59970` after installation.
  Release receipt branch: `codex/wave-clear-release-receipt`. Native Windows/GPU and manual gameplay not run;
  baseline verification failures remain as detailed above. No live-feed, Site or Windows seed release.

## 2026-10-06 — Codex — Persistent impact effects, projectile procs and a much larger Siren hunt
- Branch `codex/impact-lifetimes-siren-horde`, shared base `c52a532ad2763251570fc96d43cf88833b74758c`;
  local base checked against the entire remote Git tree. Owner requests no impact cancellation, a huge Find-me horde,
  and impact effects on added projectiles; owner chose each eligible projectile over the old once-per-volley fields.
- Impact Chip fields no longer evict older fields at the per-owner/kind or global limit. Delayed bombs and L44T Nuke marks
  finish normally. Client blast/spark buffers grow while retaining existing particles; beam/impact-mark/Nuke visual limits
  no longer discard active effects. Natural expiry and round/reset cleanup remain, and particle buffers shrink on reset.
  Supplementary point-light pools and separately balanced Q/E ability-cast limits remain unchanged.
- Each primary projectile, Blade Runner, Firework fragment and L44T Copy/Paste shot can proc impact effects on players,
  cover and minions. Described exclusions (Satellite Office, Rear Guard, Four-Way Stop, Double Helix and Splitter),
  reflections, nonrecursive fragments/copies, cooldowns and explicit once-per-attack Shrapnel/Vacuum rules are retained.
  Full and simplified card descriptions explain the rule/exclusions. Weapon damage/DoTs, attack timing/hitboxes,
  cooldowns and movement are unchanged; more independent impact effects intentionally increase aggregate damage/control.
- Siren possession spawn rate and living targets increase fourfold: 72/120/168/192 adds at 12/20/28/36 per second for
  1/2/3/4 party members; relay caps are 72/112/112/112. Entry reservations increase only during possession. Rendering
  covers 192 adds + 16 corpses while keeping full-rig/shadow budgets. Other phases, hints, possessed HP and rewards retain
  their rules. Network/save formats are unchanged; larger authoritative snapshots/effect counts are intentional.
- Checks: Linux, Node 24.19.0/npm 11.9.0; client types and client/relay release build pass. 121 affected tests pass;
  final projectile/L44T/card checks pass 39/39. Full final unit run: 1306 total, 1293 pass, 12 fail, one skipped;
  failure names exactly match the untouched base. `npm run verify` retains four server TypeScript errors and WebSocket
  rematch timeout; its transient party-relay failure passes in the final full unit run. HTTPS integration passes
  (3439 matching snapshots/rematch), and PvE multiplayer passes (812 shared ticks, reconnect, victory, no disagreement).
  Chaos network check hits the old voxel-burst/impact-ragdoll cosmetics assertion identically on untouched base.
- Real-Rapier stress with 192 adds/16 corpses: 2.084 ms average sim step, 1.126 ms PvE share, 4.536 ms p99;
  snapshot p95 60,065 bytes, 208 add tuples, no dropped horde events. Original CPU and 64 KiB snapshot gates pass.
  All 10 remote-channel/menu-status updater fixtures pass. `git diff --check` passes.
- Windows/native GPU, real-browser visual/difficulty acceptance and remote human play not run. Heavy combinations can
  increase GPU/memory/simulation load; headless crowd results do not measure unlimited overlapping effects on Windows.
- Prepared TEST candidate `0.2.b18d54478b46`, game build `9df11a70d649891bf1a915a4`. Both real updater payloads pass:
  103 game files/107 with launcher, all installed hashes, launcher activation, same-version skip, offline/corrupt refusal,
  settings preservation and source-privacy checks. Schema 1 is 24,708,136 B; schema 2 is 20,575,267 B.
  Two complete release builds produced byte-identical manifests and payloads for both schemas.
- Release: owner approved merge/publication. PR #55 merged at `c146cd6a5fc724d0038915a1c91a313146d8addc`;
  its complete tree `29c193dc42d9aa1de955de06341f1de31c96e754` matches the tested checkout exactly.
  TEST channel `ecbbdd077a9db20db999a34668cba8c891ed4872` publishes the candidate above (run `37519393074`).
  Public Git blob hashes of both manifests and payloads match the verified local files. The temporary chunk-assembly
  branch deleted itself; no publishing workflow/chunks remain on channel main.
- Actual network updater checks pass for both schemas from `0.2.a06765ab0a43` to `0.2.b18d54478b46`: all 103 installed
  game files match, settings survive, schema-2 launcher activation succeeds, and a second update skips. The actual menu
  status handler advertises this build/version for both schemas. Status: merged and released to the existing TEST Update
  button. Native Windows/GPU/difficulty acceptance remains. No live-feed, Site or new Windows seed release.

## 2026-10-06 — Codex — PvP chip presentation for Siren personal picks
- Branch `codex/siren-pvp-chip-ui`, shared base `9de0365171d3b22404e2bd125f2634d971418dc5`
  (local checkout verified against its complete Git tree). Owner requests UI only and publication to the existing Update button.
- Siren's personal screen now reuses the PvP 3D pilot stage, hover look/reach, compact card tray and nameplate. A separate
  presentation snapshot feeds only DraftStage/DraftView; the arena, simulation, input, audio and networking keep the real
  fight snapshot. Personal offers/order, immediate pick command, queue, Choose later/C/Esc, number keys, visibility gates,
  damage warning and vulnerability are retained. No PvP turn order, ready gate, selection timer or post-pick hold is adopted.
- Both trays now fill the available width and distribute cards left to right instead of centering a capped-width group.
  Card widths stay readable; one offer stays centered; narrow/short screens retain horizontal scrolling. The personal tray
  reflows on resize, and an attack warning updates without moving or rebuilding the card buttons.
- Checks: Node 24.19.0/npm 11.9.0; client typecheck passes; 227 affected PvE/draft/card tests pass. Two new regressions
  exercise independent personal views through the real DraftStage without mutating fight state, and shared tray markup
  for 1/2/3/6/8/12 offers at five viewport sizes. Full unit suite: 1286 tests, 1273 pass, 12 failures matching the previous
  untouched-baseline names exactly, one skipped. No shared/server/relay/launcher code or gameplay rules changed.
- Native Windows/GPU, real-browser visual acceptance and remote-player manual play not run. Chromium's download failed
  (invalid/truncated archive); headless rig tests are not visual acceptance. Owner visual review remains.
- Release: PR #53 merged at `6e3da8e3f6f6f8e56891bab7077c505acee69b9a`; its complete tree
  `20463da03a359f3437e8d211aa00a10dac412db8` matches the tested checkout. Server compilation retains the four documented
  baseline errors; all 10 remote-channel/menu-status fixtures pass. Two release builds produced byte-identical manifests
  and payloads for `0.2.a06765ab0a43`, build `798dc25d1e90f4bc954933ac`. Real-payload checks pass for both schemas:
  103 game files (107 including launcher), all installed hashes, launcher activation, same-version skip, corruption/offline
  refusal, settings preservation and no source archive/maps. Schema 1: 24,707,221 B; schema 2: 20,576,951 B.
- Published atomically to TEST channel `b61a81fcb07f0e56ae51ef166367a61071313d09` (publishing run `37513170104`).
  Public Git blob hashes of all four feed files exactly match the verified local files. The temporary chunk-assembly
  branch deleted itself; no workflow or chunks were added to channel main. No live-feed, Site or new Windows seed release.
- After raw-GitHub cache propagation, the actual network updater passed for both schemas from `0.2.8829a9b74310`
  to this release: all 103 installed game hashes match, schema-2 launcher activation succeeds, settings survive, and a
  second run skips. The actual menu-status handler advertises this build/version for both schemas. Status: merged and
  released to the existing TEST Update button. Native visual review remains.

## 2026-10-05 — Codex — Attack recovery, cheaper swings and sprint stamina
- Branch `codex/combat-flow-stamina`, exact shared base `ab93263c704266949432f9ee32034b157921d720` (PR #50).
  Owner requests smoother attack endings/combos, lower attack stamina, sprint drain/visual, and all steps to the test Update button.
- Sword, shield and greatsword recover along a single quaternion arc between fitted contact/guard poses, avoiding late captured
  wrist regrips. Dagger recoveries limit abrupt wrist turns in chest space; the full spin stays intact. Combo transitions remember
  the final floor-corrected displayed pose. Ground clearance and the greatsword support grip are retained. Contact timing,
  hitboxes, damage, cooldowns, attack travel, first-person poses and DEV choreography are unchanged.
- Standard primary stamina: daggers 10→7, sword 18→14, shield 22→16, greatsword 26→20; Echo Strike 3→2 per extra hit.
  Specials/roll costs and tiny DEV jab costs remain unchanged. Only the three golden traces with standard melee costs change.
- Double-tap-W sprint retains 1.35x forward speed, costs 12 stamina/s, suppresses simultaneous regeneration, and falls back to
  jogging at zero. Exhaustion stays latched until W is released, preventing regen/sprint oscillation. Round/reset/respec clears it.
  Shared host/prediction helpers and `caps.move=2` synchronize stamina/exhaustion; old move-v1 hosts retain their free-sprint
  prediction. HUD adds an amber SPRINTING chevron/bar and a release-W exhaustion prompt (reduced motion respected).
- Checks: Node 24.19.0/npm 11.9.0, fresh `npm ci`, client types pass. Eight new tests cover recovery at 60/120 Hz and three
  attack rates, actual-pose combo continuity, drain/exhaustion/reset, old-host compatibility and delayed JSON snapshot replay.
  Existing captured-melee/greatsword animation tests pass; revised costs and all PvP golden traces pass (31 affected tests).
  Full `npm run verify` retains four known server TypeScript errors and WebSocket rematch timeout; HTTPS multiplayer passes.
  A movement-network run had a transient shared-snapshot mismatch; rerun passes (635 identical snapshots, 0.128 m P95 correction,
  <0.001 m instantaneous visual correction), and untouched-base comparison passes. Test-channel/status fixtures: 10/10 pass.
- Visual measurements (60 Hz, 1x attack rate): largest recovery/exit angular step sword cuts 29/32/34°→13/9/15°,
  dagger cuts 1–3 36/70/47°→25/18/22°. The dagger spin is preserved. These are rig measurements, not native visual acceptance.
  The previously recorded dagger spin→Echo Strike unwind remains; contact/anticipation is deliberately not time-filtered.
- Windows EXE/native GPU/manual gameplay not run (Linux container; Chromium download failed). No Site/live-feed publish or new seed ZIP.
  Final full unit run (`node --import tsx --test --test-concurrency=4 tests/*.test.ts`): 1271/1284 pass, 12 failures
  matching the untouched baseline names exactly, one skipped (baseline 1263/1276). The first unrestricted repeat exited early
  without a summary; the bounded run completed.
- Release: PR #51 merged at `db58d1befb5c1c38871117f7117f31f48758d996`; its complete tree matches the tested checkout
  (`a6beb516700bf7cc86faa6585aecf73b975c04f5`). Two `release:prepare` builds produced byte-identical schema-1/schema-2
  manifests and payloads for `0.2.8829a9b74310`, game build `10e08a8dd84292d1164dc939`. PR #5 payload checks pass:
  all 103 game files (107 with launcher files), actual install/launcher activation, same-version skip, corruption/offline refusal,
  settings preservation, and no editable source/maps in public payloads. Sizes: schema 1 24,706,861 B; schema 2 20,576,024 B.
- Published both manifests and payloads atomically to the existing test feed at `b7307d60b91020d609a0f26e5b9c8082318b5777`.
  The connector's 16 MiB request cap required eight verified chunks and one temporary GitHub publishing job (run 37334499364):
  assembled bytes checked against exact SHA-256/length, refused a moved feed, then pushed feed-only main and deleted its staging
  branch. No publishing workflow/chunks were left on main; no source, secrets, saves, Windows seed or live Site/feed changed.
  Public Git blob hashes match all four local feed files exactly. After raw-GitHub cache propagation, the actual network
  updater passed for both schemas from previous `0.2.00f72f2f7eb8` to this release: all 103 installed game file hashes match,
  schema-2 launcher activation succeeds, settings sentinel survives, second run skips, and the actual remote menu-status handler
  advertises this build/version. Status: merged and released to the existing TEST Update button; Windows feel review remains.

## 2026-10-05 — Claude — Melee animations (sword, daggers, shield, greatsword) from the Explosive RPG Character pack
- Branch `claude/sleepy-ritchie-g8ruhv` from `claude/visual-revision` `0aa70dc` (PR #49 merge, plus its log commit
  `b19733a`). Owner requests 2026-10-05: "re-do the animations (Melee) with the ones from the pack" (the owner's licensed
  Explosive "RPG Character Mecanim Animation Pack", shared by Drive link; FBX/.meta files stay outside the repository), then
  "convert the greatsword as well".
- Change (presentation only): offline converter `scripts/mocap/convert-explosive.mjs` (+ `explosive-pack.mjs`: FBX 7.3 load,
  Z-up fix, 3ds Max biped → Mixamo bone names, T-pose from the Unity avatar in the .meta) writes the generated tables
  `src/client/mocap/{sword,daggers,shield,greatsword}-clips.ts`. Guard idle, walk and run carries, block, block impact and
  stagger come from the pack. Cuts: sword R1 forehand, R2 backhand, R6 high diagonal; daggers reverse grip in both fists, R1
  / L1 / R2 thrusts and stabs, the 4th L1 under the full-body spin; shield Attack3 lunge and Attack1 rising bash (the pack's
  left-arm shield read onto the game's right hand); greatsword (2Hand-Sword) high guard, Attack5 overhead forehand, Attack8
  flat backhand, both hands on the hilt (one-handed only in the run carry). The Mixamo greatsword converter stays as a
  documented way back.
- How the captures fit the game: each cut's [start, contact start, contact end, settled] marks retime onto the
  authoritative windup / hit window / recovery (contact always inside the hit window; sweep directions match the server's);
  windup pace keys (`MocapClip.pace`) spend the short game windups where the pose moves (daggers keep 35% of the captured
  pull-back, sword cut 2 60%). Cuts stand on the procedural phrase's footwork, the captured hips lowered just enough for the
  planted feet to reach the floor; guard, block and stagger keep the gait's feet under the captured hips (legs re-solved
  onto the gait's targets). The converter keeps the greatsword's two-handed hilt (fists, pommel, blade root) off the voxel
  head, pauldrons and torso and brings the support grip within reach. At runtime the blade clearance turns and lifts blades
  off the body and floor; a final floor guard runs only on grounded frames a captured layer posed.
- Review: two adversarial rounds. Round 1 (13 findings, all fixed): skating cut feet, loop-seam elbow pops, dagger wrist
  flips, block foot slides, follow-ups entering from the guard, floor-lift flips, among others. Round 2 (14 findings, 11
  fixed): greatsword hit/parry feet popping 13-15 cm and sinking 6-7 cm (now no worse than procedural), cut feet hovering up
  to 9 cm (now ≤ 2 cm), the greatsword run blade snapping ~1 rad each stride (carries now ≤ 0.02 rad per 1/960 s on every
  weapon), the backhand's 35 cm pre-contact blade drop and a recovery snap (cuts now continuous at 3840 Hz), fists 5-14 cm
  inside the head (Attack2 replaced by Attack5; fists ≥ 3 cm clear), the support hand 9 cm off the hilt in the guard (max
  5.9, mean 0.8 cm), the roll blend ignoring its slerp, the floor guard acting on specials and juggles, and the clearance
  cost (greatsword run 1.34 ms → 0.28 ms per rig-frame; broad phase per body part, covered carry layers skipped), and no
  test of the greatsword's sweep directions. Regression tests added for each. Not fixed (low): an Echo Strike follow-up
  after the dagger spin still unwinds ~150° in ~40 ms (the procedural phrases do the same); the converter's guard-heading
  shift tilts the chest up to ~6.5° where its hip/spine lean eases engage; no test drives an Echo Strike follow-up through
  a captured set.
- Impacts: hit timing, hitboxes, damage, cooldowns, movement rules, attack travel and network behaviour unchanged (client
  presentation only; nothing in src/shared or the server reads the clips). First-person view models and the procedural
  phrases (MOCAP off) unchanged. `rarity-combat` "dagger primaries and echoes alternate ..." (a baseline failure that asserts
  the two dagger cuts are exact arm mirrors) still fails with different numbers: the captured cuts are not mirror images.
- Known limits: the reverse-grip dagger turned up out of the floor in a 60-90 ms windup rotates up to ~1 rad within a
  millisecond, and sword cut 2's windup turns ~0.07 rad within 1/15360 s (both continuous, a flick at 60 fps; the sword's was
  0.25 before); the shield's procedural hit reaction over the captured guard dips an ankle 1.4 cm (as before this work).
- Checks: full unit suite under Node 24.19.0 (the portable runtime's major; the updater test needs it): 1276 tests, 1263
  pass, 12 fail, 1 skipped, the 12 failing names identical to the `b19733a` baseline; pvp-golden traces, melee-fluidity,
  locomotion-root-sync, reaction-fluidity, block-riposte and blocking pass in it. `melee-mocap` 16/16 and `greatsword-mocap`
  8/8, new this round: planted cut feet within 20 mm of the phrase's, hit/parry reactions no worse than procedural (no
  ankle under -0.82), carries < 0.04 rad per 1/960 s, the roll blend, floor-guard gating, greatsword sweep signs, greatsword
  cuts continuous 960 vs 3840 Hz with the fists out of the head, guard support hand < 8 cm (mean < 3.5 cm). check:types
  pass; server tsc only its 4 baseline errors. Lab strips (scratch) of both greatsword cuts, guard, run and block reviewed.
- Not run: native Windows look and feel, online play with remote players, owner review of the new style.
- Status: merged into `claude/visual-revision` at `ab93263` (PR #50; PR CI: unit 1276 with only the 12 known failures,
  server build 4 baseline errors, websocket only "Timed out: rematch", HTTPS passes). Published to the test channel (not
  the live feed or Site): test `0.2.00f72f2f7eb8` at channel `72dfa69`; schema 1 `arena-525848d69e26c0da.json.gz`
  24,703,508 B, schema 2 `arena-7f88f2a6261b3172.pack` 20,575,873 B. Candidate (`--test-only --channel`, Node 24.19.0,
  tooling 35ea65d): every stage passed except the baseline-only verify (the same 12 names, 4 server tsc errors, websocket
  rematch timeout); repeat build identical. Raw feed served the new manifests; old-launcher and v2 installs at
  `0.2.e07d5479254f` updated, then reported current; settings sentinel untouched; menu status `0.2.00f72f2f7eb8`.

## 2026-10-05 — Claude — Melee block riposte (blocking melee is worth it)
- Branch `claude/sleepy-ritchie-g8ruhv` from `claude/visual-revision` 6ad1516 (PR #48 merge). Owner request 2026-10-04:
  "Make blocking melee actually worth it, maybe longer stunned time? enough to get a hit or 2".
- Before: the blocker stayed locked in its own 0.30 s Block window and the attacker's 0.25-0.95 s stun often ended first;
  at a late block, a counter landed in 11 of 20 weapon pairings and a second hit in none.
- Change (`successfulBlock`, melee kind): when a human standard-melee blocker (Daggers, Sword, Shield, Greatsword) blocks
  and the attacker is not the Siren, the attacker's stun is `max(old stun, min(1.4, ready + 1.0 + 0.004 × blocked damage)
  + chip walk-back)` (`ready` = the blocker's attack cooldown + windup / attack rate; walk-back = the extra rebound of
  block-force chips over the base push, as distance / run speed; `BLOCK_PUNISH`, `meleeBlockPunish`). The Block window keeps
  running (guard, recoil and greatsword blockHit clip, protection from other hits) until the blocker's attack, special or
  roll actually starts (`riposteTry`; an action that cannot start keeps the window), mirrored in the predictor via the
  optional `riposteAt` view field. The blocker's first landed direct hit on that attacker ends the riposte stun
  (`settleRiposte`; an absorbed hit or another player's leaves it), and the riposte clears the attacker's stagger-escape
  immunity, so the counter's normal follow-up applies (Dagger pair stun, Shield knockdown, Sword/Greatsword stagger). The
  parried attacker's queued afterimage echoes spare the blocker (they still hit others). Bot, ranged and magic blockers and
  a Siren attacker keep the old stun (bot difficulty, boss fights and pvp-golden unchanged). Client: the parry stagger keeps
  its old pace (shaped on 0.95 s) and holds the opened guard, so a long stun never plays in slow motion.
- Measured (sim duel, block on the tick before the strike, counter walks into reach, then swings): every pairing keeps one
  free counter, two for Daggers and a Shield knockdown, up to a 0.65-0.75 s host-time reaction (0.65 s worst: Greatsword
  vs Greatsword), also with Bulwark (×1.8 block force). Repulsor (×3) loses it in 2 of 16 pairings (Sword or Shield
  attacker vs a Greatsword blocker): its ring-out trade. Stuns 1.15-1.4 s (1.8 s with Repulsor), but the counter ends them.
- Review: two adversarial rounds. Round 1 (window ended at impact): 9 confirmed findings, fixed by the v2 design (guard and
  recoil visible online, no echo or Emperor-jab holes, counters keep knockdown/stagger, only standard-melee blockers riposte,
  concrete-value tests). Round 2 (v2): stagger immunity or an absorbed counter freed the attacker early; block-force chips
  pushed the attacker out of reach; a failed attack/roll dropped the guard; echoes were deleted for everyone; the window was
  too tight for online delay. All five fixed, each covered by a test that fails without its fix. Round 3: one display-only
  finding (the counter's hit reaction kept the riposte end time), fixed with a test; no regressions found elsewhere.
- Impacts: hit timing, hitboxes, damage, knockback and cooldowns unchanged; the blocked attacker's stun length and the
  human melee blocker's ability to end its own window early change. Network: optional `riposteAt` snapshot field (old
  clients ignore it; old hosts never send it, so the predictor behaves as before); no new caps or inputs.
- Checks (Node 24.19.0, at cc0e9b6): check:types pass; server tsc only its 4 baseline errors (same lines as 6ad1516);
  block-riposte 7/7 (each review fix fails its test when disabled), blocking, reaction-fluidity 16/16, all pvp-golden traces
  identical; full unit suite 1258: 1245 pass, the 12 known failures, 1 skipped (as before); network block fails only its
  baseline "65.5 !== 77" (a projectile reflection check) and default only "Timed out: rematch", as on 6ad1516.
- Not run: native Windows feel and look of the riposte (guard hold, parry hold, counter), real online/relay latency (the
  1.0 s window is sized from the measured sim timings plus estimated delay), owner balance playtest.
- Status: merged into `claude/visual-revision` at `0aa70dc` (PR #49; PR CI: unit 1258 with only the 12 known failures,
  server build 4 baseline errors, websocket only "Timed out: rematch", HTTPS passes). Published to the test channel (not
  the live feed or Site): test `0.2.e07d5479254f` at channel `d67f3b6`; schema 1 `arena-5c6212e3dcbf4f0d.json.gz`
  24,647,325 B, schema 2 `arena-6f333439386ba6a2.pack` 20,539,120 B. Candidate (`--test-only --channel`, Node 24.19.0,
  tooling 35ea65d): every stage passed except the baseline-only verify (the same 12 names, 4 server tsc errors); repeat
  build identical. Raw feed served the new manifests; old-launcher and v2 installs at `0.2.6a229a16de6f` updated, then
  reported current; settings sentinel untouched; menu status `0.2.e07d5479254f`.

## 2026-10-04 — Claude — Double-tap-W sprint, Tab melee lock-on, roll-cancel of melee attacks, Escape menu board
- Branch `claude/sleepy-ritchie-g8ruhv` from `claude/visual-revision` d31fa25 (PR #47 merge). Owner requests 2026-10-04.
- Sprint: two W presses within 300 ms latch a held `Input.sprint` while W stays down (client `SprintTap`); shared rule
  (`movement.ts` `SPRINT`, `sprintIntent`) gives ×1.35 run speed when moving within ~53° of the aim, not while attacking,
  blocking, aiming, guarding, swinging, stunned, locked or swimming; works in the air. No stamina cost. Sent only to hosts
  with `caps.move` (and offline). Snapshot `sprinting` only when true; extra forward lean on the rig. Bots never sprint.
- Roll-cancel: a human pilot's fresh roll press cancels a live standard melee primary (sword, daggers, shield, greatsword;
  windup, sweep, recovery or Echo follow-ups) or an Emperor jab. No stamina refund; the attack cooldown is refunded to the roll
  duration only when the cancel comes before the first strike (`beforeStrike`; Echo follow-ups are marked and never refund).
  E specials, Emperor Electric, Siren moves, casts and reloads stay committed. Bots never cancel. The predictor mirrors it on
  `caps.move` hosts; the cancelled attack's pose ends (`rollCancelled`) so it never resumes after the roll.
- Lock-on (client only, `src/client/melee-lock.ts`): for a melee pilot alive in a fight, Tab locks the nearest enemy in view
  (16 m, 50° cone; players, PvE adds, enemy summons, Slenderman; never allies or the hidden boss); a tap cycles clockwise,
  holding 0.4 s releases; the lock ends when the target dies, disappears or passes 24 m, or the pilot stops being eligible.
  Yaw/pitch ease toward the target and mouse look pauses while locked; a reticle marks the target. Line of sight is required
  to lock or cycle; behind cover the lock stops steering and releases after 1 s. Otherwise Tab keeps the Builds board.
  Escape now shows the match menu with the player/party board beside it (stacked below it on windows ≤1180 px; not in the
  Dev Sandbox, whose panel sits there) (owner suggestion).
- Impacts: hit timing, hitboxes, damage and cooldowns unchanged for attacks that are not cancelled; a cancel before the strike
  deals no hit, mid-sweep loses the rest of the arc and Echo follow-ups; movement gains sprint for humans only. Network:
  optional `sprint` and `rollCancel` input flags, optional `sprinting` and `followup` snapshot fields, `HOST_CAPS.move`.
  The host cancels only for inputs carrying `rollCancel` (sent with Shift by new clients), so older clients keep today's
  behaviour on a new host. pvp-golden unchanged.
- Checks (Node 22.22.0): check:types pass; server tsc 4 baseline errors; sprint-rollcancel 15/15, melee-lock 5/5, all 6
  pvp-golden traces identical; full unit suite 1250, 13 failures = the 12 known names + the Node 22 portable-update test;
  network motion, party and HTTPS pass (block and emperor-network fail identically on d31fa25; default only "rematch").
  Two adversarial review rounds (4 areas): 15 findings fixed. Real-app (Vite dev server, headless Chromium): Tab lock turns
  the camera onto the dummy with the reticle, hold releases, death releases; sprint 6.8 → 9.18 m/s; greatsword roll-cancel
  clears the commitment (stamina 100 − 26 − 29); Escape menu + board at 1440 and stacked at 1100 px.
- Not run: native Windows feel (sprint lean, lock-on turn rate, roll-cancel blends), online/relay latency feel, bots sprinting.
- Status: merged into `claude/visual-revision` at `6ad1516` (PR #48; PR CI: unit 1250 with only the 12 known failures, server
  build 4 baseline errors, websocket only "Timed out: rematch", HTTPS passes). Published to the test channel (not the live
  feed or Site): test `0.2.6a229a16de6f` at channel `aac8363`; schema 1 `arena-25f71767d56ffd80.json.gz` 24,644,929 B,
  schema 2 `arena-7d91f8f13fbea198.pack` 20,536,649 B. Candidate (`--test-only --channel`, Node 24.19.0, tooling 35ea65d):
  every stage passed except the baseline-only verify; repeat build identical. Old-launcher and v2 installs at
  `0.2.57123b899145` updated, then reported current; settings sentinel untouched.

## 2026-10-04 — Claude — Siren PvE: much harder "Find me..." (more spawns), 30 s intermissions
- Branch `claude/sleepy-ritchie-g8ruhv` from `claude/visual-revision` 2ae916d (PR #46 merge). Owner requests 2026-10-04.
- "Find me..." (Siren phase 2): possession spawn rate ×1.8 → ×5 and alive ×1.6 → ×3, with a possession-only hard cap
  `HORDE.maxAlivePossession` 48 (waves and other phases keep 40, relay rooms 28): 18/30/42/48 adds alive at 3/5/7/9 per s
  for h=1..4 (was 10/16/22/29 at 1.08-3.24). 48 + 16 corpses fits the client's 64 impostors. Details: docs/PVE.md.
- Intermissions: 30 s each (were 12 s between waves and 8 s before the boss).
- Fixed (pre-existing, made common by the harder hunt): a party wipe during the hunt counted as "found" (Siren back at
  50%, chip-3 reveal events, possessed reward at defeat); a debug phase skip during the hunt left the add possessed.
- Impacts: PvE only. No change to hit timing, hitboxes, damage values, cooldowns, movement or PvP; pvp-golden unchanged.
  Network: up to 48 add tuples (+16 corpses) in phase 2 instead of 40; snapshot p95 50.4 KB at that worst case.
- Checks (Node 22.22.0): check:types pass; server tsc 4 baseline errors; pve-*.test.ts 203/203 (new pve-possession
  tests fail without their fixes); test:pve-perf ok at 48 adds; pve-flow 3/3; network pve passes (rawP95 33.3 KB);
  full unit suite 1231, 13 failures = the 12 known names + the Node 22 portable-update test.
  Before/after hunt matrix (72 hunts each): adds alive about 2×, downs 25 → 54, wipes 4 → 13, time to find unchanged.
- Not run: native Windows FPS at 48 adds, relay-hosted hunt, full `npm run test:pve-balance`. Owner playtest needed.
- Risks: phase 3 inherits the bigger field (median 26.5 adds when found, about 42 at h=4); kill income rises during the
  hunt, which still has no time limit (the add starts chasing at 60 s).
- Status: merged into `claude/visual-revision` at `d31fa25` (PR #47; PR CI: unit 1231 with only the 12 known failures, server
  build 4 baseline errors, websocket only "Timed out: rematch", HTTPS passes). Published to the test channel (not the live
  feed or Site): test `0.2.57123b899145` at channel `fee293c`; schema 1 `arena-8133bc1171033b69.json.gz` 24,638,975 B,
  schema 2 `arena-885f3d47d405ddc3.pack` 20,533,942 B. Candidate (`--test-only --channel`, Node 24.19.0, tooling 35ea65d):
  every stage passed except the baseline-only verify; repeat build identical. Raw feed served the new manifests; old-launcher
  and v2 installs at `0.2.fd485a10e3d0` updated from the live feed, then reported current; settings sentinel untouched;
  menu status `0.2.57123b899145`.

## 2026-10-04 — Claude — Sandbox rarity sort, keypress dummy cast, dummy respawn, DEV-only sandbox; Ward Zero PvP vents/machines
- Branch `claude/sleepy-ritchie-g8ruhv` from `claude/visual-revision` accc362 (PR #44 merge). Owner requests 2026-10-04.
- Chip list: strict tier order common → … → mythic → mishima → ansatsuken → l44t → siren (`RARITY_ORDER`; the old sort
  used catalog weight, so tiers interleaved), grouped per tier in the list; Rarity ↑ is the default sort.
- Dummy cast "Press key" (AI, E, Q, Block, Dash, Jump, 1-4, Mode): the dummy stands still, turns to the asking pilot and
  presses that key with the chip's weapon (holds where the kit charges or aims); keys the dummy's weapon has no use for are
  greyed out and refused. DEV kits are granted to the dummy as a scoped `weapon:` unlock, never the DEV role. Slenderman
  (Scare Tactics, E) stalks the pilot who asked. Repeats refresh the dummy's cooldowns (sandbox only). A Siren chip cast
  takes the sandbox to Ward Zero; any other chip restores the previous arena and stops her song.
- Dummy respawn: a killed dummy stands up on its own pad after a delay (panel "Respawn (s)", default 5, 1-30, 0 = the old
  round end), refilled and with once-per-round chips (Emergency Exit, Do Not Press, Round Two, CTRL+Z) re-armed; waits for
  animation holds and a vent-fall body landing. A downed human still resolves the round (bots beat the human, or a human
  duel) without the waiting bots.
- DEV-only sandbox: `sandboxAllowed()` = DEV account or the Vite dev server; the home link is hidden and the menu, WebMCP,
  `window.chipArena`, local debug commands, the panel and F3 are gated. Production build check: link hidden, no panel.
- Ward Zero outside PvE (PvP + sandbox): no Chip/Recruit/Heal machines, their pads or prompts (`machinesPresent` = a real
  PvE run). Vents: snapshots announce each opening `VENT_PIT.warn + lead` (1.5 s) early; the stage rattles the lid and fills
  the rim for 1 s, swings it 0.5 s ahead of the pit turning lethal, trails it on close, and keeps drawing the last opening
  while an interpolated view lags (online); positional metal cues. No add ever climbs out. Sandbox "Vent fall" forces a real
  opening (warn → swing → fall) and never hides or snaps the regular cycle.
- Impacts: no change to hit timing, hitboxes, damage, cooldowns or movement outside the sandbox. Ward Zero PvP keeps the same
  lethal vent schedule (presentation only leads it). Network: Ward Zero snapshots carry upcoming vent openings 1.5 s early;
  `snapshot.sandbox` (respawn/safe/repeat) in sandbox rooms only; revive reason `sandbox-respawn`. pvp-golden unchanged.
- Review: adversarial review workflow (4 areas): 13 distinct findings, all fixed, most with a regression test (the vent,
  round-end and fixture tests fail without their fix); verifiers confirmed the respawn fixes on the pushed code.
- Checks (Node 22.22.0): check:types pass (server tsc keeps its 4 baseline errors, identical on accc362); sandbox-tools
  16/16, ward-pvp-presentation 6/6, sandbox-access 1/1; full unit suite 1228, 13 failures = the 12 known names + the Node 22
  portable-update test (all fail on the base too); network pve and maps pass; default network only "Timed out: rematch".
  PR CI (Node 24.19.0) `ea042c6`: unit 1228, only the 12 known failures; server build only the 4 baseline errors; websocket
  only "Timed out: rematch"; HTTPS passes.
- Not run: native Windows, online sandbox room, packaging. Real-app captures (Vite dev server, headless Chromium/SwiftShader):
  tier groups, greyed keys, respawn after 3 s with the round going on, keypress Slenderman jumpscare, Ward Zero downed preview
  without machines, PvP vent warn → swing → open → fall with no add.
- Earlier publications (from the channel's PUBLISHED.md): PR #43 `492b251` -> test `0.2.6bb5c3639b53` (channel `7a393b8`;
  schema 1 `arena-38d7b91c0c5dbe17.json.gz` 26,206,825 B, schema 2 `arena-2870598814579f84.pack` 22,099,609 B); PR #44
  `accc362` -> test `0.2.cb814cb80ece` (channel `1bc3517`; schema 1 `arena-2bf02d3b1f9a2899.json.gz` 24,625,849 B, schema 2
  `arena-614b8df19c9400d9.pack` 20,530,767 B).
- Status: merged into `claude/visual-revision` at `29d184b` (PR #45); published to the test channel (not the live feed or
  Site): test `0.2.fd485a10e3d0` at channel `b2241e2`; schema 1 `arena-d23a8fd7f204c872.json.gz` 24,637,893 B, schema 2
  `arena-3d53ae883a980000.pack` 20,534,868 B. Candidate (`--test-only --channel`, Node 24.19.0, tooling 35ea65d) passed
  every stage except verify, which had only the baseline failures (12 known unit names, 4 server tsc errors, websocket
  "Timed out: rematch"); repeat build identical. Raw feed served the new manifests; an old-launcher install and a v2 install
  at `0.2.cb814cb80ece` each installed the new version from the live feed with `manual-update.mjs`, then reported current;
  settings sentinel untouched; menu status reports `0.2.fd485a10e3d0`. Native Windows look/feel review remains.

## 2026-10-04 — Claude — Developer Sandbox tools; Ward Zero as a PvP and sandbox map
- Branch `claude/sandbox-devtools` from `claude/visual-revision` 492b251. No PR opened; not merged; not released.
- Sandbox panel (`src/client/sandbox-panel.ts`): live chip search (name/id/description) + sort (default, rarity ↑/↓ by
  catalog weight, name; pure `findChips`), "Every chip" scope; Dummy cast (dummy takes the chip + a fitting weapon and
  fights as a hard bot for 4 s, repeat every N s, safe mode = humans keep 1 HP); sandbox map select; animation tests (vent
  fall + SPLAT 2/10 s, downed/HELP and holographic revive via the DEV fixture injector, death effect, killcam, weapon intro,
  melee/greatsword, hit/stagger, summons, summon deaths, Slenderman), slow motion (local only). Warm-up re-runs when drawn.
- Authority (`src/shared/sandbox-tools.ts`, sandbox debug commands only): dummy-cast, safe, map, vent-fall, down, hit,
  summons-die; DEV-only weapons need `canUse`. `respec` extracted from the spec command unchanged.
- Ward Zero (owner request): pickable PvP map (not in random rotation); outside PvE its vents run a fixed 12 s cycle as
  pits (the PvE VentPits code), vent-fall kill with normal attacker credit; PvP keeps normal respawn (no splat body); no
  adds, boss, machines or boss song (vents pause while a Siren pilot forces the map); bots use the existing vent steering.
  `VentPits.splatAt` (PvE default 10 s) and `lands`. Snapshot gains `vents` only on Ward Zero outside PvE.
- Impacts: no change to hit timing, hitboxes, damage, cooldowns or movement outside the sandbox and Ward Zero PvP.
- Checks: check:types pass; sandbox-tools 6/6; pve-* + pvp-golden 213/213 (all 7 traces identical); full unit suite
  1211, only the 13 known Node 22 failures; network pve passes; default network only "Timed out: rematch" (baseline).
  Playwright/SwiftShader screenshots of the panel, Ward Zero sandbox fall/splat, downed/revive, PvP Ward Zero match.
- Payload: `npm run build` stopped at the live guard: schema 1 26,218,140 B (+11,315 B; 3,740 B over 25 MiB, before
  this entry). Guard unchanged; the 32 MiB test channel accepts it. Not run: Windows, online sandbox room, packaging.

## 2026-10-04 — Claude — publication records: PRs #37-#41 and PR #42 on the test channel
- PRs #37-#41 merged `7b91b6c` -> test `0.2.7c06fa8f3a9f` (channel `d14910c`); schema 1 `arena-054ddd2d7339d417.json.gz`
  26,187,119 B, schema 2 `arena-96b2e5e50fd6120c.pack` 22,093,779 B.
- PR #42 merged `5bb0366` -> test `0.2.24a26b19d4ee` (channel `db0bad1`); schema 1 `arena-516d55633004c310.json.gz`
  26,187,925 B, schema 2 `arena-81b2d09164d54a85.pack` 22,093,555 B.
- Both: candidate (`--test-only --channel`, Node 24.19.0) passed every stage except baseline-only verify (12 known Node 24
  unit names, 4 server tsc errors, websocket "Timed out: rematch"); old launcher `run.mjs` and the dcc7430 seed
  `manual-update.mjs` each installed the new version from the live feed, then reported current. Live feed and Site untouched.

## 2026-10-04 — Claude — summon chips get real combat; Slenderman (Scare Tactics) rework

- Branch `claude/summons-slender` from `claude/visual-revision` 7b91b6c, merged with 5bb0366 (PR #42 freeze fix). No PR opened; not merged; not released.
- Summons in scope: zombies (Necronomicon, Graveyard Shift, Corpse Party), toy soldiers (Puppet Master "Toy Soldier"), and
  the L44T Scare Tactics Slenderman (DEV-only, unchanged access). No other chip spawns a creature.
- Summon combat (`src/shared/chaos.ts` SUMMON): windup → strike → recovery with damage on the strike frame. Zombie lunge+claw
  (windup .42 s, reach 1.75 m to start, 2.4 m checked at the strike, recover .48 s, 12 dmg); toy soldier aims and fires one
  round (windup .45 s, starts within 7 m, 9.5 m at the strike, cover blocks the ray, recover .55 s, 8 dmg; it now holds ~5 m
  instead of walking into melee). Damage through `sim.damage(kind 'minion')` credited to the summoner, blocks parry as before,
  no life steal on summon bites (as before). Targets: nearest enemy pilot (PvE boss unless hidden) or enemy summon; PvE adds via
  the horde assist, which now only chases and returns the add (the horde no longer applies its instant D38 hit).
- Health: pools unchanged from the chip texts (zombie 2× owner max HP, toy 25). New: burn/poison DoTs on chaos summons, all
  direct-hit procs (Static/Vacuum/Shrapnel) on summon hits, standard-melee specials hit summons in PvP. `minionDeath` event.
  Caps unchanged (14 zombies, 3 toys per owner, 34 total). Bots focus a hostile summon within 6 m that is clearly closer than
  their pilot target (`botFocus`).
- Slenderman (`src/shared/leet.ts`, SLENDER in leet-content): a 160 HP kinematic chaos minion (`kind 'slender'`, every damage
  path). Never moves while his target's authoritative camera (input yaw/pitch/camera mode, wall-clipped) sees his head or chest
  within 36°; staring builds sanity (.09/s + up to .5/s within 14 m); looking away decays it (.3/s). Unseen for .35 s and off
  cooldown (3.2 → 1.3 s over his 22 s life) he teleports behind the target to .72 → .5 × the gap. Reach (1.6 m) or sanity 1 →
  the existing 2.2 s jumpscare then knockout (`leet-scare`). Ends on owner death, expiry, recast, round reset or his death.
- Client: `summon-visuals.ts` (attack/flinch/corpse poses, tin-soldier look + rifle, tracer and pooled voxel break-up),
  summon health bars in `overhead-health.ts`, new Slenderman model `slender-model.ts` (suit/shirt/tie, faceless head, long
  limbs, six tentacles, sway, head tilt, teleport flicker/smear + glyph-pool static), victim-only static overlay and
  drone/heartbeat `slender-static.ts`. All meshes use the rig's Lambert palette and existing MeshBasic variants (no new programs,
  no lights); static is DOM/CSS, audio is Web Audio oscillators (no assets).
- Impacts: hit timing (summon damage now on a strike frame, was instant), hitboxes (Slenderman capsule .28 r × 2.46 m, new),
  damage (unchanged numbers; DoTs/procs now apply to summons), cooldowns (zombie ~.9 s cycle unchanged, toy 1.0 s, Slenderman
  hop cooldown new), movement (attack windups/recovery root summons; toy keeps range; Slenderman teleports only), networking
  (snapshot: summon hp whole numbers, timers to ms; leetFields gain slender/seen/sanity/hops/hopAt/from/yaw/hp; new events
  minionShot, minionDeath, leet-slender-hop). pvp-golden: only `necronomicon` regenerated (header note); the other six identical.
- Checks: `npm run check:types` pass; `tests/summons.test.ts` 16/16; chaos, leet, chaos-visuals, first-block, pve-foundation,
  chip-vfx-sweep, pvp-golden pass (known baseline failures excepted). Full suite once: 1205 tests, 15 failed = the 13 known +
  2 chip-vfx-sweep tests (synthetic snapshots without `events`), fixed and rerun green. `run-network pve` pass (victory);
  default network fails only the known "Timed out: rematch"; `run-network chaos` fails at its cosmetics assertion on the base
  too (pre-existing, before the summon section). `npm run build` ok: live payload 26,206,719 / 26,214,400 bytes.
- Not run: Windows/GPU, real multiplayer, audio listening, packaging. Lab captures via tools/visual-lab vfx.html
  (zombie, toy, summonhit, summondeath, slender, slendermodel, slenderhop); health bars do not draw in the lab (not "playing").
- Risks/next: balance of the Slenderman (sanity rates, 160 HP, hop shrink) and the toy's ranged shot need owner playtest.

## 2026-10-04 — Claude — random lag spikes (full-screen freezes, every mode, offline)

- Branch `claude/lag-spikes` from `origin/claude/visual-revision` 7b91b6c. Implemented on branch; no PR, not merged, not published.
- Method: Playwright + SwiftShader real-app runs (dev server) of a PvP bot match, the Developer Sandbox firing every gun
  and magic school, and an offline TEST BOSS run (waves, spawns, Siren phases 2-3). Recorded a rAF frame log, long
  tasks, a CDP CPU profile and a trace of every WebGL `linkProgram`. Also timed a headless local PvP match in Node with
  `--trace-gc`.
- Cause 1 (the freezes, worst when shooting): GPU programs were linked mid-game. three.js links a program the first
  time a new material variant is drawn, and deletes it when the last material using it is disposed. Per-event materials
  (projectile trails per shot, melee trails, impact glows, death fireworks, wolf-landing dust, Siren claw slashes) were
  re-linked every time the previous effect of that kind had expired. Pooled effects (chip blasts, sparks) linked on
  first use. On the boss stage the seeker's point light changed the light count, which re-keys every material.
  Fix in `src/client/shader-warmup.ts` (renderer only):
  - every linked program is pinned (cap 512);
  - the first compiled ShaderMaterial of each shader source is kept, so its stage ids stay stable;
  - on a new scene composition (map, PvE stage, round) the scene and hidden probes for per-event variants are compiled.
    When a Siren is present, this runs once more with one extra point light.
  SwiftShader evidence:
  - firing test: main-thread shader-compile time 31.2 s -> 2.5 s, links after start 64 -> 9, hitches over 950 ms 26 -> 0
    with a link (all 26 had been link frames);
  - PvP: links after start 22 -> 3;
  - PvE boss fight: links 17 -> 2 (Siren slash 13 -> 1), in-fight compile time 28.6 s -> 5.7 s.
- Cause 2 (GC pauses): the killcam history ran structuredClone on the whole snapshot at 20 Hz and kept 6 s of it, so
  the static map's props were stored again in every frame. Fix: `Killcam.record` reuses the previous frame's copy of a
  prop or event when it is equal (`sameData`); players and the rest are still copied. Headless PvP (5 hard bots, 2 min):
  major GC max 30-55 ms -> 6-9 ms, total GC about 1.7 s -> 1.0 s, sim step p999 12-14 ms -> 5.5 ms. Playback is
  unchanged (the killcam tests pass).
- No gameplay, snapshot, network or save changes. Hit timing, hitboxes, damage, cooldowns and movement are unchanged.
- Checks (Node 22.22.2): `check:types` passes. The new `tests/lag-spikes.test.ts` passes 5/5. pvp-golden passes 7/7.
  `tests/pve-*.test.ts` + killcam + lag-spikes pass 217/217. The full suite had 1189 tests and only the 13 known
  failures. `run-network.mjs pve` (TEST_PORT 8823) reached victory, deflated p95 5620 B. `npm run build` gave a live
  payload of 26,187,878 B against a limit of 26,214,400.
- Not fixed or not reproduced:
  - offline PvP still builds a full snapshot every tick on the main thread (about 1.8 MB/tick of short-lived allocation,
    mostly Rapier vector wrappers and prop views);
  - intro and cutscene light-count changes (Siren intro, bass-van cabin light) still compile once each per session;
  - auto-graphics RenderBudget resizes were not seen;
  - no audio-decode, HUD or worker catch-up hitch was found;
  - SwiftShader cannot show real GPU timings.
- Next: owner test on Windows. The first match after launch may still compile once during the intro or countdown.

## 2026-10-04 — Claude — PvE vent pits: black open vents, vent-fall deaths, timed splat

- Branch `claude/pve-vent-pits` from `origin/claude/visual-revision` 8840198, merged with 3457323 (PRs #37–#40). Not merged, not released, no PR.
- Visual: an open Ward Zero vent draws a shaft into the floor quad (per-pixel ray into a 2.1 m box: seamed walls and rungs fading to black, black bottom, faint red lip). It replaces the old black plane that the floor tiles showed through. Its opacity follows the existing lid curve (`entryOpenAmount`, moved verbatim to `src/shared/pve/vent-pits.ts` and re-exported from `client/siren/stage.ts`), so timing is unchanged. It is fully dark at the lethal open amount (0.5).
- Fall (server, `VentPits` in `pve/vent-pits.ts`, stepped from `PveSystem.stepAfterPhysics`): a connected, alive member or recruit whose capsule centre is within 0.78 m of an open vent (open amount ≥ 0.5) and not jumping dies at once with kind `vent-fall`. Credit goes through `Simulation.damage` like the `fall` kill, so a recent attacker within attributionSeconds is the source and killer. Roll i-frames, the revive shield and chip saves do not hold the floor up. God mode and hidden players are exempt. The boss and adds never fall. Closed vents are solid. PvP Ward Zero vents never open, so PvP is unchanged; pvp-golden is identical.
- Splat: at death+9.4 s the record becomes `vent-splat` (new death id, same source and kind, `landAt`). The body falls 13 m and lands at exactly death+10 s on `splatSpot()`: the hall centre, otherwise the nearest clear ring point off blocks, machines, vents, the boss (radius × scale + 2.4 m) and other bodies. It is then a normal kept body. `inVentShaft()` blocks paid revives, beacons, the HELP bubble and the F prompt until it lands. The Tab board and HUD show "In a vent · lands in Ns".
- Respawn rules: a wave/phase `respawnAll` during the shaft time revives normally on a pad and skips the splat. The solo self-revive waits for the landing and then revives at the landing spot. Holds shift `landAt`.
- Bots: `VentPits.steer` deflects party-bot and recruit moves around open or scheduled vents. Recruits can fall but have no splat (no kept body).
- Snapshot: only the optional `DeathPresentation.landAt` was added. Client effects are in `client/pve/vent-fall.ts`. Audio reuses dash, jump, arcade-heavy, water and arcade-punch. Small economy.ts gates. horde.ts and entries.ts were not touched.
- Checks (Linux, Node 22): `check:types` passes. The new `tests/pve-vent-fall.test.ts` passes 7/7. `tests/pve-*.test.ts` passes 200/200. pvp-golden passes 7/7. `run-network.mjs pve` reached victory. The full suite had 1184 tests and only the 13 known failures. `npm run build` gave a live payload of 26,187,038 B against a limit of 26,214,400 (27 KB headroom). Pve-lab images were captured under SwiftShader (kinds ventfall and splat were added).
- Not run: Windows/GPU, online multiplayer by hand, packaging, publication.
- Next: owner review of the pit look and the splat. The tight update headroom matters for the next asset change.

## 2026-10-04 — Claude — PvE: Siren adds take hits while entering
- Branch `claude/pve-adds-hittable-entering` from `origin/claude/visual-revision` `8840198`. Implemented on branch; not merged/published.
- Root cause: an entering add had no physics body (created only at `activeAt`), and `Horde.hit()/status()` returned early unless
  `phase==='active'`, so every damage path (all go through registered colliders / `chaos.minions` -> `hitAdd`) ignored it.
- Now an entering add registers its body when it comes into sight (`shared/pve/entering.ts` `entryExposedAt`): doors/cells at
  `walkAt` (leaves/teeth open, walk starts); vents at climb age push+open .8 s (grate open, head and hands over the rim) =
  `activeAt-1.15`, also for re-emerging travellers. The body has no gravity and a no-contact collision group (queries still
  hit it) and follows the drawn body every tick: the door/cell walk, or the vent climb (`VENT_CLIMB_BODY` keys now shared
  with the client pose). Pending, behind closed leaves/teeth/grate and underground adds stay unhittable.
- Hits on entering adds: damage, gibs, crawl, stagger, DoTs/slows apply; no knockback until active (stays on its path); no
  attacks (AI unchanged). Kill: corpse at the path point (vent: the climb exit, not over the pit), entry slot released,
  credit/money once. An entering add that loses a leg reports state `crawl` (entry stays >= 0); client `enteringView()` keeps
  it on its entry path and keeps the entry pose for a corpse killed on the way in. Snapshot encoding unchanged.
- Hit timing/damage/cooldowns unchanged; hitboxes: new for entering adds only; PvP untouched (pvp-golden passes).
- Checks: check:types pass; new tests/pve-adds-entering.test.ts 6/6; tests/pve-*.test.ts + combat-fx + pvp-golden 214/214
  (one pve-horde assertion updated: entering adds are in chaos.minions once in sight); `run-network.mjs pve` (TEST_PORT 8817)
  victory, deflated p95 5715 B; full suite 1162 pass / 13 fail = the 13 known Node 22 failures; build live payload
  26,170,710 B (limit 26,214,400). Not run: Windows hand-play, visual check of hitbox vs. climb in the client.
- Balance note: adds are now easier to kill at doors, cells and vents (about 1.2 s / 1.0 s / 1.15 s exposed before active).

## 2026-10-04 — Claude — melee attack stamina 40% lower (all melee)
- Branch `claude/melee-stamina` from `origin/claude/visual-revision` `b7f3dad`.
- Owner: "Lower stamina drain from melee attacks by a good portion, on ALL melee." Primary swing cost (`meleeCost`,
  souls-combat.ts) ×0.6: daggers 16→10, sword 30→18, shield 36→22, greatsword 44→26. Echo Strike follow-ups 5→3
  (`followupCost`). E specials keep their costs (30/38/45/55). DEV melee (Emperor, Ansatsuken) unchanged (already 3–4/hit).
- Gameplay change: stamina only. Hit timing, hitboxes, damage, cooldowns, movement and networking are unchanged.
- Tests: three pins updated with comments (souls-combat costs, melee-reference-update dagger spend, modifiers stamina gate).
  pvp-golden melee-dev, siren-melee, siren-ward-zero regenerated; with the old costs all seven traces matched the old hashes.
  Full Node 22 suite before pin updates: only those 6 plus the 13 known failures; pvp-golden 7/7 and the 3 updated files pass.

## 2026-10-04 — Claude — greatsword blade clears the shoulders (mocap follow-up)

- Publication (previous task): PR #36 merged 076afe5 -> test 0.2.7f4cf400e693; channel c351c41; schema1 arena-79bbf8ca845d2b9e.json.gz 26,167,432 B, schema2 arena-4bbd4d77d7a7b6cf.pack 22,081,531 B; old launcher and seed manual-update both installed then reported current; candidate verify baseline-only.
- Task: owner report that the greatsword runs through the shoulder. Branch `claude/greatsword-shoulder` from
  `origin/claude/visual-revision` 076afe5. Implemented on branch; no PR, not merged, not published.
- Cause: the run carry (shoulder rest) had its grip dragged toward the support shoulder (wide voxel shoulders), laying the
  blade through the chest and right pauldron. Converter: the support hand now lets go where reaching the grip would need
  a shift over ~.25 m (the clip table is regenerated). Runtime: `src/client/mocap/blade-clearance.ts` turns the blade
  about the grip (and lifts the grip off the shoulders/chest) until the blade slab clears the pauldrons, chest shell and
  head by 2 cm, and rolls the flat onto a nearby surface; continuous and stateless; cuts only turn the blade (no grip
  shift). Visual-lab fix: the raw `mocap=` preview import no longer breaks the lab on a clean checkout.
- Impacts: presentation only. Hit timing, hitboxes, damage, cooldowns, movement and networking unchanged.
- Checks (Node 22.22.2): `npm run check:types` pass; greatsword-mocap (new clearance test over idle/walk/run/block),
  pvp-golden and the 8 MOCAP-off melee/locomotion files: 86/86 pass. Lab strips (run/walk/idle) reviewed.
  Not run: full suite, build, Windows/GPU, multiplayer.
- Next: owner check of the run carry; the blade now rides up against the outside of the right pauldron rather than
  lying flat across its top.

## 2026-10-04 — Claude — killcam uses Call of Duty timing
- Branch `claude/killcam-cod` from `origin/claude/visual-revision` `076afe5`.
- Owner: the ~4 s aftermath "feels too long"; use Call of Duty logic. `KILLCAM` before 1.5 → 3 s (lead-in from the killer's
  view), after 4 → 1.25 s, hold .6 s unchanged: ~4.85 s in all (was ~6.1 s), so a round-ending kill fits the 5 s result
  screen. Skip, killer camera and the one-way cut to the body are unchanged. Client presentation only: no change to hit
  timing, hitboxes, damage, cooldowns, movement, respawn/result timing or networking.
- Checks: killcam unit + presentation 16/16, killcam network scenario passes, `npm run check:types` passes. Not run: full
  suite (killcam constants and their tests only), Windows hand-play.

## 2026-10-04 — Claude — publication record: PR #34 + PR #35 on the test channel
- Owner confirmed publication and granted standing permission for pushes, merges and test-channel updates (2026-10-04).
- PR #34 merged `c307925`, PR #35 merged `ab2e4c5` -> test `0.2.31513375c335` (channel commit `e49b238`).
  Schema 1 `arena-627b9d85d7fb7712.json.gz` 26,148,392 B; schema 2 `arena-e002c9381d84a9a6.pack` 22,068,484 B.
- Candidate (`candidate.py --test-only --channel`, Node 24.19.0): every stage passed except baseline-only verify (12 known
  Node 24 unit names, 4 server tsc errors, websocket "Timed out: rematch"); source-privacy and channel-e2e passed.
- Verified from the live feed: old launcher `run.mjs` 0.2.19e114ae7c87 -> new then "up to date"; dcc7430 seed
  `manual-update.mjs` test-seed -> new via schema 2 then "current". Live feed and Site untouched.

## 2026-10-04 — Claude — greatsword Mixamo mocap (client visual only)

- Task: owner-supplied Mixamo "Great Sword Pack" converted into the voxel rig's pose system for the greatsword.
  Branch `claude/greatsword-mocap`, base `origin/claude/batch-intros-fx-killcam` ec169ed (PR #34 → visual-revision).
  Implemented on branch; no PR opened, not merged, not published.
- Changes: offline converter `scripts/mocap/` (three FBXLoader in Node; Mixamo → rig joint rotations, hinge knees,
  heading, hips height, chest-space IK targets for grip/blade/edge/poles/free hand; root travel stripped; quantized,
  delta-coded) writing `src/client/mocap/greatsword-clips.ts` (8 clips, ~22 KB source, ~8 KB gzip). Runtime
  `src/client/mocap/` (decoder, Catmull-Rom sampling, C1 retime, rig pose through rig-ik, per-weapon sets). Greatsword
  third-person rig: captured guard idle, walk/run upper body over the gait (stride-synced), alternating cuts
  (slash / slash (3)), block raise + blocked-hit impact, hitstun reel under the procedural impulse. FBX/zip never
  committed; rerun steps in DEVELOPMENT.md. Other weapons, first-person view model, wolf-spin special, rolls,
  knockdowns, deaths (ragdoll) and intros keep their procedural code.
- Timing: each cut is retimed so its captured contact (blade crossing the front arc) spans swingAt..swingUntil and
  settles at the end of recovery, at any attack rate. Hit timing, hitboxes, damage, cooldowns, movement, networking,
  snapshots and src/shared are unchanged (pvp-golden passes in the full suite).
- Tests: new `tests/greatsword-mocap.test.ts` (table, contact alignment within a frame, joint limits, limb continuity,
  two-hand grip). Eight procedural melee/locomotion suites now set `MOCAP.enabled=false` so they keep guarding the
  procedural layers (still live for first person and other weapons); their greatsword choreography assertions
  (kinetic-chain lag, low-then-rise sweep, two-beat recovery, sprint carry, transition turn bounds) describe the
  replaced third-person phrases — owner review of that scoping requested.
- Checks (Linux, Node 22.22.2): `npm run check:types` pass; full unit suite 1170 tests, only the 13 known failures;
  `npm run build` pass, live payload 26,167,005 B (limit 26,214,400; +18,673 B vs 26,148,332). Visual lab strips
  (SwiftShader) before/after reviewed. Not run: Windows/GPU, multiplayer, packaging, update-channel publication.
- Risks / next: owner playtest of feel (hoist is the capture's speed, ~as fast as the strike; turning in place slides
  the captured idle stance; deep low sweep on cut 2). Mapped clips can be swapped in convert-greatsword.mjs.

## 2026-10-04 — Claude — killcam starts closer to the kill (result screen stays 5 s)
- Branch `claude/killcam-forward` from `origin/claude/visual-revision` `c307925` (PR #34 merge).
- Owner: keep the 5 s result screen, just move the killcam forward. `KILLCAM` before 2 → 1.5 s and live hold 1.2 → .6 s;
  aftermath stays 4 s. The kill now plays ~2.1 s after death (was 3.2 s), so a round-ending kill shows ~2.9 s of
  aftermath before the 5 s result ends (was ~1.8 s); other deaths show the full 4 s. Client presentation only: no change
  to hit timing, hitboxes, damage, cooldowns, movement, respawn/result timing or networking.
- Tests: killcam pins/timing updated (`tests/killcam.test.ts`, `tests/killcam-network.ts`); killcam unit + presentation
  16/16 pass, killcam network scenario passes, `npm run check:types` passes. Not run: full suite (change is confined to
  killcam constants), Windows hand-play.
- Publication record: PR #34 merged `c307925` (intros refresh, combat FX on adds, hold-to-aim, ADS zeroing, killcam
  aftermath). CI was baseline-only (4 server tsc errors, known Node 24 unit names, websocket "Timed out: rematch"; a
  first-run `sawDestruction` websocket failure did not recur on the one rerun). Not yet published to the test channel:
  publication paused for owner confirmation after an auto-mode permission block.
- Next: owner confirms publication; build the test candidate from the merge of this branch and publish both feeds.

## 2026-10-04 — Claude — weapon intros refresh, 3 s lobby return, ADS sight alignment, Emperor audio leak, AoE outlines

- Branch `claude/intros-refresh` from `origin/claude/visual-revision` `dcc7430`. No PR (orchestrator merges). Not merged,
  not published.
- Publication record (pending from the integration notes): PR #33 merged `dcc7430` -> test `0.2.1412350a765a`; channel
  `ae36a7e`; schema1 `arena-255764ffce5226cd.json.gz` 26,135,339 B (old launcher network update verified
  0.2.5036b50f0c34 -> new) and schema2 `arena-258068ee0d6f182c.pack` 22,063,628 B (seed network update verified via v2);
  candidate checks passed except the baseline verify; private GitHub Release creation blocked for that session (HTTP 403),
  seed ZIP at `releases/channel-dcc7430` for the owner to upload.
- Lobby return: the host's Return to lobby countdown is 3 s (was 5 s), `LOBBY_RETURN_SECONDS` on the authoritative sim
  (server and host rooms). It is the only automatic lobby timer; PvP match results and PvE results still wait for a click.
- Weapon intros (11 non-DEV): `weapon-intro-effects.ts` now draws only pooled ChipBlasts voxels (muzzle cones, tracers,
  casings, smoke, blade trails, a voxel crescent, honeycomb barrier, quake/fire/shock/void/kinetic blasts in miniature,
  serpent and portal voxels) instead of rings, cones, planes, tubes and line bolts. Fixed 60 Hz stepping from age 0 with a
  reseeded generator keeps it a pure function of age (scrub/late join/frame rate identical); one ChipBlasts per stage.
  Motion: magic intros hold the staff in the left hand and cast with the right (as in-game Q/E); guns get an ADS cheek
  weld and recoil into the shoulders. Hits, name card (3.2 s), 4.8 s duration, cameras and intro audio unchanged.
- ADS (owner: sights must line up with the shots): aimed gun shots launch on the ballistic arc that meets the crosshair ray
  hit point (authority, `spawnProjectile`); speed/gravity/damage/spread unchanged, hip fire unchanged; ADS bots aim straight.
  First-person view model rotates/shifts so each gun's sight line (pistol sights, MG carry handle, rifle scope axis) runs
  down the camera axis. This is a gameplay change (ADS no longer drops below the crosshair at range).
- Emperor audio leak (lightning staff and rifle): client cues mapped Blink Strike, its teleport and Orbital Mark to
  Emperor's `electric-hook` clips. Now dash/lightning cues; electric-hook plays only for Emperor events. The authority was
  already Emperor-gated (Q's legacy `electric` input is ignored for other specs).
- AoE outlines removed: field voxel rims and dome wireframes, blast/impact ground rings (now fill the disc to 92 % of the
  radius), outline telegraph motes (now inside the area), Orbital Mark shrinking ring, melee/wolf/impact rings. Areas read
  through their volume plus an edge-less ground glow that keeps the enemy/ally tint. Gameplay radii unchanged; the
  ability aim preview path is untouched.
- Animation impact: intro-only presentation; no hit timing, hitbox, damage, cooldown, movement or network change from the
  intros. ADS zeroing changes ADS projectile launch pitch only.
- Tests: new `weapon-intro-effects`, `ads-alignment`, `emperor-audio-isolation`; updated lobby-return (3 s), chip-blasts and
  chip-vfx-sweep (no outlines), ranged-magic-fluidity (sights on the crosshair), ability-presentation (blink cue),
  abilities-foundation (rifle scoped launch now zeroed). pvp-golden: ffa-foundry and teams-moonlit regenerated (gun bots ADS;
  verified identical with zeroing disabled); the other five traces unchanged.
- Checks (Linux, Node 22): `npm run check:types` pass; full `npm test` 1132 tests, failures = the 13 known Node 22 baseline
  names plus 3 caused here and then fixed (ADS rifle launch test, two golden traces) - all three pass after the update;
  pvp-golden 7/7; `npm run build` pass, live payload 26,140,097 B (< 26,214,400). Visual lab before/after strips for all 11
  intros, ADS first-person sights with a crosshair, and four area effects (SwiftShader).
- Not run: duel-balance (exceeded 10 min; ADS zeroing makes aimed pistol/MG/rifle shots, incl. 25 m+ ADS bots, more
  accurate at range - needs an owner balance look), native Windows/GPU, online multiplayer, packaging/update publication.
- Next: owner review of intro look/feel and ADS feel at range; merge, then the standing test-channel publication.

## 2026-10-04 — Claude — combat FX on extra projectiles, weapon effects on Siren adds, hold-to-aim

- Branch `claude/combat-fx-fixes` from `origin/claude/visual-revision` `dcc7430`; no PR (owner merges).
- Owner reports: impact effects missing on additional projectiles and capping out; weapon effects and
  life steal missing on Siren minions; plus "remove the aim-circle … show it if we hold the button".
- Root causes: projectile hits on adds emitted no `impact` event and applied no DoTs (adds had no status
  state at all); chip extras (Backfire/Crossfire/Helix), and Orbitals were stripped of
  burn/poison and never life-stole; Splitter never split on adds; add hit events above the 20/s per-attacker
  bucket were silently dropped; the 180-spark impact pool dropped new impacts when full; melee/thrown add
  hits and flame lance / water slows skipped adds.
- Changes: shared on-hit for adds (`Simulation.minionWeaponHit/meleeHitMinion/addProcs`): DoTs, life steal,
  Static/Vacuum/Shrapnel per direct attack, impact event per projectile, Splitter on adds. `weaponFx`
  secondary shots keep the weapon DoTs + life steal (players and adds) but no chip procs. Horde: burn/poison
  DoTs (elements.ts rules, credited to the source), slows (Tidal Wave, Shackles, Soak, Geyser Wall) and
  enemy frost/time fields slow adds; over-bucket hits coalesce (flushed on a token, after 0.25 s or on death);
  `PveAddView.st` status bits in flags bits 26-28 (tuple length unchanged); flame/bubble/droplet status rigs
  on the nearest 16 adds. Spark pool recycles oldest. Hold-to-aim: Frag, Meteor, Geyser Wall, Black Hole cast
  on key release for human pilots (tap = release cast; hold > 0.2 s shows the preview; roll/block/other
  non-ADS ability cancels); bots, rocket and Orbital Mark (a dart, no preview) keep cast-on-press; host
  advertises `caps.aimHold`, old hosts keep cast-on-press and the always-on circle; HUD "READY · HOLD TO AIM".
- PvP impact: with Helix/Crossfire/Backfire/Orbitals, secondary shots now burn/poison and life-steal (Splitter
  fragments still never reapply enchantments, per the existing pinned rule);
  aimed E abilities cast on release. pvp-golden unchanged. Hit timing, hitboxes, damage values, cooldowns and
  snapshot sizes unchanged; add movement is slower while slowed.
- Checks (Linux, Node 22.22.2): `npm run check:types` passes; full unit suite 1152 tests, failures equal the
  13 known Node 22 baseline names after fixing one regression found by it (Splitter fragments re-enchanting,
  reverted); new `tests/combat-fx.test.ts` (projectile counts x impacts/heals/coalesced add hits for pistol/MG
  multishot, rifle buckshot, water torrent; DoTs/slows on adds per path) and `tests/aim-hold.test.ts` (tap,
  hold-release, block/roll cancel at 30/60/120 Hz, latest-input and relay-batch delivery; preview gating);
  pvp-golden identical; `node tests/run-network.mjs pve` passes (victory, rejoin, old client refused). Visual
  lab `tools/visual-lab/vfx.html?fx=multishot|buckshot` (Playwright/SwiftShader): sparks on every multishot hit.
- Not run: native Windows/GPU, online multiplayer by hand, packaging; no test-channel publication from this
  task branch (shared branch publication after approval/merge).
- Risks/next: owner playtest of hold-to-aim feel (release latency ≈ one input frame) and add status visuals;
  Orbital Mark kept press-cast (existing test pins it as a dart without ground preview).
  Status: implemented on branch; not merged; not released.

## 2026-10-04 — Claude — kill-cam aftermath: keep watching ~4 s past the kill

- Branch `claude/killcam-aftermath` from `claude/visual-revision` `dcc7430`. No PR; not merged; not published.
- Owner: "move forward a bit, show about 4 seconds after kill" (clarified: keep watching ~4 s after). Window was 3 s
  before / .6 s after the fatal hit (3.6 s clip, starting ~1.2 s after death); now 2 s before / 4 s after (6 s clip),
  still starting after a 1.2 s live hold on the death (`KILLCAM.hold`). The aftermath is no longer waited for: later
  history frames stream into the clip while it plays (playback lags live by 3.2 s); a stalled feed ends the clip .5 s
  after its last frame. History 6 s / 132 frames. Camera: killer's shoulder view, cutting once to the victim's body
  if the killer is gone, dead or >24 m away during the aftermath. Skip/respawn/epoch/lobby rules unchanged.
- Files: `src/client/killcam.ts`, HUD comment, `tests/killcam.test.ts` (timing pins, aftermath + body-cut tests),
  `tests/killcam-network.ts` (replay frame past the kill). No simulation, hit timing, damage, networking or renderer change.
- Checks: `check:types` ok; killcam + presentation 16/16; `tests/killcam-network.ts` ok (3 clients, 87 snapshots,
  aftermath); full unit suite 1110 pass / 13 fail = known Node 22 failures list exactly.
- Risk / next: a round-ending kill enters `result` for 5 s (`resultSeconds`), then countdown/draft cancels the replay,
  so 1v1 and last-kill replays show only ~1.8 s of aftermath. Showing the full 4 s there needs `resultSeconds` ≈ 8
  (gameplay pacing; owner decision, not changed). No Windows/visual playtest of the new framing.

## 2026-10-04 — Claude — ranged/magic Q+E ability overhaul integration (+ PvE downed, chip throw, update headroom)

- Branch `claude/abil-integration` from foundation `claude/abil-foundation`; merges abil-guns, abil-magic, abil-chips,
  abil-presentation, abil-vfx, then `claude/pve-downed` `bf23737`, `claude/pve-chip-throw` `a130059` and
  `claude/update-headroom` `f491e72` (all clean merges). Base of the streams: `claude/visual-revision` `cfdce4b`.
  No PR opened yet; not merged; not published.
- Ability overhaul (design in the orchestrator plan; player tables and balance in `docs/ABILITIES.md`): guns get Q = ADS
  (rifle 3x scope) and E = Frag / Underbarrel Rocket / Orbital Mark; magic gets faster, flatter primaries (Soak, enemy
  chain, 20 % drain), a Q spell and a strong placed/travelling E per school; legacy push/pull/nova/fireball E removed per
  action id (L33T root, melee specials, Necronomicon kept); 16 magic chips; Rally Pong v2 for 3D; first-person staff
  fix; HUD cooldown row; chip VFX sweep; PvE life steal on every direct-hit path. Mixed builds: the host advertises an
  ability flag; older clients keep the legacy controls.
- Integration retune (`tests/duel-balance.ts`, `npm run test:duel-balance`, n=20 per pairing): magic vs guns (open)
  fire 21→55 %, water 3→61 %, darkness 5→55 %, lightning 58→87 %; BOT_LEAD stays [0,0,0] (any lead gave 80-100 % for
  the leading side) and now applies to magic bots only; lightning 15/.28 s/55 m/s/gravity 20 (was 15/.27/85/30);
  frag 60→66; pistol/MG bots ADS from 25 m.
- PvE downed (`bf23737`): downed members keep their body, HELP bubble, holographic regeneration on revive (presentation
  only; `PvePartyView.upAt`). Chip throw (`a130059`): chips land 2.0-2.7 m in front of the machines (pop .55 s, hop
  1.1 m, still inside the .6 s dispense delay). Update headroom (`f491e72`): test-channel schema 1 up to 32 MiB,
  updater v2 (schema 2), launcher self-update/restore; live profile output unchanged.
- Astra/Codex-history files touched: src/shared/simulation.ts, content.ts, config.ts, chaos.ts, arcade.ts, emperor.ts,
  standard-melee.ts, host-room.ts, relay-protocol.ts, src/server/index.ts, src/client/main.ts, renderer.ts,
  prediction.ts, snapshots.ts, audio.ts, character-animation.ts, control-hints.ts, match-hud.css and other client
  presentation files, desktop/main.cjs, desktop/update.cjs, launcher/*, scripts/package-update.mjs,
  package-desktop.py, repackage-desktop.py, docs/DEVELOPMENT.md, tests chaos/modifiers/rarity-combat/relay/
  rig-arena-sniper/first-block.
- Animation/combat impacts: hit timing, damage, cooldowns and hitboxes changed deliberately for ranged/magic (new Q/E
  windups, lockouts, fuses, blast radii, orb radii, primary numbers; magic E no longer shares the primary cooldown or
  reload). Movement: ADS walk multipliers, slows (Soak/Shackles/Geyser, floor .4, immunity), pulls, knock-ups, Blink
  teleport; a roll cancels an ability windup. Networking: Q input bit, optional snapshot fields (omitted when default),
  host capability flag in welcome; predictor mirrors ADS and field pulls. Melee, DEV classes, PvP death presentation:
  unchanged. Downed/chip-throw: presentation and chip landing position only.
- Checks (Linux, Node 22.22.2): `npm run check:types` pass; full unit suite 1122 tests, 14 failures = the 13 known
  Node 22 names + `dev-bots` "ability bots use Q" (start distance below the new 25 m ADS range; test fixed, the file
  then passed 10/10; full suite not re-run). Intentionally changed tests: pvp-golden (4 traces regenerated once with a
  header; melee-dev and siren-melee identical), gun-abilities (frag 66, ADS 25 m), magic-abilities (lightning row),
  dev-bots (Q range), abilities-foundation (kits). `node tests/run-network.mjs pve` victory; `chaos` fails only on the
  known cosmetics assertion; `tests/pve-loopback-network.ts` pass; `npm run test:pve-perf` incl. new abilities scenario
  (avg step .93 ms, p99 2.16 ms, snapshot p95 54,945 B, no ability/explosion events dropped). `npm run build` (live):
  payload 26,135,216 B (limit 26,214,400; 79,184 B headroom); test-channel profile: schema 1 26,135,216 B, schema 2
  22,064,822 B. Playwright real-app sandbox smoke (SwiftShader): fire Q Lance + E Meteor, pistol ADS + frag, no console
  errors.
- Not run: Windows/Electron, GPU, online multiplayer with people, real updater on Windows, release:prepare x2,
  verify:standard, test:network:local, test:https, test:stress, pve-balance, duel gate at n=200, full visual capture set.
- Risks: lightning still wins 62-95 % of open bot duels; water beats rifle/darkness in the open; melee bots lose
  ~100 % to ranged/magic as before; payload headroom is 79 KB under the live 25 MiB guard.
- Pending publication record from the previous integration: PR #32 merged `cfdce4b` -> test `0.2.5036b50f0c34`
  (payload `arena-164f10d7096c7469`, 26,045,324 B, channel `70fe238`). CI first run hit the flaky party-relay 403 (not
  touched by the PR; passed 5/5 locally), rerun passed; otherwise baseline-only. Updater verified.
- Next: orchestrator review, PR into `claude/visual-revision`, merge, test-channel publication; owner playtest of feel
  and balance.

## 2026-10-03 — Claude — update headroom review round 2: short writes, journalled restore, current-check

- Branch `claude/update-headroom` (base `cfdce4b`), implementation `f951fbc`; tooling PR #5
  `claude/test-update-channel` `2519377`. Not merged, not published, no GitHub Release.
- `launcher/update.mjs`: the schema-2 download and unpack loop until every byte is written and
  hash the bytes written (tested with a FileHandle that writes a few hundred bytes per call, and
  one that writes nothing). `windowsSafe` also refuses `CONIN$`, `CONOUT$`, `CLOCK$`, `COM0`,
  `LPT0` and device names followed by spaces.
- A launcher restore (`desktop/update.cjs` and `update.mjs`) writes `runtime/launcher-restoring.json`
  first and replaces files by copy-and-rename. The desktop finishes an interrupted restore before
  the next Update or Create Room, and `update.mjs` before its next start. Nothing is proven while
  the marker exists.
- Blind spot "reports current but never checks": `current` still never proves. While Update is
  unproven, a `current` result is compared with the badge's update check (`feedVersion` in
  `desktop/main.cjs`; in a TEST-ONLY seed, the channel handler). A different feed version counts as
  a failed run and restores the launcher. `installed` proves only when `release.json` has the
  reported version. Residual gap (documented): the check is offline or unavailable, or the feed
  equals the installed version.
- Packager: the live profile refuses `CHIP_UPDATE_SCHEMA2`, `CHIP_UPDATE_SCHEMA2_MAX_MIB` and
  `CHIP_UPDATE_LAUNCHER_OVERLAY` before it builds; live output is unchanged (same values).
- Seed privacy (tooling): the seed no longer contains `chip-arena-source.zip`. It is
  distributed only as an owner-only GitHub Release asset on this private repo, tag
  `test-seed-<content version>`.
- Checks: candidate `f951fbc` + tooling `2519377` on Node 24.19.0 passed setup, fixture 10/10,
  updater tests 28/28, runtime pins, build, repeat build (byte-identical feed), actual payload,
  Windows/desktop packages, seed ZIP, `source-privacy-zip`, `source-privacy-feed` and
  `channel-e2e` 33/33. Content `0.2.5036b50f0c34` (game unchanged): schema 1 26,045,324 bytes,
  identical to the published payload, and schema 2 22,028,495 bytes. Seed ZIP: 284,149,423 bytes
  (was 333,669,480), SHA-256
  `95d34fb255d5f61db3ad5e1bfbe661a470899a8195bf093afd630602c93c4742`. That ZIP was installed and
  its `start.mjs` served `/`, the JS bundle, music and `/health`; `/chip-arena-source.zip` is a 404.
  `verify` failed only on the baseline: the same 12 Node 24 unit names as round 1, server tsc
  errors, the websocket rematch timeout, and the HTTPS tick-0 flake. Node 22 full suite: 980 tests,
  13 failures, exactly the known list. A first run during the candidate build also failed
  `network-bandwidth` ping queue under load; that file passed twice on its own and the idle rerun
  was clean. Node 24 updater and desktop tests: 46/46. New tests fail on the round-1 code (7/21).
  `tsc` passes.
- Not run: native Windows (rename/locking, Electron `session.fetch` through the badge handler,
  real Create Room), real GitHub hosting, the seed release itself. Next: owner/orchestrator
  review; merge; publish the feed and the private seed release from one candidate of the merged
  commit.

## 2026-10-03 — Claude — update headroom review round 1: launcher probation in stable desktop code

- Branch `claude/update-headroom` (base `cfdce4b`), implementation `2db36fa`; tooling PR #5
  `claude/test-update-channel` `ebd998f`. Not merged, not published, no GitHub Release.
- Blocking finding confirmed and fixed: a new launcher that loaded and spoke the protocol but then
  failed (for example a broken `remote-feed.mjs` mapping, or an `update.mjs` whose `applyUpdates`
  throws) was never rolled back, and an Update could delete the backup of a `start.mjs` that had
  never run. Now `desktop/update.cjs` (seed-only, never self-updated) owns probation. A new
  launcher must work once in a later run of each kind it takes part in. For Update, that is an
  Update that installs a version, for `update.mjs`, `manual-update.mjs`, `remote-feed.mjs` and
  `local-feed.mjs`. For hosting, that is a Create Room that reaches `YOUR GAME:`
  (`desktop/main.cjs`), for `start.mjs` and `update.mjs`. Until then, any failed run of that kind
  restores the previous launcher and drops the staged copy. `update.mjs` no longer confirms
  itself. A launcher activated while another is unproven keeps the original backup.
- Non-blocking fixes: v2 refuses the hold notice (`hold:true`) instead of downgrading; a schema-1
  install drops a launcher staged by schema 2; unpack, syntax-check and install phases log progress
  (90 s desktop silence timeout); schema 2 and the packager refuse Windows device names and trailing
  dots or spaces; live `CHIP_UPDATE_SCHEMA1/2_MAX_MIB` can only lower 25 MiB; the test-channel overlay
  is checked before any file is written.
- Not covered (documented in DEVELOPMENT.md): failures after `YOUR GAME:` (online tunnel), a bug
  that appears only after the new launcher installed one update, non-desktop entry points. A
  rolled-back launcher is staged again by the next Update until a fixed version is published
  (each such Update re-downloads the payload, about 22 MB today).
- Checks: candidate `2db36fa` + tooling `ebd998f` on Node 24.19.0. Passed: setup, fixture 9/9,
  updater tests, runtime pins, build, repeat build (all four feed files byte-identical), both
  payloads, Windows/desktop packages, seed ZIP, and the new in-candidate `channel-e2e` 30/30.
  Schema 1 is 26,045,324 bytes, identical to the published `arena-164f10d7096c7469.json.gz`.
  Schema 2 is 22,026,471 bytes. The seed ZIP is 333,669,480 bytes, SHA-256 `73376dff…a876c7`.
  `verify` failed on baseline items: 12 unit names, 4 server tsc errors and the websocket rematch
  timeout. It also failed HTTPS once with a tick-0 snapshot mismatch; the game source equals
  `cfdce4b`, and `tests/https-network.ts` then passed twice on its own, so this is treated as flaky
  under load. Full `channel-e2e` with the 0e82a31 seed (what testers hold) as the old install:
  38/38. Live packager output is byte-identical to the `cfdce4b` packager on the same build.
  Node 22 full suite: 976 tests, 13 failures, exactly the known Node 22 list. Node 24 updater,
  desktop and manual-updater tests: 27/27. tsc passes.
- Not run: native Windows (rename/locking, Electron, real Create Room), real GitHub hosting, the
  seed release. Next: owner/orchestrator review; merge; publish the feed and the seed from one
  candidate of the merged commit.

## 2026-10-03 — Claude — update-size headroom: 32 MiB test guard, updater v2, launcher self-update

- Owner approved on 2026-10-03 ("do what you need") after asking why updates are limited to 25 MiB.
  Branch `claude/update-headroom` from `cfdce4b` (shared `claude/visual-revision`). Implementation
  `299a742` + `f192798`; this entry and a test-only whitelist check follow. Tooling: PR #5
  `claude/test-update-channel` `7c7ded2`, `2a17b17`, `b5ff54c`. Not merged, not published.
- A. `scripts/package-update.mjs` profiles (`scripts/update-format.mjs`). `live` (default) keeps the
  25 MiB Site guard, and its output is byte-identical to the old packager (checked). `test-channel`
  (set by `candidate.py --channel`) allows schema 1 up to the 32 MiB cap of every installed launcher,
  holds schema 1 back when it no longer fits, and always adds schema 2.
- B. Schema 2 is Brotli of `CHIPPACK` + index + raw files (no base64). `launcher/update.mjs` reads
  `latest-v2.json` only when the caller opts in. Only a 404 falls back to `latest.json`. The download
  is streamed with a 256 MiB cap, a byte count and SHA-256, then unpacked file by file under the same
  `allowedPath` rule plus a launcher whitelist. Launcher files are syntax-checked, staged and applied
  at the next start, with journal and backup. (Rollback in this first version covered only a crash,
  an `update.mjs` that cannot load and a silent entry-point death; review round 1 below corrects
  this.) The desktop timeout now counts 90 s of silence (45 min maximum).
  Settings, saves, `runtime/` and `test-channel.json` are never targets. Schema 1 is unchanged.
- Current build (`f192798`, content `0.2.5036b50f0c34`, unchanged game): schema 1 is 26,045,324 bytes,
  byte-identical to the published payload. Schema 2 is 22,025,342 bytes (-15.4%, Brotli q11). Seed
  ZIP `Chip-Arena-TEST-ONLY.zip` is 333,652,190 bytes, SHA-256 `413ea6e9…a5125`.
- Checks (Node 24.19.0 candidate, tooling `2a17b17`): verify matched the baseline only (12 unit names,
  4 server tsc errors, rematch timeout; HTTPS passed). These passed: updater tests 20/20 (including
  the new `tests/update-v2.test.ts`), fixture tests 9/9, repeat build of all four feed files
  byte-identical, `check-payload` on both feeds, Windows/desktop packaging and the seed ZIP launcher
  check. `channel-e2e.mjs` passed 27/27 against a local HTTPS feed using the real code of the
  published `cfdce4b` ZIP and the new ZIP. Old installs: schema 1 at 30.9 MiB installs, 33.9 MiB
  is refused with the install kept, and the hold notice shows "Update Available!" and installs
  `…-download-the-new-TEST-ZIP-from-GitHub-Releases`. New installs: v2 is preferred, a 63.7 MiB
  payload installs, launcher staging then activation then confirmation works, broken-entry and
  incompatible-API rollbacks work, and these are refused: traversal, hash mismatch, oversize,
  longer body, dropped connection. Same-version skip and settings/session preservation pass.
  tsc passes. Node 22: full suite has 972 tests with 13 failures, the known Node 22 set; updater-v2 tests 13/13.
- Not run: native Windows (rename/locking, Electron), real GitHub raw/Release hosting, the
  GitHub Release seed itself (procedure in tooling `docs/TEST-CHANNEL-README.md`).
- Risks / next: testers need one new seed ZIP for v2 (old installs keep schema 1 up to 32 MiB).
  `resources/app` (Electron shell, badge handler) does not self-update. Launcher rollback: see the
  review round 1 entry. Channel Git history grows about
  48 MB per publication. Next: the owner or orchestrator reviews, merges into the shared branch,
  publishes the feed and the GitHub Release seed.

## 2026-10-03 — Claude — test publications of PR #30 and PR #31

- Owner asked to merge and publish both boss-test follow-ups for the Update button.
- PR #30 (desync after Esc/pause, Esc → pause menu, taken chips wait) merged at `6fd525c`; content `0.2.85b68726b813`,
  payload `arena-71bb7881594044cd.json.gz`, channel commit `7a53f4e`.
- PR #31 (29 damage/health chips, 6 boss-only money chips, green/red stat lines, ~2.5x money, instant F revive, camera
  filter, PvE Tab board, owner stage track re-encoded to 128 kbps) merged at `e9edad4`; content `0.2.9a0466bf45fe`, payload
  `arena-cd9fa8ddbe4b964c.json.gz` (26,021,943 bytes, under the 25 MiB hosting limit), channel commit `8f6f5a3`.
- Both: merge trees identical to the tested heads; CI and candidate `verify` matched the baseline only (12 Node 24 unit
  names, 4 server tsc errors, rematch timeout; HTTPS passed); repeat builds byte-identical; actual-payload checks passed;
  no secrets; the real launcher updater upgraded an isolated install over the network and then skipped.
- Not run: native Windows Update button, GPU visuals. Live feed and Site unchanged.

## 2026-10-03 — Claude — legendary / mythic chip rework and volumetric chip blasts

- Branch `claude/pve-legendary-vfx` from `claude/pve-batch2` `9b5445c`; no PR. Owner request: rework broken/weak
  Legendary+ chips so they feel exciting, and replace flat on-impact/area visuals with real voxel explosions.
- Audit of all 26 legendary + 13 mythic chips (code read, unit probes, seat-swapped bot duels); table in the session
  scratchpad (`after/legendary-vfx/audit.md`). Broken: Trading Places never swapped grounded pilots (room check hit
  the floor); blinks (Pocket Portal, Emergency Exit) cast from floor contact stopped at 0 m; Meteor Shower meteors
  were deleted by the 3-per-owner field cap; bots aimed gravity-free shots (boomerang/Pong) with gravity.
- Reworked (ids, rarities and the 7-mythic melee pool kept): Satellite Office (homing satellite shots), Red Light
  Green Light (freeze, then relaunch at the current aim ×2 speed ×1.5 damage), Return to Sender (return leg re-hits
  ×1.4), Comet Dash (landing blast follows the roll, low gravity removed), Tumble Engine (no penalty, i-frames),
  Thunder Step (locks lightning on up to 2 enemies), Now You See Me (blink to target, ×1.5, 0.25 s untouchable),
  Emergency Exit (cheats a lethal hit, +40 HP, 1.2 s immunity), Time Out (Block freezes nearby enemies 1.2 s),
  Supernova (no Block penalty, nova Blocks recharge in 0.6 s), Trading Places (fixed, 0.5 s daze), Meteor Shower
  (5 tracked meteors, 1.5 s cd), Pocket Portal (arrival blast), Do Not Press (+30% HP, 2 s immunity); Hurricane,
  Vampire, Repulsor damage penalties softened. Tuning in `CHAOS_TUNING` (chaos.ts); card stat lines on the cards.
- VFX: new pooled `src/client/chip-blasts.ts` (two InstancedMeshes, fixed capacity, 3 zero-intensity lights):
  fireball + shock shell/ring that ends exactly on the event radius + debris + smoke + flash; shock bolts, implosion,
  void, frost, toxic, quake, nova styles; live-field emitters (storm bolts from a cloud, flames, acid, frost shards,
  accretion spirals, falling meteors, comet trail). Time/repulsion fields get a dome. Keyed to authoritative events:
  explosion events now carry `message` (chip tag) except plain weapon `blast`; fields carry `fx`. Quality/compat
  respected. Dev-only `tools/visual-lab/vfx.html` renders the real Renderer on a real Simulation.
- Tests: new `tests/legendary-rework.test.ts` (15) and `tests/chip-blasts.test.ts` (6). Intended-behaviour test
  updates: chaos.test (Satellite/Stop-Go/Exit/Panic numbers), first-block (Time Out freezes the shooter; thawed for
  the reset check), pve-economy (clear the Exit immunity before scripted kills).
- Checks (Node 22.22.2): check:types pass; full suite 954 tests, 13 fail = the known Node 22 set; pvp-golden 5/5
  unchanged (no golden trace uses a reworked chip). Not run: Windows, real-app play, network suites, perf on GPU.
- Risks: duel numbers are bot-based (bots rarely Block/roll); Thunder Step/Comet/Supernova may need owner tuning;
  Static Discharge (rare) showed no arcs in bot duels (not investigated). Status: implemented on branch, not merged.
## 2026-10-03 — Claude — menus: Boss Test skull pill, simpler online room

- Branch `claude/pve-menus` from `claude/pve-batch2` 9b5445c. Owner asks: "glossy black pill
  button with some skull design, place it on the right" and "Simplify Online Room Options
  (… see 'Ready' easier) … details under an 'Advanced Options' option".
- Home: Test Boss leaves the main list and becomes `bossPill()` (pve/menu.ts, pve.css): glossy
  black pill, inline bone-skull SVG with lavender eyes, sheen on hover, visible focus ring; on
  the right of the menu (moves under the menu at <=860px, full width on phones).
- Room lobby (PvP main.ts, PvE pveLobbyBody): big "n/m READY" meter, green READY / amber NOT
  READY badges and green ready cards, a larger Ready button with a "Waiting for n pilots" line,
  room code + copy buttons at the top of the side column. Mode + arena stay visible; sets,
  rounds, friendly fire, random map, bots/difficulty, bot drafts, host-only bot loadouts,
  draft summary, invite link and connection report move into a collapsed "Advanced Options"
  fold (summary line shows sets/rounds/bots) remembered in localStorage
  `chip-arena-room-advanced` (try/catch; room-options.ts). `matchOptions(lobby,part)` splits
  the same pieces; practice/create keep the full block. PvE party roster uses the room's
  density columns. Short viewports shrink lobby portraits so names clear the Ready bar.
- No setting semantics, element ids, host gating or network messages changed.
- Checks: check:types clean; full suite 925/938, failures = the 13 known Node 22 names;
  pvp-golden 5/5; new tests/room-options.test.ts; pve-ui pins updated for the pill (intended).
  Playwright with the local backend, host + guest, PvP and PvE online rooms at 1920x1080,
  1280x720 and 390x844, plus home at 820/390 widths. Not run: Windows desktop, packaging.
- Risks: in the headless runs a PvE online guest sometimes dropped to DISCONNECTED under heavy
  CPU load (seen on base 9b5445c too); the phone lobby still has pre-existing class-row clutter.
- Status: implemented on branch; not merged; not published.

## 2026-10-03 — Claude — PvE stage music (Restricted Zone Pulse) + batch 2 verification

- Branch `claude/pve-batch2` (from `948b5ba` = `claude/visual-revision` `6fd525c` + econ-revive, chips, camera,
  tab-board merges); no PR.
- Owner: "lets replace the music on the stage to the one attached- until Siren spawns of course :)". Added the owner's
  `public/music/restricted-zone-pulse.mp3` (byte-exact, 4,113,904 B). In a PvE run on Ward Zero the client loops it from
  the first in-game phase (specialization, intros, countdown, waves, intermissions, reveal) until `bossSong` is set at the
  Siren's intro tick, where Black Siren Protocol takes over unchanged. Lobby keeps the menu music; 35% under intro
  stings; rewinds for Retry/new runs; music volume/mute apply; load error falls back to the rotation. Not in
  `MUSIC_TRACKS`, so PvP rotation is unchanged. PvE pre-decodes the boss song so it starts at offset 0 on the intro.
  Files: `src/client/{music,audio,boss-theme,main}.ts`, new `tests/pve-stage-music.test.ts`, docs PVE/ASSET-CREDITS.
- Animation/hit timing/hitboxes/damage/cooldowns/movement/networking: unchanged (client audio only; no wire change).
- Checks (Node 22.22.2): check:types pass; full suite 933 tests, 920 pass, 13 fail = the known set exactly;
  pvp-golden 5/5; `run-network.mjs pve` victory; `pve-loopback-network.ts` pass; `test:pve-balance` 60 runs, 22/60
  victories (S7 table: 13/60), money at boss h=1 pistol 619 / rifle 317. Real app (Vite, offline TEST BOSS, SwiftShader):
  stage track playing through specialization/intro/countdown/fight; paused and rewound the tick the Siren song started
  (boss buffer started once, offset 0); Tab board, chip screen green/red stat lines, one F tap revived a party bot.
- Size: the track is 2:53 (not 8.5 min; the header claimed 64 kbps but it is ~190 kbps VBR). Re-encoded to 128 kbps CBR without
  cover art (2.77 MB); `npm run build` passes with a 26,022,078-byte (24.8 MiB) payload, under the 25 MiB hosting limit.
- Status: implemented on branch; not merged; not released.

## 2026-10-03 — Claude — PvE playtest fixes: more money, instant revive

- Branch `claude/pve-econ-revive` from `213f62b` (PR #30 head); no PR (integrator merges).
- Revive: the hold-F channel cancelled one round trip after it began because the client's
  `pveIdle()` idled all input (interact too) while the party row showed a channel. Revive is
  now instant: one F press near a downed teammate charges and revives (50% HP, shield); deny
  'money' otherwise. Channel state/UI removed; sub-frame F taps are latched in `main.ts`.
  E is the special ability (`secondary`), not interact.
- Money (`balance.ts`): income about 2.5x (rewards 25/20/80, head 10, start 150, wave clear
  100+50w, thresholds 250, boss $10 per 1%, possessed 300/100); prices about 1.2x (chip
  175+25 max 350, recruit 300, heal 75, revive 150+50 max 400). Harness, solo pistol median:
  earned at boss 1512 -> 3406, bought 2 recruits + 4 chips -> 2 + 8. Table in docs/PVE.md.
- Animation/hit timing/hitboxes/damage/cooldowns/movement/networking: unchanged (wire fields
  unchanged; `channel` is simply never set). PvP untouched; pvp-golden passes.
- Checks (Node 22.22.2): check:types pass; full suite 913 tests, 900 pass, 13 fail = the known
  set; `node tests/run-network.mjs pve` victory; balance harness h=1,2 pistol/rifle seeds 1-5
  before/after; real app (Vite, offline TEST BOSS, SwiftShader ~20 fps): one F tap revived a
  downed party bot and charged $150; holding F 3 s revived once; too poor = no revive.
- Not run: Windows build, hosted multi-browser play. Risk: owner judgement of the new numbers.
  Status: implemented on branch; not merged; not released.
## 2026-10-03 — Claude — more damage/health chips, green/red chip stat lines, PvE money chips

- Branch `claude/pve-chips` from `213f62b` (PR #30 head, `claude/pve-fix-desync-holdchip`); no PR opened.
- Owner request: more damage and health chips; chip cards show the description first, then
  positives in green and negatives in red (for example "+100% Health"); money-gain chips in PvE
  (boss fight only).
- 29 stat chips (`src/shared/power-chips.ts`, added to `CHIPS`): 15 damage (flat, per class,
  close/far range, finisher, crit/backstab, glass-cannon trade-offs) and 14 health (flat/percent
  max health, regeneration, life steal, damage reduction, tank trade-offs), common to legendary.
  None are mythic, so the rainbow melee pool test keeps its size. All run through `statsFor`.
- `src/shared/chip-stats.ts` builds per-copy stat lines from chip effects (old chips get them too),
  or uses explicit `Chip.stats`. `chipCard` renders `<ul class="chip-stats">` after the description,
  and the aria-label adds the lines with "(penalty)". Board tooltips show the same lines.
- PvE money chips (`src/shared/pve/money-chips.ts`, 6 chips from common to legendary) are not
  in `CHIPS`, so PvP never sees them. For human pilots, an ordinary (non-rainbow) PvE pick has a 30%
  chance to swap its last offer for one (`withMoneyOffer`, seeded economy stream). `Economy.grant`
  scales earnings through `moneyWithChips` (one line; refunds are unchanged). Party bots skip them.
- `tests/pve-foundation.test.ts`: the offerChips base-hash check now runs on the base catalogue
  (new chips removed temporarily), so it still proves the default path is unchanged.
  New `tests/power-chips.test.ts` covers this work. New Lucide icons were generated via `icons:generate`.
- Checks (Node 22.22.2): `npm run check:types` passes; full suite 905 pass / 13 fail, which is exactly the
  known Node 22 failure set; pvp-golden unchanged (PvP traces draft no chips). Playwright shots: draft 1600x900 and 1280x720, PvE start screen,
  money and mixed offer screens (session scratchpad, after/pve-chips).
- Risks: PvP draft pools grew, so offer odds per existing chip are lower. Balance numbers have
  not been playtested. Picks now draw extra economy RNG, which shifts seeded PvE runs.
  The draw comes after the catalogue offers.
- Status: implemented on branch; not merged; not released.
## 2026-10-03 — Claude — camera snaps ("pulls another direction randomly and instantly")

- Branch `claude/pve-camera` from `claude/pve-fix-desync-holdchip` `213f62b`; no PR (orchestrator). Not merged, not released.
- Finding: the live camera's yaw/pitch are purely client-local (only pointer-lock `mousemove` changes them in play; the
  server, prediction, holds, revives and teleports never write them; cutscene/spectator/killcam are separate cameras).
  So a sudden turn in play can only come from a mouse delta. Chromium pointer lock (Windows/Electron) is known to
  report bogus single huge `movementX/Y` on the first move after a lock is (re)acquired (Esc→Resume, chip screen,
  board, alt-tab) and as rare lone spikes; main.ts applied every delta unfiltered. The Windows trigger itself was not
  reproducible here (Linux headless). Position prediction was also checked in scripted offline boss runs (solo and
  3 party bots): no snaps, corrections ≤0.44 m eased.
- Fix (client input sanitising, all modes, no feel change in normal play): `src/client/mouse-look.ts` `MouseLookFilter`
  drops >48 px deltas on the first event / first 120 ms after a lock, and a lone >260 px event that is also >5× what
  the recent hand speed predicts (a same-direction follow-up confirms a real flick). Wired in `main.ts` mousemove and
  `pointerlockchange`. No hit timing, hitboxes, damage, cooldowns, movement or networking changes.
- Checks: `tests/mouse-look.test.ts` (new, 7 cases); real app (Playwright, synthetic locked mousemoves): fresh-lock
  300 px jump and a 900 px spike ignored, 10 px tracking and a 1620 px ramped flick applied exactly; check:types,
  pvp-golden and the full unit suite (failures = the known Node 22 set). Not run: Windows/Electron, real mouse.
- Follow-up (owner: "exiting chip selection ... seems to make us look upward"): consistent, same mechanism. Closing
  the chip screen re-locks; Chromium warps the cursor from the click (Choose later / cards sit below the centre) to the
  lock centre, read as a move up. Chromium sets `pointerLockElement` before `pointerlockchange`, so the warp could reach
  the filter before it knew of the new lock. The filter now starts a fresh lock at the first locked event after an
  unlocked spell (`track()`), settle window 250 ms. Nothing resets or changes pitch while the screen is open. Real app with
  emulated lock + warp (0,−180 px): base `213f62b` pitched up 0.32 rad per close (to the 1.1 clamp); `07f91fd` still
  missed the Esc close; now 0 on Choose later click, Esc and card pick; aiming after re-lock exact.
- Next: owner playtest on Windows; if snaps persist, consider `requestPointerLock({unadjustedMovement:true})` (raw input;
  removes OS acceleration, so a feel change to decide).
## 2026-10-03 — Claude — PvE Tab party board (no rounds/sets; kills, deaths, money)

- Branch `claude/pve-tab-board` from `claude/visual-revision` `6fd525c`; no PR (orchestrator). Not merged, not released.
- Owner feedback: the Tab board in PvE still counted rounds/sets. In a boss run (`snapshot.pve`) `PlayerBoard` now renders
  `src/client/pve/board.ts`: stage heading (wave n/3 + adds left, boss phase n/4 + health, intermission, victory/defeat),
  run clock, party totals, one scoreboard row per party member (humans and party bots; recruits nested with HP) with
  status/HP, kills (+heads), deaths, assists, revives, add and boss damage, money now and earned, weapon and chips (same
  hover inspection). Live cells swap in place so chip hover survives updates. HUD nav button reads Party in PvE.
- Authority: all stats were already live in the party rows; only the run start was missing, so the director view gains
  optional `startedAt` (PvE-only field; PvP snapshots carry no `pve`, pvp-golden unchanged). Fixture base sets it.
- PvP board path unchanged (test pins its markup hash from `6fd525c`). No combat, timing, hitbox, movement or network
  behaviour change.
- Checks: check:types pass; new tests/pve-board.test.ts 5/5; pvp-golden, pve-ui, pve-contract, pve-director, model-ui,
  pve-foundation pass; full suite 904 pass / 13 fail = the known Node 22 list exactly. Playwright (SwiftShader) real
  offline Test Boss Tab board plus fixture wave/boss states at 1920x1080 and 1280x720 inspected. Not run: Windows build,
  network suites, multiplayer playtest.
- Risk/next: at 720p with four members and many chips the board scrolls. Owner playtest of the board.

## 2026-10-03 — Claude — PvE playtest fixes: offline desync after Esc, taken chips wait as pending picks

- Branch `claude/pve-fix-desync-holdchip` from `claude/visual-revision` `58f5d7b`; no PR opened (orchestrator). Not merged, not released.
- Root cause (owner: shots "meters behind", rubberbanding, `pendingInputs` 63 at 7 ms RTT, starting at Esc): while the
  offline boss run was paused the client kept sending one idle input per tick to the paused worker; after Resume the
  host consumed them one per tick behind the live inputs, a permanent ~1 s backlog. Real app (pre-fix): 62 queued inputs
  consumed after a 4 s pause. Scripted 60 Hz run (new test, control case): pending 81–92, median lag 3–3.5 m, p95 ~8 m.
- Fix: `LocalRoom.send` drops fight inputs while paused; `main.ts` syncs the worker pause before stepping. After: pending
  ≤2, p95 predicted-vs-authority 0.000002 m (max ≤0.45 m from contact with bots/boss). PvE only: pointer lock lost while
  playing (browser Esc) opens the game menu and pauses the offline run; Resume / Esc re-lock; menu text says the boss test
  is paused. Taking a dispensed chip never opens the chip screen (pending pick, `C`/CHIPS icon); the client option is
  removed, `prefs` is a server no-op for published clients. No change to hit timing, hitboxes, damage, cooldowns,
  movement rules, snapshot format or PvP.
- Checks (Linux, Node 22.22.2): `check:types` pass; PvE tests + `pvp-golden` 163/163 (+2 new in
  `tests/pve-local-input-sync.test.ts`); full suite 912, failures equal the 13 known Node 22 names; `run-network.mjs pve`
  and `pve-loopback-network.ts` pass; Playwright/SwiftShader app check of Esc → menu → Resume (no inputs posted while paused).
- Not run: Windows desktop, real pointer-lock Esc (headless has none; whether a re-lock without a click is granted is up
  to Chromium), online PvE, packaging. Risk: a worker stall >100 ms still drops sim time (shared PvP clock, unchanged).
- Next: owner playtest on Windows (Esc → menu → Resume, long run with party bots); then merge/publish decision.

## 2026-10-03 — Claude — PvE Siren TEST BOSS merge and test-channel publication

- Owner authorized merge and test publication ("When this is complete, please merge and publish to test").
- PR #28 merged into `claude/visual-revision` at `2bec705ef5aaff6efb890ba8ae848d1175a646f3`; tree identical to the
  tested head `6410e3d`. CI `verify` (Node 24.19) matched the recorded baseline only: units 898/910 with exactly the 12
  names, the four server tsc errors, the WebSocket rematch timeout; HTTPS passed.
- Candidate build at the merge commit (`candidate.py --test-only --channel`, Node 24.19.0/npm 11.9.0): same baseline-only
  `verify`; repeat build byte-identical; actual payload passed (102 files, skip, offline/corrupt refusal, settings
  sentinel, URL restriction); packaging passed. Payload audit vs `0.2.177d472cf940`: only the client/host-worker bundles,
  CSS, `index.html`, `server.mjs` and `release.json` changed; no key-pattern matches outside the embedded Rapier WASM.
- Published content `0.2.65597ffe7165`, build `5ec47234ba96cc77d4eb2b7a`, payload `arena-1a9e8da3344ad6c7.json.gz`
  (23,244,770 bytes, SHA-256 `1a9e8da3344ad6c7a0d6203dcf29d142903ab7988e3c58f68a90b203cb99a546`), channel commit
  `de5cc4d`. Live feed and Site unchanged.
- Real network: the real launcher updater upgraded an isolated install `0.2.177d472cf940` → `0.2.65597ffe7165` on the
  first check; the second check skipped.
- Not run: native Windows Update button, GPU look/fps, real multi-machine play, desktop Host online.
- Status: merged and published to TEST. Next: owner playtest; retune `src/shared/pve/balance.ts` from feedback.

## 2026-10-03 — Claude — PvE Siren TEST BOSS: streams integrated (S7)

- Owner request 2026-10-02 (first PvE mode: Siren TEST BOSS on Ward Zero; spec and design in `docs/PVE.md`).
- Branch `claude/pve-integration` from `claude/pve-foundation` `2484995` (base `claude/visual-revision` `b6d2f58`,
  unchanged at integration time). Merged with `--no-ff`, no conflicts: horde `4210963`, director `0d97649`, support
  `bbb1354`, horde visuals `da32e28`, world visuals `54eb934`, UI `6d62613`. No PR yet; not merged into the shared branch.
- Streams (all on this branch): F foundation (contract, balance, seeded rng, Ward Zero layout, boss chip track, fixtures,
  `PveSystem`/`PveVisuals` facades, gated hooks, golden PvP trace); S1 horde (nav flow fields, vent/door/cell entries,
  swipe/lunge/smash, limbs and gibs, corpses, vent travel, possession, assists); S2 director (opening, 3 waves,
  intermissions, holds, reveal → intro → boss, floors, possession, grow, no cooldown, victory/defeat); S3 support
  (money, chip/recruit/med machines, personal picks, rainbow, revive/respawn/self-revive, recruits, party bots); S4 UI
  (TEST BOSS picker and Host online, HUD money/party/boss bar, personal chip screen with under-attack blink, Siren
  reveal, results, offline worker room, hello/rejoin, debug ops); S5 horde visuals (crowd LOD and impostors, swipe,
  crawl, limp, vent climb/dive, door walk-out, black blood and flame); S6 world visuals (vent lids, door leaves, cell
  gates, machines, pickups, holograms, revive beacons, grown-boss FX scaling).
- S7 changes: cross-stream requests (test seed-era assertions, door `leaf.z` 26.8, vent exit = climb stand point and
  `nearVent` 2.0, contract `pendingHiresOf`, renderer → horde graphics mode, `bossIntroExtra` 1.8 s, fixture revive
  target, `tests/fixtures/pve-horde-60.json`); fixes: stale add body handle after a kill (Rapier panic), flow-field
  seeds for players in the 2 m service corridors (stalled runs), PvE-only snapshot clock catch-up below 20 fps
  (combat numbers froze); new debug op `boss-damage:n`; balance retune (waves 22/28/34 at .5/.6/.7/s, shambler 6,
  runner 4, boss `damageScale` .2-.26, `hpPerExtraMember` .5); new tests `pve-flow`, `pve-integration`, balance
  harness `npm run test:pve-balance`, real-server suite `run-network.mjs pve`, relay/loopback boss extensions.
- Systems touch (Astra's files): the foundation's gated hooks in `simulation.ts`, `chaos.ts`, `siren.ts`,
  `siren-contract.ts`, `siren-motion.ts`, `bot-abilities.ts`, `standard-melee.ts`, `stage-hazards.ts`, `movement.ts`,
  `intros.ts`, `config.ts`, `content.ts`, `host-room.ts`, `server/index.ts`, client `host-worker.ts`, `browser-room.ts`,
  `renderer.ts`, `snapshots.ts`, `overhead-health.ts` (edit map in `docs/PVE.md`). S7 added one line each in
  `renderer.ts` (graphics mode) and `snapshots.ts` (PvE-gated step cap). S1-S6 did not edit `src/shared` outside `pve/`.
- Animation impacts: PvP unchanged (golden trace identical). PvE adds: add attack hit times from `PVE_TIMING`
  (swipe/lunge .34 s, smash .43 s) shared by authority and poses; add hitboxes are capsules (crawlers smaller), limb
  hits by point; add damage per `balance.ts` (retuned); the grown Siren's capsule and reach ×1.5; phase 4 removes boss
  cooldowns (no Last Laugh floor, D35; concurrency caps kept); humans are frozen while their chip screen is open (D18);
  networking adds `snapshot.pve`, `{type:'chip',pve}` commands, hello-gated ready and keyed rejoin.
- Checks (Linux, Node 22.22.2): `check:types` pass; full units 909/896, exactly the 13 baseline failure names;
  `pvp-golden` identical; `pve-flow` (deterministic victory twice, defeat, debug flow; `PVE_LONG=1` pass); network
  suites match baselines (default rematch timeout, chaos cosmetics, block 65.5≠77 known; others pass) plus new `pve`
  pass; siren/killcam/body-damage/https/pve-loopback/pve-relay pass; `test:pve-perf` ok (0.51 ms/step, 49.9 KB p95);
  server build only the 4 known errors; balance table in `docs/PVE.md`; offline real-app smoke (Playwright/SwiftShader)
  36 screenshots through victory, Retry and defeat.
- Not run: native Windows GPU look/fps, real internet multi-machine play, desktop Electron Host online, Site browser
  hosting, hardware audio, hosted two-browser smoke (the client cannot open test rooms, so no debug ops), Node 24.19.
- Risks: balance is harness-tuned (bot humans do not dodge; boss mostly wins against them); melee bots weak in crowds;
  large hordes on low-end GPUs (R1); open chip screen can cover the boss intro if never picked.
- Final independent review (fuzzed 15 seeds × up to 10 min, directed sims, real-app PvE→PvP, network suites): one
  blocking bug fixed — Retry soft-locked in weapon select when a party member had disconnected (`start()` now drops
  disconnected humans in PvE; regression test `tests/pve-retry-disconnect.test.ts`). After the fix: full units 910/897,
  exactly the 13 baseline names; types pass. Known minor: a cinematic hold cancels an in-progress revive channel
  (no charge; press F again). The Siren chip tray (smile, chip card, ??? cards) was confirmed rendering in the app.
- Status: implemented on branch. Next: PR into `claude/visual-revision`, merge and test-channel publication
  (owner-authorized 2026-10-02: "When this is complete, please merge and publish to test").

## 2026-10-02 — Claude — chest-up chip draft merge and test-channel publication

- Owner asked to merge and publish the chip-selection change for the in-game Update button.
- Merge:
  - PR #26 merged into `claude/visual-revision` at `fd2f745b8c84f7ebb72d365f63aeef9daa0cb707`.
  - The merge tree `ef1a268` is identical to the tested PR head `f79072f`.
- Build: `candidate.py --test-only --channel` at the merge commit (tooling `claude/test-update-channel` `fd13d5a`;
  Node 24.19.0, npm 11.9.0).
  - Passed: setup, fixture tests, updater tests, runtime pins, build, repeat build (byte-identical payload),
    actual payload (102 files, same-version skip, offline/corrupt refusal, settings sentinel, URL restriction),
    and Windows/desktop/isolated packaging.
  - `verify` matched the recorded baseline only:
    - units 728/740, exactly the 12 recorded names (portable-update passes on 24.19)
    - the four server tsc errors
    - the WebSocket rematch timeout
    - HTTPS integration passed
- Payload audit against `0.2.58d079f38769`:
  - Only the client JS/CSS bundles, `index.html` and `release.json` changed.
  - The minion module is not in the bundle.
  - The only key-pattern matches are inside the embedded 2 MB Rapier WASM base64, identical in the previous payload.
- Published content `0.2.177d472cf940`, build `d47c040895b0bfc483017fa2`, payload `arena-50e93185d7def3f6.json.gz`
  (22,978,928 bytes, SHA-256 `50e93185d7def3f6f07e1928cc55ad8ff3ef1a9197d70c03d87cbdd5d3f647fc`), at channel commit
  `7a9ff3d`. Live feed and Site unchanged.
- Real network: the real launcher updater in an isolated install upgraded `0.2.19e114ae7c87` → `0.2.177d472cf940`
  from the public channel on the first check, and the second check skipped (up to date).
- Not run: the native Windows Update button, GPU visuals.
- Status: merged and published to TEST. Next: owner playtest of the draft. Siren PvE TEST BOSS mode is in progress
  on a separate task branch based on this merge.

## 2026-10-02 — Claude — chest-up chip draft (ROUNDS-style) and Siren minion design

- Owner request: (a) in the chip draft, zoom up to the picker chest-up, like ROUNDS card picks. Everyone gets the
  same pose except bosses, which keep their own pose but are still framed close. (b) Begin designing Siren minions:
  procedural zombie Pilots (rot, black-flame burns, wounds), friendly to the Siren and its team; spawn logic comes
  later. Owner face decision: blank by default, some with a cracked head leaking black flame, no burned-in eyes.
- Branch `claude/visual-revision-draft-minions` from shared `claude/visual-revision` `433202a`. It merges two task
  branches:
  - `claude/visual-revision-draft-closeup` at `b4626e6`
  - `claude/visual-revision-siren-minions` at `d80388a`
  Each was implemented, adversarially reviewed and fixed in its own worktree.
- Chip draft (`draft-stage.ts`, `draft-view.ts`, `draft-screen.css`):
  - Layout: the cards sit in a tray at the top, with the picker filling the screen below. Framing adapts to window
    size, phone portrait and two card rows.
  - Pose: `draftPose` gives every non-boss Pilot the same calm pose with the weapon hidden. `isBossSpec` (currently
    `siren`) keeps the boss's own animation, framed chest-up.
  - Reach: the arm on the hovered card's side reaches along a curved wrist path. The head and chest turn to the card.
  - Grab: still keyed to the authoritative `chosenAt`, continuing from the hover reach.
  - Turn start: a push-in on each turn (none with reduced motion).
  - Pick moment: the tray height is held across the pick, so cards no longer jump. The no-WebGL fallback centres
    the tray.
  - Draft lab: dev-only `tools/draft-lab`.
  - No markup ids, network, preference or authority change.
- Siren minions (`src/client/siren/minions/**`, `docs/SIREN-MINIONS.md`, lab `kind=minions`): design only. Nothing in
  the game imports the module, so the bundle and gameplay are unaffected.
  - The seed corrupts a standard Pilot rig with rot, black-flame burns, carved wounds, missing parts, dying violet
    glow and weapon scraps.
  - Faces: blank by default; about 25% of seeds are cracked; `eyes` appears only on request.
  - Animation set: shamble, frenzied run, idle twitches, lunge/club/smash, flinch, black-flame-pool emergence, and
    burn-away death. Planted feet hold through turns; the step phase is rate-independent; death rests on the floor.
  - `prewarmSirenMinions(budgetMs)` spreads layout building over frames.
  - The doc proposes (does not implement) a systems contract: a chaos-minion kind `siren-add`, which reuses the
    existing owner/team friendliness, plus spawn rules on `SIREN_ADD_SPAWNS`. Systems files (Astra) are untouched.
- Hit timing, hitboxes, damage, cooldowns, movement rules and network behaviour are unchanged.
- Checks (Linux, Node 22.22.2) on the integration merge `27da229`:
  - `npm run check:types` passed.
  - Full `node --import tsx --test tests/*.test.ts`: 740 tests, 727 pass. The 13 failures are identical to the
    recorded Node 22 baseline (12 names plus portable-update).
  - New `tests/draft-closeup.test.ts` 11/11 (uniform pose, boss exception, framing per aspect, reach/look, 30/60/120 Hz
    agreement, pick keeps the tray still).
  - New `tests/siren-minions.test.ts` 11/11 (determinism, rig contract, budget, face pool, planted feet on turns, floor
    contact, prewarm, no Math.random).
  - Reviewers' Playwright measurements on the real app (Developer Sandbox Preview Draft) at 1920x1080, 1280x720,
    1024x768, 844x390 and 390x844, plus SwiftShader renders of the minion gallery, strips and Ward Zero horde.
- Not run: native Windows/GPU look and frame rate, a real online draft (hover is local, so others see the scan),
  the Siren as the picker in the real app (lab only).
- Risks:
  - The pilot is small with two card rows on 720p.
  - The reach reads as a raised hand beside the head.
  - Minions cost about 50 draws each and about 11k triangles; the doc suggests a cap of about 12 live, with shadows
    off at range.
  - Emergence needs solid floor at the spawn point.
- Next: owner review of the draft feel and the minion gallery. Then spawn/AI/friendliness systems with Astra on the
  doc's proposed contract.

## 2026-10-02 — Claude — Ward Zero rebuilt as a sealed, music-synced hall

- Owner request: revise Siren's stage. It should be a closed-off place (no open world), a big building for a "crazy"
  demon, with better design, still reactive to the music including hazards, and planned for adds spawning with the
  Siren and players running around to grab chips. Branch `claude/sleepy-ritchie-g8ruhv`, based on the shared
  `claude/visual-revision` `a9269ff` (plus the previous publication log).
- Shared layout (`siren-stage-layout.ts`, Claude-owned contract):
  - `SIREN_HALL`: walls reach a 16 m roof, and the roof is a collider.
  - 20 wall columns (`SIREN_COLUMNS`); the four pylons now run to the vault.
  - New data-only hooks: `SIREN_ADD_SPAWNS` (four cells and four ward doors, each with an inward yaw) and
    `SIREN_CHIP_SPOTS` (six pads on the running loop). Nothing spawns from them yet.
  - Cell cabinets moved out of the cells.
  - Partitions, cells, furniture, vents and `sirenStageHazards` are unchanged. 72 props (was 51), 18 breakable.
- Systems touch (Astra's file, coordinate here): `src/shared/siren.ts` `ground()` clamps its ray origin under the
  roof on map `siren`, so effects aimed at the vault land on the floor. No hit timing, hitbox, damage, cooldown,
  movement or network rule changed; vents keep their schedule, damage and launch.
- Client:
  - `WardZeroVisuals` was rewritten from the shared layout: an asylum great hall turned concert hall, with organ,
    face and rose window, chained great doors, galleries of patients, backwards clocks, and a vault with fan,
    chandeliers, work lights and speakers.
  - All of it is song-locked, and the vents still read `sirenStageHazards`. The beacon spin is now integrated per
    frame, so section changes no longer make the beams jump.
  - MapScenery and NightWorld draw no sky, skyline, trims or grid for theme `siren`. They apply `WARD_ZERO_AIR`
    (haze, overhead key light) and restore the old key position elsewhere.
  - The roof prop casts no shadow. `sirenStage.shell(props)` cuts the near walls and roof away for an outside camera
    (menu overview).
  - Dressing costs about 177 draws (old ward about 257) and 5 point/spot lights (was 4).
  - The lab has new stage views. `docs/SIREN.md` gained a Ward Zero section; screenshot `docs/ward-zero-hall.jpg`.
- Checks:
  - `npm run check:types` passed.
  - New `tests/siren-hall.test.ts` (2) and `tests/siren-stage-visuals.test.ts` (4) passed; the extended
    `siren-contract` passed.
  - Every `tests/siren-*.test.ts` passed, and `tests/siren-network.ts` passed (89 identical snapshots).
  - Full `npm test` on container Node 22: 705/718. The failures are the 12 recorded baseline names plus the
    portable-update test, which fails only on Node 22 and is re-checked on 24.19 in the publication run.
  - Visual: lab sheets at song 50/82/152, plus the real `ArenaRenderer` driven by a local Simulation with an
    authorised Siren and four bots. Checked: build, groove, lift telegraph/eruption, breather, climax, doors,
    galleries, vault and menu overview.
  - Headless SwiftShader only. Native Windows/GPU look, frame rate and audio sync are not checked.
- Risks:
  - Bots still have no pathing and ignore vents.
  - Adds and chip pickups need systems work (spawn and grant logic, minion vent damage) that uses the new hooks.
  - Cell add spawns sit about 2.4 m from the back wall, so multi-unit `summon` offsets need a clearance check.
- Merge and publication (owner asked to merge into the DEV-button branch; standing test-channel instruction):
  - PR #24 merged into `claude/visual-revision` at `5af891cec6b06c2e04fad34d43d3027ec6826964`, tree identical to
    the tested `75a25b9`. That merge also carries the previous chip-draft publication log and Astra's PR #23 log.
  - CI and a clean Node 24.19.0/npm 11.9.0 `npm run verify` match the recorded baseline only: units 706/718 with
    exactly the 12 names (the portable-update test passes on 24.19), the four server tsc errors, and the WebSocket
    rematch timeout. HTTPS integration passed.
  - Two `release:prepare` builds, plus a third at the merge commit, were byte-identical.
  - PR #5 `check-payload` passed (102 files, skip, offline/corrupt refusal, settings sentinel, URL restriction);
    channel/status tests 6/6.
  - Payload audit against `0.2.0a02ae6804bc`: only the client bundles, index, server and release files changed. The
    one key-pattern match is the embedded Rapier WASM base64, also present in the previous bundles.
  - Published content `0.2.58d079f38769`, build `0e75b40bf0eb4d6e09780ae6`, payload `arena-66974bad66385f57.json.gz`
    (22,975,402 bytes, SHA-256 `66974bad66385f5744f25439b8153c776b9ed27b3b278cb4268c897c9cc5b8ce`), at channel
    commit `d5f371486bc8934499fb8af598b346e10b87bb66`. Live feed and Site unchanged.
  - Real network: the isolated install upgraded `0.2.0a02ae6804bc` → `0.2.58d079f38769` from the public channel on
    the first check (102 files verified, settings preserved, same-version skip, menu status).
  - Not run: the native Windows Update button, GPU visuals and audio.
- Status: merged and published to TEST. Next: native look and frame-rate review of Ward Zero, then systems work for
  adds and chip pickups on the new hooks.

## 2026-10-02 — Claude — chip-draft merge and test-channel publication

- Owner asked to merge the chip draft and publish it for the in-game Update button. PR #22 merged
  `claude/sleepy-ritchie-g8ruhv` into `claude/visual-revision` at `a9269ff44e0a14fd245d7e6de9a1da8ffc75883b`
  (tree identical to the tested branch, after merging the shared head `2b436fe` incl. Astra's kill-effect fix).
- Published content `0.2.0a02ae6804bc`, build `d6c50207790e5a10bc39dbc9`, payload
  `arena-d03e12bc009aee7c.json.gz` (22,963,995 bytes, SHA-256
  `d03e12bc009aee7cee056be98f5bce7b59accea1e85eef2843eacc47b106e07c`) at channel commit
  `bc845295ba6d32cdd5edbc4f6b29a123eab36462`. Live feed and Site unchanged.
- Clean detached worktree, Node 24.19.0/npm 11.9.0, `npm run setup`. `npm run verify` nonzero on the recorded
  baseline only: units 697/709 with exactly the 12 names, the four server tsc errors, WebSocket rematch
  timeout; HTTPS integration passed. Two `release:prepare` builds byte-identical; PR #5 check-payload passed
  (102 files, skip, offline/corrupt refusal, settings sentinel, URL restriction); channel/status tests 6/6.
  Payload audit vs `0.2.c9064e15755f`: only the client bundle/CSS, index, server and release files changed; one
  key-pattern match is inside the embedded Rapier WASM and identical in the previous payload.
- Real network: `0.2.c9064e15755f` installed from the channel into an isolated folder, then upgraded to
  `0.2.0a02ae6804bc` on the first manifest check (102 files, settings sentinel, same-version skip, menu status).
  Native Windows Update button and GPU visuals not run. Exclusive-tier draft cinematics were checked in the
  browser with the real markup (shortened spotlight ends with each server reveal).

## 2026-10-02 — Astra — kill-effect persistence test publication

- Owner authorized merge, publication and any needed Update-button fix. PR #21
  merged at 2b436fe7c73f832aeba49679583a59fbcddc4c77. Published that exact tested
  source as 0.2.c9064e15755f (build 2febd9d796681c42028cfeba) to the existing TEST
  channel at e427c71726ea6fac98d985915b2a1d527b07571e. The later concurrent PR #22
  draft-screen merge is not included in this package; preserve it in future builds.
- All tracked source blobs matched the approved remote tree except regenerated
  build identity. Two release:prepare builds produced identical feed bytes.
  Payload arena-217607dd6f5fb52a.json.gz: 22,954,568 bytes, SHA256
  217607dd6f5fb52a7d29f813d9de7410723d71788673ce874fc3f5233eaa20d1.
  Browser upload used the owner's existing fallback authorization; downloaded
  payload hash matched before atomic manifest activation and old-payload removal.
- Actual payload check passes: 102 installed file hashes, settings preservation,
  same-version skip, offline/corruption refusal. Remote channel/status tooling 6/6.
  Real public-feed install from 0.2.95b7f0a5f044 passes all 102 file hashes,
  settings sentinel, same-version skip and menu status. Client updateStatus reports
  available for the old build and current for the new build. No button code change
  needed. Public-feed test uses curl transport to accommodate this environment's
  previously observed Node/proxy timeout; native Electron click/restart unrun.
- The previous entry records the exact code checks and unchanged baseline failures:
  12/12 focused, types/client build pass; broad verification nonzero on known unit,
  server TS and generic WS failures; HTTPS passed. No need to repeat unchanged
  broad gameplay suites for publication alone.
- Status: merged and published to TEST, live feed/backend unchanged. Select
  Impact ragdoll & gibs and Save Pilot once after updating. Protection is local
  per account/device until the account service supports that effect. Native
  Windows selection/restart and cross-device acceptance remain unverified.

## 2026-10-02 — Claude — full-screen 3D chip draft and a longer kill-cam pre-roll

- Owner feedback after the kill-cam test update: let the replay start a little later, and rework chip
  selection into a full-screen scene with the pilot. Branch `claude/sleepy-ritchie-g8ruhv`, merged with
  shared `claude/visual-revision` `7d31530` first. Not merged into the shared branch; not published.
- Kill cam: new `KILLCAM.hold` (.6 s). Playback now starts ~1.2 s after death (aftermath + hold); the
  stalled-packet deadline grows by the same amount, and the full clip still fits the 5 s result. Tests derive
  their timing from the constants, and a new test covers the hold. Lifecycle/cancellation unchanged.
- Draft: `draft-stage.ts` renders the current picker's dressed rig on a podium through the main renderer
  (PlayerIntros pattern) while the phase is `draft`. `draftScreen` (draft-presentation.ts) replaces the panel with
  a full-screen overlay: heading/turn order on top, the picker's nameplate, and the offers as a horizontal tray
  (one row up to six; two rows for 7-12; mid-width screens split six as 3+3; phones and short landscape windows
  use one sideways-scrolling row). Cards stay real `<button data-chip>` chips (compact size), so the click path,
  readyAt gate, aria labels and existing markup tests are unchanged.
- Look and grab: `draft-view.ts` maps card centres onto a plane in front of the pilot. The head/neck/chest turn
  to the hovered or focused card on your own turn; otherwise they follow the boot-in, then a slow scan. On a pick
  (driven by authoritative `draft.chosen/chosenAt`, so bot, timeout and other viewers' picks animate too), the
  others shatter, the card flies (fixed-position copy) into the projected palm while the hand on that side
  reaches (rig-ik `hand`), a glowing 3D chip lands in the palm at .48 s with a rarity flash and the install
  sound for every viewer, and the existing details spotlight with all rarity cinematics opens at .85 s (inserted
  on the sim clock, duration shortened to end with the 3.2/4.6/4.8 s server reveal). Emperor/Ansatsuken
  cinematic keyframes now follow `--selection-duration`. Arrow keys move focus, Space/Enter pick.
- Set losers draft at hp 0, so the stage poses a cleaned-up `stageView` (alive, idle), including Siren. The
  arena keeps updating underneath but is not drawn; compatibility (SVG) rendering keeps the old arena backdrop
  with a dark card backdrop. No sim, network, protocol, timing, hitbox, damage, cooldown or save change.
- Checks (Node 24.19.0/npm 11.9.0): types pass; full units 689/701 with exactly the 12 recorded baseline names;
  new `tests/draft-screen.test.ts` (7: rows, timeline inside the reveal, own/other markup + escaping, taken slot
  and spotlight gating, look/reach accuracy and bone lengths, dead-picker staging) and the kill-cam hold test
  pass; `tests/killcam-network.ts` passes (87 snapshots); Vite build passes. Independent review (3 lenses)
  found the row clipping the flying card, arrow keys swallowed for dead pickers, dead-picker posing, gaze before
  IK, per-pick hand latch, contact-sound dedupe, light-count shader hitch, reduced motion, online clock and
  row scroll reset; all fixed here.
- Rendered (headless Chromium/SwiftShader, sandbox draft preview): 1-12 offers at 1920x1080, 1366x768,
  1280x720, 960x540, 844x390 and 390x844; hover look-at; grab with either hand; spotlight hand-off; a guest
  watching a host's pick in a real WebSocket test room. `docs/chip-arena-draft-stage.jpg`.
- Not verified: native Windows/GPU frame pacing (software GL), exclusive-tier (Mishima/Ansatsuken/L44T/Siren)
  grabs, audio levels, two-PC play. Hover is local only (other viewers see the scan, as before no protocol).
  `tools/chip-lab` still renders the old panel markup.

## 2026-10-02 — Astra — preserve kill effects across legacy account sync

- Branch codex/kill-effect-save from shared 16f4bcc9f4ea71ab165d462b13e48fd5a6f1372f.
  Owner reports Impact ragdoll & gibs periodically changing to Voxel breakup,
  including the customization dropdown.
- Evidence: currently served /chip-arena-source.zip at AUTH_ORIGIN has an older
  cosmetics catalog without impact-ragdoll; its sanitizer falls back to voxel-burst
  and account savedProfile/view both use that sanitizer. This archive is evidence
  of the published site's version, not a direct production DB/worker inspection.
  Current desktop code accepts impact-ragdoll, but account loads overwrite local
  cosmetics and saves previously reported success without checking the returned choice.
- Added account-scoped pending kill-effect preference. Explicit local selections
  and save attempts survive an older server's unchanged fallback and failed requests.
  A successful acknowledgement or a different later cloud effect clears the pending
  value. Current access checks still reject revoked/unauthorized DEV effects.
  Save messages disclose when the cloud did not retain the effect; other profile
  fields retain their existing cloud behavior. No combat/network/save-server schema
  changes, credentials, production account mutations or backend deployment.
- Verified: Node 24.19.0; types and 12/12 focused sync/account tests pass; client
  Vite build passes. Full units 690/702: same 12 failure names as published baseline,
  no new unit failures. Four existing server TypeScript errors remain.
  Full verify exits nonzero on those baseline failures and the known WebSocket
  rematch timeout. HTTPS passes: five sets, four drafts, 3,693 matching snapshots,
  destruction agreement, retry, rematch and guest leave (188.4 s).
- Limits: local protection is device/account scoped, not cross-device cloud support.
  Until the account backend recognizes the new effect, select it and Save Pilot
  once after this fix; a previously lost choice cannot be reconstructed safely.
  Windows dropdown/restart acceptance not run. Implemented on task branch only;
  not merged or published. Do not publish the whole browser Site to fix this issue.

## 2026-10-02 — Astra — bot intro integration and test publication

- Owner authorized merge and in-game publication. PR #18 merged into the kill-cam
  branch at 54fa61b; PR #19 integrated into claude/visual-revision at
  cc8eb637083d881a96f53699d67452d4b602e0b5. This exact source includes Claude's
  kill-cam presentation and Siren super armor plus boss-only bot intros.
- Published content 0.2.95b7f0a5f044, build 4beea896863a860cc27c9216 to the existing
  TEST channel, commit 630b9f55c83ef7235d7d78f8502af48d22978ec7. Payload
  arena-4df9cf86d6afed03.json.gz, 22,953,915 bytes, SHA256
  4df9cf86d6afed031452b5c8b59cc1867a179719290ef02408162c325d43f2c3.
- Node 24.19.0/npm 11.9.0; real Rapier and unchanged locked dependencies.
  Source files checked against remote blob hashes (build identity regenerated).
  Two release:prepare builds produced identical feeds; actual payload installation
  verified all 102 files, settings sentinel, same-version skip and corrupt/offline
  refusal. Remote channel/status tooling 6/6 passes.
- Full verify remains nonzero: units 685/697 with the same 12 documented failures,
  four known server TypeScript errors, generic WebSocket rematch failure.
  HTTPS integration passes: five sets, four drafts, 3,365 identical snapshots,
  destruction agreement, retry, rematch and guest leave. Focused 23/23 and intro
  network 192 identical snapshots previously passed.
- Connector upload exceeded its 16 MiB request limit. Owner approved browser
  fallback; payload uploaded in GitHub UI, downloaded checksum verified, then
  manifest/log activated atomically and old payload removed. Real public feed
  installation from 0.2.1aeb4fad8c90 passed all 102 file hashes, same-version skip,
  settings preservation and menu status. Default Node network fetch hit the
  proxy timeout; the public-feed check used curl as the fetch transport with a
  60-second timeout. Shipped updater and its timeouts were not modified.
- Status: merged and published to TEST; live feed unchanged. Native Windows/GPU,
  replay audio and physical two-PC acceptance remain owner checks. Existing test
  installations use the in-game Update button; CDN propagation can take minutes.

## 2026-10-02 — Astra — boss-only bot intros

- Branch `codex/boss-only-bot-intros`, remote base `codex/killcam-siren-armor`
  `9d7c218683486b8a7c6fe31f0ec1e110595e703d` (includes Claude kill-cam UI).
- Authority intro queue now excludes ordinary bots, including non-boss DEV weapons.
  Human intros remain; authorized Siren bots retain their entrance, last in order.
  Removed slots add no wait. Empty queues go directly to countdown. Global skip and
  sandbox still bypass all intros; boss music uses the shortened authoritative queue.
- Checks: Node 24.19.0, locked dependencies installed offline, real Rapier; 23/23
  focused bot-intro, existing intro, kill-cam and Siren armor tests pass. New bot
  cases rerun 3/3 after test type-narrowing correction; `npm run check:types` passes.
  Existing two-client intro network suite passed (192 identical intro snapshots,
  both human names, combat locked; host skip setting synchronized).
- No hit timing, hitbox, damage, cooldown, movement, snapshot schema or save changes.
  Native Windows visual/audio acceptance not run. Separate add-on PR targets the
  kill-cam feature branch; implemented on branch, not merged or published.

## 2026-10-02 — Claude — owner-requested kill-cam test-channel publication

- Owner asked to publish the merged kill-cam work to the existing test Update channel.
  Source `claude/visual-revision` at `7d31530cbe996dd45744e45da2442ace9eefecdb` (PR #16 merge,
  includes Claude PR #17); clean detached worktree, Node 24.19.0 / npm 11.9.0 (official tarball,
  SHA-256 checked), `npm run setup`, real Rapier 0.19.3. Live feed and Site unchanged.
- Published content `0.2.1aeb4fad8c90`, build `786c2215912319ba449a53d3`, payload
  `arena-0a306d01b2140b24.json.gz` (22,953,525 bytes, SHA-256
  `0a306d01b2140b24f4c387104e9c9ac9cf14847e1238020ee35e8cab578d2fe4`) at channel commit
  `012d37bdf6aa504f0d8b4058b9552c7936743f3a` in `sheldon37/chip-arena-test-updates`
  (manifest + single payload swapped together; PUBLISHED.md row added).
- Gate on the exact commit: `npm run verify` nonzero on the recorded baseline only. Units
  682/694 with exactly the 12 recorded failure names (the updater test that failed under the
  container's Node 22 passes on 24.19.0); four known server tsc errors (chaos.ts, leet.ts ×2,
  standard-melee.ts); generic WebSocket rematch timeout. HTTPS integration passed.
- `release:prepare` run twice: manifest and payload byte-identical. PR #5 `check-payload.mjs`
  passed on the real payload (102 files, same-version skip, offline/corrupt refusal, settings
  sentinel, URL restriction); remote-channel/status tests 6/6 in their original layout.
- Independent read-only pre-publish audit (3 agents: provenance, secrets/safety, channel
  format) found no blockers: recomputed GAME_BUILD matches; kill-cam UI and Siren armor are
  present in the bundles; every byte changed vs `0.2.c99e20077a04` traces to `97daa88`/`4e9a65d`;
  no secrets (embedded JWKs are public verify keys, unchanged); +8,244 bytes, under the caps.
  The committed `build-info.generated.ts` is stale (the build stamps it); cosmetic.
- Real network: before publishing, the then-public `0.2.c99e20077a04` was installed through
  the real updater from the channel into an isolated folder (102 files). The public manifest
  served the new version on the first check after push; the same folder then upgraded to
  `0.2.1aeb4fad8c90` (102 files verified, settings sentinel preserved, same-version skip,
  remote menu-status handler reports the new build/version).
- Not verified: native Windows EXE/Update button, GPU visuals, replay audio, two-PC play.
  Diagnostic test build with the documented baseline failures. Existing test installations
  press Update.

## 2026-10-02 — Claude — kill-cam UI, transitions and replay camera framing

- Task: `docs/KILLCAM-HANDOFF.md` UI/camera scope. Branch `claude/sleepy-ritchie-g8ruhv`
  (service-assigned name) from Astra's feature head `97daa8861211d76f730052dd092a9234b51f426d`
  (PR #16, `codex/killcam-siren-armor`). Siren armor and replay lifecycle untouched.
- Replaced the placeholder `killcam-hud.ts` with a broadcast-style frame in new `killcam.css`:
  letterbox bars, corner `KILL CAM` tag with a clock relative to the fatal hit (−2.4s → +0.3s),
  lower-third card (killer name in their colour, team tag, weapon icon/name, readable
  cause; environmental/self deaths read "You were eliminated" + cause), a timeline with a
  fatal-hit marker and red aftermath segment, and a pill Skip button (`data-action="killcam-skip"`
  kept, works without pointer lock). Nothing covers the screen centre. Names, weapon and team
  strings are HTML-escaped; colours must match `#rgb/#rrggbb` before reaching inline style.
- Transitions: bars/card/tag animate in with a short flash covering the camera cut. In the last
  0.3 s the bars, card and controls retract while the tag stays until the cut. Return to live
  spectating mounts a one-shot flash and a green `LIVE` chip (1.6 s, fight phase only). HUD is
  redrawn immediately at both boundaries (no 100 ms lag behind the camera cut). The root keeps a
  stable class/`data-death` per clip, so the 10 Hz HUD morph never restarts entry animations.
  Reduced motion: no flash/pulse, instant states. Responsive rules for ≤850 px, ≤580 px phones
  and ≤480 px-tall landscape. No backdrop blur over the 3D view (cheap at every quality).
- Developer sandbox panel and live death ticker are hidden during replay (`#ui[data-killcam]`).
- Camera: visual review found the centred third-person pivot hid the victim behind the killer
  in melee kills. Replay-only `killcam-camera.ts` moves the pivot to the recorded pilot's right
  shoulder (.85 m, wall-checked) and .22 m up, converging on the same aim point. Live,
  spectator and first-person cameras, aiming and muzzles are unchanged.
- Small systems touch (coordinated here for Astra): `KillcamInfo.at` = recorded fatal-hit time
  clamped into [start,end], for the timeline marker. Additive; sampling/lifecycle unchanged.
  No gameplay, hit timing, hitboxes, damage, cooldowns, movement or network/snapshot changes.
- Checks (Node 22.22.0 in this container; repo pins 24.19.0): `npm run check:types` pass;
  focused `killcam`/`siren-super-armor` + new `killcam-presentation` tests 18/18; 
  `tests/killcam-network.ts` passes (3 clients, 75 identical snapshots, victim-only replay,
  authority unchanged, live packets, skip, round reset, armor damage/no launch). Vite build pass.
  Full units 677/690: the 12 recorded baseline names plus `portable update ... survives offline`,
  which fails identically with this diff stashed (environmental, Node version), so no new failures.
- Rendered verification (headless Chromium + SwiftShader, local Vite + authority, ~2 fps):
  local hard-bot 1v1 death (replay into round result), 6-pilot FFA death (completion → live
  spectating with LIVE chip), Space skip mid-replay, sandbox self death (victim fallback card),
  online WebSocket room with bots (killer view, completion, spectating), performance graphics +
  first-person preference (replay forced third-person; next round after draft restored
  first-person with no replay leftovers), high graphics. Static layouts at 1920×1080,
  1280×720, 820×1180, 390×844 and 844×390; escaping probe raised no dialog.
  Screenshots: `docs/chip-arena-killcam.jpg` (online melee replay, shoulder framing),
  `docs/chip-arena-killcam-phone.jpg` (phone layouts, killer and environmental).
- Not verified: native Windows/GPU frame pacing (SwiftShader runs ~2 fps, so animation feel was
  judged from timings, not smooth playback), replay audio, physical two-PC rooms, simultaneous
  kills, killer disconnect mid-clip, lava/fall/Siren Last Laugh-specific deaths and destruction
  rewinds. Card shows the weapon held in the recorded frame, not necessarily the killing attack.
- Status: owner-authorized integration done. PR #17 (`4e9a65d`) merged into
  `codex/killcam-siren-armor` at `9d7c218`; PR #16 merged into `claude/visual-revision` at
  `7d31530cbe996dd45744e45da2442ace9eefecdb`, tree `37424fa` identical to the tested tree.
  Published afterwards on the owner's explicit request; see the publication entry above.

## 2026-10-02 — Astra — kill-cam systems and Siren ability super armor

- Task branch `codex/killcam-siren-armor`, exact shared base
  `claude/visual-revision` at `4dbb1513a2f8d6c782cfddb5b8ee91fad1dd9d47`.
  All 549 remote source blobs verified before edits. Separate local verification
  checkout; GitHub commits preserve the actual remote parent, not synthetic history.
- Added bounded client snapshot history and automatic victim-only replay: up to
  3 s before death + .6 s aftermath, recorded killer camera with victim fallback,
  Space/Esc/button skip, then live spectating. Live authority/input identity and
  network processing continue. Historical HP, deaths, destruction and attacks
  never borrow live/future state. Round/draft/respawn/lobby/disconnect cancel safely.
- Added replay renderer/audio reset boundaries and a basic isolated HUD seam.
  Claude owns final UI/camera polish: `src/client/killcam-hud.ts` and
  `docs/KILLCAM-HANDOFF.md`. No designed UI overhaul or regenerated assets.
- Siren's eight numbered abilities resist physical interruption through recovery,
  including Marked lock and Last Laugh hold. HP/DOT/lethal damage still apply.
  Guards cover normal/DEV melee, rebound, juggling, Chip forces and stage launches.
  Primary attacks and persistent rain/fire/seeker lifetimes do not extend armor.
  Damage values, attack/hitbox timing, cooldowns and costs are unchanged; forced
  movement/stun resistance changes intentionally. No snapshot/save schema change.
- Node 24.19.0/npm 11.9.0, real Rapier 0.19.3, unchanged lockfile/dependencies.
  New focused tests 14/14; typecheck and final client Vite build pass.
  Full units 678/690 versus untouched gameplay baseline 664/676: identical 12
  failure names, no new failures. Initial fixture-only TypeScript errors were
  fixed; final typecheck passes. Full `npm run verify` was rerun and remains
  nonzero: those baseline units, four known server Rapier/NodeNext type errors,
  and the existing generic WebSocket rematch timeout. Client/relay compile pass.
- Dedicated `tests/killcam-network.ts`: 3 real WebSocket clients / 75 identical
  snapshots; victim-only replay, authority unchanged, continued live packets,
  skip/reset and shared armor damage/no launch. Existing Siren network suite passes
  with 89 identical snapshots. Final HTTPS integration passes: five sets, four drafts, 3,516 shared snapshots,
  destruction agreement, retry, rematch and guest leave (179.4 s).
- Visual test blocked: Cloud Browser rejected local Vite with
  `net::ERR_BLOCKED_BY_CLIENT`; no installed local Chromium or native Windows/GPU.
  Rendered replay/transition/audio quality and physical two-PC acceptance remain
  unverified. Node-only 8-player/262-prop diagnostic: 101 retained snapshots,
  ~18.8 MiB heap delta, ~1.24 ms/capture, ~.046 ms/sample; not a GPU benchmark.
- Status: implemented on task branch for PR/Claude handoff; NOT merged or released.
  No update-feed or Site publication. Next: Claude visual/UI review using the handoff,
  owner-approved integration into the shared branch, then the existing test channel.

## 2026-10-01 — Astra — owner-authorized body damage merge and test publication

- After the verification report below, the owner explicitly requested merge and
  publication to the existing test Update channel. PR #14 merged into
  `claude/visual-revision` at `25599c4052af5f3f50457ea7491dcb01b6216076`.
  Its tree exactly matches the verified combined head `d7af2d2`.
- Published content `0.2.c99e20077a04`, build `4dea4f9ddc1b3bb217acf8f9`,
  channel commit `8bf843b2963b0575e6a13e41c10afcfa0e7fcca8` in
  `sheldon37/chip-arena-test-updates`. Manifest and payload changed atomically;
  PUBLISHED.md records the source commit. Live feed and Site were not changed.
- Clean release directory: all 549 source blobs verified against the merge tree.
  `npm run release:prepare` passes twice with byte-identical manifest/payload.
  Payload: 22,945,281 bytes; SHA-256
  `aafb58f58c54cb5245694beb127d4d328e294cc1c87540d698948ebe0c9246ff`.
  Existing check-payload verifies all 102 installed files, same-version skip,
  settings sentinel, offline/checksum refusal and URL restrictions. PR #5's
  remote-channel/status tests pass 6/6 using the original scripts in their required
  directory layout (an initial relocated-script test failed on its own missing path).
- Network verification: installed the previous public version `0.2.2fb402472edb`
  through the real updater into an isolated folder; all 102 files, settings
  preservation, same-version skip and the remote menu-status handler passed.
  The main public endpoint initially served the cached old manifest, then returned
  the new version on the seventh propagation check (~120 s). The follow-on real
  network installation was blocked by this environment's network policy for
  raw.githubusercontent.com; the previous install stayed unchanged. New-payload
  installation is verified by the local actual-payload check above, not claimed
  as a completed public-network upgrade or native Windows button test.
- Release remains an owner-approved diagnostic test update: the 12 baseline unit
  failures, four server tsc errors, generic WebSocket rematch timeout and unrun
  real-Rapier rendered/Windows visual acceptance from the previous entry remain.
  No additional gameplay or presentation changes were made during publication.

## 2026-10-01 — Astra — real-Rapier body damage verification and PR #14 integration

- Integrated Claude `afb7561075ba71cef78ff091f2a5e7cea6b2b725` (parent systems
  `2f148ad5dfaf1495ca9018633a454783bbf4d86c`) into `codex/body-damage-systems`,
  draft PR #14. Physics fixes are commit `6148fe27b0684fbab3f50ac3ef1439225da9b2d9`.
  Shared target remains `claude/visual-revision` at `a2480ba6985bb8a60efc10f79839eeb7d3dc9a27`.
- Separate API-materialized verification checkout: all 549 tracked file bytes matched
  Claude's Git blob hashes, including media recovered from the uploaded source/test ZIP.
  Node 24.19.0, npm 11.9.0, fresh `npm ci --no-audit --no-fund`; runtime Rapier 0.19.3.
  These results use real Rapier WASM, not Claude's stand-in. Local synthetic Git history
  was not pushed; remote commits retain Claude's actual parent history and tree.
- Fixed only the physics adapter and regression tests. Impact deaths with a motion
  contract no longer receive the legacy per-part spin (measured up to 18 rad/s for a
  melee victim inheriting only 0.3 m/s). Their initial linear velocity still consumes
  the authority result once plus the existing bounded kick for a detached subtree.
  Explicit cosmetics and old snapshots without motion retain their legacy fallback.
- Real Rapier exposed a release discontinuity missed by the prior chest-only test:
  the right forearm jumped 9.2 cm (cavity) / 9.9 cm (decap) when script visibility
  switched to solver-corrected bodies. At the 50/60 s boundary, copy the exact scripted
  pose and velocities once before physical steps continue. The same boundary probe
  (10 microseconds apart) now measures <0.05 mm across connected parts in those fixtures.
  Detached heads remain physical throughout. Claude's animation curves/meshes are unchanged.
- New regression coverage observes real bodies/joints: no added spin or double force,
  equal kick across a severed arm subtree, all-part release at 20/30/60/144 Hz, identical
  late/stepped physics, immediate head flight, and head/arm subtrees freed by a spine crush.
  `body-damage-visuals.test.ts` passes 7/7. Initial requested focused run passed 61/61;
  the final full suite also passes all requested wound, visual, existing ragdoll and
  body-damage tests, including the three added regressions. Wound tests pass 11/11.
  `body-damage-network.ts`: three real WebSocket clients, four identical HostedRoom
  snapshots (living wound, fatal cavity/kneel, later death, round reset).
- Complete systems baseline `2f148ad`: 646/658 units. Claude before fixes: 661/673.
  Final fixes: 664/676. Exactly the same 12 failure names on base and final; no new
  failures. Restoring matching media made three of the previously reported 15 pass:
  original intro music assets, fourteen-map budget/reset, and menu-music playback.
  Remaining failures cover music catalog count, behavior/catalog counts, Demon Step,
  reference poses, disconnect forfeit, block knockback, dagger leading-hand pose,
  vertical-platform support, shield DoT and PCM sample rate. They were not suppressed.
- `npm run check:types` and Vite pass. Broad `npm run verify` also passes client/relay
  compilation, but server compilation retains the four known errors (chaos.ts,
  leet.ts twice, standard-melee.ts); generic WebSocket integration times out at rematch.
  HTTPS integration passes: five sets, four drafts, 3,430 identical shared snapshots,
  destruction, retry, rematch and guest leave; 174.5 s. Broad verification is not green.
- Visual gate BLOCKED: `npm run dev -- --host 127.0.0.1` started Vite and the authority,
  but Cloud Browser rejected the kneel lab URL with `net::ERR_BLOCKED_BY_CLIENT`.
  No real-Rapier rendered lab scenes/screenshots, Windows/GPU, first-person/spectating
  or two-physical-PC visual acceptance is claimed. BODY-DAMAGE-HANDOFF lists the exact
  scene queries for Claude/owner's remaining review; no presentation rewrite was made.
- No gameplay damage, hit timing, hitboxes, cooldowns, knockback values, movement rules
  or network contracts changed in this follow-up. Status: combined feature/fixes pushed
  to draft PR #14; NOT merged or published. Owner explicitly required stopping after
  integration if verification fails: baseline failures plus the blocked visual gate
  prevent step 6. Next: complete real-Rapier visual review and resolve the failing gates
  before merging/publishing; the existing test and live feeds are untouched.

## 2026-10-01 — Claude — body damage visuals: wounds, severing/crushing, kneel/fall

- Task: Astra's `docs/BODY-DAMAGE-HANDOFF.md` (draft PR #14). Branch `claude/body-damage-visuals` from the
  `codex/body-damage-systems` head `2f148ad` (shared base `a2480ba`). Presentation only; the contract is consumed
  unchanged (`resolveBodyWound`, `deathMotion`, `bodyDeathPhase` timing, `bodyPartVelocity`).
- Living wounds (new `body-wounds.ts`; rig-owned layouts in `voxel-kit.ts`): each snapshot wound is anchored once on a
  rest rig (a ray along the shot through the named part; thin plates such as the sternum carry the hole into the body
  behind; contact points just off the surface use the nearest solid face) and carved into that part's voxels.
  Puncture: narrow channel with a bloody rim. Cavity: ragged hollow with lifted petals. Cut: tilted groove. Thrust:
  slot. Bruise: dented, discoloured layer. Scorch: char and embers. Flesh lines the walls (one extra instanced mesh per
  wounded part; the flesh material emits a share of its colour so openings read red under match lighting). Wounds
  stay on the part through animation and are copied into ragdolls, knockdowns and kill effects. Dedupe by `id@at`;
  snapshot absence, heal, reset, rematch, rig rebuild and player removal restore the shared sculpt and release the
  buffers. Pooled spray only for fresh wounds (none on late join or at the first-person camera). Reduced quality
  carves coarser; low detail draws plain boxes; the SVG compatibility renderer skips wounds. Siren and sever/crush
  wounds are skipped (the death effect owns them).
- Impact ragdoll (`ragdoll.ts`, new `body-trauma.ts`, `death-effects.ts`, `gibber-vfx.ts`): `impact-ragdoll` reads
  `death.wound`/`motion`. Severed `destroyedPart`: detaches at its joint with matching voxel cross-sections (flesh, bone,
  marrow, set flush where each part's geometry ends). Crushed part: no body; bounded fragments (≤40, reduced 16), its
  hat or weapon thrown loose, ragged stumps with splinters on the parent and on freed children. Shorter spray
  profiles. `bodyPartVelocity` is unchanged; its bounded kick now reaches the whole detached subtree (inherited
  velocity once). DEV Gibber keeps its path but gets the voxel caps instead of flat discs. Wound cells never shape
  colliders; voxel burst ignores them.
- Kneel/fall (new `body-collapse.ts`): forward kinematics over the ragdoll's own part frames from authority time. Head
  jolt along the shot, legs buckle, knees on the floor at `kneesAt` (feet pivot on a visual ankle inside the shin part
  and stay flat), forward fall about the knees with some hip flexion, limp arms. Cover and the floor limit the
  scripted fall. Bodies get the pose and its velocities before every physics step and are released at the first step
  at or after `releaseAt` (≤1/60 s later); tone holds the release bend .3 s. A severed head flies from the first frame.
  `ragdoll` motion (airborne, launched) is unchanged.
- Renderer: `BodyWounds.update` after posing, before `deathEffects.update` (dead rigs are carved before the corpse
  copies them); the first-person view model gets the local player's wounds. Dev-only lab `tools/body-lab`
  (`?scene=pistol|cavity|decap|cut|thrust|crush|limb|torso|kneel|airborne|finisher|low|gallery`).
- No damage, attack timing, hitboxes, cooldowns, movement rules, knockback values or network fields changed.
- Checks: new `tests/body-wounds.test.ts` 11/11 (no physics engine). New `tests/body-damage-visuals.test.ts` 4/4 only
  against a local toy physics stand-in (not committed): unverified until run with real Rapier. Whole suite on base
  vs branch here (no npm packages): no test that passes on base fails on the branch (53→64 passing; 143→158 with the
  stand-in). Scratch typecheck of changed client files, new tests and the lab: no new errors. Lab renders reviewed for
  every scene plus the wound gallery from several angles; scripted kneel frames are pixel-identical at 20/30/60/144 fps,
  for a late first sighting and for repeated snapshots. Costs here: carve ~0.7 ms per wound, collapse setup ~1.1 ms per
  death, 0.02 ms per physics step.
- Not run: real Rapier (the new integration test; existing ragdoll tests in `weapon-entrances-gibber`,
  `interiors-ragdolls-hud`, `reaction-fluidity`; post-release motion), Vite build, verify, Windows/GPU, multiplayer,
  in-game first person and spectating. The npm registry is blocked in this sandbox.
- For Astra: (1) impact deaths still get the legacy cosmetic spin (lever×dir×15 for melee plus random) on every part;
  on the stand-in melee deaths flail. Please judge it with real Rapier (physics adapter, unchanged here). (2) Optional
  contract: a blade swing direction would let cut grooves follow the swing (today: seeded tilt plus the shot's
  vertical). (3) A `spine` crush removes the upper trunk root only; pelvis stays, head and arms fly free.
  (4) Explicit cosmetic effects keep their behaviour (no sever/crush/kneel); living wounds still ride along.
- Status: implemented on branch and pushed; not merged or published. Next: Astra runs the physics tests and reviews
  post-release motion with real Rapier, then owner-authorized integration into the shared branch.

## 2026-10-01 — Astra — body damage systems and Claude visual handoff

- Owner requested impact-driven bullet holes/cavities, blade cuts/stabs, lethal
  severing/crushing, public default ragdolls/gibs, and grounded fast-headshot
  knees-to-forward-collapse. Astra owns functions; Claude owns visuals.
- Task `codex/body-damage-systems`, shared base `a2480ba`. Added authority-owned
  `body-damage.ts` and cosmetic physics/timeline helpers in `body-ragdoll.ts`.
  Actual projectile identity/velocity/radius survive reflection; debris passes
  finite mass, closing speed and contact point. Damage mitigation affects visual
  severity before remaining-HP clipping. Melee cuts, thrusts, throws and shockwave
  crushes use distinct descriptors. Wounds are bounded to eight per life.
- Optional snapshot wound/death-motion fields carry stable IDs and chosen results;
  reset/heal clears them. Default `impact-ragdoll` is public; explicit saved effects
  and DEV/Siren overrides remain. No damage, attack timing, hitbox, cooldown or
  movement-rule changes. Cosmetic corpse velocity consumes gameplay knockback
  once. No living limb-health system or damaging corpse physics.
- Client code is only the existing-ragdoll fallback/velocity adapter. Claude still
  must implement holes, cavities, crushed fragments, live wounds and kneel/fall.
  Exact ownership, field semantics, timing and acceptance checklist are in
  `docs/BODY-DAMAGE-HANDOFF.md`.
- Checks (Node 24.19.0/npm 11.9.0): 16/16 new real-Rapier tests pass, including
  projectile collisions, reflection, mitigation, blocked/allied hits, reset,
  snapshot cloning and phase selection. Typecheck passes. New three-client real
  WebSocket test receives four identical HostedRoom snapshots: living wounds,
  fatal cavity/kneel choice, later death snapshot and reset. Focused death/default
  tests pass 22/22. Existing selected-voxel effect test now explicitly selects it.
- Full units: 643/658; fresh baseline 627/642 with exactly the same 15 failure
  names. Four media tests cannot run from this source-only API checkout; remaining
  failures reproduce on the base. An earlier incomplete-schema baseline had 25
  failures; the fresh comparison supersedes that result. No dependency changes.
  Client Vite build passes. Relay build passes after fetching its existing license
  file; server build reproduces the four documented TS errors. Broad verify's
  WebSocket suite reproduces the rematch timeout. HTTPS passes five sets/four
  drafts, 3,314 matching snapshots, retry, rematch and leave (182.6 s). Full
  `npm run verify` exits 1; recorded failures were not suppressed. After correcting
  the selected-voxel fixture, the final unit run has no new failure names.
- Windows/GPU, visual acceptance and two physical PCs are unrun. This branch does
  not implement Claude's visuals and is not a green production release candidate.
- Status: draft PR #14, initial systems handoff `fdaf42c`, visuals pending;
  not merged/published.
  Claude should branch from this task head as `claude/body-damage-visuals` and
  preserve the shared contracts. Await owner-authorized integration and actual
  verification before the standing test-channel publication.

## 2026-10-01 — Codex — combined Chip cards, Siren teams and instant bot drafts

- Owner authorized merging Claude's `claude/chip-cards` at `1d16c68` and the
  isolated team fix at `a93d9eb` into `claude/visual-revision`, adding optional
  hidden bot selections, and publishing to the existing test Update channel.
  Integration branch: `codex/chip-cards-bot-draft`; shared base `256395d`.
- Added opt-in **Skip bot chip choices** in Practice draft settings, Create Room
  and host-controlled lobby settings. Bots receive their usual first offered
  Chip through normal eligibility/award checks immediately; their selection and
  reveal screens are skipped. Human choices, timers and reveals are unchanged.
  The saved preference defaults off; room settings survive lobby changes and
  rematches. Guests cannot change it, and only literal true enables it.
- Preserved Claude's card redesign and three Siren Chips (191 total). Combined
  the narrow simulation changes with the team fix: Ward Zero overrides the map,
  not the selected game mode or team assignments. All other Claude-owned card,
  visual, contract and SIREN.md files retain their supplied contents.
- Checks (Node 24.19.0/npm 11.9.0): types pass; focused card/team/skip/settings
  regressions 19/19 pass. Three real WebSocket clients agree on 87 snapshots,
  including instant bot awards, Siren's Crown raising HP to 18,000, preserved
  teams and allied damage protection. Dedicated-server checks pass across 14
  maps/eight players, with 35 identical fight snapshots and host-only settings.
  Full units: 630/642, exactly the same 12 failure names as the isolated fix's
  619/631 baseline, including stale catalog counts. Client/relay builds pass;
  the four known server TypeScript errors and generic WebSocket rematch timeout
  remain. HTTPS passes five sets/four drafts, 3,472 identical snapshots, retry,
  rematch and leave (177.2 s). Full `npm run verify` exits 1 for those known
  failures; broad verification is not green.
- Gameplay changes are bot draft pacing, restored team rules and Claude's three
  Chip stat effects. No attack timing, hitboxes, cooldowns or movement physics
  changed. An optional boolean travels through existing settings/snapshot
  objects; new Chip IDs require clients to update together. No save migration.
- Not run: local visual preview (Chromium download failed), native Windows/GPU,
  audible sync or two physical PCs. Claude's own visual review is recorded below.
- Status: implemented and verified on integration branch; owner-authorized merge
  and test publication next. Publication results will be recorded separately.

## 2026-10-01 — Claude — Chip card redesign and Siren-exclusive Chips

- Owner request: Chip cards looked too basic; make them more appealing, keep the DEV Chips' "wow", give the Siren
  its own glitchy black-flame animated Chips, and add 2–3 boss Chips for testing. Branch `claude/chip-cards`, base
  shared `claude/visual-revision` `256395d` (includes Astra's PR #11 and the bot test publication).
- Cards (`chip-card.ts`, new `chip-cards.css`): each Chip is a processor package: metal legs, a raised heat spreader
  holding the lit die (icon), grade with rank pips, name, effect, compatibility and stack slots. Legendary is
  gold-plated with a gleam, Mythic holographic; Mishima, Ansatsuken and L44T keep their auras on their own packages
  (Mishima's current now runs around the edge instead of across the title). Old card rules were removed from
  `style.css` (72 rules; spotlight cards keep their package background). Player-board chips follow the same look.
- Siren: new rarity `siren` (`content.ts`, `leet-content.ts` weight 0) and `src/shared/siren-chips.ts`: Hollow Crown
  (+3,000 HP), Black Flame Hide (30% thorns, half knockback), Psychotic Break (damage ×1.25, speed ×1.12, incoming
  damage ×1.15). Siren-only, offered with the other DEV kits' exclusive odds (two guaranteed while any remain).
  `siren-card.ts`: black flame, the grin, scanlines and a glitch burst; picking one plays a Siren reveal on the
  default 3.2 s draft timing. `simulation.ts` gets two guard clauses (draft pick and sandbox add refuse Siren Chips
  for other specs). New dev-only `tools/chip-lab`; docs/SIREN.md has the details.
- Gameplay impact: only when the Siren drafts these Chips (HP, thorns, received knockback, damage, movement speed,
  incoming damage through existing stat paths). No hit timing, hitboxes, cooldowns, movement rules or network
  schema changed; three new Chip ids travel in existing fields, so all clients need the update together.
- Checks: new `tests/chip-cards.test.ts` 5/5 (tsx). `rarity-combat` tier-markup expectation adds Siren's 16
  particles. With a temporary local physics stub (removed, not committed), the whole suite on base and branch:
  0 tests that pass on base fail on the branch (186 vs 181 passing; +5 new). The catalog-count tests in core,
  rarity-combat and chaos already fail on base (188 ≠ 182) and now report 191; names unchanged. Scratch typecheck
  of client, simulation and the new test: no new errors (TS 6 flags the CSS import like the existing ones). Chip lab
  renders reviewed: all tiers, hover, reveal, Siren glitch, spotlights, board, 720p, narrow and reduced motion.
- Not run here: real-physics tests, `verify`, Vite build, Windows/in-game draft, multiplayer (npm registry blocked).
- Status: implemented on branch and pushed; not merged or published. Next: owner-approved merge into
  `claude/visual-revision`, then the standing test-channel publication from the merged commit (needs Node 24/npm).

## 2026-10-01 — Astra — preserve teams in Siren matches (isolated branch)

- Owner reported that a red squad versus blue Siren lost its team behavior and
  requested a separate branch while Claude may be working on Chips. Branch
  `codex/siren-team-preservation` starts at shared `256395d`; implementation
  `9844501`. No shared-branch merge or test-channel publication for this task.
- Root cause: `Simulation.syncSirenArena()` explicitly changed the selected mode
  to FFA at match start/intro. Team IDs survived, but allied protection, bot target
  filtering, team colors and shared wins stopped applying. Ward Zero now overrides
  only the map; the selected mode, teams and friendly-fire option remain intact.
- Six new regressions failed on the original code and pass after the fix. They
  cover saved/newly selected/player/bot Siren, red-squad wins, colors, ally damage,
  bot/Marked targeting, bot rebuilds, rounds, sets, rematches and lobby return.
  Updated the old intro assertion that required FFA. Expanded the three-client
  loopback test to submit team choices and verify them after selection and reset.
- Targeted tests: 39/39 pass. Three real WebSocket clients agreed on 87 snapshots;
  team assignments and allied damage protection passed alongside the existing
  Siren checks. Types pass; full units 619/631 with exactly the same 12 baseline
  failure names as PR #11, no new failures. Client/relay compilation passes;
  the same four server TypeScript errors and generic WebSocket rematch timeout
  remain. HTTPS passed five sets, four drafts, 3,679 matching snapshots, retry,
  rematch and leave (187.5 s). Full `npm run verify` exits 1 for those known
  baseline failures; broad verification is not green.
- No Chip implementation, visuals, attack timing, hitboxes, damage values,
  cooldowns, movement rules or network schemas changed. Team gameplay now follows
  the lobby choice instead of being silently disabled by Siren. Windows/GPU and
  physical two-PC playtesting remain unrun.
- Status: implemented on the isolated branch, not merged or released. Published
  content remains `0.2.c452eefcbbac`. Next: coordinate with Claude's Chip work,
  integrate the narrow simulation hunks, then publish under the standing test
  channel instruction. Do not replace the shared branch with this branch wholesale.

## 2026-10-01 — Astra — cutscene/bot test publication and standing channel instruction

- Owner asked to publish the merged changes and continue using the Update channel
  unless told otherwise. Recorded that standing instruction in AGENTS.md and
  DEVELOPMENT.md for both agents. It covers this existing test channel; merge
  authorization, live feed and Site publication remain separate.
- PR #11 merged into `claude/visual-revision` at
  `fd673130a56dac89db1df90133fda10ea0afc728`, with the exact verified task tree.
  Built that source in a clean worktree. Documentation branch:
  `codex/siren-bots-test-publication`, base `fd67313`.
- Published content `0.2.c452eefcbbac`, build `bd733d69b623c360820e25b4`, to
  `sheldon37/chip-arena-test-updates` at
  `79e939f500d2147162428ac4cdeae82c2ed78a90`. Payload: 22,904,059 bytes / 102 files;
  SHA256 `3212d9ce61a9515778588d783fbe371f591906a45f69e63f573b18c39098c5a0`.
  Successful workflow `36821019040` verified and atomically published feed/history
  and removed its temporary branch. Main contains only feed, README and history.
- Includes Claude's silent smile/Last Laugh cutscene, removed voice hooks, Siren
  15,000 base HP, DEV-only bot loadout choices, and normal/DEV bot abilities.
  No gameplay, hit timing, hitboxes, damage, cooldowns, movement or networking
  changed during publication; gameplay impacts of PR #11 are recorded below.
- Node 24.19.0/npm 11.9.0: clean npm restore and two `release:prepare` builds
  produced identical feed bytes. Actual payload checker passed all installed
  hashes, same-version skip, offline/corrupt checksum refusal, settings sentinel
  and test-feed URL restriction. Channel/status tests 6/6; auth/manual updater
  tests 7/7. Original music MP3 hash unchanged.
- Actual network updater installed previous `0.2.149269b37b5b` from immutable
  channel `3e9d5d9`, then upgraded through main to `0.2.c452eefcbbac`. All 102
  installed hashes, preserved settings, same-version skip and menu version/build
  passed. The previous manifest needed one timeout retry; the new upgrade passed.
- Reused exact-code PR #11 checks: 57/57 feature tests, full units 613/625 (same
  12 baseline failures), three-client Siren 84 matching snapshots, HTTPS 3,505
  matching snapshots. Known four server TS errors and generic WebSocket rematch
  timeout remain; broad verification is not green. Windows Electron/GPU,
  audible/visual acceptance and physical two-PC testing remain unrun.
- Status: published to the existing test Update channel. Existing test installs
  press Update. No new seed ZIP needed; live feed and Site unchanged. This
  documentation follow-up is scope/whitespace checked. Next: Windows playtest
  of the two cutscene roles, DEV bot choices/abilities and Siren HP.

## 2026-10-01 — Astra — cutscene integration, DEV bots, abilities and Siren HP

- Owner requested merging `claude/siren-cutscene` `452bbe6` into the shared branch,
  removal of dropped voice hooks, DEV bot loadouts, 15,000 Siren HP, and bot weapon
  abilities. Task branch `codex/siren-cutscene-dev-bots` starts at shared `dd8bb6e`
  and includes Claude's exact `452bbe610403000d02bb9352e2ab62e3ed12637d` commit/tree.
  Claude's visual, contract, lab and cutscene-test files remain unchanged.
- Removed laugh/You file loading, playback and event hooks from SirenAudio and
  its caller, plus the obsolete intro-laugh test. Music and lock-on beeps remain;
  shared event names still drive visuals. Claude's camera/HUD wiring is retained.
- Bot selectors in local practice and the host's room lobby show ??? only with
  verified DEV access. Authority permission comes from the actual configuring
  player, not request fields, item grants or a DEV guest. Bots receive only the
  selected weapon grant, never a DEV role/account identity. Duplicate Sirens fall
  back to ordinary random weapons; humans and bots share the existing slot limit.
- Active bots now choose normal E abilities, Emperor electric/wavedash, Ansatsuken
  Hadoken/uppercut/Tatsumaki, L33T Root Command and Siren's mode switch/all eight
  abilities. Basic attacks release during recovery so specials can get the next
  ready frame. Close-range casters approach into ability range. All decisions use
  the normal input/combat path, respecting visibility, resources and cooldowns;
  dummy bots remain inactive. Random loadouts continue to use ordinary weapons.
- Siren base HP is 15,000 for players/bots, before normal HP Chip modifiers.
  Saved loadouts, specialization, rounds/rematches and sandbox stat rebuilds agree.
  Animation impact: Claude's change is presentation-only. This follow-up changes
  HP, bot choices and bot approach movement; attack damage, hit timing, hitboxes,
  cooldown values, movement physics and snapshot/input schemas remain unchanged.
- Implementation `8979723`, PR #11. Final checks (Node 24.19.0/npm 11.9.0): types pass; units
  613/625, with exactly the same 12 failure names as the previous verified source.
  All 57 Siren/cutscene/DEV-bot tests pass. Client and HTTPS relay build; existing
  four server TypeScript errors remain. Three real WebSocket clients agreed on 84
  snapshots, including DEV bot grants, guest refusal, duplicate Siren refusal,
  15,000 HP, melee, mode, Marked and music continuity. Full `npm run verify` also
  reproduced the known generic WebSocket rematch timeout. HTTPS passed five sets,
  four drafts, 3,505 matching snapshots, retry, rematch and leave (177.6 s).
  Overall verification remains nonzero only for the documented baseline failures.
- Status: implemented and verified on task branch / PR #11 for the owner-requested merge.
  No new test-channel or live publication. Published content remains
  `0.2.149269b37b5b`. Native Windows/GPU, visual/audio acceptance and physical two-PC
  testing remain unrun. Next: owner-requested shared-branch merge and Windows playtest;
  publication of these follow-up changes remains separate from the prior test release.

## 2026-10-01 — Claude — Siren: silent smile and Last Laugh cutscene

- Owner request after testing the merged Siren: no voice lines or laughter (the intro just smiles), and the Last
  Laugh grab becomes a cutscene for the player being killed and for the Siren's player. Branch `claude/siren-cutscene`,
  base `claude/visual-revision` `dd8bb6e` (Astra's PR #8 merge plus #9/#10 records; reviewed, no overlap problems).
- Smile: intro bar 4 and the Last Laugh windup no longer shake or laugh; the grin draws open slowly, the head creeps
  over, the eyes narrow, and the windup soot rings slow to one per 0.7 s. Same timings and cues.
- Cutscene: new `src/client/siren/cutscene.ts`, wired in `renderer.ts`, `main.ts` and `style.css`. Letterboxed shots
  from the catch: lift, close-up (victim: the grin from below; Siren: its grin from above, past the victim), grip
  two-shot, crush from high on the front right with a jolt. The victim's continues 1.75 s past the crush as their death
  camera; the Siren player's ends at `releaseAt`, when its movement unlocks; a hold broken before the crush cuts back.
  HUD and combat numbers fade, overhead health and the first-person view model hide, and mouse look is ignored while it
  runs (the Siren is locked in the move; this stops the held victim swinging under a directed camera). Other players
  are unaffected. The camera eases after the Siren's facing.
- Impact: camera, HUD and presentation only. No hit timing, hitboxes, damage, cooldowns, movement rules, network
  schema or snapshot fields changed. Astra's optional `siren-laugh`/`siren-you` clip hooks in `siren-audio.ts` stay
  silent without files; per the owner they should not be added (removing the hooks is a systems-file call).
- Checks: tsx `tests/siren-cutscene.test.ts` (7 new), `siren-visuals`, `siren-contract` 25/25 pass. Scratch typecheck
  of the changed files against three r180: same error set as base, none in changed files. Lab renders of both roles
  inspected shot by shot (`times=` lab parameter added). Not run here: full units, `verify`, Vite build, Astra's
  rapier-based Siren tests, in-game Windows and multiplayer checks (npm registry unreachable; rapier unavailable).
- Status: implemented on branch and pushed; not merged or published. Next: owner/Astra review and merge into
  `claude/visual-revision`, then an in-game check as both the victim and the Siren (optionally a test-channel update).

## 2026-10-01 — Astra — Siren test Update publication

- Owner requested the in-game Update button delivery for testing. Built the exact
  shared source `abb0bbb5294e0299520be2e265ad32dd4c595752` in a separate worktree;
  source/assets/tests match the verified Siren integration `1a8a5d0`. Publication
  documentation branch: `codex/siren-test-publication`, base `abb0bbb`.
- Published content `0.2.149269b37b5b`, build `6b9d6a07736b51c965dd7c77`, to
  `sheldon37/chip-arena-test-updates` at
  `3e9d5d92efaa585ab63a62961abfcf822deea728`. Payload: 22,896,921 bytes / 102 files;
  SHA256 `70ab73d6b2f07ae804d605f38b33f3c94e79d68c0eb976f5f512981fd8b02aa3`.
  Workflow run `36814727228` verified hashes and atomically published feed/history;
  its temporary branch was removed. Main contains only feed, README and history.
- Node 24.19.0/npm 11.9.0: clean restore; two `release:prepare` builds produced
  identical feed bytes. PR #5 actual payload checker passed installation, all file
  hashes, same-version skip, offline/corrupt checksum refusal and settings sentinel.
  Channel/status tests 6/6; auth/manual updater tests 7/7. Original MP3 hash verified.
- Actual network updater installed previous `0.2.f3f24aed59e8` from immutable
  channel `873e23f`, then upgraded from main to Siren. All 102 installed hashes,
  preserved settings sentinel, same-version skip and menu version/build passed.
  The old seed manifest needed two timeout retries; the Siren upgrade succeeded.
- Reused exact-code integration verification: Siren 42/42, full units 598/610
  (same 12 baseline failures), three-client Siren 80 matching snapshots and HTTPS
  3,509 matching snapshots. Existing four server TS errors and generic WebSocket
  rematch timeout remain; overall broad verification is not green. This is the
  owner's diagnostic test build, not a claim of native acceptance.
- No gameplay, hit timing, hitboxes, damage, cooldowns, movement or networking
  changed during publication. Windows Electron/GPU, audible sync, visual quality
  and two physical PCs remain untested; optional laugh/You clips remain absent.
  No new seed ZIP was needed. Live feed and Site unchanged.
- Status: published to the existing test channel. Next: existing test installations
  press Update, select Siren under DEV-only ???, and perform SIREN-SYSTEMS Windows
  acceptance. This documentation follow-up is whitespace/scope checked only.

## 2026-10-01 — Astra — owner-authorized Siren merge

- Owner asked to merge after assessment. PR #8 (`codex/siren-boss`, verified head
  `1a8a5d0`) merged into `claude/visual-revision` as `9f010f5aae7dda46c7c1942e85764fdc3c78fd8e`.
  Readback confirms the merge tree exactly matches the previously tested tree;
  no gameplay, assets or networking changed during merge.
- Reused the completed checks from the prior integration entry: 42/42 Siren tests,
  three-client Siren 80 matching snapshots, HTTPS 3,509 matching snapshots;
  full units 598/610 with the same 12 baseline failures. Client/relay builds pass;
  existing server TS errors and WebSocket rematch timeout remain. GitHub scope
  passed; its full CI rerun was still running at merge. No green CI claim.
- This follow-up on `codex/siren-merge-status` updates documentation only;
  whitespace checked. No additional code tests required for the identical tree.
- Status: merged, not published. Windows/GPU/visual/audio acceptance remains
  unrun. No update-channel/live-feed publication was requested or performed.
  Next: owner playtest/release decision; optional laugh/You clips still pending.

## 2026-10-01 — Astra — integrate Claude's Siren visuals into PR #8

- Continuing `codex/siren-boss` / PR #8 from systems `514d784`; imported Claude's
  `8062eb2` and `2e986f4` visual commits. Shared target remains `claude/visual-revision`
  at `cf5fa1c`. Both DEV_LOG entries preserved; Claude-owned visual/contract/lab
  files and tests match `2e986f4` exactly.
- Completed the current `docs/SIREN.md` handoff: exclude world/first-person/intro
  Siren armor halos, apply effects and Ward Zero lighting after the current camera,
  skip the generic intro camera and preserve viewport aspect, call first-person
  restoration for ordinary specs. Fixed first-person rig churn: the world-rig
  marker does not exist on Claude's view model, so rebuild only on spec transition.
- Lock beeps now use Claude's crosshair pulse schedule, with 80 ms audio lookahead,
  deduplication, stale-cue rejection and interrupted-lock cancellation. Optional
  intro laugh moves from reveal (3.9184 s) to smile (6.0082 s). Voice assets still
  await the owner; the original song is already present.
- Gameplay impact: Black Rain height 9 -> 8 m above the landing point and downward
  speed 11.25 -> 10 m/s, keeping the 0.8 s fall/marker window. Damage, cooldowns,
  input/network schema, other move timings and collision radii are unchanged.
- Checks: types pass; all 42 Siren tests pass, including four new integration
  regressions (120-frame view-model reuse/spec restoration, intro aspect/glow,
  exact shared beep timing/cancellation, and smile-cue laugh). Full units 598/610;
  exactly the same 12 failure names as the prior branch/clean shared base. Client
  and relay builds pass; the same four server TS errors remain. General WebSocket
  suite reproduces the known rematch timeout. HTTPS passed five sets,
  four drafts, 3,509 matching snapshots, retry/rematch/leave (177.4 s).
- Three real Siren WebSocket clients agree on 80 snapshots, including access and
  duplicate refusal, melee, mode, Marked and music continuity. Overall verify is
  still red due to baseline failures. No Windows/GPU/in-game visual pass, audible
  sync assessment, physical two-PC check or packaging/updater test performed.
- Status: combined on task branch / PR #8; not merged into the shared branch or
  published. Next: owner Windows visual/audio acceptance, optional voices, then
  owner-authorized merge/publication. Handoff: `docs/SIREN-SYSTEMS.md`.

## 2026-10-01 — Astra — Siren systems and DEV selection category

- Branch `codex/siren-boss`, requested base `cf5fa1c5548e66d1364c98f9ea053202f918ec50`
  on `claude/visual-revision`. Imported Claude's contract `84b692f` before writing
  snapshot/audio/façade integration. Claude-owned files and contract tests unchanged.
- Added owner-requested fourth category `???` (verified DEV only), retaining the
  original weapon resource classes, old loadouts and exact grants. Added Siren grants,
  authority-enforced one-connected-Siren rule in all selection paths, TAKEN UI,
  Q/F/V mode binding, 1–4 abilities, HUD and DEV beat overlay. Siren forces FFA/Ward
  Zero, last intro, and continuous authoritative boss music; public rotation stays
  at 14 arenas. Original owner MP3 included and credited Xtrr - musiccreator.
- All ten move mechanics, seeded rain, capped fire patches, telegraphed launches,
  dodgeable grab/head crush, destructible-piercing/foundation-stopped homing shot,
  death/disconnect/reset cleanup, shared movement prediction, façade routing for
  world/first-person/lobby/intro/effects/deaths, optional voice hooks and lock beeps.
  Siren capsule locomotion stays standard with committed roots/rush/lunge; hover
  belongs to Claude. No replacement/placeholder visuals authored by Astra.
- Combat/network impact is intentional for Siren: shared contact/recovery times,
  damage/ranges/cooldowns in `siren-content.ts`, optional input/player/world/shot/
  song snapshot fields. Marked defaults to 85 damage, Last Laugh instant head crush
  at catch +3.2 s. Generic chip procs/attack-rate timing do not all apply to this
  custom boss kit; exact tuning/limits and manual checks in `SIREN-SYSTEMS.md`.
  Existing weapon mechanics retained; DEV resource lookups now use weapon class.
- Checks on Node 24.19.0/npm 11.9.0: types pass; focused Siren/rig 37/37; final unit
  587/599 with exactly the same 12 failures as clean base (555/567). Client/relay
  build passes, four known server TS errors remain. Full verify attempted: generic
  WebSocket rematch timeout remains; HTTPS passed five sets/four drafts/3,497 agreed
  snapshots, retry/rematch/leave (177.6 s). Fixed initial integration failures then
  reran type/unit/build stages. Overall verify remains red. Existing tests updated
  only for authorized DEV fixtures/new intro count and Siren's separate hover,
  camera and music paths; no contract tests weakened or changed.
- Three real Siren WebSocket clients passed access/duplicate refusal, shared melee,
  mode, Marked seeker and song continuity with 80 identical snapshots. Native Windows,
  finished visual quality, real-hardware/audio sync and physical two-PC tests unrun.
- Next: owner/Claude integrate `claude/visual-revision-siren`, provide optional
  laugh/You voice files, perform Windows acceptance, then owner decides merge/release.
  Current façade is Claude's minimal contract implementation, not finished visuals.
  Status: implemented/tested on task branch; not merged or published. Updater, public
  test channel, live Site/feed, accounts database and installation untouched.

## 2026-10-01 — Claude — Siren DEV boss visuals (first pass)

- Branch `claude/visual-revision-siren` on top of the contract (`84b692f`). New files only under
  `src/client/siren/**` and `tools/siren-lab/**`, plus the owned contract files; no existing file changed except this log.
- Apparition (black-flame body, rim, flame tongues, leg wisps, hover, blank/angry eyes, jagged grin, claws and palm
  orbs), the full floating stance and all ten moves keyed to `SirenMoveState` times, effects for every move and world
  state, the Last Laugh hold/char/head crush, the Siren's own death, first-person claws, the dev-siren intro scene and
  Ward Zero's dressing with beat/section-driven lights, beacons, painted grins, giant eyes and vent visuals.
- `SIREN_STAGE_THEME.floor` lightened to `0x2a2731` for readability; `SIREN_VENTS` exported (additive).
- Checks: `tests/siren-contract.test.ts` + `tests/siren-visuals.test.ts` 18/18 (tsx; every move animates to finite
  poses, effects create/retire pieces, intro/stage/kill timelines, first-person restore). New files typecheck against
  three r180 types. Lab renders inspected for idle, all moves, intro, stage, death and first person. Not run here: the
  repo's full suite, `verify`, Vite build and an in-game check (npm registry unreachable; the systems are on Astra's
  branch). No hit timing, hitbox, damage, cooldown, movement or network behavior is defined here.
- Status: implemented on branch, not merged. Next: Astra wires the façade (docs/SIREN.md), then an in-game pass.

## 2026-10-01 — Claude — Siren DEV boss: shared contract for the Astra/Claude split

- Owner: new playable DEV boss "Siren" (one per match, free-for-all for now, adds later) with a beat-synced intro and
  stage. Astra builds systems on `codex/siren-boss`; Claude builds visuals on `claude/visual-revision-siren`. Both start
  from `claude/siren-contract`, based on `claude/visual-revision` at `cf5fa1c`. See `docs/SIREN.md`.
- Contract (new files only, no existing file changed): `src/shared/siren-contract.ts` (ids, slots, move timing table,
  snapshot/event shapes, spawn points, intro cues, accessors), `src/shared/boss-music.ts` (464 detected beats, bars,
  sections, intro window, loop; drifting tempo), `src/shared/siren-stage-layout.ts` (Ward Zero collision layout and
  beat-synced vents), `src/client/siren/index.ts` (visual façade with placeholder implementations), tests.
- Checks: `tests/siren-contract.test.ts` 11/11 passed (tsx). New files typecheck against local three r180 types. The
  npm registry was unreachable from this session, so the full suite, `verify:fast` and the Vite build were not run.
- Combat/network impact: none yet (no existing code calls the contract). Status: implemented on branch, not merged.

## 2026-10-01 — Codex — individual DEV primary combo steps

- Owner follow-up: allow one punch, two punches, or the full chain rather than
  automatically firing the whole string. Branch `codex/dev-click-combos`, based
  on shared `claude/visual-revision` at `6e32a60` after the previous publication.
- Ansatsuken primary now commits one step per activation (right punch, left punch,
  high kick); Emperor commits left then right. Release stops after the committed
  hit; further taps or holding attack continue, matching standard melee's existing
  input/cooldown behavior. Next-step memory lasts 2.4 s after recovery. Blocking,
  stun, specials, Demon Step, roll, death and round reset clear a partial chain.
- Costs split across steps: normal Ansatsuken 4 each / 12 total; Emperor 3 each /
  6 total. Ten-Hit Rush remains ten requested steps (2.2 each / 22); Pressure String
  three (2 each / 6). Existing per-step damage, hitboxes, finisher rewards and hit
  effects retained. Nonfinal recoveries use the original .25/.17 s cadence divided
  by attack rate; final recoveries retain their original durations. Rush uses a
  .069 s windup at base rate so an individual hit fits its .115 s cadence.
- Animation renders only the selected fist/kick and finishes its recoil before
  that hit's recovery ends. Optional `singleHit` snapshot flag distinguishes this
  from older full-string samples. Movement remains unlocked; successful Demon
  Step/roll now cancels the partial chain. Demon Step i-frame timing is unchanged.
  Saves, input fields, account access and special-move damage/cooldowns unchanged.
- Checks: types passed; 64 focused combat/animation/i-frame checks passed; full
  suite 555/567 with the same 12 baseline failure names. Three existing tests were
  updated for the deliberately changed per-activation counts/costs. Ten new tests
  cover taps, hold/release, timeout/block/dodge resets, resource refusal, added chip
  steps and correct snapshot fist selection; tap tests fail on the prior source.
  Eight real WebSocket clients agreed on 200 snapshots, including stopping after
  one/two hits, special moves, Demon Step, parry and knockdown/recovery.
- Final recoil-window animation checks: 32/32 passed. PR #7 merged into the shared
  branch at `66451cdf8240b02c836741cfd1c0853ba9d7c5a1` (implementation `f5b28ec`).
  Clean exact-commit release builds produced byte-identical payloads twice;
  updater validation passed all 101 file hashes, settings preservation, same-version
  skip and corrupt/offline refusal. Published test content `0.2.f3f24aed59e8`,
  build `775161394d78cfecd5769154`, 18,267,588-byte payload, channel commit
  `873e23fddca58ea05a57332ee4ca301a5dff9ae0`. The verified temporary split-upload
  workflow succeeded and removed its branch. Existing testers press Update.
- Real network installation verified `0.2.298dbb325341` -> `0.2.f3f24aed59e8`:
  all 101 installed hashes matched, settings sentinel survived, second Update
  reported current, and menu badge returned the new build/version. The historical
  seed manifest needed one retry after the unchanged updater's 5 s timeout in
  this Linux/proxy environment; the new-version upgrade itself passed directly.
- Status: merged and published to the existing test channel; live Site/feed
  untouched. Native Windows/GPU playtest remains unrun; known baseline failures
  remain unresolved. Next: owner checks tap/hold/release feel on both DEV weapons.

## 2026-10-01 — Codex — merge and publish DEV combos / Demon Step to the shared test channel

- Owner explicitly requested merge + publish and the same in-game Update workflow
  Claude uses. PR #6 (`codex/dev-melee-combo`, implementation `66111c2`) merged into
  `claude/visual-revision` at `5ecc20f7aaba065a6c8071b983bfe393f00e14af`.
  Merge tree matches the already-tested implementation exactly. PRs #1–#5 remain
  separate; no unrelated fixes merged. Shared source/channel choice is now recorded
  in AGENTS and DEVELOPMENT for both agents.
- Published test content `0.2.298dbb325341`, game build `9499e3a5ba09df675dcc401e`,
  channel commit `2112c4a45ebae1b97afbcb631e03d00deb13eb62` in
  `sheldon37/chip-arena-test-updates`. Existing channel installations press Update;
  no new seed ZIP or launcher change is needed. Live Site/feed untouched.
- Build/checks: clean worktree at the merge SHA, npm ci, `release:prepare` twice
  with byte-identical feeds (18,266,547 bytes). PR #5's actual updater checker
  verified all 101 files, current-version skip, settings preservation, offline and
  corrupt-payload rejection. Remote-channel/status tests 6/6 and auth/manual
  updater tests 7/7 passed. Full source verification remains as recorded below:
  545/557 unit tests, identical 12 baseline failures, four baseline server build
  errors and the existing WebSocket rematch timeout. No gate was marked green.
- Publication transport: the connector's 16 MiB request limit could not carry the
  18 MB payload. Uploaded two parts to a temporary channel branch; one-shot GitHub
  workflow verified the final SHA256 and every file hash, then atomically pushed
  only the feed + PUBLISHED row onto the unchanged channel head. Workflow succeeded
  and deleted its staging branch; no scheduled or persistent publishing workflow.
- Network verification passed after the raw GitHub cache refreshed: installed
  previous content `0.2.19e114ae7c87`, updated through the real network updater to
  `0.2.298dbb325341`, verified all 101 installed hashes and settings sentinel,
  then confirmed current-version skip and the menu badge's build/version.
  Linux network test used its environment proxy; the historical seed manifest
  required one retry after the updater's existing 5 s timeout. Updater code and
  timeouts were unchanged. Native Windows validation remains separate.
- Combat effects are those detailed below: combo animation flow plus Demon Step
  protection only; no further timing, damage, hitbox, cooldown, movement, save or
  protocol changes. Windows EXE/GPU and two-PC DEV playtesting remain unrun.
- Status: merged and released to the existing TEST channel. Next: owner playtests
  both DEV combo chains/exits and Demon Step startup/middle/recovery in-game.

## 2026-09-30 — Astra — DEV melee combo holds and Demon Step i-frames

- Owner: apply the standard melee combo-ready system to DEV melee; follow-up adds
  invulnerability frames to Ansatsuken's Demon Step. Branch `codex/dev-melee-combo`,
  based on Claude's requested visual work at `32422fbf0c96b13cf1007f892e5668748e84a1e7`
  (`claude/visual-revision`, PR #4). PRs #1–#5 remain unmerged at inspection.
- Emperor and Ansatsuken now recover directly into their next chamber rather than
  returning to guard before raising it. Share standard melee's 2.4 s visual hold
  and 0.8 s fade. Remember dodge/parry/hit exits even when the fighter layer is
  skipped or the exit field clears; discard stale state on death/rewind/rig reuse.
  Next-string entry and planted footwork retained. L33T is magic and unaffected.
- Demon Step: the existing 0.30 s dash protects from +0.05 s inclusive to +0.25 s
  exclusive (0.20 s / 12 frames at 60 Hz). Requires authorized Ansatsuken, a live
  player and an active dash. Canceled/expired dashes lose protection. Applies to
  authoritative damage, direct hits, normal projectiles and thrown daggers/shields;
  avoids hit procs, new status application, knockback and confirmed stuns/launches.
  Existing burn/poison, overtime, falls and forced L33T scare damage follow the
  same exceptions as dodge rolls. Updated the weapon's description.
- Combat/network impact: attack contacts, hitboxes, damage values, combo counters,
  attack cooldowns, dash cost/speed/distance/cooldown, saves and protocol unchanged.
  The intentional gameplay change is Demon Step damage avoidance in that window.
  Reuses `arcade.demonStepAt` and `dashTime`; no new snapshot or input fields.
- Checks (Linux, Node 24.19.0 / npm 11.9.0): types passed; 15 new tests passed;
  existing roll/combo/DEV footwork checks 13/13 passed. Full final unit suite
  545/557 passed, with exactly the same 12 failure names as a clean checkout of
  Claude's base (530/542). The live end-pose regression fails on the unchanged base.
  Client/relay compilation passed; server compilation retains the four baseline
  errors covered by PR #2. Full `npm run verify` hit the known WebSocket rematch
  timeout; HTTP/SQLite integration passed five sets, four drafts, snapshot agreement,
  lost-response retry, rematch and guest leave (179.5 s). Overall verify remains red.
- Not verified: Windows EXE/GPU appearance or two-machine DEV combat. Visual-lab
  browser launch was blocked by the missing Playwright Chromium executable.
  No packaging, merge, test-channel publish or live-feed change in this task.
- Next: review/merge this branch into `claude/visual-revision`, playtest both LMB
  strings into another string/dodge/parry and Demon Step's startup/middle/recovery,
  then build/publish a test-channel version when requested. Keep Claude's existing
  log entries. Status: implemented/tested on task branch; not merged or released.

## 2026-10-01 — Claude — Rain Warrior and Emperor's Oath intros (channel update)

- Owner: smoother DEV intros, fix their feet, Emperor's Oath camera in front of the player.
  626cfc5: new intro-footwork.ts (world-planted steps, pivots, lift arcs) used by both
  intros. Emperor's Oath: slip 0.044 -> 0 m, lowest ankle -0.847 -> -0.795, chest square
  to the camera (dot .78 -> 1.00); hips-led oath, rear-foot step, breathing hold; still 3
  shots, framing within limits; right arm path unchanged. Rain Warrior: feet no longer
  twisted off the knees, blended two-step pivot turn, ends in the bladed karate stance,
  breathing opening, new hadoken beat at the 4.6 s hit (orb may need polish). Durations,
  nameAt and access unchanged.
- Tests: new tests/dev-intro-fluidity.test.ts (6; 4 fail on old code); full suite only the
  13 known failures. Known: pre-existing synth backdrop bars also show on the rain stage.
- Published: content 0.2.19e114ae7c87 (channel 924651e), network update verified.

## 2026-09-30 — Claude — DEV fighting styles: ground-anchored feet (channel update)

- Owner: emperor (Tekken) / ansatsuken (Street Fighter) feet looked wrong while running
  and attacking; touch up moves. 2776860: the ground pass now covers both styles; fighter
  moves layer per-foot edits on the gait (layerLeg/gaitFoot) instead of fixed leg angles;
  bladed stances in stances.ts; kick chamber no longer flips the knee; combo-ready stance
  now works in live play. Slip e.g. backpedal 6.0 m -> 0.003 m; ankles never below the
  floor; knee flips gone. Guard bounce, hip-driven strikes, donkey kick, Shoryuken and
  Demon Step touched up. Access control and gameplay unchanged.
- Tests: new tests/devstyle-fluidity.test.ts (fails on old code); full suite only the 13
  known failures; no existing test edited.
- Published: content 0.2.87477529ed9b (channel e19eea3), verified network update; full
  channel ZIP (SHA256 168f0a2c...07a3) sent to the owner in parts for a second PC.

## 2026-09-30 — Claude — souls roll, fluid greatsword, combo-ready holds (channel update)

- Owner feedback: rolls levitated; greatsword looked robotic; after a hit, hold the pose
  for the next swing (combo), exit by rolling or timing out to idle; same for DEV styles.
- 587d99d roll: floor-solved shoulder roll (tuck was 13-65 cm above the floor, now within
  ~2 cm in 6 directions), dip, 360-degree tumble, crouch exit. 9ab51ce greatsword: smooth
  splines (old curves stopped at every reversal), hips-first chain (chest +71 ms), weight
  shift, heel pivot; combo-ready holds for sword/daggers/shield/greatsword (next cut starts
  from the hold; roll/hit/block/special release over .22 s; 2.4 s timeout). 35569fa
  emperor/ansatsuken chambered fighting stance between strings (L33T unchanged).
- Tests: new tests/roll-greatsword-fluidity.test.ts (8); full suite only the 13 known
  failures; no existing test edited. Known: kneeling roll exit can graze ~2.5 cm, diagonal-
  back exit bobs ~7 cm, greatsword reverse cut still raises high in windup.
- Published to the test channel: content 0.2.da174b55f7c3 from 35569fa (channel 5842c18);
  verified updating 0.2.4d3dcebad34a -> 0.2.da174b55f7c3 over the network, then current.

## 2026-09-30 — Claude — online test update channel (owner-requested)

- Owner asked for a working Update button so testers stop downloading ZIPs; approved a
  PUBLIC test-only channel repo: https://github.com/sheldon37/chip-arena-test-updates
  (feed at https://raw.githubusercontent.com/sheldon37/chip-arena-test-updates/main/feed/).
  Live feed and Site untouched.
- Tooling: branch `claude/test-update-channel` (base release/candidate-workflow 7ff6462),
  PR #5, head fd13d5a. `candidate.py`/`make-test-package.py --channel <url>` builds a
  TEST-ONLY ZIP whose Update button and menu badge read the channel through the unchanged
  production updater (schema/SHA256/32 MB checks); no live fallback. Without --channel the
  fixture is unchanged. Tests: scripts/release/test-remote-channel.test.mjs (6/6 with the
  existing status test); real-network install from the channel verified (installed, then
  current).
- Published: content 0.2.4d3dcebad34a from `claude/visual-revision` 0e82a31 (channel repo
  7757039, logged in its PUBLISHED.md). Channel seed ZIP (SHA256 205883b4...905b) built from
  0e82a31; packaging step re-run manually after fixing a packager bug (fd13d5a).
- Cross-development (Claude or Astra): to ship a new test version, build a candidate from
  the exact commit with `--test-only --channel <feed url>` using the PR #5 tooling, then
  replace the channel repo's `feed/` files (one payload only), add a PUBLISHED.md row,
  push, and log it here. Testers press Update; raw GitHub may cache ~5 min.
- Status: test channel live for testers; not a player release.

## 2026-09-30 — Claude — Dark Souls sprint carries, intro label, test build 2

- Owner feedback on test build 1: melee blades faced backwards while running; wanted a
  Dark Souls sprint and a two-handed greatsword low carry (owner reference image), and the
  intro weapon pill removed so the weapon name fades in with the pilot name.
- Changes (7c735d3, f688db0, a3b40e8): forward-down sprint carries for sword, daggers,
  shield (right-hand forearm ride; still meets the arm-swing test) and a two-handed
  greatsword carry with sprint lean; walk uses the same carry. Intro weapon name is plain
  text animated with the name. Tests: owner-approved greatsword exemption in the
  rig-arena-sniper arm-swing clause (replaced by a grip/blade-position assertion); two
  new-in-revision tests that pinned the rejected tip-back carry now pin forward-down
  (verified failing on the old carry). Full suite: only the 13 known failures.
- Test build 2 from a3b40e8 (same tooling/flags as build 1): SHA256 7ab1728b...9c9a,
  content 0.2.4d3dcebad34a; verify failed only on known items. Not published.

## 2026-09-30 — Claude — visual revision TEST-ONLY Windows build

- Owner requested a Windows test build of `claude/visual-revision` at 2557a12. Built with
  PR #3 tooling (release/candidate-workflow 7ff6462) `candidate.py --test-only`, Node
  24.19.0 / npm 11.9.0 (Python 3.11): Chip-Arena-TEST-ONLY.zip, SHA256 0f4df606...71c5,
  content 0.2.754d8ed53b1b, build 8d4cb82a5c68b3f79c2e3b0b. Status BLOCKED/publishable:false.
- Passed: setup, fixture tests, updater tests, runtime pins, two builds with identical
  payload, actual payload install, Windows/desktop/isolated packaging. `verify` failed only
  on known items: 12 known unit failures (portable-update passes on Node 24), the 4 server
  TS errors (PR #2), WebSocket rematch timeout.
- Delivered to the owner as 10 split parts; not published, no live feed change. Native
  Windows run pending owner test.

## 2026-09-30 — Claude — complete visual revision

- Task: owner-requested complete visual revision (animations, intros, model, UI),
  presentation-only. Branch `claude/visual-revision`, base `migration/shared-development`
  `dfb90c8`; PR to migration/shared-development (see GitHub). Plan, owner direction,
  invariants and ownership: docs/VISUAL-REVISION-PLAN.md. Player-facing summary:
  docs/UPDATE-HISTORY.md (copied from PR #3's version 7ff6462 plus a new entry — on
  merge conflict keep both sides' rows/sections).
- Changes: dev-only visual lab (tools/visual-lab, not in the build); no-behaviour module
  split; ground-anchored locomotion; continuous reactions/knockdown/ragdolls; Souls-like
  melee from owner reference GIFs (DEV classes untouched); shouldered guns, reloads,
  casting, first-person view model; detailed voxel model in armor slots with hue-true
  glow; reworked intros; UI polish and in-place HUD updates. Unchanged: hit timing,
  hitboxes, damage, cooldowns, movement rules, network/snapshot formats, saves, access
  control; src/shared, server and relay untouched; no pre-existing test edited.
- Checks (Linux, Node 22.22.2): full `node --import tsx --test tests/*.test.ts` 523 tests,
  only the 13 pre-existing failures; `npm run check:types` passes; independent reviews
  (invariants, animation quality, real-app smoke via Playwright/SwiftShader) passed.
  `npm run build:check` still stops on the 4 known server TS errors (fixed by PR #2).
- Not run: native Windows/GPU, real-hardware performance, online multiplayer, packaging.
- Known open: jump airborne pose barely visible in lab strips; dagger idle guard less low
  than the reference; warm accents pale under lobby/portrait lighting; intro close-ups
  blow the chest glow to white (pre-existing); crossed-arms lobby fist clips (pre-existing);
  Developer Sandbox buttons overflow at 1280x720 (pre-existing); intro scenes render
  slower under software GL, so check frame rate on the owner's PC. Owner-gated stale tests
  (fighter high kick, rarity-combat dagger mirror) still fail as before.
- Next: owner playtest on Windows and review of feel (cadence, melee, glow); then merge
  decisions. Status: implemented on branch; not merged; not released.

## 2026-09-29 — Astra — shared development log

- Branch: `migration/shared-development`; base before this documentation task:
  `dcdcd930565d05ace379e263206e13366b3e4e69`; PR #1.
- Added this log and a shared AGENTS.md handoff requirement. CLAUDE.md already
  imports AGENTS.md, so both agents use the same policy.
- Checks: read current remote AGENTS.md and PR metadata; documentation-only change.
  No gameplay/build/Windows tests run for this task. Existing baseline failures
  remain documented in MIGRATION-REPORT.md; this entry does not revalidate them.
- Runtime, game mechanics, assets and update feed unchanged. No player release.
- Status: implemented on migration branch; PR #1 still draft/unmerged at writing.
- Next: each agent synchronizes this branch before making its own task branch;
  after migration merges, start new tasks from updated main. Review relevant open
  PRs to avoid overlapping work. Resolve baseline failures before release work.
