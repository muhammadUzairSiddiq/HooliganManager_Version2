"""Inspect the generated review APK independently of Unity's build success flag."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import re
import subprocess
import zipfile

root = Path(__file__).resolve().parents[2]
android = Path(r'C:\Users\User\Desktop\D drive Data\Unity Game Engine\6000.0.80f1\Editor\Data\PlaybackEngines\AndroidPlayer')
apk = root / 'Artifacts/Builds/Hooligan-RTS-Recovery-Review.apk'
out = root / 'Artifacts/Recovery'
checks = []

def check(value, message):
    checks.append(('PASS ' if value else 'FAIL ') + message)

def run(args):
    result = subprocess.run([str(arg) for arg in args], capture_output=True, text=True)
    return result.returncode, result.stdout + result.stderr

if not apk.is_file():
    raise SystemExit('Review APK is missing; run RecoveryBuild.Android first.')

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
    'build': 'Artifacts/Recovery/android-build.txt',
}
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
latest_source = max(path.stat().st_mtime for path in (root / 'Assets/Scripts').rglob('*.cs'))
latest_source = max(latest_source, max((root / path).stat().st_mtime for path in sources))
check(build_end is not None and latest_source <= build_end.timestamp()
      and 0 <= build_end.timestamp() - apk.stat().st_mtime < 600,
      'APK and build report are newer than the reviewed implementation sources')
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
delivery = {
    'inspected_utc': datetime.now(timezone.utc).isoformat(),
    'apk': str(apk), 'apk_bytes': apk.stat().st_size, 'apk_sha256': digest(apk),
    'package_badging': badging.splitlines(),
    'signature_verification': signature.splitlines(),
    'source_sha256': {path: digest(root / path) for path in sources},
    'evidence_sha256': {label: digest(root / path) for label, path in reports.items()},
    'physical_device_validation': 'not performed',
    'network_test_ads_validation': 'not performed',
    'production_release_certified': False,
}
(out / 'review-delivery-manifest.json').write_text(json.dumps(delivery, indent=2), encoding='utf-8')
(out / 'apk-package-checks.txt').write_text('\n'.join(checks) + '\n', encoding='utf-8')
print('\n'.join(checks))
print(f'APK bytes: {apk.stat().st_size}; SHA256: {delivery["apk_sha256"]}')
raise SystemExit(any(row.startswith('FAIL ') for row in checks))
