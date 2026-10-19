# Permanent account XP: presentation and proposed data contract

Status (2026-10-19, Claude): the presentation is implemented on `claude/account-xp-presentation`. It is not merged and not
published. The data contract below is a proposal awaiting Astra. Nothing is bound yet: no award or level is shown to players,
and every results screen and the home menu draw exactly as before.

Owner request (2026-10-19): "please start the presentation side of permanent account XP … PvP and PvE completions should award
permanent account XP, mainly from active playtime and objectives." The scope is:

- a post-match XP panel;
- a compact level in the main menu;
- pending, confirmed and failed states;
- DEV preview fixtures.

The presentation must not calculate rewards, choose the curve, write account XP or grant unlocks. It animates each confirmed
completion once and never shows a pending award as saved.

## What the presentation does and does not do

- **Displays** what gameplay and account code supply:
  - the completion id, the status and the retry state;
  - the breakdown's keys, labels and amounts;
  - the totals and levels before and after;
  - the level thresholds.
- **Interpolates** only inside the supplied thresholds, for the animation:
  - the bar's position is `(total − start) / (next − start)` of a supplied band;
  - a level is crossed exactly at a supplied threshold.
- **Validates and degrades.** It never guesses: no curve, no extrapolated band, no corrected sum (see Validation).
- **Keeps a local record** of the completions it has already celebrated. This is UI state only, never account XP:
  - the file is `account-xp-celebrated-v1.txt` in the user data folder;
  - it holds the newest 200 keys, one per line: account, a tab, the completion id.
- **Never** computes an amount, a level, a threshold or an unlock. It never writes account data and never sends anything.
- **Keeps XP apart from in-run chips and currency:**
  - its own ultraviolet (`NativeAccountXpTone`), never the wallet's gold or the chips' cyan;
  - its own words: XP, LEVEL, SAVED, PENDING; never $, chips, credits or money;
  - its own panel beside the results, never inside them.

## Proposed contract v1 (Core `NativeAccountXp.cs`)

The types hold fields only, so Unity's JsonUtility and the managed codec both round-trip them. An absent number reads 0, so
`version: 0` means "not set" and is refused.

### `AccountXpAward`: one completion's award for the viewing account

| Field | Meaning (all values come from gameplay or account code) |
|---|---|
| `version` | 1. A newer version is shown and flagged in DEV; 0 is refused. |
| `completionId` | Unique per completed match or run, per account. Stable across reconnects and re-issues. At most 128 characters, no control characters. |
| `account` | The account the award belongs to. An award for anyone else is never shown. |
| `mode` | `"pvp"`, `"pve"` or `"expedition"`. A display tag only. |
| `status` | `"pending"`: the completion is recorded, the account write is not confirmed. `"confirmed"`: the write succeeded. `"failed"`: the write was refused or could not be confirmed, and nothing is saved. Any other word is refused. |
| `revision` | Rises each time the authority re-issues this completion's award. |
| `lines[]` | `{key, label, xp}`: the breakdown, in display order, shown as given. A missing label falls back to the key. |
| `previousTotal`, `previousLevel` | The account's saved total and level before this award. |
| `newTotal`, `newLevel` | The total and level once this award is saved. |
| `bands[]` | `{level, start, next}`: contiguous, ascending thresholds covering `previousLevel..newLevel` at least. `start <= total < next`. A last band whose `next` is not above its `start` is the top level. |
| `attempt` | Confirmation attempts so far (1 is the first). |
| `retry` | For a failed award: `"auto"` (the authority retries by itself), `"manual"` (the player may ask), `"later"` (retried when the service is reachable again) or `"none"` (not retried). Absent means unknown. |
| `retryIn` | For an `"auto"` retry: seconds from this issue to the next attempt. 0 or less means no countdown. |
| `reason` | For a failed award: the authority's short reason, shown verbatim (at most 200 characters). |

### `AccountXpSummary`: the signed-in account, for the menu

| Field | Meaning |
|---|---|
| `version`, `account` | As above. |
| `known` | False, or a null summary, means unknown: no level is shown. |
| `total`, `level` | The **saved** values. A pending award never adds to them. |
| `levelStart`, `nextLevelAt` | The current level's thresholds. `nextLevelAt` not above `levelStart` means the top level. |
| `pending` | Awards recorded but not yet confirmed. The menu shows a PENDING marker only. |
| `revision` | Rises when the summary changes. |

