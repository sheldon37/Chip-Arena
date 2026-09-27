# Chip Arena — playable prototype

An original, fully 3D, voxel-styled 1v1 arena combat game. This is a new project, with no Embertrail assets, repository, database, or deployment dependencies.

The public client supports local practice and **browser-hosted rooms through a same-origin HTTPS relay**, without a ChatGPT login or game account. Create Room makes you the host; guests enter an eight-character code or use an invite link. No server address is needed in the normal flow. The creator runs the authoritative simulation in a Web Worker, while the guest sends validated input intentions through the website’s own HTTPS room service.

The previous direct PeerJS connection is replaced. The new transport follows the useful part of Embertrail invasion’s approach: normal HTTPS keeps the session connected even on networks that cannot establish peer data channels. It uses only Chip Arena’s own code, server and database. Local/supervised verification and public deployment verification are distinguished in [test results](docs/TEST-RESULTS.md).

## Run it

Requires Node.js 22.14+ (tested with Node 24.19.0) and npm.

```sh
npm ci
npm run dev
```

Open `http://localhost:4173`. The development command starts the browser client and multiplayer server together. Open two independent browser windows, create a room in one, and join its code in the other. Default rooms use browser hosting. To exercise the dedicated server, enable Settings → Advanced connection settings → Use a dedicated server, with `ws://localhost:4173/socket`. The development proxy forwards that path.

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

`npm start` serves `dist/client`, a local SQLite-backed room relay, and the persistent authoritative WebSocket endpoint from one process. No accounts, secrets or paid services are needed for local development; SQLite room storage initializes from the included migrations. The example `.env.example` documents server and client settings. Copy it to `.env` and use `node --env-file=.env server-dist/server/index.js` if you want Node to load that file explicitly.

## Play

Main menu → Play vs Bot → class and difficulty → specialization → fight through a set → Chip draft → next set. A player must win **two rounds to win a set** (2–0 or 2–1). The match is first to **five set wins**. The set loser drafts one Chip only after that second round win; there is no draft after the first win or the final set. A draw replays the round without changing either score or awarding a Chip. Rematch resets set/round scores and Chips and returns to specialization selection. The lobby can instead let both players draft after each nonfinal set. Tune `CONFIG.wins` (sets) and `CONFIG.roundsPerSet` centrally in `src/shared/config.ts`.

Click the arena to capture the mouse. `Escape` releases it and opens the match menu. Local practice pauses in that menu; online matches continue.

| Control | Action |
|---|---|
| WASD | Move relative to camera direction |
| Mouse | Aim and camera |
| Left mouse | Primary attack |
| Right mouse | Timed Block: reflect projectiles / negate melee |
| E | Specialization secondary; hold for Shield guard |
| Space | Jump |
| Shift | Dash |
| R | Reload when applicable |
| V | Toggle first / third person (saved locally) |
| M | Mute / unmute sound |
| Hold Tab | Player board; hover Chips for effects and stacked values |
| Escape | Menu / release pointer lock |

The player board shows both players’ specialization, installed Chips, duplicate counts, health, round progress and set scores. Hold Tab to release the pointer and inspect; release it to return. The **Builds TAB** button also opens a persistent board, with an × close button. Matches continue while inspecting; movement and attack input stop while the board is open. Click the arena if the browser requires a gesture to recapture the mouse.

Duplicate Chips remain eligible in future drafts until their individual stack cap. Each copy modifies the build; the board reports combined numerical effects for that Chip (additive or multiplicative as defined), alongside its cap. For example, two Ember Circuits add 6 burn DPS and two Venom Circuits add 5 poison DPS. There is no global slot limit.

Designed for a desktop mouse and keyboard. Menus adapt to small displays, but touch combat controls are not implemented.

## Implemented content

- Pistol, Rifle, Machine Gun; Fire, Water, Lightning, Darkness; Sword, Daggers, Shield, Greatsword.
- Thirty working Chip definitions with compatibility filters, per-Chip stack caps, descriptions and numerical effects. No global equipment-slot limit.
- Stationary dummy plus Easy, Normal and Hard bots, with no damage bonuses. Bots submit the same input structure as human clients.
- One compact arena with durable foundations, elevation, breakable wall sections, loose crates, dense metal boxes and a supported stone beam.
- Actual dynamic debris, support collapse, mass-conserving fracture, CCD and physical impact damage using pre-solver point velocities.
- Private rooms, two ready states, synchronized choices, authoritative match state, 12-second disconnect forfeits, rematch votes and return to lobby.
- Persistent local name/class/color/settings preferences.
- Developer sandbox with specialization, Chip add/remove, dummy/bot changes, healing, reset, collider/support display, launcher and impact diagnostics.

