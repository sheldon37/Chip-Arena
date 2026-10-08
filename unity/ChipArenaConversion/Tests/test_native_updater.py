"""Release-channel validation uses real ZIP payloads; never publishes to a remote."""
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

PROJECT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('publish_native_update', PROJECT / 'tools/publish_native_update.py')
publisher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(publisher)


class NativePublisherTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.feed = self.root / 'feed'
        (self.feed / 'objects').mkdir(parents=True)
        self.channel = self.root / 'channel'

    def payload(self, char='b', revision=2, extra=None, embedded=None):
        build = dict(schema=1, product='chip-arena-unity', channel='native-test', platform='windows-x64', version=char * 64, revision=revision, bootstrap=1)
        files = {'native-build.json': json.dumps(embedded or build).encode(),
                 'ChipArena-Native-Conversion-TEST.exe': b'fixture', 'UnityPlayer.dll': b'fixture',
                 'ChipArena-Native-Conversion-TEST_Data/globalgamemanagers': b'fixture'}
        if extra:
            files.update(extra)
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, 'w', zipfile.ZIP_DEFLATED) as z:
            for name, data in files.items():
                z.writestr(name, data)
        data = stream.getvalue()
        digest = hashlib.sha256(data).hexdigest()
        part = dict(name=digest + '-0000.part', sha256=digest, bytes=len(data))
        m = dict(schema=1, build=build, sha256=digest, bytes=len(data), unpackedBytes=sum(map(len, files.values())), files=len(files), parts=[part])
        (self.feed / 'objects' / part['name']).write_bytes(data)
        self.manifest(m)
        return m

    def manifest(self, m):
        (self.feed / 'latest.json').write_text(json.dumps(m))

    def test_stage_preserves_electron_and_previous_native_objects(self):
        (self.channel / 'feed').mkdir(parents=True)
        electron = self.channel / 'feed/latest.json'
        electron.write_text('untouched Electron feed')
        first = self.payload('a', 1)
        publisher.stage(self.feed, self.channel)
        second = self.payload('b', 2)
        publisher.stage(self.feed, self.channel)
        native = self.channel / 'native/windows-x64'
        self.assertTrue((native / 'objects' / first['parts'][0]['name']).is_file())
        self.assertEqual(json.loads((native / 'latest.json').read_text()), second)
        self.assertEqual(electron.read_text(), 'untouched Electron feed')

    def test_reject_same_or_older_publication(self):
        self.payload('b', 2)
        publisher.stage(self.feed, self.channel)
        for char, revision in [('b', 3), ('c', 1)]:
            with self.subTest(char=char):
                self.payload(char, revision)
                with self.assertRaises(ValueError):
                    publisher.stage(self.feed, self.channel)

    def test_reject_tampering_before_staging(self):
        m = self.payload()
        part = self.feed / 'objects' / m['parts'][0]['name']
        data = bytearray(part.read_bytes())
        data[-1] ^= 1
        part.write_bytes(data)
        with self.assertRaisesRegex(ValueError, 'checksum'):
            publisher.stage(self.feed, self.channel)
        self.assertFalse(self.channel.exists())

    def test_reject_unsafe_archive_paths(self):
        for name in ('../escape', '/absolute', 'C:/escape', 'CON.dll', 'bad\\path', 'UnityPlayer.DLL', 'trailing. '):
            with self.subTest(name=name):
                self.payload(extra={name: b'bad'})
                with self.assertRaises(ValueError):
                    publisher.validate(self.feed)

    def test_reject_wrong_channel_and_embedded_identity(self):
        m = self.payload()
        m['build']['channel'] = 'live'
        self.manifest(m)
        with self.assertRaises(ValueError):
            publisher.validate(self.feed)
        self.payload(embedded=m['build'])
        with self.assertRaisesRegex(ValueError, 'Embedded'):
            publisher.validate(self.feed)

    def test_reject_part_path_and_declared_sizes(self):
        for field, value in [('name', '../outside'), ('bytes', 1)]:
            m = self.payload()
            m['parts'][0][field] = value
            self.manifest(m)
            with self.assertRaises(ValueError):
                publisher.validate(self.feed)


if __name__ == '__main__':
    unittest.main()