### Validation (`NativeAccountXpContract.Check`; never throws)

| Input | Result |
|---|---|
| No completion id, an unknown status, version 0, or another account's award | Not shown to players. A developer sees it with its flaws. |
| Totals or levels backwards or negative | The status is shown as given, with "XP details are unavailable"; no numbers. |
| Bands missing, with a gap, a skipped level, a top band in the middle, or not holding both totals | The numbers are shown, without a bar or a fraction. |
| Line sum ≠ `newTotal − previousTotal`, a negative line, more than 32 lines, an unknown retry word | Shown as given, flagged on the DEV line (developers only). |
| 32 or more levels in one award | The chain collapses into one jump with "LEVEL UP ×n". |

## Producer timing (proposed)

1. **At completion:** the authority issues `status: "pending"` with the breakdown, both totals and levels, and the bands. The panel
   shows:
   - the saved level and progress;
   - the gain as a hatched ghost on the bar;
   - "CONFIRMING REWARD…" and "Not saved to your account yet."
   It shows no level-up and never says saved or earned.
2. **After the account write succeeds:** the same `completionId` is re-issued with `status: "confirmed"` and `revision` + 1. The
   totals may differ from the pending issue, for example if another award landed first. The panel animates from this record:
   - the lines lock in;
   - the bar fills inside the bands, with a pop and flash at every threshold crossed;
   - the panel says SAVED TO YOUR ACCOUNT.
   This happens once, ever, for this account and completion.
3. **If the write fails:** the same `completionId` is re-issued with `status: "failed"`, `attempt`, `retry`, `retryIn` and `reason`.
   The panel shows "COULDN'T CONFIRM YOUR XP", the reason and the retry state:
   - `auto`: "RETRYING IN 9s" while a `retryIn` is running, otherwise "RETRYING AUTOMATICALLY…";
   - `manual`: a TRY AGAIN button (binding 3);
   - `later`: "WILL RETRY WHEN YOU'RE BACK ONLINE";
   - `none`: "THIS REWARD WAS NOT SAVED".
   Nothing is shown as saved; the gain stays a red hatched ghost marked NOT SAVED.
4. **A retry** re-issues the same `completionId` as pending (`attempt` + 1), then as confirmed or failed. The celebration happens
   at the confirmation.
5. **The menu summary** refreshes after each confirmed write and at sign-in. Until then the menu shows the previous saved level,
   with PENDING while `pending > 0`.

## Animate once

- The first sight of a confirmed completion starts the animation and writes it to the local record at once.
- A completion already in the record shows its final state immediately. That covers a reopened panel, another screen, a new
  session, a re-issued confirmed record, reduced effects, and a producer handing over a fresh copy every frame.
- A panel closed and reopened mid-way carries on from the same clock; it does not restart.
- Pending and failed awards are never recorded, so the celebration lands at the confirmation.
- DEV preview ids (`mock-…`) are remembered for the session only and are never written.

## The bindings (Runtime `NativeAccountXpUI.cs`): one line each

```csharp
internal AccountXpAward AccountXpAwardOf(AccountXpSurface surface)=>null;   // 1. the viewer's award on this results surface
internal AccountXpSummary AccountXpSummaryOf()=>null;                        // 2. the signed-in account's saved XP
internal Action<string> AccountXpRetryOf()=>null;                            // 3. optional: a manual retry by completionId
```

