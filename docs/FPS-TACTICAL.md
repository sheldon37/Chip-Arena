# First-person tactical view (KINEMATION Tactical Shooter Pack clips)

Owner, 2026-11-21: "Utilize new FPS system / animations from tactical pack." Claude (F1), branch `claude/fps-tactical`.

## What changes

In first person (chosen in Settings; aiming never forces it), the pistol, rifle and machine gun are held by the pilot model's
own arms and hands, posed by the pack's first-person clips. Since the second pass (owner, same day: "Use pack guns") first
person draws the pack's own gun for each, whatever the skin; without the local guns prefab the game's models are held as in
the first pass (below, "The game's weapon models in hand").

| | Pistol | Rifle (bolt-action sniper) | Machine gun |
|---|---|---|---|
| Gun drawn in first person | WK-11 Viper | Oryx SRM-12 | TR15 |
| Pack clip set | WK-11 Viper | Oryx SRM-12 | TR15 (non-grip idle) |
| Weapon clips (the gun's own bones) | `A_W_..._Fire`: slide, hammer, trigger; `Reload_Tac`: magazine | `Fire`: bolt, trigger; `Reload_Tac`: magazine | `Fire`: bolt, safety, magazine release; `Reload_Tac`: magazine, release |
| Idle | `Idle_Pose` | `Idle_Pose` | `Idle_Pose_Non_Grip` |
| Fire | `Fire` as an additive kick | `Fire` (shot and bolt) as an action | procedural recoil |
| Reload | `Reload_Tactical` | `Reload_Tactical` | `Reload_Tactical` |
| Draw | `Quick_Draw` | `Draw` | `Draw` |
| Walk / sprint / idle sway | the pack's procedural layer (`A_FP_Idle_Loop`, `A_FP_Walk_Loop`, `A_FP_Idle_Sprint_Loop` on `ik_hand_gun_additive`) |||
| Jump | `A_FP_Jump_Start` from the take-off, `A_FP_Jump_Loop` airborne, `A_FP_Jump_End` from the landing (the additive bone) |||

The pack's sprint and walk: `A_FP_Sprint` moves the gun exactly as the idle pose plus `A_FP_Idle_Sprint_Loop` on the additive
bone (the bake compared them frame by frame), and `A_FP_Walk` as the walk pose plus `A_FP_Walk_Loop`; so the sprint carry (the
gun swung across the body) and the walk sway are the pack's own at full strength (intensity 1 with camera shake on), each loop
stepped by the pilot's stride (3 strides per walk loop, 2 per sprint loop), so its cadence follows the movement speed. The
machine gun's pack has no character fire clip (the TR15's automatic fire is the pack's procedural recoil): its shots are the
procedural recoil plus the TR15's own bolt clip each shot.

Plus the look sway, jump hop and landing from the game's own view layer, procedural recoil on every shot, aiming down the
sights onto the model's own rear sight (the rifle's scope as before), and the clips' camera motion (camera shake on).
Third person is untouched. Every other weapon and class (but the Siren, the Top Dog and the Cyber Lord) is held by the pilot's
own arms on the old view model's hands ("Every other weapon and class" below); a scope, the chip select's raised wrist and a
downed or grabbed pilot keep the old view model.

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
| Pack gun's weapon clips | as the arms' clip it belongs to | the same | the reload's and the bolt's on their arms clip's share; a shot's from the shot, within the interval (the TR15's .117 s bolt in the machine gun's .085 s: 1.4x) |
| Jump | start .5 s, end .67 s | none (the pilot's grounded state) | at the pack's pace; airborne for as long as the pilot is |

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
- **The held gun again** (owner 2026-11-22, reversing "Use pack guns": "Can we fix this to use whatever gun we are holding").
  First person draws the gun the pilot holds, the game's model and its skin (Standard or Arsenal) as third person does, still
  posed by the pack's clips. The right hand holds the model's own grip as its grasp does; the support hand sits on the model's
  own measured handguard by third person's hold (T1's `NativeTacticalMotion.Grip`: the long guns' fore-end from below, the
  pistol's thumbs-forward wrap; its palm turned onto the hold, its anchor on the model's `Support`), let go through a reload's
  and a draw's own hand motion. The view rig's right hand is the model's weapon frame again, so the muzzle flash, the brass and
  the chip modules sit on the model as they do in third person (on the pack guns they had stood off it). The pack guns'
  moving parts went with them; the pack guns' prefab is no longer packed. A wide sleeve (the DEV Sheldon robe's kimono, with its
  cloth) is cut cleanly at mid-forearm in first person, the forearm's own skin below it; third person keeps the robe as it
  is. First-person framing targets (integrator review): the long guns lower right with the support hand back toward the
  receiver (`WeaponOffset`, `GripBack`), the aimed pistol a compact hold from lower shoulders (`AimShoulderDrop`), the rifle's
  reload turn 30% of the pack's; the view model covers about 5-13% of the screen at rest and stays out of the central 30% box
  but for the barrel and the rifle's scope front.
