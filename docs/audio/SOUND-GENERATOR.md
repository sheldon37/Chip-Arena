# ElevenLabs Windows batch generator

Astra's generator reads Claude's `sound-requests.json` verbatim and writes the
`chip-arena.sound-receipt/1` contract. It generates originals only; Claude's importer
owns trimming, loudness, mono conversion and bank packaging. No audio is committed.

## Setup and use (from repository root)

Install Python 3.11+ and ffmpeg; `py -3 --version` and `ffmpeg -version` must work.
No Python packages are needed. In PowerShell, set the key for this process without
putting the value in shell history (it is held in the environment only):

```powershell
$audioSecret = Read-Host 'ElevenLabs API key' -AsSecureString
$env:ELEVENLABS_API_KEY = [System.Net.NetworkCredential]::new('', $audioSecret).Password
Remove-Variable audioSecret
```

Check planned usage (no key, API calls, or output writes):

```powershell
py -3 unity/ChipArenaConversion/tools/generate_sound_requests.py --dry-run
py -3 unity/ChipArenaConversion/tools/generate_sound_requests.py --sample-only --dry-run
```

Generate the six-file listening sample and validate it against the importer:

```powershell
py -3 unity/ChipArenaConversion/tools/generate_sound_requests.py --sample-only --out "$env:USERPROFILE\Documents\ChipArena\SoundRequests\zombie-sample" --check
```

It prints the plan and requires `GENERATE` before paid requests. After listening,
continue into the same folder; already completed variants are verified and skipped:

```powershell
py -3 unity/ChipArenaConversion/tools/generate_sound_requests.py --out "$env:USERPROFILE\Documents\ChipArena\SoundRequests\zombie-sample" --check --zip "$env:USERPROFILE\Documents\ChipArena\zombie-batch.zip"
```

Use a new ZIP path each time. Both batch and ZIP must be outside the repository.
A complete new batch defaults to 70 zombie files. `--include-player-steps` adds the
78 optional files (18 player steps and the 60 Central Station cues, which a pack-designed
batch already covers). `--key pve.zombie.moan` selects a key; repeat `--key` to select
several. `--variant 2` selects a take; repeat it for multiple variants. To regenerate,
select the desired keys/variants and use a NEW output folder; originals are never
overwritten. Without `--out`, a new UTC timestamp folder under Documents is used.
`--yes` explicitly authorizes the printed paid batch without the interactive prompt.
`Generate-Sounds.cmd` in the Unity project runs the same script, forwards arguments,
and pauses on success/failure. A double-click uses the environment inherited by
Explorer; launch it from the configured PowerShell session if the key is session-only.
Clear the key afterward with `Remove-Item Env:ELEVENLABS_API_KEY`.

## The Backrooms bosses' batch

The same tool with the second manifest (`docs/audio/SOUND-REQUESTS.md`). `--sample-only` doesn't apply: it names zombie keys. A short listening sample is `--key` repeated:

```powershell
py -3.14 unity/ChipArenaConversion/tools/generate_sound_requests.py --manifest docs/audio/sound-requests-bosses.json --key boss.hum.tube.strike --key boss.hum.tear --key boss.hum.scratch --key boss.deepend.break --key boss.deepend.drip --key boss.deepend.iris --variant 1 --out "$env:USERPROFILE\Documents\ChipArena\SoundRequests\boss-sample" --check
```

Then the rest into the same folder; finished takes are skipped:

```powershell
py -3.14 unity/ChipArenaConversion/tools/generate_sound_requests.py --manifest docs/audio/sound-requests-bosses.json --out "$env:USERPROFILE\Documents\ChipArena\SoundRequests\boss-sample" --check --zip "$env:USERPROFILE\Documents\ChipArena\boss-batch.zip"
```

## Resume and uncertain requests

Re-run with exactly the same manifest and `--out`. Completed files are decoded and
SHA-256 checked before being skipped. Changed manifest bytes/settings, corrupt audio,
unknown files or incompatible receipts stop rather than silently spending credits.
Do not run two generators against the same folder.

Before each request, a secret-free `pending-request.json` marker is written. If the
network outcome is uncertain or the process crashes, automatic resume refuses that
folder to avoid a duplicate paid request. Check ElevenLabs history/usage and inspect
the local files first. If the completed file is already in receipt.json with matching
hash, archive/remove the leftover marker and resume. If the request definitely failed
and no unreceipted audio exists, archive/remove the marker and resume. If generation
may have been billed but no usable download exists, explicitly choose whether to pay
for a new take in a new folder. Never delete the whole batch to fix one request.

