"""Every footfall key of the licensed bank holds takes of one recording series of its own surface (owner 2026-10-16: walking on
one surface cycled through sounds of different terrains; docs/ASSET-CREDITS.md, Footstep surfaces)."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
CREDITS = ROOT.parents[1] / 'docs/ASSET-CREDITS.md'
CORE = ROOT / 'Assets/ChipArena/Core'

# The surface folder (or pack folder and series stem) each key's takes must come from.
SURFACE = {
    'move.step.concrete': '_Concrete_Footsteps', 'move.step.metal': '_Metal_Footsteps', 'move.step.wood': '_Wood_Footsteps',
    'move.step.gravel': '_Gravel_Footsteps', 'move.step.snow': '_Snow_Footsteps', 'move.step.stone': 'Fantasy_Game_Footstep_Stone_Medium',
    'pve.zombie.step.shuffle': 'Asphalt_Trainers_Walk_Slow', 'pve.zombie.step.run': 'Trainers_Asphalt_Run',
    'pve.zombie.step.heavy': 'Fantasy_Game_Footstep_Stone_Heavy', 'pve.zombie.step.crawl': 'FRICTION_Deep_Body_Drag',
}
# Other surfaces' folders and series that must never appear in a key that is not theirs.
FOREIGN = ('_Stones_Footsteps', '_Rock_Footsteps', '_Dirt_Footsteps', '_Grass_Footsteps', '_Mud_Footsteps', '_Water_Footsteps')


def series(path):
    """A source's recording series: the file name without its take (_RR03, _A) and channel suffix."""
    name = path.rsplit('/', 1)[-1]
    name = re.sub(r'(_mono|_stereo)?\.wav$', '', name)
    return re.sub(r'(_RR\d+|_[A-J]|_\d\d)$', '', name)


def footstep_rows(text):
    rows = {}
    for line in text.splitlines():
        m = re.match(r'^\| `((?:move\.step|pve\.zombie\.step)\.[a-z]+)` \|(.*)\|\s*$', line)
        if not m:
            continue
        cells = [c.strip() for c in m.group(2).split('|')]
        sources = re.findall(r'`([^`]+\.wav)`', cells[-1])
        rows.setdefault(m.group(1), []).append(sources)
    return rows


class FootstepCreditsTests(unittest.TestCase):
    def setUp(self):
        if not CREDITS.is_file():
            self.skipTest('docs/ASSET-CREDITS.md is not in this tree')
        self.rows = footstep_rows(CREDITS.read_text(encoding='utf-8'))

    def test_every_footfall_key_is_listed_once(self):
        contract = set(re.findall(r'K\("((?:move\.step|pve\.zombie\.step)\.[a-z]+)"', (CORE / 'NativeSoundBank.cs').read_text(encoding='utf-8')))
        self.assertEqual(contract, set(SURFACE), 'the bank contract and this test name the same footfall keys')
        for key in SURFACE:
            self.assertEqual(len(self.rows.get(key, [])), 1, f'{key} is listed exactly once in ASSET-CREDITS.md')

    def test_one_surface_folder_and_one_series_per_key(self):
        for key, want in SURFACE.items():
            sources = self.rows[key][0]
            self.assertTrue(4 <= len(sources) <= 8, f'{key}: 4 to 8 takes ({len(sources)})')
            self.assertEqual(len(set(sources)), len(sources), f'{key}: no take twice')
            folders = {s.rsplit('/', 1)[0] for s in sources}
            self.assertEqual(len(folders), 1, f'{key}: one source folder ({folders})')
            names = {series(s) for s in sources}
            self.assertEqual(len(names), 1, f'{key}: one recording series ({names})')
            self.assertTrue(all(want in s for s in sources), f'{key}: every take from {want}')
            for other in FOREIGN:
                self.assertFalse(any(other in s for s in sources), f'{key}: no take from {other}')
            for other_key, other in SURFACE.items():
                if other_key != key and other != want:
                    self.assertFalse(any(f'/{other}/' in s or f'/{other}_' in s for s in sources), f'{key}: no take from {other_key}\'s {other}')

    def test_walk_and_run_never_share_a_key(self):
        # Walking and sprinting on one surface play one key (the gait only changes rate and gain), so its takes are one gait's.
        for key in SURFACE:
            gaits = {g for s in self.rows[key][0] for g in ('_Walk_', '_Run_', '_Sneak_') if g in s.rsplit('/', 1)[-1]}
            self.assertLessEqual(len(gaits), 1, f'{key}: one gait\'s takes ({gaits})')


if __name__ == '__main__':
    unittest.main()
