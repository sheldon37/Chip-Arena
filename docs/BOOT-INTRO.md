# The logo intro at launch

Owner, 2026-11-11: "Lets add this as an intro when starting the game (Use this time to load assets in). (This is also the new
logo for the game)". The Unity game (`unity/ChipArenaConversion`) plays the owner's logo animation every time it starts and loads
the game under it. Presentation only: no rule, save or network state reads it.

## What the player sees

1. Unity's own splash screen, if the build shows one (see "Unity's splash screen" below). The intro waits for it to finish.
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
- Then Awake: the maps 0.8 s, the two motion banks 2.3 s (about 14 MB and 94 MB of JSON), and presentation.Initialize 3.5 s
  (0.9 s of it the source models' JSON).
- Then the menu's first frame: 0.8-0.9 s, frozen.

With the intro (Runtime/NativeBootLoad.cs):

1. **Before the splash.** A `BeforeSplashScreen` hook (`NativeBootPrefetch`) decides whether the intro will play, from the
   command line and the Editor's asks only. If it will, it reads three resources' bytes on the main thread (about 25 ms):
   the two motion banks and `SourceModels`. Workers then do the rest (Runtime/NativeBankLoad.cs):
   - the envelope check, base64, gzip and `JsonUtility.FromJson`;
   - for the motion banks, their own clip checks, and the weapon intros' clearance plans (`NativeIntroMotion.Prepare`).
   All of it is plain C#, with no Unity object. All three finish during Unity's splash (by 2.1 s).
   - Every read of a motion bank (`Ensure`) waits for its pending load, so nothing reads a bank before it is in. A failure
     rethrows where the synchronous load threw.
   - The source models are still built where they always were, in `presentation.Initialize`, from that JSON
     (`NativeSourceModels`); there it takes 11 ms instead of 0.9 s.
2. **Awake** runs as before, in the same order. The intro starts where the banks used to load.
3. **Banks.** The first frame's loading step joins both banks and logs `MOTION_BANKS_READY ... (on workers from before the
   splash ...)`.
4. **Assets.** What the menu's first frames would load on the main thread comes in by `Resources.LoadAsync`. This runs at the
   Low background priority while the intro plays (restored after it): the model's bake (`ChipArenaVisualBody`, unless
   `--ca-voxel-bodies`), its outfit table and the two icon sets.
5. **Scene.** Once the clip has settled on its logo (from 8.1 s, or once it is over or skipped), the menu's scene builds and
   draws for two frames under it, with its music held (`NativeAudio.MenuHeld`). Its stalls (0.35 s, then 0.03 s) land on a
   still frame.
6. **GUI.** Then the menu's GUI draws for two frames under it (0.23 s, then 0.08 s), making its skins, text, icons and portraits.
7. **Ready.** The intro may crossfade.

Until step 5, `LateFrame` and `OnGUI` return at once and `Update` takes no input. The network, deployment and account ticks
still run. The rest of Awake (audio, presentation, cameras, lighting, settings, updater) runs exactly as before, in the same
order. The updater's check starts in Awake as before, and its state shows on the home screen after the crossfade.

**Still on the main thread before the clip's first frame (about 4.1 s after the splash, a grey window):**
- the maps (`NativeUnityMapLoader`, 0.75 s: content, Astra's side);
- `presentation.Initialize`, 2.6 s. Most of this is the source models' procedural builds: `HordeKindModels` 1.1 s,
  `HelmModels` 0.47 s, `WeaponSkins` 0.23 s, `MachineSkins` and `ModelHats` 0.16 s each;
- 0.5 s from the end of Awake to the first frame.

These are the next things to move: built lazily, or under the settled logo. The grey is the splash's background colour (a
Player Settings choice); black would meet the clip's black.

## Files

| File | What |
| --- | --- |
| `Assets/StreamingAssets/BootIntro.mp4` | The clip. The player plays it by URL; the build copies StreamingAssets as it is. |
| `Assets/ChipArena/Resources/BootIntroLogo.jpg` | The clip's last frame: the logo held after a skip, or in place of a failed clip. |
| `Core/NativeBootIntroRules.cs` | When it plays, the letterbox, the timings, and the timeline (`NativeBootIntroFlow`). |
| `Runtime/NativeBootIntro.cs` | The VideoPlayer, its sound, the overlay, the indicator and the skip. |
| `Runtime/NativeBootLoad.cs` | The loading steps and the hooks in ConversionGame's Update, LateFrame and OnGUI. |
| `Runtime/NativeBankLoad.cs` | A bank decoded on a worker. |
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

The builds' Player Settings show Unity's splash screen (`m_ShowUnitySplashScreen: 1`; the repository does not track
ProjectSettings). It plays for about 2.2 s before the first scene loads, and the intro waits for it (`SplashScreen.isFinished`).
The banks decode during it. After it, its grey background (`m_SplashScreenBackgroundColor`, 0.12) shows until the clip's first
frame.
Turning it off is the owner's call, and a Player Settings change: `PlayerSettings.SplashScreen.show=false` in
`PrototypeBuild.Prepare` (build tooling) or the builder's ProjectSettings. Nothing here changes it.

## Logs

- `BOOT_INTRO playing at T s since start` or `BOOT_INTRO off (reason)`, from Awake.
- `MOTION_BANKS_READY ...`.
- `BOOT_HANDOFF interactive at T s since start ...`, with the steps' times since the intro began and the warm frames' cost.
- `BOOT_INTRO shown_ms= play_ms= over_ms= ready_ms= fade_ms= done_ms= skipped= failed= held_ms= frames n= mean= p95= p99=
  max= over33= over50=`. These are the main-thread frame intervals while the clip moved.

**DEV:** `--ca-boot-hold S` keeps the loading open until S seconds after the intro began, to see the hold and its indicator.
The capture tool's `boothold=S` sets the same.

## Measured (2026-11-11)

These runs used Windows Development builds, as the builder makes them. The PC has an RTX 4090. The window was 1600x900,
with fresh preferences. A scratch probe, never committed, timed every frame from process start. "Interactive" means the menu
takes input: for the intro, the start of its crossfade.

| | Before (base 7e4d6378) | With the intro |
| --- | --- | --- |
| Launch to the first frame | 10.2-10.3 s | 7.1-7.5 s (the clip's first frame) |
| Launch to an interactive menu | 11.0-11.2 s (3 runs) | 17.2-17.5 s with the clip played out; 8.3-8.4 s skipped at 0.5 s |
| The menu's first frame | 0.80-0.94 s, one frozen frame | split under the settled logo: 0.35, 0.03, 0.23, 0.08 s |
| Main-thread frames while the clip moves (0-8.1 s) | - | 1166 frames: mean 6.9 ms, p99 7.1-7.2 ms, max 7.6-9.4 ms, none over 20 ms |
| The first 3 s after interactive | mean 7.8-8.4 ms, p99 22-24 ms, max 38-41 ms | mean 7.9-8.0 ms, p99 10-11 ms, max 15-16 ms |

Notes on these numbers:
- The frame rate is capped at 144 (6.9 ms).
- With `-nointro`, the old path measured 10.7 s.
- The skip numbers use a press 0.5 s into the clip: the logo then held 0.7 s while the menu built under it.
