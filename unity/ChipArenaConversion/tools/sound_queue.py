"""Opt-in local ElevenLabs queue, invoked only by a real asset install (never --check).
The Git queue contains immutable selections; credentials and originals stay on the PC.
"""
import contextlib
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import zipfile

import generate_sound_requests as gen

QUEUE = gen.REPO / 'docs/audio/sound-queue.json'


def home():
    base = os.environ.get('LOCALAPPDATA')
    return Path(base) / 'ChipArena/SoundQueue' if base else None


def write(path, value):
    gen.atomic(path, (json.dumps(value, indent=2) + '\n').encode())


def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def protect(data, decrypt=False):
    # Windows DPAPI binds the encrypted secret to the logged-in builder user.
    if os.name != 'nt':
        raise ValueError('Credential setup requires Windows.')
    import ctypes
    from ctypes import wintypes
    class Blob(ctypes.Structure):
        _fields_ = [('size', wintypes.DWORD), ('data', ctypes.POINTER(ctypes.c_char))]
    buf = ctypes.create_string_buffer(data)
    source = Blob(len(data), ctypes.cast(buf, ctypes.POINTER(ctypes.c_char)))
    output = Blob()
    ctypes.windll.kernel32.LocalFree.argtypes = [ctypes.c_void_p]
    ctypes.windll.kernel32.LocalFree.restype = ctypes.c_void_p
    fn = ctypes.windll.crypt32.CryptUnprotectData if decrypt else ctypes.windll.crypt32.CryptProtectData
    if not fn(ctypes.byref(source), None, None, None, None, 1, ctypes.byref(output)):
        raise ValueError('Could not unlock the key; run setup as the Windows user running the builder.')
    try:
        return ctypes.string_at(output.data, output.size)
    finally:
        ctypes.windll.kernel32.LocalFree(ctypes.cast(output.data, ctypes.c_void_p))


