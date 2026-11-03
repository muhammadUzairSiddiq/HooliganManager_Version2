# Recovery implementation status

## Active production pass - latest owner priorities

The latest request prioritizes strategic matchday, simple mobile RTS controls,
real-action tutorial guidance, visible Home investment, fewer circles and roads
that never disappear. The current production pass is implemented for local review.
The current table below separates completed checks from pending packaging.
Current implementation includes touch-only qualified-building reveal, optional
site outlines, seven action-driven tutorial lessons, saved Home levels and work,
matchday approach/resource choices and crew-dependent optional escort/stall,
controllable multi-officer Police transport, shop trade/investment and Unity IAP.
Current native results are evidence of the named isolated checks, not release or
client acceptance. The matching Android review package has completed and passed
independent packaging checks.

| Latest production-pass check | Actual result | Evidence |
| --- | --- | --- |
| Runtime/editor compile | Zero errors; 33 runtime warnings | compile-runtime.txt / compile-editor.txt |
| Campaign/economy rules | 42 passed, 0 failed | rules-checks.txt |
| Tutorial/Home investment/visibility/strategic matchday | 52 passed, 0 failed | production-strategy-playtest.txt; starter-speed bounded escort, actual save/reload, allocation, staffed trade, aftermath, explicit capture and Home income replay protection |
| Multi-officer Police vehicle | 21 passed, 0 failed | police-vehicle-playtest.txt; empty-van selection, real boarding, road navigation, exit and Continue |
| Full on-foot Police career | 49 passed, 0 failed | police-playtest.txt; actual shift, escort handover, exact Guard/Patrol Continue, booking and recovery |
| Commerce and Away business persistence | 30 passed, 0 failed | commerce-playtest.txt; physical investment/trade, actual Riverside return/revisit, separate Home/Away records |
| Home visual-control flow | 33 passed, 0 failed | redesign-playtest.txt; final one-line Home identity and explicit Home camera framing, construction, recruitment and order-mode transitions |
| RTS/city compatibility | 102 passed, 0 failed | Artifacts/CityQA/rts-controls-playtest.txt; explicit building touch, unchanged roads/rails, selection/duties/gestures, police boarding/departure and actor caps |
| Role menu and new campaign | 16 passed, 0 failed | Artifacts/Milestone01/playtest-checks.txt; no RESULT line, all 16 assertions PASS; three editor aspect ratios; fixture preserves the editor scene |
| Native saves / ads | 12 / 14 passed, 0 failed | unity-save-checks.txt / ads-checks.txt; final repeats complete, no physical device or network advertisement |
| Purchases and commerce rules | 40 passed, 0 failed | purchases-checks.txt; actual SDK orders through the real local service callback, native save-before-confirmation, wrong-wallet retry and replay safety; no store checkout |
| Final Android review package | Succeeded; 0 errors, 38 warnings | android-build.txt; isolated batch build completed 03 November 2026 at 01:42:51 local; duration 26m02s |
| Original/copy implementation identity | 252 source/configuration hashes agree before and after build | isolated-build-source-check.json; all runtime/editor scripts and reviewed packaging configurations |
| APK integrity/native SDK/signature/freshness | 11 passed, 0 failed | apk-package-checks.txt and review-delivery-manifest.json; ARM64 IL2CPP, ads/UMP/Billing DEX entry points, sample App ID and review signature; no install or device execution |

Read Production_Gap_Comparison.md for the requested baseline comparison. The
preserved Police continuation failure had 46 passes and 3 fixture failures before
the corrected 49/0 run. Strategy failures 43/4 (startup resume/incidental capture)
and 48/3 (physical escort) are retained separately; the current bounded-route
starter-guide run is 52/0. The old rail-removal assertion was replaced because
it contradicted the latest instruction. No previous APK certifies the new source.

Final IAP review reproduced a source-level recovery gap in the installed SDK's
per-session suppression: a pending purchase refused while the correct firm was
unavailable could be skipped on a later fetch in the same app session. The
adapter now explicitly retries fetched pending orders, with automatic fetched
pending dispatch disabled. Six native local checks use real SDK order models
and the actual callback; they verify wrong-wallet deferral, original-wallet
retry, durable currency/receipt save before confirmation, repeated fetch, native
reload and missing campaign service. These checks pass within the 40/0 suite;
they are not platform payment or SDK network tests.

