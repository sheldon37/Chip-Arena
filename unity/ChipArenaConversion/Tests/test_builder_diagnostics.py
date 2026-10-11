"""Useful build reports from real Unity error formats, without a Unity installation."""
from contextlib import redirect_stdout
import io
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

PROJECT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PROJECT / 'Builder'))
sys.path.insert(0, str(PROJECT / 'tools'))
import builder
import package_builder


class DiagnosticTests(unittest.TestCase):
    def test_native_failures_include_cause_and_stack_instead_of_license_noise(self):
        for cause in ('MissingReferenceException: The object of type Rigidbody has been destroyed.',
                      'Exception: Native locomotion: stepped through ceiling',
                      'Exception: Native DEV combat: held E resumes after hook'):
            with self.subTest(cause=cause):
                log = ('\ufeff[Licensing] Error: signature check failed (ignored)\r\n'
                       'PASS native locomotion\r\n' + cause + '\r\n'
                       '  at ChipArena.NativeArena.Validate() in Assets/Check.cs:45\r\n\r\n'
                       'executeMethod method threw exception.\r\nExiting with return code 1')
                result = builder.unity_errors(log)
                self.assertIn(cause, result)
                self.assertIn('Assets/Check.cs:45', result)
                self.assertNotIn('[Licensing]', result)
                self.assertNotIn('executeMethod', result)

    def test_compiler_errors_beat_generic_build_failure(self):
        log = ('Assets/Pilot.cs(12,7): error CS1002: ; expected\n'
               'Assets/Map.cs(15,9): error CS0103: unknown identifier\n'
               'BuildFailedException: Scripts have compiler errors\n')
        result = builder.unity_errors(log)
        self.assertIn('error CS1002', result)
        self.assertIn('error CS0103', result)
        self.assertNotIn('BuildFailedException', result)

    def test_unknown_failure_gets_bounded_tail_and_fatal_marker_is_kept(self):
        tail = builder.unity_errors('\n'.join('progress ' + str(i) for i in range(10000)))
        self.assertIn('progress 9999', tail)
        self.assertNotIn('progress 9950', tail)
        self.assertLess(len(tail), 13000)
        self.assertIn('Aborting batchmode', builder.unity_errors('startup\nAborting batchmode due to failure:\n  No valid license'))

    def test_report_keeps_unicode_and_full_log_when_notepad_fails(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            project = root / 'Project with spaces'
            (project / 'Builds').mkdir(parents=True)
            log = project / 'Builds/build.log'
            log.write_bytes('\ufeffPASS maps\r\nException: problème λ\r\n  at Check()\r\n'.encode('utf-8'))
            attempt = dict(status='failed', at='2026-10-11T19:21:00Z', sourceCommit='abc', stage='Unity build')
            console = io.StringIO()
            with patch.object(builder, 'open_error_report', side_effect=OSError('missing Notepad')), redirect_stdout(console):
                builder.build_diagnostics(root, attempt, project, ValueError('build failed'))
            self.assertEqual((root / 'Latest-Build.log').read_bytes(), log.read_bytes())
            self.assertIn('problème λ', (root / 'Latest-Build-Error.txt').read_text(encoding='utf-8'))
            self.assertIn('problème λ', console.getvalue())
            self.assertIn('Could not open Notepad', console.getvalue())

    def test_diagnostic_write_failure_does_not_replace_build_error(self):
        with tempfile.TemporaryDirectory() as temporary, \
                patch.object(builder, 'diagnostic_file', side_effect=PermissionError('file is locked')):
            builder.build_diagnostics(Path(temporary), dict(status='failed', at='now'), error=ValueError('original'))

    def test_packaged_kit_contains_log_button_and_no_work_or_logs(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'Chip-Arena-Builder-v3.zip'
            package_builder.package(path)
            with zipfile.ZipFile(path) as archive:
                names = archive.namelist()
                self.assertEqual(len(names), 9)
                self.assertIn('Chip Arena Builder/Open-Latest-Log.cmd', names)
                self.assertIn(b'Latest-Build-Error.txt', archive.read('Chip Arena Builder/builder.py'))
                self.assertFalse(any('/Work/' in name or '/Cache/' in name or name.endswith('.log') for name in names))
                self.assertIsNone(archive.testzip())


if __name__ == '__main__':
    unittest.main()
