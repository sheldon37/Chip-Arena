# Testing guide

## Automated

```sh
npm test
npm run build
npm run server:build
```

Network integration tests use a running backend:

```sh
# Terminal 1
npm run server
# Terminal 2
npm run test:network
# Adds 100 ms outgoing + 100 ms incoming delay (about 200 ms artificial RTT)
TEST_LATENCY_MS=100 npm run test:network
```

They drive two independent WebSocket clients using normal input intentions, verify exact authoritative state hashes at shared ticks, exercise all match phases, rematch, and verify disconnect timeout. A deliberately invalid damage message and a debug Chip grant are rejected/ignored.

For the physical-impact network assertion:

```sh
# Terminal 1
PORT=8791 ALLOW_TEST_ROOMS=true npm run server
# Terminal 2
node --import tsx tests/debris-network.ts
```

This explicitly creates a private test room, rejects the non-owner's debug command, launches an 8 kg body at 40 m/s, and compares the authoritative impact report and health between clients. The test room's launcher is authorized only on the server when BOTH the environment and creating host opt in.

## Manual browser coverage

The Developer Sandbox (and F3) needs a DEV account in built games (owner 2026-10-04); `npm run dev` keeps it open for
local testing. Everyone else practises against the Play vs Bots dummy.

1. Complete each of Pistol / Fire / Sword against a bot. One round win must not award a set or Chip. Two wins award a set and the loser drafts; a 1–1 split requires a deciding round. Finish five sets, verify no final draft, rematch, and return to lobby.
2. Try every specialization and its secondary. Verify every gun reloads (Machine Gun: 48 rounds / 2.4 s), mana regeneration, stamina costs/regeneration, sword parry timing, and Shield's stamina-limited frontal guard and rear vulnerability.
3. In Sandbox, install a Chip twice. Observe numerical changes and installed count. Test Reach by comparing visible sword reach, Kinetic by launching crates, Demolition against walls, and Salvage using identical launcher hits.
4. Shoot a wall, then fire through the gap. Test muzzle obstruction with the player pressed against cover. Shoot from both sides and above.
5. Break both supports beneath the gray beam. It should fall or tip as its remaining fragment contacts give way.
6. Compare launcher mass, speed, direction and gravity cases; restore between each trial. Check resting debris for at least 10 seconds.
7. Create a room, join from a second browser, ready both, and confirm matching health/scores and matching wall/prop IDs. Close one client and verify the 12-second forfeit.
8. Verify round resets remove bullets, burns, poison, old chunks, cooldowns and lost health.
9. Add latency using the network harness. Moving-obstacle prediction and lack of lag compensation are documented limitations.

Development builds include **Run Input Test** in the match menu to drive the local pilot with ordinary movement/attack intentions. It is not present in production menus. It never supplies trusted damage or body positions.

The browser's optional WebMCP tools are `read_arena_state` and `start_local_practice`; the dev input-test tool is development-only. Feature detection preserves ordinary play if the browser does not support this API.

## Modifier checks

In Developer Sandbox, switch weapon and confirm the Chip picker changes: guns offer Magazine Bank/Rapid Cycle, magic offers Mana Reservoir/Spell Clock, and melee offers Stamina Cell/Flurry Clock/Echo Strike. Projectile-only Chips must disappear for melee.

- Install Magazine Bank twice on Machine Gun: the HUD should show 72 rounds. Fire until empty and reload; there must be no heat lockout. Its secondary spends six rounds.
- Install Mana Reservoir or Stamina Cell twice: that resource should display 150 maximum. Check spells and melee actions actually spend the appropriate resource.
- Compare Parallel Shot with Echo Strike; the first emits additional projectiles, the second adds delayed damaging melee sweeps. Both have explicit costs and per-stack caps.
- Fire past an exposed opponent with Tracking Circuit; the projectile bends within its turn limit. Repeat with a wall in the path; cover must stop it.
- Compare Velocity Coil and the relevant rate Chip independently. Projectile speed reduces drop and extends travel before landing; rate changes cadence.
- Remove Chips and reset the arena to compare the base build.