The first final GUI packaging attempt reached Google Mobile Ads 11.6's
`ResolveSync(true)` call but produced no compiler/Gradle activity or fresh build
report. Installed EDM 2.1 uses a different GUI queue/prompt path from batch mode.
The original editor was left open. The exposed Google SDK Gradle-preprocessor
switch is now off to avoid its redundant synchronous call; EDM remains enabled,
and committed Gradle templates already declare the pinned native dependencies.
The project SO App ID synchronization and build validation remain active. A
separate project copy successfully built through
`Tools/Recovery/BuildIsolatedReview.ps1`; 252 original/copy input hashes agreed
before and after it. Native ads, consent and billing classes are independently
verified inside the resulting APK. The blocked GUI attempt is not a successful
GUI build. The existing editor still needs a restart; no GUI restart/recovery
test was performed. Its edit scene was MainMenu and clean before the attempt;
pre-existing project changes and real campaign saves were preserved.

## Android delivery identity

The current review APK is `Artifacts/Builds/Hooligan-RTS-Recovery-Review.apk`.
Actual ZIP size: **254801528 bytes** (254.8 MB decimal). SHA256:
**5e306ee9d558cfbe057c37cf0930728d57a4467087fdaceaba017cca648fce3a**.
Unity's report lists 1566154282 total build-output bytes, which includes more
than the APK; that number is not the download size. The completed build timestamp
is 2026-11-02T20:42:51.5630737Z (03 November 01:42:51 Pakistan time).

The package uses ARM64 IL2CPP, Google sample ad configuration and review signing.
Purchases remain in explicit EditorTest configuration and are disabled on device
until the owner supplies real store products and selects Store mode. The exact
artifact has not been installed/run on a physical device. Network consent/test
ads, sandbox payments/restore/refunds, natural whole-campaign balance, upload
signing/AAB and submission are still required before a production release.

Both supplied PDFs and the complete September transcript are byte-identical
project copies under `Docs/Recovery/Sources`, with SHA256 provenance. The final
compiled report appends both PDFs intact and the complete typeset transcript.

Updated 03 November 2026. The expanded redesign is implemented as a local review revision following the owner's rejection of the earlier presentation. Current native test evidence is listed first; earlier integration evidence remains separately labelled. Client acceptance, full campaign balance and physical device validation are not claimed.

## Expanded redesign - current work

The expanded redesign now has a dedicated city HUD with one current purpose,
tap-to-move and one explained Orders menu. The permanent command row is hidden.
Home is a named highlighted world territory with three crew-built permanent
facilities, visible reused models, recruitment of individual members, supply
production and a one-time training benefit. Construction requires real arrival
and can be cancelled without payment. The latest production pass saves completed
levels and unfinished work with builder identities; loading waits for spawned
actors before resuming. Native production checks now verify that continuation
and one-time payment. The management sheet reveals advanced activities
after headquarters is established; ordinary map annotations are an optional layer.

Police is a separate playable career with controllable officers, real patrol
checks, concurrent dispatch, on-site assessment, identified-suspect detention,
on-foot escort, station booking, shift rewards, saved positions/duties, and
recovery. Firm save writes are redirected to a separate police store while that
mode is active; exit restores the original firm campaign. The latest revision
binds the police base to the existing police building and blocks accidental
world orders through Dispatch. Core RTS orders remain available without ads.

Google Mobile Ads 11.6.0 and Unity External Dependency Manager 2.1.0 are installed
and pinned. Assets/Resources/Ads/AdsConfiguration.asset is the requested
ScriptableObject: Disabled/Test/Live, Android and iOS test/live app and ad-unit
IDs, rewarded/interstitial switches, cooldowns and optional reward size. The
project asset is configured in Test mode with Google's public sample IDs; forced interstitials are
disabled. Real device initialization waits for UMP consent eligibility. The
editor simulator is labelled, makes no network ad request and earns no revenue.
The optional supply reward is capped once per matchday and credits only a valid
reward callback. Privacy options are exposed when UMP requires them.

## Earlier expanded verification (v3; superseded by latest production pass)

