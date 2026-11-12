# Release versioning and tutorial refresh - 12 November 2026

Unity release Player builds reserve the next patch version and numeric
build number. Development builds leave both values alone. The isolated
review runner shares the main editor's release counter. It restores the
temporary build copy's settings after packaging so the existing input hash
check still covers the source and copy. Failed builds may leave a version
gap because a reserved version is never reused.

On a new installed version, startup clears the game's temporary files and
Unity's download cache. The firm and Police saves each carry a tutorial
version receipt. The first load on a new version refreshes the tutorial
flags. Subsequent loads can resume the saved tutorial part. Campaign
progress, purchases and settings remain in their existing saves.

The native Unity startup and save run passed 19 checks. It exercised
consecutive release reservations, nested temporary cache cleanup, firm and
Police tutorial migration, and repeated loads on the same version using
disposable saves. See `Artifacts/Recovery/unity-save-checks.txt`.

The Android client APK, `Artifacts/Builds/Hooligan-Client.apk`, packaged
successfully on 13 November as **0.3.1 (build 4)**. Unity reserved the
version and built the player; packaging was retried from the generated
project with a clean Gradle cache at a short Windows path. The final APK
passed ZIP integrity and signature verification. Its package ID and
signing certificate match the 0.3.0 tracking review APK. See
`Artifacts/Recovery/client-android-package-verification.txt`. A physical
Android upgrade and first-launch tutorial run remain unverified.

The next Android release attempt initially failed before Java compilation
because the default Gradle metadata cache could not create the
androidx.browser:browser:1.8.0 descriptor directory. The generated Unity
project was packaged successfully with the same source and a clean Gradle
cache at a short Windows path. The resulting client is **0.3.2 (build 5)**;
manifest, v2 signature, ZIP integrity and the ARM64 IL2CPP library were
verified. That workaround packaged the APK without repairing Unity's
default Gradle cache. The same error returned on the next Unity build
attempt (0.3.3, build 6).

The damaged `androidx.browser` metadata folder was unreadable on Windows.
Only its parent `browser` cache folder was renamed to quarantine the
damaged data. A fresh Unity build used the normal Gradle location,
recreated the dependency descriptor there, and produced **0.3.4 (build
7)** at `Artifacts/Builds/Hooligan-Client.apk`. The APK's manifest,
matching signing certificate, ZIP integrity and ARM64 library were
verified. A physical device install was not performed.

# Automatic Matchday and direct squad orders - 12 November 2026

