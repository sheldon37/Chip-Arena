# First-person tactical view (KINEMATION Tactical Shooter Pack clips)

Owner, 2026-11-21: "Utilize new FPS system / animations from tactical pack." Claude (F1), branch `claude/fps-tactical`.

## What changes

In first person (chosen in Settings; aiming never forces it), the pistol, rifle and machine gun are held by the pilot model's
own forearms and hands, on the game's own weapon models, posed by the pack's first-person clips:

| | Pistol | Rifle (bolt-action sniper) | Machine gun |
|---|---|---|---|
| Pack clip set | WK-11 Viper | Oryx SRM-12 | TR15 (non-grip idle) |
| Idle | `Idle_Pose` | `Idle_Pose` | `Idle_Pose_Non_Grip` |
| Fire | `Fire` as an additive kick | `Fire` (shot and bolt) as an action | procedural recoil |
| Reload | `Reload_Tactical` | `Reload_Tactical` | `Reload_Tactical` |
| Draw | `Quick_Draw` | `Draw` | `Draw` |
| Walk / sprint / idle sway | the pack's procedural layer (`A_FP_Idle_Loop`, `A_FP_Walk_Loop`, `A_FP_Idle_Sprint_Loop` on `ik_hand_gun_additive`) |||

Plus the look sway, jump hop and landing from the game's own view layer, procedural recoil on every shot, aiming down the
sights onto the model's own rear sight (the rifle's scope as before), and the clips' camera motion (camera shake on).
Third person is untouched. Every other weapon and class, a scope, the chip select's raised wrist and a downed or grabbed pilot
keep the old view model.

## Timing: clips on the game's rules (AGENTS.md animation impacts)

Nothing here changes a rule. No hit timing, hitbox, damage, cooldown, movement value or network message is read differently
or written. The clips are retimed onto the rules (`Core/NativeTacticalView.cs`, `NativeTacticalTiming`, held by
`Tests/TacticalViewChecks.cs`):

| Clip | Pack length | Game rule | Retime |
|---|---|---|---|
| Pistol reload | 3.47 s | `Reload` 1.1 s (`ReloadAt..ReloadUntil`) | warped: the dip and the off-screen reach quickened, the insert held (about 3.2x on average) |
| Rifle reload | 3.08 s | 2.6 s | uniform 1.19x |
| Machine gun reload | 3.48 s | 2.4 s | uniform 1.45x |
| Rifle shot and bolt | 1.17 s to `OnActionEnded` (1.67 s with settle) | `Interval` 1.15 s, or `ShootReady` | the bolt is home when the next shot is allowed; the settle plays after at its own pace |
| Pistol kick | 0.67 s | `Interval` 0.42 s | quickened to end within the interval |
| Aim down the sights | the pack's procedural aim | `Settle` (pistol .15 s, rifle .3 s, machine gun .2 s) | the aim's progress moves at 1/Settle, eased |
| Draw | Viper quick draw .67 s, SRM-12 1.5 s, TR15 1.83 s | none (no weapon swap rule) | .5 s, .9 s, 1 s; any shot cuts it |

The reload clip ends, back at rest, exactly at `ReloadUntil`; the empty reloads (the pistol's slide release, the rifle's
bolt) do not fit the rules' spans, so the tactical reloads play every time.

## How it is built

- **No pack code runs.** The local bake (git-ignored `Assets/LocalAssets/ChipArenaLocalTools/Editor/ChipArenaTacticalBake.cs`,
  polling `Temp/f1-tacbake.request`) samples the pack's own FBX clips on their own skeleton in the Editor and writes the
  view-motion bank `ChipArenaViewMotion.bytes` (`Assets/LocalAssets/ChipArenaLocal/Resources`, git-ignored, about 1.5 MB). Per
  frame, in the pack camera's frame: the gun bone (`ik_hand_gun` with the weapon's rotation offset), both hands, elbows and
  shoulders, both palms, the 15 finger segments per hand as directions in the palm, `camera_bone` and `ik_hand_gun_additive`.
  The game reads the bank by name (`Core/NativeTacticalView.cs`) and solves the view in Unity-free code; without the bank the
  old view model draws. The bank travels with the other `.bytes` beside the visual body in the LocalAssets zip
  (`tools/pack_visual_body.py` already carries every `.bytes` there), so no build tooling changes.
