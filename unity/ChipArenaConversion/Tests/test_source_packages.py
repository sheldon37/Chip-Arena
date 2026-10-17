"""Regress the package-resolution failures observed in the owner's Windows build log."""
from pathlib import Path
import json
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
from package_source import validate_dependencies


class SourcePackageTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = json.loads((ROOT / 'tools/builtin-packages-6000.3.24f1.json').read_text())
        cls.dependencies = json.loads((ROOT / 'Packages/manifest.json').read_text())['dependencies']

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
