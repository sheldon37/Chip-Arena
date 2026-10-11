"""Regress the package-resolution failures observed in the owner's Windows build log."""
from pathlib import Path
import json
import sys
import unittest
import tempfile
import zipfile
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
import package_source
import package_builder
from package_source import validate_dependencies


class SourcePackageTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = json.loads((ROOT / 'tools/builtin-packages-6000.3.24f1.json').read_text())
        cls.dependencies = json.loads((ROOT / 'Packages/manifest.json').read_text())['dependencies']

    def test_licensed_inputs_excluded_from_source_and_builder_archives(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            files = ['Assets/keep.txt', 'Assets/LocalAssets/synthetic.txt',
                     'Assets/LocalAssets.meta', 'tools/LocalAssets.zip',
                     'tools/LocalAssets.zip.meta', 'Builder/LocalAssets/synthetic.txt',
                     'Builder/LocalAssets.zip']
            for name in files:
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text('synthetic packaging sentinel')
            with patch.object(package_source, 'ROOT', root):
                self.assertEqual([rel.as_posix() for _, rel in package_source.selected()], ['Assets/keep.txt'])
            for name in package_builder.FILES:
                (root / 'Builder' / name).write_text('helper fixture')
            destination = root / 'builder.zip'
            with patch.object(package_builder, 'PROJECT', root):
                package_builder.package(destination)
            with zipfile.ZipFile(destination) as archive:
                self.assertEqual(len(archive.namelist()), len(package_builder.FILES))
                self.assertFalse(any('LocalAssets' in name for name in archive.namelist()))

    def test_current_dependencies_exist_in_official_editor_catalog(self):
        validate_dependencies(self.dependencies, self.catalog)

    def test_windows_log_failures_are_rejected(self):
        for module in ('inputlegacy', 'physicscore2d', 'tetgen', 'timelinefoundation'):
            with self.subTest(module=module):
                dependencies = dict(self.dependencies)
                dependencies['com.unity.modules.' + module] = '1.0.0'
                with self.assertRaisesRegex(ValueError, 'Unsupported dependency'):
                    validate_dependencies(dependencies, self.catalog)

    def test_unavailable_version_is_rejected(self):
        dependencies = dict(self.dependencies)
        dependencies['com.unity.modules.physics'] = '99.0.0'
        with self.assertRaisesRegex(ValueError, 'Unsupported dependency'):
            validate_dependencies(dependencies, self.catalog)

    def test_editor_ability_ceiling_validation_matches_runtime_events(self):
        # UBA #32 passed managed source CI but its real PreExport failed here when the
        # ability-shot emitter was intentionally removed and this Editor-only assertion
        # still required a hologram. Keep both sides of the contract synchronized.
        ability = (ROOT / 'Assets/ChipArena/Runtime/NativeAbilities.cs').read_text()
        editor = (ROOT / 'Assets/ChipArena/Runtime/NativeFollowupValidation.cs').read_text()
        self.assertNotIn('Emit(EventKind.CeilingHit', ability)
        self.assertIn('int ceilingHitsBefore=Events.Count(e=>e.Kind==EventKind.CeilingHit);', editor)
        self.assertIn('AbilityShots.Count==0&&Events.Count(e=>e.Kind==EventKind.CeilingHit)==ceilingHitsBefore', editor)

    def test_empty_manifest_is_rejected(self):
        with self.assertRaises(ValueError):
            validate_dependencies({}, self.catalog)


if __name__ == '__main__':
    unittest.main()
