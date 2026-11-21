# Expedition quota module and end sequence (presentation)

Claude, 2026-10-20. Owner: "Also, improve ui of expedition quota + expedition ending/completion".
Branch `claude/expedition-quota-ui`. Presentation only: nothing here decides, rolls, awards, saves or sends anything.

## Files

| Part | File |
| --- | --- |
| Quota rules and per-frame model (engine-free) | `Assets/ChipArena/Core/NativeExpeditionQuotaModel.cs` |
| Ending outcome, sequence, model and layout (engine-free) | `Assets/ChipArena/Core/NativeExpeditionEnding.cs` |
| MOCK views for every quota and ending state (DEV preview, checks, renders) | `Assets/ChipArena/Core/NativeExpeditionEndingFixtures.cs` |
| Quota module drawing and its sync | `Assets/ChipArena/Runtime/NativeExpeditionQuotaHud.cs` |
| End sequence drawing | `Assets/ChipArena/Runtime/NativeExpeditionEndingHud.cs` |
| End sequence wiring, keys, actions, DEV preview | `Assets/ChipArena/Runtime/NativeExpeditionEndingUI.cs` |
| Checks (`--expedition-ending`, in the full run) | `Tests/ExpeditionEndingChecks.cs` |

Hooks in shared files are one line each: `NativeExpeditionLiveUI.cs` (sync, keys, the screen, the in-scene top centre),
`NativeExpeditionHud.cs` (`SceneQuota`: the objective tag steps aside), `NativeQuotaMarksPresentation.cs` (the LAST tag steps
aside while the module shows it), `NativeDevPanel.cs` and Core `NativeDevTools.cs` (the DEV preview), `Tests/Program.cs`, and the
allowed-file list in `Tests/QuotaMarkChecks.cs` (the module reads the markers' threshold, words and kite on purpose).

## What existed before

- In run: one objective tag at the top centre (`WARD ZERO · QUOTA 31 / 36`) and, at 5 left, the markers' `LAST 5 · MARKED` tag
  under it. No progress bar, no kill feedback, no emphasis near the end, no moment when the quota was met (the tag changed to
  `QUOTA MET · BOSS INCOMING`).
- At the end: victory opened the J screen once per loop (world board, victory panel with the extract and continue votes, chips);
  the account XP award opened its card over the J screen. Extraction, the run ending and a wipe had no screen of their own: a wipe
  froze the run and the account XP card opened over the board. Leaving goes through the pause menu.

## The quota module (in run)

Top centre, where the objective tag was, while the viewer's zone quota is the objective (`NativeExpeditionQuota.Owns`).

- Tall body (372 x 62 canvas units, narrower canvases down to 300): the zone's accent tick, its name, `KILL QUOTA` (and the loop
  after the first), the party's pips in this zone (lit while up, hollow red while down), the authority's count over the target,
  a bar of one cell per kill (a large target groups kills), and `n LEFT`.
- Every kill (the replicated `progress` rising): the new cells flash, a light runs along the fill to its edge, the count pops
  (+20 % easing out over .28 s). Nothing is predicted: the count is the authority's.
