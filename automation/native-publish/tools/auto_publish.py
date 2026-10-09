"""Retrieve and verify UBA deliveries; publish only the current native-test candidate.

No downloaded source or player is executed. Unity access is GET-only. Publication
uses the separately configured channel checkout, never Unity build credentials.
"""
import argparse
import base64
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
from urllib.error import HTTPError, URLError
from urllib.parse import quote, urlsplit
from urllib.request import HTTPRedirectHandler, Request, build_opener
import zipfile

import package_cloud_build as cloud
import publish_native_update as publisher

REPO = 'sheldon37/Chip-Arena'
BRANCH = 'codex/unity-cloud-current'
ORG = '2474084689228'
PROJECT = '06275870-8c41-42b2-b21a-f75ff362f3dc'
TARGET = 'chip-arena'
UNITY = '6000.3.24f1'
API = 'https://build-automation.services.api.unity.com/v2'
BASE = f'{API}/orgs/{ORG}/projects/{PROJECT}/buildtargets/{TARGET}/builds'
GH = 'https://api.github.com/repos/' + REPO
PUBLICATION = 'native/windows-x64/cloud-publication.json'
SHA = re.compile(r'^[a-f0-9]{40}$')
MAX_DOWNLOAD = 3 * 1024**3
MAX_LOG = 64 * 1024**2


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def request(url, authorization=None, limit=16 * 1024**2, destination=None):
    """Never forward an Authorization header through a redirect or log a signed URL."""
    parsed = urlsplit(url)
    if parsed.scheme != 'https' or parsed.username or parsed.password or parsed.port not in (None, 443):
        raise ValueError('Expected a public HTTPS endpoint')
    if authorization and parsed.hostname not in ('api.github.com', 'build-automation.services.api.unity.com'):
        raise ValueError('Credential destination is not an API endpoint')
    headers = {'User-Agent': 'ChipArena-Native-Automation/1', 'Cache-Control': 'no-cache'}
    if authorization:
        headers['Authorization'] = authorization
    try:
        try:
            response = build_opener(NoRedirect).open(Request(url, headers=headers), timeout=60)
        except HTTPError as error:
            if error.code not in (202, 301, 302, 303, 307, 308):
                raise ValueError(f'HTTP {error.code} from {parsed.hostname}; check service permissions') from None
            response = error
        with response:
            status, response_headers = response.status, response.headers
            if destination is not None and status == 200:
                total = 0
                with destination.open('wb') as output:
                    while block := response.read(1024 * 1024):
                        total += len(block)
                        if total > limit:
                            raise ValueError('Download exceeds size limit')
                        output.write(block)
                return status, response_headers, b''
            data = response.read(limit + 1)
            if len(data) > limit:
                raise ValueError('Response exceeds size limit')
            return status, response_headers, data
    except (URLError, TimeoutError) as error:
        raise ValueError(f'Network request to {parsed.hostname} failed ({type(error).__name__})') from None


def api_json(url, authorization):
    status, _, data = request(url, authorization)
    if status != 200:
        raise ValueError('Unexpected API response status: ' + str(status))
    return json.loads(data)


def storage_url(value):
    parsed = urlsplit(value)
    host = parsed.hostname or ''
    allowed = host == 'storage.googleapis.com' or host.endswith('.storage.googleapis.com') or host.endswith('.amazonaws.com') or host.endswith('.blob.core.windows.net')
    if parsed.scheme != 'https' or not allowed or parsed.username or parsed.password or parsed.port not in (None, 443):
        raise ValueError('Build download is not on an allowed HTTPS storage host')
    return value


def download_api(url, destination, authorization, limit):
    status, headers, data = request(url, authorization, limit, destination)
    for _ in range(4):
        if status == 200:
            return
        if status not in (202, 301, 302, 303, 307, 308):
            raise ValueError('Unexpected download response')
        # UBA v2 artifact downloads return 303 JSON {url: ...}, without Location.
        link = headers.get('Location')
        if not link:
            link = json.loads(data).get('url')
        status, headers, data = request(storage_url(link), limit=limit, destination=destination)
    raise ValueError('Too many download redirects')


