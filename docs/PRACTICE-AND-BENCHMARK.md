# Boss Phase Practice and Chip Lab

In solo Boss Battle, select a boss. P1 through P4 (or P3 for three-phase bosses) launch a bookmark with the current Chip Lab build. Defeating the selected phase or going down shows Retry phase and Back to Boss Battle. The pause menu can restart an attempt early. Each attempt creates a new arena, sets the weapon before spawning the pilot, and uses live combat transitions for the selected phase. Practice grants no account XP or boss clear.

Account shows the names and count of defeated bosses. The normal Boss Battle and Expedition combat authorities mark connected human participants in that encounter. DEV commands disable credit. DEV accounts can practice every boss. “Preview earned clears only” uses the DEV account's actual wins, so a developer can test the locked and unlocked UI without writing anything to account progress.

Cloud persistence needs the account Worker change and its additive Drizzle migration 0007. The field is independent of profile revisions and DEV grants. The authenticated session can only add to its own clear mask. As with the existing player-hosted XP system, the service trusts the game client's outcome report. The client retains newly earned clears under the account and service origin until a successful sync. Historical XP receipts have no boss ID to backfill. Include the new column when creating or restoring independent account-service snapshots.

In the solo Training Room, open the pause menu and choose Chip Lab. Select a weapon and up to five compatible chips. The trial starts on the first damaging hit against a training target and captures ten simulation seconds. The HUD shows elapsed time and total damage. The Lab shows the last hit, hit count and emitted chip proc events. Passive stat modifiers contribute through damage. Save a completed trial as a baseline, change the build, and repeat. Copy and paste a CA1 build code to share a weapon, equipped weapon techniques and chips. Import validates access, chip compatibility and stack limits before replacing the current build.

## Measuring multiplayer

Run the transport comparison with the .NET 8 CoreTests project and argument `--party-benchmark`. It uses real loopback TCP connections, the game's compressed transport, and an identical synthetic snapshot sequence for each pass. One host plus 1, 3 or 5 clients. Each guest must receive every scheduled snapshot.

For rendered tests, keep each client process open for both passes. Use the same boss, loadout, graphics and resolution. Record 2- and 4-player Boss Battles. The direct boss roster caps at four. Use the same supported PvP map at 2, 4 and 6 for a separate player-count comparison. Include a down, respawn where the mode allows it, and return to lobby. Each process writes PARTY_BENCH JSON windows to its Unity Player.log. Give each log a unique filename. Run:

```sh
python unity/ChipArenaConversion/tools/party_benchmark.py host.log guest1.log guest2.log guest3.log > comparison.csv
```

Compare rows for the same map, epoch, intro visit, stage and seat. UTC identifies the start of a window. Five-second windows also close at stage, roster and arena changes. P95 and worst frame times are local to each window. Outbound KiB/s counts successfully written framed game packets, excluding underlying transport headers. Cross-machine UTC alignment depends on clock synchronization. Visit 1 is the first intro observed on that map since the process started. Use each client's existing PERF_HITCH details for attribution.
