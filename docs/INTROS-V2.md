# Intros v2: the contract

Owner, 2026-11-20: "I need to redo essentially all of them (except top dog and Bombardier ones, these are good quality.)
The old ones however, I think id like to essentially "archive" them, so theyre no longer visible in the customization
screen. I want to move the intros to a much higher quality standard, great animations and effects, non-block based
background and designs, etc. With great camera work as well!"

This file is the contract between the framework (I0, branch `claude/intro-framework`) and the intro authors. Branch
from the newest framework commit and merge it again as it moves. Presentation only: no wire, save or rule change. New
intro ids are new strings; old ids keep working.

Ownership (one writer per file):

| Owner | Files |
| --- | --- |
| I0 (framework) | `Core/NativeIntroRules.cs`, `Core/NativeIntroCatalog.cs` (except your one registry line), `Core/NativeIntroV2.cs`, `Core/NativeIntroCamera.cs`, `Core/NativeIntroVoid.cs`, `Core/NativeIntroShowcase.cs`, `Runtime/NativeIntros.cs`, `Runtime/NativeIntroV2Presentation.cs`, `Runtime/NativeIntroCardScreen.cs`, `Runtime/NativeIntroAudio.cs`, the Locker's intro list, `Tests/IntroV2Checks.cs`, this file |
| Each author | one Core file per intro or family (`Core/NativeIntro<Name>.cs`), its set script (`tools/intros/<name>_set.py`), its sound rows (`docs/audio/intros/<author>.json`) and their contract lines on its slot in `Core/NativeIntroSoundKeys.cs`, its own extra checks (`Tests/Intro<Name>Checks.cs`), and ONE line per intro in the registry |

## 1. Registering an intro

`Core/NativeIntroCatalog.cs` holds the registry, `All`. Replace the comment on your slot with one line. Leave the blank
comment lines between slots, so that two authors' lines never touch and merges stay clean.

```csharp
NativeIntroEntry.Weapon("sword","Edge of Dawn",8.2f,6.1f,()=>new NativeIntroSwordDawn(),"I3"),
NativeIntroEntry.Generic("eclipse","Eclipse",7.6f,5.8f,()=>new NativeIntroEclipse(),"I2"),
NativeIntroEntry.Default("Arrival",7.2f,5.4f,()=>new NativeIntroArrival(),"I2"),
NativeIntroEntry.Dev("architect","DEV · Architect of Reality",9.4f,7.2f,()=>new NativeIntroArchitect(),"I1"),
```

| Factory | Id | Who sees it | When it plays without a pick |
| --- | --- | --- | --- |
| `Weapon(spec, …)` | `intro2-weapon-<spec>` | everyone (Locker card) | for every pilot of that weapon. Specs: `sword daggers shield greatsword pistol rifle machinegun bow fire water lightning darkness katana glaive fangs` (`NativeIntroCatalog.WeaponSpecs`). A DEV animation class (DEV Greatsword, DEV Katana) takes its base weapon's intro. |
| `Generic(slug, …)` | `intro2-<slug>` | everyone | never (a pick) |
| `Default(name, …)` | `intro2-default` | everyone | for a pilot whose weapon has no intro, and for the archived DEV classes (Emperor, Ansatsuken, L33T, Cyber Lord) |
| `Dev(slug, …)` | `dev-<slug>` | only live DEV access (the owner): listed, picked and staged only for them | never |

The arguments are: the display name (the Locker card; the name card never shows it), `Duration` and `NameAt` in
seconds (6–9 s; the dev intro up to 16 s, the coordinator's call for the owner's song), the maker, the author tag, and an
optional one-line Locker blurb. The registry's `Duration` and `NameAt`
are what the match reads (`NativeIntroRules.Duration` / `NameAt`), so they must equal your script's own length and
name beat. The checks enforce this.

- **Until a weapon's intro is registered**, that weapon's old signature still plays, so nothing breaks mid-night.
- **Timing tests:** `Tests/ParityFollowupChecks.cs` ("Siren follows human intro") ticks a match with a human **pistol** seat and
  expects the Siren's intro next, so the pistol's intro must end within 8.0 s. Run the full suite after registering: it
  stops at the first failing check.
- **The Siren** keeps its boss entrance. **The Top Dog** and **the Bombadier** keep theirs.
- **A script must always be `Ready`.** If it uses a pack clip that may be missing, give it a procedural fallback pose.
  The runtime throws on a script that isn't Ready.

## 2. The per-frame interface

An intro is a `NativeEntranceScript`, like every directed entrance, that also implements `INativeIntroV2`
(`Core/NativeIntroV2.cs`). Usually you derive from `NativeIntroV2Script`, which is a `NativeProceduralEntrance`. A script
that must derive from another base (for example `NativeBladeIntro`, for pack-clip blades) implements `INativeIntroV2`
itself.

Every call is a pure function of the presentation clock `t` (`Clock(age)`). Never keep history; a late join or a
scrub must draw the same frame. Never allocate per frame.

