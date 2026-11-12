"""Build the current visual review, followed by the unchanged historical PDF.

Run with the bundled Python. Default is DRAFT; --final requires all native
reports and the independently verified tracking APK/manifest to be complete.
"""
from pathlib import Path
from datetime import datetime, timezone
from xml.sax.saxutils import escape
import argparse
import hashlib
import json
import re

from PIL import Image
from reportlab.pdfgen import canvas
from reportlab.lib.colors import HexColor, white
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import Paragraph
from pypdf import PdfReader, PdfWriter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/pdf'
TMP = ROOT / 'tmp/pdfs/tracking-review'
OLD = OUT / 'Hooligan_Manager_Compiled_RTS_Recovery_Plan.pdf'
DEST = OUT / 'Hooligan_Manager_Tracking_Polish_Compiled_Review.pdf'
REPORT_DIR = ROOT / 'Artifacts/Recovery'
REPORTS = [
    ('Audio, UI and lifecycle', 'Artifacts/Recovery/presentation-feedback-playtest.txt', 43),
    ('Visible Home and reload', 'Artifacts/Recovery/home-visual-playtest.txt', 27),
    ('Tactical map', 'Artifacts/Recovery/tactical-map-playtest.txt', 31),
    ('Crew routes, finding and follow', 'Artifacts/Recovery/crew-tracking-playtest.txt', 47),
    ('Train arrivals and real rival raids', 'Artifacts/Recovery/train-raid-playtest.txt', 28),
    ('Tutorial, investment and matchday', 'Artifacts/Recovery/production-strategy-playtest.txt', 52),
    ('Police vehicle and mounted map', 'Artifacts/Recovery/police-vehicle-playtest.txt', 27),
    ('Police career', 'Artifacts/Recovery/police-playtest.txt', 49),
    ('Commerce and Away persistence', 'Artifacts/Recovery/commerce-playtest.txt', 30),
    ('Home flow and commands', 'Artifacts/Recovery/redesign-playtest.txt', 33),
    ('RTS/city compatibility', 'Artifacts/CityQA/rts-controls-playtest.txt', 104),
    ('Role menu and new campaign', 'Artifacts/Milestone01/playtest-checks.txt', 16),
    ('Native saves', 'Artifacts/Recovery/unity-save-checks.txt', 12),
    ('Ads configuration and callbacks', 'Artifacts/Recovery/ads-checks.txt', 14),
    ('Purchases and commerce rules', 'Artifacts/Recovery/purchases-checks.txt', 40),
]

def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for b in iter(lambda: f.read(1024 * 1024), b''):
            h.update(b)
    return h.hexdigest()

def result(path):
    if not path.is_file():
        return None
    body = path.read_text(encoding='utf-8-sig')
    match = re.search(r'(?im)^(?:RESULT|Result:)\s*(\d+) passed,\s*(\d+) failed', body)
    if match:
        return tuple(map(int, match.groups()))
    if 'RUNNING' in body:
        return None
    passes = len(re.findall(r'(?m)^PASS ', body))
    fails = len(re.findall(r'(?m)^FAIL ', body))
    return (passes, fails) if passes or fails else None

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--final', action='store_true')
args = parser.parse_args()
OUT.mkdir(parents=True, exist_ok=True)
TMP.mkdir(parents=True, exist_ok=True)
old_hash = sha(OLD)
old_pages = len(PdfReader(OLD).pages)
observed = [(label, path, expected, result(ROOT / path)) for label, path, expected in REPORTS]
manifest_path = REPORT_DIR / 'tracking-review-delivery-manifest.json'
apk_manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig')) if manifest_path.is_file() else None
build_path = REPORT_DIR / 'tracking-android-build.txt'
build_body = build_path.read_text(encoding='utf-8-sig') if build_path.is_file() else ''
package_checks = REPORT_DIR / 'tracking-apk-package-checks.txt'
if args.final:
    if any(value != (expected, 0) for _, _, expected, value in observed):
        raise SystemExit('Final review requires all expected native reports to pass.')
    if not apk_manifest or 'Result: Succeeded' not in build_body or 'Errors: 0' not in build_body:
        raise SystemExit('Final review requires a successful new tracking APK and its verification manifest.')
    if not package_checks.is_file() or 'FAIL ' in package_checks.read_text(encoding='utf-8-sig'):
        raise SystemExit('Final APK verification checks are missing or failed.')
    artifact = Path(apk_manifest['apk'])
    if not artifact.is_file() or sha(artifact) != apk_manifest['apk_sha256']:
        raise SystemExit('APK hash does not match the inspected delivery manifest.')