def github(path, token):
    return api_json(GH + path, 'Bearer ' + token)


def current_head(token):
    sha = github('/git/ref/heads/' + BRANCH, token)['object']['sha']
    if not SHA.fullmatch(sha):
        raise ValueError('Unexpected cloud branch commit')
    return sha


def native_checks(sha, token):
    result = github('/actions/runs?head_sha=' + sha + '&per_page=100', token)
    runs = [r for r in result['workflow_runs'] if r['head_sha'] == sha and r['head_branch'] == BRANCH
            and r['path'].split('@')[0] == '.github/workflows/unity-conversion.yml'
            and r['event'] in ('push', 'workflow_dispatch')]
    if not runs:
        return False
    run = max(runs, key=lambda r: (r['id'], r.get('run_attempt', 1)))
    if run['status'] != 'completed' or run['conclusion'] != 'success':
        return False
    jobs = github(f"/actions/runs/{run['id']}/jobs?filter=latest&per_page=100", token)['jobs']
    passed = {j['name'] for j in jobs if j['status'] == 'completed' and j['conclusion'] == 'success'}
    return {'native-source', 'native-updater-windows'} <= passed


def build_rejections(build, sha):
    expected = dict(buildStatus='success', lastBuiltRevision=sha, scmBranch=BRANCH,
                    buildtargetid=TARGET, platform='standalonewindows64')
    rejected = [key for key, value in expected.items() if build.get(key) != value]
    # UBA uses an underscore version ID, while the installed Editor and receipt
    # use dotted versions. Accept only the two spellings of our exact pinned ID.
    if build.get('unityVersion') not in (UNITY, UNITY.replace('.', '_')):
        rejected.append('unityVersion')
    if build.get('localUnityVersion') not in (None, '', UNITY):
        rejected.append('localUnityVersion')
    return rejected + [key for key in ('deleted', 'deletedArtifacts', 'deletedLogs', 'headless') if build.get(key)]


def eligible_build(build, sha):
    return not build_rejections(build, sha)


def describe_build(build, sha, source):
    # Allowlist bounded scalar metadata only: no signed links, environment or credentials.
    keys = ('build', 'buildStatus', 'lastBuiltRevision', 'scmBranch', 'buildtargetid',
            'platform', 'unityVersion', 'localUnityVersion', 'deleted', 'deletedArtifacts',
            'deletedLogs', 'headless')
    values = {key: str(build.get(key))[:160] for key in keys}
    print('Unity candidate metadata: ' + json.dumps(dict(source=source, values=values,
                                                        rejected=build_rejections(build, sha))))


def check_build(build, sha):
    if not eligible_build(build, sha):
        raise ValueError('Build is not a successful current-branch Windows candidate')
    for result in (build.get('testResults') or {}).values():
        if isinstance(result, dict) and result.get('failed', 0):
            raise ValueError('Unity test results contain failures')
    if (build.get('buildReport') or {}).get('errors', 0):
        raise ValueError('Unity build report contains errors')


def artifact_filename(artifacts):
    files = [f for artifact in artifacts if artifact.get('primary') or artifact.get('key') == 'primary'
             for f in artifact.get('files', []) if f.get('filename', '').lower().endswith('.zip')]
    if len(files) != 1:
        raise ValueError('Expected exactly one primary Windows ZIP artifact')
    return files[0]['filename']


def safe_entries(archive):
    rows = archive.infolist()
    if len(rows) > 20000 or sum(r.file_size for r in rows) > 6 * 1024**3:
        raise ValueError('Archive inventory exceeds limits')
    names = set()
    for row in rows:
        name = row.orig_filename
        parts = name.rstrip('/').split('/')
        if not name or len(name) > 240 or '\\' in name or any(not p or p in ('.', '..') or p.endswith(('.', ' ')) or re.search(r'[\x00-\x1f\x7f<>:"|?*]', p) or re.fullmatch(r'(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])', p.split('.')[0], re.I) for p in parts):
            raise ValueError('Unsafe delivery archive path')
        kind = (row.external_attr >> 16) & 0xf000
        if kind not in (0, 0x8000, 0x4000) or (kind == 0x4000) != row.is_dir() and kind != 0:
            raise ValueError('Non-file delivery archive entry')
        if name.lower() in names:
            raise ValueError('Duplicate delivery archive path')
        names.add(name.lower())
    return rows


