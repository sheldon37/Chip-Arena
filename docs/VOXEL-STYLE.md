# The voxel style: kept, guarded, and not where new work goes

**Owner direction (2026-11-18):** "I want to essentially remove voxel entirely BUT keep its data incase I use it for a map or game mode. The voxel player model + weapons can be selected as skins or costumes instead. All the new things ive been making, I want to remove the voxel dependency and move forward with a more detailed framing."

The plan is `docs/VOXEL-RETIREMENT.md` (approved with every default); Astra's part is `docs/VOXEL-RETIREMENT-ASTRA.md`. This page is the rule new work follows and the guards that hold it (package V0).

## The new-content rule
- **New work is authored, timed and socketed on the model:** the bake's skeleton and motion bank, the life-size weapon models (VisualWeapons, the weapons pack bank), mesh banks and modelled props.
  - NOCTIEL, THE HUM, THE DEEP END, the boss relics and Thunderheart Carillon are the pattern: their own skeletons or banks, their own sockets, no rig reads.
- **Do not add** a Classic or no-bake branch, a read of the 22-joint rig's bones as the source of a pose or an anchor, a CapturedRig pose, a voxel-box measure, a voxel model id, or a `WeaponShape.Cube` prop.
- **Where a package must still read the voxel layer today,** its DEV_LOG entry names it under "Voxel dependency", and the ratchet's pin moves on purpose in the same commit.
- **The baseline** is the plan's audit of new content (section 1H). Everything voxel drawn today has a row in the plan's inventory (section 1) and a package that moves it.
- **Never delete voxel data or code.** It is kept, checked and loadable for a future voxel map or mode, as the costume (V2) and as the body of builds without the bake.

