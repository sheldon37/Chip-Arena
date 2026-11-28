# Realistic gun and magic runs (the grounded stride)

Owner, 2026-11-23: "Next; I want to fix the running/sprinting animations for weapons. Currently, its very 'hoppy' looking. Lets make it
look more realistic (Mostly on guns and magic) think melee is fine?" Claude (L1), branch `claude/run-realistic`.

## What made it hoppy (measured)

Measured on the model in two ways: in play (Cyber City, third person; BoZo `motion=` logs every frame at a fixed 1/30 s, on a clear
course: a run, a sprint, a back-pedal and both strafes at the rules' speeds), and on the clips themselves (the motion bank's records
posed on the baked body by the local lab `ChipArenaL1Lab.cs`, with the game's contact guard: the body only kept out of the floor).

- **The clips.** The animation pack's runs ahead for the gun, bow (the pistol's), staff and unarmed (the Bombadier's) sets are cartoon
  bounds. Posed on the body the pelvis rises and falls 20-31 cm a stride, both feet are off the floor 58-75 % of it, and the lower foot
  rises 38-54 cm. Their diagonals ahead bob 12 cm. The gun set's side, back and back-diagonal runs are low already (7-8 cm); the
  staff's and the unarmed set's are not (11-24 cm).
- **The pace and the sprint.** The rules run at 6.8 m/s and sprint at 9.18 (x1.35). The pack's runs travel 4.3-5.2 m/s on the body,
  so the game plays them 1.3-1.6 times their pace and the bound repeats 3.5-4.3 times a second. The sprint had no clip of its own: it
  is the run ahead again, capped at twice its pace (5.45 steps a second). For the long guns 2 x 4.30 = 8.59 m/s is short of 9.18, so
  the planted feet slid 0.30 m/s (0.57 at worst).
- **Not the camera or the root.** The root (the pilot's simulated body) moves level: 0.0 cm. The camera follows that body, not the
  model; only Settings' camera shake (off by default) sways it while sprinting.
- **Not a carry bob.** The guns' carry (the Tactical pack's ready hold over the legs, T1) rides the chest: the gun bobs as much as the
  pelvis (18.6 against 18.9 cm on the rifle). The staff's own run swings the staff hand 34-43 cm a stride.

So: an over-bouncy clip, played fast, and a sprint without a clip whose feet slide.

## What changed (presentation only)

The pistol, rifle, machine gun, every caster (fire, water, lightning, darkness, L33t) and the Bombadier stride on a **grounded stride**
(`Core/NativeGroundedStride.cs`) when the motion bank carries it (`NativeVisualMotion.Stride`, `Grounds`):

| Way | Clip | Pelvis a stride (posed) | Travel (its planted feet) | Rate at the run / sprint |
|---|---|---|---|---|
| Ahead, and the sprint | Frank gunslinger 8-way run F | 7.2 cm, flight 10 cm high | 5.70 m/s, 1.9 m steps | 1.19 (3.6 steps/s) / 1.61 (4.8 steps/s) |
| Ahead right, ahead left | Frank gunslinger 8-way run FR, FL | 7.3 cm | (2.79, 4.83), (-2.44, 4.99) m/s | 1.26, 1.29 |
| Right, back-right, back, back-left, left | the gun set's own runs (as before for the guns) | 6.9-7.6 cm | 3.36-3.72 m/s | 1.82-1.87 |
| The 8 walks | the gun set's own walks (as before for the guns) | | 2.1-2.4 m/s | |

- **The sprint** is the run ahead's long stride at 1.61 times its pace (4.8 steps a second, 1.9 m steps: a sprinter's), no longer the
  pack's bound capped at twice its pace with sliding feet. The long guns' upper body still switches to the pack's sprint carry.
