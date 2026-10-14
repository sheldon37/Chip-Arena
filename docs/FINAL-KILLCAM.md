# Final killcam

Owner request (2026-10-13): "Also, id like to add a final killcam. Similar to killcam we have now, but it plays for everyone on final kill (If you are the last player dead on the round, instead of seeing your normal kill cam, you will see this one). It will replicate the slow motion on death like call of duty."

Owner follow-up (2026-10-13): "The final killcam isn't playing, its supposed to be at the end of every round (per set)". It was skipped in offline Play vs Bots, on environmental, self and team-kill deaths, and on draws; it now plays at the end of every round that ends on a death.

Owner follow-up (2026-10-13): "Can we set the final kill to be from the perspective of the killed player?" Every final replay now follows the victim's recorded third-person view, for everyone watching. Killer credit and broadcast labels are unchanged.

It is presentation only. Branch `claude/final-killcam`; Unity code in `unity/ChipArenaConversion/Assets/ChipArena`.

- Core logic: `Core/NativeFinalKillcam.cs`. It is engine free and covered by `Tests/FinalKillcamChecks.cs`.
- Runtime: `Runtime/NativeFinalKillcamPresentation.cs`. It hooks into the existing killcam in `Runtime/NativeReplay.cs`.
- Supporting presentation files:
  - `Runtime/NativeReplayAudio.cs`
  - `Runtime/NativeSlowMotionPresentation.cs`
  - `Runtime/NativeCorpseSmoothing.cs`

The normal killcam contract (`docs/KILLCAM-HANDOFF.md`) is unchanged for every death that is not the final kill.

## Three-second start delay (2026-10-14)

Final killcams now wait three real-time seconds before playback. The live camera remains visible during the wait; the final victim does not enter a normal killcam first. Replay history is pinned as soon as the final clip is selected. Ordinary non-final killcams keep their existing timing. The round result banner does not start during the wait; it arrives with the hand-off after the clip, as before, so it plays once.

The authority adds those three seconds to qualifying death-ending Results: 11.5 seconds for credited final kills and 8 seconds for local fallback clips (including practice, environmental deaths and draws). No-death, sandbox and cooperative results retain their existing timing. Clip fitting reserves the delay before choosing playback length, so the wait preserves the previous replay budget. The older duration descriptions below are historical.

## The final-kill rule (every client, from data it already has)

Each client checks once, on the first frame where it sees the match phase go from Fight to Result. A late joiner never sees that step, so it skips. The candidates are the pilots' replicated death records.

The final kill is the latest death of a pilot who is not on the winner's side (any pilot on a simultaneous knockout), ordered by `at` and then by `seed` (the authority's event order), between `FightAt` and the Result frame. All of these must hold:
- **Window:** the death is at most 0.5 s before the first Result frame the client saw.
  - The host resolves in the same step as the death.
  - A guest's first Result frame is at most one 20 Hz send later.
  - A round that resolves later is not ended by that kill. Examples are the 12 s disconnect grace and a sandbox hold.
- **Any cause:** every final replay plays from the victim's view. A kill (`owner` is a pilot other than the victim and, in a teams match, not a teammate: `Credited`) retains the killer's credit and "FINAL KILL" labels. An environmental death, a self-elimination or a team kill shows "FINAL ELIMINATION" with the cause (`SoloCause`). The fall and overtime credit that the authority already assigns (last attacker within 7 s) counts as a kill. A simultaneous knockout shows the last death and a "NO WINNER · Draw" outcome.
- **Mode:** not PvE or cooperative (the Siren boss flow) and not the Dev Sandbox. Practice (offline Play vs Bots) shows it (`NativeFinalKillcam.InPractice`).
- **History:** this client has recorded at least 0.5 s before the hit, and the clip fits into the time left (see below).

The host and the guests hold the same death records and see the same step, so they pick the same death. Two deaths in one step resolve by seed. A replicated marker, in the Astra prompt below, would remove the timing inference.

## Who sees it, and the order rules

