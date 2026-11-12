"""Local synthetic tests; no Unity, APK, campaign files or manifests are opened."""
from pathlib import Path
import tempfile
import unittest

from review_evidence import validate_completed_report


def passed_rows(count):
    return '\n'.join(f'PASS synthetic assertion {index}' for index in range(count))


class CompletedReportTests(unittest.TestCase):
    def test_completed_native_rules_and_static_formats(self):
        for tail in ('', '. Rules only; no Unity scene or device execution.'):
            with self.subTest(tail=tail):
                self.assertTrue(validate_completed_report(
                    '\ufeffDETAIL fixture context\n' + passed_rows(3) +
                    '\nRESULT 3 passed, 0 failed' + tail + '\nUTC 2026-11-03T00:00:00Z')[0])

    def test_known_pass_only_baselines(self):
        for minimum in (12, 14, 16):
            with self.subTest(minimum=minimum):
                self.assertTrue(validate_completed_report(passed_rows(minimum), minimum)[0])
                self.assertFalse(validate_completed_report(passed_rows(minimum - 1), minimum)[0])

    def test_failed_unfinished_truncated_and_inconsistent_reports(self):
        rejected = (
            '', 'RUNNING 2026-11-03', 'PASS', passed_rows(1),
            passed_rows(2) + '\nFAIL real native navigation failed\nRESULT 2 passed, 1 failed',
            passed_rows(2) + '\nRESULT 2 passed, 1 failed',
            passed_rows(2) + '\nRESULT 2 passed, 0 failed\nRUNNING replacement run',
            passed_rows(2) + '\nPENDING native completion',
            passed_rows(2) + '\nERROR fixture exception',
            passed_rows(2) + '\nRESULT Succeeded',
            passed_rows(2) + '\nRESULT 0 passed, 0 failed',
            passed_rows(2) + '\nRESULT 3 passed, 0 failed',
            passed_rows(2) + '\nRESULT 2 passed, 0 failed\nRESULT 2 passed, 0 failed',
            passed_rows(2) + '\nRESULT 2 passed, 0 failed\nPASS later incomplete assertion',
        )
        for report in rejected:
            with self.subTest(report=report):
                self.assertFalse(validate_completed_report(report)[0])

    def test_running_or_failure_cannot_hide_in_pass_only_format(self):
        for marker in ('RUNNING', 'FAIL:', 'ERROR:', 'IN PROGRESS'):
            with self.subTest(marker=marker):
                self.assertFalse(validate_completed_report(passed_rows(16) + '\n' + marker, 16)[0])

    def test_assertion_description_does_not_trigger_status_marker(self):
        self.assertTrue(validate_completed_report(
            'PASS Pending purchase failure is handled\nRESULT 1 passed, 0 failed')[0])

    def test_failed_utf8_report_from_synthetic_temp_file(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            report = root / 'report.txt'
            report.write_text(passed_rows(3) + '\nRESULT 3 passed, 1 failed', encoding='utf-8')
            accepted, _ = validate_completed_report(report.read_text(encoding='utf-8-sig'))
            self.assertFalse(accepted)


if __name__ == '__main__':
    unittest.main()
