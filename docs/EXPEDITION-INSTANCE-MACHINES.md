# Machines in the generated instances

Owner request, 2026-11-04: "lets add machines to new expedition instances (chip machine, recruit machine, mending machine)".

**What "new" means.** Of the instances a run can roll, Ward Zero (its PvE content's machines), Central Station and City Ironworks (four each, authored in `ExpeditionLayout.json`) already have machines. The two newest, **the Backrooms (Level 0) and the Poolrooms**, are generated from the run's seed and had none, so a run through them had nowhere to spend: the comment in `NativeExpeditionLayout.BindContent` says it plainly, "New instances require their own machine authoring".

**What now stands there.** Four machines in each, along the way from the door to the far end: a chip machine at about a quarter of the way, a recruit machine at about half, a mending machine (`med`) at about two thirds, and a second chip machine near the end. Every peer rebuilds them from the seed with the rest of the level (`Core/NativeInstanceMachines.cs`; both generators' `Identity` carry them).

**The rules needed nothing new.** A machine on the area's `machines` list is built, drawn, labelled and served exactly as the authored ones are: `BindContent`, `BuildPveMachines` on host and guests, `TryExpeditionInteract`, `BuyExpeditionMachine`, the same prices, F to use, the same health, cooldown and recruit-cap checks. **Live as soon as this is on a build; nothing waits on Astra**, unless the review below turns something up.

## Where they stand

Each stands with its back to a real wall (never an open doorway, a short wall or the level's edge door), an open cell in front of it, 14 m or more from the next, 10 m from the door and 7 m from the exit, never on a party slot, a roaming spawn or the exit cell. Lit cells are preferred, so one reads from down a corridor.

| | Level 0 | The Poolrooms |
| --- | --- | --- |
| Cell | 3.2 m | 2 m |
| Its collider | .9 m deep against the wall, leaving the cell's centre (its nav node) 0.61 m clear | Reaches past the cell's centre, so the cell comes off the navigation |
| Extra rule | A dead-end-free spot: cells with two links score slightly worse | A deck corner (a wall on a side too), deck in front, and only if every other cell is still reached without it |

## What this changes in the simulation (for Astra to review)

The levels are Astra's generators, and these are the only touches, all additive:

- `BackroomsGenerated.Machines`, `PoolroomsGenerated.Machines`: the picks, expedition only (the PvP arenas have none).
- `NativeBackroomsExpedition.Area`, `NativePoolroomsExpedition.Area`: the `machines` list.
- `NativeBackroomsKinds.Anchors`: no spawn anchor in a machine's cell (a Faceling out of the wall behind a machine, a Tiler through it). The anchors' count falls by at most those cells.
- `NativePoolroomsGenerator.Map`: a machine's cell has no nav node or links. The Backrooms' navigation is untouched.
- Both `Identity` strings carry the machines, so a build mismatch refuses cleanly, as any other layout change.

`Tests/InstanceMachineChecks.cs` (`CoreTests --instance-machines`), 24 seeds of each: all three kinds; a whole wall behind; the front, dispenser and drop on open floor; a pilot fits at the front; every nav node and spawn exit clear of the collider; the party's slots, door and exit clear; enemies and the exit still reached; the same from the same seed; the PvP arenas unchanged.

## How they look

The machines are the PvE machines, unchanged. Round their feet, each level's own dressing, from `Core/NativeInstanceMachineDress.cs` (flat on the floor or up on the wall; nothing here collides):

- **Level 0**: a damp patch of carpet where it has stood, a fluorescent tube on a bracket on the wall over it with a bad contact (its own, so it stays readable when the circuit overhead is dead), its cable clipped down the wall, a note taped to the wallpaper.
- **The Poolrooms**: a ribbed rubber mat on the tiles, a clean white tube on a galvanised bracket, a yellow wet-floor sign, a puddle or two.

## Animation impact

None. Hit timing, hitboxes, damage, cooldowns and movement are unchanged. New collision: the machines' existing hidden block, in these two maps.

## Not verified

The chip's whole purchase, the recruit and the heal running inside these two levels with a real director and two clients: the path is the authored machines', and native and Windows play were not run here. The look was reviewed in Unity with the level's real generated map and content bound, not in a full run.
