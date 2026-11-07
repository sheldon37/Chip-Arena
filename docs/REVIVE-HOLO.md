# Holographic revive

Owner request (2026-10-13): "Improve revive effect as well (If user was gibbed / wounded, repair with cyber holographic lines and player levitates up for a moment, returns to normal after)".

The holographic revive is presentation only. It changes no revive rule, timing, shield, hitbox or network message. It draws over the existing C17 revive (`Runtime/NativeInteractionPresentation.cs`: the scan of the kept body, its repair voxels and the stand from its pose), whose window is unchanged: a 0.8 s scan, then a 0.6 s stand (`NativeReviveRules`).

## Revive paths

| Path | Where it starts | What the hologram does |
|---|---|---|
| Paid revive (a teammate's F) and solo self-revive | `NativePve.RevivePve` sets `RevivedAt`; the body stays at the revive anchor (`NativeReviveAnchor.cs`) | Full repair of the kept body in place, lift, ring and column |
| Free respawn at a wave clear or boss phase | `RevivePve(p, pad)` | The C17 voxels assemble at the pad; the pilot floats in the column and sets down |
| Dev Sandbox respawn (`SandboxRestore`) | Sets `RevivedAt`, not a PvE revive | Unchanged; the spawn-in presentation owns it |

## What it shows

Everything is a pure function of the death record, the revive time and the anchor:
- **Lift.** The body rises from the anchor's floor to 0.5 m by 0.48 s, holds while it is repaired and stands, and sets down exactly at 1.4 s. Only the drawn rig's root moves; the authority body and collider never do.
- **Hologram band.** A band sweeps the body with the scan front, feet to head.
- **Craters.** A wounded joint's clean skin dithers in over its craters as the front passes. Hardlight voxels fill each crater cell first; the cells are the ones the wounds took out, where the cut lays them on that skin (`NativeWoundSkin.Cut(..., holes)`, `PrototypeHoles`).
- **Missing parts.** A gibbed part dissolves where it lay, and its voxels fly back to the joint. It then re-forms as a wireframe (feature edges), a hologram, then its skin.
- **Split bodies.** A body cut in two (the bisect) floats back together first. Kept PvE bodies are never split today, so in play only the Sandbox preview shows this.
- **Blood wash.** The blood and ink within 1.5 m fade from 0.22 s.
- **Ring and column.** A floor ring, a disc under the floating body, a column with rising data streaks and a landing pulse. They linger 0.32 s after the body lands.
- **First person.** The view rises gently (at most 0.18 m) inside a scanline vignette. With reduced effects or camera shake off the view stays still. A first-person viewer never stands in a column.
- **Unwounded revives.** They get the lift, the ring and column and the band only.

## Code

| File | Role |
|---|---|
| `Core/NativeReviveHolo.cs` | Timeline (`Lift`, `ViewLift`, `Vignette`, `Aura`); repair curves on the C17 front (`Arrive`, `Heal`, `Wire`, `Fill`, `Solid`, `Band`); hardlight pieces (`Crater`, `Halo`, into caller arrays); `Edges` (wireframes) |
| `Core/NativeWoundSkin.cs` | The optional `holes` recorder on `Cut` (recording changes no cut); `PrototypeHoles` |
| `Core/NativeBlood.cs` | `NativeBloodSplats.FadeNear` |
| `Runtime/NativeReviveHoloPresentation.cs` | `BeginHolo` (once per revive: models, holes, cached delegates), `RenderRevival`, `ReviveLinger`, `ReviveLift`, `ReviveView` (first person), the Sandbox preview |
| `Runtime/NativeReviveModels.cs` | `QueueHolo` (any model as hardlight or its wireframe), `ReviveGlow` (ring and column) |
| `Resources/NativeReviveHolo.shader`, `NativeReviveScreen.shader` | The additive hardlight and the vignette; compiled by the Editor build gate |
| `Runtime/NativeWoundGeometry.cs` | Each pilot keeps two carves (its live body and the body it left), so a revive no longer recuts every frame against its corpse; a kept carve is found without allocating |

## Settings, preview and checks

- **Tiers.** Crater voxels 160/64/24 and streaks 12/6/0 for Full, Reduced (reduced effects) and Low (low graphics); Low draws no wireframe. Without the hardlight shader the C17 voxel boxes and floor frame draw as before.
- **Dev Sandbox: "Preview Revive".** A stand-in of you, 3.2 m ahead, lies headshot-wounded, heavily wounded, gibbed or split. It is revived on a loop, cycling the three skins.
- **Checks: `Tests/ReviveHoloChecks.cs`.** They cover the lift window and exact landing, the repair finishing by the scan's end, the crater cells lying on each skin (recording changing no cut), tier budgets, determinism, no allocation per frame, the wireframe edges, the wash and the wiring.