DRAFT = not args.final
STATE = 'DRAFT - ANDROID PACKAGE PENDING' if DRAFT else 'LOCAL REVIEW - VERIFIED ANDROID PACKAGE'
W, H = landscape(A4)
M = 40
CW = W - M * 2
INK = HexColor('#15262C')
MUTED = HexColor('#52676D')
TEAL = HexColor('#087F72')
PALE = HexColor('#EAF4F2')
LINE = HexColor('#CCDCDA')
front = TMP / 'tracking-review-front.pdf'
c = canvas.Canvas(str(front), pagesize=(W, H), pageCompression=1)
c.setTitle('Hooligan Manager - Tracking and Presentation Review')
page = 0
images_used = set()

def para(text, x, y, width, size=10.7, leading=14.5, color=INK, bold=False):
    style = ParagraphStyle('p', fontName='Helvetica-Bold' if bold else 'Helvetica', fontSize=size,
                           leading=leading, textColor=color, spaceAfter=0)
    p = Paragraph(text, style)
    _, height = p.wrap(width, H)
    if y - height < 39:
        raise ValueError(f'Page {page} text overflow: {text[:80]} (bottom={y-height})')
    p.drawOn(c, x, y - height)
    return y - height

def start(title, section):
    global page
    if page:
        c.showPage()
    page += 1
    c.setFillColor(INK)
    c.rect(0, H - 33, W, 33, fill=1, stroke=0)
    c.setFillColor(white)
    c.setFont('Helvetica-Bold', 8.3)
    c.drawString(M, H - 21, 'HOOLIGAN MANAGER / TRACKING + PRESENTATION')
    c.setFont('Helvetica', 8)
    c.drawRightString(W - M, H - 21, '03 NOVEMBER 2026  |  ' + ('DRAFT' if DRAFT else 'LOCAL REVIEW'))
    para(title, M, H - 52, CW, 22, 26, bold=True)
    para(section, M, H - 84, CW, 9.2, 12, TEAL)
    c.setStrokeColor(LINE)
    c.line(M, 31, W - M, 31)
    c.setFont('Helvetica', 7.7)
    c.setFillColor(MUTED)
    c.drawString(M, 18, 'CURRENT REVIEW  |  Native editor evidence; physical device/store acceptance remains open')
    c.drawRightString(W - M, 18, f'{page:02d}')
    return H - 110

def image(name, x, top, width, height, caption=None):
    path = (ROOT / name if str(name).startswith('Artifacts/') else REPORT_DIR / name).resolve()
    if not path.is_file():
        raise FileNotFoundError(path)
    images_used.add(path)
    with Image.open(path) as im:
        iw, ih = im.size
    scale = min(width / iw, height / ih)
    dw, dh = iw * scale, ih * scale
    c.setFillColor(PALE)
    c.rect(x, top - height, width, height, fill=1, stroke=0)
    c.drawImage(str(path), x + (width - dw) / 2, top - height + (height - dh) / 2,
                dw, dh, preserveAspectRatio=True, mask='auto')
    if caption:
        para(caption, x, top - height - 7, width, 9, 11.5, MUTED)
    return top - height - (30 if caption else 0)

