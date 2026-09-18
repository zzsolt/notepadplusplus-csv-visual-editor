from pathlib import Path
import importlib.util
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('ci_plan', ROOT / 'tools/ci/plan.py')
plan = importlib.util.module_from_spec(spec)
spec.loader.exec_module(plan)


class PlanTests(unittest.TestCase):
    def test_documentation_needs_no_validation(self):
        for paths in ([], ['README.md', 'CHANGELOG.md', 'docs/building.md']):
            self.assertEqual({'validate': False, 'full': False}, plan.classify(paths, 'pull_request', 'docs'))

    def test_source_updates_are_cheap(self):
        for path in ('src/CsvVisualEditor/Main.cs', 'src/CsvVisualEditor.Localization/Catalogs/hu.json',
                     'tests/localization/test_catalog_gate.py', 'tests/release/test_release_package.py', 'tools/ci/plan.py',
                     'packaging/nppPluginList/entry.template.json', 'LICENSE', 'THIRD_PARTY_NOTICES.txt',
                     '.github/workflows/ci.yml', 'Directory.Build.props', 'global.json'):
            self.assertEqual({'validate': True, 'full': False}, plan.classify([path], 'pull_request', 'Update'))

    def test_explicit_candidate_runs_full_gate(self):
        self.assertEqual({'validate': True, 'full': True},
                         plan.classify(['src/a.cs'], 'pull_request', 'Candidate [test-package]'))

    def test_documentation_candidate_marker_does_not_start_windows(self):
        self.assertEqual({'validate': False, 'full': False},
                         plan.classify(['docs/building.md'], 'pull_request', 'Docs [test-package]'))

    @patch.object(plan, 'git')
    def test_synchronize_uses_incremental_changes(self, git):
        git.side_effect = ['docs', 'docs/building.md\0']
        base, before, head = 'a' * 40, 'b' * 40, 'c' * 40
        event = {'action': 'synchronize', 'before': before,
                 'pull_request': {'base': {'sha': base}, 'head': {'sha': head}}}
        self.assertEqual({'validate': False, 'full': False}, plan.plan_event('pull_request', event, head))
        git.assert_any_call('diff', '--name-only', '-z', before, head, '--')

    def test_manual_validation_runs_full_even_without_upload(self):
        self.assertEqual({'validate': True, 'full': True}, plan.classify([], 'workflow_dispatch', ''))

    def test_unsupported_event_fails_closed(self):
        with self.assertRaises(ValueError):
            plan.classify(['src/a.cs'], 'issue_comment', '[test-package]')

    def test_bad_sha_is_not_used_as_git_argument(self):
        for value in ('--all', 'main', '', 'a' * 39, 'g' * 40):
            with self.assertRaises(ValueError):
                plan.revision(value)

    @patch.object(plan, 'git')
    def test_pr_marker_read_from_head_not_merge(self, git):
        base, head = 'a' * 40, 'b' * 40
        git.side_effect = ['ordinary head', 'src/a.cs\0']
        result = plan.plan_event('pull_request', {'pull_request': {'base': {'sha': base}, 'head': {'sha': head}}}, 'c' * 40)
        self.assertFalse(result['full'])
        git.assert_any_call('log', '-1', '--format=%B', head, '--')
        git.assert_any_call('diff', '--name-only', '-z', base, head, '--')

    @patch.object(plan, 'git')
    def test_new_branch_uses_tree(self, git):
        git.side_effect = ['initial', 'src/a.cs\0']
        self.assertTrue(plan.plan_event('push', {'before': '0' * 40, 'after': 'b' * 40}, 'b' * 40)['validate'])
        git.assert_any_call('ls-tree', '-r', '--name-only', '-z', 'b' * 40)

    @patch.object(plan, 'git')
    def test_deleted_ref_does_no_work(self, git):
        self.assertFalse(plan.plan_event('push', {'deleted': True}, 'a' * 40)['validate'])
        git.assert_not_called()

    def test_workflow_keeps_explicit_windows_gate_and_bounded_time(self):
        text = (ROOT / '.github/workflows/ci.yml').read_text()
        self.assertNotIn('matrix:', text)
        self.assertIn("if: needs.plan.outputs.full == 'true'", text)
        self.assertIn('needs: [plan, validate]', text)
        self.assertIn('timeout-minutes: 30', text)
        self.assertIn('tools/build-local.ps1', text)
        self.assertIn("steps.candidate.outcome == 'success'", text)
        self.assertIn("steps.package_candidate.outputs.publish == 'true'", text)
        self.assertNotIn('artifacts/restore.log', text)

    def test_local_candidate_has_every_gate_before_packaging(self):
        text = (ROOT / 'tools/build-local.ps1').read_text()
        required = ('tools/localization/verify.py', 'Test-TestPackagePolicy.ps1',
                    'tests/CsvVisualEditor.Core.SmokeTests', 'tests/CsvVisualEditor.Core.Tests',
                    'tests/CsvVisualEditor.NativeAot.SmokeTests/CsvVisualEditor.NativeAot.SmokeTests.csproj',
                    'Invoke-Smoke.ps1', 'src/CsvVisualEditor/CsvVisualEditor.csproj',
                    'Invoke-HostReview.ps1', 'Invoke-LocalizedHostReview.ps1',
                    'tests/release', 'npp_plugin_package.py', 'LICENSE.txt', 'THIRD_PARTY_NOTICES.txt',
                    'Compress-Archive')
        positions = [text.index(value) for value in required]
        self.assertEqual(sorted(positions), positions)
        self.assertNotIn('--english-only', text)
        self.assertIn('if ($LASTEXITCODE -ne 0) { throw', text)
        self.assertIn('Push-Location $destination', text)
        self.assertIn("'-OutputPath', (Join-Path $destination 'host-review')", text)
        self.assertIn("Join-Path $layout 'CsvVisualEditor.dll'", text)
        self.assertNotIn("package/CsvVisualEditor'", text)

    def test_no_hosted_translation_or_transfer_workflows_remain(self):
        self.assertEqual(['ci.yml'], sorted(p.name for p in (ROOT / '.github/workflows').glob('*.yml')))


if __name__ == '__main__':
    unittest.main()
