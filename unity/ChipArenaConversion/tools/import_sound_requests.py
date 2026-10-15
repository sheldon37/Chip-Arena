"""Import a batch of requested sound files (docs/audio/sound-requests.json) into a new private LocalAssets bank zip.

Run from the repository root with Python 3.11+ (ffmpeg on PATH for an import; a --check needs none):

  py -3 unity/ChipArenaConversion/tools/import_sound_requests.py --manifest docs/audio/sound-requests.json --in "<batch folder or .zip>" --check
  py -3 unity/ChipArenaConversion/tools/import_sound_requests.py --manifest docs/audio/sound-requests.json --in "<batch folder or .zip>" [--in ...]
      --base "<current LocalAssets zip>" --out "<new LocalAssets zip>" --report import-report.json

The contract is docs/audio/SOUND-REQUESTS.md. Files are named <key>_<NN>.<mp3|wav> (NN 01..count); a batch may hold
receipt.json (schema chip-arena.sound-receipt/1, manifestSha256 = the sha256 of the manifest's bytes). Across several --in,
the newest file wins per name (modification time; a tie goes to the later --in).

--check validates the names, keys, variants and the receipt, and writes nothing. An import also, per file: decodes it, makes it
mono 44.1 kHz, trims leading and trailing silence below the manifest's trimSilenceDbfs (-50 dBFS), keeps the first sound event,
cuts it to targetSeconds.max with the manifest's fade (20 ms), rejects it when shorter than targetSeconds.min or near-silent,
matches its loudness to the base bank's clips of its category (the median momentary maximum of the base's step or voice clips,
else -24 / -18 LUFS), and encodes Ogg Vorbis q5 mono (as the bank's clips: no metadata, true peak at or under -1 dBTP) to
Resources/EffectsAudio/<key>/<n>.ogg with its .meta. A key whose whole count arrives replaces its clips; a partial set appends
after them (at most the bank's clip limit). Every other file of the base zip is copied unchanged. The new zip is then checked with
tools/install_local_assets.py --check. The report lists every file (accepted, trimmed or rejected with a reason) and each key's
counts. Only ffmpeg runs, on the batch files as data. Clips are written only into the output zip, never into the repository.
Exit status: 0 done, 1 on a hard error (bad names, unknown keys, extra variants, a bad receipt, a failed check).
"""
import argparse
import datetime
import hashlib
import json
import math
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import sys
import tempfile
import uuid
import zipfile
from array import array

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))
import install_local_assets as installer  # noqa: E402

REQUEST_SCHEMA = 'chip-arena.sound-requests/1'
RECEIPT_SCHEMA = 'chip-arena.sound-receipt/1'
REPORT_SCHEMA = 'chip-arena.sound-import-report/1'
NAME = re.compile(r'^(?P<key>[a-z0-9.-]+)_(?P<nn>\d{2})\.(?P<ext>mp3|wav)$')
AUDIO_EXT = ('.mp3', '.wav')
IGNORED = {'receipt.json', 'thumbs.db', '.ds_store', 'desktop.ini'}
RATE = 44100
CEILING_DB = -1.5
LIMITER_HEADROOM_DB = 6.0
FALLBACK_TARGET = {'step': -24.0, 'voice': -18.0}
# The first sound event ends after this much silence (s): a footfall's next footfall, a voice's next breath.
EVENT_GAP = {'step': .12, 'voice': .3}
NEAR_SILENT_DBFS = -40.0
MAX_INPUT_BYTES = 64 * 1024 * 1024


class Hard(Exception):
    pass


# ---------------------------------------------------------------- the request manifest and the batch
def load_manifest(path):
    raw = Path(path).read_bytes()
    try:
        data = json.loads(raw.decode('utf-8-sig'))  # (a Windows editor's byte order mark is allowed; the hash is of the raw bytes)
    except ValueError as error:
        raise Hard(f'manifest {path}: {error}')
    if data.get('schema') != REQUEST_SCHEMA:
        raise Hard(f'manifest schema must be {REQUEST_SCHEMA}')
    sounds = {}
    for s in data.get('sounds', []):
        key = s['key']
        if key in sounds:
            raise Hard(f'manifest: {key} listed twice')
        if s.get('category') not in FALLBACK_TARGET:
            raise Hard(f'manifest: {key} category must be step or voice')
        t = s['targetSeconds']
        if not (0 < float(t['min']) < float(t['max'])) or not (1 <= int(s['count']) <= 99):
            raise Hard(f'manifest: {key} targetSeconds or count out of range')
        sounds[key] = s
    edit = data.get('edit', {})
    return raw, data, sounds, float(edit.get('trimSilenceDbfs', -50)), float(edit.get('fadeOutMs', 20)) / 1000