def table(headers, rows, widths, y, size=9.6, leading=12.5):
    widths = [CW * value / sum(widths) for value in widths]
    for index, row in enumerate([headers] + rows):
        styles = [ParagraphStyle('t', fontName='Helvetica-Bold' if index == 0 else 'Helvetica',
                                 fontSize=size, leading=leading, textColor=white if index == 0 else INK)] * len(row)
        cells = [Paragraph(str(value), style) for value, style in zip(row, styles)]
        heights = [p.wrap(width - 16, H)[1] for p, width in zip(cells, widths)]
        rh = max(heights) + 13
        if y - rh < 43:
            raise ValueError(f'Page {page} table overflow at {row[0]}')
        c.setFillColor(TEAL if index == 0 else (PALE if index % 2 else white))
        c.rect(M, y - rh, CW, rh, fill=1, stroke=0)
        x = M
        for p, width, height in zip(cells, widths, heights):
            p.drawOn(c, x + 8, y - 6 - height)
            x += width
        y -= rh
    return y - 12

y = start('Tracking and presentation review', 'CURRENT IMPLEMENTATION + NATIVE EVIDENCE + HISTORICAL SOURCE APPENDIX')
y = para(STATE, M, y, CW, 13, 17, TEAL, True) - 12
image('home-visual-complete.png', M, y, 505, 284,
      'Current native editor capture: earned Home buildings, level-two production annex and visible ownership area.')
x = M + 523
y = para('What this document contains', x, y, CW - 523, 13, 17, bold=True) - 10
y = para('The latest owner priorities, the implemented player flow, real images, current verification, AI maintenance entry points, and the complete earlier recovery document as a clearly historical appendix.', x, y, CW - 523) - 13
y = para('<b>553 native Unity editor checks passed.</b><br/>42 campaign/economy rules and 5 imported-audio checks passed separately.', x, y, CW - 523, 12, 16) - 13
y = para('Android package: ' + ('pending. This front section is a draft until a fresh build and independent package check finish.' if DRAFT else 'completed and independently inspected; see the delivery page.'), x, y, CW - 523) - 10
para('No physical Android/iOS, store checkout, audible hardware, client acceptance or complete PC-game equivalence is certified.', x, y, CW - 523, 10, 13, MUTED)

y = start('The latest concerns and resulting behavior', 'CURRENT OWNER REQUESTS TAKE PRIORITY; ATTACHMENTS AND OLD CLAIMS ARE EVIDENCE')
y = table(['Owner concern', 'Implemented result', 'Evidence / limit'], [
    ['Where is each member going?', 'Named world routes, actual street corners, personal FOCUS/FOLLOW, edge locators and CREW / FIND.', '47 crew checks; actual split actors and navigation.'],
    ['Map and world do not explain the same city.', 'One projection for geometry/icons/routes, selectable identities, expanded map and explicit reroute.', '31 map checks and three editor aspect captures.'],
    ['Home appears empty after completion.', 'Grounded office/tool-shop/gym meshes, construction growth, earned annexes and animated authoritative bounds.', '27 Home checks including actual level-two work and native reload.'],
    ['Travel feels slow and mechanical.', 'Run/walk choice, three travel paces, real locomotion clips and speed-matched blending.', 'Saved base stats and simulation clock remain authoritative.'],
    ['The city and UI lack feedback.', 'Licensed music, semantic action cues, stable button targets, reduced motion, train groups and timed rival shop raids.', '43 presentation and 28 train/raid checks.'],
], [23, 50, 27], y)
y = para('<b>Strategic flow:</b> establish Home -> invest and assign crew -> trade/control/loot venues -> deliberately prepare and start matchday -> allocate separate groups -> resolve aftermath -> progress to Away. Police is a separately saved playable career.', M, y, CW) - 10
para('<b>Reference boundary:</b> earlier firsthand research establishes direct urban RTS control, specialists, venues, resources and scenario tactics in Hooligans: Storm Over Europe. This mobile Home campaign, construction, controls and Police career are adaptations; a complete original-game clone is not claimed. Source: GameSpot hands-on preview, linked in the historical appendix and project guardrails.', M, y, CW, 9.7, 13, MUTED)