| Check | Result | Evidence |
| --- | --- | --- |
| Runtime/editor compilation | Zero errors | compile-runtime.txt and compile-editor.txt; 33 runtime warnings, mainly existing unused/obsolete fields |
| Standalone campaign/economy rules | 42 passed, 0 failed | rules-checks.txt; no scene or native serializer |
| Native startup/save serializer | 12 passed, 0 failed | unity-save-checks.txt; isolated campaign |
| Actual role menu/New Game and three layouts | 16 passed, 0 failed | Artifacts/Milestone01/playtest-checks.txt; 1280x720, 2400x1080, 1024x768 |
| New Home construction/control/recruit flow | 24 passed, 0 failed | redesign-playtest.txt; real actors, cancellation, costs, facility models, supply benefit, separate guard and native save |
| Playable Police full shift and recovery | 32 passed, 0 failed | police-playtest.txt; actual patrol, dispatch, detention, walking escort, booking, next shift, downed-officer recovery, separate save |
| RTS/city/police compatibility regression | 101 passed, 0 failed | Artifacts/CityQA/rts-controls-playtest.txt; persistent/split selection, duties, gestures, neutrality, bribe payment once, boarding/van departure, occlusion and actor caps |
| Ads configuration/reward callback rules | 14 passed, 0 failed | ads-checks.txt; official package imported, actual project SO; no network impressions/device validation |
| Android review package | Packaging in progress | redesign-android.log and android-build.txt will record actual outcome |
| Physical Android/iOS and Google network test ads | Not performed | No connected device; editor simulation and APK compilation are separate evidence |

All report filenames without a prefix are under Artifacts/Recovery. Home/Police
fixtures accelerate time and crew travel and suppress unrelated pressure to
isolate their contracts. The Police run completes a real station-based career
loop and restores the exact original firm save bytes. The RTS suite exercises
local combat/autonomy independently. These checks do not certify natural balance
of the full Home/Away campaign or human readability/touch on devices.

A reproduced real-station patrol-to-dispatch failure was fixed by checking nearby
connected street polygons for actual agent paths. Patrol points are distinct
connected locations; a zero-length patrol is rejected. The accelerated patrol
test waits for navigation frames and measures peak travel. Downed-officer saves
cannot overwrite recovery because live capture is suspended before healing and
reloading. Police dispatch blocks input through the sheet; Police/Home and live
call markers correspond on the minimap.

## Earlier recovery verification (before expanded redesign)

These values describe the earlier recovery revision. Several report paths below
are reused by current checks and now contain superseding results; the current
expanded verification table above is authoritative for those paths. The earlier
82-check city fixture is retained as earlier evidence, not a full run through the
new Home presentation. It does not establish current full-campaign acceptance.

| Check | Earlier result | Evidence and limits |
| --- | --- | --- |
| Runtime and editor compilation | Zero errors; 32 runtime warnings | Historical value; Artifacts/Recovery/compile-runtime.txt and compile-editor.txt now hold the current expanded compile results |
| Standalone rules | 42 passed, 0 failed | Artifacts/Recovery/rules-checks.txt is reused; current result is listed above |
| Native startup and saves | 12 passed, 0 failed | Artifacts/Recovery/unity-save-checks.txt is reused; current result is listed above |
| Native MainMenu to New Game | 15 passed, 0 failed | Historical menu result; Artifacts/Milestone01/playtest-checks.txt now contains the expanded role-menu checks |
| Native city integration | 82 passed, 0 failed | Artifacts/Recovery/city-integration-checks.txt; actual actors, navigation, economy, matchday, scenes, deployment, extraction, return and defeat recovery |
| Native RTS and police regression | 101 passed, 0 failed | Artifacts/CityQA/rts-controls-playtest.txt is reused; current compatibility result is listed above |
| Android review APK | Superseded by expanded review build | See the current Android packaging row above; no earlier queued build is treated as a completed package |
| Physical Android/iOS | Not performed | ADB reports no connected devices. Editor ratios and an APK are not device validation |

The integration fixture uses five durable, fast members and suppresses ambient pressure for isolated economy/coverage assertions. It seeds later mission prerequisites to test extraction and return. It does not certify a naturally played five-chapter campaign. Matchday coverage is tested separately from the crowd director so hostile contest cannot make the positioning assertion random; the RTS suite exercises live conflicts and duty AI.

## Implemented behavior

