"""Run source checks through a real local relay; no deployed service or account is used."""
import argparse
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import urllib.request

root = Path(__file__).resolve().parents[1]
def positive_seconds(value):
    try:
        seconds = int(value)
    except (TypeError, ValueError):
        raise argparse.ArgumentTypeError('test timeout must be a positive integer in seconds')
    if seconds <= 0:
        raise argparse.ArgumentTypeError('test timeout must be a positive integer in seconds')
    return seconds

parser = argparse.ArgumentParser()
parser.add_argument('--dotnet', default='dotnet')
parser.add_argument('--compiled-relay')
parser.add_argument('--compiled-tests')
parser.add_argument('--test-timeout', type=positive_seconds,
                    default=os.environ.get('CHIP_TEST_TIMEOUT_SECONDS', '300'),
                    help='test process timeout in seconds (default: CHIP_TEST_TIMEOUT_SECONDS or 300)')
args = parser.parse_args()
for project, compiled in [('Relay/ChipArena.Relay.csproj', args.compiled_relay), ('Tests/CoreTests.csproj', args.compiled_tests)]:
    if not compiled:
        subprocess.run([args.dotnet, 'build', str(root / project), '-c', 'Release', '--nologo'], cwd=root, check=True)
relay = args.compiled_relay or str(root / 'Relay/bin/Release/net8.0/ChipArena.Relay.dll')
tests = args.compiled_tests or str(root / 'Tests/bin/Release/net8.0/CoreTests.dll')
with socket.socket() as reserve:
    reserve.bind(('127.0.0.1', 0))
    port = reserve.getsockname()[1]
with tempfile.TemporaryFile(mode='w+') as log:
    server = subprocess.Popen([args.dotnet, relay, '--urls', f'http://127.0.0.1:{port}'], cwd=root, stdout=log, stderr=log)
    try:
        for attempt in range(200):
            try:
                with urllib.request.urlopen(f'http://127.0.0.1:{port}/health', timeout=.3):
                    break
            except OSError:
                if server.poll() is not None:
                    raise RuntimeError('Relay exited before becoming ready')
                time.sleep(.05)
        else:
            raise RuntimeError('Relay startup timed out')
        env = os.environ.copy()
        env['CHIP_TEST_RELAY'] = f'ws://127.0.0.1:{port}'
        result = subprocess.run([args.dotnet, tests], cwd=root, env=env, timeout=args.test_timeout)
        raise SystemExit(result.returncode)
    finally:
        server.terminate()
        try:
            server.wait(timeout=5)
        except subprocess.TimeoutExpired:
            server.kill()
            server.wait(timeout=5)
