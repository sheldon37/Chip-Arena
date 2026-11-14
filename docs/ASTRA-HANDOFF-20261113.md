# November 13 hand-off checkpoint

Base inspected: shared `8c7d885380e508e435645c5770e8e9dfff339d32`.
Task branch: `codex/nov13-handoff`. Implementation and diagnostics, not a released
Windows build. No real licensed pack downloaded or private Drive zip modified.

| Item | Result | Remaining acceptance |
|---|---|---|
| Private add-ons | Explicit packing allowlist, verified install, generic map lifecycle and lighting callbacks; docs/PRIVATE-ADDONS.md | Actual Unity import and callback execution with stand-in; pack ownership and real adapter integration |
| DEV grant keys/icons | Fangs added to both grant lists; Site source has all three mappings; native ClassIcons already contains all three bespoke icons | Site publication blocked by its baseline build; client build/update |
| Katana/Fangs rules | Source review below | Native collision and two-client tests of Fangs branch before integration |
| Bisect sealing | New opt-in 0–90 degree diagnostic reproduces additional gaps | Root cause and cap repair; exact original 52-degree fixture still needs reproduction |
| Network visual checks | Existing managed bisect late-frame/replay checks pass | Actual host plus guest for paired victims, daze, hit-4 blend/trail, blunt kick and Behind_01 |
| Editor lock | FIFO command wrapper, holder metadata, priority, conservative dead-holder recovery, orphan waiter exit | Windows/shared Editor acceptance; agents adopt wrapper; old waiter processes remain untouched |
| Stage bank rebuilds | Current shared source already includes far_layers and rooftop opt-out | Released private banks and Blender environment required for byte-for-byte rebuild comparisons |

## Rules review

Katana: `SuccessfulMeleeBlock` uses the longer BlockPunish duration only for human
standard-melee/Katana parries, excludes Siren and keeps the existing rebound.
`SettleRiposte` ends the live hold on the parrier's first positive-damage hit;
other pilots' hits do not consume it. `KatanaCounter` additionally checks the
window, reach, visibility, target eligibility and hold state. DEV Katana uses
Katana rules via normalized seat motion. No new rule change made in this task.

Fangs review target: `claude/fangs-travel` at `8339c21a0a41f5b3cb0a0c107e9ec61e920cbfed`.
This is separate from the task's shared base and has not been merged here.

| Cut | Lunge before → after (m) | Reach before → after (m) |
|---|---:|---:|
| 1 | 0 → 0.63 | 2.10 → 1.90 |
| 2 | 0 → 1.07 | 2.10 → 1.57 |
| 3 | 0.50 → 1.00 | 2.20 → 2.06 |
| 4 | 0 → 0 | 2.20 → 2.20 |
| 5 | 1.20 → 1.67 | 2.60 → 2.93 |

Cut damage, hit times and lengths are unchanged in this diff. Veil adds a 2.5 m
backward hop; Bloom adds 1 m forward travel. Travel is authority-side, grounded,
wall-clamped, checked for floor at 0.25 m intervals, and stopped 0.9 m before a
foe within a 0.8 m corridor. CutReachAt compensates for travel not completed at
impact, including air/wall cases. These are movement/balance changes, not merely
animation. Validate wall/corner and narrow-ledge cases, moving targets, airborne
casts, attack-rate scaling and guest snapshots in Unity before release. Source
review alone does not establish collision correctness. No frontal-critical
push-back added; the owner's optional decision remains open.

## Bisect diagnostic

Run from repository root:

```
dotnet run --project unity/ChipArenaConversion/Tests/CoreTests.csproj -c Release -- --melee-bisect-sweep
```

Uses the existing DEV Katana diagonal fixture, rotates its cut normal from 0 to
90 degrees in one-degree increments while retaining its point and body pose,
and compares both halves against the uncut-body backface-ray baseline. It is
opt-in because this exposes an unresolved defect, not a passing regression yet.
The normal existing `--melee-bisect` route passed 37,171 checks at current angles.

Sweep: 182 halves, 10 failures. Angle/half/backface rays: 0/1/572; 45/0/20;
45/1/4; 46/0/4; 47/0/20; 47/1/6; 48/0/14; 48/1/4; 49/0/5; 49/1/3.
This differs from rebuilding the original clip-derived 52-degree request; it
establishes more failing planes, not the precise cause of the owner's 13-ray gap.
No plane angle or sealing threshold was relaxed to hide the failures.

## Site blocker

Published Site version 69 source is `95a77ab986812d21f4dd75e5d8ea4537f8abd698`,
older than shared Unity. Changes are isolated on Site source branch
`codex/dev-grants-20261113`, commit `81d4b38` (three grant keys, icon mappings,
rebuilt account Worker). Direct grant acceptance/rejection/deduplication and icon
mapping checks pass; Worker bundling passes. `npm run build` reports eight
TypeScript errors, identical on pristine source: missing shared abilities module,
Siren audio/intro typing and pong Simulation. No Site version saved or deployed.
The built browser asset was not patched by hand; publishing only the Worker would
leave the Site's grant UI stale. Repair/reconcile the Site source before publishing.

## Final verification receipt

Pinned Unity 6000.3.24f1 semantic compilation passed for Input System, Player,
Assembly-CSharp and Editor (existing warnings). Source package validation passed
with 3,938 files. No Unity process or Windows player was run. Full CoreTests stopped
after 283 passing groups at a BootIntroChecks first-frame timing assertion
(weapon-sword: 28 ms); the focused rerun passed all 135 boot-intro checks. This does
not establish a green full run. The separate plane sweep intentionally remains
failing. Site baseline and changed source have exactly the same eight TS errors.
Site source branch was pushed successfully. GitHub CLI push has no credentials;
the connected GitHub API is used to publish the exact reviewed task tree instead.
