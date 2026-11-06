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
    def test_import_rejection_does_not_block_later_originals_or_regenerate(self):
        later = dict(self.job, id='sample-two', variants=[2])
        q.write(self.queue, dict(schema='chip-arena.sound-queue/1', jobs=[self.job, later]))
        original = self.command
        def reject(args):
            original(args)
            args = list(map(str, args))
            if '--out' in args:
                # Both jobs must already be receipted and shared before importing.
                self.assertEqual(len(self.calls), 2)
                self.assertTrue((self.root / 'sample-two.zip').is_file())
                q.write(Path(args[args.index('--report') + 1]),
                        dict(problems=[], keys={'selected': dict(accepted=0)},
                             files=[dict(file='pve.zombie.moan_01.mp3', status='rejected', reason='test rejection')]))
        with patch.object(q, 'command', side_effect=reject):
            for _ in range(2):
                with self.assertRaisesRegex(ValueError, 'Importer rejected'):
                    self.run_queue()
                self.assertEqual(len(self.calls), 2)
        report = json.loads((self.root / 'sample-one.import.json').read_text())
        self.assertEqual(report['files'][0]['reason'], 'test rejection')
    def test_windows_line_endings_preserve_approval_and_cached_originals(self):
        first = self.run_queue()
        self.manifest.write_bytes(self.manifest.read_bytes().replace(b'\n', b'\r\n'))
        self.assertEqual(q.gen.load_manifest(self.manifest)[1], self.job['manifestSha256'])
        self.assertEqual(self.run_queue(), first)
        self.assertEqual(len(self.calls), 1)

    def test_crlf_bundled_boss_sample_and_importer_use_same_hash(self):
        import hashlib
        import import_sound_requests as importer
        bundle = self.root / 'bundle'
        shutil.copytree(q.BUNDLE, bundle)
        for path in bundle.rglob('*.json'):
            path.write_bytes(path.read_bytes().replace(b'\n', b'\r\n'))
        plans = q.plan(bundle / 'docs/audio/sound-queue.json', bundle)
        self.assertEqual(len(plans), 9)  # the bosses' five jobs, then NOCTIEL's four (takes 1-4)
        row, path, manifest, digest, jobs = plans[0]
        self.assertEqual(len(jobs), 6)
        self.assertEqual(sum(j['durationSeconds'] for j in jobs), 11)
        self.assertEqual(hashlib.sha256(importer.load_manifest(path)[0]).hexdigest(), digest)
        # A real content change remains blocked even with Windows line endings.
        path.write_bytes(path.read_bytes().replace(b'fluorescent', b'altered fluorescent', 1))
        with self.assertRaisesRegex(ValueError, 'Manifest changed'):
            q.plan(bundle / 'docs/audio/sound-queue.json', bundle)
        self.assertFalse(self.calls)

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
    def test_missing_queue_names_file_and_stage_without_requests(self):
        self.queue.unlink()
        with self.assertRaisesRegex(ValueError, 'read queue and manifest: FileNotFoundError') as error:
            self.run_queue()
        self.assertIn(str(self.queue), str(error.exception))
        self.assertFalse(self.calls)
    def test_missing_key_names_file_and_stage_without_requests(self):
        key = self.root / 'key.dpapi'; key.unlink()
        with self.assertRaisesRegex(ValueError, 'read saved ElevenLabs credential: FileNotFoundError') as error:
            self.run_queue()
        self.assertIn(str(key), str(error.exception))
        self.assertNotIn('PRIVATE', str(error.exception))
        self.assertFalse(self.calls)
    def test_unity_only_bundle_covers_all_boss_clips_without_sample_duplicates(self):
        q.check_bundle()
        with patch.object(Path, 'is_file', return_value=False):
            queue, repo = q.queue_inputs(q.QUEUE, q.gen.REPO)
        self.assertEqual(repo, q.BUNDLE)
        every = q.plan(queue, repo)
        plans = [p for p in every if p[0]['manifest'] == 'docs/audio/sound-requests-bosses.json']
        jobs = [job for row in plans for job in row[4]]
        self.assertEqual(len(jobs), 121)
        self.assertAlmostEqual(sum(job['durationSeconds'] for job in jobs), 195.3)
        names = [job['file'] for job in jobs]
        self.assertEqual(len(set(names)), len(names))
        expected = q.gen.entries(plans[0][2], optional=True)
        self.assertEqual(set(names), {job['file'] for job in expected})
        self.assertEqual(plans[0][0]['id'], 'boss-sample-20261105')
        self.assertEqual(len(plans[0][4]), 6)
        self.assertEqual(sum(len(p[4]) for p in plans[1:]), 115)
        # NOCTIEL's batch (owner 2026-11-06, "queue it on the builder"): every take once, within each job's cap.
        noctiel = [p for p in every if p[0]['manifest'] == 'docs/audio/sound-requests-noctiel.json']
        self.assertEqual([p[0]['variants'] for p in noctiel], [[1], [2], [3], [4]])
        takes = [job for row in noctiel for job in row[4]]
        self.assertEqual(len(takes), 92)
        self.assertAlmostEqual(sum(job['durationSeconds'] for job in takes), 148.6)
        files = [job['file'] for job in takes]
        self.assertEqual(len(set(files)), len(files))
        self.assertEqual(set(files), {job['file'] for job in q.gen.entries(noctiel[0][2], optional=True)})
        self.assertEqual(len(every), len(plans) + len(noctiel))
    def test_stale_bundle_fails_packaging_check(self):
        packaged = self.root / 'bundle/docs/audio'; packaged.mkdir(parents=True)
        canonical = self.repo / 'docs/audio/sound-queue.json'
        canonical.write_text('canonical')
        (packaged / 'sound-queue.json').write_text('stale')
        with self.assertRaisesRegex(ValueError, 'Refresh tools/sound_bundle'):
            q.check_bundle(self.repo, self.root / 'bundle')
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