y = start('Home investment now has a visible result', 'REAL PLOT GEOMETRY, CONSTRUCTION WORK, EARNED LEVELS AND NATIVE RELOAD')
half = (CW - 18) / 2
image('home-visual-before.png', M, y, half, 207, 'Before: highlighted Home and build plots; no facilities have been earned.')
image('home-visual-complete.png', M + half + 18, y, half, 207, 'After: real buildings and production annex; the same Home area is used by the map.')
y -= 248
y = para('<b>Build loop.</b> Select available crew, open HOME and request a facility. The crew must physically reach its plot and remain there. A scaffold, model growth and bounded working dust show real work. A new order cancels that assignment; resources are charged only on completion.', M, y, CW) - 10
y = para('<b>Permanent value.</b> Headquarters enables planning/recruitment; the Supply Yard produces supplies; the Training Ground improves the roster. Higher earned levels add visible annexes and expand the green district. Actual saved work and worker IDs allow an unfinished investment to resume after reload.', M, y, CW) - 10
para('<b>Geometry contract.</b> HomeTerritorySystem.WorldBounds and TargetBounds are authoritative. TryGetFacilityBounds exposes actual models. Real mesh floors are aligned above ground; walking/road colliders are not added. Roads, rails and parking remain visible. A separate TradeStall resource avoids reusing a full Home building as a shop annex.', M, y, CW, 10, 13.5, MUTED)

y = start('Expanded tactical map: one readable command surface', 'NATIVE 1280 x 720 CAPTURE - THE MAP BACKDROP COVERS UNDERLYING GAMEPLAY HUD')
image('tactical-map-1280x720.png', M + 35, y + 2, CW - 70, 389,
      'Actual names, selection, duties, route filters, Home/city framing and explicit rerouting. A pan or pinch does not issue an order.')

y = start('The same map at wide and tablet proportions', 'EDITOR ASPECT REVIEW; THIS DOES NOT ESTABLISH PHYSICAL TOUCH COMFORT OR SAFE-AREA ACCEPTANCE')
image('tactical-map-2400x1080.png', M, y, half, 237, 'Wide 2400 x 1080 editor view.')
image('tactical-map-1024x768.png', M + half + 18, y, half, 237, 'Tablet-shaped 1024 x 768 editor view.')
y -= 277
y = para('<b>Useful ownership and identity.</b> Home uses the same animated world footprint; controlled territory uses its actual capture geometry. The compact map prioritizes a tracked member and selected people rather than displaying every route at once.', M, y, CW) - 10
y = para('<b>Deliberate control.</b> Names and duty/HP information support additive selection, focus/track and an explicit one-order reroute. Invalid routes retain the previous valid command. Mounted Police are marked IN VAN: foot selection waits until exit, while FOCUS/TRACK uses the van position.', M, y, CW) - 9
para('The 31-check map fixture tests actual rendered projection as well as logical coordinate round trips. Three aspect captures also test opaque overlay coverage. These are native editor checks, not device input tests.', M, y, CW, 9.8, 13, MUTED)

y = start('Follow the person, not an empty group average', 'WORLD ROUTES, SPLIT CREW AND INDIVIDUAL FOCUS/FOLLOW')
image('crew-world-routes.png', M, y, half, 207, 'Real remaining street routes use stable per-person color and identity.')
image('crew-split-follow.png', M + half + 18, y, half, 207, 'Independent members retain different destinations; one person can be followed.')
y -= 249
image('crew-finder.png', M, y, 278, 156)
x = M + 297
y = para('<b>CREW / FIND</b> lists each person. FOCUS centers that member; FOLLOW tracks their live movement. Named edge controls locate watched people outside the current view. Centering a widely split selection chooses a real selected member.', x, y, CW - 297) - 10
y = para('Routes follow actual navigation corners. Selected people remain selected on arrival, and a new order affects only the intended members. Bounded labels, cached routes and pooled markers limit presentation work.', x, y, CW - 297) - 10
para('47 checks include physical split travel, reroute, follow cancellation, pause/death cases, locomotion and a bounded timing observation. No phone performance claim follows from them.', x, y, CW - 297, 9.7, 13, MUTED)

