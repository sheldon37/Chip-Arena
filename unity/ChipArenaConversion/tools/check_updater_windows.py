"""Exercise the real .NET Framework launcher with child processes and file replacement on Windows."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import tempfile
import time
import uuid
import zipfile
import zlib

PROJECT = Path(__file__).resolve().parents[1]
EXE = 'Chip Arena.exe'
DATA = 'Chip Arena_Data'
LAUNCHER = 'Chip Arena.exe'


def wait_for(path, process=None):
    deadline = time.monotonic() + 40
    while time.monotonic() < deadline:
        if path.exists():
            return
        if process is not None and process.poll() is not None:
            raise RuntimeError(f'Process exited ({process.returncode}) before {path}')
        time.sleep(.05)
    raise TimeoutError(str(path))


def identity(char, revision):
    return dict(schema=1, product='chip-arena-unity', channel='native-test', platform='windows-x64', version=char * 64, revision=revision, bootstrap=2)


def fixture_icon(path):
    def chunk(name, data):
        return struct.pack('>I', len(data)) + name + data + struct.pack('>I', zlib.crc32(name + data))
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', 256, 256, 8, 6, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress((b'\0' + bytes([30, 180, 90, 255]) * 256) * 256)) + chunk(b'IEND', b'')
    path.write_bytes(struct.pack('<HHHBBBBHHII', 0, 1, 1, 0, 0, 0, 0, 1, 32, len(png), 22) + png)


def run():
    if os.name != 'nt':
        raise SystemExit('This integration gate requires Windows.')
    compiler = Path(os.environ['WINDIR']) / 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    refs = ['/r:System.Runtime.Serialization.dll', '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll', '/r:System.Windows.Forms.dll']
    common = [str(PROJECT / 'Assets/ChipArena/Core/NativeUpdateRules.cs'), str(PROJECT / 'Updater/UpdateStorage.cs')]
    with tempfile.TemporaryDirectory(prefix='chip updater spaces ') as temporary:
        base = Path(temporary)
        source = base / 'Unity project with spaces'
        for relative in ['Updater/Launcher.cs', 'Updater/UpdateStorage.cs', 'Updater/UpdateInstaller.cs',
                         'Updater/Launcher.exe.config', 'Assets/ChipArena/Core/NativeUpdateRules.cs']:
            target = source / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(PROJECT / relative, target)
        icon = base / 'launcher icon.ico'
        fixture_icon(icon)
        driver = base / 'compiler driver.exe'
        subprocess.run([str(compiler), '/nologo', '/warnaserror+', '/target:exe', '/out:' + str(driver), *refs,
                        str(PROJECT / 'Assets/ChipArena/Core/NativeUpdateRules.cs'),
                        str(PROJECT / 'Assets/ChipArena/Editor/NativeLauncherCompiler.cs'),
                        str(PROJECT / 'Updater/LauncherBuildFixture.cs')], check=True)
        destination = base / 'packaged Windows'
        # Unity supplies slash-separated paths; exercise mixed separators, spaces, response file and icon.
        subprocess.run([str(driver), source.as_posix() + '\\.', destination.as_posix(), icon.as_posix()], cwd=base, check=True)
        launcher = destination / LAUNCHER
        assert launcher.is_file() and (destination / (LAUNCHER + '.config')).is_file()
        print('PASS Windows packaging: exact Editor compiler step with mixed separators, spaces and icon')
        missing = source / 'Updater/UpdateInstaller.cs'
        missing.unlink()
        rejected = subprocess.run([str(driver), str(source), str(base / 'incomplete output'), str(icon)], capture_output=True, text=True)
        assert rejected.returncode != 0 and 'Native launcher source is missing' in (rejected.stdout + rejected.stderr)
        assert not (base / 'incomplete output' / LAUNCHER).exists()
        print('PASS Windows packaging: missing source rejected before compiler invocation')
        fixture = base / 'fixture.exe'
        subprocess.run([str(compiler), '/nologo', '/warnaserror+', '/target:exe', '/out:' + str(fixture), *refs, *common,
                        str(PROJECT / 'Updater/WindowsFixture.cs')], check=True)
        for launcher_name, fails in ((LAUNCHER, False), (LAUNCHER, True), (EXE, False), (EXE, True)):
            root = base / (launcher_name + (' rollback case' if fails else ' install case'))
            app = root / 'app'
            work = root / '.native-update'
            app.mkdir(parents=True)
            work.mkdir()
            shutil.copy2(launcher, root / launcher_name)
            shutil.copy2(PROJECT / 'Updater/Launcher.exe.config', root / (launcher_name + '.config'))
            (root / 'native-install.txt').write_text('chip-arena-unity/1')
            (root / 'settings-sentinel').write_text('preserve')
            for player, build in ((app, identity('a', 1)), (base / (launcher_name + (' new-bad' if fails else ' new-good')), identity('b', 2))):
                player.mkdir(exist_ok=True)
                (player / DATA).mkdir()
                shutil.copy2(fixture, player / EXE)
                (player / 'UnityPlayer.dll').write_bytes(b'test fixture only')
                (player / DATA / 'globalgamemanagers').write_bytes(b'fixture')
                (player / 'native-build.json').write_text(json.dumps(build))
            environment = dict(os.environ, CHIP_UPDATER_TEST_NO_DIALOG='1')
            launched = subprocess.run([str(root / launcher_name)], cwd=base, env=environment, timeout=20)
            assert launched.returncode == 0
            wait_for(work / 'normal-start.txt')
            # .NET may expand the runner's DOS 8.3 temp path; compare directory identity.
            assert os.path.samefile((work / 'normal-start.txt').read_text(), app)
            print('PASS Windows normal launch from another working directory: ' + launcher_name)
            new = base / (launcher_name + (' new-bad' if fails else ' new-good'))
            if fails:
                (new / 'fail-start.txt').write_text('Fail before startup acknowledgement')
            (app / 'obsolete.dll').write_text('old version only')
            token = uuid.uuid4().hex
            transaction = work / token
            transaction.mkdir()
            archive = transaction / 'download.zip'
            files = [p for p in new.rglob('*') if p.is_file()]
            with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
                for path in files:
                    z.write(path, path.relative_to(new).as_posix())
            payload = archive.read_bytes()
            digest = hashlib.sha256(payload).hexdigest()
            parent = subprocess.Popen([str(app / EXE), '--hold'])
            try:
                wait_for(work / 'fixture-parent.json', parent)
                process = json.loads((work / 'fixture-parent.json').read_text())
                manifest = dict(schema=1, build=identity('b', 2), sha256=digest, bytes=len(payload), unpackedBytes=sum(p.stat().st_size for p in files), files=len(files),
                                parts=[dict(name=digest + '-0000.part', bytes=len(payload), sha256=digest)])
                (transaction / 'request.json').write_text(json.dumps(dict(token=token, manifest=manifest, parentPid=process['pid'], parentStartedUtcTicks=process['ticks'])))
                environment = dict(os.environ, CHIP_UPDATER_TEST_NO_DIALOG='1')
                updater = subprocess.Popen([str(root / launcher_name), '--install', token], env=environment)
                try:
                    wait_for(transaction / 'ready.txt', updater)
                    assert (app / 'obsolete.dll').exists(), 'Updater changed files while the requesting game still ran'
                    (work / 'close-parent').write_text('close')
                    parent.wait(timeout=10)
                    code = updater.wait(timeout=30)
                    assert code == (1 if fails else 0), (code, (work / 'updater.log').read_text())
                    installed = json.loads((app / 'native-build.json').read_text())
                    assert installed['version'] == ('a' if fails else 'b') * 64
                    assert (app / 'obsolete.dll').exists() == fails
                    assert (root / 'settings-sentinel').read_text() == 'preserve'
                    assert not (work / 'journal.json').exists()
                    print('PASS Windows launcher ' + launcher_name + ': ' + ('startup failure rollback' if fails else 'wait, install, restart and acknowledgement'))
                finally:
                    if updater.poll() is None:
                        updater.kill()
                        updater.wait()
            finally:
                if parent.poll() is None:
                    parent.kill()
                    parent.wait()
                time.sleep(2)  # Fixture child exits; release Windows handles even when an assertion fails.

        # Simulate power loss after app -> previous, before the new player directory moves in.
        app.rename(work / 'previous')
        (work / 'journal.json').write_text(json.dumps(dict(token=token, phase='old-moved', before=identity('a', 1), after=identity('b', 2))))
        try:
            recovered = subprocess.run([str(root / launcher_name)], env=environment, timeout=20)
            assert recovered.returncode == 0, (work / 'updater.log').read_text()
            assert json.loads((app / 'native-build.json').read_text())['version'] == 'a' * 64
            assert not (work / 'journal.json').exists()
            print('PASS Windows launcher: normal launch recovers an interrupted directory swap')
        finally:
            time.sleep(2)


if __name__ == '__main__':
    run()
