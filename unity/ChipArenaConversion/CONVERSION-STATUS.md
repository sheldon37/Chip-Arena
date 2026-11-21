# Native Unity conversion candidate

Source baseline: `05950eaef80ff2483a9c44d898cad98be1a76bbc`. Editor: **6000.3.24f1**.
Logic branch: **codex/unity-parity-logic**, PR #66 into **claude/sleepy-ritchie-g8ruhv**. The Electron game and accepted Unity lab remain separate.
This is an implementation candidate with explicit open decisions and unrun acceptance gates. Catalogue coverage does not prove every Chip, network path or presentation is at parity. See docs/UNITY-PARITY-GAPS.md and docs/UNITY-LOGIC-HANDOFF.md.

| Source feature group | Native implementation |
|---|---|
| Weapons and combat | All 15 weapons; gun/magic/melee primaries and Q/E, timed block/reflect, recovery, status effects, modifiers and summons |
| Chips | All 236 catalog entries mapped to explicit stat/mechanic handlers; per-class eligibility, upgrades and six PvE money Chips |
| Signed DEV content | Emperor, Ansatsuken, Leet and Siren, their abilities, styles and grant verification |
| Match flow | Rounds, sets, teams, readiness, simultaneous knockouts, rematch and timed one/both-side drafts |
| Ward Zero | Wave schedules, economy, personal picks, vending/med/recruit stations, recruits, revives, boss phases, horde limb loss, weighted entry queues, vents, moving doors and pit transitions |
| Characters and animation | Original voxel geometry, source-authored weapon/hat models, 165 motion clips and captured melee animation with the source runtime layer (gait, footwork, hip seating, blade clearance, blending, recovery, hit/parry reactions); first/third person, ADS and rifle scope |
| Damage and deaths | Source voxel wound carving/anchors, impact sprays, headshot kneel/fall/velocity handoff, detached parts, 11 scripted elimination scenes, dropped weapons and sky death announcements |
| World and effects | 15 layouts; 139 source model exports, 10 stage-hazard parts and three procedural textures; night sky, snow, water and chimney smoke on the render clock; animated saws, spikes and lava; Ward Zero baked from its first rendered frame with song-mood fan, doors, chandeliers, clocks and beacon beams; menu backdrop orbit; combat light/glow/trails and ability fields |
| Entrances and draft | 20 source entrance stages with camera/light cues and the source intro overlay (Siren boss title); countdown, round result, set score, match end, killcam frame and LIVE handoff; appearance previews; source chip cards (per-tier package materials, traces, die glass and line icons, the Emperor/Ansatsuken/L44T/Siren card art) with per-tier entrances, hover/focus/keys, shatter, palm fly and details spotlight; the picker standing behind the low tray, bowing to look at the highlighted card (held between cards; gaze and turn springs, smooth pick clock) with the arm on its side lifting toward it, slowed reaching hand, grab, then the chip (hand-sized) planted into the other wrist with a rarity pulse, also as the Siren pick's follow-through; Siren personal pick screen and chip reveal |
| Audio | 83 original audio files; combat, map/menu/boss music, machine/round/entrance cues; procedural Slender drone/static/heartbeat and death-announcement sounds |
| Menus and HUD | Saved settings (including interface scale), appearance, build/Chip inspection, source-palette menu skin, styled Unity update card, source fight HUD layout (vitals, cooldown cards, SET/ROUND pill, pixel-centred crosshair and hit marker), PvE banners/boss bar/money/pending chips/results, health bars, damage numbers, machine prompts, spectator switching and isolated kill replay |
| Developer tools | Signed local sandbox with sorted Chip grants, targets and speed controls; configured grant-management UI |
| Accounts | Configured HTTPS flows; managed P-256 verifier tested on .NET, profile/recovery preservation and reload API; Mono/live service/DPAPI acceptance still required |
| Multiplayer | Native TCP LAN/code discovery and standalone relay source; worker transport, explicit JsonUtility presence, bounded frames/events, replica prediction/interpolation, lobby/vote/metrics APIs. Real loopback transport tests pass; Unity host/guest and WAN acceptance unrun |
| Windows delivery | Separate builder, voxel CA icon, source asset receipts and import/shader/animation/native-physics gates; EXE and complete-folder ZIP produced only after gates pass |

## Native adapters and boundaries

- PhysX replaces Rapier. Collision/recovery and cosmetic bodies use native adapters; contact settling and rendered animation still need on-device acceptance.
- Unity materials, lights, shaders, pooled effects, procedural audio and IMGUI menus replace browser rendering/WebAudio/DOM. Source geometry, textures, timing and captured animation are retained. Overlays follow the source CSS numbers on one shared canvas, but IMGUI cannot reproduce blur filters, clip-path shards, SVG icons/flames or CSS 3D card rotation exactly (approximated: backdrop blur from one shared downsampled frame copy, text blur as ring copies, baked polygon and band clips), legacy GUILayout menu text is matrix-scaled, and particles, holograms and cosmetic physics are not pixel-identical reproductions.
- Native protocol **5** includes wound/death, summon/Slender, machine and draft timing state. It deliberately does not interoperate with Electron or earlier native checkpoints.
- Account and relay origins are configurable; no credentials or production endpoints are bundled. No live service was changed. A public native relay requires a separate deployment.
- Authority remains on the host or unattended server. A player-hosted room closes when that authority exits. Review of the pinned original source found no automatic host migration, so its absence is not an unfinished parity item.
- Electron's Chromium shell, browser storage and web updater are replaced by the separate Unity player, preferences and manual test-build ZIP. The existing updater/feed is untouched.

## Acceptance status

PR #66 checkpoints `718cd4f` / `d80d974`: managed regressions, real loopback UDP/TCP/WebSocket, 24 Python tests and 51 updater transaction checks pass. Runtime source compiles against genuine Unity 6000.3.24f1 API DLLs using .NET 8 Roslyn (C# 9). That is API compilation, not Mono execution or a Unity build. The new Editor JsonUtility/PhysX gates are wired but unrun. Build-Windows.cmd was attempted through cmd.exe and could not start (Linux host, exit 127). GitHub CI 37808988291 on d80d974 passed normal-project checks and Windows launcher/process fixtures; those do not execute a Unity player. No merge, feed publication or Site release.

**Not run:** activated Unity import, shader compilation, rendered play, actual native physics gates, remote internet rooms, live account flows, headless Unity execution and the Windows EXE build. These require the activated Windows Editor. Run Build-Windows.cmd; it must print UNITY_TEST_BUILD_READY before the resulting package is treated as built. Review Builds/build.log if a gate fails.

Disconnect policy (native seat-resume addition): a 12-second grace delays match resolution; bodies are disabled while disconnected. A resume during grace preserves health. After timeout the seat remains dead until the next round, and fewer than two opposing connected sides end the match. The 90-second seat lease preserves identity/loadout/results access, not a right to rewind a completed forfeit.