- **The pack's guns in hand** (second pass, retired 2026-11-22). The local bake also writes `ChipArenaViewGuns.prefab` beside the bank: the
  pack's `W_WK-11_Viper`, `W_Oryx_SRM-12` and `W_TR15` prefabs unpacked, every script, animator, particle, light and inactive
  attachment stripped, each renderer on a Built-in Standard material rebuilt from the pack's textures (albedo, normal, metal and
  gloss), on the view-model layer without shadows; and bank version 3 adds each gun's muzzle and sight aim point and its weapon
  clips. The gun rides the clip's gun bone (`ik_hand_gun` with the weapon's rotation offset, the frame the pack's arms were
  animated on), so both hands take the pack's own places and palms on its real grips, magazine and bolt, every finger the
  clip's (the pistol's support hand in the pack's thumbs-forward wrap, under and around the firing hand), and the rifle tilts
  the pack's full reload and bolt turn. Aiming brings the gun's aim point onto the eye. The view rig's right hand takes the
  Standard gun's frame with its muzzle on the pack gun's, at the pack gun's share of the Standard gun's length, so the muzzle
  flash, the brass and the chip kit sit on the pack gun. The game's model and its skin are hidden in first person only
  (`VisualWeaponDrawn` for the view rig); third person, the menus and every other rig keep the game's guns and skins. Arsenal
  skins: the pack gun draws as it is in first person (carrying a skin's tint onto it was left out: a skin's look lives in its
  own model's materials and parts, not one tint).
- **The arms run in from the screen's edges.** The view body was cut at the elbows (its first use: the old view model holds
  the hands low); the tactical view's elbows are on screen, so its cut sleeves showed as flat black blocks. The view body now
  keeps a second cut at the shoulders (`TacticalArmCut`) and shows it while the tactical view draws (upper arms, sleeves and
  gloves whole). The old view model keeps the elbow cut: tried at the shoulders on the greatsword, its body (placed by the right
  hand alone) raised whole upper arms into the air.
