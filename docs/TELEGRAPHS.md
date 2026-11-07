# Attack warnings (telegraphs)

Owner, 2026-11-06: "We need better visual indicators of his attacks [the circle/lines that appear where an attack is coming]. Honestly all bosses and enemies need better visual indicators. I want something that players can see and it's not a basic design."

One shared renderer draws every boss's and enemy's ground warnings. It is presentation only: each warning is the attack's own geometry and timing from the rules (Astra's), drawn better. Nothing here is read by a rule.

| Part | Where |
| --- | --- |
| Shapes, looks, timing, extents, the frame's list | Core `NativeTelegraph.cs` |
| The renderer (one quad per warning, one mesh per frame) | Runtime `NativeTelegraphPresentation.cs` |
| The look, per pixel | `Resources/NativeTelegraph.shader` |
| Checks | `Tests/TelegraphChecks.cs` (`--telegraphs`) |

## What a warning shows

- **Where:** the shape on the floor, with a crisp edge never thinner than about a pixel and a half at any distance, a glow outside it, and a dark film under it, so a bright warning reads on a bright floor.
- **When:** the fill closes in toward the hit and is full on it, with a bright front line where it is. The edge's pulse quickens as the hit nears. In the last 0.28 s (`LockSeconds`) it strobes: the lock-on.
- **The hit:** a white flash over the shape and a shock ring running out from its edge for 0.35 s (`FlashSeconds`). A hazard that keeps hurting after its hit (Feather Rain's lanes) stays **live**: a steady burn, then it fades.
- **Whose:** a look per boss family, so a player knows what is coming before reading the shape.

## Shapes

All in the warning's own frame: x its right, y its front (the game's yaw: 0 faces +z, 90 faces +x).

| Shape | A | B | C |
| --- | --- | --- | --- |
| `Circle` | radius | | |
| `Ring` | outer radius | inner radius | |
| `Cone` | radius | half angle (degrees) | inner radius |
| `Lane` | length forward | half width | length behind |
| `Square` | half size | | |
| `Wave` (a travelling front) | radius now | half thickness | half angle (degrees) |

The fill comes `FromEdge` (closing in), `FromCentre` (a blast spreading) or `Along` the attack (a swing or a dash).

## Looks

| Look | For | Pattern |
| --- | --- | --- |
| `Danger` | the default, enemies | hot red-orange, chevrons running toward the hit |
| `Fallen` | NOCTIEL | white on black, engraved rings drifting in, a dial of ticks |
| `FallenKill` | NOCTIEL's one-hit kills | a deeper black film, white spokes, a harder pulse |
| `Safe` | a safe place (the Eclipse's pools) | soft, breathing, no film |
| `Industrial` | the Station, the Ironworks | amber hazard stripes |
| `Molten` | heat attacks | molten shimmer |
| `Fluorescent` | THE HUM | a fluorescent buzz that flickers, a ceiling-tile grid |
| `Water` | THE DEEP END | water light, caustics |
| `Siren` | the Siren | magenta chevrons |

## Adopting it

A presenter fills a `NativeTelegraph.Spec` with the attack's shape (its rule's own numbers), calls `NativeTelegraph.Timed(ref spec, start, hit, now, liveSeconds)` and, when it returns true, `Telegraph(spec)` (or `Telegraph(spec, stageOrigin)` for a stage drawn away from the level). It then removes its old warning lines and keeps its attack effects. The list is cleared before the world draws and drawn after its effects (`RenderAbilities`).

Checks make each adopter honest: sample the floor, and every point the rule would hit must be inside a warning, which may be slightly larger (a lane's square ends) but never smaller.

## Adopted

- **NOCTIEL** (2026-11-06), each warning against `NativeFallenAuthority.FallenAttack`:
  - Cleave's 151 degree fan (8 m, its 0.25 dot), live through the three cuts.
  - Vow's lane, 3 m either side of the dash and 3 m past each end.
  - Severance's fan (a 0.5 dot), then each wave's front as it travels.
  - Descent's 9 m circle where it lands.
  - Feather Rain's eight lanes, live while the blades fall.
  - The Verdicts' 3 m lines through the target (three for Last Verdict).
  - Wing Sweep's 28 m circle.
  - The Eclipse's safe pools.
  - Fall of Light's falling column cells.
  - A 3 m Hush circle filling under any pilot standing still.

  The old warning lines and squares are gone. The crack, the dark, the blades, the columns and the impacts stay as the attacks' own effects.
- **THE HUM** (2026-11-07, `Fluorescent`), each against `NativeBackroomsBossRules.Contacts`:
  - The rows' 0.7 m lanes, live for the burst's 0.1 s.
  - Noclip's capsule (1.5 m round the dash) and phase 3's return.
  - Reach's 6 m, 130 degree arc on its aim (the view now carries the replicated `actionYaw`).
  - Stoop's 4.5 m; Lights Out's 3.5 m half circle, shown only in the tubes' last 0.25 s.
  - Fall Through's 2.4 m; Max Hum's charge, then its travelling front.
  - Wall Hand's 2.5 m round the wall point (the old ring was 1.3 m off it and smaller).
- **THE DEEP END** (2026-11-07, `Water`, on the pool's surface):
  - Breach's 3.2 m; Belly Flop's lane (2 m either side); Thrash's 5 m; the Lull's 1.8 m; the Drain's 2.6 m.
  - The Seiche's front, crossing the pool and back.
  - The Hydro Jet's lane from the mouth, following the sweep.
  - Last Lap's body where it breaks the surface.
- **THE 6:47** (the Station, 2026-11-07, `Industrial`), each against the zone boss authority:
  - Buffer Swipe's and Shunt's arcs (new: they had none).
  - Departure's run, 2.1 m round the body (the old lane was 1.6 m), and phase 3's return.
  - All Aboard's box by the held door; the panes' 1.4 m.
  - Live Rail's 2.2 m round every puddle while live.
  - Terminus's slam, the ground it rolls over, and its front.
- **THE CONVERTER** (the Ironworks, 2026-11-07, `Molten`):
  - Pour's fan, 1.2 to 8 m.
  - Stomp's 1.9 m and Grab's 1.2 m.
  - Slam's ground and front, then the box ahead.
  - Charge's 3.2 m down the run.
  - Every hazard pool (pour, blow drops, deslag patches, the charge's trail), filling to its hit and live while it burns.
- **The Siren's kit** (a pilot's, or the horde Siren's through her possessed add; 2026-11-07, `Siren`):
  - The eruption, filling to its burst.
  - Black Rain's radius while the drops fall.
  - Each fire patch's burn (softer: there can be many).

  Each boss's old rings, strips, wedges and dashes are gone. The attacks' own effects stay (the scorch, bursts and tears, the water's darkening, the Converter's shock wall, the horns' rings). A move the authority calls off takes its warning with it. `BossVfxRealismChecks`' telegraph baselines for the 6:47 and the Converter were re-recorded on purpose; their cues are unchanged.
- **Next:** the enemies (the horde's kinds, the Hulk's charge and jump-down, the Ironworks' and the Station's kinds), and the stages' own hazard warnings.
