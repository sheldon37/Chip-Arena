# The logo intro at launch

Owner, 2026-11-11: "Lets add this as an intro when starting the game (Use this time to load assets in). (This is also the new
logo for the game)". The Unity game (`unity/ChipArenaConversion`) plays the owner's logo animation every time it starts and loads
the game under it. Presentation only: no rule, save or network state reads it.

## What the player sees

1. Black from about 1.2 s after launch: the intro's curtain, up a frame before the game's Awake. There is no Unity splash
   screen any more (owner, 2026-11-11: "Yes, remove it"; see "Unity's splash screen" below). The clip starts about 3.5 s after
   launch.
2. The clip, full screen. It is letterboxed or pillarboxed to keep 16:9 on any screen, with black elsewhere.
   - Its sound plays at the **Menu Music** level, levelled to the menu song (x0.72; the clip measures -21.7 LUFS, the menu song
     -14.1 LUFS at its x0.3). **Mute audio** silences it.
   - The cursor is hidden, and nothing under the intro takes a click or a key.
3. **A skip:** a click (left or right), Esc, Enter or Space skips it once it has been up half a second. So does a pad's A, B or
   Start. The clip gives way to the final logo over 0.25 s, and its sound fades with it.
4. **A hold:** if the game is not ready when the clip ends or is skipped, the final logo holds. After 0.3 s a small loading
   indicator fades in under the chip: ten orange dots turning, and `L O A D I N G` in the logo's cream.
5. **The crossfade:** once the game is ready (and the clip is over), the intro crossfades into the main menu over 0.6 s.
   - The menu takes input from the start of the crossfade.
   - The menu song starts from its top.
   - A notice raised under the intro (a save warning, an account message) shows now, for its full 6.5 s.
   - A start-up error ends the hold and shows after the intro, never under it.

If loading finishes early, the clip still plays out unless skipped. A clip that cannot play (the file missing, no decoder, an
error, or no frame within 4 s) is replaced by the final logo, which holds while the game loads. A clip whose clock stops for 2 s
counts as over.

## When it does not play

- on a dedicated server (`--ca-server`: Awake returns before the intro);
- in batch mode;
- with `-nointro` (also `--nointro`, any case) on the command line;
- in the Editor, unless asked (below), and never under the local capture tool's runs (a run of the BoZo tool's play job,
  SessionState `bozo-play-job`), even when "always" is on.

Without the intro, start-up is unchanged: the motion banks load in Awake as before, and none of the loading below runs.

**In the Editor:** use **Chip Arena > Boot intro**.
- **Play once** enters Play mode with the intro.
- **Play in Play mode** plays it on every entry until it is unticked (EditorPrefs `ChipArena.BootIntro.PlayMode`).

A capture-tool run asks for it with `bootintro=1`, which sets the Play-once flag (SessionState `ChipArena.BootIntro.Once`).

## The loading under it

Before the intro, everything loaded on the main thread before the first frame. Measured in a Windows build (2026-11-11):

- Unity's splash ran first (2.2 s), with nothing of the game running.
- Then Awake: the maps 0.8 s, the two motion banks 2.3 s (about 14 MB and 94 MB of JSON), and presentation.Initialize 3.5 s.
  Of that, 0.9 s was the source models' JSON and about 1.8 s their procedural builds.
- Then the menu's first frame: 0.8-0.9 s, frozen.

With the intro (Runtime/NativeBootLoad.cs):

