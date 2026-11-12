"""Inspect the generated review APK independently of Unity's build success flag."""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import hashlib
import json
import re
import subprocess
import zipfile
from review_evidence import validate_completed_report

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--tracking-polish', action='store_true', help='Verify the separate tracking/presentation review artifacts')
parser.add_argument('--guided-operations', action='store_true', help='Verify the separate first-use/crowd/Police operations review artifacts')
args = parser.parse_args()
full_inputs = args.tracking_polish or args.guided_operations
root = Path(__file__).resolve().parents[2]
android = Path(r'C:\Users\User\Desktop\D drive Data\Unity Game Engine\6000.0.80f1\Editor\Data\PlaybackEngines\AndroidPlayer')
apk_name = 'Hooligan-RTS-Guided-Operations-Review.apk' if args.guided_operations else 'Hooligan-RTS-Tracking-Polish-Review.apk' if args.tracking_polish else 'Hooligan-RTS-Recovery-Review.apk'
apk = root / 'Artifacts/Builds' / apk_name
out = root / 'Artifacts/Recovery'
prefix = 'guided-' if args.guided_operations else 'tracking-' if args.tracking_polish else ''
build_report_name = 'guided-android-build.txt' if args.guided_operations else 'tracking-android-build.txt' if args.tracking_polish else 'android-build.txt'
checks = []

def check(value, message):
    checks.append(('PASS ' if value else 'FAIL ') + message)

def run(args):
    result = subprocess.run([str(arg) for arg in args], capture_output=True, text=True)
    return result.returncode, result.stdout + result.stderr

if not apk.is_file():
    method = 'RecoveryBuild.AndroidTrackingPolish' if args.tracking_polish else 'RecoveryBuild.Android'
    raise SystemExit(f'Review APK is missing; run {method} first.')

with zipfile.ZipFile(apk) as package:
    check(package.testzip() is None, 'APK entries pass ZIP integrity checks')
    check('lib/arm64-v8a/libil2cpp.so' in package.namelist(), 'ARM64 IL2CPP runtime is packaged')
    dex = b''.join(package.read(name) for name in package.namelist() if re.fullmatch(r'classes\d*\.dex', name))
    check(b'Lcom/google/android/gms/ads/MobileAds;' in dex, 'Native Google Mobile Ads entry point is packaged')
    check(b'Lcom/google/android/ump/UserMessagingPlatform;' in dex, 'Native UMP consent entry point is packaged')
    check(b'Lcom/android/billingclient/api/BillingClient;' in dex, 'Native Google Play Billing client is packaged')

build_tools = android / 'SDK/build-tools/36.0.0'
code, badging = run([build_tools / 'aapt.exe', 'dump', 'badging', apk])
check(code == 0 and 'launchable-activity:' in badging, 'Android package and launchable activity can be read')
code, manifest = run([build_tools / 'aapt.exe', 'dump', 'xmltree', apk, 'AndroidManifest.xml'])
config = (root / 'Assets/Resources/Ads/AdsConfiguration.asset').read_text(encoding='utf-8-sig')
test_id = re.search(r'android:\s*\n\s*testAppId:\s*(\S+)', config)
check(code == 0 and test_id is not None and test_id.group(1) in manifest,
      'Packaged Android AdMob App ID matches the project Test configuration')
check('android.permission.INTERNET' in manifest, 'Required network permission is present')
code, signature = run([android / 'OpenJDK/bin/java.exe', '-jar', build_tools / 'lib/apksigner.jar', 'verify', '--verbose', apk])
check(code == 0, 'APK signature verifies (review signing; no store certification)')

reports = {
    'home': 'Artifacts/Recovery/redesign-playtest.txt',
    'police': 'Artifacts/Recovery/police-playtest.txt',
    'rts': 'Artifacts/CityQA/rts-controls-playtest.txt',
    'strategy':'Artifacts/Recovery/production-strategy-playtest.txt',
    'vehicle':'Artifacts/Recovery/police-vehicle-playtest.txt',
    'commerce':'Artifacts/Recovery/commerce-playtest.txt',
    'purchases':'Artifacts/Recovery/purchases-checks.txt',
    'build': f'Artifacts/Recovery/{build_report_name}',
}
if full_inputs:
    reports.update({
        'home_visual': 'Artifacts/Recovery/home-visual-playtest.txt',
        'train_raid': 'Artifacts/Recovery/train-raid-playtest.txt',
        'crew_tracking': 'Artifacts/Recovery/crew-tracking-playtest.txt',
        'tactical_map': 'Artifacts/Recovery/tactical-map-playtest.txt',
        'presentation_feedback': 'Artifacts/Recovery/presentation-feedback-playtest.txt',
        'presentation_audit': 'Artifacts/Recovery/presentation-static-audit.txt',
        'rules': 'Artifacts/Recovery/rules-checks.txt',
        'menu': 'Artifacts/Milestone01/playtest-checks.txt',
        'saves': 'Artifacts/Recovery/unity-save-checks.txt',
        'ads': 'Artifacts/Recovery/ads-checks.txt',
        'editor_telemetry': 'Artifacts/Recovery/crew-tracking-editor-telemetry.json',
    })
if args.guided_operations:
    reports.update({
        'guided_tutorial':'Artifacts/Recovery/guided-tutorial-playtest.txt',
        'police_operations':'Artifacts/Recovery/police-operations-playtest.txt',
        'road_incident':'Artifacts/Recovery/road-incident-playtest.txt',
    })