## Timed Block

Every specialization has the same right-click Block: **0.30 s active, 4.00 s cooldown measured from activation**. The cooldown is fixed and does not scale with weapon, fire rate, Overclock, mana, stamina or movement upgrades. Only explicit Block cooldown modifiers change it. Each press activates once; holding does not auto-repeat. A quick tap is latched for at least one input tick. Blocking cancels pending swing/burst attacks, prevents attacks during its active window, and leaves ordinary movement available. It has no ammo/mana/stamina cost. Existing secondary actions are on **E**.

The short window protects from all directions. An incoming projectile reverses its pre-impact flight velocity, changes responsibility to the blocker, and can hit its original shooter. It retains original damage, gravity, remaining lifetime/travel, elemental payload and an unused native blast. Reflections do not borrow the blocker’s weapon effects, trigger direct-hit counters, split, home or generate Shrapnel. A projectile may reflect three times; the next successful Block absorbs it. Reflections cap at 100 direct damage and 120 m/s. Swept collision and cover remain authoritative.

A correctly timed melee hit is fully negated, including knockback, burn/poison application and direct-hit triggers. The swing still records that contact, so it cannot retry every frame. Existing burns, poison, falling, debris impacts and area damage are not negated. Both players’ Block activation is processed before any attack resolution in that tick. There is no lag-compensated Block rewind; online timing follows host/server time.

| Block Chip | Per-copy effect | Cap |
|---|---|---:|
| Rebound Capacitor | Cooldown ×0.85; 2 copies = 2.89 s | 5 |
| Phase Buffer | Active window +0.08 s; 2 copies = 0.46 s | 4 |
| Return Amplifier | Reflected damage ×1.15 and speed ×1.10 | 4 |
| Counter Pulse | First success per activation: 3 m pulse, +6 base damage and +120 impulse, with distance falloff and cover | 4 |

All four are offered to every class and stack explicitly. Counter Pulse affects opponents and loose props; it cannot apply DoTs or recursively trigger Chips. Even maximum duration/cooldown stacking leaves a vulnerability gap: 0.62 s active per 1.775 s cooldown. The HUD shows Block readiness, active state and recharge. Other players see rotating mint-colored rings; first person shows a defensive hand pose and an open diamond around the crosshair. The parry sound differentiates activation and successful impact by gain. Developer Sandbox includes **Test Block**, using ordinary input intentions.

## Room codes and browser hosting

1. Create Room → choose class → Create Room. Keep this page open.
2. Copy the code or invite link; the link pre-fills the guest’s code. A selectable link field remains available if clipboard access is denied.
3. The guest opens the website, chooses Join Room and enters the code, or opens the invite directly.
4. Both ready, then choose specializations. The creator remains HOST and the other player GUEST.

The creator’s worker runs shared Rapier physics at 60 Hz and produces snapshots at 20 Hz. HTTPS exchanges target 50 ms and coalesce snapshots; effective guest delivery depends on network/D1 latency. Guest movement prediction/reconciliation and remote interpolation use the same existing client path. The host accepts only input, ready/class/spec/draft selections, ping, rematch votes and match-end lobby requests. No browser-hosted room permits gameplay debug mutations, including by the host through the protocol. Names/classes/colors, finite inputs, sequence numbers, payload sizes and message rates are validated.

This is **trusted-host authority**, not an independent anti-cheat server: a modified host can cheat. A closed host closes the room; host migration/reconnection are not implemented. A disconnected guest forfeits after 12 simulation seconds. A stalled host closes the guest’s UI session after 12 seconds without snapshots rather than inventing a new authoritative match result. Host hardware, background throttling and network conditions affect the match.

The website relays gameplay over normal HTTPS. No PeerJS, STUN, TURN, webcam or microphone permission is required. Room registration and atomic guest joining use a separate Chip Arena D1 database. Each seat has a random token kept in memory; the service stores its hash. The guest cannot publish authoritative snapshots or issue debug mutations. Selection messages use IDs/acknowledgements; retries do not apply them twice. Movement is coalesced while preserving brief action taps. Only one request is in flight per client, with bounded queues, bodies and payload sizes. Brief outages retry automatically; a prolonged outage closes the session clearly. Authenticated exchanges now validate and update a seat in one database batch. Browser-hosted input intentions hold for up to 0.75 seconds before stopping, so ordinary relay jitter does not repeatedly cancel movement. The dedicated server keeps its 0.3-second stale-input cutoff. Guest collision capsules update every prediction/replay step; locomotion and external velocity are restored separately during reconciliation.

