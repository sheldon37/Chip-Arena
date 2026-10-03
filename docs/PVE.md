# PvE: Siren TEST BOSS (Ward Zero)

Status (2026-10-03): **integrated on `claude/pve-integration`; not merged, not released.** The foundation and the six
streams (S1 horde, S2 director, S3 support, S4 UI, S5 horde visuals, S6 world visuals) are merged on top of
`claude/pve-foundation` (base `b6d2f58`, the shared branch tip, unchanged since). S7 applied the cross-stream requests,
fixed two integration defects, retuned `balance.ts` with the balance harness, added the end-to-end flow test and the
network suites, and ran the real-app smoke. See "Integration (S7)" below. The owner-authorized merge into
`claude/visual-revision` and the test-channel publication are separate, later steps.

Design of record: the PvE design (revision 2, 2026-10-02) by the lead architect; this file carries the parts the next
agents need from GitHub alone.

## Owner decisions (2026-10-02)

| ID | Question | Decision |
|---|---|---|
| D18 | Moving while the personal chip screen is open | **Frozen in place** (no move, attack, block, roll; camera turns). `PICKS.moveWhileChoosing=false` |
| D35 | Last Laugh in PvE | **Heavy hit, no floor.** The crush deals `0.6 × victim max HP × damageScale(h)` through `damage()` (not an instant kill). In phase 4 there is **no cooldown floor** on Last Laugh either: `PVE_SIREN.play.minRecast.laugh = 0`, so only the move's own duration gates a re-cast. The concurrency caps stay (rains ≤ 2, eruptions ≤ 4, one mark, one grab at a time) |
| D7 | Defeat and getting back up | Design default: paid revive any time; free 50% HP respawn of all dead members at each wave clear and boss phase; one self-revive for a solo human; defeat only when the whole party is down |
| D40 | Recruit damage-dealer weapons | Ranged and melee only (no magic) |
| D9 | Heal purchase | Not asked; the default ships: 2 med stations ($60, 50% over 2 s, 12 s cooldown) |
| D41 | Medic focus | Not asked; the default ships: the medic follows its hirer |
| P1 (2026-10-03 playtest) | Taking a dispensed chip | **Never opens the chip screen.** It waits as a pending pick (CHIPS icon / `C`) until the pilot chooses. The start pick still opens at fight start. The "Open chip screen when I take a chip" option is gone; the `prefs` command is accepted as a no-op for published clients |
| P2 (2026-10-03 playtest) | Esc during a boss run | Opens the game menu (also when the browser only releases pointer lock); the offline run pauses and Resume re-locks the pointer. Esc on an open chip screen is still Choose later |

Earlier decisions still in force: minions are procedural zombie Pilots (PR #26, `docs/SIREN-MINIONS.md`), friendly to the
Siren's team; DEV classes stay special; recruits use non-DEV weapons.

## Running it

- Unit tests: `node --import tsx --test tests/pve-contract.test.ts tests/pve-foundation.test.ts tests/pvp-golden.test.ts`
- Flow: `node --import tsx --test tests/pve-flow.test.ts` (`PVE_LONG=1` plays every wave).
- Balance: `npm run test:pve-balance` (env `PVE_BAL_H`, `PVE_BAL_W`, `PVE_BAL_SEEDS`, `PVE_BAL_MAX_MIN`, `PVE_BAL_JSON`). Perf:
  `npm run test:pve-perf`.
- Real server: `TEST_PORT=<port> node tests/run-network.mjs pve`.
- Debug ops (offline worker room for DEV accounts, server test rooms for the owner): `skip-wave`, `skip-to-boss`,
  `boss-phase:n`, `boss-damage:n`, `money:n`, `god:on|off`, `kill-adds`, `spawn:<kind>`.
- Network smokes (not in `npm test`): `node --import tsx tests/pve-loopback-network.ts` (in-process room, then the real
  server on `TEST_PORT`, default 8794) and `node --import tsx tests/pve-relay-network.ts` (PartyRoom v2 through the local relay).
- PvP guard: `tests/pvp-golden.test.ts` hashes four PvP traces generated on the untouched base. It must stay identical;
  regenerate only for a deliberate PvP change (`PVP_GOLDEN_PRINT=1`).

## Contract summary (for streams)

| File | Owner | What it gives you |
|---|---|---|
| `src/shared/pve/contract.ts` | F, frozen | `PveOptions`/`pveOption`, `PVE_FORCED_OPTIONS`, ids (`HORDE_ID`, `BOSS_PLAYER_ID`, `ADD_PREFIX`, `RECRUIT_PREFIX`, `PARTY_BOT_PREFIX`), `PVE_GROUPS`, `PVE_TIMING`, add codec (`encodeAdd`/`decodeAdd`, 15-number tuple with target slots), `PveView` and its rows, `interpolatePve`, `pveOf`, `PveCommand`/`sanitizePveCommand`, `PVE_WIRE`/`isPveWire`, `PVE_EVENT`, the part interfaces (`HordeApi`, `DirectorApi`, `EconomyApi`, `RecruitsApi`, `PveHost`, `PveNav`) and `PvePartsFactory` |
| `src/shared/pve/balance.ts` | F (S7 retunes values) | Every tunable of design §10 plus `partyFactor`, `waveScale`, `combineMods`, `bossHp`, `damageScale`, `bossDifficulty`, `chipPrice`, `revivePrice`, `OFFER_WEIGHTS`, `DROP_ODDS` |
| `src/shared/pve/rng.ts` | F | `hash32`, `mulberry32`, `PveRng` (`next`, `int`, `pick`, `weighted`, `fork`). Parts draw only from `host.rng(label)`; never `sim.random()`, `Math.random`, clocks or `crypto` (a source scan enforces it) |
| `src/shared/pve/layout.ts` | F | `PVE_MACHINES` (8, with `front` and `dispense` points), `machineBlocks()` (ids `pve-machine-<m>`, hidden by the renderer), `PVE_ENTRIES` (16: vents 0-7, cells 8-11, doors 12-15, with `from`/`exit`/`yaw`, door `leaf` planes and cell `gate` x), `PVE_LAYOUT.bossSpawn` and `.pads` (respawn pads = the 8 player spawn pads), `standCentre`, `STAND_Y`, `faceCentre` |
| `src/shared/pve/bosses.ts` | F | `SIREN_BOSS` and `SIREN_BOSS_CHIPS` (4 chips, rarity `siren`, tag `siren-boss`, not in `CHIPS`), `BossChip.mechanic` and the reserved `empower` modifiers |
| `src/shared/pve/fixtures.ts` | F | Named, seeded `PveView` fixtures (`PVE_FIXTURE_NAMES`, `pveFixture(name,{time,seed,ids,h})`) for every stage, cinematic, party row kind, horde shape and results, plus builders (`fixtureHordeRing`, `fixtureEnteringAdds`, `fixturePartyRow`, `fixtureBossView`, `fixtureBossPlayer`, ...) |
| `src/shared/pve/system.ts` | F, frozen | `PveSystem` facade (below) |
| `src/shared/pve/{horde,director,economy,recruits}.ts` | seeds → S1, S2, S3, S3 | Keep class names and `constructor(host:PveHost)` |
| `src/client/pve/visuals.ts` | F, frozen | `PveVisuals`, `PveVisualsContext`, `PveRigRef`, `playerShown`, `BOSS_FOUND_HIDE_SECONDS` |
| `src/client/pve/{horde-view,world-view}.ts` | seeds → S5, S6 | Keep class names and method signatures |
| `tests/helpers/pve-fakes.ts` | F | `withPveParts`, `fakeParts`, `FakeHorde` (real Rapier add bodies in `chaos.minions`), `FakeDirector`, `FakeEconomy`, `FakeRecruits`, `StraightNav` |

**Facade hooks** (`PveSystem`, called only when `options.pve` is set): `admit`, `onPlayerAdded` (team, `PVE_GROUPS.player`,
Siren fallback, slots, party bots after the first human), `canJoin`/`canStart`, `helloed`, `rejoinSeat`/`adoptSeat`,
`onFightStart`, `filterInput` (holds idle everyone; recruits and party bots use `recruits.inputFor` or idle; the hidden boss
idles, otherwise `director.bossInput`; humans `economy.inputFor`), `holding`/`stepHold` (director timeline, every part's
`shift(dt)`, party horizontal motion zeroed), `stepHorde` (end of `ChaosSystem.step`), `stepAfterPhysics` (economy,
recruits, director), `teamRef`, `hidden`/`setHidden` (disables body and collider), `untouchable`/`setUntouchable`
(plus debug `god`), `damageRoom`, `damager`, `onDamage` (ledger boss damage, recruit → hirer), `onDeath`, `onRevive`,
`hitAdd`, `assistMinion`, `motion`, `rescueFall`, `sirenCrush`, `weaponAway`, `decorate` (`pve`, `intro.boss`, personal
colours, `weaponAway`), `command(id,raw,trusted)`, `debug`, `view`, `reset`, `finish`, `notifyHold`, `onAddKilled`.
Simulation helpers: `revivePlayer(p,hpFraction,pos,event='pve-revive',extra={})`, `removePlayer(id)`, `joinable()`.

**Notes for streams.**
- `resetArena` clears `chaos` (which removes every add body) **before** `pve.reset()`; the horde must not touch old add
  bodies in `reset()`.
- `PveSystem.reset()` runs from the constructor's `resetArena`, so `runIndex` starts at 1 and parts are reset once before
  the first lobby.
- `revivePlayer` emits `pve-revive {target}` by default; pass `event:'pve-respawn'` (and `extra.message`) for respawns,
  or `null` for no event. It calls `economy.onRevive(p)` through the facade.
- Party bots exist from the lobby (option `partyBots`, ids `party-bot-<k>`, seeded non-DEV ranged or melee weapon) and idle
  until `recruits.inputFor` returns input for them.
- The add codec quantizes positions and yaw to 0.01, velocities and `hitYaw` to 0.1, hp to 1%. Absent optional times
  encode as 0.
- Commands never emit `error`; only the `ready`-without-hello refusal does (PvE rooms only).
- `PveSystem.solo` is set at fight start (exactly one connected human and no party bots).
- **D35 overrides the S2 brief:** `PVE_SIREN.play.minRecast.laugh=0`. There is no 20 s Last Laugh floor in phase 4; the
  design text that mentions one (§6.7, §6.9) is superseded. Only the move's own duration and the concurrency caps gate it.
- **Distinct `n` per command:** PartyRoom de-duplicates identical pending events, so every client command must carry a
  fresh nonce `n` (two identical `open` commands without distinct `n` collapse into one on relay rooms).
- **Rejoin:** the client sends `{type:'join',code,rejoin:key,…}` with the room's **exact** stored code (the
  room code from `welcome.code`, not an online code); the server does no code normalization on rejoin. If the old
  seat was the room owner (or the owner is gone), the rejoined id becomes owner and an `owner` message is broadcast.
