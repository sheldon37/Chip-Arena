"""The sound request importer (tools/import_sound_requests.py, docs/audio/SOUND-REQUESTS.md): check mode, trimming, caps, the
manifest update and an output zip that passes tools/install_local_assets.py --check. The import half needs ffmpeg."""
from pathlib import Path
import hashlib
import json
import math
import os
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import time
import unittest
import wave
import zipfile

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]
IMPORTER = ROOT / 'tools/import_sound_requests.py'
sys.path.insert(0, str(ROOT / 'tools'))
import install_local_assets as installer  # noqa: E402

FFMPEG = shutil.which('ffmpeg')
RATE = 44100


def tone(path, parts, rate=RATE, channels=1, freq=220.0):
    """A 16-bit WAV of (seconds, amplitude) parts: sine bursts (5 ms ramps) and silences."""
    frames = bytearray()
    for seconds, amp in parts:
        n = int(seconds * rate)
        ramp = max(1, int(.005 * rate))
        for i in range(n):
            env = min(1.0, i / ramp, (n - 1 - i) / ramp) if amp else 0.0
            v = int(32767 * amp * env * math.sin(2 * math.pi * freq * i / rate))
            frames += struct.pack('<h', v) * channels
    with wave.open(str(path), 'wb') as w:
        w.setnchannels(channels)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(bytes(frames))


def entry(name):
    m = re.match(r'^(.+)_(\d{2})\.(mp3|wav)$', name)
    return {'file': name, 'key': m[1], 'variant': int(m[2])} if m else {'file': name}


def run(args, cwd=REPO):
    r = subprocess.run([sys.executable, '-I', str(IMPORTER), *args], cwd=cwd, capture_output=True, text=True, timeout=600)
    return r.returncode, r.stdout + r.stderr


REQUESTS = {
    'schema': 'chip-arena.sound-requests/1', 'file': {'pattern': '<key>_<NN>.<ext>'},
    'edit': {'sampleRate': 44100, 'channels': 1, 'codec': 'vorbis', 'trimSilenceDbfs': -50, 'fadeOutMs': 20},
    'sounds': [
        {'key': 'pve.zombie.groan', 'category': 'voice', 'priority': 1, 'count': 2, 'generateSeconds': 1.5, 'targetSeconds': {'min': .5, 'max': 1.1}, 'prompt': 'groan'},
        {'key': 'pve.zombie.hurt', 'category': 'voice', 'priority': 1, 'count': 3, 'generateSeconds': .8, 'targetSeconds': {'min': .2, 'max': .6}, 'prompt': 'hurt'},
        {'key': 'move.step.concrete', 'category': 'step', 'priority': 3, 'count': 2, 'generateSeconds': .6, 'targetSeconds': {'min': .12, 'max': .33}, 'prompt': 'step'},
        {'key': 'pve.zombie.step.run', 'category': 'step', 'priority': 2, 'count': 2, 'generateSeconds': .6, 'targetSeconds': {'min': .12, 'max': .3}, 'prompt': 'run'},
    ]}