- **The pilot model's own arms** (`Runtime/NativeTacticalViewPresentation.cs`). The view body (the outfit's forearms and hands,
  its sleeves, gloves, skin, costume tints and accent glow) is set at true size with its shoulders on the clip's; each arm
  reaches by two-bone IK with the clip's elbow as the pole; the wrist's new roll goes into the forearm twist bones; the clip's
  fingers (by direction in the palm frame) replace the grasp's while an action moves the hands, the left hand's always.
  The BoZo skeleton shares the pack's bone names (`upperarm_l`, `index_01_l`, `lowerarm_twist_01_l`...), so no retarget table.
  - Why not the pack's arms: its Operator arms (tactical gloves and shirt) cannot wear the pilot's outfit, robot shell or
    costumes, and a second arm rig would need its own look, cut and glow work; the pilot's arms already carry all of that.
- **The game's weapon models in hand.** The model's bore and up go on the pack gun's, its rear sight on the pack gun's sight
  line (the pack weapon's AimPoint height), its right wrist level with the pack's along the bore (`NativeTacticalGun`:
  `SightHeight`, `ModelOffset`; the Army pistol's and the light machine gun's rear sights measured on the aimed view as
  `ModelSight`, the rifle's scope from its bake entry). The right hand holds the model as its own grasp does and follows the
  clip relative to the gun (the bolt, the draw, the reload). The left hand takes the pack's palm and its place relative to the
  gun, moved onto the right hand for the pistol (the pack's support wrap) or under the model's own handguard (`Support`) for
  the long guns. The rifle tilts 60% of the pack's reload and bolt turn (`ActionTurn`): at the full tilt its long stock rose
  into the eye. The view rig's right hand sits on the model's fitted weapon frame, so the muzzle flash, the rounds' origin and
  the Q kit pieces stay on the model.
- **The lens.** The view model draws at 85 degrees at the hip, 74 aimed (the pack frames its arms for its camera's 90, its aim
  70-80); the world camera keeps the game's (first person 75, the aim row's sight FOV, the rifle's scope).
- **The pack's weapon meshes** (AK105, TR15, MX68, Mk14 EBR, SRM-12, R08, Herrington 11-87, MKR9, WK-11 Viper; 4K TGA
  textures) are not imported. They could become weapon skins (the owner's call).
- **Camera motion**: `TacticalCameraKick` turns the camera by the playing clip's `camera_bone` after the aim point is taken
  (as `HitKick` does), first person with camera shake on, .4 with reduced effects; the arms are placed in the camera without
  it, as the pack's arms are on the body.

## Import and disk

Only the clips the view uses, FBX and metas with their GUIDs, under the Editor checkout's git-ignored
`Assets/LocalAssets/KinemationTactical/Animations` (about 75 MB). No textures, meshes, scripts, the demo scene, URP materials
or sounds. The pack stays parked in `F:/Chip Arena Development/AssetStoreCache/KINEMATION/Animation`.

## The pack's sounds (not used; for the owner)

149 WAV files (112 MB): per weapon the draw, holster, inspect, mag check, reloads (tactical, empty, the shotgun's loop and end,
the revolver's cylinder), fire (suppressed and unsuppressed with tails: AK105, TR15 four variants, MX68, Mk14 EBR, SRM-12,
R08, Herrington, MKR9) and general aim in/out, equip/unequip, fire mode, jump, land, pickup, pistol quick draw/holster, and
five concrete walk and run footsteps.

## Checks

- `dotnet run --project unity/ChipArenaConversion/Tests/CoreTests.csproj -c Release -- --tactical-view` (also in the full run).
- Editor captures: `first=1` play captures, before and after, per gun.