Examples are in the file's comment. For instance, `=>accountXpFeed?.Award(account.Access.AccountId,surface);` and
`=>account?.Account?.xp;`. `AccountXpSurface` is `Pvp` (MatchEndPanel), `Pve` (PveResultsPanel) or `Expedition` (the J
screen's card).

**Tripwire:** `AccountXpChecks` fails if account XP data appears in any non-presentation file while bindings 1 and 2 are still
unbound. It looks for:

- `new AccountXpAward`;
- a `completionId` or `accountXp…` field;
- `"account-xp"` messages;
- an `xp` or `level` field on `NativeAccountData`.

## Where it shows

- **PvP match end / PvE results:** a column of its own (300–380 wide) beside the results, the pair centred. Its top is level
  with the results' top. The results narrow to make room, never below their minimum (PvP 480, PvE 420). Without an award they
  keep their exact old place.
- **Expedition:** a card over the J screen. The screen opens by itself on a new completion's award, and again when that award
  is confirmed, so the one celebration is seen. A completion already celebrated (a rejoin or a restart) stays closed. Close it
  from the card; reopen it from the footer's ACCOUNT XP button (final state).
- **Home:** a third line under the pilot's nameplate. It shows a small XP hexagon with the level, LEVEL n, a thin bar of the
  saved progress, and PENDING while awards wait. Nothing is shown when the summary is unknown.
- **Layout:** fits 1280×720 @1.5 to 4K and 21:9. A canvas too narrow for both panels gets a compact card at the top right,
  without the breakdown. A short canvas uses compact rows, then fewer lines with "+n MORE".
- **Reduced effects:** the final state at once; no rise, blink, pop, burst or flash.

## DEV preview (MOCK values)

To open it, use DEV panel → Previews → ACCOUNT XP (DEV PREVIEW · MOCK VALUES). It is for developers only.

- **Results fixtures:**
  - ZERO, PROGRESS, LEVEL UP, MULTI LEVEL;
  - PENDING, FAILED · AUTO, FAILED · TRY AGAIN;
  - LINE SUM OFF, NO THRESHOLDS.
  The panel shows over the match with the banner "DEV PREVIEW · ACCOUNT XP · MOCK VALUES", and beside the real PvP or PvE
  results while the preview is on.
- **Mocked authority steps:** MOCK CONFIRM and MOCK FAIL re-issue the same completion; REPLAY restarts it.
- **Menu fixtures:** UNKNOWN, ZERO, PROGRESS, PENDING, MAX LEVEL. They show under the home nameplate with a MOCK tag.
- **What is mock:** every amount, total, threshold, label and reason (`NativeAccountXpFixtures`). None of it is a proposed
  reward, curve, perk or unlock.

## Questions for Astra

1. **Where is a completion confirmed?** By the match host, the account service (`/api/accounts/…`) or a dedicated server? Who
   computes the award, and which side is authoritative for the totals, levels and bands?
2. **How does a guest learn its award?** A per-player host message at match end, or polling the account service by completion
   id? Should a guest see the pending issue before its own account write?
3. **Offline, solo, LAN, sandbox and DEV matches, bots, signed-out players:** which of these award XP? The presentation shows
   nothing without an award.
4. **The completion id:** its format and uniqueness scope (global, or per account). Is it stable across reconnects, the 90 s
   seat reclaim, host promotion, Expedition resume and rematch? Is a rematch a new completion?
5. **Can the thresholds or totals change between pending and confirmed?** For example, another award confirmed first, or a
   curve change. The panel animates from the confirmed record's `previousTotal`; is that what you expect?
6. **The most levels one award can grant, and the level cap.** Is there a top level (a band without `next`)? The presentation
   collapses 32 or more levels into one jump.
7. **Can an award be 0 XP?** Can a line be negative, for example a penalty? Can the lines' sum differ from the totals' difference
   (rounding, caps)? Today a mismatch is shown as given and flagged in DEV.
8. **Breakdown keys and labels:** who supplies the labels, and how are they localised? Is there a maximum number of lines, and
   what is their order?
9. **Retries:** are `auto`, `manual`, `later` and `none` the right set? Is a player-initiated retry allowed (binding 3)? Is there
   a maximum number of attempts? Can an award be dropped for good?
10. **Several awards pending** (for example a few matches played offline): does the results surface show only its own
    completion, and the menu just the count?
11. **Expedition:** is a completion an individual extraction, the run's end or each loop? Do dropped or extracted members get
    one? How does it relate to EXTRACTED · SAVED?
12. **PvP:** one completion per match (a set of rounds), never per round?
13. **The menu summary:** part of `NativeAccountData` (`/api/accounts/me`) or its own endpoint? How soon after a confirmed write
    does it refresh?
14. **Future level-up rewards** (unlocks, perks) are out of scope here. If they come, the award would list them and the
    presentation would only display them.

## Checks

`AccountXpChecks` (`--account-xp`, in the full run) covers:

- validation and degradation, and a fuzz;
- every fixture's sequence inside the supplied thresholds, single and chained;
- pending never saved, and never added to the menu;
- the failed retry states;
- animate once across reopen, restart and pending → confirmed;
- the bounded local record;
- separation from chips and currency (tones and words);
- the unbound bindings and the tripwire;
- zero per-frame allocation;
- layouts at 1280×720 @1.5, 1920×1080, 2560×1080, 3440×1440 and 3840×2160, plus other sizes;
- the wiring: only these surfaces reach the XP presentation; no network, account or authority file does; the only file written
  is the local record.
