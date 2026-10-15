"""The licensed effects audio stays out of the public repository and installs only into the ignored Assets/LocalAssets."""
from pathlib import Path
import io
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
import install_local_assets as installer  # noqa: E402
import package_source  # noqa: E402


def ogg(seconds, channels, rate=44100):
    """A minimal two-page Ogg Vorbis stream: the identification header, then a last page whose granule is the length."""
    def page(kind, granule, payload, sequence):
        return b'OggS\x00' + bytes([kind]) + int(granule).to_bytes(8, 'little') + b'\x01\x00\x00\x00' + sequence.to_bytes(4, 'little') + b'\x00' * 4 + bytes([1, len(payload)]) + payload
    ident = b'\x01vorbis' + b'\x00' * 4 + bytes([channels]) + rate.to_bytes(4, 'little') + b'\x00' * 12 + b'\xb8\x01'
    return page(2, 0, ident, 0) + page(4, round(seconds * rate), b'\x00', 1)


def meta(guid, folder=False):
    return f'fileFormatVersion: 2\nguid: {guid}\n' + ('folderAsset: yes\nDefaultImporter:\n' if folder else 'AudioImporter:\n')


class LicensedTree:
    """A fake licensed-assets checkout: a positional and a 2D key."""
    def __init__(self, base):
        self.base = base
        self.bank = base / 'Resources/EffectsAudio'
        self.bank.mkdir(parents=True)
        self.guid = 0
        self.write(base / 'Resources.meta', meta(self.next(), True))
        self.write(base / 'Resources/EffectsAudio.meta', meta(self.next(), True))
        self.manifest = {'schema': 1, 'events': {'impact.stone': {'clips': ['0', '1'], 'gain': .8, 'cooldown': .05}, 'ui.click': {'clips': ['0'], 'space': '2d'}, 'hit.confirm': {'clips': []}}}
        self.save()
        for key, channels, seconds in (('impact.stone', 1, .4), ('ui.click', 2, .2)):
            self.write(self.bank / (key + '.meta'), meta(self.next(), True))
            for name in self.manifest['events'][key]['clips']:
                self.clip(key, name, ogg(seconds, channels))
        (base / 'LICENSES.md').write_text('private\n')
        (base / 'tools').mkdir()
        (base / 'tools/build.py').write_text('raise SystemExit("never run")\n')

    def next(self):
        self.guid += 1
        return f'{self.guid:032x}'

    def write(self, path, text):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text)

    def clip(self, key, name, data):
        path = self.bank / key / (name + '.ogg')
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        self.write(path.with_name(path.name + '.meta'), meta(self.next()))

    def save(self):
        self.write(self.bank / 'manifest.json', json.dumps(self.manifest))
        self.write(self.bank / 'manifest.json.meta', meta('f' * 32))