- **Everyone sees it:** the killer, the other living pilots, the dead and spectators. Each client plays it from its own recorded frames, in the killcam's presentation-only scene.
- **The last victim:** the final kill is decided before the fresh local death is handled. While a final kill is decided for the round, no normal killcam arms (`ArmNormal(finalSeed)`), so the victim never sees the normal one first and never sees both.
- **Other killcams:** a normal killcam that is playing or waiting to start is cut over (`Begin` → `CutOver`). Its scene unloads, and the final one opens its own scene, "Final killcam presentation", so the two never collide.
- **Once per round:** a final killcam already shown this round is never shown again (`Already`).
- **Skip:** Space, Escape or the Skip button. It is per player and stops only this screen; a living pilot can skip with Space too. The match carries on for everyone.

## The slow-motion clock

The replay clock runs on wall seconds since the clip started (`FinalKillcamCurve.Replay(wall)`):

1. Normal speed for the pre-roll.
2. A smoothstep ramp of the playback rate down to the slow rate. It ends `Lead` replay seconds before the fatal hit.
3. A hold at the slow rate, through the hit and `Hold` replay seconds of the death reaction.
4. A smoothstep ramp back up to normal speed.
5. `Tail` replay seconds of aftermath.

The rate is continuous, and its slope is zero at each end of each ramp, so the replay clock is smooth and strictly increasing.

| | Ideal (8.5 s Result, or a match-ending kill) | Today's 5 s Result (fitted) |
|---|---|---|
| Slow rate | 0.25× | 0.25× |
| Pre-roll | 2.4 s | 1.3 s |
| Slow from / to | 0.15 s before / 0.45 s after the hit | 0.12 s before / 0.31 s after |
| Fatal hit plays at | 3.04 s | 1.82 s |
| Clip length | 6.44 s (4.19 s of replay) | 4.05 s (2.41 s of replay) |

**Fit.** `NativeFinalKillcam.Fit(available, history, behind)` searches along keyframes, from the ideal clip to the shortest. Each step only shortens the clip:

1. Pre-roll and aftermath.
2. Then the hold, the ramps and the lead.
3. Then a lighter slow rate.
4. Then everything, down to 0.5 s of pre-roll at 0.45×.

The fit returns the longest clip that fits all three limits:

- **Time:** `available` = `PhaseEnd − now − 0.9 s`. The 0.9 s hand-off keeps the round result on screen. A match-ending kill is unbounded on the host and offline, because the Match phase waits for the replay. A guest fits it into the Result time, because its match does not hold the room's sets to win.
- **History kept:** while the final killcam plays, the live history trim keeps the frame its clip starts from (`FinalKeeps`), so a large map's entity cap cannot drop the frames it is still playing.
- **History:** the client's recorded history before the hit.
- **Trimming:** the clip never falls far enough behind the live clock for the 6 s history trim to remove its frame.

The clip always ends before the next phase. Under 1.67 s available, or under 0.5 s of history, there is no final killcam.

## Recorded projectile impacts

Normal and final replays interpolate native bullets between recorded frames instead of applying the live guest's forward velocity extrapolation. The authority retains each removed bullet's ID, stopping position and simulation time in the optional `projectileEnds` frame field (one second, at most 1,024 records). Guests preserve these records in their own replay history. The replay moves a terminating shot to that endpoint and removes it at its recorded time, including at quarter speed. Surviving piercing/reflected shots continue between their recorded positions; an old host without an endpoint holds the last known position until the next frame rather than inventing forward flight.

When a moving pilot dies between captures, their replay position moves to the recorded death position and their alive/dead state changes at the death clock. This keeps the victim and projectile on the same timeline. These adjustments run only in the isolated replay scene; live collision, damage and movement are unchanged. Shots born and removed entirely between captures are still not reconstructed, and curved or bounced paths between captures remain approximations.

## Presentation

