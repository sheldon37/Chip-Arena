"""Start one verified current-head UBA build when the repository hook missed it.

Uses the existing target and service credential; never edits configuration,
cancels builds, retries failed builds, or publishes unverified artifacts.
"""
import argparse
import base64
import json
import hashlib
import re
import os
import sys
import time
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import Request, build_opener

import auto_publish as auto

TERMINAL = {'success', 'failure', 'failed', 'canceled', 'cancelled'}


def request_failure(error, sha, work):
    """Bounded, allowlisted diagnostics: never persist provider text, URLs or credentials."""
    data = error.read(65537)
    text = data[:65536].decode('utf-8', errors='replace').lower()
    categories = {
        'quota_or_billing': r'quota|billing|payment|credit|spending|budget',
        'authentication': r'unauthori[sz]ed|authentication|invalid credential',
        'permission': r'permission|forbidden|access denied',
        'source_checkout': r'repository|checkout|revision|commit not found|scm',
        'capacity': r'capacity|no available|unavailable|maintenance|overload',
        'configuration': r'configuration|build target|buildtarget|invalid parameter',
    }
    report = dict(sourceCommit=sha, httpStatus=error.code, responseBytes=len(data),
                  truncated=len(data)>65536, responseSha256=hashlib.sha256(data).hexdigest(),
                  categories=[name for name, pattern in categories.items() if re.search(pattern, text)])
    for name in ('x-request-id', 'x-correlation-id', 'request-id'):
        value = (error.headers or {}).get(name, '')
        if re.fullmatch(r'[0-9a-fA-F-]{8,64}', value):
            report[name] = value
    if work is not None:
        path = Path(work); path.mkdir(parents=True, exist_ok=True)
        (path / 'build-request-failure.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    print('Unity request diagnostics: HTTP ' + str(error.code) + '; categories: '
          + (', '.join(report['categories']) or 'unclassified')
          + '; response SHA256 ' + report['responseSha256'], flush=True)


def start_build(sha, authorization, work=None):
    # Official startBuilds contract: commit pins the source; omit branch/machine
    # overrides so the existing chip-arena target remains authoritative.
    # https://docs.unity.com/en-us/oas-build-automation-client/2.0.0
    if not auto.SHA.fullmatch(sha):
        raise ValueError('Invalid build commit')
    request = Request(auto.BASE, method='POST',
                      data=json.dumps({'commit': sha, 'clean': False}).encode(),
                      headers={'Authorization': authorization, 'Content-Type': 'application/json'})
    try:
        with build_opener(auto.NoRedirect).open(request, timeout=60) as response:
            if response.status != 202:
                raise ValueError('Unity did not acknowledge the build request')
            data = response.read(1024 * 1024 + 1)
            if len(data) > 1024 * 1024:
                raise ValueError('Unity build acknowledgement exceeds size limit')
            rows = json.loads(data)
            if not isinstance(rows, list) or len(rows) != 1:
                raise ValueError('Unexpected Unity build acknowledgement')
            row = rows[0]
            if row.get('error') or row.get('buildtargetid') != auto.TARGET or type(row.get('build')) is not int:
                raise ValueError('Unity did not acknowledge the expected target')
            print('Requested Unity build #' + str(row['build']) + ' for ' + sha, flush=True)
    except HTTPError as error:
        if error.code == 409:
            print('Unity already has a pending build; waiting for its result.', flush=True)
            return
        request_failure(error, sha, work)
        raise ValueError('Unity build request returned HTTP ' + str(error.code) + '; no automatic retry (request outcome may be ambiguous)') from None
    except (URLError, TimeoutError):
        # An ambiguous POST outcome must not be retried automatically.
        raise ValueError('Build request outcome is unknown; inspect Unity before retrying') from None