- **Missing hit info:** `HordeApi.hit` may get `hit===undefined` (fields, DoTs, chip procs, chaos-zombie assists): treat it
  as a torso hit. With `hit.point`, classify from the point (design §3.6); `kind:'blast'` carries `radius`.
- **Floor clamp:** `damage()` clamps to `room=p.hp−hpFloor(p)`; floating point can leave hp a hair above the floor, so the
  director should treat `boss.hp<=floor+1e-6` as reaching it.
- **Holds:** `stepHold` zeroes horizontal velocity of the party, recruits and the boss, and shifts every player's
  burn/poison `next`/`until` and every `untouchableUntil` by `dt`, besides each part's `shift(dt)`.
- **Inputs:** `economy.inputFor(p,base)` and `recruits.inputFor(p,base)` receive the input after the queue pop (and after
  `botInput` for bots), so S3 can edge-detect `interact` presses and releases.
- **Recruit removal:** `recruits.newest()` and `recruits.dismiss(id)→{owner}|null` let S2's boss-seat fallback remove the
  newest recruit; the caller refunds the hirer (`economy.grant(owner,RECRUIT.price,'refund')`).
- **Taunt and shields for add targeting:** `recruits.tauntTarget(pos)` returns the taunting tank id near `pos`;
  `host.untouchable(p)` is true for hidden, revive-shielded and debug-god players (adds should skip them).
- **`host.solo`** is part of `PveHost` (set at fight start).
- **sim.random:** PvE files never call `.random(` (a recursive source scan enforces it). The only allowance is a line that
  deliberately calls into a reused bot brain (botInput/botAbilities draw from `sim.random()`); such a line must carry the
  marker comment `pve-allow-sim-random`.

## Shared-file edit map