class SoundRequestImport(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp(prefix='sound-import-test-'))
        self.manifest = self.tmp / 'sound-requests.json'
        self.manifest.write_text(json.dumps(REQUESTS, indent=1) + '\n')
        self.sha = hashlib.sha256(self.manifest.read_bytes()).hexdigest()

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def receipt(self, folder, sha=None, files=()):
        (folder / 'receipt.json').write_text(json.dumps({'schema': 'chip-arena.sound-receipt/1', 'manifestSha256': sha or self.sha, 'generatedAt': '2026-10-15T23:00:00Z',
                                                          'generator': 'test/1', 'files': [entry(f) for f in files]}))

    def batch(self, name, files, receipt=True):
        folder = self.tmp / name
        folder.mkdir()
        for file, parts in files.items():
            tone(folder / file, parts)
        if receipt:
            self.receipt(folder, files=files)
        return folder

    # ---------------------------------------------------------------- check mode (no ffmpeg needed)
    def test_check_accepts_a_valid_batch_and_writes_nothing(self):
        folder = self.batch('ok', {'pve.zombie.groan_01.wav': [(.8, .5)], 'pve.zombie.hurt_02.wav': [(.3, .5)]})
        before = sorted(p.name for p in self.tmp.rglob('*'))
        code, out = run(['--manifest', str(self.manifest), '--in', str(folder), '--check'])
        self.assertEqual(code, 0, out)
        self.assertIn('Checked 2 files for 2 keys', out)
        self.assertIn('pve.zombie.hurt: 1 of 3 variants', out)
        self.assertEqual(before, sorted(p.name for p in self.tmp.rglob('*')))

    def test_check_refuses_bad_names_unknown_keys_and_extra_variants(self):
        folder = self.batch('bad', {'pve.zombie.groan_1.wav': [(.6, .5)], 'pve.zombie.yell_01.wav': [(.6, .5)], 'pve.zombie.groan_03.wav': [(.6, .5)],
                                    'move.step.concrete_00.wav': [(.2, .5)]})
        (folder / 'notes.txt').write_text('x')
        code, out = run(['--manifest', str(self.manifest), '--in', str(folder), '--check'])
        self.assertEqual(code, 1, out)
        self.assertIn('pve.zombie.groan_1.wav: the name must be', out)
        self.assertIn('pve.zombie.yell is not a requested key', out)
        self.assertIn('variant 03 is outside 01..02', out)
        self.assertIn('variant 00 is outside 01..02', out)
        self.assertIn('notes.txt: not a requested sound file (ignored)', out)

    def test_check_receipt_is_optional_but_must_match(self):
        folder = self.batch('noreceipt', {'pve.zombie.groan_01.wav': [(.8, .5)]}, receipt=False)
        code, out = run(['--manifest', str(self.manifest), '--in', str(folder), '--check'])
        self.assertEqual(code, 0, out)
        self.assertIn('no receipt.json', out)
        self.receipt(folder, sha='0' * 64)
        code, out = run(['--manifest', str(self.manifest), '--in', str(folder), '--check'])
        self.assertEqual(code, 1, out)
        self.assertIn('is not this manifest', out)
        receipt = json.loads((folder / 'receipt.json').read_text())
        receipt.update(manifestSha256=self.sha, schema='other/1', apiKey='never')
        (folder / 'receipt.json').write_text(json.dumps(receipt))
        code, out = run(['--manifest', str(self.manifest), '--in', str(folder), '--check'])
        self.assertEqual(code, 1, out)
        self.assertIn('schema must be chip-arena.sound-receipt/1', out)
        self.assertIn('must not hold a `apikey` field', out)

    def test_check_reads_a_byte_order_mark(self):
        folder = self.batch('bom', {'pve.zombie.groan_01.wav': [(.8, .5)]})
        self.manifest.write_bytes(b'\xef\xbb\xbf' + self.manifest.read_bytes())
        self.receipt(folder, sha=hashlib.sha256(self.manifest.read_bytes()).hexdigest(), files=['pve.zombie.groan_01.wav'])
        (folder / 'receipt.json').write_bytes(b'\xef\xbb\xbf' + (folder / 'receipt.json').read_bytes())
        code, out = run(['--manifest', str(self.manifest), '--in', str(folder), '--check'])
        self.assertEqual(code, 0, out)

    def test_check_reads_a_zip_and_takes_the_newest_file_per_name(self):
        old = self.batch('old', {'pve.zombie.groan_01.wav': [(.8, .5)]})
        new = self.batch('new', {'pve.zombie.groan_01.wav': [(.9, .5)], 'pve.zombie.groan_02.wav': [(.9, .5)]})
        os.utime(old / 'pve.zombie.groan_01.wav', (time.time() - 3600, time.time() - 3600))
        archive = self.tmp / 'new.zip'
        with zipfile.ZipFile(archive, 'w') as z:
            for f in new.iterdir():
                z.write(f, 'batch-1/' + f.name)
        code, out = run(['--manifest', str(self.manifest), '--in', str(archive), '--in', str(old), '--check'])
        self.assertEqual(code, 0, out)
        self.assertIn('pve.zombie.groan_01.wav: several copies; the newest is used (' + str(archive) + '!batch-1/pve.zombie.groan_01.wav)', out)

    def test_the_repository_request_manifest_fits_the_bank_contract(self):
        path = REPO / 'docs/audio/sound-requests.json'
        if not path.is_file():
            self.skipTest('docs/audio is not in this tree')
        empty = self.tmp / 'empty'
        empty.mkdir()
        code, out = run(['--manifest', str(path), '--in', str(empty), '--check'])
        self.assertEqual(code, 0, out)
        keys = installer.contract()[0]
        for s in json.loads(path.read_text())['sounds']:
            self.assertGreaterEqual(keys[s['key']][1] + 1e-6, s['targetSeconds']['max'], s['key'])

    # ---------------------------------------------------------------- import (ffmpeg)
    def ogg(self, path, seconds, volume_db, channels=1, freq=300):
        path.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run([FFMPEG, '-nostdin', '-y', '-loglevel', 'error', '-f', 'lavfi', '-i', f'sine=frequency={freq}:duration={seconds}', '-af', f'volume={volume_db}dB',
                        '-ac', str(channels), '-ar', '44100', '-c:a', 'libvorbis', '-q:a', '5', str(path)], check=True)

    def base_zip(self):
        """A small private bank: steps, an existing hurt, the old add-death grunt and a 2D click, with metas and other files."""
        base = self.tmp / 'base/LocalAssets'
        bank = base / 'Resources/EffectsAudio'
        guid = iter(range(1, 999))

        def meta(path, folder=False):
            Path(str(path) + '.meta').write_text(f'fileFormatVersion: 2\nguid: {next(guid):032x}\n' + ('folderAsset: yes\nDefaultImporter:\n' if folder else 'AudioImporter:\n'))
        bank.mkdir(parents=True)
        meta(base / 'Resources', True)
        meta(bank, True)
        events = {'move.step.concrete': {'clips': ['0', '1'], 'jitter': .06, 'cooldown': .09}, 'pve.zombie.hurt': {'clips': ['0'], 'gain': 1.1},
                  'pve.add.death': {'clips': ['0']}, 'ui.click': {'clips': ['0']}}
        for key, entry in events.items():
            meta(bank / key, True)
            for name in entry['clips']:
                clip = bank / key / (name + '.ogg')
                self.ogg(clip, .2 if 'step' in key or key == 'ui.click' else .5, -24 if 'step' in key else -14, 2 if key == 'ui.click' else 1)
                meta(clip)
        (bank / 'manifest.json').write_text(json.dumps({'schema': 1, 'events': events}, indent=1))
        meta(bank / 'manifest.json')
        (base / 'LICENSES.md').write_text('private\n')
        (base / 'tools').mkdir()
        (base / 'tools/build.py').write_text('raise SystemExit("never run")\n')
        archive = self.tmp / 'LocalAssets-base.zip'
        with zipfile.ZipFile(archive, 'w') as z:
            for p in sorted((self.tmp / 'base').rglob('*')):
                if p.is_file():
                    z.write(p, p.relative_to(self.tmp / 'base').as_posix())
        check = subprocess.run([sys.executable, '-I', str(ROOT / 'tools/install_local_assets.py'), str(archive), '--check'], capture_output=True, text=True)
        self.assertEqual(check.returncode, 0, check.stdout + check.stderr)
        return archive

    def momentary(self, data):
        p = self.tmp / 'probe.ogg'
        p.write_bytes(data)
        r = subprocess.run([FFMPEG, '-nostdin', '-hide_banner', '-nostats', '-i', str(p), '-filter_complex', 'apad=pad_dur=0.5,ebur128=dualmono=true:framelog=info', '-f', 'null', '-'], capture_output=True, text=True)
        return max(float(x) for x in re.findall(r' M:\s*(-?[\d.]+)', r.stderr))

    @unittest.skipUnless(FFMPEG, 'ffmpeg is not on PATH')
    def test_import_trims_caps_matches_loudness_and_merges_the_manifest(self):
        base = self.base_zip()
        folder = self.batch('batch', {
            'pve.zombie.groan_01.wav': [(.2, 0), (.8, .5), (.3, 0)],               # silence trimmed only
            'pve.zombie.groan_02.wav': [(.6, .4), (.5, 0), (.4, .4)],              # the first event kept
            'pve.zombie.hurt_02.wav': [(1.5, .3)],                                 # cut to .6 s (a partial set appends)
            'move.step.concrete_01.wav': [(.1, 0), (.2, .6), (.2, 0)],
            'move.step.concrete_02.wav': [(.05, .6)],                              # shorter than .12 s: rejected
            'pve.zombie.step.run_01.wav': [(.3, .001)],                            # near-silent: rejected
        })
        out, report = self.tmp / 'out/LocalAssets-new.zip', self.tmp / 'import-report.json'
        code, text = run(['--manifest', str(self.manifest), '--in', str(folder), '--base', str(base), '--out', str(out), '--report', str(report)])
        self.assertEqual(code, 0, text)
        self.assertIn('install check passed', text)
        r = json.loads(report.read_text())
        self.assertEqual(r['installCheck']['returncode'], 0, r['installCheck'])
        status = {f['file']: f for f in r['files']}
        self.assertEqual(status['pve.zombie.groan_01.wav']['status'], 'accepted')
        self.assertEqual(status['pve.zombie.groan_02.wav']['status'], 'trimmed')
        self.assertEqual(status['pve.zombie.hurt_02.wav']['status'], 'trimmed')
        self.assertIn('shorter than its 0.12 s minimum', status['move.step.concrete_02.wav']['reason'])
        self.assertIn('near-silent', status['pve.zombie.step.run_01.wav']['reason'])
        self.assertEqual(r['keys']['pve.zombie.groan']['mode'], 'replace')
        self.assertEqual(r['keys']['pve.zombie.hurt']['mode'], 'append')
        self.assertEqual(r['keys']['move.step.concrete']['mode'], 'append')
        self.assertEqual(r['keys']['pve.zombie.step.run']['mode'], 'none')
        self.assertEqual(r['targets']['step']['momentaryMaxLufs'], round(self.momentary_of(base, 'move.step.concrete/0.ogg', 'move.step.concrete/1.ogg'), 1))
        with zipfile.ZipFile(out) as z, zipfile.ZipFile(base) as b:
            names = set(z.namelist())
            manifest = json.loads(z.read('LocalAssets/Resources/EffectsAudio/manifest.json'))['events']
            self.assertEqual(manifest['pve.zombie.groan'], {'clips': ['0', '1']})
            self.assertEqual(manifest['pve.zombie.hurt'], {'clips': ['0', '1'], 'gain': 1.1})
            self.assertEqual(manifest['move.step.concrete'], {'clips': ['0', '1', '2'], 'jitter': .06, 'cooldown': .09})
            self.assertNotIn('pve.zombie.step.run', manifest)
            for keep in ('LocalAssets/LICENSES.md', 'LocalAssets/tools/build.py', 'LocalAssets/Resources/EffectsAudio/ui.click/0.ogg', 'LocalAssets/Resources/EffectsAudio/move.step.concrete/0.ogg'):
                self.assertEqual(z.read(keep), b.read(keep), keep)
            for clip in ('pve.zombie.groan/0.ogg', 'pve.zombie.groan/1.ogg', 'pve.zombie.hurt/1.ogg', 'move.step.concrete/2.ogg'):
                self.assertIn(f'LocalAssets/Resources/EffectsAudio/{clip}.meta', names)
            self.assertIn('LocalAssets/Resources/EffectsAudio/pve.zombie.groan.meta', names)
            lengths = {c: installer.ogg_info(z.read('LocalAssets/Resources/EffectsAudio/' + c)) for c in ('pve.zombie.groan/0.ogg', 'pve.zombie.groan/1.ogg', 'pve.zombie.hurt/1.ogg', 'move.step.concrete/2.ogg')}
            for c, (seconds, channels, rate) in lengths.items():
                self.assertEqual((channels, rate), (1, 44100), c)
            self.assertAlmostEqual(lengths['pve.zombie.groan/0.ogg'][0], .8 + .035, delta=.03)
            self.assertAlmostEqual(lengths['pve.zombie.groan/1.ogg'][0], .6 + .035, delta=.03)
            self.assertAlmostEqual(lengths['pve.zombie.hurt/1.ogg'][0], .6, delta=.01)
            self.assertLessEqual(lengths['move.step.concrete/2.ogg'][0], .33 + .02)
            self.assertGreaterEqual(lengths['move.step.concrete/2.ogg'][0], .12)
            step = self.momentary(z.read('LocalAssets/Resources/EffectsAudio/move.step.concrete/2.ogg'))
            self.assertAlmostEqual(step, r['targets']['step']['momentaryMaxLufs'], delta=1.5)
            groan = self.momentary(z.read('LocalAssets/Resources/EffectsAudio/pve.zombie.groan/0.ogg'))
            self.assertAlmostEqual(groan, r['targets']['voice']['momentaryMaxLufs'], delta=1.5)
        check = subprocess.run([sys.executable, '-I', str(ROOT / 'tools/install_local_assets.py'), str(out), '--check'], capture_output=True, text=True)
        self.assertEqual(check.returncode, 0, check.stdout + check.stderr)

    def momentary_of(self, base, *clips):
        with zipfile.ZipFile(base) as z:
            values = sorted(self.momentary(z.read('LocalAssets/Resources/EffectsAudio/' + c)) for c in clips)
        return (values[0] + values[1]) / 2 if len(values) == 2 else values[len(values) // 2]

    @unittest.skipUnless(FFMPEG, 'ffmpeg is not on PATH')
    def test_a_full_set_replaces_and_the_zip_never_lands_in_the_repository(self):
        base = self.base_zip()
        folder = self.batch('full', {'pve.zombie.hurt_01.wav': [(.3, .5)], 'pve.zombie.hurt_02.wav': [(.4, .5)], 'pve.zombie.hurt_03.wav': [(.35, .5)]})
        out = self.tmp / 'full.zip'
        code, text = run(['--manifest', str(self.manifest), '--in', str(folder), '--base', str(base), '--out', str(out)])
        self.assertEqual(code, 0, text)
        with zipfile.ZipFile(out) as z:
            self.assertEqual(json.loads(z.read('LocalAssets/Resources/EffectsAudio/manifest.json'))['events']['pve.zombie.hurt'], {'clips': ['0', '1', '2'], 'gain': 1.1})
        inside = ROOT / 'LocalAssets-test-never.zip'
        code, text = run(['--manifest', str(self.manifest), '--in', str(folder), '--base', str(base), '--out', str(inside)])
        self.assertEqual(code, 1, text)
        self.assertIn('outside the repository', text)
        self.assertFalse(inside.exists())


if __name__ == '__main__':
    unittest.main()
