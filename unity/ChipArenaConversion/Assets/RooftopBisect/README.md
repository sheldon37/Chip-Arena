# Rooftop shader compile-crash bisect (temporary; never merge)

The Unity Editor's shader compiler process dies compiling `ChipArena/NativeRooftop` pass 0's fragment program
(DEV_LOG 2026-10-22 BoZo core entry). The cloud session has no Windows shader compiler, so this kit lets one asset
refresh show which part of the shader triggers it. Each shader here is a copy of `Resources/NativeRooftop.shader`
(as of shared `3564080`) with one change, renamed `ChipArena/RooftopBisect/<name>`:

| Shader | Change from the real shader | If it crashes / compiles |
|---|---|---|
| `A_Current` | none (control) | must crash; if it compiles, the crash depends on the file's place or name |
| `B_LegacyOnly` | the anime surfaces are never called (`RoofToonSurface` drops out) | compiles: the cause is in `RoofToonSurface` (see D-G) |
| `C_LegacyNoToonPoints` | B, and the legacy props without `ToonPoints` (the rooftop before stage 1) | B crashes but C compiles: `ToonPoints` in this pass |
| `D_ToonFacadeOnly` | `RoofToonSurface` keeps only the facades (mode 0) | the one of D/E/F that crashes holds the cause |
| `E_ToonPropsOnly` | `RoofToonSurface` keeps only the props (mode 1) | |
| `F_ToonPuddleOnly` | `RoofToonSurface` keeps only the puddles (mode 4) | |
| `G_ToonPuddleNoClip` | the anime puddles without their `clip` | compiles where A crashes: the `clip` inside the function |
| `H_NoShadowPassNoStencil` | no shadow caster pass and no stencil block | compiles where A crashes: the pass layout, not the code |

## Steps (local Windows Editor session)

1. `git fetch origin claude/rooftop-bisect`
2. Copy the kit into the working tree only (not staged, not committed):
   `git restore --source origin/claude/rooftop-bisect --worktree -- unity/ChipArenaConversion/Assets/RooftopBisect`
3. In the Editor, refresh assets (Ctrl+R) and wait for the import to finish.
4. In `Editor.log`, find each `Compile ChipArena/RooftopBisect/<name>` line that is followed by
   `Internal error communicating with the shader compiler process`. Also select each shader in the Project window:
   the Inspector lists any compile error.
5. Record per shader: crashed / compiled / other error (with its text), in the next DEV_LOG entry or to the owner.
6. Delete `unity/ChipArenaConversion/Assets/RooftopBisect` and `RooftopBisect.meta` (Unity created the metas; nothing
   is tracked on your branch). Do not commit any of it.
