# Chip Arena — playable prototype

An original, fully 3D, voxel-styled 1v1 arena combat game. This is a new project, with no Embertrail assets, repository, database, or deployment dependencies.

**Publicly hosted multiplayer is not included.** The public hosted client runs local practice in your browser with no ChatGPT login or game account. Online rooms require this project's persistent Node.js server. The same authoritative Rapier simulation powers both modes.

## Run it

Requires Node.js 22.14+ (tested with Node 24.19.0) and npm.

```sh
npm ci
npm run dev
```

Open `http://localhost:4173`. The development command starts the browser client and multiplayer server together. Open two independent browser windows, create a room in one, and join its code in the other. The development proxy uses `ws://localhost:4173/socket`.

To run the components separately:

```sh
# Terminal 1
npm run server
# Terminal 2 (client only)
npx vite --host 0.0.0.0 --port 4173
```

To build and serve production locally:

```sh
npm run build
npm run server:build
npm start
```

Open `http://localhost:8788`, with server URL `ws://localhost:8788/socket`.

`npm start` serves `dist` and the persistent authoritative WebSocket endpoint from one process. No accounts, secrets, paid services, or database are needed. The example `.env.example` documents server and client settings. Copy it to `.env` and use `node --env-file=.env server-dist/server/index.js` if you want Node to load that file explicitly.

## Play

Main menu → Play vs Bot → class and difficulty → specialization → fight → Chip draft → next round. First to five wins. A draw replays the round with no Chip. Rematch resets scores and Chips and returns to specialization selection. The default loser-only drafting rule can be changed to both players in the lobby.

Click the arena to capture the mouse. `Escape` releases it and opens the match menu. Local practice pauses in that menu; online matches continue.

| Control | Action |
|---|---|
| WASD | Move relative to camera direction |
| Mouse | Aim and camera |
| Left mouse | Primary attack |
| Right mouse | Specialization secondary; hold for Shield guard |
| Space | Jump |
| Shift | Dash |
| R | Reload when applicable |
| V | Toggle first / third person (saved locally) |
| M | Mute / unmute sound |
| Escape | Menu / release pointer lock |

Designed for a desktop mouse and keyboard. Menus adapt to small displays, but touch combat controls are not implemented.

## Implemented content

- Pistol, Rifle, Machine Gun; Fire, Water, Lightning, Darkness; Sword, Daggers, Shield, Greatsword.
- Twenty-six working Chip definitions with compatibility filters, per-Chip stack caps, descriptions and numerical effects. No global equipment-slot limit.
- Stationary dummy plus Easy, Normal and Hard bots, with no damage bonuses. Bots submit the same input structure as human clients.
- One compact arena with durable foundations, elevation, breakable wall sections, loose crates, dense metal boxes and a supported stone beam.
- Actual dynamic debris, support collapse, mass-conserving fracture, CCD and physical impact damage using pre-solver point velocities.
- Private rooms, two ready states, synchronized choices, authoritative match state, 12-second disconnect forfeits, rematch votes and return to lobby.
- Persistent local name/class/color/settings preferences.
- Developer sandbox with specialization, Chip add/remove, dummy/bot changes, healing, reset, collider/support display, launcher and impact diagnostics.

## Weapon resources and modifiers

Guns use ammo and reloads, magic uses mana, and melee uses stamina. Machine Gun has a 48-round base magazine and a 2.4-second reload; its secondary consumes six rounds for a suppression burst. There is no heat or overheating system.

Magic starts at 100 mana and regenerates 12/s. Melee starts at 100 stamina and regenerates 24/s after 0.45 seconds without spending stamina. Sword/Daggers/Shield/Greatsword attacks cost 14/7/18/26 stamina; their parry/sidestep/guard/charge uses the same pool. Shield drains 33/s while held and 0.9 stamina per incoming frontal damage point. Other movement uses the existing movement and dash cooldown rules.

| Modifier | Guns | Magic | Melee |
|---|---|---|---|
| Extra attacks | Parallel Shot: +1 projectile/stack, max +3 | Parallel Shot | Echo Strike: +1 follow-up sweep/stack, max 2 |
| Bounces | Ricochet: +1/stack, max 3, 70% retained damage | Ricochet | Not offered |
| Capacity | Magazine Bank: +25% base magazine/stack, rounded up | Mana Reservoir: +25 mana/stack | Stamina Cell: +25 stamina/stack |
| Projectile speed | Velocity Coil: ×1.25/stack, max 4 | Velocity Coil | Not offered |
| Homing | Tracking Circuit: +35°/s/stack, max 105°/s | Tracking Circuit | Not offered |
| Attack rate | Rapid Cycle: ×1.15/stack | Spell Clock: ×1.15/stack | Flurry Clock: ×1.15/stack |

