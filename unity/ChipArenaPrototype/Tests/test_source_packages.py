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

    def test_empty_manifest_is_rejected(self):
        with self.assertRaises(ValueError):
            validate_dependencies({}, self.catalog)


if __name__ == '__main__':
    unittest.main()