- Closing in (10 left): amber, with a breathing halo and edge.
- The last five (the markers' own threshold, `NativeQuotaMarks.Threshold`): the hunt tone, the remaining cells hollow red, and a
  row with the markers' kite and words (`LAST n · MARKED`) opens under the body: the hand-off to the world markers. The markers'
  separate tag steps aside while the module shows it; the markers, arrows and ping are unchanged.
- Quota met while this client watched: the complete beat, 2.7 s. A flash, a light streak and a soft glow along the screen's top
  edge (never the centre), `QUOTA COMPLETE` popping in over a full green line, then `BOSS INCOMING` (or `EXIT UNSEALED · VOTE TO
  LEAVE` if the zone clears at once). Then the module collapses into the compact strip. Sting: the PvE clear cue (`pve.clear`,
  registered; today's `go` clip without a bank clip), once per quota.
- Compact strip (32 tall): `CENTRAL STATION · BOSS · 0 / 1 DOWN`, `WARD ZERO CLEARED · VOTE TO LEAVE`; the boss plates and the
  departure countdown sit under it as before. The transfer banner and the boss plates are stacked under the module whatever its
  height (`ExpeditionTopStack`).
- A first sight of a met quota (a late joiner, a reconnect, re-entering a zone, after a host transfer) shows the state without the
  beat or the sting. Loading and a host transfer hide the module (the objective tag says so).
- Reduced effects: no flash, streak, sweep, pop or breath; heights change at once. Low graphics: no halo or bar glow.
- Layout: it never reaches `NativeExpeditionQuota.CombatTop` (28 % of the canvas height): the crosshair and the combat numbers in
  front of it are never covered; it stays clear of the wallet panel and the J tag (checked at 9 resolutions). The longest zone name
  and eight pips always fit; the eyebrow and the PARTY label give way first; the compact strip shrinks its type (12, 11, 10) rather
  than clipping.

## The end sequence

On the Expedition screen, in place of the board, so the pointer and spectating follow PR #290 exactly (`ExpeditionScreenOpen`:
the cursor is released and the dead pilot's spectator controls pause). Escape and J close the screen as before; the board's
footer gets RUN SUMMARY back to the ending.

Outcomes (`NativeExpeditionEnding.Classify`, from the replicated view only):

| Kind | When | Title |
| --- | --- | --- |
| Victory | `phase == Victory`, the viewer not extracted (down flagged) | EXPEDITION COMPLETE |
| Extracted | `mine.extracted` (any phase; `phase == Ended` adds "the run has ended") | EXTRACTED |
| Ended | `phase == Ended`, the viewer not extracted (the others left) | RUN ENDED |
| Wiped | nobody connected, alive and not extracted, a party | WIPED |
| Failed | the same, alone | EXPEDITION FAILED |

A pilot down while teammates stand is not an ending (J's board is their progress screen, as before). A host transfer keeps a
decided ending, frozen (its choice waits, the note says so); a wipe is not decided while loading or frozen. Someone reconnecting
standing ends a wipe. Abandon or leave has no replicated state (see the proposal): the leaver goes to the menu, the others see the
pilot `OFFLINE`.

Sequence (s): the world dims behind the kit's blur and letterbox bars come in (0 to .5); the title lands (scale 1.18 to 1, blur
6 to 0, the glitch's split copies settling, .25 to .8), its rule and subtitle follow and a light sweeps it; at 1.6 it rises into
the summary's header while the bars leave; the panel enters (1.8); four stats count up, staggered .12 s, over .9 s each (run time,
zones cleared, quota kills, bosses down: 2.2); the route through the zones to the world boss, where the run ended outlined (2.5);
the party's rows (2.7, you first: weapon, name, YOU, HOST, status); your build, run wallet (counting up), world boss loot and
unopened rewards (3.5); the account XP column docks beside it (4.1); the actions (4.6); settled at 5.2. Space, Enter or a click
skip to the settled summary. Reduced effects: the settled summary at once. A first sight of an ending (a late joiner, a
reconnect, a restart) shows it settled without opening the screen; J shows it. Sting: the match end's cues (`ui.round-won` /
`ui.round-lost`, registered), once per watched ending. The world boss's dice reveal (a stored record appearing at victory) waits
for the rewards, then shows over them, modal.

Count-ups: each value walks at most 40 precomputed steps from 0 to exactly the authority's value (ease out); no string is
built per frame. The run time is the viewer's frozen account XP completion (`members[].xpCompletion.seconds`), else the clock
frozen when the ending began.

Account XP: the account XP panel itself, docked beside the summary (`AccountXpDocked`), so its own rules hold (pending, confirmed,
failed with retry, animate once). While the ending holds the screen the XP card does not pop over it. A canvas too narrow for the
column (720p at interface scale 2) keeps the card, opened over the summary once the sequence reaches it.

Actions (keyboard: left and right or A and D move the focus over the enabled ones, Enter or Space confirm; the mouse hovers to
focus): the pause menu's own Return to Lobby (host or offline, not while a return counts down; guests are told the host can return
everyone), the pause menu's Return to Main Menu (to Join when the host is gone), RUN BOARD, CLOSE.

