# Windows candidate validation — do not use the working installation

1. Read candidate.json. BLOCKED means diagnostic testing only, not release approval.
   Verify the test ZIP using PowerShell:
   `Get-FileHash .\Chip-Arena-TEST-ONLY.zip -Algorithm SHA256`
   Compare the result with candidate.json's artifact entry.
2. Close normal Chip Arena and extract into a NEW writable folder, for example
   `F:\ChipArenaTests\candidate-001`. Do not extract over the working installation.
   Prefer a separate Windows account/VM. Record OS/GPU, source/tooling SHA, and hashes.
3. Run `Chip-Arena-TEST-ONLY\Chip Arena.exe`. Confirm TEST-PROFILE appears inside that
   extracted folder. Do not sign in yet. Record startup, rendering, offline bots,
   greatsword/shield attacks, block, hitstun, dodge/Emperor wavedash, teams/labels,
   intros, spectating and lobby return. Distinguish pre-existing defects from regressions.
4. Set an obvious local preference (e.g. volume). Click the in-game Update button.
   It should report installation of candidate.json's content version, reopen the game,
   and retain that preference. Click Update again: it should report up to date.
   Restart the test game and confirm the preference remains. Inspect
   resources/game/release.json for the candidate version. Do not replace this file in
   the working installation. The seed is an artificial previous version, not old code.
5. Close the test game. In THIS TEST FOLDER ONLY, rename
   resources/game/test-feed/latest.json to latest.json.disabled. Reopen and click Update:
   an error should be visible and installed gameplay should remain usable. Close,
   restore the name, reopen and check again. Never edit the live feed for this test.
6. For an invalid-hash case, use a fresh extraction, change only the test-feed manifest's
   sha256 to 64 zeros, then click Update. It must refuse the package and retain usable
   game files. Discard that test extraction afterward; keep the original ZIP unchanged.
7. Confirm the normal installation was not altered: after closing the test app, launch
   the normal app and compare its version/settings with your before-test notes.

Additional release gates (NOT satisfied by the isolated fixture):
- Actual current/older installation -> candidate with real prior files, offline/download
  failure, interrupted staging/install, startup recovery and a tested rollback. Use a
  backed-up disposable copy under a separate Windows account/VM; do not test failures
  on your working installation. A controlled old-version test feed fixture must be
  prepared for that copy first; do not point production at a test feed.
- Real two-machine host/join, LAN and online modes, draft/match/rematch/leave, room
  directory and relay; choose test accounts and avoid changing your main profile.
- Login/logout/remember-me, encrypted session restoration, local preferences and
  customization with explicit test accounts. Real services remain in use.
- Verify cancellation while hosting: cancelling Update must keep the room; confirming
  it should stop the owned server and disconnect guests cleanly before replacement.
- Native executable security prompts/GPU behavior and shell/runtime compatibility.

Record each row as passed / failed / not run, with version, evidence and reproduction.
Approval must identify the exact source SHA and feed hash. A screenshot of a running
menu alone does not establish update compatibility or multiplayer correctness.
