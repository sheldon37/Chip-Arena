"""Trigger failure/duplication checks; all service calls are mocked."""
from contextlib import ExitStack
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch, MagicMock
from urllib.error import HTTPError

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
import ensure_cloud_build as trigger
import auto_publish as auto


class TriggerTests(unittest.TestCase):
    sha = 'a' * 40

    def test_deferred_and_ready_runs_report_truthful_workflow_outputs(self):
        success = dict(build=18, buildStatus='success', lastBuiltRevision=self.sha,
                       scmBranch=auto.BRANCH, buildtargetid=auto.TARGET,
                       platform='standalonewindows64', unityVersion=auto.UNITY)
        cases = [({'passed': False}, 'waiting_ci', False),
                 ({'rows': [dict(build=17, buildStatus='sentToBuilder')]}, 'waiting_unity', False),
                 ({'heads': [self.sha, self.sha, 'b'*40]}, 'superseded', False),
                 ({'rows': [success]}, 'ready', True)]
        for kwargs, status, ready in cases:
            with self.subTest(status=status), tempfile.TemporaryDirectory() as temp:
                output, summary = Path(temp) / 'output', Path(temp) / 'summary'
                with patch.dict(os.environ, GITHUB_OUTPUT=str(output), GITHUB_STEP_SUMMARY=str(summary)):
                    result, start, _ = self.run_case(**kwargs)
                self.assertEqual(result, ready)
                start.assert_not_called()
                self.assertEqual(output.read_text(), f'ready={str(ready).lower()}\nstatus={status}\n')
                self.assertIn('**Native publication: ' + status + '**', summary.read_text())
                if status == 'waiting_unity':
                    self.assertIn('#17 sentToBuilder', summary.read_text())
                    self.assertIn('No update published', summary.read_text())

    def run_case(self, rows=(), passed=True, config=None, heads=None):
        config = config if config is not None else dict(buildtargetid=auto.TARGET, enabled=True,
            platform='standalonewindows64', settings={'scm': {'branch': auto.BRANCH}, 'autoBuild': True})
        with ExitStack() as stack:
            stack.enter_context(patch.object(auto, 'current_head', side_effect=heads, return_value=self.sha))
            stack.enter_context(patch.object(auto, 'native_checks', return_value=passed))
            api = stack.enter_context(patch.object(auto, 'api_json', side_effect=[config, list(rows)]))
            start = stack.enter_context(patch.object(trigger, 'start_build'))
            stack.enter_context(patch('builtins.print'))
            result = trigger.ensure_build('github', 'unity', wait_seconds=0)
            return result, start, api

    def test_no_build_without_both_native_checks(self):
        result, start, api = self.run_case(passed=False)
        self.assertFalse(result);start.assert_not_called();api.assert_not_called()

    def test_missed_hook_starts_only_pinned_current_commit(self):
        result, start, _ = self.run_case()
        self.assertFalse(result);start.assert_called_once_with(self.sha, 'unity')

    def test_existing_active_build_prevents_duplicate_even_before_revision_is_known(self):
        for status in ('queued', 'sentToBuilder', 'started'):
            _, start, _ = self.run_case([dict(build=8, buildStatus=status)])
            start.assert_not_called()

    def test_successful_current_delivery_needs_no_build(self):
        row = dict(build=8, buildStatus='success', lastBuiltRevision=self.sha,
                   scmBranch=auto.BRANCH, buildtargetid=auto.TARGET,
                   platform='standalonewindows64', unityVersion=auto.UNITY)
        result, start, _ = self.run_case([row])
        self.assertTrue(result);start.assert_not_called()

    def test_failed_or_cancelled_current_build_is_not_retried(self):
        for status in ('failure', 'canceled'):
            with self.assertRaisesRegex(ValueError, 'no automatic retry'):
                self.run_case([dict(build=8, buildStatus=status, requestedRevision=self.sha)])

    def test_other_commit_failure_does_not_block_new_source(self):
        _, start, _ = self.run_case([dict(build=7, buildStatus='failure', lastBuiltRevision='b'*40)])
        start.assert_called_once()

    def test_failed_build_retains_only_private_log_and_safe_identity(self):
        rows = [dict(build=24, buildStatus='failure', signedUrl='private-url',
                     requestedRevision=self.sha), dict(build=23, buildStatus='canceled')]
        with tempfile.TemporaryDirectory() as temp, patch.object(auto, 'download_api') as download:
            trigger.retain_failed_log(rows, self.sha, 'private-auth', temp)
            download.assert_called_once_with(auto.BASE + '/24/log?redirect=manual',
                                             Path(temp) / 'provider.log', 'private-auth', auto.MAX_LOG)
            identity = json.loads((Path(temp) / 'build-failure.json').read_text())
            self.assertEqual(identity, dict(sourceCommit=self.sha, build=24, buildStatus='failure'))

    def test_unavailable_log_preserves_failure_and_does_not_print_provider_error(self):
        with tempfile.TemporaryDirectory() as temp, patch.object(auto, 'download_api',
                side_effect=ValueError('private-url')), patch('builtins.print') as output:
            trigger.retain_failed_log([dict(build=24, buildStatus='failure')], self.sha, 'auth', temp)
            self.assertTrue((Path(temp) / 'build-failure.json').exists())
            self.assertNotIn('private-url', str(output.call_args_list))

    def test_failed_build_diagnostics_cannot_request_a_retry(self):
        with patch.object(trigger, 'retain_failed_log') as retain, patch.object(trigger, 'start_build') as start:
            with self.assertRaisesRegex(ValueError, 'no automatic retry'):
                self.run_case([dict(build=24, buildStatus='failure', requestedRevision=self.sha)])
            retain.assert_called_once()
            self.assertEqual(retain.call_args.args[1:3], (self.sha, 'unity'))
            start.assert_not_called()

    def test_drifted_or_disabled_target_is_never_modified(self):
        for change in ({'enabled': False}, {'platform': 'webgl'}, {'settings': {'scm': {'branch': 'other'}}}):
            config = dict(buildtargetid=auto.TARGET, enabled=True, platform='standalonewindows64', settings={'scm': {'branch': auto.BRANCH}})
            config.update(change)
            with self.assertRaisesRegex(ValueError, 'target must remain'):
                self.run_case(config=config)

    def test_branch_advance_prevents_request(self):
        _, start, _ = self.run_case(heads=[self.sha, self.sha, 'b'*40])
        start.assert_not_called()

    def test_post_is_exact_target_pinned_commit_without_overrides(self):
        response = MagicMock();response.__enter__.return_value=response
        response.status=202;response.read.return_value=json.dumps([{'build':8,'buildtargetid':auto.TARGET}]).encode()
        with patch.object(trigger, 'build_opener') as opener, patch('builtins.print'):
            opener.return_value.open.return_value=response
            trigger.start_build(self.sha, 'private-auth')
            request=opener.return_value.open.call_args.args[0]
            self.assertEqual(request.full_url, auto.BASE)
            self.assertEqual(request.get_method(), 'POST')
            self.assertEqual(json.loads(request.data), {'commit':self.sha,'clean':False})
            self.assertIs(opener.call_args.args[0], auto.NoRedirect)

    def test_conflict_is_not_reposted_and_errors_do_not_leak_body(self):
        for code in (409, 403, 302):
            with patch.object(trigger, 'build_opener') as opener, patch('builtins.print'):
                opener.return_value.open.side_effect=HTTPError(auto.BASE,code,'error',{},io.BytesIO(b'private-server-payload'))
                if code==409:trigger.start_build(self.sha,'private-auth')
                else:
                    with self.assertRaisesRegex(ValueError,'HTTP '+str(code)) as error:trigger.start_build(self.sha,'private-auth')
                    self.assertNotIn('private',str(error.exception))
                self.assertEqual(opener.return_value.open.call_count,1)


if __name__ == '__main__':
    unittest.main()