class EffectsAudioTests(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp())
        self.project = self.tmp / 'project'
        (self.project / 'Assets').mkdir(parents=True)
        self.tree = LicensedTree(self.tmp / 'licensed')

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def run_installer(self, *args):
        out, err = io.StringIO(), io.StringIO()
        old = sys.stdout, sys.stderr
        sys.stdout, sys.stderr = out, err
        try:
            code = installer.main([*args, '--project', str(self.project)])
        finally:
            sys.stdout, sys.stderr = old
        return code, out.getvalue() + err.getvalue()

    def installed(self):
        return self.project / 'Assets/LocalAssets/Resources/EffectsAudio'

    def test_no_bank_audio_is_tracked(self):
        repo = ROOT.parents[1]
        listing = subprocess.run(['git', 'ls-files', '-z'], cwd=repo, capture_output=True)
        if listing.returncode != 0:
            self.skipTest('not a git checkout (source package)')
        tracked = [p for p in listing.stdout.decode('utf-8', 'replace').split('\0') if p]
        self.assertTrue(tracked)
        parts = lambda p: p.split('/')
        bank = [p for p in tracked if p.lower().endswith('.ogg') or 'EffectsAudio' in parts(p) or 'EffectsAudio.meta' in parts(p) or 'LocalAssets' in parts(p) or 'LocalAssets.meta' in parts(p)]
        self.assertEqual(bank, [])
        for path in ('unity/ChipArenaConversion/Assets/LocalAssets/Resources/EffectsAudio/manifest.json', 'unity/ChipArenaConversion/Assets/LocalAssets/Resources/EffectsAudio/gun.pistol.shot/0.ogg', 'unity/ChipArenaConversion/Assets/LocalAssets.meta'):
            self.assertEqual(subprocess.run(['git', 'check-ignore', '-q', path], cwd=repo).returncode, 0, path)

    def test_source_package_skips_local_assets(self):
        project = self.tmp / 'pkg'
        for rel in ('Assets/a.txt', 'Assets/a.txt.meta', 'Assets/LocalAssets/Resources/EffectsAudio/impact.stone/0.ogg', 'Assets/LocalAssets.meta'):
            (project / rel).parent.mkdir(parents=True, exist_ok=True)
            (project / rel).write_text('x')
        old = package_source.ROOT
        package_source.ROOT = project
        try:
            names = [rel.as_posix() for _, rel in package_source.selected()]
        finally:
            package_source.ROOT = old
        self.assertEqual(names, ['Assets/a.txt', 'Assets/a.txt.meta'])

    def test_contract_matches_the_core_bank(self):
        keys, schema, clips, budget = installer.contract()
        self.assertEqual((len(keys), schema, clips, budget), (226, 1, 16, 16000000))  # incl. the 14 zombie voice and footfall keys (docs/audio/SOUND-REQUESTS.md)
        self.assertEqual(sum(1 for k in keys if k.startswith('intro.')), 20)  # the 19 entrances and the DEV Sheldon Intro
        self.assertEqual(keys['gun.machinegun.shot'], ('3d', .32))
        self.assertEqual(keys['ui.click'], ('2d', .25))
        self.assertEqual(keys['field.acid'], ('loop', 6.0))

    def test_installs_from_a_folder_and_a_zip(self):
        code, out = self.run_installer(str(self.tree.base))
        self.assertEqual(code, 0, out)
        self.assertTrue((self.installed() / 'manifest.json').is_file())
        self.assertTrue((self.installed() / 'impact.stone/1.ogg.meta').is_file())
        self.assertTrue((self.project / 'Assets/LocalAssets.meta').is_file())
        self.assertFalse((self.project / 'Assets/LocalAssets/tools').exists())
        self.assertFalse((self.project / 'Assets/LocalAssets/Resources/EffectsAudio/LICENSES.md').exists())
        self.assertIn('3 keys, 3 clips', out)
        # A zip with one wrapping folder (as shared in chat) replaces it, removing a stale clip.
        stale = self.installed() / 'impact.stone/9.ogg'
        stale.write_bytes(b'x')
        archive = self.tmp / 'licensed-assets.zip'
        with zipfile.ZipFile(archive, 'w') as z:
            for path in sorted(self.tree.base.rglob('*')):
                if path.is_file():
                    z.write(path, 'licensed-assets/' + path.relative_to(self.tree.base).as_posix())
        code, out = self.run_installer(str(archive))
        self.assertEqual(code, 0, out)
        self.assertFalse(stale.exists())
        self.assertEqual(self.run_installer(str(archive), '--check')[0], 0)
        self.assertEqual(self.run_installer('--remove')[0], 0)
        self.assertFalse(self.installed().exists())

    def test_rejects_bad_content(self):
        cases = []

        def case(name, change):
            cases.append((name, change))
        case('missing clip', lambda t: (t.bank / 'impact.stone/1.ogg').unlink())
        case('missing meta', lambda t: (t.bank / 'impact.stone/1.ogg.meta').unlink())
        case('unknown key', lambda t: (t.manifest['events'].__setitem__('amb.city', {'clips': []}), t.save()))
        case('over the cap', lambda t: t.clip('impact.stone', '1', ogg(.9, 1)))
        case('stereo positional', lambda t: t.clip('impact.stone', '1', ogg(.3, 2)))
        case('wrong rate', lambda t: t.clip('impact.stone', '1', ogg(.3, 1, 48000)))
        case('not ogg', lambda t: (t.bank / 'impact.stone/1.ogg').write_bytes(b'RIFF' + b'\x00' * 80))
        case('unlisted file', lambda t: (t.bank / 'impact.stone/extra.wav').write_bytes(b'x'))
        case('bad gain', lambda t: (t.manifest['events']['impact.stone'].__setitem__('gain', 9), t.save()))
        case('space mismatch', lambda t: (t.manifest['events']['ui.click'].__setitem__('space', '3d'), t.save()))
        case('duplicate guid', lambda t: (t.bank / 'impact.stone/1.ogg.meta').write_text((t.bank / 'impact.stone/0.ogg.meta').read_text()))
        for name, change in cases:
            with self.subTest(name=name):
                base = self.tmp / ('bad-' + name.replace(' ', '-'))
                tree = LicensedTree(base)
                change(tree)
                code, out = self.run_installer(str(base), '--check')
                self.assertEqual(code, 1, name + ': ' + out)
                self.assertFalse(self.installed().exists())

    def test_rejects_unsafe_zips(self):
        for member in ('../escape.txt', '/abs.txt', 'C:/drive.txt', 'a\\b.txt'):
            with self.subTest(member=member):
                archive = self.tmp / 'bad.zip'
                with zipfile.ZipFile(archive, 'w') as z:
                    z.writestr(member, 'x')
                    z.writestr('Resources/EffectsAudio/manifest.json', '{}')
                code, out = self.run_installer(str(archive))
                self.assertEqual(code, 1, out)
                self.assertFalse((self.tmp / 'escape.txt').exists())

    def test_over_budget_is_rejected(self):
        old = installer.contract
        installer.contract = lambda source=installer.BANK_SOURCE: (*old(source)[:3], 100)
        try:
            code, out = self.run_installer(str(self.tree.base), '--check')
        finally:
            installer.contract = old
        self.assertEqual(code, 1)
        self.assertIn('budget', out)


if __name__ == '__main__':
    unittest.main()