- **The game's weapon models in hand** (the first pass; still used without the guns prefab). The model's bore and up go on the pack gun's, its rear sight on the pack gun's sight
  line (the pack weapon's AimPoint height), its right wrist level with the pack's along the bore (`NativeTacticalGun`:
  `SightHeight`, `ModelOffset`; the Army pistol's and the light machine gun's rear sights measured on the aimed view as
  `ModelSight`, the rifle's scope from its bake entry). The right hand holds the model as its own grasp does and follows the
  clip relative to the gun (the bolt, the draw, the reload). The left hand takes the pack's palm and its place relative to the
  gun, moved onto the right hand for the pistol (the pack's support wrap) or under the model's own handguard (`Support`) for
  the long guns. The rifle tilts 60% of the pack's reload and bolt turn (`ActionTurn`): at the full tilt its long stock rose
  into the eye. The view rig's right hand sits on the model's fitted weapon frame, so the muzzle flash, the rounds' origin and
  the Q kit pieces stay on the model.
- **The lens and the eye.** The eye is the pack's own camera: its `FPCamera` (the head bone at (0.02, -0.077, 0.035), turning
  with the look, never the head; `camera_bone` is a root-level null at the origin whose animated turn is the clips' camera
  motion, applied by `TacticalCameraKick`). The view model draws at the pack's lens: 90 degrees vertical at the hip (its
  FPCamera), aimed each gun's `TacAnimSettings` aimFov (SRM-12 and TR15 70; the pistol 55 since 2026-11-23, its aim row's own, below);
  the world camera keeps the game's (first
  person 75, the aim row's sight FOV, the rifle's scope). The pack's own idle poses put the Viper 6 degrees right and 21 below
  the centre, the SRM-12 and TR15 25 right and 19-31 below.
  The pistol is the one exception (integrator review 2026-11-22): the pack's two-hand Viper sat near the centre and low, and
  on the pilot's thicker arms read as a block over the view, so at the hip it sits 7.5 cm further right, 6 cm lower and 5 cm
  further out, turned 5 degrees in (`WeaponOffset`, `HipTurn`); the clips' motion plays on it as before, and aimed the sight
  picture is the eye's as before.
- **The pilot's arms** (owner: "Fix pilot's arms"). In the tactical view the body's skin under its garments is hidden (every
  skin triangle within 3.5 cm of a drawn sleeve or glove vertex, `TacticalSkinUnder`, made once per outfit), so only the
  garment draws there and bare arms, bare hands and a short sleeve's hem keep their skin; the forearms are 82% and the upper
  arms 90% of their girth (their twist bones, never their length; put back every frame for the old view model); a fill light
  from over the eye lights the view-model layer alone (`TacticalFill`, .55, no shadows) so the hands and sleeves read on dark
  stages as the gun does.
  The arm cut never turns on a renderer the outfit switched off (the dressing hides the skin parts under its garments and the
  skin-coloured hologram proxy `ProxyMesh`; turned on, the proxy showed as brown patches on the sleeves).
- **Sights.** The SRM-12 carries its iron sights (`A_Oryx_SRM-12_IronSights`; the Phantom TAC scope filled the right half of
  the view), the TR15 its own; aiming the rifle still brings up the game's scope.
- **The other pack guns** (AK105, MX68, Mk14 EBR, R08, Herrington 11-87, MKR9) are not imported.
- **Camera motion**: `TacticalCameraKick` turns the camera by the playing clip's `camera_bone` after the aim point is taken
  (as `HitKick` does), first person with camera shake on, .4 with reduced effects; the arms are placed in the camera without
  it, as the pack's arms are on the body.

## The sights on the shots' line; the machine gun on the TR15 (owner 2026-11-23)

Owner: "For the machine gun, can we fix first person to aim the iron sights where the shots are going? (Probably a easy solution
for this, lets replace our machine gun model with the one from the pack)". Claude (G1), branch `claude/mg-tr15`.

- **Where the shots go.** The client sends the screen centre's hit (the camera's centre ray, `ConversionGame.AimPoint`) as
  AimTarget, and the authority fires from its muzzle to that point inside the gun's cone, so the rounds land on the screen centre
  at any range. The view-model camera is the world camera's child (same eye and axis), so a sight on the eye's axis points there.
