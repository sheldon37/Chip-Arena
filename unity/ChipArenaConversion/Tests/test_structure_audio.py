"""The synthesized destruction clips: deterministic, asset-locked, outside the 83-clip source bank, routed by Core."""
import hashlib
import json
import re
import subprocess
import sys
import tempfile
import unittest
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CLIPS = ROOT / 'Assets/ChipArena/Resources/StructureAudio'
NAMES = ('thud', 'crack', 'gravel', 'rebar', 'groan', 'creak', 'snap', 'boom', 'rumble', 'glass', 'debris')


class StructureAudioTests(unittest.TestCase):
    def test_generator_reproduces_the_committed_clips(self):
        with tempfile.TemporaryDirectory() as out:
            subprocess.run([sys.executable, str(ROOT / 'tools/synth_structure_audio.py'), '--out', out], check=True, capture_output=True)
            for name in NAMES:
                self.assertEqual((Path(out) / (name + '.wav')).read_bytes(), (CLIPS / (name + '.wav')).read_bytes(), name)

    def test_clips_match_the_bank_format_and_have_metas(self):
        for name in NAMES:
            path = CLIPS / (name + '.wav')
            with wave.open(str(path)) as w:
                self.assertEqual((w.getframerate(), w.getnchannels(), w.getsampwidth()), (22050, 1, 2), name)
                self.assertTrue(0.3 <= w.getnframes() / 22050 <= 4, name)
            self.assertLess(path.stat().st_size, 1024 * 1024, name)
            meta = (CLIPS / (name + '.wav.meta')).read_text()
            self.assertRegex(meta, r'(?m)^guid: [0-9a-f]{32}$')
            self.assertIn('AudioImporter:', meta)

    def test_receipts_lock_every_clip(self):
        receipts = {a['path']: a for a in json.loads((ROOT / 'tools/asset-lock.json').read_text())['assets']}
        for name in NAMES:
            rel = 'Assets/ChipArena/Resources/StructureAudio/%s.wav' % name
            data = (ROOT / rel).read_bytes()
            self.assertIn(rel, receipts)
            self.assertEqual(receipts[rel]['bytes'], len(data))
            self.assertEqual(receipts[rel]['gitBlob'], hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest())
            self.assertEqual(receipts[rel]['source'], 'tools/synth_structure_audio.py')

    def test_source_bank_gate_is_untouched(self):
        bank = [p for p in (ROOT / 'Assets/Conversion/Resources/Audio').rglob('*') if p.suffix in ('.mp3', '.wav')]
        self.assertEqual(len(bank), 83)
        self.assertIn('audioCount!=83', (ROOT / 'Assets/ChipArena/Editor/NativeBuildChecks.cs').read_text())

    def test_core_routes_exactly_the_synthesized_set(self):
        core = (ROOT / 'Assets/ChipArena/Core/NativeStructureFx.cs').read_text()
        listed = re.search(r'Synthesized=\{([^}]*)\}', core).group(1)
        self.assertEqual(tuple(x.strip().strip('"') for x in listed.split(',')), NAMES)
        used = set(re.findall(r'P\+"([a-z]+)"', core))
        self.assertTrue(used <= set(NAMES) and used == set(NAMES), sorted(used))


if __name__ == '__main__':
    unittest.main()
