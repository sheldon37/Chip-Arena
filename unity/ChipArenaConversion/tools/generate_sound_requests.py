"""Generate an ElevenLabs batch compatible with Claude's sound-request importer.
No network in dry-run. Originals only; the importer owns audio processing.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import http.client
import json
import math
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request
import zipfile

REPO = Path(__file__).resolve().parents[3]
MANIFEST = REPO / 'docs/audio/sound-requests.json'
MODEL = 'eleven_text_to_sound_v2'
VERSION = 'chip-arena-sound-generator/1'
SAMPLE = {'pve.zombie.moan', 'pve.zombie.attack', 'pve.zombie.hurt',
          'pve.zombie.death', 'pve.zombie.spitter.gurgle', 'pve.zombie.step.shuffle'}
MAX_AUDIO = 16 * 1024 * 1024


def digest(data):
    return hashlib.sha256(data).hexdigest()


def utc():
    return datetime.now(timezone.utc).isoformat()


def atomic(path, data):
    tmp = path.with_name(path.name + '.tmp')
    with tmp.open('wb') as stream:
        stream.write(data)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(tmp, path)


def write_receipt(folder, receipt):
    atomic(folder / 'receipt.json', (json.dumps(receipt, indent=2) + '\n').encode())


def outside_repo(path):
    path = path.expanduser().resolve()
    if path == REPO or REPO in path.parents:
        raise ValueError('Batch folders and ZIPs must be outside the repository.')
    return path


def load_manifest(path):
    raw = path.read_bytes()
    m = json.loads(raw.decode('utf-8-sig'))
    if m.get('schema') != 'chip-arena.sound-requests/1':
        raise ValueError('Unsupported manifest schema.')
    g = m['generation']
    if (m['file']['pattern'] != '<key>_<NN>.<ext>' or
            g.get('service') != 'ElevenLabs Sound Effects' or
            g.get('promptIsComplete') is not True or g.get('oneGenerationPerFile') is not True):
        raise ValueError('Unsupported generation contract.')
    if g['outputFormat'] != 'mp3_44100_192':
        raise ValueError('This generator supports the agreed mp3_44100_192 contract only.')
    if not isinstance(g['promptInfluence'], (float, int)) or not 0 <= g['promptInfluence'] <= 1:
        raise ValueError('Invalid prompt influence.')
    keys = set()
    for s in m['sounds']:
        if not re.fullmatch(r'[a-z0-9]+(?:[.][a-z0-9]+)*', s['key']) or s['key'] in keys:
            raise ValueError('Invalid or duplicate sound key.')
        keys.add(s['key'])
        if type(s['count']) is not int or not 1 <= s['count'] <= 99 or s['priority'] not in (1, 2, 3):
            raise ValueError('Invalid count/priority.')
        duration = s['generateSeconds']
        if not isinstance(duration, (float, int)) or not math.isfinite(duration) or not .5 <= duration <= 30:
            raise ValueError('Invalid generation duration.')
        if not isinstance(s['prompt'], str) or not s['prompt'].strip():
            raise ValueError('Missing prompt.')
    return m, digest(raw)


def entries(m, keys=(), sample=False, optional=False, variants=()):
    known = {s['key'] for s in m['sounds']}
    if set(keys) - known:
        raise ValueError('Unknown --key selection.')
    selected = [s for s in m['sounds'] if (not keys or s['key'] in keys)
                and (optional or s['priority'] < 3) and (not sample or s['key'] in SAMPLE)]
    if not selected:
        raise ValueError('No sounds selected; optional keys require --include-player-steps.')
    result = []
    for s in selected:
        numbers = [1] if sample else list(variants or range(1, s['count'] + 1))
        if any(v < 1 or v > s['count'] for v in numbers):
            raise ValueError('Variant outside selected key count.')
        for v in sorted(set(numbers)):
            result.append(dict(file=f"{s['key']}_{v:02d}.mp3", key=s['key'], variant=v,
                               prompt=s['prompt'], durationSeconds=s['generateSeconds'],
                               promptInfluence=m['generation']['promptInfluence'],
                               outputFormat=m['generation']['outputFormat'], model=MODEL))
    return result


def validate_audio(path):
    if not path.is_file() or not 128 <= path.stat().st_size <= MAX_AUDIO:
        raise ValueError('Missing, empty or oversized audio.')
    if not shutil.which('ffmpeg'):
        raise ValueError('ffmpeg must be on PATH to validate downloaded audio.')
    check = subprocess.run(['ffmpeg', '-v', 'error', '-xerror', '-i', str(path),
                            '-map', '0:a:0', '-f', 'null', '-'], capture_output=True, timeout=60)
    if check.returncode:
        raise ValueError('Audio failed ffmpeg decode validation.')


def open_batch(folder, sha, all_entries):
    receipt_path = folder / 'receipt.json'
    if not folder.exists():
        return dict(schema='chip-arena.sound-receipt/1', manifestSha256=sha,
                    generatedAt=utc(), generator=VERSION, files=[])
    if not receipt_path.is_file():
        raise ValueError('Existing folder has no receipt. Use a new batch folder.')
    receipt = json.loads(receipt_path.read_text(encoding='utf-8-sig'))
    if (receipt.get('schema') != 'chip-arena.sound-receipt/1' or receipt.get('manifestSha256') != sha
            or receipt.get('generator') != VERSION):
        raise ValueError('Stale/incompatible receipt. Use a new folder; originals are preserved.')
    if set(receipt) != {'schema', 'manifestSha256', 'generatedAt', 'generator', 'files'}:
        raise ValueError('Unexpected receipt metadata; refusing to copy it into an archive.')
    expected = {e['file']: e for e in all_entries}
    seen = set()
    for e in receipt['files']:
        name = e.get('file')
        if name in seen or name not in expected or set(e) != set(expected[name]) | {'sha256'} or any(e.get(k) != v for k, v in expected[name].items()):
            raise ValueError('Receipt settings differ. Use a new batch folder.')
        seen.add(name)
        path = folder / name
        validate_audio(path)
        if digest(path.read_bytes()) != e.get('sha256'):
            raise ValueError('Completed audio changed or is corrupt; preserve it and use a new folder.')
    extras = [p.name for p in folder.iterdir() if p.name not in seen | {'receipt.json'}]
    if extras:
        raise ValueError('Unreceipted/partial files found. Preserve this folder and use a new one; do not silently regenerate.')
    return receipt


class RejectedRequest(ValueError):
    """Explicit rejection before a successful generation; safe to resume."""


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def request_audio(entry, secret, opener=None, sleep=time.sleep):
    payload = dict(text=entry['prompt'], duration_seconds=entry['durationSeconds'],
                   prompt_influence=entry['promptInfluence'], model_id=MODEL, loop=False)
    request = urllib.request.Request('https://api.elevenlabs.io/v1/sound-generation?output_format=' + entry['outputFormat'],
                                     data=json.dumps(payload).encode(),
                                     headers={'xi-api-key': secret, 'Content-Type': 'application/json'})
    opener = opener or urllib.request.build_opener(NoRedirect()).open
    for attempt in range(4):
        try:
            with opener(request, timeout=180) as response:
                if response.status != 200 or not response.headers.get('Content-Type', '').split(';')[0].strip() in ('audio/mpeg', 'audio/mp3', 'application/octet-stream'):
                    raise ValueError('Unexpected API response; batch stopped without logging its contents.')
                data = response.read(MAX_AUDIO + 1)
                if not 128 <= len(data) <= MAX_AUDIO or not (data.startswith(b'ID3') or data[0] == 255 and data[1] & 224 == 224):
                    raise ValueError('API returned invalid MP3 data.')
                return data
        except urllib.error.HTTPError as error:
            # Only explicit rate rejection is safely retryable without a duplicate paid generation.
            if error.code == 429 and attempt < 3:
                try:
                    delay = max(1, float(error.headers.get('Retry-After', 2 ** (attempt + 1))))
                except (ValueError, TypeError):
                    delay = 2 ** (attempt + 1)
                if not math.isfinite(delay) or delay > 60:
                    raise RejectedRequest('Rate limited for over 60 seconds; resume later.') from None
                sleep(delay)
                continue
            failure = RejectedRequest if 400 <= error.code < 500 and error.code != 408 else ValueError
            raise failure(f'ElevenLabs HTTP {error.code}; stopped. Check account permissions/credits. Response omitted.') from None
        except (urllib.error.URLError, TimeoutError, OSError, http.client.HTTPException):
            raise ValueError('Network outcome uncertain; stopped to avoid a duplicate paid request. Check ElevenLabs history before retrying.') from None


def generate(folder, receipt, jobs, secret, fetch=request_audio):
    folder.mkdir(parents=True, exist_ok=True)
    write_receipt(folder, receipt)
    done = {e['file'] for e in receipt['files']}
    for e in jobs:
        if e['file'] in done:
            continue
        print('Generating ' + e['file'], flush=True)
        # Durable marker prevents an uncertain paid request being repeated after a crash.
        marker = folder / 'pending-request.json'
        atomic(marker, json.dumps(e).encode())
        try:
            data = fetch(e, secret)
        except RejectedRequest:
            marker.unlink()
            raise
        path = folder / e['file']
        atomic(path, data)
        validate_audio(path)
        receipt['files'].append(dict(e, sha256=digest(data)))
        write_receipt(folder, receipt)
        marker.unlink()


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--manifest', type=Path, default=MANIFEST)
    p.add_argument('--out', type=Path)
    p.add_argument('--dry-run', action='store_true')
    p.add_argument('--sample-only', action='store_true')
    p.add_argument('--include-player-steps', action='store_true')
    p.add_argument('--key', action='append', default=[])
    p.add_argument('--variant', action='append', type=int, default=[])
    p.add_argument('--yes', action='store_true', help='Explicitly accept paid generation without interactive prompt.')
    p.add_argument('--check', action='store_true', help='Run the importer --check after generation.')
    p.add_argument('--zip', type=Path, help='Optional new archive outside the repo and batch.')
    a = p.parse_args(argv)
    m, sha = load_manifest(a.manifest)
    jobs = entries(m, a.key, a.sample_only, a.include_player_steps, a.variant)
    folder = outside_repo(a.out or Path.home() / 'Documents/ChipArena/SoundRequests' / datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S-%f'))
    archive = outside_repo(a.zip) if a.zip else None
    if archive and (archive.exists() or archive == folder or folder in archive.parents):
        raise ValueError('ZIP must be new and outside the batch folder.')
    receipt = open_batch(folder, sha, entries(m, optional=True))
    done = {e['file'] for e in receipt['files']}
    pending = [e for e in jobs if e['file'] not in done]
    seconds = sum(e['durationSeconds'] for e in pending)
    print(f'Batch: {folder}\nSelected {len(jobs)}; completed {len(jobs)-len(pending)}; requests {len(pending)}; duration {seconds:g}s')
    print(f'Estimated {seconds * 40:g} credits at documented 40 credits/s (2026-10-16); account billing may differ. MP3 192 requires Creator or higher.')
    if a.dry_run:
        return 0
    if pending:
        if not shutil.which('ffmpeg'):
            raise ValueError('Install ffmpeg and add it to PATH before generation.')
        secret = os.environ.get('ELEVENLABS_API_KEY', '').strip()
        if not secret:
            raise ValueError('Set ELEVENLABS_API_KEY locally before generation.')
        if not a.yes and input('Generate this paid batch? Type GENERATE: ').strip() != 'GENERATE':
            print('Cancelled. No API calls made.')
            return 0
        generate(folder, receipt, jobs, secret)
    if a.check:
        subprocess.run([sys.executable, str(Path(__file__).with_name('import_sound_requests.py')),
                        '--manifest', str(a.manifest.resolve()), '--in', str(folder), '--check'], check=True)
    if archive:
        archive.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(archive, 'x', compression=zipfile.ZIP_DEFLATED) as z:
            for name in ['receipt.json'] + [e['file'] for e in receipt['files']]:
                z.write(folder / name, name)
        print('ZIP: ' + str(archive))
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (ValueError, KeyError, TypeError, OSError, subprocess.SubprocessError) as error:
        # Avoid printing HTTP bodies, request headers, or arbitrary exception strings.
        print('ERROR: ' + (str(error) if isinstance(error, ValueError) else type(error).__name__ + '; check local inputs/dependencies.'), file=sys.stderr)
        sys.exit(1)