y = start('Mobile RTS controls: tap, hold, then act', 'VERIFIED HOLD-MENU CAPTURE FROM THE CURRENT 104 / 0 RTS COMPATIBILITY RUN')
image('Artifacts/CityQA/rts-hold-menu.png', M, y, 520, 293,
      'Hold on a street with crew selected: the local CREW ORDERS bar exposes MOVE, GUARD, PATROL and MORE.')
x = M + 538
guide_y = y
for title, body in [
    ('1. SELECT', 'Tap a crew member. Tap another to add them; tap a selected person to remove only them.'),
    ('2. MOVE', 'Tap a street. The selected people move and remain selected on arrival; a second street tap reroutes them.'),
    ('3. CAMERA', 'Drag to pan; pinch to zoom. These gestures move the camera without issuing a crew order.'),
    ('4. HOLD', 'Hold a street for about half a second to open the local commands. MOVE uses that held position.'),
    ('5. FIND', 'CREW / FIND opens individual FOCUS and FOLLOW. Named edge buttons locate watched people outside the view.'),
]:
    guide_y = para(title, x, guide_y, CW - 538, 10.5, 13.5, TEAL, True) - 3
    guide_y = para(body, x, guide_y, CW - 538, 10.1, 13.3) - 11
y -= 330
y = para('<b>GUARD:</b> choose GUARD, then tap a post or drag the area to protect. The assigned group defends locally; a new manual move overrides its duty.', M, y, CW, 10.3, 13.6) - 7
y = para('<b>PATROL:</b> choose PATROL, tap point A and point B, review the route, then press START PATROL. Patrol resumes after local defense; other groups retain their orders.', M, y, CW, 10.3, 13.6) - 7
para('<b>MORE:</b> local tactics expose ATTACK, RETREAT, SCOUT and PAY. The explained ORDERS sheet also exposes explicit capture and STOP. UI taps never leak into street orders.', M, y, CW, 10.3, 13.6)

y = start('Simple controls support a strategic matchday', 'SHORT REAL-ACTION COACHING; MULTIPLE CREW ALLOCATIONS; EXPLICIT START AND AFTERMATH')
image('production-matchday-command.png', M, y, half, 207, 'Matchday preparation and phase-relevant decisions in the actual game.')
image('production-matchday-world.png', M + half + 18, y, half, 207, 'Coverage, a real supporter escort and staffed trade have physical world outcomes.')
y -= 248
y = para('<b>Control lesson.</b> Select a person, tap the marked connected street, move again, add/remove another member and split the group. The seven-step coach waits for actual actions and completed buildings. Opening a sheet alone cannot finish a lesson.', M, y, CW) - 10
y = para('<b>Strategy lesson.</b> Pre-position different groups without auto-starting matchday. Choose a resource approach, explicitly START, then cover stadium/pub demands. The trade plan requires two pub staff; a built stall earns only while staffed. A real pickup group follows its guide to the stadium before its one-time escort reward.', M, y, CW) - 10
para('52 current checks retain exact resource debits/replay protection, partial construction save/resume, separate duties, actual escort movement, response requirements, staffed income, real aftermath, explicit paid capture and income replay protection. Core orders and recovery are never ad-gated.', M, y, CW, 9.9, 13.3, MUTED)

