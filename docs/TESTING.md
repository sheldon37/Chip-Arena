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
2. Try every specialization and its secondary. Verify machine-gun heat, magazine reloads, mana regeneration, sword parry timing, Shield's finite frontal guard and rear vulnerability.
3. In Sandbox, install a Chip twice. Observe numerical changes and installed count. Test Reach by comparing visible sword reach, Kinetic by launching crates, Demolition against walls, and Salvage using identical launcher hits.
4. Shoot a wall, then fire through the gap. Test muzzle obstruction with the player pressed against cover. Shoot from both sides and above.
5. Break both supports beneath the gray beam. It should fall or tip as its remaining fragment contacts give way.
6. Compare launcher mass, speed, direction and gravity cases; restore between each trial. Check resting debris for at least 10 seconds.
7. Create a room, join from a second browser, ready both, and confirm matching health/scores and matching wall/prop IDs. Close one client and verify the 12-second forfeit.
8. Verify round resets remove bullets, burns, old chunks, cooldowns and lost health.
9. Add latency using the network harness. Moving-obstacle prediction and lack of lag compensation are documented limitations.

Development builds include **Run Input Test** in the match menu to drive the local pilot with ordinary movement/attack intentions. It is not present in production menus. It never supplies trusted damage or body positions.

The browser's optional WebMCP tools are `read_arena_state` and `start_local_practice`; the dev input-test tool is development-only. Feature detection preserves ordinary play if the browser does not support this API.