Every edit is gated (`this.pve`, `kind==='siren-add'`, an optional field, or an optional argument whose default is the old
behaviour). A rebase onto a newer shared branch re-applies them mechanically. Systems touch (Astra's files):
`simulation.ts`, `chaos.ts`, `siren.ts`, `siren-contract.ts`, `siren-motion.ts`, `bot-abilities.ts`, `standard-melee.ts`,
`stage-hazards.ts`, `movement.ts`, `intros.ts`, `config.ts`, `content.ts`, `host-room.ts`, `client/host-worker.ts`,
`client/browser-room.ts`, `server/index.ts`, `client/renderer.ts`, `client/snapshots.ts`, `client/overhead-health.ts`.

**As applied (deviations from the design table, all PvP-neutral):**
- `#3 sanitizeInput`: `...(raw.interact===true?{interact:true}:{})` instead of `interact:raw.interact===true`, so PvP input
  objects keep their exact shape.
- `#20 revivePlayer` takes two optional extra arguments (`event`, `extra`) and also clears `hitStunUntil`, `lastDamager`,
  `lastSource`, resets gravity scale and sets `staggerImmuneUntil=time`.
- `#42` the new `snapshot()` line sits after `snapshotCore` (before `dispose`).
- `siren.ts:56` sets `m.reach` only when a boost exists (PvP move objects are unchanged); `NO_BOOST` is typed as
  `SirenBoost` (`at:0`).
- `siren.ts` caps (`:46`) are a new line after the existing gate line.
- `snapshots.ts:31` adds `pve` only when `current.pve` exists; `overhead-health.ts` hides only the hidden boss's bar.
- `standard-melee.ts` strike: the add loop is a new line after the player/prop loop, gated on `s.pve`.
- `host-worker.ts` resets `paused` on `create`.

**`src/shared/simulation.ts`** (line numbers at `b6d2f58`):

| # | Anchor | Style | Edit | Why | Gate |
|---|---|---|---|---|---|
| 1 | `:71` `export interface Options{` | clause | add `pve?:PveOptions;` | Mode flag; reaches clients through `snapshot().options` (`:428`) | — |
| 2 | `:74` `export interface Snapshot {` | clause | add `pve?:PveView;` | One additive field | absent in PvP |
| 3 | `:77` `sanitizeInput` return object | clause | add `interact:raw.interact===true` | F key for buy, take, heal and revive | harmless in PvP |
| 4 | after `:85` | new line | ` pve?:PveSystem;` | Field | — |
| 5 | `:86` constructor, before `this.seed=` | clause | `if(this.options.pve)Object.assign(this.options,PVE_FORCED_OPTIONS);` where `PVE_FORCED_OPTIONS={map:'siren',gameMode:'teams',friendlyFire:false,bothDraft:false,skipBotDrafts:true,randomMapEachSet:false,randomStage:false,sandbox:false}` (contract) | The ctor otherwise coerces `'siren'`→`'foundry'`; sandbox semantics (`updateBossSong` `:162`, spawn pads) never mix with PvE | `options.pve` |
| 6 | `:86` constructor, before the final `this.resetArena();` | clause | `this.pve=this.options.pve?new PveSystem(this,this.options.pve):undefined;` | Parts exist before the first reset | `options.pve` |
| 7 | after `:91` | new line | `if(this.pve&&!this.pve.admit(id,bot))return null;` | Party cap of 4; a joining human evicts the newest party bot | `this.pve` |
| 8 | `:96` end of `addPlayer` | clause | `return p;` → `this.pve?.onPlayerAdded(p);return p;` | Team, collision groups, Siren fallback for humans, party-bot creation after the first human | `this.pve` |
| 9 | after `:100` | new line | `if(this.pve)return false;` | No PvP lobby settings in PvE (`configureLobby`) | `this.pve` |
| 10 | `:129` `allied` | clause | each `this.players.get(x\|\|'')` → `(this.players.get(x\|\|'')??this.pve?.teamRef(x))` | Faction friendliness for `HORDE_ID` and add ids (E2) | `teamRef` is never reached in PvP |
| 11 | `:132` `setTeam` | clause | after `{`: `if(this.pve)return false;` | Fixed teams | `this.pve` |
| 12 | `:133` `hasOpponents` | clause | after `{`: `if(this.pve)return this.pve.canStart();` | Solo start; at least one connected human with a hello | `this.pve` |
| 13 | `:135` `setClass` | clause | after the existing Siren fallback: `if(this.pve&&p.spec==='siren')p.spec=CLASS_INFO[cls].default;` | D23 | `this.pve` |
| 14 | `:136` `setReady` | clause | `this.players.size>=2` → `(this.players.size>=2\|\|!!this.pve)` | Solo start | `this.pve` |
| 15 | `:138` `selectSpec` | clause | refusal list += `\|\|spec==='siren'&&!!this.pve` | D23 | `this.pve` |
| 16 | after `:152` (`syncSirenArena(){`) | new line | `if(this.pve)return;` | The map is fixed; it must never reset mid-run | `this.pve` |
| 17 | after `:160` (`if(this.bossSong)return;`) | new line | `if(this.pve){if(this.phase==='fight'&&this.intro?.boss&&this.time>=this.intro.started)this.bossSong={track:'black-siren-protocol',startedAt:this.intro.started};return;}` | Boss song at the PvE boss intro (D22); the PvP rules, including the sandbox branch at `:162`, never run in PvE | `this.pve` |
| 18 | `:181` `resetArena` | clause | `this.chaos.clear();` → `this.chaos.clear();this.pve?.reset();` | Clear PvE state; remove boss and recruit players; next run index | `this.pve` |
| 19 | `:181` `resetArena` | clause | `arenaBlocks(this.options.map)` → `[...arenaBlocks(this.options.map),...(this.pve?.blocks()??[])]` | Machine colliders | `this.pve` |
| 20 | after `:181` | new lines | `revivePlayer(p,hpFraction,pos)`, `removePlayer(id)`, `joinable()` (below) | Revive has no API today (map-sim-net Q5); recruit and boss cleanup | used only by PvE |
| 21 | `:186` `ray` | clause | signature `,ignoreAdds=false`; predicate `&&!(ignoreAdds&&this.chaos.minions.get(this.colliderOwners.get(c.handle)\|\|'')?.kind==='siren-add')` | The Siren and adds see through adds | default false |
| 22 | `:187` `lineOfSight` | clause | signature `,ignoreAdds=false`, passed to `ray` | Same | default false |
| 23 | after `:198` | new line | `if(this.pve?.untouchable(p))return 0;` | Hidden boss, revive shield, debug god | `this.pve` |
| 24 | after `:201` | new line | `if(this.pve){const room=this.pve.damageRoom(p);if(room!==undefined){const s=scale*(kind==='debris'?p.stats.debrisTaken:1);if(room<=0)return 0;if(amount*s>room)amount=room/s;}}` | Hard caps (D2). The amount is clamped before `:202` applies it; `s` mirrors `:202`'s debris factor | `damageRoom` is defined only for the PvE boss |
| 25 | after `:204` | new line | `if(actual>0&&source&&source!==p.id&&!this.players.has(source)&&this.pve)p.lastDamager=this.pve.damager(source)??p.lastDamager;` | Kill feed "killed by Siren minion" | `this.pve` |
| 26 | after `:207` | new line | `if(actual>0)this.pve?.onDamage(p,actual,source,kind);` | Ledger, hurtAt, revive interruption, boss money; runs before the death check on `:208` | `this.pve` |
| 27 | after `:219` | new line | `this.pve?.onDeath(p);` | Beacons, self-revive, defeat, victory, recruit removal | `this.pve` |
| 28 | `:232` `explode` minion loop | clause | append `,{point,kind:'blast',radius}` to the `hitMinion` call | Falloff and gib part for adds | the extra arg is ignored by non-add kinds |
| 29 | after `:265` | new line | `if(this.pve)p.input=this.pve.filterInput(p,p.input);` | Recruit, party-bot and boss brains (for bots, `botInput` already ran on `:265`; the boss brain refines it); idle while choosing, channelling or held. `p.ack` was already taken on `:265` | `this.pve` |
| 30 | `:306` homing | clause | skip condition += `\|\|this.pve?.hidden(target)` | Hidden boss is untargetable | `this.pve` |
| 31 | `:318` projectile→minion | clause | For `minion.kind==='siren-add'`: pass `{point,kind:projectileBodyImpact(shot).kind}`; after `hitMinion`, if `shot.direct&&WEAPONS[shot.spec].blast`, call `explode(owner,point,…)` (the expression of `:327`); if `shot.pierces>0`, `pierces--`, `hit.add(id)`, continue flight; otherwise delete | Projectile parity vs hordes (map-minions §2) | `kind==='siren-add'` |
| 32 | `:336` `botInput` | clause | `this.lineOfSight(…,p.id,opponent.id)` → add `,!!this.pve` | The Siren bot sees through its adds | `this.pve` |
| 33 | `:359` motion rules | clause | `{...p.stats,` → `{...p.stats,...this.pve?.motion(p),` | `groundGroups` (players never ground on add heads) and `groundRay` (the grown boss still grounds) | spread of `undefined` in PvP |
| 34 | before `:361` | new line | `if(pos.y<-6&&this.pve?.rescueFall(p))return;` | A falling boss returns to its spawn without damage (D27); `rescueFall` returns false for everyone else | `this.pve` |
| 35 | after `:383` | new line | `if(this.pve)return;` | The director ends the run itself (avoids the one-team end) | `this.pve` |
| 36 | `:398` countdown→fight | clause | `this.fightStart=this.time;` → `this.fightStart=this.time;this.pve?.onFightStart();` | Opening stage, start money, start picks | `this.pve` |
| 37 | `:409` disconnect forfeit | clause | `if(remaining.length<2)` → `if(remaining.length<2&&!this.pve)` | Defeat is the director's job | `this.pve` |
| 38 | before `:411` | new line | `if(this.phase==='fight'&&this.pve?.holding()){this.updateBossSong();for(const p of this.players.values())this.preparePlayer(p,dt);this.pve.stepHold(dt);this.world.step();this.tickMs=performance.now()-started;return;}` | Hold branch (D21); `started` is the local from `:407` | `this.pve` |
| 39 | after `:411` | new line | `this.pve?.stepAfterPhysics(dt);` | Economy, recruits and director after physics. It is still inside the fight block, which closes on `:413` | `this.pve` |
| 40 | `:412` overtime | clause | `&&!this.options.sandbox` → `&&!this.options.sandbox&&!this.pve` | No overtime (H1) | `this.pve` |
| 41 | `:428` `snapshot` | clause | `snapshot():Snapshot{` → `private snapshotCore():Snapshot{` (the 4.9 KB literal is not edited) | Keeps line 428's literal untouched | — |
| 42 | after `:428` | new line | `snapshot():Snapshot{const s=this.snapshotCore();this.pve?.decorate(s);return s;}` | `decorate` adds `pve`, `intro.boss`, personal colours (D25) and PvE `weaponAway` | no-op in PvP |

New methods (#20):
- `revivePlayer(p,hpFraction,pos)`:
  - clears `death, wounds, knockdown, juggle, roll, rollHeld, hitReaction, attackCommitment, meleeAction, burns, poisons,
    swing, cast, burst, emperor, arcade, leet` (the per-player subset of `resetRound`, `:180`);
  - sets hp and `staggerImmuneUntil`;
  - zeroes `ext`, `move` and the linear velocity;
  - calls `setTranslation(pos)`, where `pos` is a standing capsule centre (floor + `halfHeight()` + `radius()` + 0.05);
  - emits `pve-revive`.
- `removePlayer(id)`: `siren.remove`, `leet.release`, delete from `colliderOwners`, `world.removeRigidBody`,
  `players.delete`, `inputQueues.delete`.
- `joinable()`: `phase==='lobby'&&players.size<C.maxPlayers&&(!this.pve||this.pve.canJoin())`.

**Other shared files** (all F; line numbers at `b6d2f58` [V]):

| File:anchor | Style | Edit | Why | Gate |
|---|---|---|---|---|
| `chaos.ts:7` `MinionView.kind` | clause | `'zombie'\|'toy'` → `'zombie'\|'toy'\|'siren-add'` | Adds share the minion map, so every existing damage path reaches them | type only |
| `chaos.ts` after `:17` (`once`) | new line | `forget(key:string){this.used.delete(key);}` | D37 | — |
| `chaos.ts:60` `summon` cap | clause | `this.minions.size>=34` → `[...this.minions.values()].filter(m=>m.kind!=='siren-add').length>=34` | A horde must not block chip summons | — |
| `chaos.ts:62` `hitMinion` | clause | signature `,hit?:AddHitInfo`; after the existing guard: `if(m.kind==='siren-add'){this.sim.pve?.hitAdd(m,damage,owner,impulse,dir,hit);return;}` | Gibs, money, death list | kind |
| `chaos.ts:63` `melee` | clause | for adds pass `{point,kind,melee:true}` using the contact formula of `simulation.ts:299` | Hit location for melee gibs | kind |
| `chaos.ts:72` step loop | clause | first statement `if(m.kind==='siren-add')continue;`. Target predicate += `&&!s.pve?.hidden(q)`. `if(!q)continue;` → `if(!q){s.pve?.assistMinion(m,p,dt);continue;}` | The horde owns add AI (an add's owner `HORDE_ID` is not a player, so this loop would remove it); D38; never chase the hidden boss | kind, `pve` |
| `chaos.ts` after `:72` | new line | `s.pve?.stepHorde(dt);` | Add AI before physics | `pve` |
| `chaos.ts:74` `views` | clause | `.filter(m=>m.kind!=='siren-add')` | Adds use the compact PvE view | — |
| `siren-contract.ts:76-100` | clause | `SirenPlayerState.boost?:SirenBoost` with `SirenBoost={scale:number;reach:number;cooldown:number;at:number;minRecast?:Partial<Record<SirenMoveId,number>>;maxRains?:number;maxEruptions?:number}`. `SirenMoveState.reach?:number` | Chips 3 and 4 (Claude contract file; additive) | optional fields |
| `siren.ts` new line before the class | new line | `const NO_BOOST={scale:1,reach:1,cooldown:1};const boostOf=(p:Player)=>p.siren?.boost??NO_BOOST;` | — | — |
| `siren.ts:29` `ready` | clause | `&&!this.sim.pve?.hidden(p)` | Hidden boss | `pve` |
| `siren.ts:40,:62,:63,:86` | clause | the `lineOfSight` calls in `visibleTarget`, `melee`, `area` and the laugh catch pass `ignoreAdds=!!this.sim.pve` | The Siren sees through its adds | `pve` |
| `siren.ts:45` `aim` | clause | both `s.ray(…)` calls pass `ignoreAdds=!!s.pve` | Eruption and rain aim must not land on the first add in front of the boss | `pve` |
| `siren.ts:46` `start` | clause | refuse beyond `boost.maxRains`/`maxEruptions` | P18 caps | boost |
| `siren.ts:56` | clause | `a.ready[id]=at+(…)` → `a.ready[id]=at+Math.max((…)*boostOf(p).cooldown,boostOf(p).minRecast?.[id]??0)`; the move gets `m.reach=id==='laugh'?1:boostOf(p).reach` | P18; D35 (laugh lunge unboosted) | no boost → identical math |
| `siren.ts:58` `avoid` | clause | `\|\|this.sim.pve?.untouchable(t)===true` | Revive shield, debug god and the hidden boss also stop Siren hits, projectiles and the laugh catch (all call `avoid`, `:59,:86,:103` [V]) | `pve` |
| `siren.ts:62-65,:68,:74-80,:84-86,:92,:107` | clause | `boostOf(p).reach` × melee `range` (`:84`), rush radius (`:85`), slam (`:75`), eruption (`:79`), rain (`:80`), patch (`:64`), damnation blast (`:107`), laugh **catch** radius (`:86`), projectile radius (`:68`), event radii (`:74`,`:75`,`:107`). `boostOf(p).scale` × vertical gates 1.9 (`:62`) and 4 (`:63`), `SIREN_SPAWN` offsets (`:65`), victim pin offset (`:92`). The laugh lunge range (`:77`) is not scaled | P17, map-siren §2, D35 | no boost → identical math |
| `siren.ts:93` crush | clause | `if(s.time>=g.crushAt){` → `if(s.time>=g.crushAt){if(s.pve){s.pve.sirenCrush(p,t);this.event(p,'siren-crush',add(t.body.translation(),v(0,.65,0)),{target:t.id});this.release(g);continue;}` | D35: the crush goes through `damage()` | `pve` |
| `siren.ts` after `:34` (`clear`) | new line | `clearEffects(p:Player){…}`: `cancel(p)` (releases p's grabs, `:32` [V]) and drops p's patches, eruptions, rains, marks and siren projectiles, but keeps `p.siren` (unlike `remove`, `:33`) | Hold start and possession must not leave grabs or hazards behind | used only by PvE |
| `siren-motion.ts:9` | clause | `distance=(rush?B.rush.distance:B.laugh.range)*(move.reach??1)` | Rush reach (bot only; never predicted). Laugh moves carry `reach:1` | optional field |
| `bot-abilities.ts:28` | clause | slot 1–3 melee range gates ×`(state.boost?.reach??1)`; slot 4 (laugh, 7 m) unchanged | The bot uses the bigger reach | optional |
| `standard-melee.ts:79-87` `strike` | clause | after the player/prop loop, loop `s.chaos.minions` with `kind==='siren-add'` in range, arc and LOS, then `hitMinion(…,{point,kind,melee:true})` | Melee specials kill adds (they never hit minions today) | kind |
| `standard-melee.ts:123` | clause | pass `{point,kind:'pierce'\|'blunt'}` | Thrown gibs | kind |
| `stage-hazards.ts:31-34` | clause | skip `this.sim.pve?.hidden(p)` | Hidden boss | `pve` |
| `movement.ts:8` `MotionRules` | clause | add `groundGroups?:number;groundRay?:number;` | — | optional |
| `movement.ts:11` ground ray | clause | length `C.playerHalfHeight+C.playerRadius+.16` → `stats.groundRay??C.playerHalfHeight+C.playerRadius+.16`; the 5th `castRay` argument `undefined` → `stats.groundGroups` | Humans never ground on adds; the grown boss grounds. Both fields are undefined for every standard capsule, so the expression and the client predictor stay bit-identical | optional |
| `intros.ts:34-43` | clause | `IntroSequence.boss?:boolean`. `introFrame` accepts `phase==='fight'&&state.intro?.boss===true` and returns `boss:true` | Mid-fight boss intro reuses PlayerIntros, the HUD branch and audio (map-client-ui §2b) | `boss` is set only by PvE |
| `config.ts:45` | clause | `Input.interact?:boolean` | F key | optional |
| `content.ts:152` `offerChips` | clause | 5th param `opts?:{weights?:Partial<Record<Rarity,number>>;exclusives?:boolean}`. When `opts.weights` is present it **replaces** the weight function: `weight=(r)=>opts.weights![r]??0` (exotic tiers included; missing tiers weigh 0). The exclusive branch is skipped when `opts.exclusives===false`. Without `opts`, the code path and `random()` consumption are unchanged | Drop-tier odds and rainbow | optional |
| `host-room.ts:15` constructor | clause | settings type += `pve?:unknown;local?:unknown`. Simulation options += `pve:pveOption(settings.pve,{seed:crypto.getRandomValues(new Uint32Array(1))[0],local:settings.local===true,relay:settings.local!==true,debug:settings.local===true&&developer!==false})`. New field `local=settings.local===true` | Browser-hosted and offline TEST BOSS | — |
| `host-room.ts:17` `join` | clause | `\|\|this.sim.phase!=='lobby'` → `\|\|this.local\|\|!this.sim.joinable()` | Party cap; offline rooms take no guests | — |
| `host-room.ts` before `:24` | new line | `if(msg.type==='ready'&&this.sim.pve&&!this.sim.pve.helloed(id)){this.emit(id,{type:'error',fatal:id!=='host',message:'Update the game to join Boss Test rooms.'});return false;}` | D33 | `pve` |
| `host-room.ts` before `:28` | new line | `if(isPveWire(msg))return this.sim.pve?.command(id,msg.pve,this.local&&id==='host')??false;` | D32; debug trusted only offline | `pve` |
| `host-worker.ts` | clause | `let paused=false`; new branch `else if(m.type==='pause'&&room?.local)paused=m.paused===true;`; the clock steps only when `!paused` | Offline pause (D31) | `local` |
| `browser-room.ts:7` | clause | `settings` type += `pve?:unknown` | Threading | — |
| `server/index.ts:29` `/health` | clause | += `pveRooms` (count of rooms with `sim.pve`) and `maxTickMs` | D39 | — |
| `server/index.ts` after `:43` | new line | Rejoin branch: `if(msg.type==='join'&&typeof msg.rejoin==='string'){…}` (§9.3) | D33 | `pve` |
| `server/index.ts:44` create | clause | refuse when `msg.pve!==undefined` and PvE rooms ≥ `PVE.maxRoomsPerServer`. Simulation options += `pve:pveOption(msg.pve,{seed:randomBytes(4).readUInt32LE(0),debug:sandbox})` | D30, D39 | — |
| `server/index.ts:44` join | clause | after the room lookup: `if(room.sim.pve&&!room.sim.joinable()){room=undefined;return fail('This boss test party is full or has started.');}` | Party cap | `pve` |
| `server/index.ts` before `:51` | new line | `else if(msg.type==='ready'&&room.sim.pve&&!room.sim.pve.helloed(id)){fail('Update the game to join Boss Test rooms.');ws.close(4001,'Update required');}` | D33: an outdated client is removed from the lobby (lobby disconnects delete the player, `simulation.ts:177`) instead of blocking the start | `pve` |
| `server/index.ts` before `:53` | new line | `else if(isPveWire(msg))room.sim.pve?.command(id,msg.pve,room.owner===id);` | D32, D29 | `pve` |
| `client/renderer.ts:63,79,100,110,113,131` | clause | `pve=new PveVisuals(this.scene)`. `makeProp`: ids starting with `pve-` hide the box mesh but keep `userData.collider=true`. After the rig animation in the players loop: `r.group.visible&&=this.pve.decoratePlayer(r,p,state)`. After `chaosVisuals.update`: `this.pve.update(state,{camera:this.camera,me,dt,quality:this.effectsQuality,compatibility:this.compatibility,replay,rigs:this.players,burst:this.spellBurst})`. Events loop: `this.pve.event(event,state)` | All PvE drawing lives in PvE files | no `pve` → no-op |
| `client/snapshots.ts:31` | clause | return object += `pve:interpolatePve(a.pve,b.pve,current.pve,f,extra)` | Adds lerp by `n` (map-renderer §0) | undefined in PvP |
| `client/overhead-health.ts:13-19` | clause | skip the boss while `pveOf(state)?.bossView?.hidden`; bar height `p.pos.y+1.3*(p.siren?.boost?.scale??1)` | Hidden or grown boss | — |

Not edited (removed from revision 1): `relay-protocol.ts`. PvE commands use the whitelisted `chip` type (D32), and the
`coalesceInput` key list matters only to the legacy HttpsRoom v1, which production does not use
(`https-room.ts` imports it; PartyRoom concatenates input batches instead, `party-room.ts:15` [V]).

**Files deliberately not touched:**
- `teams.ts`, `relay-protocol.ts`, `relay/*.ts`
- `body-damage.ts`, `body-wounds.ts`, `gibber-vfx.ts`: pinned Pilot systems; adds get their own FX in S5
- `draft-stage.ts`, `draft-presentation.ts`: PvE writes its own variants
- `snapshots.ts` beyond the one clause above
- `prediction.ts`

## Baselines (recorded on the untouched base `b6d2f58`, Node 22.22.2, 2026-10-02)

**`npm test` (full unit suite, `--test-concurrency=2`): 13 failing tests**, all pre-existing (the docs pin Node 24.19.0;
12 fail there per earlier records):
1. 182 catalog entries have a real effect, eight rarities and useful weapon eligibility
2. 182 unique chips, compatibility, caps, and explicit multiplicative stacking
3. 50 behavior Chips are distinct and all are consumed by the authoritative engine
4. Demon Step moves substantially farther, matches prediction, respects cooldown and walls, and needs access
5. all bundled effects are valid audible PCM with peak headroom and distinct samples
6. blocking sword and greatsword swings throws the attacker back; Rebound Engine increases the distance
7. dagger primaries and echoes alternate server sequence and leading-hand poses
8. guest disconnect releases lobby slot and forfeits active match after timeout
9. portable update verifies the package and every path, installs atomically, skips current versions, and survives offline
10. reference poses have a straight high kick, turning uppercut, flat landing, and animated broken halo
11. shield real attack applies both elemental DoTs, ticks are nonrecursive and reset clears them
12. twenty named music tracks are distinct nonempty MP3 assets
13. vertical platforms fit default jump height and breakable supports release suspended sections

**Network suites** (`node tests/run-network.mjs <suite>`, `TEST_PORT` set per run):

| Suite | Result on the base |
|---|---|
| default (`tests/network.ts`) | FAIL: `Timed out: rematch` (known) |
| maps | pass |
| party | FAIL: `party-network.ts:14` assertion `7 !== 1` |
| intros | pass |
| first-block | pass |
| chaos | FAIL: cosmetics assertion (`players[0].cosmetics?.hat==='wizard'…`, known) |
| rarity | pass |
| motion | pass |
| block | FAIL: `block-network.ts:7` assertion `65.5 !== 77` |
| debris | pass |
| elements | pass |
| `siren-network.ts`, `killcam-network.ts`, `body-damage-network.ts` | pass |
| `npm run test:https` | pass |

After the foundation (head of `claude/pve-foundation`): the same 13 unit failures by name; network suites match the
base except `party`, which passed (its base failure is a snapshot-hash divergence assertion that is timing dependent).
The four `server:build` errors are the same (line numbers shift by the inserted lines).

**`npm run server:build`: 4 pre-existing errors** (exit 2): `chaos.ts(26,265)` TS7006, `leet.ts(27,486)` TS2322,
`leet.ts(27,663)` TS2345, `standard-melee.ts(106,175)` TS7006.

## Integration (S7, 2026-10-03)

**Merge.** `claude/pve-integration` = `claude/pve-foundation` (`2484995`) + `--no-ff` merges of `claude/pve-horde`
(`4210963`), `claude/pve-director` (`0d97649`), `claude/pve-support` (`bbb1354`), `claude/pve-horde-visuals`
(`da32e28`), `claude/pve-world-visuals` (`54eb934`), `claude/pve-ui` (`6d62613`), in that order. No textual conflicts.
`origin/claude/visual-revision` was still `b6d2f58`, so the rebase protocol (regenerate golden hashes) was not needed;
`tests/pvp-golden.test.ts` passes unchanged.

**Cross-stream requests applied.**
- Seed-era assertions follow the real parts: the foundation's 3,600-tick run keeps debug `god` on (the real horde kills an
  idle human) and accepts stage `opening|wave`; the facade metrics check counts the real horde view.
- `layout.ts`: door `leaf.z = s·26.8` (the rendered leaf plane; on the hall side of `from`); vent `exit` = the climb stand
  point past the pit edge (`centre + fwd·(1.08/max(|sin|,|cos|)+0.38)`, the client's `ventStand`). Because the exit moved
  1.5-1.9 m off the vent centre, `HORDE.ventTravel.nearVent` is 2.0 (was 1.2) so vent travel stays reachable.
- `EconomyApi.pendingHiresOf?(owner)` is in the contract (optional; recruits no longer cast).
- `Renderer.setGraphics` forwards the mode to `pve.horde.setGraphics`.
- `PVE.bossIntroExtra` 1.8 s (was 0.3) so THE SIREN title is readable before the fight resumes.
- Fixtures: a `channelling` row revives the first downed member of the party (was hard-coded `p2`). (Removed 2026-10-03 with the instant revive.)
- `tests/fixtures/pve-horde-60.json`: 60 frames at 10 Hz of a seeded wave (players and the `pve` view) for the labs.

**Integration defects fixed.**
- `horde.ts kill()`: the killed add's Rapier body handle is dropped (`a.minion=undefined`, `a.pos` kept). The tick's
  separation hash could still read the removed body and Rapier panicked (`RuntimeError: unreachable`; found by the balance
  harness at h=4, seed 5).
- `nav.ts seedCell()`: a flow field toward a player standing in a cell the conservative inflation closes (the 2 m service
  corridors between the cells and the outer walls) now seeds from the first free cell reached through physically walkable
  cells (BFS over a `loose` grid inflated by the radius only). Before, adds and the boss treated such a player as
  unreachable and the run stalled (the harness human hid there for 10+ minutes).
- New debug op `boss-damage:n` (D29 set; director): damages the visible boss through `damage()` (floors and reveals apply),
  credited to the first connected human. Client: `DEBUG_OPS.bossDamage`, `window.chipArena.pve.bossDamage(n)`. The
  real-server suite and the offline smoke use it to reach the victory.

**Balance retune** (S7 owns `balance.ts`; harness below). Changed: `WAVES` 22/28/34 adds at 0.5/0.6/0.7 per s with
`maxAlive` 10/14/18 (design 14/18/22 at 1.2/1.5/1.8, 14/18/22 alive: waves lasted ~30 s); shambler damage 6 (7), runner 4
(5); `PVE_SIREN.damageScale` .2/.22/.24/.26 (.6/.7/.8/.9) and `hpPerExtraMember` .5 (.7), so boss HP is
6000/9000/12000/15000 for h=1..4. Tests that pinned the design numbers now pin these values.

**Balance harness** (`npm run test:pve-balance`, `tests/pve-balance.ts`; not in `npm test`). For h=1..4: one human driven by
the party-bot brain (`partyBotInput`, plus "walk toward the visible boss" when it has no target) and h−1 party bots;
weapons pistol, rifle, greatsword; seeds 1-5; 12-minute cap. The human takes offer 0 of every pick and shops at each
intermission (recruits up to its cap, then chips; teleported to the machines, standing in for the walk). Medians over 5
seeds with the final values (`P1`-`P4`: seconds per boss phase; `boss s`: boss stage length of the runs that ended):

| h | weapon | wave 1 s | wave 2 s | wave 3 s | deaths before boss | money at boss | P1 s | P2 s | P3 s | P4 s | boss s | victories |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | pistol | 54 | 57 | 59 | 0.0 | 95 | 73 | 45 | 39 | 31 | 118 | 2/5 |
| 1 | rifle | 60 | 60 | 70 | 0.0 | 111 | 62 | 10 | 73 | 68 | 158 | 4/5 |
| 1 | greatsword | 53 | — | — | 2.0 | — | — | — | — | — | — | 0/5 |
| 2 | pistol | 59 | 65 | 70 | 0.0 | 192 | 39 | 16 | 50 | 44 | 93 | 2/5 |
| 2 | rifle | 65 | 64 | 69 | 0.0 | 168 | 50 | 18 | 34 | 71 | 116 | 0/5 |
| 2 | greatsword | 69 | 65 | 68 | 1.0 | 39 | 71 | 26 | 38 | 75 | 94 | 1/5 |
| 3 | pistol | 67 | 69 | 74 | 0.0 | 121 | 45 | 15 | 60 | 55 | 129 | 2/5 |
| 3 | rifle | 68 | 71 | 80 | 0.0 | 149 | 116 | 15 | 97 | — | 133 | 0/5 |
| 3 | greatsword | 72 | 76 | 78 | 1.0 | 44 | 59 | 11 | 39 | — | 71 | 0/5 |
| 4 | pistol | 71 | 73 | 82 | 0.0 | 265 | 83 | 15 | 57 | 83 | 124 | 1/5 |
| 4 | rifle | 73 | 78 | 81 | 0.0 | 140 | 116 | 20 | 72 | 151 | 134 | 1/5 |
| 4 | greatsword | 75 | 78 | 84 | 1.0 | 46 | 90 | 45 | 219 | — | 90 | 0/5 |

Reading it against the design targets:
- **Waves 60-150 s:** met for h=2..4 (59-84 s); solo waves are 53-70 s (at the lower edge). Before the retune they lasted
  25-40 s.
- **At most 1 expected solo death before the boss:** met for ranged weapons (0 deaths in all 10 solo pistol/rifle runs).
  Not met for the solo greatsword: the party-bot melee brain stands in the crowd and dies twice in wave 1 (a harness brain
  limitation; melee party bots in larger parties do survive the waves).
- **Boss 3-6 min:** not met by the harness. 13 of 60 runs win (median boss stage of the wins 148 s, range 81-412 s); 43 end
  in a party wipe, 4 hit the 12-minute cap mid-fight. The deaths come from the Siren's ranged moves (Mark, Hellfire,
  Damnation, Eruption) that the brain never dodges, blocks or rolls out of, and from the Siren reflecting the brain's
  shots. With the design's damage scale (.6-.9) every run wiped within ~40 s of the boss stage; at .15-.2 the boss phases
  last 2-4 minutes. The retune keeps boss damage at about a third of the design so real players who dodge are not
  trivialised; the owner's playtest decides (each value is one constant in `balance.ts`).
- Money at boss start (after shopping): 40-270. Unspent, the human reached the boss with ~1,100-1,800.


**Network results** (Node 22.22.2, this container):

| Suite | Result |
|---|---|
| `run-network.mjs` default | FAIL `Timed out: rematch` (known baseline) |
| maps, party, intros, first-block, rarity, motion, debris, elements | pass (party also passes; its base failure is timing-dependent) |
| chaos | FAIL cosmetics assertion (known baseline) |
| block | FAIL `65.5 !== 77` (known baseline) |
| **pve** (new, real server) | pass: 810 shared ticks identical, raw snapshot p95 32.4 KB (max 32.4 KB), deflated p95 5.6 KB, old client refused at ready (4001), third PvE room refused, guest debug ignored, mid-fight rejoin with key, victory |
| `siren-network.ts`, `killcam-network.ts`, `body-damage-network.ts`, `npm run test:https` | pass |
| `pve-loopback-network.ts` | pass: 327 + 297 (boss half: reveal 1, intro, chip 2) identical snapshots; real-server part unchanged |
| `pve-relay-network.ts` | pass: part 2 guest reaches the boss through PartyRoom v2, 888 host packets all valid, max 4 adds in view at the boss |
| `npm run test:pve-perf` | ok: avg step 0.51 ms, p99 1.18 ms, PvE share 0.17 ms, snapshot p95 49.9 KB at 40 adds |

The `pve` suite's sizes are measured with few adds (skip-to-boss); the perf script's 49.9 KB p95 is the 40-add figure.

**Real-app smoke** (Playwright + SwiftShader, offline worker room; screenshots in the session scratchpad
`after/pve-integration/`, not committed):

Driven through `window.chipArena` with real input (camera by synthetic mouse deltas through a test-only pointer-lock shim;
movement and F through `setInput`; DEV debug ops through a throwaway signing key served in place of the dev public key —
test harness only, nothing committed). 36 screenshots, one run end to end (session scratchpad; the Playwright script is not committed): home, TEST BOSS picker, personal start chip
screen, wave 1, vent climb (framed at the climb start; the add is not clearly visible), wave adds, chip dispense and pickup, dispensed-pick chip screen, Choose later and the CHIPS
icon, the red "under attack" chip screen, recruit pod with the medic row, kill cam, downed with self-revive countdown, WAVE
CLEARED respawn banner, reveal smile and chip-1 card ("The Siren is here..."), boss intro, THE SIREN boss bar with its
glitch title, "Siren has Spawned", phase 1, chip 2 reveal, HIDDEN — FIND HIM, found and chip 3, grown phase 3, chip 4 and
the no-cooldown phase, THE SIREN IS SILENCED, victory results (portrait, chips, damage, MVP, Retry / Return to Menu), Retry
into a new run, self-revive, defeat outro (PARTY WIPED) and defeat results. Metrics in a wave (SwiftShader, not
meaningful for fps): fps 20.7, draws 324, snapshotBytes 24.3 KB, tickMs 0.4, pveAdds 11, fullRigs 4, nearImpostors 0.

Judgement: the UI flow, HUD, chip screens, reveal cards, boss bar and results look right. Found and fixed: combat numbers
froze on screen at low frame rates (snapshot clock, see `snapshots.ts`; PvE only). Still seen under SwiftShader only:
numbers linger after a multi-second screenshot stall (no frames rendered meanwhile). The boss intro frames captured were
dark: the Siren intro is dark by design between flashes (siren-lab shows the same black frames). The harness framing missed
the dispensed chip (hidden behind the pilot), the grown boss and an identifiable possession hint; S5/S6 lab images cover
those close up. An open personal chip screen stays over the boss intro if a pilot never picked (seen with skip-to-boss).

**Verification** (Node 22.22.2):

- `npm run check:types`: pass.
- Full unit suite (`node --import tsx --test --test-concurrency=2 tests/*.test.ts`): 909 tests, 896 pass, 13 fail — exactly
  the 13 baseline names. `tests/pvp-golden.test.ts` passes unchanged.
- New: `tests/pve-flow.test.ts` (victory flow twice with identical hashes, defeat variant, debug-op boss flow; also
  `PVE_LONG=1`), `tests/pve-integration.test.ts`.
- `npm run server:build` / `build:check`: the 4 known errors (`chaos.ts(28)`, `leet.ts(27)` ×2, `standard-melee.ts(107)`;
  lines shifted by the foundation's inserted lines).
- Not run: `npm run verify` as one command (its parts ran individually above); Node 24.19 (this container has 22.22.2).

**Not run** (environment): native Windows GPU frame rate and look; real multi-machine internet play; desktop Electron
"Host online" (needs a Windows test build); browser hosting on the deployed Site (needs a Site release); audio on
hardware; the hosted two-browser smoke (no test-room path from the real client: the client never sends `testRoom`, so
debug ops are unavailable there; the real-server suite covers the hosted flow with two WebSocket clients instead).

## Owner playtest fixes: money and instant revive (2026-10-03, branch `claude/pve-econ-revive`)

Owner feedback: "make more money ... its very minimal" and "holding E on reviving ... only works for a split second ...
[Im fine with instant revive]".

**Why the hold revive broke.** The interact key is **F** (E is the special ability, `secondary`; the HUD shows `E · SPECIAL`).
The server started the 2.5 s channel on the F press edge and cancelled it when F was released. The client's `pveIdle()`
returned true as soon as the authoritative party row carried `channel`, and an idle input has no `interact`, so one
snapshot round trip after the channel started the server saw F "released" and cancelled it. Holding E did nothing at all.

**Revive is instant now.** One F press within `PRICES.reviveRadius` (2.2 m) of a downed teammate's beacon (human or party
bot) charges `revivePrice(revived)` and revives at once: 50% HP at the death spot plus the 1.5 s shield, `pve-revive`
`{source, amount}`. Too little money: `pve-deny 'money'`, nothing charged. The channel state, its forced idle,
`PRICES.reviveSeconds`, the prompt progress ring, the beacon `channel` mode, the HUD "REVIVING" status and the
`channelling` fixture kind are removed (`PveChannel`/`PvePartyView.channel` stay in the contract, never set). Prompt:
`F · Revive <name> $X`; beacon label `F · REVIVE` over `$X`. The client also latches an F tap shorter than a frame into the
next input (`pendingInteract` in `main.ts`), so a quick tap is never lost at low frame rates.

**Money** (`balance.ts`), income about 2.5×, prices about 1.2×:

| Value | Before | Now |
|---|---|---|
| Add reward shambler / runner / brute | 10 / 8 / 35 | 25 / 20 / 80 |
| Head bonus | 5 | 10 |
| Start money | 50 | 150 |
| Wave clear (wave w) | 40 + 20w (60/80/100) | 100 + 50w (150/200/250) |
| Boss threshold bonus (×3, everyone) | 100 | 250 |
| Boss damage | $4 per 1% | $10 per 1% |
| Possessed kill (killer / others) | 150 / 50 | 300 / 100 |
| Chip price | 150 + 25 per buy, max 300 | 175 + 25 per buy, max 350 |
| Recruit / heal | 250 / 60 | 300 / 75 |
| Revive | 100 + 50 per revive of that member, max 300 | 150 + 50, max 400 |

Balance harness (`PVE_BAL_H=1,2 PVE_BAL_W=pistol,rifle PVE_BAL_MAX_MIN=12 npm run test:pve-balance`, seeds 1-5, medians;
the harness now also logs earned and spent at boss start and earned in total; it shops at each intermission: recruits to
its cap of 2, then up to 3 chips):

| h | weapon | earned at boss (before → now) | bought before boss (before → now) | money left at boss (before → now) | earned whole run (before → now) |
|---|---|---|---|---|---|
| 1 | pistol | 1512 → 3406 | 2 recruits + 4 chips → 2 + 8 | 94 → 634 | 2924 → 6509 |
| 1 | rifle | 1436 → 3234 | 2 + 2-4 → 2 + 7-8 | 111 → 305 | 2944 → 6512 |
| 2 | pistol | 1450 → 3346 | 2 + 2-4 → 2 + 6-9 | 210 → 996 | 3139 → 8323 |
| 2 | rifle | 1243 → 3161 | 2 + 2-4 → 2 + 7 | 163 → 626 | 2304 → 5837 |

During the boss a solo player earns about $2,500-3,000 more (thresholds $750 plus $10 per 1% plus adds), enough to keep
buying chips, heals and revives. Wave lengths, deaths and victories are unchanged within seed noise (money does not
change the simulation except through what the harness buys).

## Stage music (2026-10-03, branch `claude/pve-batch2`)

Owner: "lets replace the music on the stage to the one attached- until Siren spawns of course :)". In a PvE run on Ward
Zero the client (`ArenaAudio.updateStageMusic`, `ArenaMusic.setStage`) loops `public/music/restricted-zone-pulse.mp3`
(`PVE_STAGE_TRACK`, kept out of `MUSIC_TRACKS`) from the first in-game phase (specialization, intros, countdown, opening,
waves, intermissions, the chip-1 reveal) until `bossSong` is set at the Siren's intro tick; the Black Siren Protocol
boss song then takes over exactly as before. The lobby keeps the menu music. Under a player intro sting the stage track
plays at 35% of the music level. It rewinds in lobby/match, at the boss and on entering a game, so Retry and new runs
start it from the top. Music volume and mute apply. PvE also pre-decodes the boss song during the run
(`BossTheme.preload`) so it starts on its first frame (offset 0) at the intro instead of after a mid-intro decode. A
failed load falls back to the normal rotation. Server, wire and simulation are unchanged.

**Open (publication blocker):** with the 4.1 MB track the gzip update payload is about 26.1 MiB, so
`scripts/package-update.mjs` (end of `npm run build`) refuses it (hosting limit 25 MiB per file; 22.2 MiB without the
track). The owner decides between a smaller encode/trim of the track (about 2.7 MB fits) or another payload change.

## Rendering constants (S5 and S6 mirrors)

`src/client/pve/horde-view.ts`:

```ts
HORDE_RENDER={maxRigs:{quality:24, performance:16, compatibility:6}, fullRadius:{quality:20, performance:14, compatibility:14},
 hysteresis:4, shadowRadius:15, throttleRadius:12, impostorMax:64, impostorRadius:70, createPerFrame:2, prewarmMs:3,
 spareRigs:4, shadowRigs:12}
```

`hordeMode(compatibility, graphics)` maps the renderer's graphics setting to `quality|performance|compatibility`
(`Renderer.setGraphics` forwards it). Add states on the wire: `entering|active|crawl|stagger|vent|dead`. The client minion
view (`SirenMinionView`, `src/client/siren/minions/motion.ts`) adds PvE fields `side` (swiping arm), `lost` (LIMB mask),
`crawl`/`crawlAt`, `entry` (`vent|door|cell` climb or walk-out, `rimZ` for vents) and `diveAt` (vent dive); poses follow
`PVE_TIMING` (swipe hit 0.34 s, vent climb 1.95 s, dive 0.9 s, door 0.6+1.2 s, cell 0.5+1.0 s).

## Follow-ups

- Owner playtest of the retuned balance (the harness drives humans with the party-bot brain; see the table notes).
- Party bots and recruits with melee weapons are weak against crowds (harness: the solo greatsword human dies in wave 1).
- A hosted two-browser smoke needs a test-room path in the client (or a DEV-account flag) to use debug ops.