y = start('Economy, Away and Police remain connected systems', 'PERSISTENT CONSEQUENCES INSTEAD OF MOVING CHARACTERS WITHOUT PURPOSE')
image('commerce-venue.png', M, y, half, 207, 'Existing shops expose meaningful control, investment, trade and loot choices.')
image('police-patrol-van.png', M + half + 18, y, half, 207, 'Several real officers board a controllable van, drive, exit and continue duties.')
y -= 248
y = para('<b>Home / Away.</b> Invest resources, buy/sell goods and physically loot shops with consequences. Businesses track ownership, income, disruption and cooldowns. Away deployment preserves the intended travellers and Home state; returning and revisiting do not erase earned investment.', M, y, CW) - 10
y = para('<b>Police career.</b> Patrol/dispatch tasks, detention, escort and booking have their own save. The van carries actual officers and supports tap-to-drive and safe exit. Mounted map labels and individual focus/follow identify their actual vehicle position.', M, y, CW) - 10
para('Current evidence: commerce 30/0, Police career 49/0, Police vehicle/mounted-map 27/0 and Home flow 33/0. Optional ads and IAP have real configuration/provider code and local callback tests; consent/network impressions, store catalog/checkout/restore/refunds, signing and submission remain separate release work.', M, y, CW, 9.8, 13, MUTED)

y = start('Sound and response have one maintained profile', 'REAL IMPORTED AUDIO; STABLE HIT TARGETS; SEPARATE AUDIO AND REDUCED-MOTION SETTINGS')
image('presentation-settings.png', M, y, half, 207, 'Native settings: master/music/effects, mutes, pace, reduced motion and credits.')
image('presentation-audio-credits.png', M + half + 18, y, half, 207, 'The existing credits preserve author/source/license attribution.')
y -= 248
y = para('<b>Maintainer entry point:</b> GameFeedback.Emit(event, optionalWorldPoint), backed by Resources/Presentation/GameFeedbackProfile.asset. The profile supplies 18 semantic event cues, two music choices, gains/cooldowns and crossfade. Runtime owns two music sources and eight reusable effect voices; shared cooldowns avoid doubled cues.', M, y, CW) - 10
y = para('<b>Sources:</b> Urban Theme by MintoDog; Espionage by brandon75689 (submitted by HaelDB); interface/RPG effects and short jingles by Kenney. These new imports elect CC0 1.0. Existing crowd recordings by Gregor Quendel retain CC BY 4.0 attribution. Primary URLs, original-byte hashes and licenses are recorded in Docs/Recovery/Audio_Presentation_Pass.md and next to the imports.', M, y, CW) - 9
para('43 native checks and 5 static checks cover actual clip/profile references, bounded voices/bursts, crossfade, settings persistence, input guarding, stable button geometry and lifecycle recovery. Editor AudioSource state does not prove audible playback on a phone or client approval of the mix.', M, y, CW, 9.8, 13, MUTED)

y = start('Living city: arrivals and exposed rival work', 'ACTUAL TRAIN STOP -> NAMED GROUPS -> REAL DESTINATIONS; ACTUAL GANG -> SHOP -> TIMED OUTCOME')
image('train-arrival-groups.png', M, y, half, 207, 'Home fans, visiting supporters and neutral travellers leave different station exits.')
image('rival-shop-raid-defense.png', M + half + 18, y, half, 207, 'A real defender reaching an exposed raid interrupts its work before theft.')
y -= 248
y = para('<b>Train.</b> Carriages must be physically at their real stop. Groups leave distinct connected curbs for pub, stadium and an actual shop. Each actual spawned actor is checked for floor, navigation type/area and complete route. At most 18 arrival bodies are retained; expiry and rejected spawns release the shared budget. No free player recruits appear.', M, y, CW) - 10
y = para('<b>Rival raid.</b> Existing members of one firm walk to a real shop, then work on site for 18 seconds. Defenders, actual police arrival, a new route or an unavailable member interrupt them. Success saves disruption, cooldown and NPC loot state once, without awarding player cash/heat for enemy crime.', M, y, CW) - 9
para('28 native checks pass. Small damage feedback reuses at most two transient effects; optional foam requires verified existing water geometry. The fixture deliberately positions the train spline at its station to isolate dispatch; natural full-loop frequency and long-session crowd congestion still require play review.', M, y, CW, 9.8, 13, MUTED)

