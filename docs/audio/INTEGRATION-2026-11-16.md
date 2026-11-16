# Chip Arena audio integration · 16 November 2026

This pass routes existing recordings to 25 Katana, Fangs and Glaive cues, revises or adds foley for seven entrances, and gives two expedition bosses and five enemy types recorded effects. Bombadier casts, grenade types and Finale have separate sound identities.

[Consolidated asset inventory](audio-inventory-2026-11-16.csv) · [Claude listening and artistic handoff](CLAUDE-AUDIO-HANDOFF-2026-11-16.md)

## What plays now

| Category and event | Previous sound | New sound | Status |
| --- | --- | --- | --- |
| Bombadier · Firecracker Line and links | Pistol shots and bullet ricochets | Firework airbombs with a quieter electric link accent | Integrated |
| Bombadier · mine, cluster, jump, Finale | Shared fire blast, stacked fire and quake, knife throws, heavy sword swing | Mine blast, cluster debris, short bomblet transients, kinetic jump, frag handling and positional switch. Simultaneous Finale columns share the nearest quake per listener. | Integrated |
| Glaive · sweep, hit, throw, ricochet, catch, flick, burst | Procedural ring sounds | Pitched sweep, spin, shield-throw take 2, damped metal catch, dagger throw and kinetic burst. The ring hum, skip and chime retain their tonal synth. | 7 recording assignments |
| Katana · cuts, draw, sheath, hone, dash, clash | Procedural blade sounds | Metal-on-metal scrape take 0, different swing and finish weights, metal block. The DEV kick uses its blunt swing and existing impact path. | 11 recording assignments |
| Fangs · cut, stab, knife throw, hiss, vanish, smoke, bloom | Procedural fang sounds | Dagger swings and hits, knife release, train steam-release take 0, blink and toxic burst. Drip, beat and toll keep their synth. | 7 recording assignments |
| DEV Greatsword and DEV Katana entrances | Generic stings, no dedicated action foley | Heavy Turn uses the existing Greatsword `phoenix-rising` alternate when installed, with separate heave, ground bite, turn and rest accents. Edge Flourish cues the draw, sweep, wheel and final strike. | 4 beats each |
| Glaive, Katana and Fangs entrances | Existing score beds | Crescent Return has 8 beats including two contacts and both catches. Moonlit Draw has 4 including the cut and closure. Venom Bloom has 5 including the step, flurry and strike. | Integrated |
| Hardlight Boot and Last Warning | Existing Cyber Lord bed and crowded Bombadier salvo | Four Hardlight formation/combat accents. Last Warning cues fuse, stomp, click, three salvo accents and shock. Toss and catch use the animation marks at 6.42 and 7.02 seconds. | Integrated |
| Abilities and techniques | Knife and explosion for smoke, generic arming cues, Force Wall release routed to a loop | Pressure release for Smoke Veil, mine arming for Tripwire, target lock for Sonar Dart and a force-field impact for Force Wall. Release/arm sounds follow confirmed state. | Integrated |
| THE 6:47 | Empty dedicated boss keys | 15 physical and warning events use metal, steam brakes, glass, rebar, electricity and ground impacts. Station-wide warnings stay flat. | Integrated |
| THE CONVERTER | Empty dedicated boss keys | 19 arrival, telegraph, hit, break and death one-shots. Idle furnace can use the existing fire loop at 48% of its normal bed level. | Integrated |
| Ironworks enemies | Generic or empty dedicated routes | 12 assignments across Cinderling, Stoker, Slagback, Ladleman and Rigger. Hook and rattle use specific chain takes. | Integrated |
| THE HUM, THE DEEP END and NOCTIEL | Private custom recordings | Original assignments retained. Rebuilt all 213 private boss files from the builder's nine batch archives. | Verified assets |
| Hit confirmation | Owner-selected hitmarker | Same built-in hitmarker. The empty `hit.confirm` entry deliberately selects it. | Intentional |

The host marks casts pending until it accepts the release. Hits use per-seat authority IDs. Match and preview use the local rendered pose clock, skip stale events after joining or rewinding, and stop active intro foley on interruption. Dedicated takes get first choice. Borrowed takes and the original synth or bundled clips provide fallbacks. Borrowing an asset preserves the logical cue's own cooldown. Combat uses the existing voice pool, limiter, distance falloff and pan. The pass changes neither damage nor hitbox timing.

## Audit coverage

