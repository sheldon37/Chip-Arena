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
| `Pose(t, pose, out root, out rotation)` | The keyed rig pose (`Keys`/`Steps`, `NativeProceduralEntrance`): the anchors' source for the camera and effects, and the Classic body's pose. |
| `Clip(t, out clip, out time)` / `Fade(t)` (`INativeIntroBody`; virtuals on `NativeIntroV2Script`) | The model body plays a motion-bank clip at its time (you retime it), fading in over `Fade` s; false: the model follows the keyed rig. One clip at a time (no upper-body layer tonight). A `NativeBladeIntro` keeps its own path. |
| `Hands(t, right, left)` (`INativeIntroHands`, optional) | Called before `Props`, `Set` and `Effects` with each wrist as drawn (source frame): the model's joint when the model is drawn (`Model` true), else the keyed hand anchor. For a gun Weapon intro (pistol, rifle, machine gun) the right hand also carries `HasMuzzle`, `Muzzle` and `Bore`: the drawn weapon's muzzle and barrel direction. For a staff head or a blade tip, measure your offsets in the wrist frame in your Editor hold. |
| `Head(t, head)` (`INativeIntroHead`, optional) | The head as drawn, handed in with the hands: the model's head joint when the model is drawn, else the keyed head anchor. |
| `Bow(t, out view)` (`INativeIntroBow`, optional, Bow intros) | The bow drawn as the match draws it (on the model's left hand, its string and its nocked arrow) from a `NativeBow.View` you give. Without it, the bow is carried with one arrow. |
| `Camera(age, out view)` | `NativeIntroV2Script` evaluates `Rig` (section 4). Override it only if you must. |
| `Lights(t, lights)` | The void rig (`NativeIntroVoid.Light`), then add your own: `l.Add(at, colour, distance)`, at most 8 points. |
| `Props(t, props, quality)` | Primitive set pieces through the source models. The base draws no pylon arena. |
| `Set(t, set)` | Your modelled set: override `Scene(t, set)` (section 3). |
| `Effects(t, fx, quality)` | Effects through `INativeEntranceFx`, which reaches `NativeVfxDraw`: `Glow`, `Streak`, `Smoke`, `Fire`, plus the helpers in `NativeEntranceFx` (Sparks, GroundDust, Embers, Shaft, Ribbon). |
| `Ghosts` / `Holo` / `Solid` / `Blasts` | As for every directed entrance (see `NativeEntranceDirector.cs`). |
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
- Put your rows in your own `docs/audio/intros/<author>.json`, in the manifest's sound schema.
- Add one contract line per key to `Core/NativeIntroSoundKeys.cs`, on your slot.
- `tools/intros/compile_sound_requests.py` merges every author's rows into `docs/audio/sound-requests-intros.json` and
  prints the approved builder job.
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

- **Fair game:** clips already in the motion bank, layered and retimed in code (the `NativeIntroMotion` pattern;
  `Core/NativeBladeIntros.cs` and `Core/NativeDevIntros.cs` pose the model from pack clips).
- **New pack clips:** ask I0 in `intro-motion-requests.md` in the notes folder (the clip's pack, name and why). I0 does
  at most one coordinated rebake overnight, and only if the bake tool does that safely. Never rebake the motion bank
  yourself.