- **Slowed playback.**
  - Transforms still interpolate between the recorded frames, and nothing touches the live simulation.
  - The blast and projectile particle simulations and the hand motes advance by wall time times the playback rate (`ArenaPresentation.ClockRate`).
  - Death bodies step their 1/60 s physics on the continuous replay clock and present a blend of their last two steps (`SlowBodies`, `NativeCorpse.Smooth`), so a ragdoll stays smooth at quarter speed.
  - Effects already drawn from the presentation clock (kill effects, gibs, blood, sprays, animation) slow with it.
  - All of these are off outside the final killcam.
- **Audio.**
  - One-shot effects voices are pitched with the rate (Unity pitch also slows them), at most an octave down: `Pitch(rate)` = `1 − 0.7·(1 − rate)`, clamped to 0.5–1.
  - Music ducks to 45 % while slowed.
  - Loops keep their own pitch.
  - Every voice is restored when the clip ends.
- **Camera.**
  - The victim's third-person view throughout the final replay, using their recorded position, yaw and pitch. Normal killcams retain their existing killer perspective.
  - While slowed, the boom pulls back by up to 1.9 m and up by up to 0.45 m.
  - The FOV opens by up to 7°.
  - The look keeps the victim's facing; it does not turn toward the killer–victim midpoint.
  - After the hit, the camera stays with the victim. The timeline is labelled "FINAL MOMENTS" throughout, never the killer's view.
  - Reduced effects keep the FOV and halve the move.
- **Broadcast frame (FINAL KILLCAM).** It is in the killcam's style, kept to the edges.
  - **Letterbox and frame:** a taller letterbox with gold rules, squeezing slightly while slowed. A vignette, and the killer's colour on the frame edge.
  - **Title card:** a "FINAL KILLCAM" title card with the round and set. It leaves before the slow motion and docks in the top bar.
  - **Top bar:** REPLAY, with the clock relative to the hit, and a SLOW MOTION ×rate chip.
  - **Lower third:** FINAL KILL, killer ▸ victim in their colours, the team chip, and the weapon and cause.
  - **Hit:** an edge pulse and a glitch slice on the fatal hit.
  - **Outcome card:** after the slow motion it shows ROUND / SET / MATCH WINNER, the winner and the replicated result line.
  - **Timeline:** the hit diamond, with the slowed stretch shaded gold.
  - **Skip pill:** Skip replay, with the Space key.
  - **Reduced effects:** no glitch, a lighter pulse, no squeeze.
  - **No per-frame allocation:** strings are fixed when the clip starts, and the clock and rate texts come from tables.
- **Hand-off.** When the clip ends or is skipped, a 0.45 s flash plays, and the round result banner makes its arrival under it (`FinalResultAge`). A match-ending kill's results panel opens after the replay, as it does today.

## Contract for other code

- **`ReplayActive`.** It is true while the final killcam plays, so the live HUD, notices, spawn-in, damage screen and cinematics step aside exactly as for the killcam.
- **Phase changes.** The final killcam ends on any phase that the killcam already ends on: Countdown, Draft, Lobby, or a new generation. It also ends on menu, Escape, Space, the Skip button, and disconnect or leave.
- **Unchanged.** The match rules, phase durations, network messages, damage, hitboxes and timing are not changed.

## Astra prompt (rules and networking; not done here)