@contextlib.contextmanager
def lock(root):
    f = (root / 'worker.lock').open('a+b')
    try:
        f.seek(0)
        if not f.read(1):
            f.write(b'0'); f.flush()
        f.seek(0)
        try:
            if os.name == 'nt':
                import msvcrt
                msvcrt.locking(f.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(f.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError:
            raise ValueError('Sound queue is already running on this PC.') from None
        yield
    finally:
        f.close()  # OS releases the lock even after a crash.


def limits(value):
    count, seconds = value.get('maxRequests'), value.get('maxSeconds')
    if (type(count) is not int or count < 1 or type(seconds) not in (int, float)
            or not math.isfinite(seconds) or seconds <= 0):
        raise ValueError('Request and duration limits must be positive finite numbers.')


def plan(queue, repo):
    value = json.loads(queue.read_text(encoding='utf-8'))
    if value.get('schema') != 'chip-arena.sound-queue/1' or not isinstance(value.get('jobs'), list):
        raise ValueError('Invalid sound queue.')
    result, seen = [], set()
    for row in value['jobs']:
        if row.get('approved') is not True:
            continue
        limits(row)
        identity = row.get('id', '')
        if not re.fullmatch('[a-z0-9][a-z0-9-]{0,63}', identity) or identity in seen:
            raise ValueError('Invalid/duplicate job id.')
        seen.add(identity)
        path = (repo / row['manifest']).resolve()
        if not path.is_relative_to((repo / 'docs/audio').resolve()) or path.suffix != '.json':
            raise ValueError('Manifest must be under docs/audio.')
        manifest, digest = gen.load_manifest(path)
        if digest != row['manifestSha256']:
            raise ValueError('Manifest changed after approval; create a newly reviewed job.')
        if not row.get('keys') or not row.get('variants'):
            raise ValueError('Queue jobs require explicit keys and variants.')
        jobs = gen.entries(manifest, keys=row['keys'], variants=row['variants'], optional=True)
        if len(jobs) > row['maxRequests'] or sum(j['durationSeconds'] for j in jobs) > row['maxSeconds']:
            raise ValueError('Job exceeds its approved request/duration cap.')
        result.append((row, path, manifest, digest, jobs))
    return result


def command(args):
    # The API key is never an argument or logged subprocess environment.
    env = {k: v for k, v in os.environ.items() if k != 'ELEVENLABS_API_KEY'}
    r = subprocess.run([sys.executable, '-I', *map(str, args)], capture_output=True, env=env)
    if r.returncode:
        raise ValueError('Audio import/asset check failed; originals retained. Run the documented importer check for details.')


def process(source, root=None, queue=QUEUE, repo=gen.REPO):
    root = root if root is not None else home()
    if root is None or not (root / 'config.json').is_file():
        return source
    config = json.loads((root / 'config.json').read_text())
    if config.get('enabled') is not True:
        return source
    gen.outside_repo(root)
    with lock(root):
        status = root / 'Latest-Sound-Status.json'
        def report(state, **fields):
            value = dict(state=state, updated=gen.utc(), **fields)
            mirrors = {status, source.parent / status.name}
            if config.get('statusFolder'):
                mirrors.add(Path(config['statusFolder']) / status.name)
            for destination in mirrors:
                try:
                    write(destination, value)
                except OSError:
                    print('Sound queue status write unavailable: ' + str(destination), flush=True)
        stage = 'validate limits'
        try:
            limits(config)
            stage = 'read queue and manifest'
            plans = plan(queue, repo)
            if not plans:
                report('idle', message='No approved sound jobs.'); return source
            if not source.is_file() or source.suffix.lower() != '.zip':
                raise ValueError('Automatic sound import requires a LocalAssets ZIP.')
            stage = 'validate saved originals and receipts'
            pending_count, pending_seconds, batches = 0, 0, []
            for row, path, manifest, digest, jobs in plans:
                folder = root / 'originals' / row['id']
                identity = root / (row['id'] + '.job.json')
                if identity.exists() and json.loads(identity.read_text()) != row:
                    raise ValueError('An existing job id changed; restore it or create a new reviewed id.')
                receipt = gen.open_batch(folder, digest, gen.entries(manifest, optional=True))
                done = {e['file'] for e in receipt['files']}
                pending = [j for j in jobs if j['file'] not in done]
                pending_count += len(pending); pending_seconds += sum(j['durationSeconds'] for j in pending)
                batches.append((row, path, digest, jobs, folder, identity, receipt))
            if pending_count > config['maxRequests'] or pending_seconds > config['maxSeconds']:
                raise ValueError('Pending queue exceeds local request/duration limits; no requests sent.')
            if pending_count and not shutil.which('ffmpeg'):
                raise ValueError('ffmpeg is required before any audio requests can be sent.')
            stage = 'check base LocalAssets ZIP'
            command([Path(__file__).with_name('install_local_assets.py'), source, '--check'])
            stage = 'read saved ElevenLabs credential'
            secret = protect((root / 'key.dpapi').read_bytes(), decrypt=True).decode() if pending_count else ''
            report('running', requests=pending_count, seconds=pending_seconds)
            current = source
            for row, path, digest, jobs, folder, identity, receipt in batches:
                write(identity, row)
                stage = 'generate or reuse approved originals'
                gen.generate(folder, receipt, jobs, secret)
                # Share only receipted originals and metadata, never the credential store.
                archive = source.parent / (row['id'] + '.zip')
                temporary = archive.with_suffix('.zip.tmp')
                try:
                    with zipfile.ZipFile(temporary, 'w', zipfile.ZIP_DEFLATED) as bundle:
                        bundle.write(folder / 'receipt.json', 'receipt.json')
                        for entry in receipt['files']:
                            bundle.write(folder / entry['file'], entry['file'])
                    os.replace(temporary, archive)
                except OSError:
                    print('Sound sample ZIP mirror unavailable; originals retained locally.', flush=True)
                # Base hash avoids dropping newer licensed assets or another job's imported sounds.
                stage = 'fingerprint import inputs'
                fingerprint = hashlib.sha256((sha(current) + sha(folder / 'receipt.json') + sha(path) +
                    sha(Path(__file__).with_name('import_sound_requests.py'))).encode()).hexdigest()
                output = root / ('assets-' + fingerprint + '.zip')
                proof = output.with_suffix('.receipt.json')
                if proof.exists():
                    if not output.is_file() or sha(output) != json.loads(proof.read_text())['sha256']:
                        raise ValueError('Cached imported assets changed; retained for inspection.')
                else:
                    # An interrupted import is local work, safe to redo; paid originals are untouched.
                    if output.exists():
                        output.unlink()
                    details = root / (row['id'] + '.import.json')
                    stage = 'import generated originals'
                    command([Path(__file__).with_name('import_sound_requests.py'), '--manifest', path,
                             '--in', folder, '--base', current, '--out', output, '--report', details])
                    imported = json.loads(details.read_text())
                    if imported.get('problems') or sum(k['accepted'] for k in imported['keys'].values()) != len(jobs):
                        raise ValueError('Importer rejected some sounds; inspect the local import report. No regeneration attempted.')
                    write(proof, dict(sha256=sha(output)))
                current = output
            report('complete', jobs=[r[0]['id'] for r in batches], generatedRequests=pending_count,
                   output=str(current), message='Imported assets ready; build/publication is a separate gate.')
            return current
        except Exception as e:
            # Never include arbitrary network exception text/headers or the secret.
            reason = str(e) if isinstance(e, ValueError) else type(e).__name__
            if isinstance(e, FileNotFoundError) and e.filename:
                reason += ': missing path ' + str(e.filename)
            reason = stage + ': ' + reason
            report('blocked', message=reason, stage=stage)
            raise ValueError('Sound queue blocked: ' + reason + '. Status: ' + str(status) +
                             '. No automatic uncertain-request retry.') from None
