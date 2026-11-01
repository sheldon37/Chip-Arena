"""No live API calls: generator contract, paid-request safety and importer round trip."""
from pathlib import Path
import io
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import urllib.error

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
import generate_sound_requests as gen


class GeneratorTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.folder = Path(self.tmp.name) / 'batch'
        self.m, self.sha = gen.load_manifest(gen.MANIFEST)
        self.jobs = gen.entries(self.m, sample=True)

    def tearDown(self):
        self.tmp.cleanup()

    def test_counts_and_selection(self):
        self.assertEqual(len(gen.entries(self.m)), 70)
        self.assertEqual(len(gen.entries(self.m, optional=True)), 148)  # incl. the 60 optional Central Station cue files
        self.assertEqual(len(self.jobs), 6)
        self.assertEqual(len(gen.entries(self.m, keys=['pve.zombie.moan'], variants=[2, 4])), 2)
        for args in (dict(keys=['unknown']), dict(variants=[99]), dict(keys=['move.step.metal'])):
            with self.assertRaises(ValueError):
                gen.entries(self.m, **args)

    def test_dry_run_no_key_network_or_writes(self):
        with patch.object(gen, 'request_audio', side_effect=AssertionError('network')), patch.dict('os.environ', {}, clear=True):
            self.assertEqual(gen.main(['--dry-run', '--out', str(self.folder)]), 0)
        self.assertFalse(self.folder.exists())

    def test_paths_refuse_repo(self):
        with self.assertRaises(ValueError):
            gen.outside_repo(gen.REPO / 'sounds.zip')

    def test_rate_limit_bounded_and_credentials_not_in_error(self):
        calls = []
        def rejected(req, timeout):
            calls.append(req)
            raise urllib.error.HTTPError(req.full_url, 429, 'SECRET', {}, None)
        sleeps = []
        with self.assertRaisesRegex(ValueError, 'HTTP 429'):
            gen.request_audio(self.jobs[0], 'SECRET', opener=rejected, sleep=sleeps.append)
        self.assertEqual(len(calls), 4)
        self.assertEqual(sleeps, [2, 4, 8])
        payload = json.loads(calls[0].data)
        self.assertEqual(payload['text'], self.jobs[0]['prompt'])
        self.assertEqual(payload['model_id'], gen.MODEL)
        self.assertFalse(payload['loop'])

    def test_uncertain_network_not_retried(self):
        def failed(req, timeout):
            raise urllib.error.URLError('SECRET')
        with self.assertRaisesRegex(ValueError, 'outcome uncertain') as error:
            gen.request_audio(self.jobs[0], 'SECRET', opener=failed)
        self.assertNotIn('SECRET', str(error.exception))

    def test_server_errors_not_retried(self):
        for code in (401, 403, 422, 500):
            calls = []
            def failed(req, timeout):
                calls.append(1)
                raise urllib.error.HTTPError(req.full_url, code, 'SECRET', {}, None)
            with self.assertRaisesRegex(ValueError, f'HTTP {code}'):
                gen.request_audio(self.jobs[0], 'SECRET', opener=failed)
            self.assertEqual(len(calls), 1)

    def test_bad_response_rejected(self):
        class Response(io.BytesIO):
            status = 200
            headers = {'Content-Type': 'audio/mpeg'}
        with self.assertRaises(ValueError):
            gen.request_audio(self.jobs[0], 'SECRET', opener=lambda *a, **k: Response(b'{"error":"secret"}' * 20))

    def test_pending_marker_prevents_silent_retry(self):
        r = gen.open_batch(self.folder, self.sha, gen.entries(self.m, optional=True))
        with self.assertRaises(ValueError):
            gen.generate(self.folder, r, self.jobs, 'SECRET', fetch=lambda *a: (_ for _ in ()).throw(ValueError('uncertain')))
        self.assertTrue((self.folder / 'pending-request.json').exists())
        with self.assertRaisesRegex(ValueError, 'Unreceipted'):
            gen.open_batch(self.folder, self.sha, gen.entries(self.m, optional=True))
        self.assertNotIn('SECRET', (self.folder / 'pending-request.json').read_text())

    @unittest.skipUnless(shutil.which('ffmpeg'), 'ffmpeg required')
    def test_importer_roundtrip_resume_bom_zip_and_stale_hash(self):
        sample = Path(self.tmp.name) / 'tone.mp3'
        subprocess.run(['ffmpeg', '-v', 'error', '-f', 'lavfi', '-i', 'sine=frequency=220:duration=1',
                        '-b:a', '192k', str(sample)], check=True)
        audio = sample.read_bytes()
        r = gen.open_batch(self.folder, self.sha, gen.entries(self.m, optional=True))
        gen.generate(self.folder, r, self.jobs, 'SECRET', fetch=lambda *a: audio)
        receipt = self.folder / 'receipt.json'
        receipt.write_text(receipt.read_text(), encoding='utf-8-sig')
        restored = gen.open_batch(self.folder, self.sha, gen.entries(self.m, optional=True))
        gen.generate(self.folder, restored, self.jobs, 'SECRET', fetch=lambda *a: self.fail('resume called API'))
        z = Path(self.tmp.name) / 'batch.zip'
        self.assertEqual(gen.main(['--out', str(self.folder), '--sample-only', '--check', '--zip', str(z)]), 0)
        result = subprocess.run([sys.executable, str(ROOT / 'tools/import_sound_requests.py'), '--manifest', str(gen.MANIFEST), '--in', str(z), '--check'], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertNotIn('SECRET', receipt.read_text())
        with self.assertRaisesRegex(ValueError, 'Stale'):
            gen.open_batch(self.folder, 'bad-sha', gen.entries(self.m, optional=True))
        (self.folder / self.jobs[0]['file']).write_bytes(b'broken')
        with self.assertRaises(ValueError):
            gen.open_batch(self.folder, self.sha, gen.entries(self.m, optional=True))


if __name__ == '__main__':
    unittest.main()
