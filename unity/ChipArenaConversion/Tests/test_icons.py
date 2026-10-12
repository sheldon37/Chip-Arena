"""Validate the exported source icon set (client/icons.generated.ts + icon-map.ts) the native menus and cards draw, and
the game's own class and weapon art (ClassIcons.json, tools/export_class_icons.mjs). The Lucide parity checks (pinned
source commit, ISC notice, browser IoU) cover Icons.json only: the class and weapon art is original and lives in its own
file, so it is exempt from them by construction."""
import json, re, shutil, subprocess, unittest
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
class ClassIconChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        res=ROOT/'Assets/Conversion/Resources'
        cls.art=json.loads((res/'ClassIcons.json').read_text())
        cls.lucide=json.loads((res/'Icons.json').read_text())
        cls.content=json.loads((res/'GameContent.json').read_text())
        cls.icons={i['id']:i for i in cls.art['icons']}
    def test_original_art_with_its_own_metadata(self):
        self.assertEqual(self.art['schema'],1)
        self.assertEqual(self.art['source'],'tools/export_class_icons.mjs')
        self.assertIn('Original Chip Arena',self.art['note'])
        self.assertEqual((self.art['box'],self.art['smallPx']),(24,22))
        self.assertTrue((ROOT/'Assets/Conversion/Resources/ClassIcons.json.meta').is_file())
    def test_every_class_and_weapon_has_its_own_icon(self):
        classes={p['id']:p['icon'] for p in self.art['classes']}
        specs={p['id']:p['icon'] for p in self.art['specs']}
        self.assertEqual(set(classes),{'dev','magic','melee','ranged'})
        # The Bow's art ships ahead of its content entry (docs/NEW-KITS.md section 2; the rules are Astra's).
        weapons={w['id'] for w in self.content['weapons']}
        self.assertEqual(set(specs),weapons|({'bow'} if 'bow' not in weapons else set()))
        for icon in [*classes.values(),*specs.values(),self.art['fallback']]:
            self.assertIn(icon,self.icons)
        self.assertEqual(len(set(specs.values())),len(specs))
        self.assertEqual(len(set(classes.values())),len(classes))
        self.assertFalse(set(classes.values())&set(specs.values()))
        self.assertEqual(self.art['fallback'],classes['dev'])
    def test_exempt_from_lucide_parity(self):
        lucide_ids={i['id'] for i in self.lucide['icons']}
        lucide_markup={i['markup'] for i in self.lucide['icons']}
        for id,icon in self.icons.items():
            with self.subTest(icon=id):
                self.assertNotIn(id,lucide_ids)
                self.assertNotIn(icon['markup'],lucide_markup)
        # The source maps stay in Icons.json untouched (their parity with icon-map.ts is unchanged); the art overrides them at
        # runtime in NativeIcons.Class / Spec.
        self.assertEqual({p['id']:p['icon'] for p in self.lucide['classes']}['ranged'],'crosshair')
    def test_markup_uses_the_art_subset(self):
        allowed={'d','fill','fill-rule','stroke','stroke-width','stroke-linejoin','stroke-linecap','opacity','data-knockout'}
        for id,icon in self.icons.items():
            for variant in ('markup','small'):
                markup=icon.get(variant)
                if markup is None: continue
                with self.subTest(icon=id,variant=variant):
                    self.assertEqual(set(re.findall(r'<(\w+)',markup)),{'path'})
                    self.assertTrue(set(re.findall(r'([\w:-]+)="',markup))<=allowed)
                    self.assertTrue(set(re.findall(r'(?:fill|stroke)="([^"]*)"',markup))<={'#fff','none'})
                    for d in re.findall(r' d="([^"]*)"',markup):
                        self.assertRegex(d,r'^[MLQCAZ0-9.\- ]+$')
                    self.assertTrue(any('data-knockout' not in p for p in re.findall(r'<path\b[^>]*/>',markup)))
    @unittest.skipUnless(shutil.which('node'),'node is not installed')
    def test_generated_file_is_current(self):
        run=subprocess.run(['node',str(ROOT/'tools/export_class_icons.mjs'),'--check'],capture_output=True,text=True)
        self.assertEqual(run.returncode,0,run.stderr+run.stdout)
if __name__=='__main__': unittest.main()
