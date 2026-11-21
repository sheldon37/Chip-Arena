"""Intros v2's sound manifests (docs/audio/intros/README.md): batch 1's manifest stays as its approved builder jobs pin it, batch 2
has its own manifest (tools/intros/compile_sound_requests.py --batch b2), and the queue, the importer and the installer's contract take
a second intro manifest. No network, no key, no ffmpeg."""
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
import install_local_assets as installer  # noqa: E402
import import_sound_requests as importer  # noqa: E402
import sound_queue as queue  # noqa: E402

AUDIO = REPO / 'docs/audio'
BUNDLE = ROOT / 'tools/sound_bundle/docs/audio'
COMPILER = REPO / 'tools/intros/compile_sound_requests.py'  # (not in CI's sparse checkout: those tests skip there)
KEYS = ROOT / 'Assets/ChipArena/Core/NativeIntroSoundKeys.cs'
B1, B2 = 'sound-requests-intros.json', 'sound-requests-intros-b2.json'
# Batch 1 (the dev intro and the five generic intros, Slipstream's keys included) as shipped: its approved jobs pin this hash.
B1_SHA = '68c49c1246258f5412109802add0f07a344253716526ff7367e912059d5e9784'


def lf_sha(path):
    return hashlib.sha256(Path(path).read_bytes().replace(b'\r\n', b'\n')).hexdigest()


def approved():
    return [j for j in json.loads((AUDIO / 'sound-queue.json').read_text(encoding='utf-8'))['jobs'] if j.get('approved') is True]


class IntroSoundManifests(unittest.TestCase):
    def test_batch_one_stays_as_its_jobs_pin_it(self):
        self.assertEqual(lf_sha(AUDIO / B1), B1_SHA, 'docs/audio/' + B1 + ' changed: its approved jobs pin it (put new rows in batch 2)')
        self.assertEqual((AUDIO / B1).read_bytes(), (BUNDLE / B1).read_bytes(), 'the bundle copy of ' + B1 + ' is byte-identical')
        jobs = [j for j in approved() if j['manifest'] == 'docs/audio/' + B1]
        self.assertTrue(jobs, 'approved jobs pin batch 1')
        self.assertEqual({j['manifestSha256'] for j in jobs}, {B1_SHA})

    def test_every_approved_job_matches_its_manifest_and_bundle_copy(self):
        # (sound_queue.plan re-checks these on every builder run, finished jobs too; a mismatch stops the builder at the queue)
        for job in approved():
            for base in (REPO, ROOT / 'tools/sound_bundle'):
                path = base / job['manifest']
                if base != REPO and not path.is_file():
                    continue
                self.assertEqual(lf_sha(path), job['manifestSha256'], job['id'] + ': ' + str(path))

    def test_batch_two_manifest_when_present(self):
        path = AUDIO / B2
        if not path.is_file():
            self.assertFalse((BUNDLE / B2).exists(), 'a bundle copy without its manifest')
            return
        self.assertEqual(path.read_bytes(), (BUNDLE / B2).read_bytes(), 'the bundle copy of ' + B2 + ' is byte-identical')
        _, _, sounds, _, _ = importer.load_manifest(path)
        _, _, first, _, _ = importer.load_manifest(AUDIO / B1)
        self.assertFalse(set(sounds) & set(first), 'a key requested by both batches')
        keys = installer.contract()[0]
        for key, s in sounds.items():
            self.assertTrue(key.startswith('intro2.'), key)
            self.assertIn(key, keys, key + ': not in Core/NativeIntroSoundKeys.cs')
            self.assertEqual(keys[key][0], '2d', key)
            self.assertGreaterEqual(keys[key][1] + 1e-6, float(s['targetSeconds']['max']), key)

    def test_installer_contract_reads_every_intro_key(self):
        listed = {m[1]: float(m[2]) for m in re.finditer(r'K\("(intro2\.[a-z0-9.-]+)",([0-9.]+)f', KEYS.read_text(encoding='utf-8'))}
        keys = installer.contract()[0]
        self.assertTrue(listed)
        for key, cap in listed.items():
            self.assertEqual(keys.get(key), ('2d', cap), key)

    def test_queue_and_importer_take_a_second_intro_manifest(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp) / 'repo'
            (repo / 'docs/audio').mkdir(parents=True)
            shutil.copy(AUDIO / B1, repo / 'docs/audio' / B1)
            second = json.loads((AUDIO / B1).read_text(encoding='utf-8'))
            first_key = second['sounds'][0]['key']
            second['sounds'] = [dict(second['sounds'][0], key='intro2.zztest.cue', count=2)]
            (repo / 'docs/audio' / B2).write_text(json.dumps(second, indent=2) + '\n', encoding='utf-8')
            jobs = [dict(id='b1-test', approved=True, manifest='docs/audio/' + B1, manifestSha256=B1_SHA, keys=[first_key], variants=[1],
                         maxRequests=1, maxSeconds=30),
                    dict(id='b2-test', approved=True, manifest='docs/audio/' + B2, manifestSha256=lf_sha(repo / 'docs/audio' / B2),
                         keys=['intro2.zztest.cue'], variants=[1, 2], maxRequests=2, maxSeconds=30)]
            q = repo / 'docs/audio/sound-queue.json'
            q.write_text(json.dumps(dict(schema='chip-arena.sound-queue/1', jobs=jobs)), encoding='utf-8')
            plans = queue.plan(q, repo)
            self.assertEqual([p[0]['id'] for p in plans], ['b1-test', 'b2-test'])
            self.assertEqual([j['file'] for j in plans[1][4]], ['intro2.zztest.cue_01.mp3', 'intro2.zztest.cue_02.mp3'])
            # the importer reads the second manifest and checks its keys against the contract file the game reads
            _, _, sounds, _, _ = importer.load_manifest(repo / 'docs/audio' / B2)
            keys = Path(tmp) / 'NativeIntroSoundKeys.cs'
            keys.write_text(KEYS.read_text(encoding='utf-8'), encoding='utf-8')
            with patch.object(installer, 'INTRO_SOURCE', keys):
                problems = []
                importer.check_contract(sounds, problems)
                self.assertEqual(len(problems), 1, problems)  # (not yet in the contract file)
                keys.write_text(KEYS.read_text(encoding='utf-8').replace('// ---- I3 (melee) ----', '// ---- I3 (melee) ----\n            K("intro2.zztest.cue",3f),'), encoding='utf-8')
                problems = []
                importer.check_contract(sounds, problems)
                self.assertEqual(problems, [])
            # a changed batch-1 manifest stops the queue before any request
            (repo / 'docs/audio' / B1).write_text((AUDIO / B1).read_text(encoding='utf-8') + ' ', encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'Manifest changed after approval'):
                queue.plan(q, repo)