- **The Frank 8-way run is recorded again for the guns** (`Frank_RPG_Gunslinger_8Way_Run_F/FR/FL`): the same legs as the Katana's and
  the Fangs' (`Frank_RPG_Katana@Velocity_8Way_Run_*`), which stay byte-identical for melee. The added records carry the travel their
  planted feet show (the pack's root motion is 25-40 % off it: 4.35 m/s on the run ahead whose feet travel 5.70), and the run ahead is
  lowered 2.1 cm so its planted feet sit on the floor (it hovered 2.6 cm, under the game's foot locks' 2.5 cm threshold, so its feet
  were never locked and slid).
- **Over the legs:** the guns keep the Tactical Shooter Pack's carry (T1, unchanged); the casters carry the staff steady
  (`RunUpper` = `NativeGroundedStride.StaffCarry`: the Frank mage's run, mirrored into the staff's left fist, its upper body at the
  stride's phase, `VisualRunUpper`); the Bombadier keeps its own pumping fists (`FistsCarry`: its unarmed run's upper body). A cast, an
  aim, a shot or a reload still lays over the carry; the free arm's run lowering stands down under it.
- **Unchanged:** melee (sword, greatsword, daggers, shield, Katana, Fangs, Glaive, the DEV classes), the bow, the fighters and the Top
  Dog keep their strides; every set's match idle, jumps, falls, landings, reactions and casts; first person (its own arms).
- **Fallbacks:** a bank without the stride's clips (the builder before the bank lands, CoreTests' synthetic banks) or whose stride clips
  carry no travel (a full rebake bakes the Frank runs in place) keeps each set's own stride.

## Measured, before and after

In play on the same course (Cyber City; the branch's code with the new bank against shared `39f6855b` with the released bank; the
rules' 6.8 m/s run and 9.18 m/s sprint, the Bombadier's 6.47 and 8.73), every frame at 1/30 s, each move's steady part:

