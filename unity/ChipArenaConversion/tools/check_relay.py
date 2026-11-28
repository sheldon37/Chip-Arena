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
parser.add_argument('--startup-timeout', type=positive_seconds,
                    default=os.environ.get('CHIP_RELAY_STARTUP_SECONDS', '60'),
                    help='relay startup timeout in seconds (default: CHIP_RELAY_STARTUP_SECONDS or 60)')
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
        # A deadline, not a poll count: a refused connection returns at once, so 200 polls were only ~10 s, which a busy
        # CI runner (the compiler server still running) can exceed before the relay listens.
        deadline = time.monotonic() + args.startup_timeout
        while True:
            try:
                with urllib.request.urlopen(f'http://127.0.0.1:{port}/health', timeout=.3):
                    break
            except OSError:
                if server.poll() is not None:
                    raise RuntimeError('Relay exited before becoming ready')
                if time.monotonic() >= deadline:
                    raise RuntimeError(f'Relay startup timed out after {args.startup_timeout:g} s')
                time.sleep(.05)
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