def unpack_delivery(outer, work):
    delivery = work / cloud.DELIVERY
    with zipfile.ZipFile(outer) as archive:
        rows = safe_entries(archive)
        matches = [r for r in rows if r.filename.split('/')[-1] == cloud.DELIVERY]
        if len(matches) != 1:
            raise ValueError('Cloud delivery missing or ambiguous in primary artifact')
        entry = matches[0]
        sidecar = archive.read(entry.filename + '.sha256').decode('ascii').split()
        if len(sidecar) != 2 or sidecar[1] != cloud.DELIVERY or not publisher.HEX.fullmatch(sidecar[0]):
            raise ValueError('Invalid delivery checksum sidecar')
        with archive.open(entry) as source, delivery.open('wb') as target:
            shutil.copyfileobj(source, target)
    if cloud.digest(delivery) != sidecar[0]:
        raise ValueError('Delivery checksum mismatch')
    destination = work / 'delivery'
    destination.mkdir()
    with zipfile.ZipFile(delivery) as archive:
        rows = safe_entries(archive)
        if any(r.is_dir() for r in rows):
            raise ValueError('Unexpected directory entries in cloud delivery')
        for row in rows:
            target = destination / row.filename
            target.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(row) as source, target.open('xb') as output:
                shutil.copyfileobj(source, output)
    return destination


def validate_delivery(delivery, build, provider_log):
    sha, number = build['lastBuiltRevision'], str(build['build'])
    receipt = json.loads((delivery / 'cloud-build-receipt.json').read_text(encoding='utf-8-sig'))
    for key, expected in dict(schema=1, sourceCommit=sha, buildTarget=TARGET, buildNumber=number,
                              unityVersion=UNITY, colorSpace='Linear', published=False).items():
        if receipt.get(key) != expected:
            raise ValueError('Cloud receipt mismatch: ' + key)
    feed = delivery / 'NativeUpdateFeed'
    manifest = publisher.validate(feed)
    if receipt['build'] != manifest['build'] or receipt['updateSha256'] != manifest['sha256']:
        raise ValueError('Receipt and update identity differ')
    for filename, field in ((cloud.SEED, 'seedSha256'), ('build.log', 'buildLogSha256')):
        if cloud.digest(delivery / filename) != receipt[field]:
            raise ValueError('Receipt checksum mismatch: ' + field)
    expected = {cloud.SEED, 'build.log', 'cloud-build-receipt.json', 'Windows/app/native-build.json',
                'NativeUpdateFeed/latest.json'} | {'NativeUpdateFeed/objects/' + p['name'] for p in manifest['parts']}
    actual = {p.relative_to(delivery).as_posix() for p in delivery.rglob('*') if p.is_file()}
    if actual != expected:
        raise ValueError('Unexpected cloud delivery inventory')
    publisher.validate_build_log(feed, delivery / 'build.log')
    cloud.validate_seed(delivery / cloud.SEED, feed, manifest)
    marker = f'NATIVE_CLOUD_EXPORT_READY: {sha} / {number}'
    editor_text = (delivery / 'build.log').read_text(encoding='utf-8-sig', errors='replace')
    provider_text = provider_log.read_text(encoding='utf-8-sig', errors='replace')
    if marker not in editor_text or 'NATIVE_COLOR_SPACE: Linear' not in editor_text:
        raise ValueError('Editor log is missing export or Linear markers')
    for value in (marker, 'NATIVE_UPDATE_FEED_READY:', 'UNITY_TEST_BUILD_READY:', 'NATIVE_CLOUD_DELIVERY_READY:'):
        if value not in provider_text:
            raise ValueError('Provider log is missing a completed packaging gate')
    if re.search(r'(?im)^.*Shader error in|^.*error CS\d{4}\b', editor_text + '\n' + provider_text):
        raise ValueError('Compiler/shader errors found in build logs')
    return manifest, receipt