@unittest.skipUnless(COMPILER.is_file(), 'tools/intros is outside this checkout')
class IntroSoundCompiler(unittest.TestCase):
    def run_compiler(self, *args):
        r = subprocess.run([sys.executable, '-I', str(COMPILER), *args], capture_output=True, text=True, encoding='utf-8', timeout=120)
        return r.returncode, r.stdout + r.stderr

    def test_both_batches_are_current(self):
        for batch in ('b1', 'b2'):
            code, out = self.run_compiler('--batch', batch, '--check')
            self.assertEqual(code, 0, out)

    def test_batch_two_compiles_beside_a_sealed_batch_one(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for rel in ['docs/audio/sound-requests-install.json', 'docs/audio/sound-queue.json', 'docs/audio/' + B1,
                        'unity/ChipArenaConversion/tools/sound_bundle/docs/audio/' + B1,
                        'unity/ChipArenaConversion/Assets/ChipArena/Core/NativeIntroSoundKeys.cs']:
                (root / rel).parent.mkdir(parents=True, exist_ok=True)
                shutil.copy(REPO / rel, root / rel)
            shutil.copytree(AUDIO / 'intros', root / 'docs/audio/intros')
            keys = root / 'unity/ChipArenaConversion/Assets/ChipArena/Core/NativeIntroSoundKeys.cs'
            keys.write_text(keys.read_text(encoding='utf-8').replace('// ---- I3 (melee) ----', '// ---- I3 (melee) ----\n            K("intro2.zztest.cue",3f),'), encoding='utf-8')
            row = dict(json.loads((AUDIO / B1).read_text(encoding='utf-8'))['sounds'][0], key='intro2.zztest.cue')
            (root / 'docs/audio/intros-b2').mkdir()
            (root / 'docs/audio/intros-b2/I3.json').write_text(json.dumps(dict(author='I3', sounds=[row]), indent=2), encoding='utf-8')
            code, out = self.run_compiler('--root', tmp, '--batch', 'b2', '--job', 'intros-b2-test')
            self.assertEqual(code, 0, out)
            self.assertIn('"manifest": "docs/audio/' + B2 + '"', out)
            made = root / 'docs/audio' / B2
            self.assertEqual(made.read_bytes(), (root / 'unity/ChipArenaConversion/tools/sound_bundle/docs/audio' / B2).read_bytes())
            self.assertEqual([s['key'] for s in json.loads(made.read_text(encoding='utf-8'))['sounds']], ['intro2.zztest.cue'])
            self.assertEqual(lf_sha(root / 'docs/audio' / B1), B1_SHA)
            code, out = self.run_compiler('--root', tmp)  # batch 1 rebuilds the pinned bytes
            self.assertEqual(code, 0, out)
            self.assertEqual(lf_sha(root / 'docs/audio' / B1), B1_SHA)
            # a new row in batch 1's folder: refused (sealed), nothing written
            (root / 'docs/audio/intros/I9.json').write_text(json.dumps(dict(author='I9', sounds=[dict(row, key='intro2.zzlate.cue')])), encoding='utf-8')
            keys.write_text(keys.read_text(encoding='utf-8').replace('// ---- I3 (melee) ----', '// ---- I3 (melee) ----\n            K("intro2.zzlate.cue",3f),'), encoding='utf-8')
            code, out = self.run_compiler('--root', tmp)
            self.assertEqual(code, 1, out)
            self.assertIn('is sealed', out)
            self.assertEqual(lf_sha(root / 'docs/audio' / B1), B1_SHA)
            # a key in both batches: refused
            (root / 'docs/audio/intros/I9.json').unlink()
            (root / 'docs/audio/intros-b2/I4.json').write_text(json.dumps(dict(author='I4', sounds=[dict(row, key='intro2.cloudbreak.wind')])), encoding='utf-8')
            code, out = self.run_compiler('--root', tmp, '--batch', 'b2')
            self.assertEqual(code, 1, out)
            self.assertIn('already requested by batch b1', out)


if __name__ == '__main__':
    unittest.main()