def retain_failed_log(rows, sha, authorization, work):
    """Save only the fixed-target log and non-sensitive identity, never API rows."""
    if work is None:
        return
    numbered = [r for r in rows if type(r.get('build')) is int and r['build'] > 0]
    if not numbered:
        return
    build = max(numbered, key=lambda row: row['build'])
    work = Path(work)
    work.mkdir(parents=True, exist_ok=True)
    number = build['build']
    (work / 'build-failure.json').write_text(json.dumps({
        'sourceCommit': sha, 'build': number, 'buildStatus': build.get('buildStatus'),
    }, indent=2) + '\n', encoding='utf-8')
    try:
        auto.download_api(auto.BASE + '/' + str(number) + '/log?redirect=manual',
                          work / 'provider.log', authorization, auto.MAX_LOG)
        print('Retained private Unity build #' + str(number) + ' diagnostic log.', flush=True)
    except (ValueError, OSError, KeyError, TypeError):
        # The build remains failed even if Unity has not made its log available.
        # Do not print provider errors or signed storage URLs.
        print('Unity build #' + str(number) + ' diagnostic log is unavailable.', flush=True)


def ensure_build(token, authorization, wait_seconds=900, work=None):
    sha = auto.current_head(token)
    if not auto.native_checks(sha, token):
        auto.output(False, 'Build not requested: current cloud commit ' + sha + ' has not passed both native CI jobs.', 'waiting_ci')
        return False
    config = auto.api_json(auto.BASE.rsplit('/builds', 1)[0], authorization)
    settings = config.get('settings') or {}
    if (config.get('buildtargetid') != auto.TARGET or config.get('enabled') is not True
            or config.get('platform') != 'standalonewindows64'
            or (settings.get('scm') or {}).get('branch') != auto.BRANCH):
        raise ValueError('Unity target must remain enabled Windows x64 on the cloud branch')
    print('Unity target Auto-build: ' + str(settings.get('autoBuild')), flush=True)
    deadline = time.monotonic() + wait_seconds
    requested = False
    while True:
        if auto.current_head(token) != sha:
            auto.output(False, 'Cloud branch advanced; leaving publication to the newer commit.', 'superseded')
            return False
        rows = auto.api_json(auto.BASE + '?per_page=100&page=1', authorization)
        if any(auto.eligible_build(row, sha) for row in rows):
            auto.output(True, 'Current Unity build succeeded; delivery validation can proceed.')
            return True
        matching = [r for r in rows if sha in (r.get('lastBuiltRevision'), r.get('requestedRevision'))]
        completed = [r for r in matching if r.get('buildStatus') in TERMINAL]
        if completed:
            retain_failed_log(completed, sha, authorization, work)
            raise ValueError('Current commit has a failed/cancelled or ineligible completed build; inspect Unity, no automatic retry')
        active = [r for r in rows if r.get('buildStatus') not in TERMINAL and not r.get('deleted')]
        if not active and not requested:
            # Recheck immediately before the only mutation. Never change target
            # settings or request a build for a moving/unchecked branch head.
            if auto.current_head(token) != sha:
                auto.output(False, 'Cloud branch advanced before the build request; no build requested.', 'superseded')
                return False
            start_build(sha, authorization, work=work)
            requested = True
        elif active:
            print('Waiting on Unity: ' + ', '.join('#' + str(r.get('build')) + ' ' + str(r.get('buildStatus')) for r in active), flush=True)
        if time.monotonic() >= deadline:
            pending = ', '.join('#' + str(r.get('build')) + ' ' + str(r.get('buildStatus')) for r in active)
            auto.output(False, 'No update published: Unity is still pending for ' + sha
                        + (': ' + pending if pending else '')
                        + '. The scheduled publisher will resume; no duplicate build was requested.', 'waiting_unity')
            return False
        time.sleep(30)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path, required=True)
    args = parser.parse_args()
    token = os.environ.get('GH_TOKEN', '')
    key, secret = os.environ.get('UNITY_SERVICE_KEY_ID', ''), os.environ.get('UNITY_SERVICE_SECRET', '')
    if not token or not key or not secret:
        raise ValueError('Existing GitHub/Unity service credentials are required')
    auth = 'Basic ' + base64.b64encode((key + ':' + secret).encode()).decode()
    print('::add-mask::' + auth)
    ensure_build(token, auth, work=args.work)


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, KeyError, TypeError) as error:
        print('Native cloud build: ' + str(error), file=sys.stderr)
        sys.exit(1)