| Member | What it does |
| --- | --- |
| `Context` (set by the runtime) | `Reduced` (Reduced effects), `Low` (Low graphics), `Quality` 0..1, `Name` (the live display name) |
| `Pose(t, pose, out root, out rotation)` | The keyed rig pose (`Keys`/`Steps`, `NativeProceduralEntrance`): the model's pose (bound joint for joint, section 2b), the camera's anchors, and the Classic body's pose. |
| `Anchor(which, t)` | Where a point of the body is: **the drawn model's** while the model is drawn (section 2a), else the keyed rig's. Use it for every effect and light on the body or the weapon. |
| `Clip(t, out clip, out time)` / `Fade(t)` (`INativeIntroBody`; virtuals on `NativeIntroV2Script`) | **Retired by the owner (2026-11-20: "we dont need the matches own annimation system ... they have unique animations"):** don't use it; key every beat. It still works (the model plays the clip through the match's motion), for old code only. |
| `Hands(t, right, left)` (`INativeIntroHands`, optional) | Superseded by the anchors (`LeftWrist`, `RightGrip`, `RightMuzzle` …). Still called before `Lights`, `Props`, `Set` and `Effects` with each wrist as drawn (source frame): the model's wrist joint frame when the model is drawn (`Model` true), else the keyed hand anchor. For a gun Weapon intro the right hand also carries `HasMuzzle`, `Muzzle` (the `RightMuzzle` anchor: the drawn skin's crown) and `Bore`. |
| `Head(t, head)` (`INativeIntroHead`, optional) | Superseded by the anchors (`Head`, `Eyes`, `Visor`). Still handed in with the hands: the head's centre as drawn (the `Head` anchor) and the head's turn when the model is drawn, else the keyed head anchor. (Before 2026-11-20 it was the keyed joint's frame carried by the model's head with the offset of the body's first frame; see section 2a's migration list.) |
| `Bow(t, out view)` (`INativeIntroBow`, optional, Bow intros) | The bow drawn as the match draws it (on the model's left hand, its string and its nocked arrow) from a `NativeBow.View` you give. Without it, the bow is carried with one arrow. |
| `Camera(age, out view)` | `NativeIntroV2Script` evaluates `Rig` (section 4). Override it only if you must. |
| `Lights(t, lights)` | The void rig (`NativeIntroVoid.Light`), then add your own: `l.Add(at, colour, distance)`, at most 8 points. |
| `Props(t, props, quality)` | Primitive set pieces through the source models. The base draws no pylon arena. |
| `Set(t, set)` | Your modelled set: override `Scene(t, set)` (section 3). |
| `Effects(t, fx, quality)` | Effects through `INativeEntranceFx`, which reaches `NativeVfxDraw`: `Glow`, `Streak`, `Smoke`, `Fire`, plus the helpers in `NativeEntranceFx` (Sparks, GroundDust, Embers, Shaft, Ribbon). |
| `Ghosts` / `Holo` / `Solid` / `Blasts` | As for every directed entrance (see `NativeEntranceDirector.cs`). A ghost on the model (an afterimage, the hologram, a coat like Whiteout's frost, a light draft) is the whole dressed pilot in the ghost's look: its outfit, the robot armour's plates, its helmet or mask, its hat, the chest core, the back emblem and the wrist device, its hair hidden under the helmet as the live body's is. Set `NativeEntranceGhost.Bare` (the hologram: override `HoloBare`) where you draw your own gear over a ghost. |
| `Card(t, ref card)` | The name card state (section 5). |
| `Lens(t, out lens)` | Letterbox, fade to black, focus. It comes from `Rig` by default. |
| `Music` | A bed played from the start (any `Resources/Audio/<id>` clip: `"audio/intros/<sting>"` or `"music/<track>"`) on the music channel, or null for cues only. While the bed plays, the intro is drawn on the bed's playback time, as the Siren's song locks its cell, so beats land on the picture on a guest and after a loading delay. Muted, it runs on the age. |
| `Cues` | The sound cue timeline (section 6). Build it once, as a `static readonly` array. |

Rules (the checks enforce them):
- **No ring outlines.**
- **In Reduced effects:** no shake, and nothing flashing faster than 3 Hz. Use `Flash(t, at, attack, decay)`, which
  widens and dims a pulse when Reduced.
- **A streak longer than about 1 m** must lie on its head's camera plane (`NativeCacheOpenFx.OntoHeadPlane`).
  Otherwise its ends slide off their line.
- **Frames:** source frame, +x the rig's left, y up. The pilot stands at the origin and ends facing
  `NativeProceduralEntrance.Facing` (-0.22 rad). The floor top is `NativeEntranceStage.FloorY`.

## 2a. Anchors: effects on the body as drawn (I0c, 2026-11-20)

Owner: "The effects dont line up with either the weapon, or body. ... Like slipstream, the slide effect is away from the foot."
The keyed rig has the voxel pilot's proportions (a long torso, short legs, a big head); the model is taller with a shorter
torso and longer legs. Standing, the keyed head anchor sits about 0.14 m under the model's eyes; in a deep bow (Whiteout's kneel)
about 0.3 m from the drawn head. Feet, knees, hands and the weapon differ likewise. So **`Anchor(which, t)` is now model-aware:**

- **Each frame** the model is drawn, the runtime hands the script the body as drawn (`NativeEntranceScript.SetDrawn`), after the
  model and its weapon are posed and before `Lights`, `Props`, `Set` and `Effects`. From then until the frame ends, `Anchor`
  returns the drawn point at the frame's own `t`.
- **At any other time** (a trail's past samples, `Velocity`, a spark born earlier) it returns the keyed anchor carried by its
  joint's keyed turn with the same offset, so a trail keeps its shape on the drawn body.
- **Without the model** (the Classic body, a hidden body, the managed checks) `Anchor` is the keyed rig's, as before.
  `KeyedAnchor(which, t)` always reads the keyed rig.
- **The camera stays keyed:** the rig's `Follow`, `Lead` and blends read the keyed anchors, so framing never depends on the
  drawn body and stays deterministic.

| Anchor | Drawn (the model) | Keyed fallback |
| --- | --- | --- |
| `Pelvis` | the hips bone | the pelvis joint |
| `Chest` | the torso's line half way up the chest (between `spine_02` and `neck_02`) | the chest joint + 0.1 m |
| `Head` | the head's centre (the helmet shell's) | the voxel head's centre |
| `Eyes` (new) | between the eyes | on the voxel head |
| `Visor` (new) | the visor glass's front (0.165 m ahead of the head bone; 0.125 m without the helmet) | the voxel head's face |
| `LeftShoulder`, `LeftElbow`, `LeftKnee`, `LeftFoot` (and right) | the model's joints (`LeftFoot` the ankle) | the keyed joints |
| `LeftToe` (new, and right) | the ball of the foot | the keyed toe joint |
| `LeftWrist` (new, and right) | the wrist | the keyed wrist |
| `LeftHand` (and right) | the palm (wrist to the middle finger's knuckle) | the voxel fist |
| `LeftGrip` (new, and right) | where the hand holds the weapon (`NativeIntroWeapon.Grip`): the weapon frame's origin, as the match's sweeps read it; the shield's grip bar; the bow's riser on the arrow line | the keyed wrist + that socket |
| `LeftTip` (and right) | the weapon's far end on the drawn weapon frame: a blade's tip socket (`NativeWeaponDressing.BladeTip`, the blade classes' `NativeBladeView.Line`), a gun's or staff's muzzle, the Glaive's leading fang, the bow's upper limb tip | the keyed hand + `TipLength` (0.15 m) |
| `LeftMuzzle` (new, and right) | where a shot or a cast leaves it: a gun's crown (`NativeMuzzleFx.Crown`, where the match's flash leaves the drawn skin), a staff's head, the bow's arrow rest (`NativeBowModels.Rest`; `NativeIntroWeapon.Muzzle`) | the keyed wrist + that socket |

- Weapon anchors are drawn only for a Weapon intro while its weapon is drawn that frame, on the hand(s) that hold it
  (`NativeIntroWeapon.Holds`: a staff and the bow the left, the daggers and the Fangs both, the rest the right).
- `WeaponBore(left, t)` gives the grip-to-muzzle direction (a gun's bore, the bow's arrow line). `Aim(left, t)` (hand to tip) follows the drawn tip too.
- `Tests/IntroAnchorChecks` holds every grip, tip and muzzle anchor of the fifteen weapons to the drawn weapon (both skins, each hand
  that holds it, no clip playing): within 4.5 cm of its surface, the tip at its far end. Today's worst is 3.9 cm (the darkness staff's
  crown). **The clip-free Katana lies along its weapon frame's +z**, as every weapon does (its tip socket sits on its blade's point);
  a blade line or `RightTip` on +z is right for it.
- `Root`, `Forward` and `Focus` are never drawn.

**Remove your hand-tuned head and weapon offsets once you merge this** (they were measured against the keyed rig or the old
`Head()` point, and now double up):