> Astra: match-flow support for the final killcam. The presentation is on `claude/final-killcam`; see `docs/FINAL-KILLCAM.md`. This is rules and networking only, so leave `Runtime/NativeFinalKillcamPresentation.cs` and the HUD to Claude.
> 1. **Hold Result for the final killcam.** In `NativeMatch.Resolve`, when all of the following hold, set `PhaseEnd = Time + 5 + FinalKillcamHold`, with `FinalKillcamHold = 3.5` (an 8.5 s Result). Every other result keeps `Time + 5`, including the disconnect and grace path and the simultaneous knockout.
>    - The match is not `Cooperative`, not the Dev Sandbox, and a new `NativeMatch.FinalKillcams` flag is true. The host sets that flag for online rooms. Practice is off; keep it a host option.
>    - `Winner != 0`.
>    - The latest round death of a pilot not on the winner's side (`at`, then `seed`) was recorded no more than 0.5 s before this step.
>    - That death's `owner` is another pilot, and in teams, not a teammate.
> 2. **Replicate the decision.** Add `finalVictim` (seat id, 0 = none), `finalSeed` (that death record's `seed`) and `resultAt` (the match time of the resolve step) to `NativeMatchFrame`.
>    - Set them in `Resolve` and clear them in `BeginRound`.
>    - Copy them in `ApplyNetwork`.
>    - Serialize them with the existing explicit-presence pattern and validate them: finite, nonnegative, and a present seat or 0. A missing field from an old host reads 0.
>    - Also replicate the Result length, `resultSeconds`, or let clients use `phaseEnd − resultAt`. The round result bar today assumes 5 s (`NativeMatchOverlays.ResultOverlay`: `5-left`, `left/5`). Tell Claude the field name, and Claude switches the overlay and `FinalKillWatch` to the replicated marker, keeping the local rule as the fallback.
> 3. **Bots and hosts.** Bots need nothing; they have no presentation. The host's `PhaseEnd` stays authoritative for guests. Draft, `SkipBotDrafts` and the next countdown start after the longer Result, unchanged otherwise. Nothing else waits on the replay.
> 4. **Tests.** Add the following, then record the results in DEV_LOG:
>    - Managed Core checks: 5 s without a final kill and 8.5 s with one. Draws, environmental and self deaths, team kills, PvE, the sandbox, practice and late resolution keep 5 s.
>    - A JsonUtility round trip of the new fields, with defaults for omitted fields.
>    - A relay or two-client check that a guest receives the host's `finalVictim`, `finalSeed` and `resultAt`.

Until then, the fitted 4.05 s clip in today's 5 s Result leaves 0.9 s for the round result. Once `PhaseEnd` is longer, `Available` reads it and the ideal clip plays with no code change on Claude's side.

## Astra rules/networking implementation (2026-10-13)

Implemented on `codex/final-killcam-rules`, base `b9639f9d`. The earlier presentation-only description and prompt above describe the previous state. Online hosts now hold qualifying Results for 8.5 s; practice, sandbox, draws and other normal results remain 5 s. Cooperative/disconnect flows remain as before.

Claude: switch the overlay and FinalKillWatch to the host decision when `NativeMatchFrame.hasFinalKillcam` / `NativeMatch.HasFinalKillcam` is true. The wire fields are `finalVictim` (0 means explicitly no final kill), `finalSeed`, `resultAt`; runtime properties are `FinalVictim`, `FinalSeed`, `ResultAt`. Match the victim and death seed exactly, and retain the history/once-per-round/witnessed/skip guards. Use `phaseEnd - resultAt` for the Result duration. Only absent presence uses the local selection and five-second overlay fallback. These fields clear at BeginRound. No HUD or replay presentation was edited by Astra.

Managed rules and real relay marker delivery pass. JsonUtility and guest ApplyFrame checks are added to the native Editor gate but are unrun here; Windows visual acceptance remains required.

## Presentation wired to the host decision (Claude, 2026-10-13)

`FinalKillWatch` reads the host decision first. When `HasFinalKillcam` is set, the Result step time is `ResultAt`. When `FinalVictim != 0`, the clip is that victim's death record matched by seed (`NativeFinalKillcam.Decided`). That Result is held to 8.5 s, so the ideal 6.44 s clip plays whole. With `FinalVictim == 0` or an old host, the local `Select` still picks the round-ending death, fitted into the 5 s Result. That covers practice, a death with no enemy killer, a team kill and a draw. This follows the owner's later instruction ("at the end of every round (per set)"); the host rules only qualify online kills by an enemy. The round result overlay ages over `phaseEnd - resultAt` (`ResultSeconds`).

Next (Astra, optional): to give those rounds the full clip too, qualify every round-ending death and practice in `Resolve`, i.e. `FinalKillcams` offline as well as online. The presentation then needs no change.
