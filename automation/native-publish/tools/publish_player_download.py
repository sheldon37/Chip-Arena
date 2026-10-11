"""Publish the validated, updater-ready Windows package to the existing test repository.

Preparation uses read-only Unity credentials. Publication uses the existing scoped
feed token. Never executes downloaded content or replaces an existing release asset.
"""
import argparse
import base64
import json
import os
from pathlib import Path
import re
import sys
from urllib.error import HTTPError, URLError
from urllib.parse import quote, urlsplit
from urllib.request import Request, build_opener
import zipfile

import auto_publish as auto

REPO = 'sheldon37/chip-arena-test-updates'
NAME = auto.cloud.PORTABLE
MAX_ZIP = 2 * 1024**3
FIELDS = ('sourceCommit', 'buildNumber', 'buildTarget', 'version', 'revision', 'sha256', 'colorSpace', 'unityVersion')


def release_api(path, token=None, method='GET', body=None, upload=None):
    # Construct endpoints ourselves; no artifact-provided URL can receive a token.
    if not re.fullmatch(r'/releases(?:\?per_page=100&page=\d+|/tags/[a-z0-9-]+|/\d+(?:/assets\?name=Chip-Arena-Windows\.zip)?)?', path):
        raise ValueError('Unexpected release API path')
    host = 'uploads.github.com' if upload else 'api.github.com'
    url = 'https://' + host + '/repos/' + REPO + path
    headers = {'Accept': 'application/vnd.github+json', 'User-Agent': 'ChipArena-Player-Download/1',
               'X-GitHub-Api-Version': '2022-11-28'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    data = json.dumps(body).encode() if body is not None else None
    if upload:
        headers.update({'Content-Type': 'application/zip', 'Content-Length': str(upload.stat().st_size)})
    elif data is not None:
        headers['Content-Type'] = 'application/json'
    stream = upload.open('rb') if upload else None
    try:
        with build_opener(auto.NoRedirect).open(Request(url, data=stream or data, headers=headers, method=method), timeout=120) as response:
            if response.status not in (200, 201):
                raise ValueError('Unexpected release API response')
            payload = response.read(2 * 1024**2 + 1)
            if len(payload) > 2 * 1024**2:
                raise ValueError('Release API response exceeds limit')
            return json.loads(payload)
    except HTTPError as error:
        if error.code == 404 and method == 'GET':
            return None
        raise ValueError(f'Release API HTTP {error.code}; no existing assets were overwritten') from None
    except (URLError, TimeoutError) as error:
        raise ValueError('Release API connection failed: ' + type(error).__name__) from None
    finally:
        if stream:
            stream.close()


def published_record(token):
    manifest = auto.publisher.remote_manifest()
    receipt = auto.api_json(auto.publisher.FEED_URL.rsplit('/', 1)[0] + '/cloud-publication.json', None)
    if not manifest or receipt.get('publicDownloadsVerified') is not True:
        raise ValueError('The native update must be published and publicly verified first')
    if any(receipt.get(k) != manifest['build'][k] for k in ('version', 'revision')) or receipt.get('sha256') != manifest['sha256']:
        raise ValueError('Public native feed and publication receipt differ')
    if (receipt.get('buildTarget') != auto.TARGET or receipt.get('unityVersion') != auto.UNITY or
            receipt.get('colorSpace') != 'Linear' or type(receipt.get('buildNumber')) is not int or receipt['buildNumber'] <= 0):
        raise ValueError('Unexpected native publication provenance')
    sha = receipt.get('sourceCommit')
    if sha != auto.current_head(token) or not auto.native_checks(sha, token):
        raise ValueError('Published game is not the current cloud commit with passing native CI')
    return {key: receipt[key] for key in FIELDS}


def tag(record):
    return 'native-build-' + str(record['buildNumber']) + '-' + record['sourceCommit'][:12]


def find_release(record, token):
    # The tag endpoint documents published releases only. The authenticated list
    # includes drafts, so an interrupted upload can resume without a duplicate.
    for page in range(1, 21):
        rows = release_api('/releases?per_page=100&page=' + str(page), token)
        matches = [r for r in rows if r.get('tag_name') == tag(record)]
        if len(matches) > 1:
            raise ValueError('Ambiguous existing download releases')
        if matches:
            return matches[0]
        if len(rows) < 100:
            return None
    raise ValueError('Release inventory exceeds lookup limit')


def download_url(record):
    return 'https://github.com/' + REPO + '/releases/download/' + tag(record) + '/' + NAME


def release_body(record, digest, size, verified=False):
    return (f'Download **{NAME}** below. Extract it, open **Chip Arena**, and run **Chip Arena.exe**.\n\n'
            'The Update Game button is included and configured from the first launch. Keep the whole folder together. '
            'Future updates install from the main menu; no Unity account or separate updater installation is required.\n\n'
            f'Windows x64 test build {record["buildNumber"]}. Source: `{record["sourceCommit"]}`.\n'
            f'Installed version: `{record["version"]}`. Update payload: `{record["sha256"]}`.\n'
            f'ZIP SHA-256: `{digest}` ({size} bytes).\n\n'
            + ('Public ZIP download verified against the validated Unity package.\n' if verified else 'Public ZIP verification pending.\n'))


def completed_release(release, record):
    if not release or release.get('draft') or release.get('prerelease') is not True or release.get('tag_name') != tag(record):
        return False
    assets = release.get('assets', [])
    if len(assets) != 1:
        return False
    asset = assets[0]
    digest = asset.get('digest', '').removeprefix('sha256:')
    size = asset.get('size', 0)
    return (asset.get('name') == NAME and asset.get('state') == 'uploaded' and type(size) is int and 0 < size <= MAX_ZIP
            and re.fullmatch('[0-9a-f]{64}', digest) is not None and asset.get('browser_download_url') == download_url(record)
            and release.get('body') == release_body(record, digest, size, True))


def validate_package(work, record):
    build = json.loads((work / 'build.json').read_text())
    auto.check_build(build, record['sourceCommit'])
    if build['build'] != record['buildNumber']:
        raise ValueError('Downloaded build number differs from the published build')
    manifest, _ = auto.validate_delivery(work / 'delivery', build, work / 'provider.log')
    if any(record[k] != manifest['build'][k] for k in ('version', 'revision')) or record['sha256'] != manifest['sha256']:
        raise ValueError('Player package differs from the published update')
    seed = work / 'delivery' / auto.cloud.SEED
    with zipfile.ZipFile(seed) as archive:
        rows = auto.safe_entries(archive)
        if any(not r.filename.startswith('Chip Arena/') for r in rows) or 'Chip Arena/Chip Arena.exe' not in archive.namelist():
            raise ValueError('Player download must use the single Chip Arena folder and launcher')
    if not 0 < seed.stat().st_size <= MAX_ZIP:
        raise ValueError('Player ZIP exceeds the release asset size limit')
    return seed


def prepare(work, token, unity_auth):
    record = published_record(token)
    existing = release_api('/releases/tags/' + tag(record))
    if completed_release(existing, record):
        auto.output(False, 'Player download is ready: ' + download_url(record), 'player_download_current')
        return
    work.mkdir(parents=True, exist_ok=False)
    base = auto.BASE + '/' + str(record['buildNumber'])
    build = auto.api_json(base + '?include=testResults,buildReports,links.artifacts', unity_auth)
    auto.check_build(build, record['sourceCommit'])
    filename = auto.artifact_filename(auto.api_json(base + '/artifacts', unity_auth))
    auto.download_api(base + '/download/' + quote(filename, safe=''), work / 'primary.zip', unity_auth, auto.MAX_DOWNLOAD)
    auto.download_api(base + '/log?redirect=manual', work / 'provider.log', unity_auth, auto.MAX_LOG)
    auto.unpack_delivery(work / 'primary.zip', work)
    (work / 'build.json').write_text(json.dumps(build))
    seed = validate_package(work, record)
    if published_record(token) != record:
        raise ValueError('Published build changed while preparing its download')
    plan = dict(record=record, sha256=auto.cloud.digest(seed), bytes=seed.stat().st_size)
    (work / 'player-download.json').write_text(json.dumps(plan, indent=2) + '\n')
    auto.output(True, 'Validated updater-ready player ZIP for build ' + str(record['buildNumber']), 'player_download_prepared')


def verify_public_download(record, path, size, digest):
    url = download_url(record)
    for _ in range(5):
        status, headers, _ = auto.request(url, limit=size, destination=path)
        if status == 200:
            if path.stat().st_size != size or auto.cloud.digest(path) != digest:
                raise ValueError('Public player ZIP checksum or size differs')
            return
        if status not in (301, 302, 303, 307, 308):
            raise ValueError('Public player ZIP download did not complete')
        url = headers.get('Location', '')
        parsed = urlsplit(url)
        if (parsed.scheme != 'https' or parsed.hostname not in ('release-assets.githubusercontent.com', 'objects.githubusercontent.com', 'github.com')
                or parsed.username or parsed.password or parsed.port not in (None, 443)):
            raise ValueError('Unexpected public release download redirect')
    raise ValueError('Too many public release download redirects')


def publish(work, token, feed_token):
    plan = json.loads((work / 'player-download.json').read_text())
    record = plan['record']
    if published_record(token) != record:
        raise ValueError('Published build changed before player download publication')
    seed = validate_package(work, record)
    digest, size = auto.cloud.digest(seed), seed.stat().st_size
    if digest != plan['sha256'] or size != plan['bytes']:
        raise ValueError('Prepared player ZIP changed')
    release = find_release(record, feed_token)
    if completed_release(release, record):
        auto.output(False, 'Player download is ready: ' + download_url(record), 'player_download_current')
        return
    pending = release_body(record, digest, size)
    if release is None:
        release = release_api('/releases', feed_token, 'POST', dict(tag_name=tag(record), target_commitish='main',
                              name='Chip Arena — Windows test build ' + str(record['buildNumber']), body=pending,
                              draft=True, prerelease=True, make_latest='false'))
    elif release.get('tag_name') != tag(record) or release.get('prerelease') is not True or release.get('body') != pending:
        raise ValueError('Existing release does not match this prepared download; left unchanged')
    if type(release.get('id')) is not int or release['id'] <= 0:
        raise ValueError('Unexpected release identity')
    release_path = '/releases/' + str(release['id'])
    assets = release.get('assets', [])
    if not assets:
        asset = release_api(release_path + '/assets?name=' + NAME, feed_token, 'POST', upload=seed)
    elif len(assets) == 1:
        asset = assets[0]
    else:
        raise ValueError('Unexpected existing release assets; left unchanged')
    if (asset.get('name') != NAME or asset.get('state') != 'uploaded' or asset.get('size') != size
            or asset.get('digest') != 'sha256:' + digest or asset.get('browser_download_url') != download_url(record)):
        raise ValueError('Uploaded player ZIP does not match the prepared package; release not finalized')
    if published_record(token) != record:
        raise ValueError('Published build changed before making the player ZIP available')
    if release.get('draft'):
        release_api(release_path, feed_token, 'PATCH', dict(draft=False, prerelease=True, make_latest='false'))
    verify_public_download(record, work / 'public-player.zip', size, digest)
    release = release_api(release_path, feed_token, 'PATCH', dict(body=release_body(record, digest, size, True)))
    if not completed_release(release, record):
        raise ValueError('Published player download metadata could not be verified')
    auto.output(False, 'Download and extract, then launch Chip Arena.exe: ' + download_url(record), 'player_download_verified')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=('prepare', 'publish'))
    parser.add_argument('--work', type=Path, required=True)
    args = parser.parse_args()
    token = os.environ.get('GH_TOKEN')
    if not token:
        raise ValueError('GH_TOKEN is missing')
    if args.mode == 'prepare':
        key, secret = os.environ.get('UNITY_SERVICE_KEY_ID'), os.environ.get('UNITY_SERVICE_SECRET')
        if not key or not secret:
            raise ValueError('Unity service credentials are missing')
        auth = 'Basic ' + base64.b64encode((key + ':' + secret).encode()).decode()
        print('::add-mask::' + auth)
        prepare(args.work, token, auth)
    else:
        feed_token = os.environ.get('NATIVE_FEED_TOKEN')
        if not feed_token:
            raise ValueError('NATIVE_FEED_TOKEN is missing')
        publish(args.work, token, feed_token)


if __name__ == '__main__':
    try:
        main()
    except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile) as error:
        print('Player download: ' + str(error), file=sys.stderr)
        sys.exit(1)