The original Going Dutch mission starts from a mission briefing, then puts
the squad, contextual orders and minimap in the live city. Its documented
scenarios use different primary objectives. The automatic schedule in this
project applies that live-city structure to persistent Home play.
[Going Dutch gameplay](https://www.youtube.com/watch?v=iJEN66EqZLI&t=210s)

- The Home goal stays in the main objective area. Matchday gets one compact
  status line and one View action. The Stadium pin locates the ground. There
  is no production start, paid approach selection or objective quota.
- The first fixture starts after 100 seconds of active Home time. Tutorial
  and real pause hold the clock. Rail arrivals, crowds, gates, road
  closures, police and dispersal run automatically. Aftermath advances the
  campaign calendar in-place after the streets settle.
- The only Matchday prompt asks for a guard at the stadium approach. The
  regular Guard order counts when a member actually holds the post. A
  successful guard earns extra fare credit and morale. If left unattended,
  a named rival sends a real group to the stadium and expands its Home
  presence. Home Supply Yard stock helps the pub serve fans. Away still
  returns stock through the existing extraction.
- Selected squads have direct Attack, Guard and Regroup shortcuts. Other
  squads keep their orders. The existing Orders menu retains the additional
  commands and the tutorial still uses its original controls. An idle
  member who defends against a local attacker stops after that encounter.
  Automatic rival supporter clashes focus on their opposing groups unless
  the crew actually engages.

The final native test passed 19 checks with zero runtime errors. It used
two automatic fixtures in one Home scene, verified the camera and Home
goal during kickoff, checked unattended road clearance and visible rival
reinforcement dispatch, and used the native guard action while a second
crew member retained their Home order. The test re-read the guarded result
from the isolated save. The first run captured the HUD before its refresh.
The later run caught idle retaliation chasing unrelated fights, which the
final test checked after the local-defence fix. See
`Artifacts/Recovery/automatic-matchday-playtest.txt`. The clock was
accelerated and the guard was positioned near the post for the order
test. The walking path from the original crew position was about 298 m.
Final assemblies compiled with zero errors and 31 existing runtime
warnings. Full original-season scenario parity and device testing remain open.

# Connected Home, Matchday and Away - 12 November 2026

- A completed Home Supply Yard cuts the pub's Matchday restock to one supply.
  Home's stock rescue and Yard output use the same stock balance. Matchday
  aftermath awards one next-trip fare credit. Opening a road, getting the pub
  trading and admitting a queue raise that credit. The planner displays the
  net fare and applies the credit only when departing.
- The earned Home chapter unlock remains the shared Away gate. Aftermath can
  lead directly to the unlocked Away planner. The existing objective area
  shows the next destination at Home and the extraction action once Away
  prerequisites are met. Successful Away extraction adds three supplies to the
  same Home balance once per chapter and unlocks the next destination.

The native November 12 campaign run used the real Away planner with the earned credit,
loaded Riverside, completed the eight-second extraction with actual crew, and
returned to Home with three supplies and North End available. Chapter
prerequisites were seeded in the disposable campaign. The repeat passed 74
checks with no runtime errors. It verified the live road queue resuming after
police clearance, both gate routes, the side-gate throughput receipt, £420
earned fare credit, native save, and a Riverside departure and return. The
single failing check was the older guided supporter escort. Its destination
now targets the connected checkpoint directly when the guide arrives, and
completion uses the guide and actual surviving followers at the checkpoint.
The pre-correction campaign report is `matchday-street-playtest.txt`. The
earlier gate score failure is preserved in `matchday-before-gate-credit-fix.txt`.
The focused escort retest passed all seven checks with zero runtime errors.
It used the native rally action, followed the crew's walking route, verified
three actual followers at the checkpoint and re-read the completion from the
isolated save. See `matchday-escort-playtest.txt`. Final runtime and editor
assemblies compiled with zero errors and 31 existing runtime warnings.

# Stadium gates and district police response - 12 November 2026

The [Going Dutch stadium approach at 17:33](https://www.youtube.com/watch?v=iJEN66EqZLI&t=1053s)
shows groups moving beside a fenced frontage, followed by a concentrated clash
with RTS control still available. The implementation uses that movement and
pressure in the existing city.

- Rail, pub and stadium cordons have striped 3D cones across the road,
  barricades, marked police vans, and foot details. A van approaches on the
  road mesh when the district streams in. Roof lights flash while deployed.
  Positional two-tone sirens sound on arrival and response, and respect effects
  volume and gameplay pause. The system prefers active traffic splines over
  parking access lanes and verifies a walking bypass. Local foot details can
  pursue suspects or clear the cordon on request. Upright barricades move to
  the side as officers work, traffic resumes at completion, and the real
  waiting actors continue down their route.
- Stadium gates share a walking queue at the connected outer approach. Two
  nearby selected crew can open the main lane, or one can pay £80 for the
  side lane. The gate prop opens and the crowd walks through that lane. At
  least five live supporters beyond the gate are needed for credit. The gate
  choice and objective result survive saves. New days offer two of four
  objectives where both gate routes are available.
- The reference footage is the design comparison. The existing stadium model
  and city navigation determine gate placement. Full original-game scenario
  parity and mobile-device testing remain open.

# Camera passage and live street Matchday - 05 November 2026

The owner requested automatic building visibility during camera passage, a
standard stadium button, and a busier Matchday with street consequences and
varied missions.

Reference reviewed directly in the [Going Dutch gameplay recording at 17:42](https://www.youtube.com/watch?v=iJEN66EqZLI&t=1062s).
The footage shows groups clashing outside a fenced stadium approach while the
player retains RTS control. The [first-mission walkthrough](https://www.cheatcodes.com/guide/ultimate-guide-hooligans-storm-over-europe-pc-24351/)
describes two entrances, a police squad, and followers rallied by leaders.
The new custody timers, reopening requests and Home stock rescue are adaptations
for this project.

- Building visibility checks the camera body and near clip plane. Only eligible
  mesh renderers hide and their previous visibility returns on exit. Roads,
  rail surfaces and physical colliders remain available. A related offscreen
  pointer projection error now handles degenerate camera positions.
- Matchday uses the standard world button and sits on the apron outside the
  actual stadium. Camera framing includes the model and button. The baked
  navigation has no connected Home route into the apron, so walking escort
  resolves at the connected outer stadium checkpoint. The physical apron
  and walking checkpoint have separate coordinates.
- New Matchdays start directly from the stadium with a viable crew. The old
  preparation flags retain their campaign uses. Home and visiting supporter
  waves move through the city. Multiple district closures coexist, stop traffic
  and queue nearby followers. Requested clearance uses actual police work and
  recovery equipment where required.
- The event asks for two of three results. Rally and lead a real supporter
  party, reopen a cordon through the responding officers, or stock and protect
  pub trade. Aftermath records one campaign result. Police pursue and book
  fighting player crew, rival gang members and supporters on either side.
  Booked crew can wait for release or pay bail. Saves retain street objectives,
  cordon records, supporter bookings and crew detention deadlines.
- Active older Matchdays still expose their existing coverage actions. The
  tutorial uses the real stadium button. Home's separate stock rescue lets a
  selected carrier take supplies from a threatened business to headquarters.
  Only delivery pays and a saved per-day venue receipt prevents repeat rewards.

The isolated mode and street playtest seeds crew and venue setup, uses real UI
raycasts and active agents, positions actors for focused interactions, and
accelerates the remaining phase clock after the physical objectives. Its prior
passes exposed an invalid camera pointer and delayed strikes against pooled
supporters. These reports are retained as
`matchday-street-before-framing-fix.txt` and
`matchday-street-before-pool-fix.txt`. The new combat guard checks the target's
current pool lifetime before applying delayed damage. The final isolated
November 5 run passed 58 checks with zero failures and zero runtime errors.
The retained report is `matchday-street-before-police-response.txt`.

The actual stadium apron still needs a separately verified navigation
connection if crews are to walk from Home all the way through the turnstiles.
Phone performance and touch validation remain pending.

# Real gameplay tutorial - 05 November 2026

Requirement REAL-GAMEPLAY-TUTORIAL-01: the latest owner explicitly rejects blur,
apparently floating UI buttons, instant advancement and combat during teaching.
The current request supersedes the preceding blurred-backdrop presentation.

The camera-capture layer excluded overlay UI, making the actual crew drawer
disappear outside the circle. `TutorialSpotlight` no longer creates that layer
or its material. A plain translucent black veil now covers the live render.
Real UI buttons have rectangular cutouts which follow their actual screen
bounds. The shield lets the native pointer through only to that real button;
its ordinary gameplay callback runs before the tutorial observes the result.
It does not draw substitute gameplay buttons. World/camera gestures retain an
exclusive input surface while the simulation is stopped.

`GuidedCityTutorial` now separates instruction, real work and visible result:
instructions pause, movement/work resumes normal time, actual completion pauses
again, and NEXT waits for deliberate confirmation. Selection/deselection closes
the real crew drawer so the green markers and selected count can be inspected.
MOVE, GUARD, SCOUT and REGROUP wait for real arrival; PATROL waits for the member
to physically reach both chosen endpoints. NEXT cannot bypass these actions.
BACK reviews without changing selection; explicit per-part Skip remains available
and grants no invented money, buildings, recruit or order progress. Navigation
sits above the real bottom selection panel. The normal silver pause remains
separate from tutorial clock stops.

Home onboarding is reduced from 51 to **32 steps**. Repeated movement, all-three-
facility construction, forced attack/cancel/capture-cost demonstrations and extra
map/news explanations are removed from the initial guide. Those real gameplay
commands/facilities remain available normally. It still teaches crew selection,
double-tap, camera gestures, movement, group/deselection, HQ construction, guard,
patrol, stop, scout, regroup, recruitment, minimap/news and the stadium's Matchday
actions. The later readiness guide keeps actual separate-team dispatch and now
waits for arrival. Unfinished older tutorial sequences restart at selection under
version 2 while retaining their actual campaign state; per-part reload recovery
has not been certified for this cut.

`AgentController`, `EnemyController`, `LivingRivalDirector` and `LivePoliceSystem`
suppress combat/impacts/new confrontations while teaching is active or awaiting
its eligible start. Actual navigation and scripted Police custody/vehicle work
continue. No saved HP boost or permanent hostility change supplies this protection;
normal damage/combat is available again on exit.

`StadiumMatchdayControl` exposes the real sign collider's screen bounds and a
combined stadium/sign framing bound. `CameraPanTouchOnly.TutorialFrame` frames
the actual stadium model before the sign lesson, without changing camera prefs.
Native visual review caught that focusing only the approach sign left the stadium
offscreen; the corrected capture shows the football stadium and local activities.

Current verification:

- Runtime/editor compilation: zero errors, 31 existing runtime warnings.
- `mobile-tutorial-ui-playtest.txt`: **265 passed, 0 failed**. This is a complete
  real Home walkthrough with 100 HP / 2.5 base-speed actors, actual UI raycasts at
  1280x720, 2400x1080 and 1024x768, physical arrival/patrol/build/recruit results,
  result confirmation, BACK review, combat protection/restoration, real pause,
  full stadium framing, and Police selection/skip without invented rewards.
- `guided-tutorial-playtest.txt`: **339 passed, 0 failed** in the full real Home
  and Police repeat. Normal 100 HP actors complete actual officer boarding,
  road driving, detention, van loading, station return/booking, crowd separation,
  road recovery and manual recovery-truck driving. Officers remain alive during
  work; tutorial completion releases pause/input. No forced bribe or invented
  custody result supplies completion. Neither current fixture records runtime
  exceptions or error logs. The preceding 261-check run is preserved; its
  approach-only stadium screenshot is not final framing evidence.

Evidence is under `Artifacts/Recovery/RealTutorial-20261105`, with native images
`real-tutorial-ui-add-member.png`, selection/deselection result captures and
`real-tutorial-result-stadium-matchday.png`. Existing campaign saves and unrelated
working-tree changes are preserved; checks use disposable saves and restore the
original save override/preferences. The editor is returned to clean MainMenu
edit mode. Reproduce with `mobile-tutorial-ui-playtest` for Home/Police skip
coverage or `guided-tutorial-playtest` for both full physical walkthroughs.
The final unavailable-target wording directs the user to the actual explicit
Skip control; it does not enable NEXT to bypass unperformed work. Android/iOS
touch, notch comfort, natural-session comprehension and device performance remain
unverified. No device/package certification is claimed.

# Tutorial colour and translucent blur - 05 November 2026 (superseded presentation)

Requirement TUTORIAL-COLOUR-BLUR-01: the latest owner rejects the opaque blackout
and silver tutorial pause. Teaching now keeps the world in colour, with a soft
translucent blur outside the sharp circular target. The minimal heading, short
instruction and animated hand remain. Back is blue, Skip amber and Next green,
with the existing metal rims and stable touch areas.

`ModalPresentation` distinguishes tutorial-owned clock stops from actual game
pauses. Real pause still applies silver and resumes to colour. `TutorialSpotlight`
uses a light fallback veil; `TutorialBackdropBlur` captures the world camera
without overlay UI, then applies a separable Gaussian in its own offscreen shader.
The UI shader only composites that blurred texture outside the circular cutout.
Captures are limited to 512 pixels wide and 8 Hz while the camera/world changes;
static paused steps reuse the texture. Resizing rebuilds it, and exit releases
the render texture and runtime materials. Unsupported shaders fall back to the
translucent colour veil.

Actual verification on this final source:

- Runtime/editor compilation: zero errors; 31 existing runtime warnings.
- `mobile-tutorial-ui-playtest.txt`: **269 passed, 0 failed**, including rendered
  exterior colour at 1280x720, 2400x1080 and 1024x768, tutorial/real pause
  separation, coloured navigation, input raycasts, camera gestures, staged map/news,
  separate Police navigation, exit cleanup and no invented rewards. No runtime
  error or exception logs were recorded by the fixture.
- Native captures inspected: `tutorial-find-member-colour-blur.png`,
  `tutorial-select-1280x720.png` and `tutorial-real-pause-silver.png` under
  Artifacts/Recovery. These show the coloured sharp target, blurred environment,
  coloured controls and separate silver pause menu.

Evidence and source hashes are retained in `Artifacts/Recovery/TutorialBlur-20261105`.
The intermediate multipass UI shader produced a white exterior despite passing
its input checks. That report/image are preserved as a visual failure, not
certification. The corrected shaders separate offscreen blur from UI rendering,
and the repeat adds actual rendered-pixel assertions.

This focused repeat uses disposable saves and restores the original override,
preferences and MainMenu editor scene. Unrelated changes and player saves remain
preserved. The prior full physical gameplay walkthrough was not repeated for this
presentation change. Android/iOS touch, device performance and natural-session
comprehension remain unverified; no new package/device certification is claimed.
Reproduce via the editor bridge command `mobile-tutorial-ui-playtest`.

# Modes and world Matchday correction - 05 November 2026 (preceding source cut)

Requirement MODES-WORLD-MATCHDAY-01: the current owner explicitly separates Home,
Away and Police modes and wants Matchday as an event inside the city, accessed
through a real 3D stadium button. Earlier Away Trip / Matchday HUD shortcuts and
the older role-menu wiring are superseded. The following source is implemented;
current native evidence is recorded here separately from preceding tutorial
results below.

- `SimpleShellLayout`, `MainMenuController` and `LandscapeFrontEnd` show three
  distinct mode choices. Home resumes Home (or starts a first campaign), Away
  enters its destination/traveller preparation, and Police enters its separate
  career. A separate New Home Campaign retains the overwrite confirmation.
- Away remains visibly disabled until the existing `CampaignMissions.HomeCleared`
  condition is earned. `GameManager.EnterAwayMode` checks the same rule, and
  existing unlocked/legacy saves keep access. The unlock threshold was not
  arbitrarily changed in response to this presentation correction.
- `StadiumMatchdayControl` is a camera-facing world mesh/sign with a trigger
  collider beside the stadium approach. `AgentSelectionManager` handles its real
  world tap before ground orders. Local choices prepare routes, choose the saved
  approach, post separate teams, start the live event, escort, trade/equip, retry
  or continue after its real aftermath. They do not open the command-board UI.
- `ModernCityHud` removes both shortcut buttons; its objective/help can point
  the camera at the stadium. The tutorial targets the world sign and its local
  actions, explains Away as an earned mode, and grants no skipped progression.
  The older strategy-board renderer is retained for editor/legacy checks but
  has no player entry from the revised flow.

Current source compiles with zero runtime/editor errors (31 runtime warnings).
`mode-world-matchday-playtest.txt` records **40 passed, 0 failed** in Unity:
real world/UI raycasts, full/centred mode layouts at 1280x720, 2400x1080 and
1024x768, locked/direct-call Away guards, actual Home/Away/Police entry, separate
selected-team assignments, live event time/supporter groups, one-time approach
costs and native persistence. Police returns the original firm cash, Home state
and Away unlock. The stadium mesh/collider stays at the bound stadium destination;
it does not move to a resampled response location. No player-facing entry opens
the command-board renderer.

The fixture seeds readiness/Home completion only in disposable data. It does not
claim a naturally completed Home chapter. Its initial 30-pass/6-fail report is
preserved in `Artifacts/Recovery/ModesWorld-20261105`: test action clicks were
attempted while the camera remained at the earlier preparation location. The
repeat deliberately returns to the world sign before each local action. Source
also corrects the mode backdrop/centring, sign placement and readable world-sign
materials; before captures are retained. Current captures include
`matchday-world-sign.png`, `matchday-live-world.png`, `modes-unlocked.png` and
`modes-locked-{resolution}.png` under Artifacts/Recovery.

The retargeted `mobile-tutorial-ui-playtest.txt` repeat now records **256 passed,
0 failed**, including the world sign/local-action tutorial targets, input
exclusivity, full circular highlights, hand alignment and navigation without
awards. The preceding 253-check tutorial report is preserved in ModesWorld-20261105
and does not certify this corrected mode/world flow. Both current suites record
no runtime errors during their fixtures. Reproduce this mode/world check with
`mode-world-matchday-playtest` through the existing editor bridge, and the
tutorial check with `mobile-tutorial-ui-playtest`. Both use disposable saves and
restore the original override, preferences and editor scene. The final editor
state is clean MainMenu edit mode. Physical device and full natural-campaign
validation remain separate work; no new device/APK certification is claimed.

# Mobile tutorial repair - 05 November 2026 (preceding source cut)

Requirement MOBILE-TUTORIAL-01 (current owner request): a mobile tap-led tutorial with minimal top text, circular
spotlights, an animated hand, staged HUD reveals, camera/selection/command
teaching, and usable Back / Next / Skip controls. This supersedes the earlier
large tutorial card and does not certify unrelated guided-operations features.

Reproduced cause of the unclickable guide: `TutorialVeil` lacked a
`CanvasRenderer`. Unity's `GraphicRaycaster` repeatedly threw before dispatching
button input. The shield and hand now declare their renderer dependencies.
The new overlay removes the lesson panel and suppresses the older objective
coach. Map, news, Home, orders, Matchday and Away controls reveal in sequence.
It teaches real double-tap focus/selection, pan, pinch in/out, repeated movement,
individual/group deselection, every member command, construction and recruitment.
Back/Next/Skip navigation does not grant learning, construction or campaign cash.

The renderer, input proxy and hand live in `TutorialSpotlight.cs`; lesson state
and real game predicates live in `GuidedCityTutorial.cs`. HUD owners
`ModernCityHud`, `FirstSessionGuide`, `LiveMiniMap` and `CityNewsFeed` stage the
reveal. `AgentSelectionManager` adds normal world double-tap focus/selection and
cancels pending targeting when selection becomes empty, retaining actual orders.
The highlighted Patrol confirmation remains visible under the exclusive input
gate. Closing/skipping the guide clears owned sheets, modal choices and pause.

| Actual verification | Result | Evidence under Artifacts/Recovery |
| --- | --- | --- |
| Complete firm/Police action walkthrough before the final visual clearance refinements | 195 passed, 0 failed | guided-tutorial-playtest.txt; preserved full report and firm/Police fixture saves in TutorialMobile-20261105 |
| Final presentation and input regression | 253 passed, 0 failed | mobile-tutorial-ui-playtest.txt; per-frame InputSystem single-finger drag and two-contact pinch, raycasts, navigation, modal/target clearance, complete circles, hand alignment, staged map/news in both careers, no skipped rewards, deselect targeting cleanup and normal world double-tap |
| Runtime/editor assemblies | Zero errors; 31 runtime warnings | TutorialMobile-20261105/compile-runtime.txt and compile-editor.txt |
| Initial layout and approved targets | 1280x720, 2400x1080, 1024x768 checked | tutorial-select-1280x720 / 2400x1080 / 1024x768.png |
| Native desktop clicks in an isolated preview | CREW, SELECT, double-tap, SKIP PART, BACK and NEXT observed working | TutorialMobile-20261105/manual-desktop-input.txt |

The final focused repeat deliberately skips Police physical work without awarding
arrests or bookings; its scope is UI/input, and the preceding 195-check full
walkthrough supplies the separate real custody/crowd/recovery evidence. Earlier
presentation repeats (137/0 and 254/0) and the script-reload watchdog failure are
preserved in TutorialMobile-20261105. The later 253-check repeat replaces its
direct drag-handler call with a per-frame InputSystem finger gesture, so these
totals must not be added as distinct requirements. No runtime exceptions or error
logs were observed during the final fixture.

Reproduce with `.utmp/landscape-command.txt`: `mobile-tutorial-ui-playtest` for the
focused repeat, `guided-tutorial-playtest` for the full physical walkthrough, or
`mobile-tutorial-preview` for manual review with its own disposable save. Stop
the preview to restore the original save override, preferences and editor scene.
In ordinary play, use MANAGE -> HELP -> REPLAY to review the guide deliberately.
Actual campaigns and unrelated working-tree changes were preserved. The editor
was returned to clean MainMenu edit mode. Physical Android/iOS touch, notch
comfort, natural-session comprehension and device performance remain unverified;
this task did not produce a newly certified Android/iOS package.

# Current guided operations pass - 05 November 2026

The latest owner request is being implemented in Guided_Operations_Pass.md:
exclusive first-time paused spotlight/hand tutorials, actual work/arrival gates,
per-part skip, Police teaching, visible Away access, named news, repaired map
expansion/projection and physical crowd/road/custody/integrity operations.

Current verification: PARTIAL. The mobile tutorial evidence is recorded above;
it does not certify the remaining road, crowd, integrity, strategic Matchday or
other guided-operations work as a whole. No new Android package is certified for
this cut yet. The historical 553 assertions below belong to the
03 November source and must not be presented as passes for these new changes.
The earlier tracking Android build eventually succeeded (0 errors, 4 warnings,
24m34s, 1,575,453,734 reported build bytes); it likewise predates this pass and
has no physical-device validation. Preserve that package and its provenance.
# Recovery implementation status

## Current tracking and presentation pass - 03 November 2026

The owner observed a much improved review and requested a useful synchronized
tactical map, world crew tracking/routes, faster movement, visible Home building
models/expansion, more engaging short coaching, station arrivals, rival raids,
polished sound/music and decorative UI feedback with better performance.
Read Tracking_Presentation_Pass.md and Audio_Presentation_Pass.md for this pass.
Runtime/editor compilation has zero errors and 31 runtime warnings. Audio
resource/provenance audit passes 5/0 and campaign/economy rules pass 42/0.
The current source has the following actual isolated Unity editor evidence:

| Tracking/presentation or affected check | Current result | Evidence under Artifacts/Recovery unless stated |
| --- | --- | --- |
| Authored audio, settings, fixed-hitbox animation, traffic and panel lifecycle | 43 passed, 0 failed | presentation-feedback-playtest.txt; two settings/credits captures |
| Real Home models, earned annex, ground bounds, expansion and native reload | 27 passed, 0 failed | home-visual-playtest.txt; before/work/complete/reloaded captures |
| Tactical projection, actual map selection/reroute, opaque hierarchy and three aspect ratios | 31 passed, 0 failed | tactical-map-playtest.txt; tactical-map-1280x720 / 2400x1080 / 1024x768.png |
| Production-speed gait/boost, real routes, split follow, offscreen finder, compact/expanded routes | 47 passed, 0 failed | crew-tracking-playtest.txt; native renderer/atmosphere ownership checks and telemetry |
| Actual stopped-train groups, routes/budgets, real shop raids and defender/police interruption | 28 passed, 0 failed | train-raid-playtest.txt; dispatch diagnostics and train-arrival-groups.png |
| Tutorial, actual investment/save/resume, strategic Matchday, staffed trade, physical escort and aftermath | 52 passed, 0 failed | production-strategy-playtest.txt; Home builder uses production speed/normal travel |
| Multiple officers, road transport, mounted map clarity and individual van focus/follow | 27 passed, 0 failed | police-vehicle-playtest.txt; actual moving van and independent reserve Guard |
| Police career / commerce / earlier contextual Home flow | 49 / 30 / 33 passed, 0 failed | police-playtest.txt / commerce-playtest.txt / redesign-playtest.txt |
| Broader RTS/city controls and actor budgets | 104 passed, 0 failed | Artifacts/CityQA/rts-controls-playtest.txt; real 0.6-second hold/menu survival and a separately blocked UI hold |
| Role menu / native saves / ads / purchases | 16 / 12 / 14 / 40 passed, 0 failed | Artifacts/Milestone01/playtest-checks.txt; unity-save-checks.txt / ads-checks.txt / purchases-checks.txt |
| Separately named Android tracking APK | Preparing/building; no delivery certification yet | tracking-android-build.txt and tracking-review-delivery-manifest.json required |

The completed frame sample observes 120 rendered editor frames at 1280x720 on
an NVIDIA GeForce MX130, with six crew and 45 live enemies: median 28.096 ms,
p95 59.484 ms. Actual map options are shadows/post/HDR/MSAA off and AA None.
These raw editor observations include editor overhead and do not establish phone
FPS or a measured attributable speedup. Before/failing samples are retained.
The atmosphere subsystem no longer overwrites minimap camera ownership.
The completed Unity editor ledger totals 553 passing assertions, with rules
42/0 and imported-audio audit 5/0 counted separately. The later runtime log
window (after the first completed strategy repeat through final regressions)
contains no exception or compiler-error lines: tracking-runtime-log-audit.txt.

Earlier failures are preserved with descriptive names: Home visual 25/2 and
10/17, crew 42/1 and 45/1, train/raid 24/4, strategy 48/4, and presentation
37/0 and 38/0 before subsequent lifecycle/layout coverage. Home/strategy fast
fixtures were corrected to use genuine production-speed travel rather than
changing gameplay to accommodate artificial 18-speed/5x actors. The train pass
fixed both a fixture stop race and real per-actor routing/shop-target gaps.
A source class or previous report is not a new native pass. Physical-device,
hardware listening, natural long-session play and store validation remain open.
The first broader RTS repeat was 101/1 because its synthetic hold point did not
validate UI/ground availability; its preserved report is under Artifacts/CityQA.
The corrected fixture chooses an exposed connected street, holds for real elapsed
time and separately checks UI suppression. An interrupted parallel-editor attempt
timed out before city readiness (0/1); it was not gameplay evidence. The serial
104/0 repeat completed with the editor returned to clean MainMenu edit mode.

The previous review below remains historical evidence until its affected suites
are repeated against this source. Its APK/PDF describe their specific earlier
source cut, not this new tracking/presentation pass.

## Earlier verified recovery review

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
