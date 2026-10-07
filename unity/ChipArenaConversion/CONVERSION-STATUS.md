# Native conversion status — foundation milestone

Target approved by the owner: **native Unity/C#**, with gameplay simulation and physics converted too. Branch: `codex/unity-conversion`. Source remains `05950ea`; the accepted lab and original game are preserved separately.

**The full-game conversion is incomplete.** Imported definitions do not establish implemented mechanics. This checkpoint creates the separate project, native content/rule foundation and the first native match/physics test.

| System | This checkpoint | Remaining work |
| --- | --- | --- |
| Catalogue | Exact exported definitions: 15 weapons, 236 Chips, 14 Q/E entries, rarity/exclusion tables | All runtime mechanic handlers |
| Stats and draft eligibility | Native C# matches 60 source cases / 4,620 values, eligibility, seeded offers and roll profiles | Broader gameplay interaction parity |
| Match flow | Native countdown, round/set wins, draw replay, losing-side draft, bot skip, rematch | Specialization screen, intros, exact draft staging, disconnect/rejoin and complete options |
| Source maps | 15 collision layouts / 3,451 blocks; rotations, masses and support IDs retained | Scene dressing, detailed materials, lighting, weather/music and Ward Zero machinery/vents |
| Gameplay physics | Unity capsules/rigid bodies, CCD, swept shots, support release, breakable props | Native Windows acceptance; Rapier response comparison, stepping, debris damage and complete destruction |
| Standard melee | Four primary weapons with captured sets and source base values | Full contact sweeps/footwork/riposte parity, shield guard, E moves, knockdown/juggle rules |
| Guns | Three primaries, magazines, reload, ADS, gravity, reflection and head/body hits | Exact source collision/aim parity and E handlers |
| Magic | Four basic primaries, mana/cast delay, burn/blast/Soak/chain/drain integration | Native verification, original casting poses/models and all Q/E handlers |
| Chips in combat | 89 stat-only entries enabled; unimplemented effects are not offered | Remaining 147 entries, special effects, summons, proc ownership/recursion and combinations |
| Stage hazards | Source C# schedules for saws, spikes and lava; basic native visuals/contact/swimming | Native acceptance, production effects, Siren song/vent hazards |
| DEV weapons | Definitions are reference-only; unavailable for match selection | Ansatsuken, Emperor, L33T H4x, Siren, associated exclusive mechanics and entitlement gates |
| PvE / Siren | Source inventory only | Director, waves/horde, boss phases, economy, machines, picks, downed/revive, recruits, completion cleanup |
| Animation | Captured melee / procedural guns inherited; basic staff visuals | All production layers, casting, intros, cinematics, body wounds, ragdolls and kill effects |
| Multiplayer | Not connected to this native authority | Native host/guest transport, eight-player tests, prediction, reconnect and isolated service configuration |
| Accounts / customization | Separate local test name/settings only | Native identity adapter, entitlement verification, cosmetics, persistence and safe migration design |
| UI / audio | Foundation setup/HUD/scoreboard/draft; six original gun/reload/hit WAVs | Production menu/card UI, audio library/music, intros, killcam and accessibility/options |
| Packaging | Separate Unity project, executable name, save identity and CA icon | Activated Windows build and end-to-end acceptance |

The original 35 managed lab checks are retained as regressions of the inherited code. They do **not** prove the new `NativeArena` physics authority behaves correctly. The new native rule fixtures and match checks test their own code; the Editor physics gate is separate.

One intentional contact change: lava is checked against its individual channel, preventing the source's two channel entries from applying duplicate damage to a pilot in either channel. Clock schedules still match the original fixtures; overall hazard damage/physics parity is not claimed.

## Conversion sequence

1. **Implemented; Windows gate pending:** content parity, native layouts/physics and first match loop. Confirm walking/climbing, collision, dynamic supports and round reset on the owner's activated Unity installation.
2. Complete standard combat parity: melee sweep/contact/recovery, directional blocks/ripostes, gun and magic Q/E abilities, health/status/impulse rules, projectile modifiers and native network ownership.
3. Implement every Chip handler against source-derived scenarios; retain exclusions and proc-recursion rules. Do not enable a catalogue entry merely because its data imported.
4. Port DEV styles, Siren and all PvE contracts/director/economy/AI. Carry the current wave cleanup and impact-effect lifetime behavior.
5. Port the remaining models, animation layers, maps/scenery, VFX, audio, cosmetics, intros, body damage, kill effects and killcam.
6. Complete the native lobby, online transport/prediction, identity and persistence against an isolated test configuration. No live save/account migration or updater cutover is authorized by this checkpoint.
7. Full regression and Windows/two-or-more-client acceptance, then owner evaluation of performance and game feel. Merge/publication remain separate decisions.

No deadline or full-game performance improvement is claimed. The first required external result is the native map/physics gate and playtest; this workspace can compile against Unity APIs but cannot run an activated Windows Editor or observe the user's game.
