# The Hot Dog skin

The owner's requests on 2026-11-08: "Implement this model as a dev skin (Do clean up on it if necessary)" and "Id like to also
give the body physics (Its a hotdog lol)". The model is the owner's Tripo hot dog mascot (Tripo's auto-rig, FBX). It is a
whole-body skin that takes the place of the outfit. It is presentation only: no rule, hit volume, timing, cooldown, movement
or network authority reads it.

## Where it lives

| Part | Where |
|---|---|
| The pick | `upper:hot-dog` in the outfit map (`Core/NativeLook.cs`). It is DEV only (`NativeLook.DevOnly`), so the Locker shows it only with live DEV access, last in Tops before the Sheldon robe. `Gate` drops it otherwise. |
| The rules of the look | `Core/NativeHotDog.cs`: the id, each hat's fit on the crown (`HatFit`, `HidesSprigs`), and the sausage's jiggle (`Jiggle`, `Step`, `Pose`, `Bend`). |
| The drawing | `Runtime/NativeHotDogPresentation.cs`: it reads the bank and puts its one skinned mesh on the body's bones in place of everything the body wears. The jiggle (`HotDogJiggle`) runs after the live pilot's body is posed. |
| The bank | `Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaHotDog.bytes` (HDOG v2). It is git-ignored and ships in the Drive zip (`tools/pack_visual_body.py` takes every bank beside the body's prefab). Without it, a pilot in the Hot Dog wears the default top. |
| The build | `tools/gear/hotdog_tripo.py` (Blender 5.x, headless). It reads the FBX it is given. Never commit the FBX or the bank. |
| The checks | `Tests/HotDogChecks.cs` (`CoreTests --hot-dog`, also in the full run) and the Hot Dog lines in `Tests/LookContractChecks.cs`. |

## Rebuilding the bank

```
blender --background --factory-startup --python tools/gear/hotdog_tripo.py -- --fbx FILE.fbx --dump BINDDUMP
    --out Assets/LocalAssets [--texture IMAGE] [--preview DIR]
```

- `--dump` is a bind dump of the model, the local tool's play `binddump=T` (`bind.txt`, `rest.Body.Body_AnimeBasic*.obj`).
- `--texture` takes the export's own colour image, which is embedded as it is. The FBX names
  `tripo_rgb_ccbeb702-c64d-47fb-b348-6aaf8fce7a49.jpg` but does not carry it; the owner's Tripo download zip
  (`tripo_convert_3edf9985-....zip`: the same FBX and that 2048 px JPEG in its `.fbm` folder) does. The bank in the Drive zip is
  built with it since 2026-11-08. Without `--texture` the script paints a 1024 px stand-in from the model's parts and the
  sausage's relief, in the image's colours:
  - a hot dog red sausage, its ends browned;
  - yellow mustard, dark brows, white eyes with black pupils in red-rimmed lids;
  - white teeth in a dark mouth;
  - a yellow and orange crown, charcoal noodle limbs, white gloves with yellow-trimmed cuffs;
  - red shoes with yellow rims and white laces.
- `--preview` renders a sheet beside the pilot's body, plus close-ups of the face, a hand and a shoe.

## The clean-up and retarget

The export is 14,814 triangles in 36 closed parts, skinned to Tripo's 67-bone humanoid at rest with its arms hanging. The
crossed arms it shows are its stored animations (angry, box, dance and flee), which are not used. The retarget fits it to the
model's skeleton, so the pack's clips, planted feet, weapon grips, IK, corpses and first person all work as they do on the pilot:

- **The sausage and everything on it** (face, mouth, teeth, pupils, crown sprigs and the end knot) is one rigid piece, scaled
  1.3 times.
  - Its bottom sits at 0.92 m, just under the hip joints at 0.961 m.
  - It is centred on the spine at the shoulders, so the arms leave its sides.
  - Its weights follow the pilot's spine by height: a bell round each spine bone 0.12 m wide, the neck's held at 0.6 and the
    head's at 0.35, so a bent spine bends it smoothly and the head's pitch doesn't fold its top. They sit on twins of the
    spine's bones.
- **The arm and leg tubes** are mapped bone by bone onto the pilot's bones. They are stretched along each bone to the pilot's
  length and thickened 1.3 times across. The legs grow about 3.6 times: the skeleton's stride is the pilot's, so the Hot Dog has
  long noodle legs.
- **The gloves** are scaled 1.3 times, each finger laid along the pilot's finger, so the pack's grips close them.
- **The shoes** are one rigid piece on the foot, 1.2 times longer and wider. Their height is kept, so their soles meet the
  floor where the pilot's do.

It stands about 1.86 m to the top of its crown sprigs. The pilot's hair tops out around 1.80 to 1.90 m.

## The jiggle (body physics)

The sausage's weights sit on twins of the pelvis, spine_02 to spine_05, neck_01 and the head. Each twin is a child of its bone,
at rest where the bone is. Each frame after the pose, a damped spring on the sausage's top steps on the pilot's floor point
(`NativeHotDog.Step`). The floor point carries the pilot's motion without the clips' bob, so a run doesn't set it bouncing.
Its tuning:

- a soft wobble, about 2.4 a second (ω 15 rad/s, damping ratio 0.25);
- the top keeps 0.8 of each sideways change in the body's velocity and 0.35 of each upright one, a change counting for at most
  6 m/s a frame.

It lags behind a start, a stop, a turn or a jump and wobbles back:

- **Lean:** the sideways part leans the sausage about the pelvis, more toward its top, so it bends. It goes up to 0.14 m at the top.
- **Squash:** the upright part squashes or stretches it along world up, up to 16%. A landing squashes it.

The arms, hands, legs and whatever the hands hold stay on the real bones, so weapons, IK, hit timing and hit volumes never move
with it. Only the live arena pilot's body jiggles. Corpses, portraits,
the draft and first person keep the twins at rest.

## Hats

Owner 2026-11-11: "Hats don't fit on hotdog model correctly." Every hat is made for the pilot's skull (`NativeModelHatMeshes`:
its band 0.118 m over the head bone, 0.121 to each side and 0.127 ahead and behind a point 0.025 ahead; a closed hat's crown at
0.215). Measured from the bank in the head bone's frame, the sausage's crown is that skull's size at the band but sits 2.2 cm
further ahead. Its dome rises to 0.24, 2.5 cm over a closed hat's crown, and its three sprigs stand 3 to 5 cm over that.
Before the fix, a hat rode an anchor on the head's twin 3.5 cm up and 5 cm ahead. That had three faults:

