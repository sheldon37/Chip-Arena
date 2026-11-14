# ROUNDS replicated state handoff

Implemented on `codex/rounds-state-handoff`, layered over XP PR #287 and shared `0a8e6b9`.

- Successful swap-shop emits one `ArenaEvent`: `Kind=Teleport`, `Detail="swap"`, owner `Source`, swapped pilot `Target`, old/new body centres in `From`/`Position`, authority `Time`. Both Position Exchange and Dimensional Exchange use this path. Cooldown and clearance failures emit nothing.
- Summon `NativeDeath.owner` is the summoner seat, retained in corpse snapshots even when the observer never saw the summon alive. Pilot deaths continue using owner as the killer.
- `NativeMinionFrame.until` is the absolute authority expiry. Zero means no known expiry (old hosts and permanent horde adds); replicas restore zero as internal positive infinity. Timed summons retain their precise clock through capture/apply/replay clone.
- `NativePilotFrame.shieldUntil` carries the actual authority protection end, including shortening/clearing. Old frames default to zero and clear stale replica protection.
- Nonfinite incoming clocks and negative summon expiry are rejected. JSON fields are additive; no protocol bump or save migration.

Claude can now bind these fields. Presentation remains unchanged in this programming handoff. Optional Grapple/Tractor and Packet Theft hit flags are deferred; existing visuals still infer those procs from hits.

Verification: ROUNDS managed visual regression passed 286,989 checks; Player, Runtime and Editor compiled against pinned Unity 6000.3.24f1. Added native builder checks for real JsonUtility, late join, replay cloning, recapture, old defaults, invalid clocks, actual swaps, cooldown/clearance rejection and death owner. Those Unity/PhysX checks require the desktop builder and are not claimed executed locally. No timing, hitbox, damage, cooldown or movement rules changed; only event/state replication was added.
