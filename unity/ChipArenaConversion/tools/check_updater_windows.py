"""Exercise the real .NET Framework launcher with child processes and file replacement on Windows."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import time
import uuid
import zipfile

PROJECT = Path(__file__).resolve().parents[1]
EXE = 'ChipArena-Native-Conversion-TEST.exe'
DATA = 'ChipArena-Native-Conversion-TEST_Data'


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
    return dict(schema=1, product='chip-arena-unity', channel='native-test', platform='windows-x64', version=char * 64, revision=revision, bootstrap=1)


def run():
    if os.name != 'nt':
        raise SystemExit('This integration gate requires Windows.')
    compiler = Path(os.environ['WINDIR']) / 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    refs = ['/r:System.Runtime.Serialization.dll', '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll', '/r:System.Windows.Forms.dll']
    common = [str(PROJECT / 'Assets/ChipArena/Core/NativeUpdateRules.cs'), str(PROJECT / 'Updater/UpdateStorage.cs')]
    with tempfile.TemporaryDirectory(prefix='chip updater spaces ') as temporary:
        base = Path(temporary)
        launcher = base / 'launcher.exe'
        fixture = base / 'fixture.exe'
        subprocess.run([str(compiler), '/nologo', '/warnaserror+', '/target:winexe', '/out:' + str(launcher), *refs, *common,
                        str(PROJECT / 'Updater/UpdateInstaller.cs'), str(PROJECT / 'Updater/Launcher.cs')], check=True)
        subprocess.run([str(compiler), '/nologo', '/warnaserror+', '/target:exe', '/out:' + str(fixture), *refs, *common,
                        str(PROJECT / 'Updater/WindowsFixture.cs')], check=True)
        for fails in (False, True):
            root = base / ('rollback case' if fails else 'install case')
            app = root / 'app'
            work = root / '.native-update'
            app.mkdir(parents=True)
            work.mkdir()
            shutil.copy2(launcher, root / EXE)
            shutil.copy2(PROJECT / 'Updater/Launcher.exe.config', root / (EXE + '.config'))
            (root / 'native-install.txt').write_text('chip-arena-unity/1')
            (root / 'settings-sentinel').write_text('preserve')
            for player, build in ((app, identity('a', 1)), (base / ('new-bad' if fails else 'new-good'), identity('b', 2))):
                player.mkdir(exist_ok=True)
                (player / DATA).mkdir()
                shutil.copy2(fixture, player / EXE)
                (player / 'UnityPlayer.dll').write_bytes(b'test fixture only')
                (player / DATA / 'globalgamemanagers').write_bytes(b'fixture')
                (player / 'native-build.json').write_text(json.dumps(build))
            new = base / ('new-bad' if fails else 'new-good')
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
                updater = subprocess.Popen([str(root / EXE), '--install', token], env=environment)
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
                    print('PASS Windows launcher: ' + ('startup failure rollback' if fails else 'wait, install, restart and acknowledgement'))
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
            recovered = subprocess.run([str(root / EXE)], env=environment, timeout=20)
            assert recovered.returncode == 0, (work / 'updater.log').read_text()
            assert json.loads((app / 'native-build.json').read_text())['version'] == 'a' * 64
            assert not (work / 'journal.json').exists()
            print('PASS Windows launcher: normal launch recovers an interrupted directory swap')
        finally:
            time.sleep(2)


if __name__ == '__main__':
    run()