def check_contract(sounds, problems):
    """Every requested key is in the bank contract, positional, with a cap of at least its targetSeconds.max."""
    keys, _, max_clips, _ = installer.contract()
    for key, s in sounds.items():
        if key not in keys:
            problems.append(f'{key}: not a key of the bank contract (Core/NativeSoundBank.cs)')
            continue
        space, cap = keys[key]
        if space != '3d' or cap + 1e-6 < float(s['targetSeconds']['max']):
            problems.append(f'{key}: the contract has {space} with a {cap} s cap; the request needs 3d up to {s["targetSeconds"]["max"]} s')
        if int(s['count']) > max_clips:
            problems.append(f'{key}: count {s["count"]} over the bank limit of {max_clips} clips')
    return max_clips


class Item:
    """One batch file: its name, where it came from, its bytes on demand and its time."""
    def __init__(self, name, origin, mtime, order, read):
        self.name, self.origin, self.mtime, self.order, self.read = name, origin, mtime, order, read


def safe_member(name):
    parts = PurePosixPath(name).parts
    return not ('\\' in name or name.startswith('/') or ':' in name or '..' in parts or not parts or parts[0] in ('__MACOSX', '.git'))


def scan_input(path, order, items, receipts, warnings, problems):
    """Collects the files of one --in (a folder, flat or nested, or a zip of one)."""
    path = Path(path)
    if path.is_dir():
        for f in sorted(path.rglob('*')):
            if f.is_symlink():
                problems.append(f'{f}: links are not allowed')
            elif f.is_file():
                add(f.name, str(f), f.stat().st_mtime, order, (lambda f=f: f.read_bytes()), items, receipts, warnings)
    elif path.is_file() and zipfile.is_zipfile(path):
        z = zipfile.ZipFile(path)
        for m in z.infolist():
            if m.is_dir():
                continue
            if not safe_member(m.filename) or m.file_size > MAX_INPUT_BYTES:
                problems.append(f'{path}: unsafe or oversized member {m.filename}')
                continue
            mtime = datetime.datetime(*m.date_time).timestamp()
            add(PurePosixPath(m.filename).name, f'{path}!{m.filename}', mtime, order, (lambda z=z, m=m: z.read(m)), items, receipts, warnings)
    else:
        problems.append(f'--in {path}: not a folder or a .zip')


def add(name, origin, mtime, order, read, items, receipts, warnings):
    lower = name.lower()
    if lower == 'receipt.json':
        receipts.append((origin, read))
        return
    if lower in IGNORED or name.startswith('.'):
        return
    if not lower.endswith(AUDIO_EXT):
        warnings.append(f'{origin}: not a requested sound file (ignored)')
        return
    items.append(Item(name, origin, mtime, order, read))


def newest(items):
    """The newest file per name (the later --in on a tie)."""
    best = {}
    for it in items:
        b = best.get(it.name)
        if b is None or (it.mtime, it.order) >= (b.mtime, b.order):
            best[it.name] = it
    return best


SECRET_FIELDS = {'apikey', 'api_key', 'xi-api-key', 'xi_api_key', 'token', 'accesstoken', 'access_token', 'accountid', 'account_id', 'authorization', 'secret', 'password'}


def field_names(value):
    """Every object key in a JSON value, lower-cased."""
    if isinstance(value, dict):
        return {str(k).lower() for k in value} | set().union(*(field_names(v) for v in value.values()))
    if isinstance(value, list):
        return set().union(*(field_names(v) for v in value)) if value else set()
    return set()


