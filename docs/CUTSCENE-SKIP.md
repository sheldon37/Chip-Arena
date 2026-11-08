# Vote to skip a cutscene

Owner, 2026-11-08: "lets add a Vote to skip option on cutscenes like boss intros. Once all players agree, the scene transitions
to the next thing."

## What can be skipped

- **A boss's arrival:** the PvE `intro` hold. This covers NOCTIEL's 51 s mocap intro and the 9 s arrivals of Station 647, the
  Converter, THE HUM, THE DEEP END and the Ward Zero boss, in a Boss Battle or an Expedition.
- **A boss's phase reveal:** the PvE `reveal` hold.
- **The pilots' intros** before a match (`NativePhase.Intro`). A skip ends the whole run of intros and starts the countdown.

The victory and the defeat holds can't be skipped. A cutscene stops taking votes 0.6 s before its end.

## The vote

- **Voting:** F, or the pad's Interact, casts this pilot's vote. It counts once per cutscene.
- **Who must agree:** every connected human in the arena. Bots, recruits and the boss's own seat never count, and nor does a
  pilot who has disconnected. A lone pilot skips with one press.
- **New ballots:** each cutscene gets its own ballot, so a reveal followed straight by an intro asks twice.
- **Where votes count:** in a split Expedition, a vote counts only in the voter's own area.

## On the host

`NativeArena.StepCutsceneSkip` runs each tick, before the hold ticks. Once every human has voted:

- **A PvE hold:** the rest of the hold is spent (`HoldLeft=0`), so `PveHoldTick` releases it on that same tick, exactly as its
  natural end would. The arena clock does not move.
  - The stage turns to `boss`.
  - The boss's arrival action clears.
  - NOCTIEL's phase clock starts.
  - `NativeBossSongClock`'s lead carries the boss song on from the fight's start: THE DEEP END's song at 9.07 s, NOCTIEL's from
    the drop.
- **The pilots' intros:** `NativeMatch.SkipIntros` starts the countdown.

The skip changes no hit, damage, cooldown or movement rule.

## On the wire

- A guest's vote is a `skip-vote` message.
- The ballot rides every frame as `NativeGameFrame.cutsceneSkip`: the cutscene's key, who has voted, how many must, and the skips
  so far.
- An older host sends no ballot, so its guests show no prompt.

## On screen

`Runtime/NativeCutsceneSkipUI.cs` draws a key-cap prompt at the bottom right:

- Alone, it reads `F · SKIP`.
- With others, it reads `VOTE TO SKIP`, with a dot for each pilot (filled as they vote) and the count.
- Once this pilot has voted, it reads `VOTED · WAITING`.
- A pilot still to vote is invited with a soft pulse once someone has voted. The pulse rests with reduced effects.

When the host skips, every screen cuts through a 0.55 s black dip, so the jump from the cutscene to the fight is never seen.

## Code and checks

- **Code:** Core `NativeCutsceneSkip`; Runtime `NativeCutsceneSkipAuthority.cs`, `NativeCutsceneSkipUI.cs` and
  `NativeCutsceneSkipValidation.cs`.
- **Checks:**
  - CoreTests `--cutscene-skip`.
  - The boss build checks run the skip on every boss with parties of 1 and 4 (`ValidateCutsceneSkip`).