| Requirement | Resulting behavior | Main implementation |
| --- | --- | --- |
| R01/R02/R11 | New Game supplies three living members, GBP 5,000 and heat 1. The first lesson waits for deliberate selection. Continue migration preserves earned cash, roster, heat and unlocks. Automatic selection cannot teach skills. | GameData, GameManager, RecoveryProgression, AgentSelectionManager, BattleManager |
| R03/R08 | ModernCityHud presents compact resources, CREW, MANAGE, one current objective and one explained ORDERS menu for the selected members. Tap a street to move; the old Squad panel and permanent Move/Attack/Guard/Patrol/Retreat row are hidden. The minimap remains independent. | ModernCityHud, CityMissionHUD compatibility backend, LandscapeBattleHUD |
| R04/R07 | Button hit areas remain stable; distinct action names, rewards and heat costs are retained. World availability markers no longer execute their action when shown. | LandscapeButtonPolish, ConciseButtonCaption, WorldInteractBubble |
| R05/R06/R09 | Management sheets replace one another; Matchday exposes only phase-relevant decisions. World venue buttons sit below the HUD and avoid covering crew tap targets. Small noninteractive crew identities show readiness/selection and health. | CityUiSheets, WorldButtonLayer, CityLocationLabel, CrewWorldLabels |
| R10/R12 | Eleven linked Home goals teach control, establishing headquarters, scouting, income, duties, preparation, matchday and district defense. Defense waits for its prerequisites. Riverside has six core goals; other Away districts have seven. Extortion/taxi/sabotage are optional. | CampaignMissions, RecoveryProgression, HomeTerritorySystem, CityGameplay |
| R13-R16 | Persistent additive selection and independent orders remain intact. Guard/patrol react and resume; manual movement overrides duties. Capture uses selected crew and accepted orders. Blocked navigation names the actual cause. | AgentSelectionManager, AgentController, RtsGestureController, CityGameplay |
| R17-R20 | Existing faction models/animations, minimap, free camera and bounded crowds are retained. Unit identity uses small diamonds/status labels; ordinary site outlines are hidden by default. Touch reveals only an eligible building for 12 seconds; roads/rails/colliders are preserved. | RtsBuildingVisibility, SiteOutlineVisibility, UnitAffiliationMarker, crew labels and existing RTS/city systems |
| R21/R23 | Required approaches now require both outbound and return paths for the crew's navigation type and areas. A reproduced shop-to-stadium guard failure was corrected by rejecting a one-way approach. No physical road closures were added. | CityGameplay.ReachableApproach, MatchdayStrategySystem |
| R24 | Six saved businesses use distinct existing storefronts: burger, bakery, pizza, pub, cafe and convenience. Actual selected crew travel, control/loot on site, cancel on new orders, contest, defend, lose and retake control. Income, cooldown and disruption survive reload. | CityBusinessSystem, CityBusinessLedger, PlayerData |
| R24/R28 | Home businesses continue earning during active Away simulation. Leaving no reserves at heat 3+ warns after 120s and disrupts trade after 180s. The simulation gives no offline payouts. | CityBusinessSystem, CampaignMissions.HomeReserves |
| R22 | Matchday requires learning/preparation and explicit start. Two location responses hold 20s with actual living crew. Stadium deadline 270s; pub 420s. Coverage pays once; misses cost morale/heat and can disrupt pub trade. Chapter credit waits for aftermath. | MatchdayStrategySystem, StadiumMatchdayActivity |
| R22/R28 | Arrival, build-up, pressure, kickoff, dispersal and aftermath have saved clocks/outcomes. Dispersal stops new peak flashpoints. Named groups save member health/positions, task, destination, conflict and timers. Offscreen-only conflicts advance records; visible combat uses existing AI. | MatchdaySaveState, StadiumMatchdayActivity, GameData.SaveData |
| R22/R29 | Aftermath offers a deliberate missed-response retry without repeated penalties/rewards. Continue To Next Day applies existing day progression once. A failed response does not itself create Wasted. | MatchdayStrategySystem, StadiumMatchdayActivity |
| R25 | A repeated bribe callback cannot charge twice while departing. Officers board when near the van; blocked officers remain on foot. The same van physically joins patrol. Low heat cannot be raised to five by payment. | LivePoliceSystem, PoliceCarChaser |
| R26-R28 | Away preparation preserves an intentional NONE and shows minimum crew, cost and Home reserves. Launch charges once. Actual travellers are stored separately from reserves; recruits obtained Away join that trip. Continue never substitutes reserves silently. | PlanAwayTripController, GameManager, BattleManager, PlayerData |
| R28 | Extraction is requested at ARRIVAL. Only selected members receive movement; every surviving traveller must regroup in the clear zone for 8s. Chapter reward/unlock commits once. Actual Home return retains businesses and completed Home progression. | AwayExtractionSystem, CampaignMissions, GameManager |
| R29 | A completely lost crew has an explicit Rebuild At HQ option without ads, recovering three members with a cash/morale penalty while retaining campaign/businesses. No cash is granted. Retry preserves even a valid zero-heat snapshot. | GameManager, GameData |
| R30 | Scope guide, detailed plan, current status, reproducible compiler/rules tools, isolated Unity suites and a review build entry point are included. | AGENTS.md, Docs, Tools/Recovery, editor checks |