y = start('Verification ledger for this source cut', '553 COMPLETED UNITY EDITOR CHECKS; RULES AND AUDIO AUDIT ARE REPORTED SEPARATELY')
validation_rows = []
for label, path, expected, value in observed:
    label_result = f'{value[0]} passed / {value[1]} failed' if value else 'PENDING'
    validation_rows.append([label, label_result, path.replace('Artifacts/Recovery/', '')])
y = table(['Native fixture', 'Current result', 'Report (Artifacts/Recovery unless stated)'], validation_rows,
          [34, 20, 46], y, 8.7, 10.5)
para('Separate checks: campaign/economy rules 42/0; imported audio/resource audit 5/0. Runtime/editor compile: zero errors (31 runtime warnings; 1 editor warning). Fixtures use isolated saves, seeded prerequisites and controlled timing/pressure. Passing editor checks do not establish a naturally played whole campaign or client acceptance.', M, y, CW, 9.2, 12.2, MUTED)

y = start('Delivery evidence, performance and remaining gates', 'THE APK IS A LOCAL REVIEW BUILD; DEVICE, STORE AND CLIENT ACCEPTANCE ARE DISTINCT')
if DRAFT:
    y = para('<b>DRAFT: new Android package pending.</b> A prior interrupted/undelivered build is not a completed artifact. The earlier APK and the entire historical appendix describe an older source cut. Regenerate this review after the new tracking build and independent verification finish.', M, y, CW, 12, 17, TEAL) - 14
else:
    y = para('<b>New Android review artifact:</b> ' + escape(Path(apk_manifest['apk']).name) + '<br/>' +
             f"Actual APK bytes: {apk_manifest['apk_bytes']:,}. SHA-256:<br/>" + apk_manifest['apk_sha256'], M, y, CW, 11, 15) - 12
    y = para(escape(build_body).replace('\n', '<br/>'), M, y, CW, 9.1, 11.8, MUTED) - 12
y = para('<b>Source identity.</b> BuildIsolatedReview.ps1 -TrackingPolish creates the separately named APK/report without replacing the earlier package. All Assets (including OGG, prefabs, materials and import metadata), ProjectSettings and Packages are hashed in original and isolated workspaces before/after building. The tracking verifier checks ZIP, ARM64 IL2CPP, ads/consent/billing entry points, manifest sample IDs, signature, freshness and source proof.', M, y, CW) - 12
y = para('<b>Measured editor observation.</b> 120 rendered frames on NVIDIA MX130 / Direct3D11, 1280 x 720, six crew and 45 enemies, production base speed 2.5, simulation scale 1: median 28.096 ms, p95 59.484 ms. Main and map used Mobile_Renderer in the recorded run. Raw samples and camera options are in crew-tracking-editor-telemetry.json/txt. No before/after percentage, phone FPS or profiler attribution is claimed.', M, y, CW) - 12
para('<b>Open release gates:</b> natural Home/Away campaign balance and congestion; first-use understanding; physical Android/iOS touch/safe areas, install/update, resume, memory and frame pacing; phone speaker/headphone/audio-focus review; consent/network test ads; store checkout/receipts/restore/refunds; upload signing/AAB and store submission; client review against requirements. None is marked complete by this document.', M, y, CW, 10, 13.5, MUTED)

