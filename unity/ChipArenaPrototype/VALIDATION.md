# Validation status — 2026-10-06

This is source for an isolated Unity prototype. **No Unity Editor playtest or Windows executable build is claimed.**

## Character/projectile visibility follow-up

The owner reports invisible characters and projectiles after the package repair. This is a real visual failure report; it does not establish whether testing was in the Editor or a standalone player. Both affected object types use `Graphics.DrawMeshInstanced`; the floor and cover use ordinary MeshRenderers. No runtime log, screenshot or GPU capture was supplied for this failure.

The source had no protection against Unity stripping the instancing variants for runtime-created materials. The documented fix is now applied in project preparation and a pre-build callback: **Instancing Variants = Keep All**. The shader explicitly targets model 3.5. F9/menu compatibility mode uses individual draws and separate non-instancing materials; instanced-draw exceptions also switch automatically. HUD and CSV identify the renderer mode. Manual switches cancel a frame capture to avoid comparing mixed modes.

Runtime, Editor and Windows-conditional runtime scripts compile against the actual Unity 6000.3.24f1 managed APIs with warnings as errors. Package regressions and source/archive checks pass. No new gameplay rules or networking behavior changed. Shader compilation, execution of the build callback, Windows shader retention, both rendering paths and actual restored visibility still require testing on the owner's activated Unity installation. Shader stripping is a strong code-supported hypothesis, not a reproduced GPU diagnosis.

## Passed in the authoring workspace

- The six engine-independent Core C# files compile with Microsoft's real Roslyn compiler from .NET SDK 8.0.425 against .NET 8 reference assemblies, with warnings treated as errors.
- All runtime scripts compile with C# 9 against the **actual Unity 6000.3.24f1 managed assemblies and its .NET Standard 2.1 references**, with warnings treated as errors. The Editor assembly compiles separately against that runtime assembly; a second runtime compilation with `UNITY_STANDALONE_WIN` and without `UNITY_EDITOR` also passes. These are managed API compilation checks, not Unity player builds.
- 22 managed-core regression checks pass, including movement bounds, cover collision, one-hit sword timing, parry ordering/cooldown, dodge cancellation/window, stamina, combo buffering, sprint exhaustion, jumping, swept bolts, reflection ownership, breakage, respawn, invalid input, snapshot round trip, malformed packet rejection and bot resets.
- The 192-bot/600-tick stress regression remains finite and serializable. This is a correctness check, not a published performance benchmark.
- Real TCP loopback test passes: connect host/guest, send framed input, execute remote attack on the authority, transmit/parse snapshot, and handle disconnect. This is not a two-Unity-window playtest.

The normal `dotnet run` frontend could not start in this container because its process-information lookup is unsupported. The installed SDK's Roslyn compiler and .NET runtime were invoked directly. No fake Unity types were used to make these core tests pass.

Unity API references were extracted from the official editor archive `https://download.unity3d.com/download_unity/4e7b9b5b6244/LinuxEditorInstaller/Unity-6000.3.24f1.tar.xz`. No Unity binaries are redistributed in the source archive. The reference compilation exposed and fixed an ambiguous `CompressionLevel` in the Windows ZIP builder before delivery.

## Not yet run

### Owner's first Windows attempt and package correction

The supplied 2026-10-06 log shows the real Windows Editor **6000.3.24f1** accepted a Unity Personal entitlement but exited with code 1 during package resolution, before the project's build method ran. The original manifest wrongly declared `com.unity.modules.inputlegacy`; this is now removed. The other unavailable modules named in that log (`physicscore2d`, `tetgen`, `timelinefoundation`) were not in the committed manifest. This is a confirmed failed import attempt, not a successful player build.

Package validation now uses a versioned catalog extracted from the pinned Editor's official built-in package metadata, instead of trusting the `com.unity.modules.` prefix. Regression checks reject all four names from that log and unsupported package versions. The corrected project must still be imported and built on the owner's activated Windows Editor. The icon is unchanged.

2026-10-06 icon/EXE follow-up: the generated 1254 × 1254 RGBA icon is included unchanged. The updated Editor helper compiles against the real Unity 6000.3.24f1 assemblies with warnings treated as errors. Source validation checks the PNG header, dimensions, alpha-capable format and asset metadata. The helper assigns and reads back Unity's default/standalone icon slots, and rejects incomplete Windows player output. The Windows command clears its previous log before starting, then opens the output folder only after the success marker. These runtime build checks have not executed in an activated Unity Editor; this workspace still cannot produce a licensed Windows player build.

- Unity Editor import, shader compilation, scene startup and interactive gameplay.
- Windows EXE icon embedding, Explorer/desktop shortcut appearance and Windows command execution.
- Actual Unity renderer, graphics driver, PhysX debris, UI layout and input focus validation.
- Windows player compilation, ZIP launch and two-Unity-window LAN acceptance.
- Production animation pack/rig retargeting, production gameplay parity, and comparative Electron-versus-Unity performance measurements.

The Editor's **Chip Arena → Run core and project checks** repeats the core checks inside Unity and checks the scene/shader imports. **Build Windows TEST ZIP** runs that validation before building. Editor/build failures must be fixed before calling a Windows package verified.

The branch's source-artifact workflow runs the managed tests and source structure checks only. Its artifact is explicitly named **Source**, not a compiled game.