def check_receipts(receipts, manifest_raw, warnings, problems):
    if not receipts:
        warnings.append('no receipt.json in the batch (optional): the manifest version is not checked')
        return []
    digest = hashlib.sha256(manifest_raw).hexdigest()
    seen = []
    for origin, read in receipts:
        try:
            r = json.loads(read().decode('utf-8-sig'))
        except ValueError as error:
            problems.append(f'{origin}: not JSON ({error})')
            continue
        if r.get('schema') != RECEIPT_SCHEMA:
            problems.append(f'{origin}: schema must be {RECEIPT_SCHEMA}')
        if r.get('manifestSha256') != digest:
            problems.append(f'{origin}: manifestSha256 {r.get("manifestSha256")} is not this manifest\'s {digest} (generated from another version)')
        for field in ('generatedAt', 'generator'):
            if not isinstance(r.get(field), str) or not r.get(field):
                problems.append(f'{origin}: {field} missing')
        if not isinstance(r.get('files'), list):
            problems.append(f'{origin}: files[] missing')
        else:
            for f in r['files']:
                if not isinstance(f, dict) or not isinstance(f.get('file'), str):
                    problems.append(f'{origin}: a files[] entry without its file name')
                    continue
                m = NAME.match(f['file'])
                if not m or f.get('key') not in (None, m['key']) or (f.get('variant') is not None and f.get('variant') != int(m['nn'])):
                    problems.append(f'{origin}: files[] entry {f["file"]} does not match its key or variant')
                seen.append(f['file'])
        for name in sorted(field_names(r) & SECRET_FIELDS):
            problems.append(f'{origin}: the receipt must not hold a `{name}` field (no API key, account id or token is ever written)')
    return seen


def validate(args):
    """Names, keys, variants and the receipt. Returns the plan: (manifest, chosen files by key, warnings, problems)."""
    raw, manifest, sounds, trim_db, fade = load_manifest(args.manifest)
    problems, warnings, items, receipts = [], [], [], []
    max_clips = check_contract(sounds, problems)
    for order, path in enumerate(args.inputs):
        scan_input(path, order, items, receipts, warnings, problems)
    chosen = newest(items)
    for name, it in sorted(chosen.items()):
        if it is not None and sum(1 for x in items if x.name == name) > 1:
            warnings.append(f'{name}: several copies; the newest is used ({it.origin})')
    by_key = {}
    for name, it in sorted(chosen.items()):
        m = NAME.match(name)
        if not m:
            problems.append(f'{it.origin}: the name must be <key>_<NN>.mp3 or .wav (NN two digits)')
            continue
        key, nn = m['key'], int(m['nn'])
        if key not in sounds:
            problems.append(f'{it.origin}: {key} is not a requested key')
            continue
        if not 1 <= nn <= int(sounds[key]['count']):
            problems.append(f'{it.origin}: variant {nn:02d} is outside 01..{int(sounds[key]["count"]):02d}')
            continue
        if any(v == nn for v, _ in by_key.get(key, [])):
            problems.append(f'{it.origin}: variant {nn:02d} of {key} twice (an .mp3 and a .wav)')
            continue
        by_key.setdefault(key, []).append((nn, it))
    for key, s in sounds.items():
        have = {nn for nn, _ in by_key.get(key, [])}
        missing = [n for n in range(1, int(s['count']) + 1) if n not in have]
        if have and missing:
            warnings.append(f'{key}: {len(have)} of {s["count"]} variants (missing {", ".join(f"{n:02d}" for n in missing)}): a partial set appends')
    listed = check_receipts(receipts, raw, warnings, problems)
    for name in listed:
        if name not in chosen:
            warnings.append(f'receipt lists {name}, which is not in the batch')
    return raw, manifest, sounds, trim_db, fade, max_clips, by_key, warnings, problems


# ---------------------------------------------------------------- audio (ffmpeg only)
def ffmpeg():
    exe = shutil.which('ffmpeg')
    if not exe:
        raise Hard('ffmpeg is not on PATH (an import needs it; --check does not)')
    return exe


def run(cmd, data=None):
    return subprocess.run(cmd, input=data, capture_output=True, timeout=300)