- **Pelvis bob**: the hips' rise and fall a stride (median over the strides), in the body root's frame (the root moves level).
- **Flight**: the share of the move with neither foot on the floor (each foot's ankle and toes more than 2.5 cm over their lowest: a
  heel rising off a planted ball still counts as down; at 30 frames a second short contacts read long flights) and its height (the
  lower foot's lowest point).
- **Steps/s** from the stride's phase; **step** = speed / steps per second.
- **Planted-foot slide**: each stance's slowest world speed of the foot (a locked foot stands still), median and worst over the move.
- **Weapon bob**: the gun's mount (guns), the staff hand (casters), the right fist (Bombadier), a stride.

| Weapon | Move | Pelvis bob (cm) | Flight (share, height cm) | Steps/s | Step (m) | Planted-foot slide (m/s, median/worst) | Head bob (cm) | Weapon bob (cm) |
|---|---|---|---|---|---|---|---|---|
| rifle | run | 18.9 -> **7.0** | 59% 39.0 -> **56% 11.7** | 3.95 -> 3.57 | 1.72 -> 1.91 | 0.0/0.18 -> 0.0/0.17 | 18.8 -> 7.3 | 18.6 -> 7.5 |
| rifle | sprint | 19.2 -> **7.0** | 48% 33.7 -> **62% 11.5** | 5.0 -> 4.8 | 1.84 -> 1.92 | 0.296/0.57 -> 0.126/0.81 | 19.2 -> 7.2 | 18.9 -> 7.5 |
| rifle | back | 7.2 -> **7.1** | 50% 13.2 -> **58% 14.1** | 4.66 -> 4.67 | 1.45 -> 1.45 | 0.003/0.32 -> 0.0/0.05 | 7.3 -> 7.2 | 7.5 -> 7.9 |
| rifle | right | 6.9 -> **6.3** | 54% 19.6 -> **56% 21.3** | 4.56 -> 4.57 | 1.49 -> 1.49 | 0.0/0.0 -> 0.0/0.0 | 6.7 -> 6.1 | 5.3 -> 6.0 |
| rifle | left | 7.6 -> **7.3** | 43% 10.7 -> **46% 11.6** | 4.56 -> 4.57 | 1.49 -> 1.49 | 0.0/0.14 -> 0.0/0.0 | 7.4 -> 7.1 | 7.0 -> 6.7 |
| machinegun | run | 18.9 -> **7.0** | 59% 38.9 -> **59% 11.7** | 3.95 -> 3.57 | 1.72 -> 1.91 | 0.0/0.18 -> 0.0/0.24 | 18.8 -> 7.3 | 18.6 -> 7.5 |
| machinegun | sprint | 19.2 -> **7.0** | 48% 33.7 -> **64% 11.6** | 5.0 -> 4.79 | 1.84 -> 1.92 | 0.299/0.57 -> 0.095/0.69 | 19.2 -> 7.2 | 18.9 -> 7.3 |
| machinegun | back | 7.2 -> **7.1** | 47% 13.2 -> **56% 14.1** | 4.66 -> 4.66 | 1.45 -> 1.45 | 0.0/0.11 -> 0.0/0.04 | 7.3 -> 7.2 | 7.5 -> 7.9 |
| machinegun | right | 6.8 -> **6.4** | 54% 19.9 -> **57% 20.8** | 4.56 -> 4.56 | 1.49 -> 1.49 | 0.0/0.0 -> 0.0/0.0 | 6.6 -> 6.2 | 5.4 -> 5.9 |
| machinegun | left | 7.6 -> **7.3** | 43% 10.7 -> **43% 12.2** | 4.56 -> 4.57 | 1.49 -> 1.49 | 0.0/0.13 -> 0.0/0.0 | 7.4 -> 7.1 | 7.0 -> 6.7 |
| pistol | run | 26.1 -> **7.0** | 54% 39.0 -> **56% 11.7** | 3.3 -> 3.57 | 2.06 -> 1.91 | 0.0/0.12 -> 0.0/0.0 | 26.6 -> 7.3 | 25.7 -> 7.4 |
| pistol | sprint | 25.1 -> **7.0** | 67% 38.2 -> **62% 11.5** | 4.43 -> 4.8 | 2.08 -> 1.92 | 0.0/0.21 -> 0.147/1.35 | 27.8 -> 9.5 | 44.9 -> 13.3 |
| pistol | back | 7.1 -> **7.1** | 50% 13.5 -> **56% 14.1** | 4.72 -> 4.66 | 1.43 -> 1.45 | 0.064/0.35 -> 0.0/0.06 | 6.9 -> 7.2 | 6.9 -> 7.9 |
| pistol | right | 6.8 -> **6.3** | 52% 20.5 -> **56% 21.5** | 4.57 -> 4.57 | 1.49 -> 1.49 | 0.0/0.0 -> 0.0/0.0 | 6.6 -> 6.2 | 6.1 -> 5.5 |
| pistol | left | 7.3 -> **7.3** | 41% 12.9 -> **46% 11.7** | 4.56 -> 4.57 | 1.49 -> 1.49 | 0.0/1.01 -> 0.0/0.0 | 7.0 -> 7.1 | 6.2 -> 6.3 |
| fire | run | 27.7 -> **7.0** | 59% 42.7 -> **58% 11.7** | 3.3 -> 3.57 | 2.07 -> 1.91 | 0.147/0.86 -> 0.0/0.03 | 28.0 -> 8.7 | 42.9 -> 9.1 |
| fire | sprint | 23.5 -> **6.9** | 60% 43.4 -> **62% 11.5** | 4.42 -> 4.8 | 2.08 -> 1.92 | 0.0/0.37 -> 0.159/1.25 | 23.7 -> 8.5 | 33.8 -> 8.9 |
| fire | back | 10.8 -> **7.1** | 65% 21.9 -> **56% 14.0** | 4.66 -> 4.66 | 1.45 -> 1.45 | 0.135/0.99 -> 0.0/0.07 | 11.9 -> 9.2 | 12.4 -> 10.6 |
| fire | right | 20.5 -> **6.4** | 50% 19.3 -> **52% 21.8** | 4.57 -> 4.57 | 1.49 -> 1.49 | 0.0/0.0 -> 0.0/0.0 | 20.2 -> 7.6 | 23.8 -> 8.5 |
| fire | left | 14.7 -> **7.2** | 54% 25.5 -> **46% 12.2** | 4.56 -> 4.58 | 1.49 -> 1.49 | 0.0/0.0 -> 0.0/0.0 | 16.1 -> 7.9 | 22.8 -> 8.3 |
| darkness | run | 25.2 -> **7.1** | 61% 43.0 -> **59% 11.7** | 3.3 -> 3.57 | 2.06 -> 1.91 | 0.175/0.87 -> 0.0/0.15 | 25.2 -> 8.7 | 40.3 -> 9.1 |
| darkness | sprint | 23.4 -> **6.9** | 58% 43.8 -> **62% 11.5** | 4.43 -> 4.8 | 2.08 -> 1.92 | 0.0/0.49 -> 0.204/0.87 | 23.5 -> 8.5 | 33.6 -> 8.8 |
| darkness | back | 10.8 -> **7.0** | 64% 21.8 -> **59% 14.0** | 4.67 -> 4.66 | 1.45 -> 1.45 | 0.136/0.99 -> 0.0/0.08 | 11.9 -> 9.1 | 12.4 -> 10.6 |
| darkness | right | 20.6 -> **6.5** | 48% 19.3 -> **50% 22.3** | 4.58 -> 4.58 | 1.49 -> 1.49 | 0.0/0.0 -> 0.0/0.0 | 20.3 -> 7.7 | 23.9 -> 8.6 |
| darkness | left | 14.8 -> **6.1** | 57% 24.8 -> **52% 12.1** | 4.56 -> 4.57 | 1.49 -> 1.49 | 0.0/0.0 -> 0.0/0.02 | 16.2 -> 6.6 | 23.0 -> 7.0 |
| bombadier | run | 29.9 -> **7.0** | 62% 49.1 -> **59% 11.2** | 3.78 -> 3.39 | 1.71 -> 1.91 | 0.0/0.31 -> 0.0/0.0 | 29.4 -> 7.8 | 28.2 -> 13.9 |
| bombadier | sprint | 26.8 -> **7.0** | 67% 52.9 -> **64% 11.2** | 4.5 -> 4.55 | 1.94 -> 1.92 | 0.0/0.0 -> 0.0/0.0 | 30.2 -> 7.8 | 35.7 -> 14.0 |
| bombadier | back | 9.0 -> **7.0** | 65% 11.3 -> **56% 15.0** | 4.5 -> 4.43 | 1.43 -> 1.45 | 0.0/0.12 -> 0.0/0.23 | 9.7 -> 7.5 | 14.5 -> 12.2 |
| bombadier | right | 13.6 -> **6.8** | 44% 16.7 -> **54% 20.9** | 4.33 -> 4.33 | 1.49 -> 1.49 | 0.059/0.34 -> 0.0/0.0 | 13.5 -> 5.8 | 13.2 -> 8.2 |
| bombadier | left | 23.4 -> **6.9** | 57% 24.3 -> **43% 11.9** | 4.32 -> 4.34 | 1.5 -> 1.49 | 0.0/0.0 -> 0.0/0.0 | 23.2 -> 6.4 | 22.5 -> 10.4 |

The water, lightning and L33t staffs share the fire and darkness staffs' set and code path (not filmed separately).

## Timing on the rules (AGENTS.md animation impacts)

None. No move speed, sprint rule, acceleration, hitbox, hit timing, damage, cooldown or network message is read differently or written;
the stride reads the body's travel (as before) and only picks other clips and their rate. The drawn feet and pelvis move differently
(lower bob, shorter flight); the hitboxes are the capsule's and the rig's, not the drawn legs'.

## The bank

The released bank `548c7889` (6,438,026 bytes: release 16 + T1's 14 Tactical records) with 4 records appended, every released record
byte-identical (checked record by record): `Frank_RPG_Gunslinger_8Way_Run_F`, `_FR`, `_FL` (from the BoZo tool's own motion bake,
`bozo-motionextra`; loop flag set, travel set to their feet's, the run ahead lowered 2.1 cm) and `Frank_RPG_Mage_8Way_Run_F~mirror`
(the lab's bake, the bank's own sampling and mirror). Built by the scratch `bankmerge.py` (`F:/Chip Arena Development/scratch/run-
realistic/tools`).

**Rebaking:** a full BoZo motion rebake drops T1's 14 records and bakes these Frank runs in place without their travel or lowering, so
the covered sets fall back to their own strides (the travel guard). After any full rebake, append T1's records again
(`ChipArenaT1Bake.cs`) and these four with `bankmerge.py` (the command in DEV_LOG).

## Checks

- `dotnet run --project unity/ChipArenaConversion/Tests/CoreTests.csproj -c Release -- --run-realistic` (`GroundedStrideChecks`, also in
  the full run): the fallbacks, the covered sets and the untouched ones, the carries, the feet planted at the rules' run and sprint in
  all eight ways (the bank's travel), the source pins, nothing written.
- Editor play captures before and after (BoZo `motion=`, the scratch `mstats.py` and `table.py`; the course and the arena-clock places in
  the local lab), and PrototypeBuild.Validate with the new bank.
- Review clips: `F:/Chip Arena Development/scratch/run-realistic/clips/` (per weapon a 2x2 before/after, side and three-quarter, real
  time and 2x slower; another pilot's eye on a rifle pilot running and sprinting past).
