# Native gun gait and Siren drift — 2026-10-10

Third-person gun pilots (pistol, rifle, machine gun; host, guests, bots and the killcam) no longer play the captured sword walk/run under a reset torso with a procedural leg cycle (fixed body height, square pelvis, about 9 cm of foot skating per stance at a sprint). `Core/GunMotion` runs the source's motion-quality locomotion in the stances.ts gun stances (stride scaled by the replicated velocity, planted feet, bob, sway, lean into turns, pelvis/spine/chest counter-twist, arm swing, sprint lean, air poses, landing spring) and lays the native hold over it: while the gun is up (aiming, firing, reloading, guarding, strafing, backpedalling, standing; `updateAimWeight`) the torso blends onto the `RangedPose` hold with the gait's lean, roll and hip twist cancelled, so the barrel stays on the sight while the hips and legs move; running forward lowers the gun, the long guns ride the torso in both hands with the carry's stride wobble (muzzle kept off the floor), the pistol is carried one-handed at low ready while both arms pump. The ground pass and the shared finishPose transitions (`Core/NativePoseTransition`, also used by the magic gait) follow. First person keeps `RangedPose.Apply` unchanged (`Tests/GunMotionChecks.cs` compares it with the committed hold). The converted Explosive pack clips hold only melee locomotion (Armed, Armed-Shield and 2Hand-Sword walk and forward run); blending the Armed run's torso onto guns hunched the pilot and buried the rifle muzzle, so the guns use the source gait instead. `Core/NativeSirenDrift` adds the Siren's floating lean, trailing limbs, air tuck, aim pitch, flinch and guard (siren/motion.ts) over her baked stance and move clips. Presentation only: no change to movement, shots, hitboxes, damage, timing or networking.

# Native first-person stability update — 2026-10-07

Captured base-pose history is now stored before procedural layers. First-person settling, casting, recoil and IK are applied once to a clean base each render. The prior feedback reproduced 3.43 cm of rig-space wrist drift at a frozen timestamp; 960 repeated-frame regressions are now stable. The analytic pose clock fills each 60 Hz tick up to one step, independently of physics authority, and pauses with the menu. Camera/visible-pilot translation follows Unity's interpolated Transform. ADS uses the original 26 rad/s critically damped blend, offsets and FOV counter-scale; rifle scope masking is included. Actual Unity playback and visual checks remain Windows gates.

# Native staff update — 2026-10-07

The current conversion uses source-proportioned elemental staffs, Claude’s first-person scale/roll and torso settling, plus damped guard transitions. Native wrist orientation stays in chest/world space during the guard. A 5 cm view-model lift and temporary guard-transition dip compensate for the captured native rig. The rendered staff parts and camera transforms are shared with the 5,808-case projection check. These changes do not move the authoritative pilot, muzzle or hitboxes. Native visual acceptance is still required.

# Inherited animation foundation

This file documents the accepted lab animation import. For the new native conversion boundary, read CONVERSION-STATUS.md. The old lab viewer/control descriptions below are historical; the new project currently opens the native match setup.

# Captured player animation test

Ranged update: Pistol, Rifle and Machine Gun now have playable procedural poses and viewer sets 5–7.
See RANGED.md. The captured melee data and original conversion below remain unchanged. Select Sword
with 1 during play; the default loadout is now Pistol. The ranged update adds combat rules and CAP2 networking.

This update imports the **34 retargeted captures already committed in Chip Arena**, from source
`05950eaef80ff2483a9c44d898cad98be1a76bbc`. It does not require importing the original FBX pack.
The game source identifies these captures as derived from the owner's Explosive RPG Character pack.
Keep this source distribution within the private project; the source pack itself is not bundled.

## What to try

Choose Sword, **Play solo**, and disable **Ranged bots** for the sword animation workload. Sword actors use the captured idle, walk/run carry,
three sword cuts, block and stagger. Attack contact marks are mapped onto the existing .14 s windup
and .16 s active window. The original animation-only update did not alter damage, stamina, hitboxes, movement, cooldowns or the LAN protocol.
Jump remains procedural; the roll adapts the source game's tuck and floor contact.

