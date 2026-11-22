# Third person on the Tactical Shooter Pack (guns)

Owner, 2026-11-21: "Can we fix the animations to use their shooting animations and walking/running for guns?" (and, quoting the
pack: "All animations read clean in first- and third-person"). Claude (T1), branch `claude/tp-tactical`. First person is F1's
(`docs/FPS-TACTICAL.md`, branch `claude/fps-tactical`); the two pair the same clip sets per gun.

## What changes

In third person (every pilot others see, and your own in third person) the pistol, rifle and machine gun play the pack's clips on
the upper body, on the game's own gun models and Arsenal skins:

| | Pistol | Rifle | Machine gun |
|---|---|---|---|
| Pack set | WK-11 Viper | SRM-12 | TR15 (non-grip) |
| Aim (raised: aiming, a shot, a Q/E that raises it) | `Idle_Pose` (two-handed, held out `PistolReach`) | `Idle_Pose` (shouldered) | `Idle_Pose_Non_Grip` (shouldered) |
| Shot | `Fire` (the kick) | `Fire` (shot and bolt) | procedural kick per shot (the pack has no TR15 fire clip either) |
| Reload | `Reload_Tactical` | `Reload_Tactical` | `Reload_Tactical` |
| Carry on the move and in the air | `Idle_Pose` (held out) | `A_FP_Walk`'s ready hold, `A_FP_Sprint`'s port carry by the sprint's share | the same |
| Draw (no rule draws a gun: on the round's GO, as the spawn-in times it) | `Quick_Draw`, 0.6 s | `Draw`, 1.1 s | `Draw`, 1.2 s |
| Pitch | the spine (`VisualTacticalPitch`), the barrel's own small turn as before | | |
| Support hand | wraps the right (the clip's) | closed on the model's handguard: the pack's TR15 foregrip fist (`SupportFist`) | the same |

Unchanged: the standing match idles (the long guns' low carry, the pistol's low ready; owner 2026-11-10 and `match-idles-keep`),
the legs (the game's 8-way runs, walks and sprint, jumps and landings), the guns' Q/E clips, techniques, reactions, knockdowns,
first person and every other weapon. The long guns' support fist still seats on the model's own handguard (`Support`), and the
weapon still rides the right hand (the pack's ready hold by the pack's share of the upper body, `TacHold`; the gun's own set's
hold at rest, so the idles do not move).

## What the pack's clips are (measured on the bake)

- **The walk, sprint and jump clips move no leg.** Every frame of `A_FP_Walk` (4.05 s), `A_FP_Sprint` (2.7 s), `A_FP_Walk_Loop`,
  `A_FP_Idle_Sprint_Loop` and `A_FP_Jump_Start/Loop/End` has the feet and pelvis at the same place (feet at (-0.223, 0.082,
  0.019) and (0.202, 0.082, -0.164) throughout the walk). They are first-person clips: the walk and sprint pose the arms (the
  ready hold with a small sway, the port carry); the jumps and the loops only drive the pack's additive gun bone and leave the
  body at rest. So the legs stay the game's strides and jumps, and the pack supplies the carry over them. There is no strafe or
  backward set; the carry is the same whichever way the legs go, so the 8-way blend is unchanged.
- **The draws** (`A_FP_WK-11_Viper_Quick_Draw`, `A_FP_SRM-12_Draw`, `A_FP_TR15_Draw`) carry the hands with the gun (measured: the
  right hand holds still in the gun bone's frame through every frame). The bake also seats each hand at its place on the gun in
  that gun's idle pose with the idle's closed fingers (`ikgun=`; the right throughout, the left over the draw's last part), so the
  draw ends exactly on the aim's grip. The game has no weapon swap, so the draw plays once a round, ending on GO; the holsters are
  not played (nothing in the rules puts a gun away).
- **The pack's support hands lie flat** under the handguard with the fingers out (SRM-12, TR15 non-grip, the walk and sprint), which
  read as open, splayed hands on the model; the long guns' left fingers take the TR15 foregrip's closed fist while the support
  fist holds the model's handguard (`VisualTacticalFist`), and the clip's own while it lets go (a reload).
- The aim, fire and reload clips pose the whole upper body with the hands on the pack's gun; their legs are a still stance (the
  legs' clip shows instead).

## Timing on the rules (AGENTS.md animation impacts)

Nothing here changes a rule: no hit timing, hitbox, damage, cooldown, movement value or network message is read differently or
written. The clips are fitted to the rules (`Core/NativeTacticalMotion.cs`, held by `Tests/TacticalMotionChecks.cs`):

| Clip | Pack | Rule | Fit |
|---|---|---|---|
| Rifle shot and bolt | 1.667 s, bolt home (`OnActionEnded`) at 1.167 s | `Interval` 1.15 s | the bolt home on the interval (1.015x), the settle after at its pace |
| Pistol kick | 0.667 s, settled by 0.42 s | `Interval` 0.42 s | its own pace (a new shot restarts it) |
| Machine gun kick | none | `Interval` 0.085 s | 2.4 degrees up the spine, gone in 0.11 s (held fire keeps it shaking) |
| Pistol reload | back on the gun at 3.083 s | `Reload` 1.1 s (`ReloadAt..ReloadUntil`) | 2.8x, back on the gun exactly at `ReloadUntil` |
| Rifle reload | back on the gun at 2.75 s | 2.6 s | 1.06x |
| Machine gun reload | back on the gun at 3.0 s | 2.4 s | 1.25x |
| Raise onto the aim | | `Settle` (pistol .15, rifle .3, machine gun .2 s) | the raise eases in over the Settle when aiming; a hip shot raises at once (0.12 s) |
| Carry | walk loop 4.05 s, sprint loop 2.7 s | | on their own clocks (their sway is not stepped); the legs keep the stride's rate |
| Draw | Viper quick draw 0.667 s, SRM-12 1.5 s, TR15 1.833 s to `OnActionEnded` | none (no weapon swap); the round's GO | 0.6, 1.1, 1.2 s ending on GO (1.1x, 1.36x, 1.53x) |

## The hands on the gun (owner 2026-11-22: "think we need to fix this here"; "sometimes it appears the gun and hand aren't connected")

- **The support hand grips the model in every state the pack's layers hold the gun in**: the aim standing and walking, the run's and
  sprint's carries, a jump, each shot, and a reload's start and end (`NativeTacticalMotion.GripWeight`: the pack's share of the upper
  body, let go over `ReloadLetGo` 0.15 s after `ReloadAt` and taken again over `ReloadRegrip` 0.25 s before `ReloadUntil`, joining a
  draw over its last part). `VisualTacticalGrip` turns the palm's frame (wrist to the middle knuckle, pinky to index) onto the gun's
  hold and reaches the arm so the hand's anchor sits on the model's measured `Support` (each Standard and Arsenal model's own):
  - the long guns' fore-end (`ForeEnd`): the palm under the handguard, the fingers across under it and closed up its far side, the
    thumb along its near side pointing forward, the closed fist's hollow on the handguard's axis;
  - the pistol's thumbs-forward wrap (`Wrap`): the support palm on the grip's left side, the fingers round the front over the gun
    hand's, the thumb up the frame pointing forward.
  The fingers close to the pack's TR15 foregrip fist by the same weight (`VisualTacticalFist`); half the wrist's new twist goes into
  the forearm. Before, the pack's carry left the support hand open on the near face (its own support hands lie flat, fingers out),
  and the old reach let go beyond 0.40 m.
- **A long gun grips at rest too, and between states.** Its match idle's low carry closes the hand on the handguard like the carry
  and the raise do; the grip's share is at least the body's readiness (`GripHold`, eased once over 0.12 s), not only the product of
  those three layers' eased shares, which dipped as one eased out and the next in (the hand slid up to 4 cm off the handguard for a
  moment after a reload, into a jump, or on stopping). The pistol keeps its match idle's low two-hand ready and the game's one-hand
  sprint (the pack has no pistol sprint carry), its support hand free there.
- **One hold in the hand.** A pack gun sits in the gun hand at one hold, the pack's ready hold; the gun set's own hold differed from
  it by 24.3 degrees (rifle; 21.5 machine gun, 17.0 pistol), which turned the gun in the palm whenever it was raised or lowered.
  That difference is now the wrist's.
- **The gun follows the hand.** A raised gun's barrel turn onto the aim (at most 35 degrees) and a lowered gun's 20-degree low carry
  used to turn the gun alone about its grip, inside the palm. They now turn the gun hand at the wrist and the gun is placed on the
  hand again (`VisualGunWrist`), so the grip never slides or turns in the palm. This holds for every gun, with or without the pack's
  bank; at rest the long gun's low carry is now a 20-degree wrist bend, the gun's own angle unchanged.
- **The order** (pinned in `TacticalMotionChecks`): the motion's pose, the spine's pitch and kick, the pistol's reach, the wrist's
  turns, the weapon on the hand, the support hand last; then the hand's fit, then the PuppetMaster hit bend (which carries the rig's
  hand frames with the drawn hands); every body poses before any model draws, in the same frame.

## How it is built

- **No pack code runs or is committed.** A local bake (git-ignored `Assets/LocalAssets/ChipArenaLocalTools/Editor/ChipArenaT1Bake.cs`,
  polling `Temp/t1-tpbake.request`) samples each pack FBX (imported Generic under the git-ignored `Assets/LocalAssets/KinemationTactical`)
  on its own skeleton and reads it back as a humanoid pose through an avatar built from the pack's own humanoid reference skeleton
  (`SKM_Operator_Humanoid.fbx.meta`, read from a copy outside the project) with the body's bone map (`BodyRig.fbx.meta`), so a
  muscle is the same joint on both. The records are appended to the released motion bank (`ChipArenaVisualMotion.bytes`, 499
  clips carried byte for byte) as the CAMO format the game already reads.
- `Core/NativeVisualMotion.Tactical(spec, set, bank)` gives a gun its pack set only when the bank carries every clip it plays;
  without them (the builder before the bank lands, CoreTests' synthetic banks) every gun keeps its own set.
- `Runtime/NativeTacticalMotionPresentation.cs`: the carry (upper-body muscles over the stride, the legs' hips kept), the spine's
  pitch and the machine gun's kick, the pistol held out.
- **Rebaking the bank:** the BoZo tool's full motion rebake (`MotionSpecs`) does not know the pack's clips (it reads humanoid clips
  only). After any full rebake, append the 14 pack records again with `ChipArenaT1Bake.cs` (base bank = the new one; the draws with
  `ikgun=`).

## Checks

- `dotnet run --project unity/ChipArenaConversion/Tests/CoreTests.csproj -c Release -- --tp-tactical` (also in the full run).
- Editor play captures (Cyber City, `weapon=`), before and after.
