"""Validate the exported source icon set (client/icons.generated.ts + icon-map.ts) the native menus and cards draw."""
import json, re, unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
class IconChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data=json.loads((ROOT/'Assets/Conversion/Resources/Icons.json').read_text())
        cls.icons={i['id']:i['markup'] for i in cls.data['icons']}
    def test_pinned_and_licensed(self):
        self.assertEqual(self.data['commit'],'05950eaef80ff2483a9c44d898cad98be1a76bbc')
        self.assertIn('ISC License',self.data['license'])
        self.assertIn('Lucide',self.data['license'])
        self.assertEqual(self.data['strokeWidth'],1.7)
    def test_every_mapping_resolves(self):
        self.assertIn('cpu',self.icons)  # icons.ts falls back to cpu
        for group in ('chips','classes','specs','actions'):
            for pair in self.data[group]:
                with self.subTest(group=group,id=pair['id']):
                    self.assertIn(pair['icon'],self.icons)
    def test_home_menu_actions_present(self):
        actions={p['id']:p['icon'] for p in self.data['actions']}
        for action in ('practice','create','join','sandbox','customize','settings','account','update','test-boss','resume','leave','home'):
            self.assertIn(action,actions)
    def test_markup_uses_the_supported_svg_subset(self):
        for id,markup in self.icons.items():
            with self.subTest(icon=id):
                tags=set(re.findall(r'<(\w+)',markup))
                self.assertTrue(tags and tags<={'path','circle','ellipse','rect','line','polyline','polygon'},tags)
                for d in re.findall(r' d="([^"]*)"',markup):
                    self.assertRegex(d,r'^[MmLlHhVvCcSsQqTtAaZz0-9eE.,+\- ]+$')
if __name__=='__main__': unittest.main()
