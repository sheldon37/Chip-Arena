# Native Unity conversion status

Source baseline: `05950eaef80ff2483a9c44d898cad98be1a76bbc`. Unity: **6000.3.24f1**.
This project and executable remain separate from Electron. Work is continuing toward the owner's full-conversion request.
The following is implementation status, not a claim of Windows or visual acceptance.

| Area | Native implementation at this checkpoint |
|---|---|
| Combat | Standard gun/magic/melee primaries and Q/E, modifiers, block/reflect, recovery, status fields, summons |
| Chips | Full 236-entry source catalog is eligible through explicit stat/mechanic support catalogs; six PvE money Chips |
| DEV | Signed-access Emperor, Ansatsuken, Leet and Siren, their actions and style Chips |
| PvE | Three wave schedules, horde bodies/limb damage, economy and stations, personal drafts, recruits, revives and boss phases |
| Presentation | 50 original model exports with three procedural textures, 159 original motion samples including 20 entrances, existing captured melee clips, combat lights/fields, native death bodies and effect adapters |
| Audio | 83 original source files with menu/map/boss music and combat event routing |
| Accounts | Configured HTTPS service, login/register/recover/profile/logout, signed unlock verification, Windows encrypted session persistence |
| Multiplayer | Separate native TCP rooms, host authority, bounded compressed frames, source/version lock, room codes, readiness, snapshots, reconnect seat tokens |
| Packaging | Separate Windows builder, CA icon, source asset receipts and fail-closed import/build gates |

Still being completed: vent pits and entry detail; remaining cinematic/environment presentation and native body refinements;
internet relay/discovery/host migration; account administration/settings parity; additional native gameplay gates.
Some cinematic, rig and PhysX adapters are approximations of the source renderer/physics rather than identical output.
Unity clients cannot join Electron rooms. No service endpoint or credentials are bundled and no live service mutation was performed.

Verified locally: managed/source parity checks, real loopback transport, Unity Runtime/Editor/Windows C# compilation.
Unrun: activated Unity import and shader compilation, rendered play, native PhysX acceptance, live accounts/remote rooms and Windows EXE.
See VALIDATION.md and docs/DEV_LOG.md for the detailed checkpoint record.
