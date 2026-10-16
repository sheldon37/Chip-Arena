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
18 optional files. `--key pve.zombie.moan` selects a key; repeat `--key` to select
several. `--variant 2` selects a take; repeat it for multiple variants. To regenerate,
select the desired keys/variants and use a NEW output folder; originals are never
overwritten. Without `--out`, a new UTC timestamp folder under Documents is used.
`--yes` explicitly authorizes the printed paid batch without the interactive prompt.
`Generate-Sounds.cmd` in the Unity project runs the same script, forwards arguments,
and pauses on success/failure. A double-click uses the environment inherited by
Explorer; launch it from the configured PowerShell session if the key is session-only.
Clear the key afterward with `Remove-Item Env:ELEVENLABS_API_KEY`.

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
372 credits; 70 zombies = 95.9 s / 3,836 credits; all 88 = 106.7 s / 4,268 credits.
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