1. **First, before anything of the game** (a `BeforeSplashScreen` hook, `NativeBootPrefetch`): it decides whether the intro
   will play, from the command line and the Editor's asks only. If it will:
   - **Banks.** It reads three resources' bytes on the main thread (about 25 ms): the two motion banks and `SourceModels`.
     Workers then do the rest (Runtime/NativeBankLoad.cs): the envelope check, base64, gzip and `JsonUtility.FromJson`; for the
     motion banks, their own clip checks and the weapon intros' clearance plans (`NativeIntroMotion.Prepare`).
     - Every read of a motion bank (`Ensure`) waits for its pending load, so nothing reads a bank before it is in. A failure
       rethrows where the synchronous load threw.
     - The source models are still built where they always were, in `presentation.Initialize`, from that JSON
       (`NativeSourceModels`); there it takes 11 ms instead of 0.9 s.
   - **Sculpts.** Another worker makes the pure geometry behind `NativeSourceModels`' slowest builds (Core
     `NativeBootSculpts`, 229 jobs, about 1.7 s on that worker):
     - the Siren minion kinds and the zone kinds' props (`HordeKindModels`, `IronworksModels`, `StationPropModels`);
     - the robot helmets (`HelmModels`);
     - the Standard weapons (`WeaponSkins`).
     The builders still run in `presentation.Initialize`, in the same order, and still make every mesh and material there.
     Each asks `NativeBootSculpts.Take` for its sculpt. The first ask waits for the whole worker, so no sculpt runs beside it
     after that, and each result is handed over once. Without the worker, or for anything not in its list, the builder sculpts
     as before. CoreTests hold the worker's results to the single-threaded ones.
2. **The curtain.** `ConversionGame.Boot` raises the intro's black curtain and makes the game one frame later
   (`NativeBootDefer`). Without Unity's splash the window stayed unpainted white through Awake; now that frame is black. The
   curtain neither plays nor runs its clock until Awake arms it. A curtain the game never arms (Awake failed, or chose no
   intro) comes down at once, so its error shows.
