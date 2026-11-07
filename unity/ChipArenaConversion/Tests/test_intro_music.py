"""Entrance music: intro.<style> keys of the licensed effects bank, installed only into the ignored Assets/LocalAssets."""
from pathlib import Path
import re
import shutil
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
sys.path.insert(0, str(Path(__file__).resolve().parent))
import install_local_assets as installer  # noqa: E402
import test_effects_audio as fx  # noqa: E402
from test_effects_audio import LicensedTree, ogg  # noqa: E402

RULES = ROOT / 'Assets/ChipArena/Core/NativeIntroRules.cs'
SHELDON = 'sheldon-intro'


def entrances():
    text = RULES.read_text(encoding='utf-8')
    listed = re.search(r'public static readonly string\[\] Entrances=\{([^}]*)\}', text)[1]
    return re.findall(r'"([a-z0-9-]+)"', listed)


class IntroTree(LicensedTree):
    """The fake licensed tree plus two entrance-music keys: a default with two alternates, and a single clip."""
    def __init__(self, base):
        super().__init__(base)
        self.manifest['events']['intro.weapon-sword'] = {'clips': ['pick', 'alt-a', 'alt-b'], 'space': '2d'}
        self.manifest['events']['intro.blackout'] = {'clips': ['0']}
        self.save()
        for key in ('intro.weapon-sword', 'intro.blackout'):
            self.write(self.bank / (key + '.meta'), f'fileFormatVersion: 2\nguid: {self.next()}\nfolderAsset: yes\nDefaultImporter:\n')
            for name in self.manifest['events'][key]['clips']:
                self.clip(key, name, ogg(4.8, 2))


class IntroMusicTests(unittest.TestCase):
    run_installer = fx.EffectsAudioTests.run_installer
    installed = fx.EffectsAudioTests.installed

    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp())
        self.project = self.tmp / 'project'
        (self.project / 'Assets').mkdir(parents=True)

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def test_one_intro_key_per_non_dev_entrance(self):
        keys = installer.contract()[0]
        intro = sorted(k for k in keys if k.startswith('intro.'))
        styles = [s for s in entrances() if not s.startswith('dev-')]
        self.assertEqual(len(styles), 19)
        # The owner's DEV Sheldon Intro has music too: its id has no dev- prefix (its DEV gate is the cosmetic rules').
        self.assertNotIn(SHELDON, entrances())
        self.assertEqual(intro, sorted('intro.' + s for s in styles + [SHELDON]))
        for key in intro:
            self.assertEqual(keys[key], ('2d', 4.8), key)
        self.assertFalse(any(k.startswith('intro.dev-') for k in keys))

    def test_every_keyed_entrance_keeps_its_built_in_sting(self):
        stings = ROOT / 'Assets/Conversion/Resources/Audio/audio/intros'
        rules = RULES.read_text(encoding='utf-8')
        for style in entrances() + [SHELDON]:
            if style.startswith('dev-'):
                continue
            procedural = re.search(r'case "' + re.escape(style) + r'":(?:case "[a-z-]+":)*return "audio/intros/([a-z-]+)"', rules)
            name = procedural[1] if procedural else style
            self.assertTrue((stings / (name + '.mp3')).is_file(), style + ' -> ' + name)

    def test_installs_intro_music_with_alternates(self):
        tree = IntroTree(self.tmp / 'licensed')
        code, out = self.run_installer(str(tree.base))
        self.assertEqual(code, 0, out)
        self.assertIn('5 keys, 7 clips', out)
        for name in ('pick', 'alt-a', 'alt-b'):
            self.assertTrue((self.installed() / 'intro.weapon-sword' / (name + '.ogg.meta')).is_file())
        self.assertTrue((self.installed() / 'intro.blackout/0.ogg').is_file())

    def test_rejects_bad_intro_music(self):
        cases = {
            'longer than the entrance': lambda t: t.clip('intro.weapon-sword', 'alt-a', ogg(5.0, 2)),
            'wrong rate': lambda t: t.clip('intro.weapon-sword', 'alt-a', ogg(4.8, 2, 48000)),
            'positional space': lambda t: (t.manifest['events']['intro.blackout'].__setitem__('space', '3d'), t.save()),
            'DEV entrance key': lambda t: (t.manifest['events'].__setitem__('intro.dev-siren', {'clips': []}), t.save()),
            'maxLength over the cap': lambda t: (t.manifest['events']['intro.blackout'].__setitem__('maxLength', 5.5), t.save()),
            'unlisted alternate': lambda t: (t.bank / 'intro.weapon-sword/extra.ogg').write_bytes(ogg(4.8, 2)),
        }
        for name, change in cases.items():
            with self.subTest(name=name):
                tree = IntroTree(self.tmp / ('bad-' + name.replace(' ', '-')))
                change(tree)
                code, out = self.run_installer(str(tree.base), '--check')
                self.assertEqual(code, 1, name + ': ' + out)
                self.assertFalse(self.installed().exists())


if __name__ == '__main__':
    unittest.main()
