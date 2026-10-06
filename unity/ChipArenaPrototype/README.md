# Chip Arena — Unity Lab (isolated prototype)

This is a **Unity source project**, not a prebuilt Windows game. It is an early engine-evaluation slice on `codex/unity-prototype`, based on shared Chip Arena commit `05950eaef80ff2483a9c44d898cad98be1a76bbc`.

The existing Electron game, saved accounts and update channels are separate. This project has no updater, account service, purchases, telemetry, or production endpoints.

## Open and play

1. Install **Unity 6.3 LTS, 6000.3.24f1**, through Unity Hub. The exact editor is pinned in `ProjectSettings/ProjectVersion.txt`. [Official editor download](https://unity.com/releases/editor/whats-new/6000.3.24f1).
2. In Unity Hub, choose **Add → Add project from disk**, then select this `ChipArenaPrototype` folder (the folder containing `Assets`, `Packages`, and `ProjectSettings`).
3. Let Unity finish importing. Activate your own Unity license in Hub if prompted. No license or account credentials are included here.
4. Open **Chip Arena → Open test arena**, then press the Editor's **Play** button.
5. Choose **Play solo**. Escape opens the test controls. Start with 8 bots before increasing the workload.

| Control | Action |
| --- | --- |
| WASD / mouse | Move / look |
| Double-tap W | Sprint; release W to reset exhaustion |
| Left click | Sword attack; another click queues the next hit |
| Right click | Timed block; reflects energy bolts |
| Left Shift | Dodge; cancels sword attack and has a brief invulnerability window |
| Space | Jump |
| Hold E | Fire test energy bolts |
| Escape | Session menu; pauses local combat, but not hosted combat |
| F8 | Record an uninterrupted 30-second frame-time sample |

Characters automatically respawn. All three sword combo stages currently use the source sword's 30 damage, 14 stamina, .14 s windup and .62 s interval. Their poses are procedural placeholders. The ability/cosmetic catalog and purchased animation packs are not included.

## Build a Windows test ZIP

Install the Windows build support available for this editor version in Unity Hub. Windows Editor installations normally include Mono desktop support; non-Windows editors need the Windows Mono module.

- In Unity: **Chip Arena → Build Windows TEST ZIP**.
- Or close the project in Unity and double-click **Build-Windows.cmd** on Windows. If Unity is installed elsewhere, set `UNITY_EDITOR` to its `Unity.exe` path before running the command.

The command first runs the core/project checks, applies the stylized voxel **CA** icon, then creates `Builds/Chip-Arena-Unity-TEST-Windows.zip`. It must report `UNITY_TEST_BUILD_READY` before the archive is considered built. On success, the script opens `Builds/Windows`; launch `ChipArena-Unity-TEST.exe` there. Keep the entire extracted Windows folder together: the EXE needs its adjacent `UnityPlayer.dll` and game data. Players do not need the Unity Editor to run a completed player build.

For a desktop launcher, create a Windows shortcut to `ChipArena-Unity-TEST.exe`; it uses the embedded CA icon. Do not move the EXE out of its folder. See `ICON.md` for the original icon and its generation prompt.

This is a **development build**, suitable for attaching the Unity Profiler. It is not a release or an installer. No Windows executable was compiled in the original authoring workspace; see `VALIDATION.md` for actual checks.

### Recover from the first package-resolution failure

The first source archive incorrectly requested `com.unity.modules.inputlegacy`, which is not a package in the pinned Unity Editor. This has been removed; the prototype continues to use Unity's legacy `UnityEngine.Input` API. A managed assembly name does not establish a valid Package Manager package name.

The owner's 2026-10-06 Windows log confirms **6000.3.24f1** ran and resolved a Unity Personal entitlement, then exited while resolving packages. It also reports `physicscore2d`, `tetgen` and `timelinefoundation` as unavailable. These three are absent from the committed manifest; their origin in the local project has not been verified.

Close Unity, extract the corrected source archive into a **new folder**, and open that new `ChipArenaPrototype` folder with **6000.3.24f1** in Hub. Do not copy the old `Library`, `Temp` or `Packages/packages-lock.json` into it. After import finishes, close the Editor and run the new `Build-Windows.cmd`. If using an existing Command Prompt where `UNITY_EDITOR` was set to 6000.6.4f1, set it to the 6000.3.24f1 executable for this build. Keep the original folder if it contains your own edits or assets.

Alternatively, replace only `Packages/manifest.json` with the corrected file and, with Unity closed, move the old `Library` folder and `Packages/packages-lock.json` outside the project so Unity can regenerate them. Stay on 6000.3.24f1 while diagnosing this build. If another failure occurs, share the new `Builds/build.log`; fixing package names is not proof the remaining import/player build succeeds.

## Two-player LAN / two windows on one PC

1. Run two standalone instances, or one standalone plus Editor Play mode.
2. First window: **Host LAN**. The menu shows local IPv4 addresses and the default port, `27880`.
3. Second window: enter the host's IPv4 address and choose **Join**. On the same computer use `127.0.0.1`.
4. If Windows asks about network access, private-network access is sufficient for LAN. There is no automatic firewall modification or port forwarding.
5. After a disconnect, start a new Host session before rejoining.

This is a **trusted-LAN synchronization diagnostic** with one host and one guest. The host owns movement/damage; inputs are sent at 60 Hz and snapshots at 20 Hz. It uses a separate bounded binary TCP protocol. There is no client prediction, encryption, internet relay, authentication, compatibility with existing rooms, or production networking claim. Guest responsiveness is not representative of a completed networking port.

## Compare heavier workloads

Menu presets: 0, 8, 32, 96 or 192 bots. Toggle movement-only crowds and visual PhysX debris independently. Set 60, 144 or uncapped FPS. Warm up each workload before recording; changing settings requires opening the menu, which cancels an in-progress recording.

Frame mean/p95 are a rolling sample of up to 600 frames. **Frame time includes frame limiting; it is not a GPU timer.** Authority step time measures this prototype's C# combat/kinematic update only, excluding serialization, Unity rendering and the PhysX debris simulation. Guest input acknowledgment time includes send, host scheduling and snapshot delivery.

F8 writes a CSV after 30 seconds, under this prototype's own `Application.persistentDataPath/Benchmarks`. The menu can copy the last file path. CSV metadata records Unity, OS, CPU, GPU, resolution, frame limit and session mode. Compare equal resolution, graphics settings, crowd counts and combat activity; use the Unity Profiler to locate actual CPU/GPU costs.

**This prototype is simpler than Chip Arena. A better FPS result is not proof that the entire game will be faster in Unity.** There are no production Siren mechanics, full rigs/clip blending, crowd collision, gameplay destruction physics, all Chip combinations, or save systems in this slice.

## Tests and source layout

- `Assets/ChipArena/Core`: standalone C# authority, geometric collision, snapshot codec, and threaded LAN transport.
- `Assets/ChipArena/Runtime`: Unity presentation, procedural instanced voxel rigs, particles, decorative PhysX debris, camera, menu and measurements.
- `Assets/ChipArena/Editor`: project validation and isolated Windows packaging.
- `Tests`: no-dependency .NET 8 runner for the same core code. Run `dotnet run --project Tests/CoreTests.csproj -c Release`.
- `Assets/ChipArena/Scenes/Prototype.unity`: minimal entry scene. The runtime bootstrap builds the arena when Play starts.

Read `PROTOTYPE-SCOPE.md` for preserved constants and deliberate differences. Read `VALIDATION.md` before interpreting anything as tested.
