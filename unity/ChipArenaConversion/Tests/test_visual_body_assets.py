"""The baked visual body ships beside the licensed effects audio: packed by tools/pack_visual_body.py, checked and installed
into the ignored Assets/LocalAssets by tools/install_local_assets.py, never deleting anything already there."""
from pathlib import Path
import hashlib
import io
import json
import shutil
import sys
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
sys.path.insert(0, str(ROOT / 'Tests'))
import install_local_assets as installer  # noqa: E402
import pack_visual_body as packer  # noqa: E402
from test_effects_audio import LicensedTree  # noqa: E402

PREFAB = 'ChipArenaLocal/Resources/ChipArenaVisualBody.prefab'


def meta(guid, folder=False):
    return f'fileFormatVersion: 2\nguid: {guid}\n' + ('folderAsset: yes\nDefaultImporter:\n' if folder else 'NativeFormatImporter:\n')


class Bake:
    """A fake local bake: the prefab uses a material in its own folder, which uses a pack texture; a pack material (the FBX's)
    uses a shader graph; the prefab also names a tracked shader outside LocalAssets."""
    def __init__(self, project):
        self.local = project / 'Assets/LocalAssets'
        self.guid = 0x1000
        tracked = project / 'Assets/ChipArena/Resources/Toon.shader'
        self.write(tracked, 'Shader "x" {}')
        self.write(tracked.with_name('Toon.shader.meta'), meta('a' * 32))
        for folder in ('ChipArenaLocal', 'ChipArenaLocal/Resources', 'ChipArenaLocal/Materials', 'ChipArenaLocal/Stride', 'Pack', 'Pack/Textures', 'Pack/Meshes', 'Pack/Shaders', 'Pack/Clips'):
            self.write(self.local / (folder + '.meta'), meta(self.next(), True))
        self.graph = self.asset('Pack/Shaders/Clothes.shadergraph', '{}')
        self.texture = self.asset('Pack/Textures/Top_D.png', 'PNG fake')
        self.pack_mat = self.asset('Pack/Meshes/Top.mat', f'm_Shader: {{fileID: 4800000, guid: {self.graph}, type: 3}}')
        self.mesh = self.asset('Pack/Meshes/Top.fbx', 'FBX fake', extra=f'externalObjects:\n  - second: {{fileID: 2100000, guid: {self.pack_mat}, type: 2}}\n')
        self.mat = self.asset('ChipArenaLocal/Materials/Top 1.mat', f'm_Shader: {{fileID: 4800000, guid: {"a" * 32}, type: 3}}\n  m_Texture: {{fileID: 2800000, guid: {self.texture}, type: 3}}')
        # The stride: the skeleton's animator controller names a pack clip (an FBX).
        self.clip = self.asset('Pack/Clips/Run.fbx', 'FBX clip fake')
        self.controller = self.asset('ChipArenaLocal/Stride/Stride.controller', f'm_Motion: {{fileID: 7400000, guid: {self.clip}, type: 3}}')
        self.asset(PREFAB, f'm_Mesh: {{fileID: 4300000, guid: {self.mesh}, type: 3}}\nm_Materials:\n  - {{fileID: 2100000, guid: {self.mat}, type: 2}}\nm_Controller: {{fileID: 9100000, guid: {self.controller}, type: 2}}\n')
        # The motion bank: a data file beside the prefab, loaded by name.
        self.asset('ChipArenaLocal/Resources/ChipArenaVisualMotion.bytes', 'CAMO fake')
        self.write(self.local / 'Pack/Textures/Unused.png', 'not used')
        self.write(self.local / 'Pack/Textures/Unused.png.meta', meta(self.next()))

    def next(self):
        self.guid += 1
        return f'{self.guid:032x}'

    def write(self, path, text):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding='utf-8')

    def asset(self, rel, text, extra=''):
        guid = self.next()
        self.write(self.local / rel, text)
        self.write(self.local / (rel + '.meta'), meta(guid) + extra)
        return guid