## Navigation finding and visual review

Checking only HQ-to-destination paths missed a one-way approach. In the native fixture, a member finished a shop task but could not accept a stadium guard route. Requiring a complete return path selected a safe street approach; the same order then passed and the character stayed on the requested shop endpoint. Both directions are asserted for all nine city locations and six businesses. This is a reproduced project issue, not proof of the exact cause in the missing original client video.

Current expanded captures are Artifacts/Recovery/redesign-home-world.png,
redesign-home-building.png, redesign-orders.png, redesign-management.png and
police-dispatch.png. The Home/Police captures use the isolated native fixtures.
Artifacts/Milestone01/gameplay-1280x720.png, gameplay-2400x1080.png and
gameplay-1024x768.png cover the actual starter flow at three editor aspect ratios.
Artifacts/Recovery/recovery-home.png, recovery-businesses.png and
recovery-matchday.png are earlier integration captures and are not evidence of
the latest Home presentation. Visual inspection is separate from physical touch
comfort, mobile performance and client acceptance. Existing low-poly art is
reused; the bakery uses a repurposed small storefront model.

## Remaining acceptance work

- Play the complete Home chapter and all four Away chapters naturally, with production crew stats and no seeded prerequisites. Tune costs, travel/response times, rival growth, injuries and recovery from that evidence.
- Review every business camera view and practical crowd/traffic congestion through a long session. Complete NavMesh paths alone do not measure mobile congestion or visual comfort.
- Verify native touch and safe areas on physical Android phones/tablets and iOS, resume/background saves, update installation, memory, frame pacing and long sessions. No device FPS or iOS build certification is claimed.
- Review the demonstration build with the client against the acceptance matrix. M1 clearance reported by the user is retained; current automated passes do not establish M2 client acceptance.
- Playable Police, controllable multi-officer transport, commerce, ads and Unity IAP are implemented expanded work. Cloud/PlayFab, real ads consent/network impressions, store catalog/checkout/restore/refunds, server receipt verification, full original-game content equivalence, upload signing/AAB and submission remain content/integration/release work. The review uses Google sample ads, EditorTest purchases disabled on devices, and review signing.

## Provenance and preservation

Both supplied PDFs and the September transcript were reviewed. The earlier recovery PDF contains misleading still captions: loading/Entering Home images are not proof of matchday/defeat dialogs. The compiled report preserves and corrects that evidence. The original client video and October messages were not supplied. Reference research used the GameSpot hands-on preview of Hooligans: Storm Over Europe; exact mobile gestures, economy numbers and this campaign structure are recovery adaptations.

The working tree already contained cache/save, HUD scaling, minimap, modal, editor bridge and other UI changes. Those changes were preserved. Prior Milestone01 captures/reports were retained under Artifacts/Recovery/PriorM1 before the current runs. No real campaign save was used for automation. No commit, publication or store deployment was made.

## Reproduction

Read Tools/Recovery/README.md. Run compile_recovery.py and run_recovery_rules.py
with the bundled Python, then RecoveryChecks.Run, MilestoneOnePlaytest.BeginBatch,
RedesignPlaytest.BeginBatch, PoliceCityPlaytest.BeginBatch, AdsChecks.Run and
RtsControlPlaytest.BeginBatch in Unity 6000.0.80f1. RecoveryCityPlaytest.BeginBatch
is the earlier recovery integration fixture; its retained report does not replace
the new Home/Police checks. Use isolated saves and stop other project editor
instances first. Run RecoveryBuild.Android for the local review APK. The PDF
build reads this status file so implementation evidence remains separate from
proposed design.