Only explicit HTTP 429 rejection is retried, at most three retries with bounded
backoff/Retry-After. Authentication/credit errors, server errors and connection/timeouts
stop. Explicit HTTP 4xx rejection (except uncertain 408) clears the pending marker
so correcting the account/key and resuming is straightforward. Response bodies and credentials are not printed. A 5xx/timeout retry could be
another paid generation, so it is not automatic. Invalid or undecodable audio stops
before adding it to the completed receipt. Originals and receipts are kept locally.

## Current API contract and estimate

Verified official documentation 2026-10-16:
- https://elevenlabs.io/docs/api-reference/text-to-sound-effects/convert
- https://elevenlabs.io/docs/overview/capabilities/sound-effects

POST `/v1/sound-generation`, model `eleven_text_to_sound_v2`, loop false,
`duration_seconds` 0.5–30, prompt influence from the manifest (0.65), and
`output_format=mp3_44100_192`. This format requires Creator tier or above according
to the API reference. Do not silently downgrade it; coordinate any manifest change
with the importer. The overview's WAV options do not override the shared MP3 contract.

At the overview's documented 40 credits per specified second: six samples = 9.3 s /
372 credits; 70 zombies = 95.9 s / 3,836 credits; the 88 zombie and step files = 106.7 s /
4,268 credits; all 148 = 203.5 s / 8,140 credits.
These are estimates for successful requests, not account-specific quotes or dollar
prices. Live generation, billing, audio quality and Windows double-click execution
have not been exercised in the development environment.

## Import

`--check` runs Claude's exact importer command on the folder. It validates packaging,
not listening quality. A partial sample produces expected missing-variant warnings.
Send the resulting ZIP to Claude, or run the import command in SOUND-REQUESTS.md with
the current private LocalAssets ZIP. The importer output ZIP must also be outside
the repository. No key is needed for import.

Verification: mocked request/rate-limit/error tests, real ffmpeg decoding of synthetic
MP3 fixtures, folder + ZIP importer checks, BOM receipts, resume, stale-hash/corrupt
file refusal. Tests never contact ElevenLabs.

## Automatic builder queue (existing ElevenLabs account)

No fal account is used. On the Windows builder PC, in a complete current checkout,
open `unity/ChipArenaConversion/Setup-Sound-Queue.cmd` once. Use the same Windows
user that runs the builder. Enter your ElevenLabs API key, request/duration limits,
and optionally select the existing Drive build-log folder for status. Python 3
with Tk and ffmpeg on PATH are required. This setup makes no API calls.
The key is encrypted with Windows DPAPI under
`%LOCALAPPDATA%\ChipArena\SoundQueue`; it never goes into GitHub or command arguments.
You can reopen setup to disable processing or change limits.

The normal `install_local_assets.py` asset-install step processes approved jobs in
`docs/audio/sound-queue.json`. `--check`, `--remove`, missing setup, disabled setup,
and the initially empty queue never generate audio. No Windows launcher changes
are needed **if the builder invokes this repository's installer**. A builder using
its own copied installer must be pointed at the current repository script first.
After setup, request a specific batch in chat; the approved queue is committed and
synced to the test builder. No recurring manual shell commands are needed.

Each job has an immutable unique `id`, `approved: true`, a `manifest` path under
`docs/audio`, the exact file's `manifestSha256`, explicit `keys` and `variants`
(e.g. `[1]`), plus `maxRequests` and `maxSeconds`. Both job limits and local limits
are checked before requests. Local limits cap **pending work per run**, not currency
or a monthly budget. Keep completed jobs in the queue: the original LocalAssets
ZIP is not overwritten, so their cached imports must remain in the composed pack.
Changing a completed job requires a new reviewed ID; do not use a new ID to bypass
an uncertain paid request. The queue is empty until a batch is selected.

Original MP3s, receipts, cached augmented ZIPs and `Latest-Sound-Status.json` stay
in that local folder. Each completed request is reused on subsequent builds.
An interrupted/uncertain paid request blocks instead of retrying automatically;
inspect `pending-request.json` and ElevenLabs history before resolving it.
Failed imports retain originals and do not regenerate. A status of `complete`
means assets were imported, **not** that Unity built or published successfully.
Confirm the separate builder status and playtest before declaring delivery.
The Windows GUI, DPAPI and live API path still need first-run verification on the
owner's PC; automated tests mock the network and credential store.

## Unity-only builder exports

The default queue falls back to `tools/sound_bundle/docs/audio` when root `docs/audio/sound-queue.json` is absent. Keep all three JSON files in that bundle byte-identical to their canonical docs/audio files whenever changing approved jobs or manifests; `package_source.py` checks for drift. Existing job IDs, manifest-relative paths and hashes are preserved. No API calls occur merely to resolve inputs.

Status and completed originals ZIPs are mirrored beside the input LocalAssets ZIP. Use a direct Drive folder listing if newly created files do not appear in search.