def output(ready, message):
    print(message)
    if os.environ.get('GITHUB_OUTPUT'):
        with open(os.environ['GITHUB_OUTPUT'], 'a') as stream:
            stream.write('ready=' + str(ready).lower() + '\n')
    if os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as stream:
            stream.write(message + '\n\n')


def prepare(work, token, unity_auth):
    sha = current_head(token)
    rows = api_json(BASE + '?per_page=100&page=1', unity_auth)
    if not native_checks(sha, token):
        output(False, 'Waiting for both native CI jobs to pass on cloud commit ' + sha)
        return
    candidates = [b for b in rows if eligible_build(b, sha)]
    if not candidates:
        for row in rows[:5]:
            describe_build(row, sha, 'list')
        for row in rows:
            if row.get('lastBuiltRevision') == sha and row.get('buildStatus') == 'success':
                number = row.get('build')
                if type(number) is int and number > 0:
                    detail = api_json(BASE + '/' + str(number), unity_auth)
                    describe_build(detail, sha, 'detail')
                break
        output(False, 'Waiting for a successful Unity build of cloud commit ' + sha)
        return
    number = max(b['build'] for b in candidates)
    build = api_json(BASE + '/' + str(number) + '?include=testResults,buildReports,links.artifacts', unity_auth)
    check_build(build, sha)
    current = publisher.remote_manifest()
    # A publication receipt is only a shortcut after checking the current feed.
    receipt_url = publisher.FEED_URL.rsplit('/', 1)[0] + '/cloud-publication.json'
    try:
        status, _, data = request(receipt_url)
        previous = json.loads(data) if status == 200 else {}
    except ValueError as error:
        if 'HTTP 404 ' not in str(error):
            raise
        previous = {}
    if current and previous.get('sourceCommit') == sha and previous.get('buildNumber') == number and previous.get('version') == current['build']['version'] and previous.get('sha256') == current['sha256']:
        if previous.get('publicDownloadsVerified') is True:
            output(False, f'Unity build #{number} is already published and its downloads were verified.')
        else:
            work.mkdir(parents=True, exist_ok=False)
            previous['verificationOnly'] = True
            (work / 'publication.json').write_text(json.dumps(previous, indent=2) + '\n')
            output(True, f'Unity build #{number} was pushed; public download verification will resume.')
        return
    work.mkdir(parents=True, exist_ok=False)
    artifacts = api_json(BASE + '/' + str(number) + '/artifacts', unity_auth)
    filename = artifact_filename(artifacts)
    download_api(BASE + '/' + str(number) + '/download/' + quote(filename, safe=''), work / 'primary.zip', unity_auth, MAX_DOWNLOAD)
    download_api(BASE + '/' + str(number) + '/log', work / 'provider.log', unity_auth, MAX_LOG)
    delivery = unpack_delivery(work / 'primary.zip', work)
    manifest, receipt = validate_delivery(delivery, build, work / 'provider.log')
    problem = publisher.publication_problem(manifest, current)
    if problem:
        output(False, problem)
        return
    if current_head(token) != sha or not native_checks(sha, token):
        output(False, 'Cloud branch or CI changed while downloading; the next run will recheck.')
        return
    record = dict(sourceCommit=sha, buildNumber=number, buildTarget=TARGET, version=manifest['build']['version'],
                  revision=manifest['build']['revision'], sha256=manifest['sha256'], colorSpace='Linear', unityVersion=UNITY,
                  publicDownloadsVerified=False)
    (work / 'build.json').write_text(json.dumps(build))
    (work / 'publication.json').write_text(json.dumps(record, indent=2) + '\n')
    output(True, f'Validated Unity build #{number}, commit {sha}, Linear; ready for native-test publication.')