- **Measured** (G1's local lab: a test wall at 5, 15 and 40 m, Cyber City, 2000x1173, aimed and firing; the sights projected
  through the view camera, the impacts and the aim target through the world camera): before, the machine gun's front post sat
  4.9 degrees under the shots (72 px; 44 cm low at 5 m, 1.3 m at 15 m, 3.6 m at 40 m: its rear sight point, `ModelSight`, sat 6 cm
  over the light machine gun's real sights), the pistol's 1.0-1.1 degrees under them (13 px; 6.5, 30 and 79 cm: its ModelSight
  sat 6.7 mm over the slide), the rifle's scope reticle on them (0.00-0.03 degrees). After: both guns' front posts 0.00-0.02
  degrees off the aim ray, the impacts scattered round it inside the cone (their means 0.1-0.2 degrees off, four rounds each).
- **The front post** (`NativeTacticalCalibration.Front`, `NativeTacticalMath.SightTurn`): aimed, the model's rear sight goes on the
  eye's axis at the gun's eye relief as before, and the line from it through the front post now runs along the axis (pitched
  about the gun's own right axis, then yawed: no roll). The post comes from the bake entry's `SightFront` marker, else the gun's
  `ModelFront` (the Army pistol's, measured on its slide); a model with neither keeps the bore along the view.
- **Steady aim** (`AimSteady`): fully aimed, the pack's procedural layer still turns the gun but no longer moves it. Its idle
  breathing only ever dips the gun (up to 4.4 mm at the aim's intensity), which on the TR15's aperture 9.5 cm from the eye moved
  the sight picture up to 2.7 degrees. The look sway, landing and recoil are unchanged: a burst still lifts the sights between
  rounds, and they settle back on the shots.
- **The TR15 is the machine gun's Standard model** everywhere a visual body draws it (third person, first person, the menu and
  Locker pilot, portraits, intros, corpses and replays): the pack's TR15 made static by the local lab (its skinned parts baked at
  rest, the magazine turned onto its socket, the hidden magazine rounds left out, the iron sights on its scope mount; 29,329
  triangles), on the arena toon like every other weapon (spawn-in, hit flash, the corpse's fade) with colour maps lifted from the
  pack's near-black albedo and its normal and metal maps baked in (scratch `toonalbedo.py`; 1024 body, 512 magazine and sights).
  The local tool's weapons table places it from its meshes (kit frame, +x the bore): the bore at y 0, the flash hider .4174, the
  butt -.3551, the fist 4.5 cm under the grip's top on its raked axis, the support under the handguard's right edge (T1's hold),
  `Sight` the rear aperture's centre and `SightFront` the front post's tip (4.8 mm over the aperture's line, so aimed the bore
  tips down 0.74 degrees). In first person it sits on the clips' gun bone where the pack's demo holds it (`SightHeight` .0639,
  `ModelOffset` z -.012), the eye 9.5 cm behind the aperture (`EyeRelief`, the pack's AimPoint). The game's fit lays the chip
  modules, the muzzle flash and the brass along its bore (scale .646). The Arsenal machine guns keep their models.

## The aimed pistol larger and aimed recoil from the pack (owner 2026-11-23)

Owner: "The pistol ADS still looks a bit small and simulates no recoil; Is this something we can fix? (I dont know if recoil is
in the pack or not as well)". Claude (G2), branch `claude/ads-recoil`.

- **Why it read small.** Aimed, the pistol's rear sight sat 34 cm from the eye on an 80 degree view-model lens while the world
  zoomed to the aim row's 55: the slide was 46 px wide on 2000x1173 (0.09 % of the screen visible above the hands). The pack's own
  Viper aims with the eye 16.1 cm behind its rear sight on 80 (its AimPoint; about 97 px for our slide). Now the rear sight sits
  24 cm from the eye on the aim row's own 55 degrees (`EyeRelief`, `AimFov`): the gun zooms in with the world and the slide is
  about 100 px wide, the hands below it. The sights stay on the aim ray (G1's front post: the line from the rear sight through it
  runs along the eye's axis whatever the eye relief or lens).
- **Why aimed shots barely moved.** The pistol's fire clip played before the aim and the aim's blend laid the gun on the sights
  over it, so fully aimed it was wiped; what was left was the procedural spring after the aim (the pistol's at 0.6 of its kick,
  peaking 0.27 degrees, the machine gun's 0.42); the rifle's scope never moved (the tactical view stands down while scoped).
- **The pack's recoil, as data** (`Core/NativeAimKick.cs`). The pack carries per-gun procedural recoil assets and camera shakes;
  their values are read as data (sampled every 1/60 s by the scratch tool `kickdata.py`; no pack code in git or at run time) and
  played by our own code, aimed (it takes over from the spring as the aim comes in; the hip is as before):
  | | Pistol (Viper data) | Machine gun (TR15 data) | Rifle (SRM-12 data) |
  |---|---|---|---|
  | Per shot | muzzle up 9-12 degrees, yaw 1-2, roll 2-3 (alternating), 14-16 mm back toward the eye, about the gun bone | 0.56-0.72 up (x1.1 ours), small yaw and roll, 8-9 mm back, about a pivot 4.5 cm under and 3 cm ahead | 1.8-2.4 up, roll 1.5-3, 10-15 mm back |
  | Shape | the pack's semi curves, quickened to end within 0.9 of the shot's interval | the pack's automatic curves, cut at the next round; smoothed (30/15/25 per second, pitch x2.5) from the second round; a climb of 0.06 degrees a round to 0.6; once the next round does not come both settle fast (45 per second, the climb critically damped at 55) | the pack's semi curves, on the scope overlay |
  | Camera (camera shake on) | half the pack's pistol shake: up 0.35-0.5, roll 0.75-0.95 | 0.4 of the light rifle shake | none (the scope overlay kicks) |
  | Mechanism | the slide (the Army pistol's `SM_Gate_For_Army_Pistol`, an Arsenal pistol's `*_Bolt`) runs back 3.2 cm on the Viper's own slide timing (67 ms) | the TR15's bolt is unchanged (third person) | |
- **Recovery** (measured: see the DEV_LOG entry). Every shot is back on the aim ray by the time the rules allow the next one; the
  machine gun's burst settles within about 0.15 s of the next round's time once it stops.
- **Rules.** Presentation only: the aim ray (taken before any camera turn), spread, the rules' recoil, fire rate, hitboxes and the
  wire are untouched; the view reads the pilot's own LastShot and ShootReady. Aimed the sights lift off the aim ray during a kick
  and are back before the next shot; the rounds still go to the screen centre.
- **Checks:** `--tactical-view` (`TacticalViewChecks.AimKick`): the shapes start and end at rest, the pistol's kick and recovery by
  the next shot over a string at its rate, the machine gun's climb and settle, the scope's kick, the hip and camera-shake gates,
  a fresh view not replaying a shot, the lens; the sources' gates (the slide put back, the overlay's matrix restored, no rule state).

## Every other weapon and class: the pilot's own arms (owner 2026-11-22: "Go ahead and fix")

Claude F1 then F2, branch `claude/fps-melee`. The sword, Katana, daggers, Fangs, glaive, greatsword, shield, bow, the four
staffs and L33t, the Bombadier, the Emperor, Ansatsuken and the DEV Greatsword and Katana (`Core/NativeViewArms.cs`,
`Runtime/NativeViewArmsPresentation.cs`); the guns keep the tactical view, the Siren, the Top Dog and the Cyber Lord their own.

- **The old view model stays the motion.** Its clips, hands, weapon, trails and effects are placed as before; the view body
  then turns with the eye at true size, its shoulders on an eye-anchored place (24 cm under the eye, level with it), and each
  arm reaches its hand by two-bone IK with the elbow hung under the frame, cut at the clavicles, so the arms run in from the
  screen's edges. The tactical view's girth, proxy rule and fill light come with it.
- **Each class's hold** (`NativeViewArms.Holds`): a lift of the whole old view rig (weapon, hands and effects together), measured
  on the view at rest at 2000x1173 with the view-body probe, so the weapon hand sits near x .8, y .8 of the screen, the
  greatsword's hilt low right with its blade at the right edge, the shield's hand within the arm's reach, the staff low left
  with its head clear of the centre, the fighters' fists either side of the centre at the bottom. A bow adds a carry lift
  while lowered (times 1 - Raised): its carry under the eye comes into view low left, its draw stays on the arrow line.
- **The free hand.** The one-hand blades' left hand, the staffs' right and a lowered bow's string hand hold nothing: each is
  tucked under the frame while it hangs low (`TuckWeight`, by how far under the view's centre line it is) and comes back as it
  rises into a cast or a draw (the lightning and L33t specials reach out). Nothing rides it.
- **The reach.** An arm too short for its hand reaches from its shoulder first; the arms' own IK reads the elbow again after the
  shoulder moves (read before it, the forearm fell short and the hand floated up to 30 cm off its wrist: the greatsword and the
  shield). The guns' `TacticalReach` is unchanged.
- **The dark cloth.** The default suit's sweater is .07 sRGB (.006 linear) and read as flat black in any light. While these arms
  draw, every view-body renderer takes `_ViewCloth` (`Resources/NativeVisualBodyFx.cginc` `ViewCloth`): a near-black colour
  (under .01 linear luminance, fading out by .08) lifted by `NativeViewArms.Look.Cloth` (.06 linear), so the sleeves take the
  view layer's light as dark cloth; skin, gloves and trims are unchanged; the guns' view and third person never set it.
- **Rules.** Presentation only: no hit timing, hitbox, damage, cooldown, movement value or network message is read or written;
  the lifts and the tuck move first-person drawing only. Aiming never forces first person.
- **Checks.** `--tactical-view` holds the table (every class its own finite hold, the free hands, the tuck and carry curves), the
  shader's off-unless-set lift, the reach fix and that nothing else sets `_ViewCloth`. Editor: F2's runs switch every class in
  one capture by the DEV weapon command (rest, attack, guard, special) with the probe.

## Import and disk

Only the clips the view uses, FBX and metas with their GUIDs, under the Editor checkout's git-ignored
`Assets/LocalAssets/KinemationTactical/Animations`; for the pack guns the three weapon prefabs, their attachment prefabs and
meshes (43 assets) under `KinemationTactical/Prefabs` and `Meshes`, and their textures converted from the pack's 4K TGAs into
`KinemationTactical/ViewGuns/Textures` at shipping sizes (bodies 1024, their metal/gloss 512, magazines 512 and 256; the
channels Unity does not read flattened): 3.3 MB, the meshes 2.6 MB, the prefab 0.2 MB, the bank 1.2 MB. No scripts, the demo
scene, URP materials or sounds. The pack stays parked in `F:/Chip Arena Development/AssetStoreCache/KINEMATION/Animation`.

## Shipping (the builder)

`tools/pack_visual_body.py` also follows `ChipArenaViewGuns*.prefab` beside the visual body prefab (the bank already travels
as a `.bytes` there): the guns' meshes, materials and textures go into the Drive zip's visual body. Together about 7.4 MB on
the visual body's 469.9 MB (the 480 MB cap stays, docs/EXPEDITION-OVERHAUL.md Q10). The 2048 conversions are kept outside the
project for a later swap if the cap is ever raised.

## The pack's sounds (not used; for the owner)

149 WAV files (112 MB): per weapon the draw, holster, inspect, mag check, reloads (tactical, empty, the shotgun's loop and end,
the revolver's cylinder), fire (suppressed and unsuppressed with tails: AK105, TR15 four variants, MX68, Mk14 EBR, SRM-12,
R08, Herrington, MKR9) and general aim in/out, equip/unequip, fire mode, jump, land, pickup, pistol quick draw/holster, and
five concrete walk and run footsteps.

## Checks

- `dotnet run --project unity/ChipArenaConversion/Tests/CoreTests.csproj -c Release -- --tactical-view` (also in the full run).
- Editor captures: `first=1` play captures, before and after, per gun.