| Author | Where | Replace with |
| --- | --- | --- |
| I1 | `NativeIntroArchitect.VisorAt` (`EyeUp`, `VisorFront` from the head anchor) | `Anchor(Visor, t)` (keep `EyeUp`/`VisorFront` only in the keyed camera's framing) |
| I2 | `NativeIntroGeneric.HeadLift`; `NativeIntroWhiteout.Eyes()` (the drawn head + `HeadLift` along the neck) | `Anchor(Visor, t)` / `Anchor(Eyes, t)` |
| I3 | `NativeIntroMelee.ModelHeadLift` in `Head()` (the eyes); `Record()`'s tip from `TipLength` along the wrist frame | `Anchor(Eyes, t)`; `Anchor(RightTip/LeftTip, t)` and `RightGrip`/`LeftGrip` |
| I4 | `NativeIntroRanged.ModelHeadLift`; `NativeIntroSundown` (the blow at the mouth: the head + `ModelHeadLift` - 0.1) | `Anchor(Eyes, t)` lowered to the mouth; the gun's `Anchor(RightMuzzle, t)` and `WeaponBore(false, t)` |
| I5 | `NativeIntroElemental.ModelHeadLift` and `HeadShift()` (the crown light, the crown, the eye) | `Anchor(Head/Eyes/Visor, t)`; the staff's `Anchor(LeftMuzzle, t)` |

## 2b. The keyed pose on the model

The model is posed from the keyed pose bone for bone (Core `NativeVisualRig`): each bone takes its game joint's turn since rest,
so the drawn head, chest and pelvis face exactly as the keyed ones in every pose, however deep. `IntroV2Checks` holds every
registered intro to it every 1/20 s on two skeletons (within 3°; today the worst is 0.03°). Whiteout's kneel bows the head 56°
below the horizon: from a camera above the pilot's left shoulder its visor faces the snow and the band of glass reads as turned
aside. What differs between the rig and the model is where things are, which the anchors (2a) carry.

The legs are where it matters most: **the drawn legs are 1.7 times the keyed ones on the shared root**, and the model's feet are not
planted (the legs copy the keyed turns). A keyed foot held still while the root moves slides the drawn foot back at about 1 m/s in
a walk, and a keyed drop sinks the drawn hips 1.7 times as deep (a kneel's knee 30 cm through the floor). Key the legs on the model
(section 9c, `ModelFeet`). Put floor effects at the anchor's `x`, `z` on the floor (`NativeEntranceStage.FloorY`), as Slipstream
does.

## 3. Sets: the bank and its fallback

- **What a bank is:** one local bank per author or family, `Resources/ChipArenaIntro<Name>.bytes`, in Core
  `NativePackBank` format. Write it with your own scripted Blender (5.2.3, headless) in the style of `tools/stages/*.py`.
- **Never commit it.** Put the built bank in `Assets/LocalAssets/ChipArenaLocal/Resources/` in the Editor checkout only
  under the lock, keep the master in `F:/Chip Arena Development/scratch/<task>/`, and name its path and size in your
  report.
- **Size limits:** all intro banks together at most 40 MB, and no file over 24 MiB.
- **The frame:** bank meshes are in Unity space, as `tools/stages/stagekit.py` writes them, and are drawn as authored.
  `Piece(prop, at, rotation, …)` takes `at` and `rotation` in the source frame, and the runtime mirrors them
  (x → −x). So a set modelled whole in stagekit's Unity space is placed with `Piece(prop, Vector3.Zero,
  Quaternion.Identity, 1, …)`. In Unity space the pilot stands at the origin and ends facing (sin 0.22, 0, cos 0.22).
- **Distance:** the camera's far plane stays the game's 200 m. Build sky domes, far peaks and backdrops inside about
  190 m, scaled down: from a camera that moves a few metres they read the same as at 400 m. There is no `Far` override,
  because raising it costs depth precision in the match after the intro.
- **Fog:** the glow submesh takes the fog and the stage haze too (`StageHaze`, then `UNITY_APPLY_FOG`), so a far glow
  fades with your `FogNear`/`FogFar`.
- **Many small pieces:** `set.Batch(prop, positions, turns, scale, tint)` draws instanced copies with no shadows, up to
  400 a frame in all. Use it for casings, debris and shards. `Piece` is for the set's real parts (at most 80 a frame;
  the dev intro 180 on High, 90 on Low).
- **Original work only.**
- **Prop names:** `<prop>` is the Fine tier, and `<prop>/low` is the optional Low tier. Each prop's first part is
  drawn: submesh 0 solid, submesh 1 glow. Its materials are the stage art's (`ChipArena/StageLit`: vertex colours and
  the glow), lit by the intro's lights.
- **The calls:**
  - `SetBank` names the bank (`"ChipArenaIntroEclipse"`).
  - `Scene(t, set)` draws it with `set.Piece(prop, at, rotation, scale, tint, glow)`. `set.Has(prop)` says whether the
    bank has that prop. Return false when a piece you need is missing.
- **The fallback:**
  - Without the bank (CI, a build without the zip, `--ca-no-pack-banks`), or when `Scene` returns false, the base draws
    `Fallback(t, set)`. This is the dark lit void, `NativeIntroVoid`: a vast floor lost in the fog, a soft pool under
    the pilot, drifting motes (`NativeIntroVoid.Motes`), and a key, a rim and a cool fill.
  - Tint it to your intro with `VoidLook(t)`, or override `Fallback` with your own primitives.
  - Never draw the old block arena (`NativeEntranceStage.Arena`).

## 3a. Lighting: the intro owns its light (I0c, 2026-11-20)

The same intro looked different by map (Still Tide's ice magenta in Cyber City, grey in Brickfall, teal in Polar): the arena's
light reached the set and the pilot. Now, while a v2 intro draws, it owns its light (`Runtime NativeLighting.IntroOwn`, each
frame after your `Lights`):

- **Dark:** the arena's eight practical lights, its rim, its haze, its blast flashes and its light volume and scattering.
- **Yours:** the ambient (your `Sky` and `Ground`, written to the ambient probe every frame, as `DriveProbe` did) and the fog
  (`Fog`, `FogNear`, `FogFar`), as before.
- **Your reflection** (new, `NativeEntranceLights`): what glossy set finishes and the pilot mirror, a small cubemap made from three
  linear colours, `ReflectSky` above, `ReflectHorizon` level and `ReflectGround` below, at `ReflectIntensity` (default 1). A colour
  you leave at zero takes your own: `Sky` above, `Fog` at the horizon, `Ground` below. Set them for a sky the ambient doesn't
  carry (a sunset's warm horizon on wet stone, a white sky on ice).
- **Given back exactly** when an older entrance draws next or the intro ends (`EndIntro`). The match never sees any of it: the
  arena isn't drawn while an intro is.

**Re-tune once:** every capture so far was in Cyber City, under its practical lights, haze and neon reflection. Expect your set
a little darker and its gloss to mirror your own sky; check one capture on any map and adjust `Sky`/`Ground`, your lights or
the `Reflect*` colours. Your own point lights (`l.Add`) still light the pilot and the primitives, not the bank's set pieces
(`ChipArena/StageLit` reads the arena's practical slots, which are dark now): light the set with your directionals and ambient.

## 4. The camera rig (`Core/NativeIntroCamera.cs`)

Build the shots once, in the constructor, through `Rig`. Shots must be added in time order. Each shot lasts until the
next one starts; the last runs to `SettleAt` (Total − 0.25 s) and then holds still.

```csharp
Rig.Crane(0, eyeFrom, eyeTo, rise:-.6f, look0, look1, 44, 38).Ease(NativeIntroEase.InOut).Focus(0,0,.35f)
   .Orbit(2.15f, pivot, deg0:25, deg1:-60, r0:3, r1:2.6f, h0:1.25f, h1:1.05f, look0, look1, 40, 36)
       .Follow(NativeEntranceAnchor.Chest, lead:.08f).In(NativeIntroCut.Blend,.45f).Handheld(.6f)
   .Spline(3.4f, new[]{p0,p1,p2,p3}, look0, look1, 38, 34).In(NativeIntroCut.Whip,.12f)
   .Dolly(4.2f, from, to, look0, look1, 36, 33)
   .Hero(5.2f, side:1, height:1.15f, distance:3.0f, fov:31).In(NativeIntroCut.Blend,.5f)
   .Bars(0,1).Bars(5.5f,.55f).Black(0,1).Black(.05f,0);
Ramps.Ramp(at:2.2f, rate:.35f, hold:.32f, repay:.9f);   // a speed ramp; the clock catches up after it
```

| Call | Shot |
| --- | --- |
| `Hold` | locked off |
| `Dolly` | a straight eye move |
| `Crane` | an arced rise or fall |
| `Spline` | Catmull-Rom through three or more points |
| `Orbit` | about a pivot, or about a follow anchor's ground point; degrees from the pilot's front, + toward its left |
| `Hero` | the final hero framing: a slow push in from a low three-quarter front, easing out to stillness |

Modifiers apply to the last shot:
- `Follow(anchor, lead, track)`: the look becomes an offset from the smoothed anchor, led along its motion; `track`
  carries the eye with it.
- `Fov`, `Roll` (degrees), `Ease`.
- `In(Cut | Blend | Whip, time)`.
- `Focus(from, to, aperture)`.
- `Handheld(cm)`: dropped in Reduced effects.
- `Bars` / `Black`: lens keys over the whole intro.

**Speed ramps** (`Ramps.Ramp`) slow the whole presentation clock (pose, camera, effects and cues together) and repay
the time afterwards. Every beat after the repayment, the name beat included, stays where you put it in age. Put ramps
before `NameAt`.

**Focus** is data (`NativeIntroLens.Focus` / `Aperture`). The runtime gives it a vignette that tightens with the
aperture; a real depth-of-field pass can read the same values later.

## 5. The name card (`NativeIntroCardState`, drawn by `Runtime/NativeIntroCardScreen.cs`)

`Card(t, ref card)` fills:

| Field | Meaning |
| --- | --- |
| `Shown`, `Age` | seconds since the card began |
| `Style` | `Rise`, `Slam`, `Type`, `Assemble` or `Wipe` |
| `Ink`, `Glow` | linear colours |
| `Kicker` | the line above; null means the weapon name |
| `Line` | the line below; null means the pilot's intro text |
| `Size`, `X`, `Y` | screen shares; 0 means the defaults |
| `Exit` | 0..1 |
| `Effect`, `EffectA`, `EffectB` | a bespoke effect id and its parameters |

- **The default** (`NativeIntroV2Script.Card`): Rise at `NameAt`, white ink, the accent as the glow, leaving over the
  last 0.35 s.
- **The name** is always the live display name, the seat's `name`: the account profile's name when signed in, cleaned
  to 20 characters. The card never shows the intro's own name.
- **Letterbox and black** come from `Lens` and are drawn under the card.
- **Bespoke effects** (the dev intro): set `card.Effect = "architect"`, and register one drawer in
  `Runtime/NativeIntroCardScreen.cs` `NativeIntroCardEffects` (one line, on its slot). A drawer gets a
  `NativeIntroCardFrame`: the name, its font size, its rect, each glyph's x and width, the card state and the screen
  size. It draws with `NativeUi` (Text, Fill, glow, blur, Push/Pop for transforms) before or after the plain name, as
  its `Draw(frame, before)` says.

## 6. Sound: the cue timeline

`Cues` is a static array of `NativeIntroCue(at, key, alt, clip, gain, rate)`, sorted by `at` and on the presentation
clock (cues follow speed ramps). For each cue, the runtime (`Runtime/NativeIntroAudio.cs`) plays the first of these
that exists:

1. **`key`:** the generated sound requested for this cue: `intro2.<intro>.<cue>`, in the LocalAssets effects bank once
   generated and imported.
2. **`alt`:** an existing effects-bank key that fits (for example `melee.sword.swing`, `impact.energy`,
   `blast.quake`). Search `docs/audio/` and `Core/NativeSoundKeys.cs`.
3. **`clip`:** a built-in clip that every build has (`Resources/Audio/audio/<clip>`, for example `sword`, `metal`,
   `lightning`, `stone`, `dash`, `explosion`).

So an intro sounds right before any generation: write `alt` and `clip` to fit, and check the sync in your capture.

**Requesting generated sounds:** follow `docs/audio/intros/README.md`.
- Put your rows in your own `docs/audio/intros-b2/<author>.json` (batch 2), in the manifest's sound schema. Batch 1's folder
  `docs/audio/intros/` and its manifest are sealed by their approved jobs: never add rows there.
- Add one contract line per key to `Core/NativeIntroSoundKeys.cs`, on your slot.
- `tools/intros/compile_sound_requests.py --batch b2` merges the batch's rows into `docs/audio/sound-requests-intros-b2.json` (and its
  bundle copy) and prints the approved builder job (README section 2a).
- Never put a key or token anywhere in the repo or in logs.

### 6a. Generation status

The builder queue is live on this PC (its DPAPI setup and receipts are there). It generates only inside a builder run,
which the integrator pushes. So the intros' sounds are generated unattended tonight only if the compiled manifest, its
contract keys and its queue job are merged before a builder push. Until then every cue plays its `alt` or `clip`. The
details are in `docs/audio/intros/README.md`.

## 7. The managed checks (`Tests/IntroV2Checks.cs`)

Every registered intro runs through the same checks automatically. You don't need to add anything to `Program.cs`.

| Check | What it requires |
| --- | --- |
| registry | `Duration` and `NameAt` equal the script's `Length` and `IntroNameAt`; 6 ≤ Duration ≤ 9 (dev 16); 0 < NameAt < Duration − 0.8; the kind's id prefix; Ready |
| determinism | two fresh scripts give the same pose, camera, lights, props, set calls, effects and card at sampled times, in any order (a scrub) |
| allocation | no managed allocation per frame after warm-up (pose, camera, lights, props, set, effects, card, lens) |
| budgets at peak | at most 600 FX calls a frame (the dev intro: 1000 High, 450 Low), 8 point lights, 120 primitives, 80 set pieces (the dev intro: 180 High, 90 Low), 400 batched copies (`NativeIntroSetLimits`) |
| Reduced effects | the camera never wobbles: outside cuts, the eye stays within 6 mm of a quadratic fit over ±4 frames, so no shake survives; and the summed light and glow never flash more than 3 times in any second |
| streaks | every streak longer than 1 m lies on its head's camera plane (within 2°) |
| cues | sorted, inside [0, Duration], each with a key, alt or clip, and each heard exactly once through `NativeEntranceCueClock` at 60 Hz and at a guest's 20 Hz steps |
| sets | without a bank something is still drawn (the void or your primitives); with a fake bank that has every prop, your `Scene` stays inside the piece and batch budgets and allocates nothing |
| the pose on the model | your keyed pose, bound onto the model's skeleton (the measured and the synthetic one) every 1/20 s, keeps the drawn head, chest and pelvis facing within 3° of the keyed ones (section 2b) |
| anchors (framework) | keyed until a body is handed in; then each handed anchor exactly where it was drawn at that frame's time and carried rigidly by its keyed joint at other times; `ClearDrawn` gives the keyed ones back; no allocation; the weapon sockets for all fifteen weapons; the reflection's defaults; the runtime's order (camera keyed, the drawn body before the lights, set and effects) |
| weapon anchors (`Tests/IntroAnchorChecks`) | every grip, tip and muzzle anchor of the fifteen weapons on the drawn weapon, in both skins (section 2a) |
| poses (`Tests/IntroPoseChecks`) | your keyed pose bound bone for bone on the model; the anatomical limits; the drawn feet (no slide on the floor, nothing through it); the drawn grip (the off fist on the drawn weapon); the guide's canonical poses always clean (section 9b). Your intro's findings are listed by `--intro-limits` (section 9b) and fail only there |

Put your own extra checks in your own `Tests/Intro<Name>Checks.cs`, and add one call in `Program.cs` on its own line.

## 8. Capturing (BoZo, the local Editor tool; never committed)

These play options already work: `intros=1 intro=<id> devaccess=1 introsteady=1 capdt=0,0.0333333 gat=<times>`. I0
adds the shorthand:

- **`introcap=<id>`:** plays intro `<id>` for the local pilot at the next match start. It sets `intros=1`, the pick and
  DEV access, and implies `introsteady=1`.
- **`introq=high|low`:** the graphics tier for the capture (Low is `pref=graphics:performance`).

Example, with the head-gib helpers (`take`, `on <sha>`, `cap`, `back`, `give`):

```bash
cap sword-high "$(dense 0 8.6)" 300 map=cybercity weapon=sword bots=0 idlebots=0 orbit=0 introcap=intro2-weapon-sword introq=high capdt=0,0.0333333
cap sword-low  "$(dense 0 8.5 0.5)" 120 map=cybercity weapon=sword bots=0 idlebots=0 orbit=0 introcap=intro2-weapon-sword introq=low capdt=0,0.0333333
```

The third argument is the run's limit in seconds of play: about 0.6 s of real time per captured frame, plus start-up.
The frames are `t<N>_screen.png`, so a clip is
`python -I <int>/clip.py OUT.mp4 --fps 30 --to <last frame> --pane "LABEL=DIR:_screen.png"`.

- **Timing:** the intro starts at arena time 0, and at its `Duration` the next pilot's intro follows (even with
  `bots=0` the match has a second seat).
- **What it took:** the framework's own hold filmed the showcase in about 6 minutes at High (1/30 s) and 2 minutes at Low.
- **The Locker:** add `dmhold=14` and `"locker=3:2;7:2"` (tab 2 is the Entrance tab). Without `dmhold` the panel can
  miss the shot.

**Editor relaunch** (owner: "Yes, relaunch it", for overnight only):
- `F:/Chip Arena Development/scratch/intro-framework/tools/editor_relaunch.sh` starts the owner's Editor on its own
  checkout when no Editor process is up, then waits until it is up and has compiled.
- It never kills anything. Call it before `take` when `editor_up` says down.

## 9. Motion and hands

- **Bespoke keyed animation only** (owner, 2026-11-20: "we dont need the matches own annimation system. Think like the top dog
  and Bombardier ones, they have unique animations"): key every beat with `NativeEntranceKeys` (`Key`, `Root`, `J`, `Planted`,
  `Drop`, `Spin`, `ImpactKey`, `PassKey`, `Steps`), as `Core/NativeTopDogIntro.cs` and `Core/NativeBombadierIntro.cs` do. Don't use
  `Clip()` or motion-bank clips: they ran through the match's motion layers, which walked Flashbulb's legs sideways.
- **The model follows your keys joint for joint** (section 2b); the effects follow the model (section 2a).
- **The hands, the head and the weapon:** use the anchors (`LeftWrist`, `LeftHand`, `RightGrip`, `RightTip`, `RightMuzzle`,
  `Head`, `Eyes`, `Visor` …). `Hands()` and `Head()` still work and hand in the same points.
- **Weapons in two hands:** the grip IK (section 9a). **Legs, walks, kneels and landings:** key them on the model (section 9c).
  **How to pose well, and how to read the checks:** the pose-craft guide (section 9b).

## 9a. The weapon grip IK (I0d, 2026-11-20)

Owner, on One Breath: "arms looked really odd". The front hand hung limp under the barrel, the arms tangled on the aim, and at the
end the arms were crossed, holding nothing. Two causes:
- **The keyed pose:** the off hand was put on the keyed rifle by an IK that kept the elbow wherever it was, so it tangled.
- **The drawn model:** it copied the keyed off arm turn for turn. On the model's shorter arms and narrower shoulders (its shoulders
  are 0.13 m from the spine, the rig's 0.35 m), the hand missed the drawn rifle by 10-20 cm. The match's own support grip, which
  would put the fist on the handguard, switches off once the keyed wrists are more than 0.30 m apart.

The grip IK (Core `NativeIntroGrip`) solves the off hand onto a two-handed weapon at both levels:
1. **The keyed pose**, which `--intro-limits` checks. The off arm is solved onto the weapon held in the other hand, in the match's
   own convention for that weapon, the elbow toward a pole, the hand's roll carried by the forearm.
2. **The drawn model.** The off fist closes on the drawn weapon's support point, from the drawn weapon frame, whatever the hands'
   distance. Its elbow goes to the pole's side. A long gun can be shouldered.

**Opt in** by implementing `INativeIntroGrip` on your script:

```csharp
public sealed class NativeIntroOneBreath:NativeRangedIntro,INativeIntroGrip
{
    // The off hand on the handguard throughout; the stock in the shoulder while it aims, off it as the pilot rises to the carry.
    public bool GripAt(float t,out NativeIntroGripAt g)
    {
        g=new NativeIntroGripAt(weight:1,pole:default,along:0,pull:0,shoulder:1-NativeEntranceMath.Ramp(t,RiseFrom,RiseFrom+.55f));
        return true;
    }
    protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)
    {
        // Only the main hand is yours. Leave the off hand to the grip.
        NativeEntrancePose.Reach(pose,root,rotation,false,grip,rifleTurn,1);
        NativeIntroGrip.Forearm(pose,false,NativeIntroGrip.Natural(NativeIntroGrip.Kind.LongGun,false));
    }
}
```

- **When it runs:** `NativeProceduralEntrance.Pose` calls the grip after your `Arms`. If you override `Pose`, call `GripIK(t, pose)`
  after your own `Arms`. The runtime reads the same `GripAt` for the drawn model.
- **The fields of `NativeIntroGripAt`:**

  | Field | What it does |
  | --- | --- |
  | `Weight` | 0..1. Blend it in and out over 0.1-0.2 s, as a key would. |
  | `Pole` | Where the off elbow points, in the chest's frame (+x the pilot's left, +y up, +z ahead). Zero takes the weapon's default: down and out, or for the bow's string elbow, back and high. |
  | `Along` | Slides the hand along the weapon (metres): further up a staff, back on a handguard. |
  | `Pull` | The bow's draw. Give the grip and your `Bow` view the same pull. |
  | `Shoulder` | A long gun's stock in the drawn shoulder pocket while the keyed butt is at the keyed shoulder. Use it for an aim, not for a carry. |
- **The support point per weapon:**

  | Weapon | Main hand | Off hand goes to |
  | --- | --- | --- |
  | rifle, machine gun | right | the handguard or shroud, cupped from below; the fist slides back toward the receiver if a short arm needs it |
  | pistol | right | round the shooting fist |
  | greatsword, Katana | right | the lower grip of the hilt (the Katana's own support socket) |
  | fire, water, lightning, darkness | left | the shaft, 0.40 m up from the main fist |
  | bow | left | the string at the nock (`Pull`) |
  | shield | right | the left rim, upper third, a bracing fist |

  The daggers and the Fangs hold a blade in each hand; the sword is one-handed.
- **The main hand stays yours:** keys, `NativeEntrancePose.Reach`, or `NativeIntroGrip.Hold(pose, left, spec, at, turn, pole)`.
  Hold places the main wrist in the chest's frame, as the match keys its own holds. `NativeIntroGrip.Aim(pose, yaw, pitch)` makes
  the weapon's turn from the pilot's facing, and `ChestPoint` converts a world target. Key positions in the keyed rig's
  proportions (section 9b), never at heights measured on the model.
- **Checked:** `IntroPoseChecks` binds your pose with the grip as the runtime does, and lists a drawn off fist more than 3 cm from
  the drawn support point. Each canonical weapon pose holds within 0.0 cm (the long guns after their slide).

## 9b. Pose craft: how to key a body that looks right on the model (I0d, 2026-11-20)

### How the model draws your keys

- **Joint turns are copied bone for bone** (section 2b). Your keys are joint turns on the keyed rig. Euler XYZ, source frame:
  +x the pilot's left, +y up, +z ahead.

  | Joint | Sign |
  | --- | --- |
  | `LA`, `RA` (arms) | x < 0 raises the arm forward; z > 0 takes the left arm out, z < 0 the right |
  | `LE`, `RE` (elbows) | x < 0 bends |
  | `LL`, `RL` (hips) | x < 0 brings the thigh forward |
  | `LK`, `RK` (knees) | x > 0 bends |
  | `Sp`, `C`, `N`, `Hd` (spine, chest, neck, head) | x > 0 bows forward; y turns toward the pilot's left |
  | `B` (body) | leans the whole body about the root |
- **The proportions are not copied.** The keyed rig is the voxel pilot's: legs 0.5 m long, shoulders 0.35 m from the spine at about
  1.1 m up, head joint 1.36 m, eyes 1.55 m. The model has legs 1.7 times as long, shoulders 0.13 m from the spine at 1.4 m, and eyes
  at about 1.6 m. So:
  - **Place hands and weapons in the chest's frame**, as the match does (`NativeIntroGrip.Hold`, the `NativeIntroPoses` helpers).
    A hand keyed at 1.36 m because that is the model's shoulder height is a raised arm on the keyed rig, and the model copies that.
  - **Key the legs on the model** (section 9c).
  - **Check on the model, not on the rig.** Run `--intro-limits`, and draw the model as a stick figure (I2b's
    `tools/intros/generic_sticks.py`, which reads `INTRO_GENERIC_MODEL`).

### Reading `--intro-limits`

Run `dotnet run --project unity/ChipArenaConversion/Tests/CoreTests.csproj -c Release -- --intro-limits`. It lists every registered
intro, then every canonical pose, then fails if any intro has a finding. The full suite runs the same checks, reports your
findings and fails only on the canonical poses.

```
MAP intro2-weapon-rifle  pelvis 0@0.0 ... Lupperarm 22/t3@7.2 Lforearm 22/t5@5.8 Lhand 85@5.8 ...
LIMITS intro2-weapon-rifle: 6 findings
  intro2-weapon-rifle 0.00-5.63 s L forearm: twisted past about 90° (worst 117° at 3.31 s)
  intro2-weapon-rifle 5.67 s the drawn right foot slides on the floor at 3.01 m/s (keyed for the model: ModelFeet ...)
  intro2-weapon-rifle 3.36 s the drawn calf_r is 13 cm into the floor (drops in the model's metres: ModelFeet)
POSE walk: clean
```

- **A `MAP` line** shows, for each bone, how far the drawn bone turned from your keyed one, worst first, and when: `Lupperarm 22/t3@7.2`
  is the direction 22° off, the twist 3° off, at 7.2 s. Legs, spine, neck and head must be 0. Arms and hands differ by design: the
  model spreads a hanging arm 15°, keeps a hand out of the body, and eases a wrist bent past its own range. A large arm number means
  the model moved your arm. It does that when your hand is inside the body or the head.
- **A `LIMITS` finding** names the intro, the time span, the joint and the fault, with its worst value and when it happened. Fix
  it at the worst time.

  | Finding | What it means | Fix |
  | --- | --- | --- |
  | elbow or knee bent backward, sideways or past its range | a hinge used as a ball joint | bend it about its own x only: elbows x < 0, knees x > 0, at most about 155° |
  | wrist bent past its range (85°) | the hand folded back on the forearm | point the hand along the forearm. A weapon hand is read in its weapon's convention: a gun's, the bow's and the shield's frame lie a quarter turn from the hand, so a level aim is a straight wrist (`NativeIntroGrip.Natural`) |
  | wrist twisted (turn the forearm instead), forearm twisted past about 90° | the palm turned by the wrist, or more than a forearm can roll | carry the roll in the forearm (`NativeIntroGrip.Forearm` moves it to the elbow's own twist); past 95° the hand can't get there, so rotate the arm or choose another hand turn |
  | shoulder: upper arm twisted past its range (110°) | a palm turned by spinning the upper arm | turn the palm with the forearm |
  | shoulder: arm behind or across past its range (70° behind, 55° across) | an arm through the body | turn the chest instead |
  | hip: flexed, behind, out, across or twisted past its range (140° flexed, 40° behind) | a thigh past what a hip does | in a run's flight one thigh goes forward and one back; never both back |
  | spine, chest, neck or head bent or twisted past its range | the body bent through itself | spread a big turn over the spine, the chest, the neck and the head |
  | hand, elbow or knee inside the torso | a limb through the body | move it out: on the model it is pushed out and your pose changes |
  | the drawn foot slides on the floor (more than 0.45 m/s) | a keyed planted foot on a moving root (the model's legs are 1.7 times as long) | `ModelFeet` (section 9c), or `FeetSliding(t)` where the slide is meant (a knee slide, a cut) |
  | the drawn ball, ankle, knee, hip or pelvis into the floor (more than 4 cm) | a keyed drop sinks the drawn hips 1.7 times as deep | `ModelFeet`: drops in the model's metres |
  | the drawn off fist is off the drawn weapon's support point (more than 3 cm) | the grip can't reach | bring the weapon nearer the off shoulder, or slide the hand (`Along`) |

### The canonical poses: code to copy

Each pose is in Core `NativeIntroPoses` and is checked by `Tests/IntroPoseGallery` on the model every build: clean against the
limits, feet still and on the floor, grip on the weapon. Start a key from a basis and tweak it:
`k.Key(t, NativeIntroPoses.Kneel()).Root(x, .88f, z, yaw).J(Hd, .1f)`. The gallery file is the worked example for every pose: its
stance, its `Arms` and its grip.

| Pose | Basis | Stance (`ModelFeet`: the model's ankles) | `Arms` and grip |
| --- | --- | --- | --- |
| walk with counter-swing | `NativeIntroPoses.Walk(k, plan, track, t0, steps)`: keys at each contact (hips low, turned with the leading leg, chest countering, the opposite arm forward with the elbow bent) and each passing | from `NativeIntroFeet.Track` (0.65 m a step every 0.55 s) | none |
| run | `NativeIntroPoses.Run(k, plan, track, t0, strikes)`: strike, toe-off and flight keys, the foot planted through its stance | 0.22 m ahead of the hips | none |
| kneel | `Kneel()` (hips 0.47 m down) | `Stance(.13, .42, .1, -.13, -.1, -.3)` and `ModelKneel(t, false) => 1` | none |
| crouch | `Crouch()` (hips 0.5 m down, chest over the knees) | `Stance(.15, .1, .2, -.15, -.12, -.3)` | none |
| landing | `Landing()` as an `ImpactKey` (hips 0.6 m down, arms out), then a rebound key | `Stance(.2, .05, .25, -.2, -.08, -.35)` | none |
| hero stances | `Hero()`, `HeroWide()` | `Stance(.13, .04, .15, -.13, -.04, -.3)` and `Stance(.24, .02, .3, -.24, -.02, -.3)` | none |
| sword guard | `Guard()` | `Stance(.12, .2, .15, -.15, -.2, -.5)` | `NativeIntroPoses.Guard(pose, "sword")` |
| greatsword or Katana guard | `Guard()` | as above | `Guard(pose, spec)`, grip on |
| rifle or machine-gun aim | `RifleAim()` (the match's bladed torso and clavicles) | `Stance(.14, .18, .3, -.14, -.16, -.6)` | `AimRifle(pose, spec, yaw, pitch)`, grip on with `Shoulder` 1 |
| pistol aim | `PistolAim()` | `Stance(.13, .04, .1, -.13, -.04, -.1)` | `AimPistol(pose, yaw, pitch)`, grip on |
| bow draw | `BowDraw()`, root yawed a quarter turn right of the shot | `Stance(.13, .05, .4, -.13, -.05, 0)` on that yaw | `DrawBow(pose, yaw, pitch, NativeIntroPoses.BowAnchorPull)` (the match's bow pose), grip on with the same `Pull` |
| staff hold | `StaffHold()` | `Stance(.12, .2, .15, -.15, -.2, -.5)` | `HoldStaff(pose, spec)`, grip on |
| shield brace | `ShieldBrace()` | `Stance(.13, .22, .1, -.15, -.22, -.45)` | `BraceShield(pose)`, grip on |

**The bow's draw on the model:** the bow's own full draw (`Pull` 1) puts the nock 0.82 m behind the riser. That is the keyed rig's
draw length, and on the model it lands 30 cm past the head. `BowAnchorPull` (0.55) anchors the string at the drawn jaw.

### Mistakes found so far, and what to do instead

- **Over-long strides.** The keyed legs reach about 0.3 m either side of the hips, so a keyed step of 0.5 m left a planted foot
  hanging short of the floor. On the model the stride also slid (next item). Use `ModelFeet` and write the model's steps:
  0.6-0.75 m every 0.5-0.6 s.
- **Feet planted on a moving root.** Every keyed walk slid the drawn stance foot back at about 1 m/s: 0.29 m in a 0.6 s stance in a
  measured walk. Use `ModelFeet`.
- **Drops in the keyed rig's metres.** A keyed drop sinks the drawn hips 1.7 times as deep: Whiteout's and Cloudbreak's kneels put a
  knee 30 cm through the floor. With `ModelFeet` a `Drop` is the drawn hips' own metres, and `ModelKneel` puts the drawn knee on the
  floor.
- **Both thighs back on one key.** Flashbulb at 0.80 s had both hips 75° behind, so the body falls forward. In a stride one thigh
  is forward and one back; the hip limit is 40° behind.
- **Palm turns carried by the upper arm's twist.** The dev intro's V turned each arm 60° about its line, a 126° shoulder twist. A
  palm turns with the forearm (the elbow's own y): `NativeIntroGrip.Forearm`.
- **Keys that relied on the relax.** Before 5aca7b05 the match's free-arm relax re-solved keyed arms by up to 148°, so loose keys
  looked fine. Arms are now drawn as keyed. Key them where they should be, or opt back in with `ArmsRelax(t)`.
- **Hands keyed at the model's heights.** A hand at 1.36 m is the model's shoulder height but 0.25 m over the keyed rig's shoulder,
  so the keyed arm points up and the model copies it (the rifle's chicken wing). Key hands in the chest's frame.
- **The off hand placed by the keyed rig alone.** It missed the drawn weapon, and the match's support grip was off past 0.30 m. Use
  the grip IK (section 9a).
- **A learnt blade axis.** Still Rain learnt the Katana's axis from the drawn blade in the model hand's frame, which differs from the
  keyed wrist's by the eased wrist, and then aimed the keyed wrist with it, so the cut stood straight up. The clip-free Katana lies
  along its weapon frame's +z, like every weapon (`Tests/IntroAnchorChecks`). Aim +z.

## 9c. Feet on the model: `ModelFeet` (I0d, 2026-11-20; the framework form of I2b's generic base, `claude/intro-generic` a7c8cdec)

The drawn legs are 1.7 times the keyed ones, on hips that move 1.7 times as far from rest, on the shared root, and the drawn feet
are not planted (section 2b). Opt in with `protected override bool ModelFeet=>true;` on a `NativeProceduralEntrance` (every v2
script, the blade intros too). It changes what your keys mean:

- **`Start` and `Steps` are where the drawn model's ankles go**: world x, z on the floor, a step's `H` lift in the model's metres.
  Each frame the keyed foot is put where the drawn one then lands (`NativeIntroFeet.OnModel`), so a planted drawn foot stays put.
- **`Drop` is the drawn hips' drop, in the model's metres.** For example, `Drop(-.5f)` puts the drawn hips 0.5 m lower.
- **A walking step rolls heel to toe.** Before a step of 0.25 m or more with a swing of 0.2 s or more, the heel rises onto the ball,
  and the heel strikes with the toe up.
- **The overrides:**

  | Override | What it does |
  | --- | --- |
  | `ModelKneel(t, left)` | 0..1: that leg's drawn knee down on the floor under the hips, the shin behind, the toes tucked. The leg must be planted. |
  | `ModelGround(t)` | What the feet stand on (a roof, a ledge). |
  | `ModelShift(t, plan)` | Moves the plan (feet riding a platform). |
  | `FeetSliding(t)` | true where a slide is meant (a knee slide, a cut), so the check does not flag it. |
- **The walk helpers:** `NativeIntroFeet.Track` is a hips' line with a start-up, a steady speed and a slow-down; key `RootPath` from
  it. `NativeIntroFeet.Walk(plan, track, ...)` writes the steps, each landing ahead of the hips, and `NativeIntroPoses.Walk` adds
  the body's keys.
- **If you override `Pose`,** call `ModelPose(t, pose, out root, out rotation)` instead of `NativeEntrancePose.Keyed`.
- **The generic intros' base** (`NativeIntroGenericScript`) does this itself and must not set `ModelFeet`. It should map its own
  slides for the check: `public override bool FeetSliding(float t)=>Sliding(t);`.
- **Checked** (`IntroPoseChecks`, every registered intro, on the model every 1/30 s while shown):
  - a foot flat on the floor (its ball within 1.2 cm of it) stays put, its ball under 0.45 m/s, unless `FeetSliding`;
  - no ball, ankle, knee, hip or pelvis goes more than 4 cm into the floor.

  The canonical walk slides at most 0.05 m/s, the run 0.29 m/s at its toe-off, and no pose goes more than 1 cm in.

## 9d. Pack clips as keys: `FromPack` (I0d, 2026-11-20; owner on Slipstream: "The run is very 'cartoon' like, can we make this more realistic? Could use the running pack I have")

An animation pack's humanoid clips can drive an intro's keyed body. There is still no match animation system in an intro: no `Clip()`,
and no aim, turn or foot-lock layer. The clip becomes keyed poses, drawn on the model as any keys are.

- **The bank:** `Resources/ChipArenaIntroMotion.bytes` in the git-ignored local assets (Core `NativeIntroPack.Resource`). It is the
  intros' own bank, never the match's motion bank (`ChipArenaVisualMotion.bytes`, sha 37ec44d2, which holds check). It is baked by the
  local tool's `bozo-intromotion.request` (the scratch patch `intromotion_patch.py`). The bake samples each clip on the baked body the
  way the motion bank does (a manual graph, root motion off, foot IK on), at 60 Hz. It records the bones' rotations rather than
  muscles, and it records the body's own rest skeleton. Never commit the bank or the pack's FBX. The bank ships as a local asset, as
  the motion bank does.
  - The runner pack (BIPER "Runner Action Animation Pack", `CLazyRunnerActionAnimPack`) installs under
    `Assets/LocalAssets/CLazyRunnerActionAnimPack`. Extract only the clips you need, plus `Models/ClazyRunner.FBX`, their avatar source.
    Keep each file's `.meta` so its GUID is kept.
- **One bank per author** (I0e, 2026-11-20): the runtime reads every bank of `NativeIntroPack.Banks`, each optional, and merges them
  (`NativeIntroPack.Merge`, into `NativeIntroPack.Local`), so nobody rebakes another's:

  | Bank (`Resources/<name>.bytes`) | Whose | The checks find it at |
  | --- | --- | --- |
  | `ChipArenaIntroMotion` | Slipstream's (I2b's picks), shipped: never rebake it | `CHIPARENA_INTRO_MOTION` |
  | `ChipArenaIntroMotionMelee` | I3 (melee) | `CHIPARENA_INTRO_MOTION_MELEE` |
  | `ChipArenaIntroMotionRanged` | I4 (ranged) | `CHIPARENA_INTRO_MOTION_RANGED` |
  | `ChipArenaIntroMotionElemental` | I5 (elemental) | `CHIPARENA_INTRO_MOTION_ELEMENTAL` |

  - **Clip names are unique across the banks.** A name a second bank repeats is left out (the first bank's plays, and the Runtime logs
    a warning); `IntroPackChecks` fails on it. Name a ranged or relabelled clip `<take>#<label>` (the bake does).
  - **A missing bank** leaves that bank's segments keyed: its intros play their keys, as without any bank.
  - Each clip keeps its own bank (`clip.Owner`: its skeleton for its frames and its held feet), so banks baked apart still play right.
  - Without its variable, the checks look for each bank in the folder `CHIPARENA_INTRO_MOTION_DIR`, then in the local assets'
    Resources, then in `scratch/intro-framework/` up the tree. Keep your bank in your own scratch folder and point your variable at it.
  - **The match's clips are not the intros'** (owner: the intros' motion is their own). A clip the match's motion bank plays is never
    baked into an intro bank, matched by source clip name: the clip's name (before any `#`), its take and its file's name against the
    match bank's clip names (`~mirror` dropped). The bake refuses one; `IntroPackChecks` fails on one (with the match bank here:
    `CHIPARENA_VISUAL_MOTION`, the local assets' copy, or the Editor checkout's). The notes folder's `intro-pack-clips-index.md` lists
    the owner's packs by weapon and body mechanic, with the clips the match plays marked.
  - The packing (`tools/pack_visual_body.py`) takes every `.bytes` beside the body in the Editor's Resources, so the extra banks go
    into the Drive zip with the integrator's next pack; nothing counts them.
- **Baking your bank** (I0e's form of the local tool's `bozo-intromotion.request`; the scratch patch
  `scratch/intro-framework/i0e/motion/intromotion2_patch.py`, over I0d's and idempotent, never committed). Under the Editor lock
  (`take`/`give` in `integrator-tools/headgib/common.sh`), write `unity/ChipArenaConversion/Temp/bozo-intromotion.request` in the
  Editor checkout (write a `.tmp` and move it into place). The bake needs no checkout move:

  ```
  #version <the tool's Version, from its "const int Version" line>
  F:/Chip Arena Development/scratch/<your task>/motion
  bank=ChipArenaIntroMotionMelee
  clip=Assets/LocalAssets/CLazyRunnerActionAnimPack/Animations/P1_CLazyMovement/Mvm_8way_Dash/CLazy@Mvm_Dash.FBX|Mvm_Dash|0-8|test
  clip=Assets/LocalAssets/CLazyRunnerActionAnimPack/Animations/P2_CLazyEscape/Esc_Slide_Fwd/CLazy@Esc_Slide_All.FBX||20-47|rise
  ```

  - The first line after `#version` is the output folder (on F:). `bank=` is required: your bank's name. Never `ChipArenaIntroMotion`
    (Slipstream's) or `ChipArenaVisualMotion` (the match's).
  - Each `clip=` line has four fields, split by `|`. The asset path of the FBX (or `.anim`). The take: the AnimationClip's name in it,
    or empty for its first. A frame range of the take at its own frame rate, inclusive, or empty for all of it. An optional label.
  - The bank's clip is named `<take>`, or `<take>#<label>` with a label. Use that name in `FromPack`. A range is not a loop.
  - **Refused** (listed in `intromotion.json` under `refused`): a take the match plays (its take or its file's name is in the match's
    motion bank), a repeated name (give it a label), and a bad range. **Missing** (under `missing`): a take that is not in the file.
    The report names the file's takes.
  - **Output:** `<out>/<bank>.bytes` ("CAIM" 2, each clip's source `path|take|range` after its name, which `IntroPackChecks` reads) and
    `<out>/intromotion.json` (per clip its source, length, frames, the take's fps, loop, drift and pelvis heights). It never writes
    Resources. The integrator puts your bank in the Editor's Resources and the Drive zip at release.
  - **The older form** (the folders line, then a comma list of clip names) still writes `ChipArenaIntroMotion.bytes` in format 1.
  - **Check it offline:** `CHIPARENA_INTRO_MOTION_MELEE=<out>/ChipArenaIntroMotionMelee.bytes`, then `--intro-pack`. It checks unique
    names, the match's clips, and the limbs, ankles and feet on the model. `--intro-limits` then plays your intros' segments from it.
  - **For a capture,** copy the bank into the Editor's `Assets/LocalAssets/ChipArenaLocal/Resources/` under the lock, with the Editor
    on your branch's commit. The Editor's resting commit (`9319026b`) reads only `ChipArenaIntroMotion` in format 1, so it ignores
    your bank until this framework is merged; it is harmless to leave it there.
  - **Verified 2026-11-20 (I0e):** one Editor hold (20:27-20:31, 229 s). The Editor stayed on `9319026b` and Resources were untouched;
    the tool is now v3487 with the patch. The request above, plus a match clip (`Unarmed-Idle`) and a wrong take, baked
    `Mvm_Dash#test` (frames 0-8 at 30 fps: 0.27 s, 17 frames at 60 Hz) and `Esc_Slide_All#rise` (frames 20-47: 0.9 s). It refused
    `Unarmed-Idle` (the match plays it) and listed the wrong take as missing, with the file's takes. Loaded as the Melee bank beside
    Slipstream's (`--intro-pack`, 135 checks): 11 clips, no repeated name, none the match plays, the limbs, turns and ankles on the
    clip, and the Dash's feet still (0.00 m/s at 9.16 m/s). The test bank was then deleted.
- **The conversion (Core `NativeIntroPack`):** each frame is the model binding (`NativeVisualRig.Pose`) run backwards on the bank's
  skeleton. A keyed joint's turn is its chain's last bone's world turn off the aligned rest, less the chain's start. The arms' spread
  and the wrist's eased bend are undone. The pelvis's offset goes on the keyed body over the legs' ratio. So the keys drawn on the model
  are the clip on the model: the round trip on the measured skeleton redraws the body within 0.01 degrees.
  - The drawn body takes a pack clip's arms exactly as keyed (`NativeVisualRig.Grasp.Exact`, set from
    `NativeProceduralEntrance.PackWeight(t, Arms)`). The voxel-body corrections are let go: Inside (a hand inside the shoulders) and
    Clear (a fist kept off the body). Without that, the slide's left arm was drawn 18 degrees off the clip. The match never sets it.
- **The API:**

  | Call | What it does |
  | --- | --- |
  | `k.FromPack(clip, from, to, start = 0, speed = 1, loop = false, root = Path, mask = All, blendIn = .1, blendOut = .1)` | Plays `clip` over the keys from intro time `from` to `to`, `start` seconds into the clip at `speed`. Its joints in `mask` ease in and out from what the keys put there. The planting lets go while its legs play. A foot the clip has on the floor is held where it came down against the intro's root (the keyed leg as a hinge), eased out over the last 30 ms before it lifts. |
  | `NativeIntroPack.RootMode.Path` | The intro's root (its keys or `RootPath`). Move it at the clip's `Speed` along the facing for the drawn feet to hold. |
  | `NativeIntroPack.RootMode.Kept` | The clip's own travel: on from the root at `from`, along the facing, at `Speed` times the segment's weight. The root stays where the clip took it after `to`. |
  | `k.FromPack(..., body: NativeIntroPack.BodyTurn.Pelvis)` (or `.Clip`; I0e, 2026-11-20) | Opt-in, for a segment with the pelvis in its mask (`All`, `Legs`): what the keys' body turn (joint 0, a keyed lean) does under the clip. `Keyed` (the default, as before) turns the whole clip, its legs and feet too: Slipstream's keyed 20 degree lean tilted the whole run. `Clip` eases it out by the segment's weight, so the clip stands as baked (its own lean is in its pelvis and spine). `Pelvis` rides it on the clip's pelvis: the hips and everything above them lean by it, and the legs keep the clip's turns, so the feet stay where the clip puts them. Key the lean on the body as before; the keys alone (no bank) still lean the whole body. |
  | masks `All`, `Legs`, `Torso`, `Arms`, `LeftArm`, `RightArm` | `Legs` includes the pelvis's turn and place. Keys and `Arms()` still key what a mask leaves out, such as a weapon carry over a pack run (`Legs \| Torso`). The grip IK works on top. |
  | `NativeIntroPack.Local.TryGet(name, out clip)` | The clip: `Length`, `Rate`, `Frames`, `Speed`, `Travel`, and `Loop` as the pack marks it. |
  | `clip.Sample(time, loop, joints, out body)` | Each keyed joint's local turn (22, CapturedRig's order) and the keyed body's offset. `NativeIntroPack.Euler(q, near)` gives the keys' XYZ angles. Allocation-free. |
  | `clip.Foot(time, loop, left, out ankle, out ball)`, `clip.OnFloor(time, loop, left)` | The drawn foot in the rig frame (the model's metres) and whether it is down: for planning the feet in the model's space (a car's frame). |
  | `bank.Model(clip, frame, world, at)` | The frame as baked on the model, bone by bone (for checks). |

- **Without the bank** (CI, a build without the local assets), a segment does nothing and the keys under it play. Key a fallback
  under every pack segment. The same holds for a segment whose clip is in a missing author's bank.
- **Speed:** a clip samples in place. `Speed` is how fast a foot on the floor moves back under the body, which is the speed the root
  must travel for that foot to hold. Match the root to it, or use `Kept`. Where the intro's path must differ (a train, a car), plan
  the feet in the model's space from `Foot` and `OnFloor`.
- **The first bank** (Slipstream's clips, I2b's picks; 2026-11-20): 9 clips, 131,257 bytes, at 60 Hz on the body's 180-bone skeleton:
  `Mvm_PowerDash_Front`, `Mvm_Dash`, `Mvm_Boost`, `Jmp_FrontAir_A_Start`, `Jump_Down_A_Loop`, `Esc_Slide_All`, `Esc_Slide_Start`,
  `Esc_Slide_Loop` and `Esc_Slide_End`. Each clip's keys draw its limbs within 0.1 degrees of the clip on the model, with the ankles
  within a millimetre. The turns are within 4.7 degrees, the worst being `Esc_Slide_Start`'s right hand, a wrist bent past what the
  binding's eased wrist reaches.
  - `Speed`: `Mvm_Dash` 9.12 m/s, `Mvm_PowerDash_Front` 7.95, `Jmp_FrontAir_A_Start` 2.21. On its own travel the Dash's feet slide
    0.00 m/s.
  - `Mvm_Boost` barely touches the floor on this body: its feet are down in 12% of frames, and its stance ankle stays about 5 cm high.
    Prefer the Dash, or lower the root.
  - The slides put a leg up to 6 cm into the floor (`Esc_Slide_Loop`). Lift the root by that much while sliding.
- **Checked** (`Tests/IntroPackChecks`, `--intro-pack`, and in `--intros-v2`):
  - always: the Euler round trip, the binding's round trip on the measured skeleton, and that sampling allocates nothing; banks merged
    (a repeated name listed, the first bank's playing; a clip the same through the merged banks as through its own, allocation-free),
    a Sourced bank's sources, and the match-clip finder on synthetic banks;
  - with the local banks (each found as above): no clip name in two banks, no clip the match plays (with the match's bank here), and
    every clip's keys draw its limbs along the clip on the model, and its ankles where the clip's are. A grounded clip (a foot down a
    sixth of the time or more), played through `FromPack` on its own travel, keeps its feet still and out of the floor.
  - `--intro-limits` and the bone-for-bone mapping (`IntroPoseChecks.Mapping`, I0e) hold a joint a pack segment plays in full to the clip
    on the model, not to the keyed limits or turns (a mocap hand bent flat on the floor drew 116 degrees off its keyed turn). The keyed wrist
    is the weapon's frame, which the binding eases, so a mocap wrist reads as bent past its range there. The keyed torso is the voxel
    body's. `IntroPackChecks` lists those keyed findings per clip, for information.
  - `CHIPARENA_PACK_DUMP=<clip>` prints that clip's feet frame by frame.
  - `IntroPoseChecks` loads the local bank first, so `--intro-limits` checks an intro's pack segments as the game plays them.