## What the voxel style is
The kept voxel data plus the legacy rig path, as one optional style (plan 2.4). Builds without the licensed bake draw it by default; the DEV switches draw it on purpose.
- **Data, byte-identical:** SourceModels (140 models: `pilot`, the Prototype weapons, hats, the horde bank, summons, maps, wards, entrance stages, death scenes), SourceMotion, SceneMotion, WoundParts, HazardModels, SceneryMotion, WardMotion, SlenderRig, SirenLook; the generated rig tables CapturedClips, NativeKillBoxes, NativeBroadcastEdges and the 11 NativePack* tracks.
- **Code, at its paths:** CapturedRig and CapturedAnimation, the voxel body sculpts and skins, the voxel horde kinds and gore surface, the voxel bisect and carve, NativePortraits, the box scenery builders, the unused M1 stage kit (the ratchet's `style` lines), and the voxel branches inside the shared files.
- **Switches:** `--ca-voxel-bodies` (every pilot voxel on this PC), `--ca-voxel-zombies` (voxel zombies), Settings · Player model "Classic voxel" (kept until the low-end check, O3), the map's `debris:"voxel"`. In BoZo captures: `voxel=1`, `zvoxel=1`.

## The guards (in every full CoreTests run, CI included)
### The voxel ratchet (`Tests/VoxelRatchetChecks.cs`, `Tests/voxel-ratchet.txt`)
- It pins, per Core and Runtime file outside the voxel style, the counts of the plan's five token families:
  - **gate:** `ClassicBodies`, `VisualBodiesOn`;
  - **measure:** `CapturedRig.Boxes`, `WoundedPilot(`;
  - **read:** `rig.Bones[`, `viewRig.Bones[`, `introRig`, `draftRig`, `hordeQueueRig`, `Rigs[`, `Player.Bones[`;
  - **author:** `new CapturedRig.Pose(`, `new MeleeRig(`, `.AddE(`, `.WritePose(`, `CapturedRig.Sample(`, and pose edits `.Rotation[`, `.Position[`, `.P[`, `.Q[CapturedRig.`;
  - **draw:** `WeaponShape.Cube`, `QueueHeld(`, and `Queue("weapon-`, `"loose-`, `"hat-`, `"pilot`, `"horde-`, `"summon-`.
- **Tokens, not types:** the text is counted as it stands, comments included. A comment that names `rig.Bones[` counts.
- **How it fails:** a count above its pin fails, and a file the table does not list starts at zero. The message names the file, the family, the count and the model-native route, and prints the table lines to paste if the dependency is kept on purpose.
- **Re-pinning:** lower a pin when a package removes a dependency; raise one only with a "Voxel dependency" line in DEV_LOG. `dotnet run --project unity/ChipArenaConversion/Tests/CoreTests.csproj -c Release -- --voxel-ratchet-table` prints today's counts in the table's format.
- **Baseline:** shared at `ac69455e`, the plan's counts (`ClassicBodies` 25, `VisualBodiesOn` 18, `CapturedRig.Boxes` 23, `rig.Bones[` 163 in 59 files, `WeaponShape.Cube` 228 in 25 files ...). P9 lowered the rooftop's draw pin from 3 to 0.
- **In flight:** W3's eject (`Core/NativeDeployEject.cs`, 13 rig-authoring uses on `claude/exp-w3-eject` at `048adc30`) is pinned and named; V4 moves it onto the model (plan M12).
- `--voxel` runs the ratchet, the style checks and the counter's checks alone.

### The voxel style stays loadable (`Tests/VoxelStyleChecks.cs`, the plan's first half)
- The 23 kept data files and generated tables match pinned SHA-256 digests (carriage returns taken out, so a CRLF checkout hashes as the repository does).
- Engine-free: every Electron model loads as the bank loads it, every motion clip is whole and finite, every voxel body skin and horde look builds within its budget at every tier, and every rig clip poses.
- The second half (a map with the voxel keys playing on host and guest) comes with V12.

### The DEV voxel draw counter (Core `NativeVoxelDraws`; Runtime `NativeVoxelDrawCount`, `NativeVoxelOverlay`)
- **What it shows:** the last whole frame's voxel draws, by kind. The bank counts every instance it flushes by the model id its part is registered under; the breach, gore and status cubes count at their own draws.
- **The kinds:**
  - pilot bodies, voxel weapons (the Prototype Series), voxel hats, voxel enemies (the far horde tiers, summons, the Slenderman);
  - kill cubes, box-sized gibs, wound cubes, revive cells, breach cubes, gore cubes, status markers;
  - primitive pieces and the Siren (**allowed**, until V3, V5 and V11 replace them);
  - voxel scene props (the Electron maps, stages and machines);
  - frame sculpts: the Standard sculpts drawn in the voxel weapon frame where no life-size model exists. Shown for information (V10), not counted as voxel.
- **The target:** with the bake, no costume and no Prototype picks, every row but the allowed ones reads 0. The header reads "N UNEXPECTED".
- **How to show it:**
  - the DEV panel's Camera tab, "Voxel draws (counter)" (a read-only view tool);
  - `--ca-voxel-counter` on the command line, in the Editor and Development builds only;
  - BoZo captures: `vcount=1` (each shot logs a `voxel draws:` line).
- It draws over every screen: a match, an expedition, the menus and the Deployment Station. It is off by default, and play pays nothing for it.

## P9: no voxel weapons outside the arena (V0's fix)
- The Deployment Station's pads and the rooftop draw their held models through the seat's weapon skins, never an unpicked Prototype id.
- The weapons pack bank's models (daggers, shield, bow, caster staffs) draw on every model body that holds a weapon in hand: the arena's, the portraits', the rooftop's and the Deployment Station's. The entrances keep the game's model until V5.
- A DEV animation class (the DEV Greatsword, the DEV Katana) holds, and is armed as, its base class.
- A Prototype pick still draws its voxel weapon: that is the player's choice.

## Baselines (V0's measurements)
See "Measured" below for what was run on 2026-11-18 and how the remaining runs are made.

### How the owner runs the player baseline
The shipped player is a Development Mono build, so its `PERF_SAMPLE` lines (every 30 s of a match) and `PERF_HITCH` lines (a recovered stall) land in `%USERPROFILE%\AppData\LocalLow\ChipArenaConversion\Chip Arena\Player.log`. From builds with this change on, each line also names the body style: the Player model setting, the voxel switches, and the counter's unexpected draws.
1. Start the test-channel build fresh (each launch rotates Player.log to Player-prev.log, so copy it before the next launch).
2. For each tier (Settings · Graphics High, Balanced, Low), play for at least 2 minutes each:
   - **PvP, 8 pilots:** Play vs Bots, 7 bots, Cybercity. Play once with Settings · Player model New, then once with Classic voxel.
   - **The expedition hub:** start an expedition and stay in the hub among its zombies. Play once as launched, then once launched with `--ca-voxel-zombies` (a shortcut's Target, after the exe's path).
   - The 4-pilot hub (88 zombies) needs three friends or Astra's DEV load harness (A0).
3. Run `python unity/ChipArenaConversion/tools/perf_baseline.py "<the copied Player.log>"`. It prints one row per scenario: p95 and p99 (median and worst sample), the worst frame, stalls, and the unexpected voxel draws.
4. Paste the rows into the budget table, naming the machine (CPU, GPU, memory), the tier and the resolution. If a row is over its target (12, 10 and 8 ms p95 at High, Balanced and Low), the owner hears before any overhaul code merges (OVH:937).

### The low-end run (O3)
- **On a PC the owner names:** the same steps at 1920x1080, Low.
- **Or a constrained stand-in on this PC:** start the player held to 4 cores, at 1920x1080 and Low, from a Command Prompt:
  `start "" /affinity F "<path to>\Chip Arena.exe" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0`
  Then follow the same steps. The stand-in limits the CPU only, so it does not show a weaker GPU.
- O3 removes the Classic row only if the 8-pilot PvP and the hub stay within the Low target on the model. Otherwise the row stays, relabelled "Low-end bodies (this PC)".

### Measured
- **The hub, in the Editor** (W0, 2026-11-18, `docs/EXPEDITION-DEPLOYMENT.md` "Frame budget" on `claude/exp-w0-groundwork`): one pilot with the hub's own zombies, p95 17-20 ms at High and Balanced and 14-20 ms at Low, against targets of 12, 10 and 8 ms. The Editor numbers are already over the targets before any overhaul code, so the owner hears of it (OVH:937).
- **The 8-pilot PvP and the voxel-zombie runs, in the Editor:** see the DEV_LOG entry of 2026-11-18 (V0) for the table, the machine and the commit.
- **The Development Mono player and the low-end run:** not run on this PC (the player shares the owner's saved settings and session, and the low-end machine is the owner's to name). The steps above make them.
