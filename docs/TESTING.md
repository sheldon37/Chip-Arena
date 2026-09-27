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

1. Complete each of Pistol / Fire / Sword against a bot; draft a Chip; finish a match; rematch; return to lobby.
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