Choose **Animation viewer** for all four sets. Mouse movement orbits a stationary player. Press
**1–4** for Sword / Daggers / Greatsword / Shield, **[ / ]** for previous/next clip, and **Space**
to pause. Escape opens options for **0.25× / 0.5× / 1×** speed. The viewer displays the capture name.
It does not simulate combat or record benchmarks. Choose Play solo to return to live gameplay.

| Set | Clips | Included states |
| --- | ---: | --- |
| Sword | 9 | Idle, walk, run, 3 cuts, block, block impact, stagger |
| Daggers | 10 | Idle, walk, run, 4 cuts, block, block impact, stagger |
| Greatsword | 8 | Idle, walk, run, 2 cuts, block, block impact, stagger |
| Shield | 7 | Idle, walk, run, 2 cuts, block, block impact |

Of these four melee sets, only Sword rules are playable. The other melee sets and block-impact clips are inspection samples,
not new playable classes or proof their abilities have been ported.

## What is being evaluated

- Native Unity `AnimationClip` curve playback on a 22-joint rig with source rig-v2 proportions,
  shoulder/elbow/wrist and hip/knee/ankle articulation, source low-detail body geometry, and wrist-attached weapons.
- Smooth idle/carry transitions, retimed captured cuts, source-style arm IK conversion, and
  travel-aware procedural feet underneath captured locomotion (including strafing/backpedaling).
- Increased animated crowd workload with the existing GPU-instanced draw path and F9 compatibility fallback.
- The Escape menu can switch back to **original test poses**. CSV rows identify `animation_mode`
  and `animation_pose_ms`. The pose timer includes joint sampling, procedural feet and part-matrix collection;
  it excludes GPU time. The new rig also has more parts, so this comparison includes geometry cost.

These are shared, runtime-baked **legacy clips sampled explicitly with AnimationClip.SampleAnimation**,
not a production Animator Controller, Humanoid Avatar, skinning/animation-job benchmark or FBX-import test.
No GameObject colliders or authoritative root motion are added to characters.

## Fidelity limits

The compact capture channels, source marks/pace keys and primary rig dimensions are preserved.
The rig uses the source's plain-box detail level. Sculpted armor bevels, cosmetics, production maps,
material glow, melee trails, first-person arms, Q/E abilities, death animations and the full catalog remain outside this update.
The viewer uses captured legs. Since commit 1c0f257 live melee runs the original runtime layer (`Core/Melee*.cs`):
travel-aware gait feet, cut footwork planted against the authoritative step, blade clearance against the voxel
shoulders/chest/head, chest-space recoveries and the daggers' wrist limiter, with a per-weapon floor guard;
`Tests/MeleeMotionChecks.cs` matches 872 recorded frames of the pinned original within 1.73 mm. Visual review on
Windows is still needed for tight carries and transitions. Shield has no stagger capture and keeps its captured guard
through a stun.

## Implementation and reproduction

- `Core/CapturedClips.Generated.cs`: unchanged encoded data and timing metadata, generated from four source files.
- `tools/mocap-provenance.json`: source commit and SHA256 receipts.
- `tools/import_mocap.py`: strict known-blob extraction from a checkout of the original source, without executing TypeScript.
- `Core/CapturedClip.cs`: decoder, Catmull-Rom reference sampler and monotone attack retiming.
- `Core/CapturedRig.cs`: source-frame rig math, arm IK and floor constraints. Source +x-left coordinates are reflected once for Unity.
- `Runtime/CapturedAnimation.cs`: shared clip baking, Unity curve playback, blends and presentation-only action selection.

Run `python tools/import_mocap.py /path/to/Chip-Arena` to regenerate from the pinned source files.
Run `dotnet run --project Tests/CoreTests.csproj -c Release` for clip/rig and combat/network regressions.
The Unity **Run core and project checks** and Windows builder now sample 102 native poses and verify
actual transform curve bindings against the source conversion before permitting a build.