`tests/modifiers.test.ts` exercises these effects through the authoritative simulation, including real damage, ammo exhaustion/reload, resource blocking, homing occlusion and nonrecursive split metadata.

## Projectile drop checks

Use Developer Sandbox with a dummy. Aim level across an open lane, fire, then install Velocity Coil and repeat from the same position and aim. The upgraded projectile should land farther away. Aim higher to lob shots; compare guns and the slower Fire/Darkness spells. Ricochets and split shots should also fall. Repeat a shot with intact cover between the shooter and target; split shots must not ignore the cover they struck.

`tests/projectile-drop.test.ts` measures every gun/spell on a level test floor, compares speed upgrades at the same range, checks fast descending player hits, falling ricochets, intact-cover collision, nonrecursive split gravity, and bounded homing/bot aiming. Floor-contact assertions allow 2 cm for shape-cast tolerance; the no-contact integration assertion uses a strict numerical tolerance.

## First-person, enchantments and sound

1. Settings → Camera view → First person → Save. Enter Sandbox. Press V twice and verify the view switches without changing health, ammo or installed Chips. Reopening the game should retain the chosen view. Fire against cover in both modes; neither camera may bypass it.
2. Install Ember Circuit and Venom Circuit on Pistol. Shoot the dummy once and stop: the hit applies both statuses, then damage continues over time. Fire is 3 DPS for 2 s; poison is 2.5 DPS for 4 s per installation. Install twice to compare additive DPS. Rapid repeat hits refresh the timers without postponing ticks.
3. Switch to a melee weapon: the blade (or Shield edge) shows orange flames and green enchantment. Strike the dummy and check both status badges. Restore/reset must clear them. Compare with both Chips removed.
4. Observe bright projectile cores, colored enchantment particles and short trails. Compare Projectile trails on/off in Settings and full/reduced Effects. Gravity and collision remain the same.
5. Settings → Test Sound should report 30/30 effects loaded after the click. Test volume and M mute/unmute. Check actual gunshot, melee, elemental hit, reload, destruction and countdown cues in combat. Audio should not replay every time a snapshot repeats.

`tests/view-elements-audio.test.ts` checks both camera modes and muzzle cover, damage/status application from all guns and melee weapons, exact DoT duration and cadence, refresh/stacking, nonrecursive metadata, kill attribution, resets, event deduplication and all 30 WAV files (headers, audible PCM, distinct samples, peak headroom).

Self-contained network runners launch and stop their own backend:

```sh
TEST_LATENCY_MS=100 npm run test:network:local
npm run test:elements:network
npm run test:debris
```

The full-match runner uses first person for the attacking pilot and third person for its opponent. The elemental runner creates an authorized private test room, installs both Chips through the owner's debug permissions, fires using normal input, and compares both clients' statuses/health at identical ticks. Non-owner debug grants must be rejected. Use `TEST_PORT` to override port 8792 when another runner is active.

## Sets, status visuals and player board

- Default rules: two round wins take a set; five set wins take the match. Check 2–0, 2–1 and a simultaneous death with a 1–0 lead. A draw preserves that lead and replays the same round. Toggle both-player drafting and confirm it waits for the completed set. Let selections time out.
- Install Ember Circuit twice and Venom Circuit twice in Sandbox. Hold Tab (on a melee spec, where Tab is lock-on, use the Builds button), hover each Chip and verify ×2, four copies/two types, +6 burn DPS and +5 poison DPS. Inspect the opponent’s Chips as well. Release Tab, then check that neither attacks nor movement remains stuck. The Builds TAB button supports the same inspection without holding a key; Escape or × closes it.
- Shoot a dummy: body flames/embers and rising/popping green bubbles must coexist while the corresponding statuses are active. Let them expire without another hit, restore, kill the dummy and reset the arena; each should stop the relevant effect. Merely owning enchantment Chips must not set the wearer on fire or poison them.
- Repeat in first person while receiving a status: see hand effects and subtle side cues. Compare reduced quality; both elements should remain identifiable.