3. **Awake** runs as before, in the same order. The intro is armed where the banks used to load.
4. **Banks joined.** The first frame's loading step joins both banks and logs `MOTION_BANKS_READY ... (on workers from before
   the splash ...)`.
5. **Assets.** What the menu's first frames would load on the main thread comes in by `Resources.LoadAsync`. This runs at the
   Low background priority while the intro plays (restored after it): the model's bake (`ChipArenaVisualBody`, unless
   `--ca-voxel-bodies`), its outfit table and the two icon sets.
6. **Scene.** Once the clip has settled on its logo (from 8.1 s, or once it is over or skipped), the menu's scene builds and
   draws for two frames under it, with its music held (`NativeAudio.MenuHeld`). Its stalls (0.35 s, then 0.03 s) land on a
   still frame.
7. **GUI.** Then the menu's GUI draws for two frames under it (0.21 s, then 0.08 s), making its skins, text, icons and portraits.
8. **Ready.** The intro may crossfade.

Until step 6, `LateFrame` and `OnGUI` return at once and `Update` takes no input. The network, deployment and account ticks
still run. The rest of Awake (audio, presentation, cameras, lighting, settings, updater) runs exactly as before, in the same
order. The updater's check starts in Awake as before, and its state shows on the home screen after the crossfade.

**Still on the main thread before the clip's first frame** (black, Awake: about 2.4 s):
- the maps (`NativeUnityMapLoader`, 0.9 s). This is content, on Astra's side, and was left alone.
- `presentation.Initialize`, 1.3 s. The rest of the source model builds (their meshes and materials, and `MachineSkins`,
  `ModelHats`, `SuitModels` 0.07-0.17 s each), and `CapturedAnimation` 0.17 s.

## Files

| File | What |
| --- | --- |
| `Assets/StreamingAssets/BootIntro.mp4` | The clip. The player plays it by URL; the build copies StreamingAssets as it is. |
| `Assets/ChipArena/Resources/BootIntroLogo.jpg` | The clip's last frame: the logo held after a skip, or in place of a failed clip. |
| `Core/NativeBootIntroRules.cs` | When it plays, the letterbox, the timings, and the timeline (`NativeBootIntroFlow`). |
| `Runtime/NativeBootIntro.cs` | The VideoPlayer, its sound, the overlay, the indicator and the skip. |
| `Runtime/NativeBootLoad.cs` | The loading steps and the hooks in ConversionGame's Update, LateFrame and OnGUI. |
| `Runtime/NativeBankLoad.cs` | A bank decoded on a worker. |
| `Core/NativeBootSculpts.cs` | The source models' sculpts made on a worker, and handed to their builders. |
| `Editor/NativeBootIntroBuild.cs` | The Editor menu, and the build check. |
| `Tests/BootIntroChecks.cs` | CoreTests `--boot-intro`. |

The clip is a 4 Mbps re-encode of the owner's 8.9 Mbps upload (docs/ASSET-CREDITS.md "Logo intro"):

- 5,266,834 bytes, High profile without B-frames (Unity's Windows decoder skews B-frame timestamps);
- the same 241 frames, 24 fps and 1280x720;
- the AAC stream copied unchanged;
- SSIM 0.995 against the upload.

Every build checks the clip twice: the project's copy before it builds, and the player's
`Chip Arena_Data/StreamingAssets/BootIntro.mp4` after. Both must match `NativeBootIntroRules.ClipBytes` and `ClipSha256`
(`BOOT_INTRO_CLIP_IN_BUILD` in the build log). A changed clip needs those two constants, its asset-lock receipt and its
credits entry updated.

The project needs the **Video module** (`com.unity.modules.video` in `Packages/manifest.json`). It was not in the project
before; without it there is no `UnityEngine.Video`.

## Unity's splash screen

It is off: `m_ShowUnitySplashScreen: 0` in the tracked `ProjectSettings/ProjectSettings.asset` (owner, 2026-11-11, asked about
it: "Yes, remove it"). That is the only Player Settings change. It played for about 2.2 s before the first scene loaded. The
intro still waits for `SplashScreen.isFinished`, which is now true at once.

## Logs

- `BOOT_INTRO playing at T s since start` or `BOOT_INTRO off (reason)`, from Awake.
- `MOTION_BANKS_READY ...`.
- `BOOT_HANDOFF interactive at T s since start ...`, with the steps' times since the workers started, the warm frames' cost,
  and the sculpts: made, taken by their builders, sculpted by a builder itself, and unused (all 229 taken when the list matches).
- `BOOT_INTRO shown_ms= play_ms= over_ms= ready_ms= fade_ms= done_ms= skipped= failed= held_ms= frames n= mean= p95= p99=
  max= over33= over50=`. These are the main-thread frame intervals while the clip moved.

**DEV:** `--ca-boot-hold S` keeps the loading open until S seconds after the intro began, to see the hold and its indicator.
The capture tool's `boothold=S` sets the same.

## Measured (2026-11-11)

These runs used Windows Development builds, as the builder makes them. The PC has an RTX 4090. The window was 1600x900,
with fresh preferences. A scratch probe, never committed, timed every frame from process start. "Interactive" means the menu
takes input: for the intro, the start of its crossfade. There were 3 runs per figure, 2 for the skips.

| | Before (base 7e4d6378) | Intro, Unity splash on | Intro, no splash, sculpts on a worker, curtain |
| --- | --- | --- | --- |
| Launch to anything of the game on screen | 10.2-10.3 s (the menu) | 7.1-7.5 s (the clip) | 1.2 s (black), 3.3-3.7 s (the clip) |
| Launch to an interactive menu | 11.0-11.2 s | 17.2-17.5 s played out; 8.3-8.4 s skipped at 0.5 s | 13.4-13.7 s played out; 4.6 s skipped at 0.5 s |
| The menu's first frame | 0.80-0.94 s, one frozen frame | under the settled logo: 0.35, 0.03, 0.23, 0.08 s | the same |
| Main-thread frames while the clip moves (0-8.1 s) | - | 1166 frames: mean 6.9 ms, p99 7.1-7.2 ms, max 7.6-9.4 ms | 1165-1166: mean 6.9-7.0 ms, p99 7.2-7.3 ms, max 8.7-10.3 ms |
| The first 3 s after interactive | mean 7.8-8.4 ms, p99 22-24 ms, max 38-41 ms | mean 7.9-8.0 ms, p99 10-11 ms, max 15-16 ms | mean 7.2 ms, p99 8.5-9.5 ms, max 12-14 ms |

Notes on these numbers:
- The frame rate is capped at 144 (6.9 ms). No frame while the clip moves was over 20 ms.
- With `-nointro`, the old path measured 10.7 s (with the splash).
- The skip numbers use a press 0.5 s into the clip: the logo then held 0.7 s while the menu built under it.