A guest who leaves cannot be replaced within the same HTTPS room; create a fresh room for another guest. Room lifetime is two hours. HTTPS polling improves connection reliability but has more latency than WebSockets. The dedicated server path remains in Advanced settings for independently authoritative deployments. All relay settings live in `src/shared/relay-protocol.ts`.

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

Eligible direct hits apply these statuses. A repeated hit refreshes that element's duration and retains its strongest DPS per source; it never delays the existing 0.25-second tick cadence. Installation stacks add DPS, not duration. Fire and poison tick independently and can kill, with attacker attribution. General incoming-damage reduction applies to their ticks; directional guarding does not remove an already applied status. Burning and Poisoned badges show the remaining duration and DPS on the HUD, with distinct effects on affected players: layered orange/yellow body flames and rising embers while burning, and translucent green bubbles that rise and pop while poisoned. Both effects coexist, follow the character and disappear when the status expires, the player dies, or the round resets. In first person, matching effects appear around your hands plus subtle screen-edge cues; they do not cover the crosshair. Full/reduced quality uses bounded, reusable cosmetic meshes without damage colliders. The Fire specialization's native 4 burn DPS adds to Ember Circuit.

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
| `src/shared/blocking.ts` | Block duration/cooldown helpers; numerical tuning is centralized in config and Chip definitions |
| `src/shared/host-room.ts` | Browser-hosted room rules, input/selection validation, rematch votes and disconnects |
| `src/client/browser-room.ts` / `host-worker.ts` | HTTPS relay adapter, host simulation worker, timeout and cleanup |
| `src/shared/content.ts` | All 11 weapons and 30 Chips; compatibility, stacking and stat effect handlers |
| `src/shared/arena.ts` | Authored collision blocks, spawn routes, supports and materials |
| `src/shared/elements.ts` | Fire/poison duration, cadence, color, per-stack DPS and refresh handlers |
| `src/shared/view.ts` | Shared eye/camera/muzzle offsets, camera collision and FOV |
| `src/client/audio.ts` / `scripts/generate-audio.mjs` | Sound mapping, audio mix, voice budget and editable synthesized assets |
| `src/client/status-effects.ts` | Authoritative-status-driven body flames, poison bubbles, first-person cues and fixed mesh budgets |
| `src/client/player-board.ts` / `src/shared/chip-inspection.ts` | Player board, duplicate counts and numerical stack tooltips |
| `src/client/projectile-trails.ts` | Cosmetic trail duration, width, sampling and budget |
| `src/shared/projectiles.ts` | Ballistic flight, low-arc bot aiming, and bounded homing rotation |
| `src/shared/impact.ts` | Point velocity, normal closing speed, energy/damage formula, impact deduplication and fragment budget |
| `src/shared/simulation.ts` | Authoritative movement, attacks, statuses, projectiles, bots, destruction, round/set progression |
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

See [docs/TESTING.md](docs/TESTING.md), [docs/TEST-RESULTS.md](docs/TEST-RESULTS.md), and [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md). Run `npm test` for the automated calculation, combat, destruction and match-state suite. `npm run test:network:local` starts and tests a temporary backend automatically; `npm run test:debris` does the same for the impact assertion. `npm run test:stress` runs 15 combined-Chip stress cases. Every production build also produces `dist/client/chip-arena-source.zip`. `npm run test:https` exercises two real HTTP clients through the shared relay handler.

## Current limits

- Browser-hosted rooms use HTTPS and trusted-host authority. No independent dedicated backend is publicly deployed; no accounts, ranked matchmaking, host migration, refresh recovery, or reconnection tokens. Public internet match validation is separate from the local/supervised tests.
- Full-state JSON networking is simple and comparatively bandwidth-heavy. One Node process owns its rooms; multi-instance routing is not implemented.
- Art, sound and secondary VFX are prototype quality. Water and darkness use basic particles/pulses; they do not simulate fluid or volumetric smoke.
- Bot navigation is heuristic, not a navmesh. Debris piles can cause temporary blocking.
- WebGL hardware performance must be measured on a real desktop GPU. The automated browser environment has WebGL disabled.
- If WebGL fails, an SVG-based **3D** compatibility renderer uses the same Three.js scene/camera and simulation. It has visible painter-order artifacts and lower performance; it is not the intended high-quality rendering path.
- No mobile touch gameplay support.
- WebMCP registration is feature-detected; browser-context validation was unavailable in this environment.

Official API references verified during implementation: [Rapier rigid bodies and CCD](https://rapier.rs/docs/user_guides/javascript/rigid_bodies/), [Rapier RigidBody API](https://rapier.rs/javascript3d/classes/RigidBody.html), [Three.js Raycaster](https://threejs.org/docs/pages/Raycaster.html), [ws server examples](https://github.com/websockets/ws).