Parallel Shot costs one ammo or the normal spell cost per volley. With it installed, every projectile in that volley deals 75% damage across a 7° fan. Echo Strike costs five extra stamina per follow-up sweep, each at 45% damage/impulse; each sweep can hit each target once, and the combo extends recovery. Homing requires a visible target within 16 m and a 50° forward cone; it cannot pass through cover. Projectile speed upgrades flatten the trajectory and extend effective range before landing. Split fragments never home or recursively trigger effects. Capacity Chips cap at eight stacks; rate Chips cap at six and multiply with Overclock. Installed effects persist only for the match.

Drafting and the sandbox Chip picker filter by the equipped weapon. The HUD shows current and upgraded maximum ammo, mana, or stamina. Every Chip describes its exact effects and cap. In the sandbox, installing/removing a Chip refills resources for immediate comparison; normal drafts refill them at the next round reset.

## Fire and poison enchantments

Both elements work with every gun and melee specialization, including Shield, and also support magic builds. They can be installed together. There are no element equipment slots.

| Chip | Added damage per installation | Duration | Maximum stacks | Visual |
|---|---:|---:|---:|---|
| Ember Circuit (`ember`) | 3 burn DPS; 6 total before mitigation | 2 seconds | 6 | Orange flame/ember bullets and animated blade-edge flames |
| Venom Circuit (`poison`) | 2.5 poison DPS; 10 total before mitigation | 4 seconds | 6 | Green toxic bullets and pulsing green blade enchantment |

Eligible direct hits apply these statuses. A repeated hit refreshes that element's duration and retains its strongest DPS per source; it never delays the existing 0.25-second tick cadence. Installation stacks add DPS, not duration. Fire and poison tick independently and can kill, with attacker attribution. General incoming-damage reduction applies to their ticks; directional guarding does not remove an already applied status. Burning and Poisoned badges show the remaining duration and DPS on the HUD, with matching particles on affected players. The Fire specialization's native 4 burn DPS adds to Ember Circuit.

DoT ticks, explosions, discharges and split secondary projectiles cannot apply another enchantment or trigger direct-hit effects. Primary ricochets/piercing remain eligible direct attacks. Round resets and Sandbox Restore clear both statuses. Projectile element flags and victim statuses come from the authoritative simulation; cosmetic effects never decide damage.

## Camera, projectile visibility and sound

Use **V** for first person or third person, or choose a saved default in Settings. First person hides your full body and uses the same weapon socket geometry for visible hands/weapons, recoil, reload and melee motion. The server accepts only a camera-mode intention, reconstructs the eye/camera and physical muzzle itself, and checks muzzle obstruction. Both modes retain ballistic drop and cover collision.

Projectiles have bright cores and short, depth-tested ribbons following observed flight positions. Online projectiles interpolate between authoritative snapshots. Trails expire after 0.18 seconds and are bounded to 120 ribbons with 12 points each; they never add colliders. Toggle Projectile trails or reduce Effects in Settings. Hit confirmation briefly colors the crosshair.

Thirty original synthesized effects cover distinct weapons, spells, melee, reloads, hits, parries, elemental ticks, destruction, movement, installation and round cues. Audio uses distance attenuation and stereo panning, a 24-voice cap, master compression, volume and mute controls. **M** toggles mute. Settings → **Test Sound** unlocks audio after a browser gesture and reports loaded effects. No microphone, account or external audio service is used. Browser autoplay rules require an initial click.

Editable generation source: `scripts/generate-audio.mjs`; regenerate with `npm run audio:generate`. Bundled WAVs live in `public/audio`. The source archive includes both. Audio events are deduplicated by authoritative IDs; historical events older than 0.4 seconds are skipped.

## Projectile drop

Every gun bullet and magic projectile accelerates downward. Launch speed and gravity are separate weapon settings. Velocity Coil multiplies launch speed by 1.25 per stack; it does not cancel gravity. At the same horizontal distance, a flat shot with one Coil has 36% less gravitational drop. Aiming higher allows longer arcs. Damage does not decrease solely because of distance.