- the sausage's crown follows the head bone only about 30% of the way (the rest is the neck and upper spine, so its top doesn't
  fold), so a hat tilted and slid off whenever the head turned or pitched;
- 5 cm ahead put hats over the face;
- the sprigs poked through the closed hats.

Now (`Runtime/NativeHotDogPresentation.cs`, `HotDogHatOn` and `HotDogUnderHat`):

- **The frame:** each time a hat is drawn, its anchor is placed in the crown's own skinning frame: the blend of the bones and
  weights of a vertex over the head, read once from the bank. The hat stays on the crown however the head turns and as the
  sausage jiggles, on live bodies, portraits and corpses alike.
- **The fit:** `NativeHotDog.HatFit` moves each hat as `NativeRobotHelm.HatFit` does for the helmet. It scales the hat about the
  band's middle (`HatPivot`), lifts it and moves it ahead by that hat's own measure, so its band sits round the sausage and its
  crown clears the dome. A hat that isn't listed takes the cap's fit.
- **The sprigs:** under a closed hat (`HidesSprigs`: the cap, beanie, top hat, wizard, cowboy, cone and the Sheldon Head) the body
  draws a copy of the mesh without them. They are the small parts wholly within 8 cm of its top, found by welding the bank's
  seams at load. The crown, antenna and halos leave them standing.

## The bend kept even

The owner, through the Katana session (2026-11-11): "their hot dog skin deforms during the Katana's counter". The counter curls
the spine about 50 degrees past the stance at one or two joints, and the sausage, weighted along the spine's bones, folded into a
spike at that height; its jiggle was idle. Now each frame (`HotDogJiggle`, before the jiggle) each sausage twin's turn, its
bone's against its rest with the body's own turn taken off, is eased toward its neighbours' (`NativeHotDog.SpinePasses` passes, a
quarter each side; the pelvis's and the head's kept as posed) and the twins take the eased turn (`SpineEven`, all of it). The
bend is the same but spread down the sausage as an arc. Hats and the animated face ride the same twins, so they follow it.

## Wounds and cuts

The Hot Dog has no human skeleton inside, alive or dead (the body's and the corpse's `VisualSkeletonMake` are skipped for it).
Its wounds still open holes with their pits of flesh, and its cuts show the cut cap. In the first live capture a falling brick
wounded it, and the pilot's skeleton showed as ribs inside the sausage, its leg bones out past the noodle legs.

## Open items

- **Rights:** this assumes a Tripo Pro export (the owner said on 2026-11-05 they would buy Pro). The free plan gives no rights to
  its outputs. It stays DEV only and out of git.
- **The account Worker** (`src/relay/accounts.ts`, Astra's part) does not list `hot-dog` in `LOOK_SLOTS.upper` or `DEV_LOOK`, so
  it drops the pick from a saved account look. `NativeLook.KeepSkin` puts it back from this PC's prefs on load until the Worker
  lists it as a DEV look.
- **The export's texture**, once the owner sends it: rebuild with `--texture` and repack the Drive zip.