| Kind | Actions |
| --- | --- |
| Victory | EXTRACT OR GO AGAIN (the board's victory panel: its extract and continue votes), CLOSE |
| Extracted, others still in | MAIN MENU, CLOSE (keep watching), RUN BOARD |
| Ended, wiped, failed | host or offline: RETURN TO LOBBY, MAIN MENU, RUN BOARD; guest: MAIN MENU, RUN BOARD |
| The host gone | MAIN MENU, RUN BOARD |

Layout (`ExpeditionEndingLayout`): the summary centred, or centred together with the XP column (`NativeAccountXpLayout`); two
party columns from three pilots; a short canvas turns compact (and drops the route under 520 canvas units). Checked at
1280x720 @1.5, 1920x1080, 3440x1440, 2560x1080, 3840x2160, 1280x720, 1600x900, 1920x1080 @1.5 and 1280x720 @2 for 1 to 8 pilots,
with and without the XP column and the loop vote.

## What it reads (replicated or authoritative only)

The view's `phase`, `loop`, `elapsed`, `loading`, `migrating`, `area`, `world`, `run`, `host`, `viewer`, `freshRun`,
`finalUnlocked`, `instances[]` (`id`, `progress`, `target`, `entered`, `cleared`, `current`, `bosses`, `defeated`),
`bossArrivals[].area`, `continueVotes`, `dice[]` and `loot[]` (the world boss share, through `NativeExpeditionPresentation.Share`),
`members[]` (`account`, `name`, `weapon`, `alive`, `connected`, `extracted`, `host`, `area`, `extractVote`, `xpCompletion`) and the
viewer's own block (`account`, `alive`, `extracted`, `extractionSaved`, `wallet`, `chips`, `rewards`). Another pilot's wallet, chips
or rewards are never shown (the contract never carries them). `ExpeditionEndingChecks` scans the rules for any other field.

## DEV preview

DEV panel · Previews · EXPEDITION QUOTA & ENDING (developers only; banner `DEV PREVIEW · EXPEDITION QUOTA & ENDING · MOCK VALUES`):
quota fixtures (RUNNING 12/36, CLOSING 28/36, LAST 3, QUOTA MET, BOSS, CLEARED, LOOP 2), ending fixtures (VICTORY, VICTORY ·
DOWN, VICTORY · GUEST, EXTRACTED · SAVING, EXTRACTED · SAVED, RUN ENDED, WIPED, FAILED (SOLO), HOST CHANGE, LATE JOINER, MISSING
DATA), and KILL +1, REPLAY BEAT, REPLAY ENDING. Actions in the preview only say what they would do. The XP column uses the account
XP preview's MOCK while that is on.

## Contract proposal for Astra (not bound; the UI degrades without each)

Nothing below is assumed by the code today: each field's absence is handled (shown as nothing, or derived from what is there).

1. `ExpeditionViewMember.kills` (`int`, public): hostile kills credited to this pilot this run, every area and loop. Set by the
   authority where a monster kill is recorded (`MonsterDefeated`), from the combat authority's killer. Why: per-pilot party rows
   (today a row has only a status), and a per-pilot share of the quota.
2. `ExpeditionViewMember.downs` and `.revives` (`int`, public): times down this run (`Defeat`), revives and reprints this pilot
   paid for or performed (`Revive`, `Reprint` payer). Why: the party rows, as the PvE results show them.
3. `ExpeditionViewMember.bossDamage` (`int`, public, optional): damage dealt to zone and world bosses this run. Needs the combat
   authority's attribution. Why: the party rows' MVP mark, as the PvE results have.
4. `ExpeditionViewPersonal.earned` (`int`, private): money credited to the viewer this run (the sum of `Credit`). Why: RUN WALLET
   today is a balance after spending; the summary would show what the run earned beside it.
5. `ExpeditionView.ending` (a new `ExpeditionEndingView`, public), set once by the authority:
   `string kind` (`"victory"`, `"wiped"`, `"ended"`) at `FinishFinal`, at the `Defeat` that leaves nobody standing, at the last
   `ExtractMember`; `double at` (`elapsed` at that moment); `string area`; `int loop`; optional `string cause` (what downed the
   last pilot: a zone boss archetype, a horde kind, a hazard id, or `"disconnect"`). Why: one authoritative end for every client
   (guests, reconnects), the end time without the account XP completion (absent when XP is disabled or DEV quarantined), and the
   cause of failure (today the summary says where and what phase, not what).
6. `ExpeditionViewMember.extractedAt` (`double`, -1 none) and `.downCause` (`string`): the extracted pilot's own end time; a row's
   "downed by" line.
7. `ExpeditionViewMember.left` (`bool`): the pilot left through the menu (not a dropped connection that can come back). Why: the
   party rows could say LEFT THE RUN instead of OFFLINE, and the leaver's outcome would be distinct from a disconnect.
8. Questions: what a wipe keeps (chips, wallet, rewards: today the summary makes no claim); what extraction banks in a fresh run
   (the summary uses the existing wording: "Your next Expedition starts fresh"); whether Return to Lobby from an ended or wiped
   Expedition should end the session for everyone (the button is the pause menu's own, with its condition).

## Verification

- Managed: `ExpeditionEndingChecks` (`--expedition-ending`): the stages on hand-built views and the real split-area run for all
  three zones; the model's flashes, pop, beat (once, with one cue, only when watched), collapse, LAST row, re-entry, host transfer,
  new loop, Reduced effects and pips; the module's layout and fit at 9 resolutions; every ending fixture and the real authority
  (the preview's victory run: a pilot down, the world boss, an extraction, the continue votes, the next loop; a wipe at the world
  boss; a solo failure; a reconnect after a wipe); count-up timing and monotony; skip; Reduced effects; a host change mid-sequence;
  the actions per outcome and client and their focus; degradation (missing names, weapons, chips, wallet, dice, completion, a bare
  view, the guests' JSON decode); the summary's layout with the XP column; zero allocation per frame; and source scans
  (replicated fields only, presentation only, the hooks, the cursor path, the pause menu's own actions, the registered cue keys,
  the DEV preview, the metas).
- Offline renders: a recording UnityEngine stand-in paints the real Runtime draw classes' IMGUI calls in Chromium over the existing
  offline scene frames (Ward Zero's north hall, Central Station's concourse); the kit blur is emulated with a canvas blur; the
  crosshair, three combat numbers and the J tag are drawn as the game draws them. Not the Unity renderer.
- Not run: Unity Editor play, a Windows build, two clients, a real run to an ending.