Measured flat-shot landing distances from a projectile center 1.6 m above a level test floor (no homing, no ricochet, no cover):

| Weapon | Base speed (m/s) | Gravity (m/s²) | Base landing distance | One Velocity Coil |
|---|---:|---:|---:|---:|
| Pistol | 52 | 32 | 16.19 m | 20.25 m |
| Rifle | 60 | 30 | 19.30 m | 24.11 m |
| Machine Gun | 49 | 36 | 14.38 m | 17.98 m |
| Fire | 24 | 12 | 11.84 m | 14.76 m |
| Water | 31 | 16 | 13.24 m | 16.51 m |
| Lightning | 85 | 40 | 22.93 m | 28.68 m |
| Darkness | 15 | 5 | 11.43 m | 14.28 m |

These are prototype settings and controlled test measurements, not guaranteed range from every arena position. Projectile radius, elevation, pitch, cover, bounces and homing change landing distance. The slower secondary fireball uses 78% of Fire's launch speed and the same gravity. Split projectiles inherit their parent's gravity. Ricochets reflect contact velocity and continue falling; homing steers within its existing limit before gravity is applied each tick.

The shared authoritative simulation integrates each ballistic step and sweeps its complete displacement against collision geometry at 60 Hz. The renderer follows those positions and velocities. Bots compensate for drop through ordinary aim inputs, while retaining reaction delays and aim error. Primary projectiles are bounded by a 3-second lifetime and 160 m travel budget; split shots by 1.2 seconds and 18 m. Those are cleanup limits, not the old weapon-specific flat-flight range cutoff. `Weapon.range` remains the aiming-ray distance (and melee reach for melee weapons).

Tune per-weapon `speed` and `gravity` in `src/shared/content.ts`; flight budgets in `src/shared/config.ts`; integration/aim helpers in `src/shared/projectiles.ts`.

## The defining physics demonstration

1. Open **Developer Sandbox**. The target waits on the far spawn.
2. Set mass to `8`, speed to `15`, material to `wood`, mode to `Direct`.
3. Launch. Observe the physical chunk travel and the target's health change. The impact panel shows mass, pre-impact normal speed, energy, damage and source.
4. Restore health and reset the arena before comparisons. Compare `8 kg / 8 m/s` with `16 kg / 8 m/s`, then `8 kg / 12 m/s`. These values avoid the per-impact cap in many contacts.
5. Compare Direct, Glancing, Falling and Settled. Settled contact should not repeatedly hurt the target. Falling debris has zero launch velocity and gains speed from gravity.
6. In ordinary combat, hit a loose crate to launch it; destroy the center wall to open a real firing line. Destroy **both** orange wooden supports under the gray beam. The beam becomes dynamic and falls/tips as its support fragments move away.

The launcher creates a chunk five meters in front of the target for repeatable measurements. It is a test fixture, not a combat ability. Regular combat can damage either player with debris, including the launcher.

For a two-client authoritative launcher test, start the server with `ALLOW_TEST_ROOMS=true` and run `tests/debris-network.ts`. Only the creating player in an explicitly requested test room can send debug mutations. A normal room rejects those commands, regardless of UI visibility.

## Edit the balance and content

| File | Settings and responsibilities |
|---|---|
| `src/shared/config.ts` | Match timing, tick/snapshot rates, movement, impact energy thresholds, resource regeneration, modifier tuning, limits, materials |
| `src/shared/content.ts` | All 11 weapons and 26 Chips; compatibility, stacking and stat effect handlers |
| `src/shared/arena.ts` | Authored collision blocks, spawn routes, supports and materials |
| `src/shared/elements.ts` | Fire/poison duration, cadence, color, per-stack DPS and refresh handlers |
| `src/shared/view.ts` | Shared eye/camera/muzzle offsets, camera collision and FOV |
| `src/client/audio.ts` / `scripts/generate-audio.mjs` | Sound mapping, audio mix, voice budget and editable synthesized assets |
| `src/client/projectile-trails.ts` | Cosmetic trail duration, width, sampling and budget |
| `src/shared/projectiles.ts` | Ballistic flight, low-arc bot aiming, and bounded homing rotation |
| `src/shared/impact.ts` | Point velocity, normal closing speed, energy/damage formula, impact deduplication and fragment budget |
| `src/shared/simulation.ts` | Authoritative movement, attacks, statuses, projectiles, bots, destruction, round progression |
| `src/server/index.ts` | Private room protocol, input validation, message limits, fixed-step loop and debug authorization |
| `src/client/prediction.ts` | Collision-aware movement prediction, pending input replay and reconciliation |
| `src/client/renderer.ts` | Three.js voxel rigs, sockets, weapon animation, camera collision, effects and rendering |
| `src/client/main.ts` / `style.css` | UI, controls, network client, interpolation, audio and preferences |

