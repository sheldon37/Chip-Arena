"""Start one verified current-head UBA build when the repository hook missed it.

Uses the existing target and service credential; never edits configuration,
cancels builds, retries failed builds, or publishes unverified artifacts.
"""
import base64
import json
import os
import sys
import time
from urllib.error import HTTPError, URLError
from urllib.request import Request, build_opener

import auto_publish as auto

TERMINAL = {'success', 'failure', 'failed', 'canceled', 'cancelled'}


def start_build(sha, authorization):
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
        raise ValueError('Unity build request returned HTTP ' + str(error.code)) from None
    except (URLError, TimeoutError):
        # An ambiguous POST outcome must not be retried automatically.
        raise ValueError('Build request outcome is unknown; inspect Unity before retrying') from None


def ensure_build(token, authorization, wait_seconds=900):
    sha = auto.current_head(token)
    if not auto.native_checks(sha, token):
        print('Build not requested: current cloud commit has not passed both native CI jobs.')
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
            print('Cloud branch advanced; leaving publication to the newer commit.')
            return False
        rows = auto.api_json(auto.BASE + '?per_page=100&page=1', authorization)
        if any(auto.eligible_build(row, sha) for row in rows):
            print('Current Unity build succeeded; delivery validation can proceed.', flush=True)
            return True
        matching = [r for r in rows if sha in (r.get('lastBuiltRevision'), r.get('requestedRevision'))]
        if any(r.get('buildStatus') in TERMINAL for r in matching):
            raise ValueError('Current commit has a failed/cancelled or ineligible completed build; inspect Unity, no automatic retry')
        active = [r for r in rows if r.get('buildStatus') not in TERMINAL and not r.get('deleted')]
        if not active and not requested:
            # Recheck immediately before the only mutation. Never change target
            # settings or request a build for a moving/unchecked branch head.
            if auto.current_head(token) != sha:
                return False
            start_build(sha, authorization)
            requested = True
        elif active:
            print('Waiting on Unity: ' + ', '.join('#' + str(r.get('build')) + ' ' + str(r.get('buildStatus')) for r in active), flush=True)
        if time.monotonic() >= deadline:
            print('Unity is still pending; a later publisher check will resume.', flush=True)
            return False
        time.sleep(30)


def main():
    token = os.environ.get('GH_TOKEN', '')
    key, secret = os.environ.get('UNITY_SERVICE_KEY_ID', ''), os.environ.get('UNITY_SERVICE_SECRET', '')
    if not token or not key or not secret:
        raise ValueError('Existing GitHub/Unity service credentials are required')
    auth = 'Basic ' + base64.b64encode((key + ':' + secret).encode()).decode()
    print('::add-mask::' + auth)
    ensure_build(token, auth)


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, KeyError, TypeError) as error:
        print('Native cloud build: ' + str(error), file=sys.stderr)
        sys.exit(1)