class VisualBodyTests(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp())
        self.dev = self.tmp / 'dev'
        self.bake = Bake(self.dev)
        self.tree = LicensedTree(self.tmp / 'licensed')
        self.base = self.tmp / 'LocalAssets.zip'
        with zipfile.ZipFile(self.base, 'w') as z:
            for path in sorted(self.tree.base.rglob('*')):
                if path.is_file():
                    z.write(path, path.relative_to(self.tree.base).as_posix())
        self.project = self.tmp / 'build'
        (self.project / 'Assets').mkdir(parents=True)

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def quiet(self, call, *args):
        out, err = io.StringIO(), io.StringIO()
        old = sys.stdout, sys.stderr
        sys.stdout, sys.stderr = out, err
        try:
            code = call(list(args))
        finally:
            sys.stdout, sys.stderr = old
        return code, out.getvalue() + err.getvalue()

    def packed(self):
        out = self.tmp / 'new.zip'
        code, text = self.quiet(packer.main, str(self.base), str(out), '--project', str(self.dev))
        self.assertEqual(code, 0, text)
        return out, text

    def install(self, source, *extra):
        return self.quiet(installer.main, str(source), '--project', str(self.project), *extra)

    def test_packs_only_what_the_bake_uses(self):
        out, text = self.packed()
        self.assertIn('Left out (not meshes, materials, textures or prefabs): Pack/Shaders/Clothes.shadergraph', text)
        with zipfile.ZipFile(out) as z:
            names = set(z.namelist())
            manifest = json.loads(z.read('visual-body.json'))
        self.assertEqual(manifest['prefab'], PREFAB)
        expected = {PREFAB, 'ChipArenaLocal/Materials/Top 1.mat', 'Pack/Textures/Top_D.png', 'Pack/Meshes/Top.fbx', 'Pack/Meshes/Top.mat', 'ChipArenaLocal/Stride/Stride.controller', 'Pack/Clips/Run.fbx', 'ChipArenaLocal/Resources/ChipArenaVisualMotion.bytes'}
        expected |= {p + '.meta' for p in expected} | {f + '.meta' for f in ('ChipArenaLocal', 'ChipArenaLocal/Resources', 'ChipArenaLocal/Materials', 'ChipArenaLocal/Stride', 'Pack', 'Pack/Textures', 'Pack/Meshes', 'Pack/Clips')}
        self.assertEqual(set(manifest['files']), expected)
        for rel, digest in manifest['files'].items():
            self.assertEqual(hashlib.sha256(z_bytes(out, rel)).hexdigest(), digest, rel)
        self.assertNotIn('Pack/Textures/Unused.png', names)
        self.assertNotIn('Pack/Shaders/Clothes.shadergraph', names)
        self.assertFalse(any('Toon.shader' in n for n in names))
        # The base's members are kept as they were, and the base itself is untouched.
        with zipfile.ZipFile(self.base) as z:
            for name in z.namelist():
                self.assertIn(name, names)
                self.assertEqual(z.read(name), z_bytes(out, name))
        # Deterministic: the same inputs give the same bytes.
        first = out.read_bytes()
        self.packed()
        self.assertEqual(out.read_bytes(), first)

    def test_installs_beside_the_audio_and_deletes_nothing(self):
        out, _ = self.packed()
        keep = self.project / 'Assets/LocalAssets/Pack/Textures/Other.png'
        keep.parent.mkdir(parents=True)
        keep.write_text('a full local pack keeps its other files')
        code, text = self.install(out)
        self.assertEqual(code, 0, text)
        self.assertIn('Installed the visual body: 24 files', text)
        local = self.project / 'Assets/LocalAssets'
        self.assertTrue((local / PREFAB).is_file() and (local / (PREFAB + '.meta')).is_file())
        self.assertTrue((local / 'Pack/Meshes/Top.fbx').is_file())
        self.assertTrue((local / 'Resources/EffectsAudio/manifest.json').is_file())
        self.assertTrue(keep.is_file())
        self.assertFalse((local / 'visual-body.json').exists())
        self.assertEqual(self.install(out, '--check')[0], 0)
        self.assertEqual(self.install(out)[0], 0)  # again, over itself
        # --remove takes the audio only.
        self.assertEqual(self.quiet(installer.main, '--remove', '--project', str(self.project))[0], 0)
        self.assertTrue((local / PREFAB).is_file())

    def test_without_a_visual_body_the_audio_installs_as_before(self):
        code, text = self.install(self.base)
        self.assertEqual(code, 0, text)
        self.assertIn('No visual body in the source', text)
        self.assertFalse((self.project / 'Assets/LocalAssets/ChipArenaLocal').exists())

    def test_rejects_bad_visual_bodies(self):
        out, _ = self.packed()
        cases = {
            'a script': lambda m, root: (write(root, 'ChipArenaLocal/Run.cs', 'class X{}', m), write(root, 'ChipArenaLocal/Run.cs.meta', meta('b' * 32), m)),
            'a shader': lambda m, root: (write(root, 'Pack/X.shader', 'Shader "x" {}', m), write(root, 'Pack/X.shader.meta', meta('c' * 32), m)),
            'a changed file': lambda m, root: (root / 'Pack/Textures/Top_D.png').write_text('changed'),
            'a missing meta': lambda m, root: m['files'].pop('Pack/Meshes/Top.fbx.meta'),
            'a missing folder meta': lambda m, root: m['files'].pop('Pack/Meshes.meta'),
            'a parent path': lambda m, root: m['files'].__setitem__('../escape.png', '0' * 64),
            'the top Resources folder': lambda m, root: write(root, 'Resources/Body.png', 'x', m),
            'no prefab': lambda m, root: m.__setitem__('prefab', 'ChipArenaLocal/Other.prefab'),
            'a duplicate guid': lambda m, root: write(root, 'Pack/Textures/Top_D.png.meta', (root / 'Pack/Meshes/Top.fbx.meta').read_text(), m),
            'an unknown field': lambda m, root: m.__setitem__('run', 'x'),
        }
        for name, change in cases.items():
            with self.subTest(name=name):
                root = self.tmp / ('bad-' + name.replace(' ', '-'))
                with zipfile.ZipFile(out) as z:
                    z.extractall(root)
                manifest = json.loads((root / 'visual-body.json').read_text())
                change(manifest, root)
                (root / 'visual-body.json').write_text(json.dumps(manifest))
                code, text = self.install(root, '--check')
                self.assertEqual(code, 1, name + ': ' + text)
                self.assertIn('visual body', text)
                self.assertFalse((self.project / 'Assets/LocalAssets').exists())

    def test_over_budget_is_rejected(self):
        out, _ = self.packed()
        old = installer.VISUAL_BODY_BUDGET
        installer.VISUAL_BODY_BUDGET = 10
        try:
            code, text = self.install(out, '--check')
        finally:
            installer.VISUAL_BODY_BUDGET = old
        self.assertEqual(code, 1)
        self.assertIn('budget', text)

    def test_needs_a_bake(self):
        empty = self.tmp / 'empty'
        (empty / 'Assets/LocalAssets').mkdir(parents=True)
        code, text = self.quiet(packer.main, str(self.base), str(self.tmp / 'x.zip'), '--project', str(empty))
        self.assertEqual(code, 1)
        self.assertIn('no bake', text)
        self.assertFalse((self.tmp / 'x.zip').exists())


def z_bytes(archive, name):
    with zipfile.ZipFile(archive) as z:
        return z.read(name)


def write(root, rel, text, manifest):
    path = root / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding='utf-8')
    manifest['files'][rel] = hashlib.sha256(path.read_bytes()).hexdigest()


if __name__ == '__main__':
    unittest.main()
