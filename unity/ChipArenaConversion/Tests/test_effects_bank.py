"""Verify the candidate archive through the same public installer Claude and the builder run."""
import json
from pathlib import Path
import subprocess
import sys
import unittest
from unittest.mock import patch
import zipfile

import test_visual_body_assets as visual
import effects_bank as fx
import install_local_assets as installer
import pack_effects_bank as packer


class EffectsBankTests(unittest.TestCase):
    setUp = visual.VisualBodyTests.setUp
    tearDown = visual.VisualBodyTests.tearDown

    def bank(self):
        for directory in ('ChipArenaFx', 'ChipArenaFx/Resources', 'ChipArenaFx/Resources/PackBank'):
            self.bake.write(self.bake.local / (directory + '.meta'), visual.meta(self.bake.next(), True))
        self.bake.asset('ChipArenaFx/Resources/PackBank/muzzle.png', 'pixels')
        return self.bake.local / 'ChipArenaFx/Resources/PackBank/muzzle.png'

    def test_pack_replace_install_and_fallback(self):
        image = self.bank()
        candidate = self.tmp / 'candidate.zip'
        with zipfile.ZipFile(self.base) as archive:
            original = {m: archive.read(m) for m in archive.namelist()}
        size = sum(map(len, original.values()))
        with patch.object(installer, 'MAX_ZIP_BYTES', size):
            packer.pack(self.base, candidate, self.dev)
        cmd = [sys.executable, '-I', str(Path(installer.__file__)), str(candidate), '--project', str(self.project)]
        subprocess.run(cmd + ['--check'], check=True)
        subprocess.run(cmd, check=True)
        self.assertEqual((self.project / 'Assets/LocalAssets' / image.relative_to(self.bake.local)).read_bytes(), image.read_bytes())
        image.unlink()
        image.with_suffix('.png.meta').unlink()
        self.bake.asset('ChipArenaFx/Resources/PackBank/hole.png', 'new pixels')
        replacement = self.tmp / 'next.zip'
        packer.pack(candidate, replacement, self.dev)
        with zipfile.ZipFile(replacement) as archive:
            self.assertTrue(all(archive.read(k) == v for k, v in original.items()))
            self.assertNotIn(image.relative_to(self.bake.local).as_posix(), archive.namelist())
        subprocess.run([*cmd[:3], str(replacement), *cmd[4:]], check=True)
        self.assertFalse((self.project / 'Assets/LocalAssets' / image.relative_to(self.bake.local)).exists())
        with patch.object(fx, 'BUDGET', 1), self.assertRaisesRegex(installer.Problem, 'budget'):
            packer.pack(replacement, candidate, self.dev)
        subprocess.run([*cmd[:3], str(self.base), *cmd[4:], '--check'], check=True)

    def test_tamper_partial_and_unsupported(self):
        image = self.bank()
        files = fx.inventory(self.bake.local)
        with self.assertRaises(installer.Problem):
            fx.verify(self.bake.local)
        (self.bake.local / fx.MANIFEST).write_text(json.dumps({'schema': 1, 'files': files}))
        image.write_text('changed')
        with self.assertRaisesRegex(installer.Problem, 'hashes'):
            fx.verify(self.bake.local)
        self.bake.asset('ChipArenaFx/script.cs', 'test')
        with self.assertRaisesRegex(installer.Problem, 'unexpected'):
            fx.inventory(self.bake.local)