def publish(work, channel, token):
    record = json.loads((work / 'publication.json').read_text())
    sha = record['sourceCommit']
    if current_head(token) != sha or not native_checks(sha, token):
        raise ValueError('Cloud branch/CI changed before publication; retry on the next run')
    if record.pop('verificationOnly', False):
        current = publisher.prepare_channel(channel)
        if current is None or current['build']['version'] != record['version'] or current['sha256'] != record['sha256']:
            raise ValueError('Feed advanced before pending download verification')
        verify_published(current, record, work, channel)
        return
    build = json.loads((work / 'build.json').read_text())
    check_build(build, sha)
    manifest, _ = validate_delivery(work / 'delivery', build, work / 'provider.log')
    if manifest['build']['version'] != record['version'] or manifest['sha256'] != record['sha256']:
        raise ValueError('Prepared publication identity changed')
    current = publisher.prepare_channel(channel)
    problem = publisher.publication_problem(manifest, current)
    if problem:
        output(False, problem)
        return
    publisher.stage(work / 'delivery/NativeUpdateFeed', channel)
    (channel / PUBLICATION).write_text(json.dumps(record, indent=2) + '\n')
    if current_head(token) != sha:
        raise ValueError('Cloud branch advanced during staging; no update was pushed')
    # One atomic commit moves the manifest together with all its verified objects.
    publisher.git(channel, 'add', '--', 'native/windows-x64')
    publisher.git(channel, 'commit', '-m', f"Publish Unity cloud build {record['buildNumber']} ({sha[:12]})")
    publisher.git(channel, 'push', 'origin', 'HEAD:main')
    output(False, f"Published Unity build #{record['buildNumber']} to native-test. Channel commit: " + publisher.git(channel, 'rev-parse', 'HEAD'))
    # GitHub raw content can lag a successful push. Record this honestly; never rebuild.
    verify_published(manifest, record, work, channel)


def verify_published(manifest, record, work, channel):
    if publisher.remote_manifest() != manifest:
        raise ValueError('Publication was pushed, but the public manifest has not refreshed; rerun to verify')
    verify_public_parts(manifest, work / 'public-verification')
    # Persist verification once, so idle scheduled runs do not redownload the game.
    current = publisher.prepare_channel(channel)
    if current != manifest:
        raise ValueError('Feed advanced during public verification; no receipt changed')
    record['publicDownloadsVerified'] = True
    record['verifiedAt'] = datetime.now(timezone.utc).isoformat()
    (channel / PUBLICATION).write_text(json.dumps(record, indent=2) + '\n')
    publisher.git(channel, 'add', '--', PUBLICATION)
    publisher.git(channel, 'commit', '-m', f"Verify public Unity build {record['buildNumber']} downloads")
    publisher.git(channel, 'push', 'origin', 'HEAD:main')
    output(False, 'Public manifest and every downloaded payload part passed archive/hash verification. Update is ready.')


def verify_public_parts(manifest, destination):
    destination.mkdir(parents=True, exist_ok=True)
    (destination / 'objects').mkdir(exist_ok=True)
    (destination / 'latest.json').write_text(json.dumps(manifest))
    for part in manifest['parts']:
        status, _, _ = request(publisher.FEED_URL.rsplit('/', 1)[0] + '/objects/' + part['name'],
                               limit=publisher.MAX_PART, destination=destination / 'objects' / part['name'])
        if status != 200:
            raise ValueError('Public payload download did not complete')
    publisher.validate(destination)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=('prepare', 'publish'))
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--channel', type=Path)
    args = parser.parse_args()
    token = os.environ.get('GH_TOKEN', '')
    if not token:
        raise ValueError('GH_TOKEN is missing')
    if args.mode == 'prepare':
        key, secret = os.environ.get('UNITY_SERVICE_KEY_ID', ''), os.environ.get('UNITY_SERVICE_SECRET', '')
        if not key or not secret:
            raise ValueError('Add UNITY_SERVICE_KEY_ID and UNITY_SERVICE_SECRET as Actions secrets')
        auth = 'Basic ' + base64.b64encode((key + ':' + secret).encode()).decode()
        print('::add-mask::' + auth)
        prepare(args.work, token, auth)
    else:
        if args.channel is None:
            raise ValueError('--channel is required for publication')
        publish(args.work, args.channel, token)


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, KeyError, TypeError, zipfile.BadZipFile, subprocess.CalledProcessError) as error:
        # API errors are sanitized by request(); never print payloads or signed URLs.
        print('Native cloud publication: ' + str(error), file=sys.stderr)
        sys.exit(1)
