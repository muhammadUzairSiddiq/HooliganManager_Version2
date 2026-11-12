"""Pure completion checks for the recovery tools' text assertion reports."""
import re


def validate_completed_report(text, minimum_pass_only=0):
    """Return (accepted, explanation); a positive count alone is not completion.

    Most suites must have one consistent RESULT summary. Only explicitly known
    pass-only formats may omit it, and those must contain their complete baseline
    assertion count. This intentionally makes no cross-suite timestamp claim.
    """
    lines = [line.strip() for line in text.lstrip('\ufeff').splitlines() if line.strip()]
    unfinished = next((line for line in lines if re.match(
        r'^(?:FAIL|ERROR|RUNNING|PENDING|IN\s+PROGRESS)\b', line, re.I)), None)
    if unfinished:
        return False, 'failed or unfinished evidence: ' + unfinished[:160]
    passes = sum(bool(re.match(r'^PASS\s+\S', line)) for line in lines)
    if not passes:
        return False, 'no positive PASS assertions'
    results = [line for line in lines if re.match(r'^RESULT\b', line, re.I)]
    if not results:
        if minimum_pass_only and passes >= minimum_pass_only:
            return True, f'{passes} PASS assertions; recognized completed pass-only format'
        return False, (f'incomplete pass-only report: {passes}/{minimum_pass_only} assertions'
                       if minimum_pass_only else 'missing completed RESULT summary')
    if len(results) != 1:
        return False, 'expected exactly one completed RESULT summary'
    summary = re.match(r'^RESULT\s+(\d+)\s+passed,\s*(\d+)\s+failed\b', results[0], re.I)
    if not summary:
        return False, 'malformed RESULT summary'
    passed, failed = map(int, summary.groups())
    if failed:
        return False, f'RESULT records {failed} failed assertions'
    if passed <= 0 or passed != passes:
        return False, f'RESULT PASS count {passed} disagrees with {passes} positive assertions'
    if minimum_pass_only and passed < minimum_pass_only:
        return False, f'incomplete baseline assertion count: {passed}/{minimum_pass_only}'
    result_index = lines.index(results[0])
    if any(re.match(r'^PASS\s+\S', line) for line in lines[result_index + 1:]):
        return False, 'assertions continue after the completion summary'
    return True, f'{passed} completed PASS assertions, zero failures'