Native curve validation is implemented but has not executed in the authoring workspace. Its real result will
appear in your Unity Console/build log. This workspace only compiled against actual Unity APIs and ran pure C# tests.

Unity API references: [SampleAnimation](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AnimationClip.SampleAnimation.html),
[SetCurve](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AnimationClip.SetCurve.html).

## Native conversion additions — 2026-10-07

`MagicPose` ports the source analytic primary/Q/E casting phrase and first-person variants. Magic no longer plays sword attacks/blocks: the staff is in the left hand, the right hand casts, and the guard lays the staff across the chest. Source phrase validation covers 1,728 samples. The Unity quaternion/IK adaptation and guard transitions still require visual acceptance. Third-person magic pilots (2026-10-09) walk on the source gait (`MagicMotion`: the shared `MeleeGait` locomotion in the source magic stance, with the magic layer added over it and the ground pass after it) instead of a reset torso over the captured sword walk, which had frozen the torso; `Tests/MagicMotionChecks.cs` replays `tools/export_magic_motion.mjs` timelines within 0.1 mm. The first-person view model keeps `MagicPose.Apply`. L33t H4x (2026-10-10) is a magic-class staff pilot in the source (stance MAGIC, `magicLayer` default branches, .18 s release) with no clips of its own; its replicated `MagicVisual` stays 0, so `MagicPose.School` routes it (`Core/NativePoseRoute`) onto the same magic gait and first-person staff view instead of the sword set. The magic gait now also shows the juggle and ports `finishPose`'s key-change blends (juggle, knockdown, flinch, roll, guard, hit impact); `Tests/LeetMotionChecks.cs` covers its routing, states, staff hold, allocation and continuity. `RangedAbilityPose` adds support-hand grenade throws/visor taps and rocket recoil using source gesture keys.

`NativeViewPresentation` draws a separate forearm/weapon rig on layer 10 through a depth-only camera. The saved V/menu toggle and ADS choose this view; the authoritative pilot, collision shape and muzzle remain world-space. Head/body are excluded from the local first-person pass. Presentation changes do not change melee contact timing, projectile authority, damage, cooldowns, movement or networking. The new ability handlers separately add native gameplay behavior; no native network implementation is added.

# Captured-melee runtime layer — 2026-10-08