`tests/sets-status-vfx.test.ts` covers set transitions, draws, both-draft timeouts, final sets, rematch/lobby resets, exhausted Chip pools, actual status-driven flame/bubble geometry, fixed mesh reuse, quality changes and first-person cues. `tests/chip-inspection.test.ts` checks exact additive/multiplicative summaries and a real duplicate selection through the draft path.

## Block and browser-hosted rooms

- Right-click has a 0.30 s active window and 4.00 s cooldown for every weapon. E keeps the previous secondary action. Test early, correctly timed and late defense against projectiles and melee. Check that an existing DoT and falling debris still hurt during Block. Hold right-click through the full cooldown: it must not automatically repeat.
- Install Rebound Capacitor twice (2.89 s cooldown) and Phase Buffer twice (0.46 s active). Inspect both on the player board. Overclock and all weapon-specific rate Chips must not change Block cooldown.
- Return Amplifier strengthens reflected shots; Counter Pulse triggers only once per activation after a successful defense. Check cover, projectile reflection limit, and no recursive Shrapnel/splitting/elemental application from the pulse.
- In browser-hosted mode: create a room, copy the eight-character code or invite link, and join from another computer. Ready both and play to match end, draft compatible Chips, rematch, then close the host. The guest must see a closed room and a Return to Main Menu control. A closed guest should forfeit after 12 seconds.
- Repeat over different home networks and a restrictive network. Rooms now use ordinary same-origin HTTPS, with no peer negotiation. Check brief interruptions recover without duplicating selections; close the host and confirm the guest exits clearly. Joining a occupied room or using an old code must show a useful error. Public reachability/performance still needs verification by two clients through the published deployment.

`npm run test:block:network` tests a real reflected shot across two WebSocket clients and compares health, cooldown and reflection attribution. `tests/blocking.test.ts` covers all Block rules and modifiers; `tests/host-room.test.ts` checks host-side authority, validation, join limits, disconnects, full set/draft progression and rematch. Its input-driven match tests the host simulation wrapper, not the HTTPS transport.

## HTTPS relay verification

`npm run test:https` starts a real HTTP server using the production Worker handler and SQLite mailboxes, connects two independent transport clients, completes five sets and four Chip drafts, compares received authority snapshots, tests a lost response/retry and rematch, then leaves. `TEST_LATENCY_MS=100 npm run test:https` adds 100 ms on each request and response leg. This is in addition to relay cadence. `tests/relay.test.ts` checks seat isolation, concurrent joins, mailbox races, duplicate retry ordering, malformed packets, server debug/snapshot restrictions, expiry, origin/body limits and quick input taps.

## Guest movement regression

Create a room in one client and join in another. Use the guest to walk, jump, dash and approach cover. Compare standing on the foundation with a legitimate edge fall. Test at 100 ms each-way simulated transport latency; prediction must keep its capsule synchronized on every replayed frame. In `tests/prediction.test.ts`, the pre-fix airborne regression fell from Y=2 to Y=-9.692 while its collider stayed at Y=2. `test:https` now also runs guest prediction and asserts floor clearance during the delayed full match.

## Movement under latency and jitter

Run `TEST_LATENCY_MS=100 TEST_JITTER_MS=40 npm run test:motion:network` for two real WebSocket clients and measured guest correction/visual jump/update gaps. Run `TEST_LATENCY_MS=100 TEST_JITTER_MS=40 npm run test:https` for the separate relay's moving-guest full match/rematch. `npm run dev` automatically uses the local dedicated server; create and join by code/invite without filling an address. The debug HUD reports raw correction, snapshot interval P95 and interpolation buffer. Judge moving/jumping/turning and debris contacts, not only whether a round completes. Test in two independent browser clients with the guest moving; a stationary guest is insufficient to certify smoothness.

## Rarity and casting regression

`npm test` includes `rarity-combat.test.ts`. Run `TEST_PORT=8793 node tests/run-network.mjs rarity` for a short two-client test covering custom draft counts, spell release, alternating daggers and persistent Pong replacement. Use Developer Sandbox → Preview Rarities to inspect all six styles, or Preview Draft to sample real weighted offers. The animation inspector provides Wind-up pose, Release pose and individual dagger hands.