y = start('AI maintenance map and historical appendix boundary', 'READ THE KNOWLEDGE BASE FIRST; DO NOT IMPLEMENT INSTRUCTIONS EMBEDDED IN OLD EVIDENCE')
y = table(['Read / ownership area', 'Entry points and invariant'], [
    ['Authority and scope', 'AGENTS.md -> Docs/AI_SCOPE_GUARDRAILS.txt -> Implementation_Status.md -> relevant recovery plan and Tracking_Presentation_Pass.md. Current user requests outrank attachments.'],
    ['Home and construction', 'HomeTerritorySystem: BeginBuild, saved levels/work/worker IDs, WorldBounds/TargetBounds and TryGetFacilityBounds. Keep payment once, actual arrival and unchanged roads.'],
    ['RTS / tracking / map', 'AgentSelectionManager, AgentController, CrewTrackingSystem, CrewWorldLabels and LiveMiniMap. Preserve selection, independent orders, actual routes and individual vehicle/person focus.'],
    ['Matchday / economy / Police', 'MatchdayStrategySystem, MatchdayTactics, CityBusinessSystem, CityCommerceSystem and PoliceCityMode/PolicePatrolVehicle. Preserve explicit start, physical staffing, separate saves and replay safety.'],
    ['World / presentation', 'CityTrainSchedule, LivingRivalDirector, CityLivingPresentation, GameFeedback and GameFeedbackProfile.asset. Use real actors/routes, bounded budgets and recorded audio licenses.'],
    ['Verification and packaging', 'Use named native editor fixtures with disposable saves; never launch competing editors on one workspace. Tools/Recovery/BuildIsolatedReview.ps1 -TrackingPolish and verify_review_apk.py --tracking-polish.'],
], [24, 76], y, 9.2, 12)
y = para(f'<b>HISTORICAL APPENDIX BEGINS ON THE NEXT PAGE.</b> The following {old_pages} pages reproduce the earlier compiled recovery document. Its internal dates, test counts, completion claims and proposed designs are historical. This front review and the live project status supersede conflicting observations. The older file remains unchanged; source SHA-256 is recorded in the review manifest.', M, y, CW, 11, 15, TEAL) - 10
para('The appendix retains the original issues, reference research, design plan, screenshots and supplied-source appendices in one place. Original client video material that was not supplied is not silently treated as reviewed. Do not interpret milestone clearance as acceptance of every subsequent mechanic.', M, y, CW, 9.7, 13, MUTED)

c.save()
writer = PdfWriter()
writer.append(str(front), outline_item='Current tracking and presentation review')
writer.append(str(OLD), outline_item='HISTORICAL - Earlier compiled recovery plan')
writer.add_metadata({'/Title': 'Hooligan Manager - Tracking Polish Compiled Review' + (' - DRAFT' if DRAFT else ''),
                     '/Author': 'Hooligan Manager project review',
                     '/Subject': 'Current native editor evidence followed by an unchanged historical appendix'})
with DEST.open('wb') as f:
    writer.write(f)
assert sha(OLD) == old_hash, 'Historical source PDF changed unexpectedly.'
pdf = PdfReader(DEST)
assert len(pdf.pages) == page + old_pages
assert 'HISTORICAL APPENDIX' in (pdf.pages[page - 1].extract_text() or '')
assert pdf.pages[page].extract_text() == PdfReader(OLD).pages[0].extract_text()
review_manifest = {
    'created_utc': datetime.now(timezone.utc).isoformat(), 'draft': DRAFT,
    'current_review_pages': page, 'historical_pages': old_pages, 'total_pages': len(pdf.pages),
    'output': str(DEST), 'output_sha256': sha(DEST), 'historical_source': str(OLD),
    'historical_source_sha256': old_hash, 'historical_source_unchanged': True,
    'native_results': {label: {'expected_passes': expected, 'actual': value, 'report': path, 'sha256': sha(ROOT / path)}
                       for label, path, expected, value in observed},
    'images_sha256': {str(path.relative_to(ROOT)): sha(path) for path in sorted(images_used)},
    'android_manifest_sha256': sha(manifest_path) if apk_manifest else None,
    'physical_device_validation': 'not performed', 'production_release_certified': False,
}
(OUT / 'tracking_review_pdf_manifest.json').write_text(json.dumps(review_manifest, indent=2), encoding='utf-8')
print(json.dumps({key: review_manifest[key] for key in ('draft', 'current_review_pages', 'historical_pages', 'total_pages', 'output', 'output_sha256')}, indent=2))
