# Chip Arena — test update channel

> DRAFT for `sheldon37/chip-arena-test-updates/README.md`, kept on tooling branch
> `claude/test-update-channel` (`docs/TEST-CHANNEL-README.md`). Copying it into the channel
> repository is part of the next channel publication. It is not published yet.

**Test-only.** This public repo is the update channel for Chip Arena **TEST-ONLY** builds.
It is not a player release and not the live update feed. The game source stays in the private
`sheldon37/Chip-Arena` repo. Anyone with the URL can download these test game files.

## Get the test build (seed install)

Download `Chip-Arena-TEST-ONLY.zip` from this repo's **Releases** page. This link always points
to the newest seed:

```
https://github.com/sheldon37/chip-arena-test-updates/releases/latest/download/Chip-Arena-TEST-ONLY.zip
```

Extract it into a new, writable folder. Close the normal game first, then run `Chip Arena.exe`.
Settings stay in `TEST-PROFILE` inside that folder. Later versions arrive through the in-game
**Update** button, so testers download the ZIP only once.

You need a new ZIP when:

- you have a build from before updater v2 (2026-10-03). Its Update button can only install
  updates up to 32 MiB. When an update is larger, that old build shows the version
  `…-download-the-new-TEST-ZIP-from-GitHub-Releases`. Old builds also stop a schema-1 download
  after 60 seconds and a whole update after 90 seconds, so near 32 MiB they need about
  4.5 Mbit/s or more. On a slower connection, use the new ZIP.
- the Electron shell or the test bootstrap changed. Only the game and the `launcher/` files
  update themselves.

## How it works

TEST-ONLY builds read this feed:

```
https://raw.githubusercontent.com/sheldon37/chip-arena-test-updates/main/feed/
```

| File | Read by | Format | Limit |
| --- | --- | --- | --- |
| `feed/latest-v2.json` + one `arena-<16 hex>.pack` | Builds with updater v2 (seeds from 2026-10-03), first | Schema 2: Brotli container, raw files, per-file SHA-256, also carries launcher files | Launcher: 256 MiB. This channel: 95 MiB, because GitHub rejects files over 100 MB. |
| `feed/latest.json` + one `arena-<16 hex>.json.gz` | Every older test build, plus v2 builds when `latest-v2.json` is missing | Schema 1: gzip of base64 JSON | 32 MiB, enforced by every installed launcher |

- The Update button and the menu badge read the same manifest. A v2 build falls back to
  `latest.json` only when `latest-v2.json` returns 404. It never falls back to the live feed.
- The updater (`launcher/update.mjs`) checks the manifest, the size cap, the byte count and the
  payload SHA-256 before it unpacks anything. It then checks every file's path, size and
  SHA-256. Game files go to `server.mjs`, `dist/client/`, `drizzle/` and `release.json`.
  Launcher files must be on a fixed list.
- Updater v2 stages launcher files and applies them at the next start (an Update or Create Room),
  keeping a backup. The new files stay on probation until they have worked once in a later run:
  an Update that installs a version for the update files, and a Create Room that reaches
  "YOUR GAME:" for `start.mjs` and `update.mjs`. Until then, a failed Update or a failed room start
  restores the previous launcher. The desktop shell does this, and it never updates itself.
  Not covered: a failure after "YOUR GAME:" (for example the online invite), and a bug that only
  appears after the new launcher has already installed one update. The feed still lists a
  rolled-back launcher, so each later Update stages it again and the one after restores it again;
  publish a fixed version, which the restored launcher installs. Settings, saves, `runtime/` and
  `test-channel.json` are never update targets.
- A v2 build never installs the schema-1 hold notice (`"hold": true` in `latest.json`). If
  `latest-v2.json` is briefly missing while the notice is up, Update says to try again later.
- Raw GitHub files can be cached for about 5 minutes, so a new version can take a few minutes to
  appear.

Tooling: branch `claude/test-update-channel` of the private repo (`scripts/release/*`,
`docs/RELEASE-CANDIDATES.md`). Updater v2: branch `claude/update-headroom`
(`launcher/update.mjs`, `scripts/package-update.mjs`, `docs/DEVELOPMENT.md`).