def decode(exe, data, suffix):
    """Mono 44.1 kHz float samples of an encoded file (written to a temp file: ffmpeg probes mp3 better from a file)."""
    with tempfile.TemporaryDirectory(prefix='sound-import-') as tmp:
        src = Path(tmp) / ('in' + suffix)
        src.write_bytes(data)
        r = run([exe, '-nostdin', '-hide_banner', '-loglevel', 'error', '-i', str(src), '-map', '0:a:0', '-ac', '1', '-ar', str(RATE), '-f', 'f32le', '-'])
    if r.returncode != 0 or len(r.stdout) < 4:
        raise ValueError('cannot decode: ' + r.stderr.decode('utf-8', 'replace')[-200:].strip())
    pcm = array('f')
    pcm.frombytes(r.stdout[:len(r.stdout) // 4 * 4])
    if sys.byteorder != 'little':
        pcm.byteswap()
    return pcm


def windows(pcm, size):
    """Peak of each window of `size` samples."""
    out = []
    for i in range(0, len(pcm), size):
        s = pcm[i:i + size]
        out.append(max(max(s), -min(s)) if len(s) else 0.0)
    return out


def edit(pcm, request, category, trim_db, fade):
    """Trim, keep the first event, cap and fade. Returns (samples, notes, trimmed) or raises ValueError (rejected)."""
    lo, hi = float(request['targetSeconds']['min']), float(request['targetSeconds']['max'])
    seconds_in = len(pcm) / RATE
    thr = 10 ** (trim_db / 20)
    w = int(.005 * RATE)
    peaks = windows(pcm, w)
    loud = [i for i, p in enumerate(peaks) if p >= thr]
    peak = max(peaks) if peaks else 0.0
    if not loud or 20 * math.log10(peak + 1e-12) < NEAR_SILENT_DBFS:
        raise ValueError(f'near-silent (peak {20 * math.log10(peak + 1e-12):.1f} dBFS)')
    first = loud[0]
    # The first event: from the onset to the first run of EVENT_GAP seconds under the threshold.
    gap = max(1, int(EVENT_GAP[category] / .005))
    last, quiet = first, 0
    for i in range(first, len(peaks)):
        if peaks[i] >= thr:
            last, quiet = i, 0
        else:
            quiet += 1
            if quiet >= gap:
                break
    events_after = any(p >= thr for p in peaks[last + gap:]) if last + gap < len(peaks) else False
    start = max(0, first * w - int(.005 * RATE))
    end = min(len(pcm), (last + 1) * w + int(.03 * RATE))
    notes, trimmed = [], False
    if events_after:
        notes.append('kept the first sound event')
        trimmed = True
    if (end - start) / RATE > hi:
        end = start + int(hi * RATE)
        notes.append(f'cut to {hi} s')
        trimmed = True
    out = array('f', pcm[start:end])
    n = len(out)
    if n / RATE < lo:
        raise ValueError(f'{n / RATE:.3f} s after trimming, shorter than its {lo} s minimum')
    fin, fout = min(n, int(.002 * RATE)), min(n // 2, int(fade * RATE))
    for i in range(fin):
        out[i] *= i / fin
    for i in range(fout):
        out[n - 1 - i] *= i / fout
    notes.insert(0, f'{seconds_in:.3f} s in, {n / RATE:.3f} s kept from {start / RATE:.3f} s')
    return out, notes, trimmed


def measure(exe, path):
    """(momentary max LUFS, sample peak dBFS, true peak dBTP) of an audio file (as the bank's build tool measures)."""
    r = run([exe, '-nostdin', '-hide_banner', '-nostats', '-i', str(path), '-filter_complex', 'apad=pad_dur=0.5,ebur128=peak=sample+true:dualmono=true:framelog=info', '-f', 'null', '-'])
    err = r.stderr.decode('utf-8', 'replace')
    ms = [float(x) for x in re.findall(r' M:\s*(-?[\d.]+)', err)]
    summary = err[err.rfind('Summary:'):]

    def find(pattern):
        m = re.search(pattern, summary, re.S)
        return float(m.group(1)) if m and 'inf' not in m.group(1) else None
    return (max(ms) if ms else None, find(r'Sample peak:\s+Peak:\s+(-?[\d.inf]+) dBFS'), find(r'True peak:\s+Peak:\s+(-?[\d.inf]+) dBFS'))


def encode(exe, pcm, target, out):
    """Gain to `target` (momentary max) with a lookahead limiter past the ceiling, Ogg Vorbis q5 mono, true peak <= -1 dBTP."""
    with tempfile.TemporaryDirectory(prefix='sound-import-') as tmp:
        wav = Path(tmp) / 'cut.f32'
        data = array('f', pcm)
        if sys.byteorder != 'little':
            data.byteswap()
        wav.write_bytes(data.tobytes())
        raw = ['-f', 'f32le', '-ar', str(RATE), '-ac', '1', '-i', str(wav)]
        level = Path(tmp) / 'level.wav'
        run([exe, '-nostdin', '-y', '-hide_banner', '-loglevel', 'error', *raw, '-c:a', 'pcm_f32le', str(level)])
        m_max, peak, _ = measure(exe, level)
        wanted = (target - m_max) if m_max is not None else 0.0
        trim, final, gain, limited = 0.0, (None, None, None), 0.0, False
        for _ in range(4):
            ceiling = CEILING_DB - trim
            gain = wanted - trim
            limited = peak is not None and peak + gain > ceiling
            if limited:
                gain = min(gain, ceiling - peak + LIMITER_HEADROOM_DB)
            chain = f'volume={gain:.2f}dB' + (f',alimiter=limit={10 ** (ceiling / 20):.4f}:attack=1:release=60:level=false:latency=true' if limited else '')
            r = run([exe, '-nostdin', '-y', '-hide_banner', '-loglevel', 'error', *raw, '-af', chain, '-map_metadata', '-1', '-fflags', '+bitexact', '-flags:a', '+bitexact', '-c:a', 'libvorbis', '-q:a', '5', str(out)])
            if r.returncode != 0:
                raise ValueError('encode failed: ' + r.stderr.decode('utf-8', 'replace')[-200:].strip())
            final = measure(exe, out)
            if final[2] is None or final[2] <= -1.0:
                break
            trim += final[2] + 1.0 + .3
    info = installer.ogg_info(Path(out).read_bytes())
    if info is None:
        raise ValueError('the encoder wrote no Ogg Vorbis')
    return {'gainDb': round(gain, 2), 'limited': limited, 'momentaryMaxLufs': final[0], 'truePeakDbtp': final[2], 'seconds': round(info[0], 3), 'bytes': Path(out).stat().st_size}


# ---------------------------------------------------------------- the bank
def audio_meta(guid, seconds):
    load = 1 if seconds > 1.0 else 0
    return (f'fileFormatVersion: 2\nguid: {guid}\nAudioImporter:\n  externalObjects: {{}}\n  serializedVersion: 7\n  defaultSettings:\n    serializedVersion: 2\n'
            f'    loadType: {load}\n    sampleRateSetting: 0\n    sampleRateOverride: 44100\n    compressionFormat: 1\n    quality: 0.7\n'
            '    conversionMode: 0\n    preloadAudioData: 1\n  platformSettingOverrides: {}\n  forceToMono: 0\n  normalize: 0\n'
            '  loadInBackground: 1\n  ambisonic: 0\n  3D: 1\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')


FOLDER_META = 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'


def guid_of(meta):
    if meta.is_file():
        m = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(encoding='utf-8', errors='replace'), re.M)
        if m:
            return m.group(1)
    return uuid.uuid4().hex


def category_targets(exe, bank, sounds, manifest_events):
    """The loudness per category: the median momentary maximum of the base's clips of the requested keys of that category and
    their kin (the pilot steps; the old add-death grunt), else the bank's family default."""
    kin = {'step': [k for k in manifest_events if k.startswith('move.step.')], 'voice': ['pve.add.death']}
    for key, s in sounds.items():
        kin[s['category']].append(key)
    targets, measured = {}, {}
    for cat, keys in kin.items():
        values = []
        for key in sorted(set(keys)):
            for name in manifest_events.get(key, {}).get('clips', []):
                clip = bank / key / (name + '.ogg')
                if clip.is_file():
                    m = measure(exe, clip)[0]
                    if m is not None:
                        values.append(m)
        values.sort()
        measured[cat] = len(values)
        targets[cat] = round(values[len(values) // 2] if len(values) % 2 else (values[len(values) // 2 - 1] + values[len(values) // 2]) / 2, 1) if values else FALLBACK_TARGET[cat]
    return targets, measured


def write_zip(root, out):
    out = Path(out)
    out.parent.mkdir(parents=True, exist_ok=True)
    tmp = out.with_name(out.name + '.tmp')
    with zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED) as z:
        for path in sorted(root.rglob('*')):
            if path.is_file():
                z.write(path, path.relative_to(root).as_posix())
    os.replace(tmp, out)


def outside_repo(path):
    """Refuses an output zip inside this repository unless git ignores it (clips never go into the repository)."""
    repo = TOOLS.parents[2]
    target = Path(path).resolve()
    if not target.is_relative_to(repo) or not (repo / '.git').exists():
        return
    try:
        ignored = run(['git', '-C', str(repo), 'check-ignore', '-q', str(target)]).returncode == 0
    except OSError:
        ignored = False
    if not ignored:
        raise Hard(f'{path}: write the new zip outside the repository (clips never go into it)')


def import_batch(args, plan, report):
    raw, manifest, sounds, trim_db, fade, max_clips, by_key, warnings, problems = plan
    exe = ffmpeg()
    outside_repo(args.out)
    with tempfile.TemporaryDirectory(prefix='sound-import-base-') as scratch, tempfile.TemporaryDirectory(prefix='sound-import-clips-') as staging:
        scratch, staging = Path(scratch), Path(staging)
        try:
            installer.safe_extract(Path(args.base), scratch)
            content = installer.content_root(scratch)
        except installer.Problem as problem:
            raise Hard(f'--base {args.base}: {problem}')
        bank = content / 'Resources/EffectsAudio'
        mpath = bank / 'manifest.json'
        bank_manifest = json.loads(mpath.read_text(encoding='utf-8'))
        events = bank_manifest['events']
        targets, measured = category_targets(exe, bank, sounds, events)
        report['targets'] = {c: {'momentaryMaxLufs': t, 'measuredClips': measured[c]} for c, t in targets.items()}
        files, keys = [], {}
        for key in sorted(by_key):
            s, cat = sounds[key], sounds[key]['category']
            # Edit and encode every file of the key into staging first; the bank changes only after.
            encoded = []
            for nn, it in sorted(by_key[key]):
                row = {'file': it.name, 'source': it.origin, 'key': key, 'variant': nn, 'category': cat}
                files.append(row)
                try:
                    pcm = decode(exe, it.read(), Path(it.name).suffix)
                    cut, notes, trimmed = edit(pcm, s, cat, trim_db, fade)
                    tmp = staging / f'{key}_{nn:02d}.ogg'
                    stats = encode(exe, cut, targets[cat], tmp)
                    row.update(status='trimmed' if trimmed else 'accepted', notes=notes, **stats)
                    encoded.append((row, tmp, stats))
                except ValueError as error:
                    row.update(status='rejected', reason=str(error))
            entry = events.get(key)
            old = list(entry.get('clips', [])) if entry else []
            mode = 'replace' if len(encoded) == int(s['count']) else 'append' if encoded else 'none'
            if mode == 'append' and len(old) + len(encoded) > max_clips:
                for row, _, _ in encoded[max(0, max_clips - len(old)):]:
                    row.update(status='rejected', reason=f'{key} would hold more than {max_clips} clips (a partial set appends)')
                encoded = encoded[:max(0, max_clips - len(old))]
                mode = 'append' if encoded else 'none'
            if mode == 'replace':
                names = [str(i) for i in range(len(encoded))]
            elif mode == 'append':
                taken, names, n = set(old), [], 1 + max([int(x) for x in old if x.isdigit()] or [-1])
                while len(names) < len(encoded):
                    if str(n) not in taken:
                        names.append(str(n))
                    n += 1
            else:
                names = []
            folder = bank / key
            if names:
                folder.mkdir(parents=True, exist_ok=True)
                fmeta = bank / (key + '.meta')
                if not fmeta.is_file():
                    fmeta.write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n' + FOLDER_META)
            if mode == 'replace':
                for name in old:
                    if name not in names:
                        for f in (folder / (name + '.ogg'), folder / (name + '.ogg.meta')):
                            f.unlink(missing_ok=True)
            for (row, tmp, stats), name in zip(encoded, names):
                target = folder / (name + '.ogg')
                meta = target.with_name(target.name + '.meta')
                guid = guid_of(meta)
                shutil.copyfile(tmp, target)
                meta.write_text(audio_meta(guid, stats['seconds']))
                row['clip'] = f'{key}/{name}.ogg'
            if names:
                new_entry = dict(entry) if entry else {}
                new_entry['clips'] = names if mode == 'replace' else old + names
                if not entry and cat == 'step':
                    new_entry['jitter'] = .06
                events[key] = new_entry
            keys[key] = {'requested': int(s['count']), 'arrived': len(by_key[key]), 'accepted': len(names),
                         'rejected': sum(1 for r in files if r['key'] == key and r['status'] == 'rejected'), 'mode': mode,
                         'clips': events.get(key, {}).get('clips', [])}
        bank_manifest['events'] = dict(sorted(events.items()))
        mpath.write_text(json.dumps(bank_manifest, indent=1) + '\n', encoding='utf-8')
        report['files'], report['keys'] = files, keys
        write_zip(scratch, args.out)
    check = run([sys.executable, '-I', str(TOOLS / 'install_local_assets.py'), str(Path(args.out).resolve()), '--check'])
    report['installCheck'] = {'returncode': check.returncode, 'output': (check.stdout + check.stderr).decode('utf-8', 'replace').strip()}
    if check.returncode != 0:
        problems.append('the new zip fails tools/install_local_assets.py --check:\n' + report['installCheck']['output'])


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    parser.add_argument('--manifest', required=True, help='docs/audio/sound-requests.json')
    parser.add_argument('--in', dest='inputs', action='append', required=True, help='a batch folder or .zip (repeatable; the newest file wins per name)')
    parser.add_argument('--check', action='store_true', help='validate only; write nothing')
    parser.add_argument('--base', help='the current LocalAssets zip')
    parser.add_argument('--out', help='the new LocalAssets zip')
    parser.add_argument('--report', help='the import report (JSON)')
    args = parser.parse_args(argv)
    if not args.check and not (args.base and args.out):
        parser.error('an import needs --base and --out (or use --check)')
    try:
        plan = validate(args)
    except Hard as error:
        print('Sound import refused: ' + str(error), file=sys.stderr)
        return 1
    raw, manifest, sounds, trim_db, fade, max_clips, by_key, warnings, problems = plan
    report = {'schema': REPORT_SCHEMA, 'manifest': str(args.manifest), 'manifestSha256': hashlib.sha256(raw).hexdigest(),
              'inputs': [str(p) for p in args.inputs], 'check': bool(args.check),
              'found': {k: sorted(nn for nn, _ in v) for k, v in sorted(by_key.items())}}
    if not problems and not args.check:
        report.update(base=str(args.base), out=str(args.out))
        try:
            import_batch(args, plan, report)
        except Hard as error:
            problems.append(str(error))
    report['warnings'], report['problems'] = warnings, problems
    if args.report and not args.check:
        Path(args.report).write_text(json.dumps(report, indent=1) + '\n', encoding='utf-8')
    for w in warnings:
        print('warning: ' + w)
    if problems:
        print('Sound import refused:\n' + '\n'.join(problems), file=sys.stderr)
        return 1
    found = sum(len(v) for v in by_key.values())
    if args.check:
        print(f'Checked {found} files for {len(by_key)} keys: names, variants and receipt valid; nothing written.')
    else:
        done = sum(k['accepted'] for k in report['keys'].values())
        rejected = sum(1 for f in report['files'] if f['status'] == 'rejected')
        print(f'Imported {done} of {found} files into {args.out} ({rejected} rejected); install check passed.' + (f' Report: {args.report}' if args.report else ''))
    return 0


if __name__ == '__main__':
    sys.exit(main())