## Architecture and deliberate prototype decisions

The server advances at 60 Hz and sends full snapshots at 20 Hz. Clients send movement/aim/button intentions and selections. They never supply authoritative health, damage, grants, projectile hits or final body positions. Bots run in the simulation. Local practice instantiates the same simulation directly.

Players are rotation-locked dynamic capsules. Input movement and external knockback are stored separately. The physics solver handles debris collision impulses; debris damage does not add a second launch impulse. Attack impulses use the external velocity component. Melee uses an angularly swept segment/volume during a defined attack window and remembers already-hit target IDs. Projectiles use swept spheres and a server-reconstructed camera-to-target, body-to-muzzle obstruction check.

Destruction uses four authored cuboid fragments, dividing parent mass and inheriting linear and angular motion. A small, symmetric fracture separation velocity is an intentional arcade effect. Static durability and finite mass are independent. The bridge attachment fails only when both explicit supports fail; this is not a general structural engineering solver.

Energy is `0.5 * finiteMass * normalClosingSpeed^2`, with minimum speed 3.6 m/s, safe energy 45 J, scale 0.035, and maximum 65 damage per impact. Pair separation plus a 0.4-second cooldown prevents resting or duplicated contacts from damaging every step. A shared destruction event is capped at 75 pre-mitigation damage per target over 0.35 seconds. Cosmetic particles have no collider or damage authority. Source attribution expires after seven seconds.

Secondary projectiles, burns, poison, blasts and electrical discharges cannot recursively fire direct-hit effects. Splitter is bounded; a projectile volley or complete melee combo only procs Shrapnel once. Burn refreshes for two seconds and poison for four seconds per source, preserving their tick cadence without stacking duration.

No rewind / lag compensation is implemented. Online hits use present server state. Client prediction uses a cheaper kinematic collision model, so interactions with moving debris, ramps and knockback can need correction. This is a prototype limitation, not lockstep or deterministic client physics. Remote players, projectiles and debris interpolate over about 100 ms.

## Test and deployment guides

See [docs/TESTING.md](docs/TESTING.md), [docs/TEST-RESULTS.md](docs/TEST-RESULTS.md), and [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md). Run `npm test` for the automated calculation, combat, destruction and match-state suite. `npm run test:network:local` starts and tests a temporary backend automatically; `npm run test:debris` does the same for the impact assertion. `npm run test:stress` runs 15 combined-Chip stress cases. Every production client build also produces `dist/chip-arena-source.zip`.

## Current limits

- No publicly deployed persistent backend, accounts, ranked matchmaking, persistence, room recovery or reconnection tokens.
- Full-state JSON networking is simple and comparatively bandwidth-heavy. One Node process owns its rooms; multi-instance routing is not implemented.
- Art, sound and secondary VFX are prototype quality. Water and darkness use basic particles/pulses; they do not simulate fluid or volumetric smoke.
- Bot navigation is heuristic, not a navmesh. Debris piles can cause temporary blocking.
- WebGL hardware performance must be measured on a real desktop GPU. The automated browser environment has WebGL disabled.
- If WebGL fails, an SVG-based **3D** compatibility renderer uses the same Three.js scene/camera and simulation. It has visible painter-order artifacts and lower performance; it is not the intended high-quality rendering path.
- No mobile touch gameplay support.
- WebMCP registration is feature-detected; browser-context validation was unavailable in this environment.

Official API references verified during implementation: [Rapier rigid bodies and CCD](https://rapier.rs/docs/user_guides/javascript/rigid_bodies/), [Rapier RigidBody API](https://rapier.rs/javascript3d/classes/RigidBody.html), [Three.js Raycaster](https://threejs.org/docs/pages/Raycaster.html), [ws server examples](https://github.com/websockets/ws).