The 34 captured clips were already ported; this adds the source runtime that plays them (mocap/*.ts, locomotion, rig-ik,
blade-clearance), engine-free in `Core/Melee*.cs` and driven from `CapturedAnimation`:

- Gait and stances (melee, daggers, greatsword, neutral), phrase footwork with planted lead foot, hip seating, ankle
  holds and reachable targets; recovery blending with the wrist limiter; blades kept off the floor.
- Blade clearance against the rig's 11 body boxes (turn-only for cuts, full for carries), with the flat rolled onto the surface.
- Hit reactions and parry motion from the Hit/Block events, light reactions and torso recoil for non-melee weapons.
- Parity: `Tests/MeleeMotionChecks.cs` replays scripted timelines through the transpiled pinned TS and compares joint
  points (872 frames, worst 1.73 mm). Deviation: after a shield hitstun the source uses its procedural guard (about a
  16 cm hip pop); those frames are excluded and the native pose keeps the captured guard.

Presentation only: hit timing, hitboxes, damage, cooldowns, movement and networking are unchanged. The build gate
(`ValidateMelee`) checks lead-foot drift, ankle and tip clearance and finite bones. Rendered motion still needs the
owner's Windows playtest.

Owner-requested melee feel changes (2026-10-12, `docs/MELEE-FEEL.md`) deviate from the source on purpose: the walk/run
torso keeps the captured carry posture with a damped stride swing and a countered pelvis twist (`MeleeMotion.SteadyTorso`);
each cut plays its authored sweep across the hit window and follows through into the real recovery (`MeleeSwing`, shared
with the first-person view model and the slash crescent, which now follows the visible blade); a changed commit yaw
eases in over 0.1 s. `Tests/MeleeMotionChecks.cs` compares those scenarios with the Unity-owned
`Tests/melee-motion-pinned.json` (`CHIP_PIN_MELEE=1`) and keeps the hit-reaction scenarios on the source;
`Tests/MeleeFeelChecks.cs` bounds the torso motion and pins the swing timing, speeds, continuity and crescent sync.

Melee hit layer (2026-10-10, `Core/NativeHitReact`): every third-person route (captured melee, DEV fighters, gun and magic
gaits) adds, after its own reactions and before its ground pass, a weight-scaled impact accent from the attacker's side
(front, back, left, right; crisp rise, held impact frame, damped recovery, alternating along a combo), a head snap on a
headshot, a dazed stun pose (guard dropped, head lolling, sway) only while `StunUntil` lasts and held through a combo's
extensions, a guard shove on a blocked melee blow (blocked bullets do not shove) and, for gun and magic pilots (whose
routes showed nothing while stunned), the stunned recoil with a planted catch step. A client-side ledger reads the
snapshot's Hit/Block/Emperor/Arcade events (melee or not, attacker heft; the hit behind the current reaction is kept over
later field, splash or Siren-shot hits) on host, guests and the killcam; non-melee hits keep the source's light reaction
only. The local pilot gets a bounded first-person camera kick (after the aim point; off with camera shake, 40% input
with reduced effects) and a view-model guard drop while stunned. `Tests/HitReactionChecks.cs` covers selection, bounds,
allocation and hooks; the source parity checks run without the layer.

Pack hit reactions (2026-10-10, `Core/NativePackReact`, `Core/NativePackClips.Generated.cs`): on the captured melee and
DEV fighter routes the hit layer plays the owner's Explosive pack reaction clips, laid additively over the route's guard
(each joint's turn away from the clip's first frame, so nothing pops): GetHit-F1 from the front, F2 for heavy blows,
R1/L1 from the sides (Armed for sword, daggers and shield, 2Hand-Sword for the greatsword, Unarmed for the fighters),
Block-GetHit 1/2 alternating along a string of blocked melee blows (Armed-Shield for the shield, Block-Dual for the
daggers), and the Stunned loop for stuns longer than about .3 s after a blow (the melee hit stuns of .08-.32 s keep the
procedural daze, where the loop popped up). In play that is the swap-shop stun only (.5 s from its blow; the loop shows
at about a third under the GetHit clip): a freezer-block freeze has no blow of its own, so it keeps the route's pose (a
blocked attacker's parry reel) and the procedural daze over its last .3 s. Light hits move the upper body and hips;
while stunned the whole body takes the clip with the feet planted, and that share and the Stunned loop are exactly zero
from `StunUntil`. Blows from behind (B1 read as an upright flinch), gun and magic pilots (the Unarmed torso swept the
muzzle and staff aside) and non-melee hits stay procedural. The procedural accent stays on top as the weight, direction
and headshot modulation; the captured melee route holds its guard under the clip instead of its own stagger, while the
DEV fighters keep the source's impulse recoil (the head snap) under theirs. Arms follow the clip's hand travel and a
share of its blade turn through the rig's IK, keeping the route's elbow bend, the greatsword's two hands on the hilt in
front of the chest, and the blade clear of the head and torso (`Core/MeleeClearance`, turn only, once more after the
procedural accent); the trunk bend eases into a soft limit (the side captures fold the pack character by up to 1.7
rad). The tracks are exported offline by `tools/export_pack_reactions.mjs` (docs/DEVELOPMENT.md);
`Tests/PackReactionChecks.cs` checks their provenance hashes, deterministic decoding, selection, stun end, non-melee
hits, finite bones, blade clearance, allocation and the MeleeMotion hooks. Presentation only: damage, hitboxes, stun
durations, timing, movement, input, network frames and saves are unchanged.

Pack entrance motion (2026-10-12, `Core/NativeEntrancePack`, `Core/NativePackIntros.Generated.cs`; owner: "Sword and
greatsword intro animations need to change (The player animations)"): the weapon signatures' pilot is driven by one
continuous track per entrance retargeted from the owner's Explosive pack (`tools/export_pack_intros.mjs`, its ENTRANCES
table), sampled on the directed entrance's presentation clock instead of the old baked Electron clip and its weapon-intro
layer. Flash Draw: Relax-Unsheath-R-Hips (calm stand) into Sword-Attack-R2's windup slowed into a coil, R2's backhand
draw-cut pinned to the 1.8 s hit, Armed-Boost1's overhead raise as the flourish and Boost1's raised blade held in salute.
Colossus Oath: 2Hand-Sword Idle-Alert1 (blade up in oath), Jump's crouch and flight (lifted .42 m) with the blade raised,
Attack2's chop biting the floor on the 1.8 s hit, its recovery and the two-handed Idle (blade shouldered). Owner scope
update the same day ("change the player animations on the rest too ... (except daggers, those are good)"): Bastion Drop
(Armed-Shield Idle, Boost1, a .22 m hop landing into Block on the 1.45 s hit, read mirrored onto the right hand as in
play); Deadeye (Pistol-Reload-R1, Armed-Idle-Pistol-R's one-hand aim, Pistol-Attack-R1's shot on 2.12 s carried into the
barrel-up hold); Longwatch (2Hand-Shooting Idle, Crouch-Idle legs under Fire-Rifle1's aim raised level, the 1.8 s shot,
back to the low ready); Belt Rattle (Idle, Activate, Fire-Rifle2's hip stance through the 24 Hz burst, held at the hip).
The magic schools hold the staff in the left hand and cast with the right, as in play: the pack's staff and off-hand casts
are read mirrored (2Hand-Staff; Armed's palm casts for the thrusts). Cinder Crown (Idle, Cast-L-Summon1's sweep back and
raise out to the side as the flame gathers, Armed-Cast-L-Attack-Hand3's wind-up and palm thrust throwing the lance on
1.8 s); Tidal Serpent (Cast-L-AOE1's gathering crouch and raised hand under the rising serpents, Cast-L-AOE2's slam to the
floor on 2.12 s, the crouch held); Thunderstep (the Fall pose lifted 2.3 m, a hard drop into Jump's touchdown on 1.45 s and
its landing crouch, Idle-Alert1); Eventide Gate (Cast-L-Summon2's hand on the floor opening the gate, Summon1's raised
claw, Armed-Cast-L-Attack-Hand2's crouching palm thrust on 2.12 s). A hand pressed to the floor rests on it.
Guns use grip 'gun' (the barrel along the fist's metacarpals) and the gameplay support grips (`RangedPose.Support`); the
pack's rifle fire barely moves the gun, so the shots add an authored recoil (the old layer's follow-through curve). The torso, legs
and root are the capture's; the arms reach the captured grip through the rig's IK with the gameplay melee clearance
(`MeleeClearance`) and a planned forearm/hand clearance, the greatsword's support hand on its grip; planted feet hold the
spots they land on (leg IK, the body sinking up to 6 cm where a lunge outreaches the voxel leg, the heel rising over a deep
crouch), swinging feet are kept out of the floor and the blade tip above it. Camera, effects and timing stay the director's
(shot tweaks for the new motion; the greatsword's scrape sparks now fly where the tip scrapes after the bite). The daggers
and every signature without a track keep their baked clip. `Tests/PackIntroChecks.cs` covers provenance, determinism,
feet, floor, grips, blade and staff clearance, the hit, the detail shots' framing (muzzle or casting hand in frame and in
view, the pilot under 40% of the frame), allocation and the unchanged daggers. Presentation only: the match sees only the
style and its 4.8 s phase; no hit timing, hitbox, damage, cooldown, movement or network change.
