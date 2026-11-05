"""Exercise the actual Unity-only export shape without credentials or API calls."""
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
import sound_queue as q
import export_sound_inputs as export


class SoundInputTests(unittest.TestCase):
    def test_committed_bundle_matches_reviewed_inputs(self):
        export.export(check=True)

    def test_unity_only_checkout_resolves_the_real_approved_sample(self):
        with tempfile.TemporaryDirectory() as d:
            tools = Path(d) / 'unity/ChipArenaConversion/tools'
            tools.mkdir(parents=True)
            for name in ('sound_queue.py', 'generate_sound_requests.py', 'export_sound_inputs.py'):
                shutil.copy(Path(q.__file__).parent / name, tools / name)
            shutil.copytree(q.BUNDLED_REPO, tools / 'audio-inputs')
            script = ('import sound_queue as q; import export_sound_inputs as e; '
                      'e.export(check=True); path,repo=q.inputs(); plans=q.plan(path,repo); '
                      'assert len(plans)==1; row,_,_,_,jobs=plans[0]; '
                      'assert row["id"]=="boss-sample-20261105"; '
                      'assert len(jobs)==6 and sum(j["durationSeconds"] for j in jobs)==11; '
                      'print("PASS Unity-only approved sample")')
            result = subprocess.run([sys.executable, '-c', script], cwd=tools, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)

    def test_canonical_queue_never_falls_back_to_different_manifest(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d); path = root / 'docs/audio/sound-queue.json'
            path.parent.mkdir(parents=True); shutil.copy(q.QUEUE, path)
            with patch.object(q, 'QUEUE', path), patch.object(q.gen, 'REPO', root):
                queue, repo = q.inputs()
                self.assertEqual(repo, root)
                with self.assertRaises(FileNotFoundError): q.plan(queue, repo)

    def test_stale_bundle_fails_packaging_check(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d); shutil.copytree(q.BUNDLED_REPO, root / 'bundle')
            path = root / 'bundle/docs/audio/sound-queue.json'
            path.write_text(path.read_text() + '\n')
            with patch.object(q, 'BUNDLED_REPO', root / 'bundle'):
                with self.assertRaisesRegex(ValueError, 'stale'): export.export(check=True)

    def test_empty_canonical_queue_overrides_old_approved_bundle(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d); path = root / 'docs/audio/sound-queue.json'
            path.parent.mkdir(parents=True)
            path.write_text(json.dumps({'schema':'chip-arena.sound-queue/1','jobs':[]}))
            with patch.object(q, 'QUEUE', path), patch.object(q.gen, 'REPO', root):
                queue, repo = q.inputs()
                self.assertEqual(q.plan(queue, repo), [])

if __name__ == '__main__': unittest.main()