## Publishing a new test version (Claude or Astra)

1. Build a candidate from an exact, approved commit. You need Node 24.19.0, npm 11.9.0 and
   Python 3.

   ```
   python scripts/release/candidate.py --commit <40-char SHA> --output <new dir> --test-only \
     --channel https://raw.githubusercontent.com/sheldon37/chip-arena-test-updates/main/feed/
   ```

   Check `candidate.json`: `repeat-payload`, `actual-payload`, `isolated-test-package` and
   `channel-e2e` must pass. `channel-e2e` installs the candidate's own ZIP and drives its real
   Update path (and Create Room) against a local HTTPS copy of this feed, including launcher
   rollback; it needs `openssl`. `payloadBytes` shows the schema-1 and schema-2 sizes.
2. Replace `feed/` in this repo:
   - **Schema 1 fits** (the candidate has `feed/latest.json`): copy all four candidate files:
     `latest.json`, its `arena-*.json.gz`, `latest-v2.json` and its `arena-*.pack`.
   - **Schema 1 held** (the candidate has only `latest-v2.json`): keep old installs on the last
     version that fit, and show them the notice:

     ```
     node scripts/release/schema1-hold.mjs <this repo>/feed <candidate>/feed <tmp>/hold
     ```

     Copy `<tmp>/hold/latest.json` with its `arena-*.json.gz`, plus the candidate's
     `latest-v2.json` with its `arena-*.pack`.

   Delete the old payloads. `feed/` must end with exactly two manifests, one `.json.gz` and one
   `.pack`.
3. Commit, naming the source commit and content version, then push to `main`.
4. Add a row to `PUBLISHED.md`, and record the version in the private repo's `docs/DEV_LOG.md`.
5. After about 5 minutes, check that the raw `latest-v2.json` and `latest.json` show the new
   values.

### Publishing a seed ZIP as a GitHub Release (owner or orchestrator only)

Use this procedure for the first updater-v2 seed, and again whenever a new ZIP is needed (see
above). Use the `Chip-Arena-TEST-ONLY.zip` from the **same candidate** whose feed you publish,
built from the merged game commit. The ZIP embeds the source archive, so its SHA-256 changes with
every game commit, even when the game itself is unchanged.
`make-test-package.py` has already checked that its launcher files equal the ones in
`latest-v2.json`.

1. Publish that candidate's feed first (steps 1-4 above), so new seeds start current.
2. Record the asset hash: `sha256sum Chip-Arena-TEST-ONLY.zip`. The current ZIP is about
   300 MB; GitHub release assets may be up to 2 GB.
3. Create the release on `sheldon37/chip-arena-test-updates`. Use the web UI (Releases → Draft a
   new release) or the GitHub CLI:

   ```
   gh release create seed-<content version> Chip-Arena-TEST-ONLY.zip \
     --repo sheldon37/chip-arena-test-updates --target main \
     --title "TEST-ONLY seed <content version> (updater v2)" --notes-file notes.md --latest
   ```

   - Tag `seed-<content version>`, targeting the channel commit that publishes the same feed.
   - Keep the asset name exactly `Chip-Arena-TEST-ONLY.zip`, so the `/releases/latest/download/`
     link above stays stable.
   - Mark it **Latest**, not a pre-release, because `/releases/latest` skips pre-releases.
   - Notes should say: NOT A PLAYER RELEASE. Also give the SHA-256, the source commit, the tooling
     commit, the content version, "extract to a new folder, close the normal game", and that later
     versions arrive through the Update button.
4. Verify the release. Download the asset through the `/releases/latest/download/` link, compare
   its SHA-256, and on Windows extract it, start it, and check the badge and the Update button.
5. Record the tag, the asset SHA-256 and the source commit in `PUBLISHED.md` and in the private
   repo's DEV_LOG.

Release assets do not add to this repository's Git history. Older seed releases can stay, or the
owner can delete them.

## Rules

Never put credentials, player data or signing keys here. Publish only builds from reviewed
branch commits. Moving these builds to the live feed is a separate release that the owner must
approve. Every feed publication adds its payloads (about 26 MB + 22 MB today) to this repo's Git
history. Shrinking that history (squash and force-push) needs the owner's approval.