| Systems | Findings |
| --- | --- |
| Sword, Daggers, Shield, Greatsword | Existing swing, impact, charge/spin, bash/throw and block families retained. Selected takes also serve related new weapons. |
| Pistol, Rifle, Machine Gun, Bow | Existing shot, reload, ADS, handling, projectile, ricochet and impact paths retained. |
| Fire, Water, Lightning, Darkness | Existing elemental shot, impact, status, blast and sustained field paths retained. |
| Siren, Emperor, Ansatsuken, L44T, Cyber Lord, Top Dog | Original music, fighter hits, ability and cartoon cues retained. Hardlight Boot and Force Wall changed. Top Dog's authored entrance follows the shared interruption-aware clock. |
| Bombadier, Glaive, Katana, Fangs, DEV Greatsword, DEV Katana | Integration details above. The roster contains 24 weapon rows. |
| Remaining entrances | Reviewed the 12 standard weapon signatures, seven procedural entrances, Emperor, Rain Warrior, Matrix, Siren and Sheldon. Retained their existing score/sting routes, including the Siren's boss song and Sheldon's owner track. |
| Chips | Reviewed semantic proc, wild-chip effect, weapon kit, movement, guard and status routing. The catalog has 236 chip definitions. Stat modifiers use the affected action's sound. Individual chip listening tests remain outstanding. |
| General gameplay | Reviewed the registry and presentation routes for spawns, attacks, footsteps, landing, hazards, deployment/revive machines, purchases, match transitions, selection and UI. Existing working routes retained. |

## Inventory and licensing

The CSV has 16,743 records. Filter by collection to avoid counting source and shipped versions as separate sounds.

| Collection | Records | Evidence |
| --- | ---: | --- |
| Purchased source survey | 15,325 | Claude's `audio-inventory.csv` and `astra-audio-handoff.md`. Package-relative paths, surveyed durations, prior usage and audition candidates. |
| Installed Ogg | 806 | Read every clip in the derived bank, including all 593 base clips and 213 custom boss clips. Key, take name, duration and channels are recorded. |
| Boss batch sources | 213 | Each original MP3 matched an accepted import. |
| Unity tracked | 132 | Active project's gunshots, effects, music, stings and structure resources, including literal usage where identifiable. Dynamic ID selection can also use these resources. |
| Other tracked | 92 | Legacy, prototype and tooling audio. |
| Unpopulated registry key | 175 | All 538 accepted keys compared with the 363 populated keys. Includes fallback or dedicated-recording status per cue. |

The derived bank has 364 manifest entries and occupies 16,648,910 bytes. The existing 18,000,000-byte limit leaves 1,351,090 bytes. The imports include 77 Hum, 44 Deep End and 92 NOCTIEL clips. The source ZIP identifies the licensed effects packs and tracks private intro provenance. Only code, documentation and metadata are in this change. The Windows builder still imports its private files into `Assets/LocalAssets/Resources/EffectsAudio`.

The Drive connector could read the base ZIP and the nine private boss batches. `Ordoss.zip` exceeded its download limit, as does the larger `Magica Soft.zip` mirror. Raw source candidates are based on the earlier survey. Heavy Turn now selects a previously unused Greatsword score alternate. Most other edits reuse recordings already played by other actions.

## Verification and limits

| Check | Result |
| --- | --- |
| Actual private media | Rebuilt both imports, validated and installed locally. Checked every path, Ogg duration, 44.1 kHz format, positional mono and import metadata. All named takes resolved. |
| Managed effects suite | Passed on the working implementation with the private bank. Included confirmed and cancelled casts, duplicated and late snapshots, listener-dependent Finale selection, 20,000 voice allocations and 22,984 empty-bank parity contexts. All eight Python audio installer tests passed. |
| Unity references and relay | Player, runtime and editor reference compilation passed on an intermediate implementation. Local loopback passed delayed-client and recovery scenarios. The full suites did not produce a final green result before the workspace disconnected. The broader Python run exposed a pre-existing cinematic test expecting the old rig clock literal. The revised guard checks both active render paths for the sampled intro clock, hidden rig weapon and age-driven halo. |
| Published revision | The implementation was recovered against the shared GitHub base following the disconnection. Static source, registry, inventory and fallback checks were repeated on the recovered files. The [native CI checks for this PR](https://github.com/sheldon37/Chip-Arena/pull/401/checks) record the current revision’s reference compilation, managed suite, packaging and Windows updater results. A Unity player listening test remains outstanding. |
| Audible mix and performance | Pending Windows Editor/player, host and guest listening, output-bus clipping and in-game frame/GC profiling. No production or test-channel release. |

Of the 175 unpopulated keys, 71 use recorded one-shot substitutes, one uses the furnace loop, 44 use existing creature fallback paths, nine keep intentional synths, one uses the built-in hitmarker and 49 boss cues still lack dedicated sound or a substitute. Prioritize encounter identity and warnings before filling the list. No mandatory new generation was established.
