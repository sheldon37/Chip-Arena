"""Queue integration with synthetic audio and mocked imports: no network or credits."""
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
import zipfile
from unittest.mock import patch
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
import sound_queue as q

class QueueTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.repo = self.root / 'repo'
        audio = self.repo / 'docs/audio'; audio.mkdir(parents=True)
        self.manifest = audio / 'sound.json'
        shutil.copy(q.gen.MANIFEST, self.manifest)
        self.queue = audio / 'queue.json'
        self.job = dict(id='sample-one', approved=True, manifest='docs/audio/sound.json',
                        manifestSha256=q.sha(self.manifest), keys=['pve.zombie.moan'], variants=[1], maxRequests=1, maxSeconds=30)
        self.save_queue()
        q.write(self.root / 'config.json', dict(enabled=True, maxRequests=10, maxSeconds=30))
        (self.root / 'key.dpapi').write_bytes(b'ENCRYPTED')
        self.base = self.root / 'base.zip'; self.base.write_bytes(b'BASE')
        self.calls = []
        original = q.gen.generate
        self.patches = [patch.object(q, 'protect', return_value=b'PRIVATE'),
                        patch.object(q.gen, 'validate_audio'), patch.object(q, 'command', side_effect=self.command),
                        patch.object(q.gen, 'generate', side_effect=lambda f,r,j,s: original(f,r,j,s,fetch=self.fetch))]
        for p in self.patches: p.start()
    def tearDown(self):
        for p in reversed(self.patches): p.stop()
        self.tmp.cleanup()
    def save_queue(self): q.write(self.queue, dict(schema='chip-arena.sound-queue/1', jobs=[self.job]))
    def fetch(self, entry, secret): self.calls.append(entry); return b'AUDIO' * 100
    def command(self, args):
        args = list(map(str, args))
        if '--out' in args:
            Path(args[args.index('--out') + 1]).write_bytes(b'IMPORTED')
            q.write(Path(args[args.index('--report') + 1]), dict(problems=[], keys={'selected': dict(accepted=1)}))
    def run_queue(self): return q.process(self.base, self.root, self.queue, self.repo)
    def test_rebuild_and_changed_base_never_regenerate(self):
        output = self.run_queue(); self.assertTrue(output.exists()); self.assertEqual(len(self.calls), 1)
        self.assertEqual(self.run_queue(), output); self.assertEqual(len(self.calls), 1)
        self.base.write_bytes(b'NEW BASE'); self.assertNotEqual(self.run_queue(), output)
        self.assertEqual(len(self.calls), 1)
    def test_disabled_and_empty_do_not_generate(self):
        q.write(self.queue, dict(schema='chip-arena.sound-queue/1', jobs=[]))
        self.assertEqual(self.run_queue(), self.base)
        q.write(self.root / 'config.json', dict(enabled=False)); self.save_queue()
        self.assertEqual(self.run_queue(), self.base); self.assertFalse(self.calls)
    def test_manifest_mutation_blocks(self):
        self.manifest.write_text(self.manifest.read_text() + '\n')
        with self.assertRaises(ValueError): self.run_queue()
        self.assertFalse(self.calls)
    def test_caps_checked_before_requests(self):
        q.write(self.root / 'config.json', dict(enabled=True, maxRequests=1, maxSeconds=.01))
        with self.assertRaises(ValueError): self.run_queue()
        self.assertFalse(self.calls)
    def test_uncertain_request_blocks_next_build(self):
        with patch.object(self, 'fetch', side_effect=RuntimeError('network')):
            with self.assertRaises(ValueError): self.run_queue()
        with self.assertRaises(ValueError): self.run_queue()
        self.assertFalse(self.calls)
    def test_changed_job_and_corrupt_cache_block(self):
        output = self.run_queue(); output.write_bytes(b'CORRUPTED')
        with self.assertRaises(ValueError): self.run_queue()
        self.job['maxRequests'] = 2; self.save_queue()
        with self.assertRaises(ValueError): self.run_queue()
        self.assertEqual(len(self.calls), 1)
    def test_shared_status_and_zip_exclude_credentials(self):
        shared = self.root / 'drive'; shared.mkdir()
        self.base = shared / 'LocalAssets.zip'; self.base.write_bytes(b'BASE')
        self.run_queue()
        status = json.loads((shared / 'Latest-Sound-Status.json').read_text())
        self.assertEqual(status['state'], 'complete')
        with zipfile.ZipFile(shared / 'sample-one.zip') as bundle:
            self.assertEqual(set(bundle.namelist()), {'receipt.json', 'pve.zombie.moan_01.mp3'})
            self.assertNotIn(b'PRIVATE', bundle.read('receipt.json'))
        self.run_queue()
        self.assertEqual(len(self.calls), 1)
    def test_failure_reason_survives_missing_local_status(self):
        shared = self.root / 'drive'; shared.mkdir()
        self.base = shared / 'LocalAssets.zip'; self.base.write_bytes(b'BASE')
        q.write(self.root / 'config.json', dict(enabled=True, maxRequests=1, maxSeconds=.01))
        original = q.write
        def fail_local(path, value):
            if path == self.root / 'Latest-Sound-Status.json':
                raise PermissionError('not writable')
            original(path, value)
        with patch.object(q, 'write', side_effect=fail_local):
            with self.assertRaisesRegex(ValueError, 'Pending queue exceeds local'):
                self.run_queue()
        self.assertEqual(json.loads((shared / 'Latest-Sound-Status.json').read_text())['state'], 'blocked')
        self.assertFalse(self.calls)
    def test_bad_limits(self):
        for value in [0, -1, float('nan'), float('inf'), True]:
            with self.assertRaises(ValueError): q.limits(dict(maxRequests=1, maxSeconds=value))
    def test_lock_excludes_second_worker(self):
        with q.lock(self.root):
            with self.assertRaises(ValueError):
                with q.lock(self.root): pass
    def test_manifest_escape(self):
        self.job['manifest'] = '../escape.json'; self.save_queue()
        with self.assertRaises(ValueError): self.run_queue()
        self.assertFalse(self.calls)

if __name__ == '__main__': unittest.main()
