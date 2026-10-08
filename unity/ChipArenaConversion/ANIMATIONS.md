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

`MagicPose` ports the source analytic primary/Q/E casting phrase and first-person variants. Magic no longer plays sword attacks/blocks: the staff is in the left hand, the right hand casts, and the guard lays the staff across the chest. Source phrase validation covers 1,728 samples. The Unity quaternion/IK adaptation and guard transitions still require visual acceptance. `RangedAbilityPose` adds support-hand grenade throws/visor taps and rocket recoil using source gesture keys.

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