sources = [
    'Assets/Scripts/UI/Landscape/ModernCityHud.cs',
    'Assets/Scripts/Battle/HomeTerritorySystem.cs',
    'Assets/Scripts/Battle/AgentController.cs',
    'Assets/Scripts/Battle/AgentSelectionManager.cs',
    'Assets/Scripts/Battle/RtsGestureController.cs',
    'Assets/Scripts/Battle/PoliceCityMode.cs',
    'Assets/Scripts/SaveDataInLocal/PoliceCampaignStore.cs',
    'Assets/Resources/Ads/AdsConfiguration.asset',
    'Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset',
    'Packages/manifest.json',
    'Assets/Scripts/Battle/MatchdayStrategySystem.cs',
    'Assets/Scripts/Battle/MatchdayTactics.cs',
    'Assets/Scripts/Battle/TerritoryControlPoint.cs',
    'Assets/Scripts/Battle/PolicePatrolVehicle.cs',
    'Assets/Scripts/Battle/CityBusinessSystem.cs',
    'Assets/Scripts/Battle/CityCommerceSystem.cs',
    'Assets/Scripts/Monetization/GamePurchasesService.cs',
    'Assets/Resources/Purchases/PurchasesConfiguration.asset',
]
build_report = (root / reports['build']).read_text(encoding='utf-8-sig')
check('Result: Succeeded' in build_report and 'Errors: 0' in build_report, 'Unity build report records success with zero errors')
build_time = re.search(r'^UTC: (.+)$', build_report, re.M)
build_end = datetime.fromisoformat(build_time.group(1).strip().replace('Z', '+00:00')) if build_time else None
def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(chunk)
    return value.hexdigest()
# Tracking builds fingerprint every Unity input, including imported OGG audio,
# material/prefab/scene assets, plugin binaries and all .meta import settings.
# The legacy profile retains its existing source list and output identities.
if full_inputs:
    source_paths = sorted(path for leaf in ('Assets', 'ProjectSettings', 'Packages')
                          for path in (root / leaf).rglob('*') if path.is_file())
    sources = [path.relative_to(root).as_posix() for path in source_paths]
else:
    source_paths = list((root / 'Assets/Scripts').rglob('*.cs')) + [root / path for path in sources]
source_hashes = {path: digest(root / path) for path in sources}
latest_source = max(path.stat().st_mtime for path in source_paths)
check(build_end is not None and latest_source <= build_end.timestamp()
      and 0 <= build_end.timestamp() - apk.stat().st_mtime < 600,
      'APK and build report are newer than the reviewed implementation sources')
proof_path = out / f'{prefix}isolated-build-source-check.json'
if full_inputs:
    proof = json.loads(proof_path.read_text(encoding='utf-8-sig')) if proof_path.is_file() else {}
    expected_hashes = {path.replace('\\', '/'): value.lower()
                       for path, value in proof.get('source_inputs_sha256', {}).items()}
    check(proof.get('source_and_copy_unchanged') is True
          and proof.get('execute_method') == 'RecoveryBuild.AndroidTrackingPolish'
          and proof.get('apk') == apk_name and proof.get('build_report') == build_report_name,
          'Tracking build provenance identifies the selected APK and unchanged isolated inputs')
    check(expected_hashes == source_hashes,
          'Every current asset, import setting, script, package and project setting matches the build provenance')
    reports['build_provenance'] = str(proof_path.relative_to(root).as_posix())
for label, path in reports.items():
    check((root / path).is_file(), f'{label} evidence report is present')
    if full_inputs and label not in {'build', 'build_provenance', 'editor_telemetry'}:
        # Each suite certifies its own reviewed behavior. An unrelated later
        # fixture or UI edit does not invalidate an unchanged Home suite by age.
        # The build's all-input hashes still enforce APK/source identity above.
        try:
            text = (root / path).read_text(encoding='utf-8-sig')
            accepted, detail = validate_completed_report(text, {'menu': 16, 'saves': 12, 'ads': 14}.get(label, 0))
        except (OSError, UnicodeError) as error:
            accepted, detail = False, f'unreadable evidence ({type(error).__name__})'
        check(accepted, f'{label} evidence completed successfully: {detail}')
evidence_hashes = {label: digest(root / path) for label, path in reports.items() if (root / path).is_file()}
delivery = {
    'inspected_utc': datetime.now(timezone.utc).isoformat(),
    'apk': str(apk), 'apk_bytes': apk.stat().st_size, 'apk_sha256': digest(apk),
    'package_badging': badging.splitlines(),
    'signature_verification': signature.splitlines(),
    'variant': 'guided-operations' if args.guided_operations else 'tracking-polish' if args.tracking_polish else 'recovery',
    'source_sha256': source_hashes,
    'evidence_sha256': evidence_hashes,
    'physical_device_validation': 'not performed',
    'network_test_ads_validation': 'not performed',
    'production_release_certified': False,
}
(out / f'{prefix}apk-package-checks.txt').write_text('\n'.join(checks) + '\n', encoding='utf-8')
# Failed verification must not replace a previous successful delivery manifest.
if not any(row.startswith('FAIL ') for row in checks):
    (out / f'{prefix}review-delivery-manifest.json').write_text(json.dumps(delivery, indent=2), encoding='utf-8')
print('\n'.join(checks))
print(f'APK bytes: {apk.stat().st_size}; SHA256: {delivery["apk_sha256"]}')
raise SystemExit(any(row.startswith('FAIL ') for row in checks))
