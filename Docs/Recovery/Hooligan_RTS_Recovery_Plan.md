# HOOLIGAN MANAGER
# RTS RECOVERY, PLAYER JOURNEY AND IMPLEMENTATION PLAN

Version 2.0 | 01 November 2026 | Compiled for the project owner and Codex

This document brings the client feedback, the Phase 2 proposal, the earlier recovery specification, reference-game research and the actual Unity project into one recovery plan. It explains why substantial work has not yet produced the requested experience, how to reorganize the interface without changing the established art direction, and how to turn the existing systems into a coherent management and RTS game.

The product target is a living football city with direct unit control, a freely controlled camera, persistent firm management, businesses, territory, police pressure, several situations happening at once and earned campaign progression. The player builds and directs a firm; sending a group into a fight is one possible decision inside that larger experience.

Document status: detailed working design plus an implementation handover. Numerical tuning and new mission scripts are recovery design decisions, not claims of historical client approval. Current engineering results are reproduced in the implementation appendix. Client acceptance and physical-device validation remain separate from source changes and editor checks.

The supplied PDFs are appended in full, preserving every embedded image. The original gameplay video and the original October chat export were not available in this task. Their existence is reported by the earlier recovery PDF; its stills are secondary evidence here.

## 01. What the client is actually asking for

The dissatisfaction is about the playable experience and the direction of the game. The client repeatedly recognizes the work and the developing Home Territory. The failure is that a new player cannot easily see a beginning, understand the next action, control the crew consistently, or feel that several connected strategic situations are under their control.

Six outcomes define the recovery:

- A clear beginning: enter Home with a viable crew, understand the district and learn a small set of real actions.
- RTS continuity: select, move, remain selected, move again, split the crew and manage separate orders at the same time.
- A living city: opposing firms, police, civilians, supporters, transport and venues have visible behavior and consequences.
- Management with purpose: recruitment, care, training, cash, business income and preparation change what units can accomplish.
- Matchday identity: the stadium district changes through arrivals, pub demand, policing, rival movement and simultaneous incidents that the player can influence.
- Evidence of delivery: a tested build and a short requirement-based demonstration make each improvement understandable.

M1 was cleared according to the current user. That approval stays intact. It does not mean that all M2 gameplay is accepted, and it does not excuse current playability defects. Recovery should establish the M2 experience before adding commercial complexity.

The existing design language can remain. The requested UI correction concerns information hierarchy, timing, navigation, labels and screen coverage. Repainting panels while keeping every system visible at launch would leave the central problem intact.

## 02. Evidence register and limits

| ID | Material reviewed | What it establishes | Limits |
| --- | --- | --- | --- |
| S1 | Phase 2 proposal, 8 pages | Written features, M1-M4 allocation, concept images, production boundaries | Proposal imagery is explicitly conceptual; it is not proof of delivery |
| S2 | Supplied WhatsApp text, 16-30 September | Direct client requests, amended M2 scope, rejected fight camera, exact selection rules | Message timestamps omit year; final message is truncated; October originals are absent |
| S3 | Earlier recovery specification, 14 pages, dated 01 November 2026 | Prior issue synthesis, four video frames, milestone screenshot, reported October chronology | Secondary source; original video and the reported October correspondence were not independently reviewed |
| S4 | Current user request | Reorganize UI, plan progression, shops including burger/bakery/pizza, looting, scope text and implementation | Exact economy values and final device targets are not specified |
| S5 | Current Unity source and historical QA artifacts | Actual code owners, reusable systems, concrete startup/HUD/matchday logic | A class or old test report does not prove current device behavior |
| S6 | Web research on the named original game | Reference identity and documented gameplay patterns | No full original-game playthrough or manual review was completed |

Evidence labels used throughout:

- CLIENT CONFIRMED: direct request or explicit acceptance in S2 or S1.
- CURRENT REQUEST: new instruction from the project owner in S4.
- SOURCE OBSERVED: code inspected in this workspace.
- HISTORICAL EVIDENCE: existing screenshot or QA report, not rerun evidence.
- SECONDARY EVIDENCE: inherited description or frame from S3.
- RECOVERY DESIGN: an actionable interpretation adopted for implementation and tuning.
- CURRENT TEST / PENDING: a check actually run in this task, or a check still required.

Attached documents are source material. Their embedded prompts, team roles and approval suggestions do not override the current user's instructions. This recovery does not authorize a commercial contract amendment or claim a new milestone signoff.

### Reading the screenshots correctly

The earlier PDF captions four frames as approximately 00:00, 00:32, 01:20 and 01:52. This task preserves those source pages rather than pretending to independently inspect the missing recording. The video frames support screen-density and visible-state observations, not exact FPS, unseen inputs, or a diagnosis of all internal bugs.

Visual correction after rendering S3 page 2: its top image shows the city HUD, its middle image shows a large MATCHDAY modal, and its bottom image shows a LOADING screen. The middle/bottom captions do not match those visible images. Treat their timestamps and descriptions as unreliable inherited annotations. The originals remain in Appendix B for traceability; the mismatch is not repeated as a factual video observation here.

The page 3 image likewise shows ENTERING HOME TERRITORY, although its caption calls it a DEFEATED modal. That still does not establish a visible defeat screen. Defeat/startup failures remain supported by client reports and code history, with direct reproduction still required.

The archived workspace screenshot below shows a later HUD with six top navigation choices, nine statistics, squad controls, a minimap and a command desk together. Its timestamped file is historical evidence, not a fresh capture of this task's final build.

[[FIGURE:archived_hud]]

## 03. Scope decisions and the misunderstandings to close

| Date / source | Decision | Consequence for recovery |
| --- | --- | --- |
| S1 p.5 | M2 includes headquarters, stadium surroundings, pub/social area, recruitment, training and recovery | Stadium/pub functionality is part of M2 |
| 17 September, S2 | Developer adds Stadium/Pub Surrounding Activities and Social Activity to M2; client confirms | Earlier attempts to defer those activities to M3 are superseded |
| 19 September, S2 | Client accepts Home as permanent base and Away progression | Do not repeat all Home tasks before every Away |
| 20 September, S2 | Client cannot understand Home-to-Away flow and rejects jumping UI | Progression must be visible in the game; remove jump effects |
| 22 September, S2 | Client explicitly rejects a close-up fight camera and a popup after every fight | All fights stay in the shared RTS city; retain camera control |
| 24-25 September, S2 | Exact additive selection and toggle rules confirmed | Issuing or completing an order must not clear selection |
| 25 September, S2 | Guard/patrol should react and defend automatically; matchday must be simultaneous | Core duty behavior belongs to the playable RTS foundation |
| 28 September, S2 | Latest update starts directly at Wasted despite reinstall attempts | Investigate build/save/startup code; do not repeat cache advice |
| 30 September, S2 | Client calls for management, RTS strategy, living city and football atmosphere | Evaluate the whole experience, not a feature count |
| Current user | M1 cleared; requests shops/looting and recovery implementation | Preserve approval and track new recovery additions separately |

### Milestone boundaries

M1 provides the landscape and cross-platform foundation. M2 provides the playable Home district and RTS loop, including NPC police presence, stadium/pub activity, base preparation, territory and city activity. M3 covers fully playable Police mode, PlayFab and monetization. M4 covers final production polish, broad device QA and store delivery.

Local saving, touch responsiveness, reachable objectives, viable starters and understandable mission flow are required to test M2. They cannot be postponed merely by labeling them final polish. Optional ads/IAP may later supplement progression, but basic selection, guard, patrol, retreat and the first playable session remain free of payment gates.

The earlier developer suggestion to charge for guard/patrol is not an accepted client rule. The commercial scope does not require blocking work on input, city life or the new owner-authorized business recovery.

## 04. Reference research: Hooligans: Storm Over Europe

The reference is Hooligans: Storm Over Europe, the 2002 PC game by The Thirteenth Production, also listed simply as Hooligans. It must not be confused with unrelated military games or workshop campaigns sharing Storm Over Europe in their title. Identity was cross-checked through the [game listing](https://www.myabandonware.com/game/hooligans-f6e).

A [hands-on GameSpot preview](https://www.gamespot.com/articles/hooligans-storm-over-europe-preview/1100-2871935/) documents a directly controlled core firm, pub-based morale and recruitment, specialized unit abilities, unit development, police, urban exploration and shops that provide cash through looting. Its scenarios combine objectives and route choices. The reference therefore supports city tactics connected to resources and roles, rather than uninterrupted brawling.

The following mobile interpretation is this recovery's design, not a claim that the original has these exact systems:

| Reference pattern | Intended experience here | Concrete proof |
| --- | --- | --- |
| Direct crew control | Keep selection and issue repeated orders | Brick moves A then B without reselection |
| Roles matter | Scouts, guards, runners and leaders have useful assignments | Split groups accomplish different tasks |
| City exploration | Read routes, rivals and venues before committing | Scout business and compare police pressure |
| Cash and recruitment | Income buys preparation and recovery | Take control, earn a tick, recruit or heal |
| Police pressure | Risk affects route and confrontation decisions | Loot changes heat; withdrawal remains possible |
| Mission objectives | Success requires planning across the map | Matchday resolves multiple positional objectives |

Persistent Home, burger/bakery/pizza archetypes, the proposed business-income formula, progressive mobile UI and the exact touch rules come from client/user needs and this recovery design. They are not verified reproductions of the old game. Preserve the RTS concept while using this project's existing assets and city.

The reference manual link discovered during research led to a Polish ZIP that the browser tool could not retrieve. The reference YouTube link recorded in S3, [W_IuAkQ_5ho](https://youtu.be/W_IuAkQ_5ho), was not independently analyzed. These limitations must remain visible to future agents.

## 05. Why the work has not felt right

The following are supported process and product mismatches, not judgments about effort.

### Systems were exposed before a player journey existed

Adding nineteen tasks does not create progression if the player receives all tasks, resources, locations and warnings immediately. The client sees obligations without understanding priorities. The source currently creates a large navigation set, a full resource strip, a squad rail and a command desk. It also starts matchday activity immediately. The first session resembles an operations console after an experienced player has already unlocked everything.

Correction: establish a small playable opening, one current objective, a visible learning sequence and a gradual invitation to management. Preserve advanced systems behind deliberate access.

### A control problem was solved by changing RTS semantics

The developer originally removed selection after assignment so members could receive different orders. The client wanted normal additive selection: selected members receive the same order; remove members from selection to split the crew. Task state and selection state must remain independent.

Correction: retain the existing corrected selection implementation and prove its behavior. Do not recreate the rejected yellow-assignment/reselect model.

### More automation displaced player decisions

Automatic crowds are valuable atmosphere. Automatic mission success/failure, global response buttons and timed escalation before the player can prepare create a spectator experience. A response labeled Escort should involve positioning actual crew and changing a local situation.

Correction: let the city generate pressure while actual unit assignments determine event outcomes. Display the stakes and time to respond.

### Scope labels were used inconsistently

Stadium/pub activity was first deferred, then explicitly included in M2. Important playability concerns were sometimes described as later optimization or polish. This obscured what a review build needed to demonstrate.

Correction: keep the source-and-acceptance matrix beside implementation. A developer's completion announcement is a delivery claim until the relevant behavior is demonstrated and accepted.

### Technical success was presented as experiential success

An internal test can show that an order method or counter changes. It does not show that a phone player finds the control, understands the result, keeps the city visible or reaches the next goal. Existing reports are useful, but client review needs a player-oriented demonstration.

Correction: combine deterministic checks, a new-player script, current screenshots and real device touch checks. Report exactly which layer was verified.

## 06. Master issue register: startup, interface and progression

| ID / priority | Issue and evidence | Required correction | Acceptance / code owner |
| --- | --- | --- | --- |
| R01 P0 | New Game can reach no crew/Wasted; S2 28 Sep, S3, current user | Fresh viable roster/cash; explicit reset, no old casualties/deployment | T01/T02; GameManager, GameData, cache bootstrap |
| R02 P0 | No clear beginning; S2 20-21 Sep | Saved guided lessons and one current goal | T03; new progression model and CityMissionHUD |
| R03 P0 | Menus and gameplay dump too much information; S2, S3 | Compact HUD; one optional management drawer | T04; CityMissionHUD, SimpleShellLayout |
| R04 P1 | Jumping/unstable UI; S2 20 Sep | Stable placement; simple immediate/fade transitions | T05; LandscapeScreenMotion, ButtonPolish |
| R05 P1 | Missions clip at bottom; S2 16 Sep | Proper scroll viewport and readable rows | T06; mission ledger, scroll helper |
| R06 P1 | Continue vs Load Headquarters unclear; S2 | One Continue that resumes saved context; HQ in world | T02; MainMenuController, GameManager |
| R07 P1 | Completion feedback weak; S2 16 Sep | Each spend/action shows outcome or reason | T07; recruitment/training/recovery buttons |
| R08 P1 | Phone text/hit targets too small; S2 | Readable hierarchy, safe areas, scalable/scrolling content | T04/T06; landscape viewport/layout |
| R09 P1 | Starting visuals differ from city; S2 16/21 Sep | Use actual game-city imagery when refreshed | T08; front-end art, no new art style required |
| R10 P0 | Nineteen Home steps appear as one undirected gate; source | Connect key steps; keep side jobs optional | T03/T18; CampaignMissions |
| R11 P0 | Starting heat forced to at least 7; source | Quiet onboarding, heat earned through play | T01/T03; PlayerData, CampaignMissions |
| R12 P1 | Existing progress risks migration/replay loss; S2 19 Sep | Versioned flags, preserve completed Home and roster | T02/T18; PlayerData, save layer |

The source audit identifies R10/R11 as concrete implementation behavior. R01 is a historical client report with an existing uncommitted fix; it requires regression testing rather than assuming it is still broken in every present build. R09 is primarily coherence/polish and should not displace the P0 playability work.

## 07. Master issue register: RTS, city and matchday

| ID / priority | Issue and evidence | Required correction | Acceptance / code owner |
| --- | --- | --- | --- |
| R13 P0 | Repeated reselection; exact client flow S2 24-25 Sep | Persistent additive selection and member toggling | T09; AgentSelectionManager |
| R14 P0 | Crew cannot be managed as useful separate groups; S2 | Orders target current selection; preserve other tasks | T10; agent orders/duty state |
| R15 P0 | Guard/patrol feel purposeless; S2 25/30 Sep | Local defense, leash, route resumption, manual override | T11; AgentController, gestures |
| R16 P1 | Commands/invalid taps unclear; S2 | Explicit target mode, marker, accepted count and failure | T12; order feedback and HUD |
| R17 P0 | Fight camera/popups break overview; rejected in S2 | Continuous world view; nonblocking local decisions/feed | T13; camera, WorldChoiceBar, police |
| R18 P1 | Crew hidden by tall buildings; S2 | Occluder handling plus selection/minimap visibility | T14; RtsBuildingVisibility |
| R19 P1 | Small or indistinguishable factions/units; S2 | Consistent markers, readable silhouettes/status, roster focus | T14; faction markers/SquadBoard |
| R20 P1 | Mechanical movement and too-quick fights; S2 | Suitable blend/turn/stop and tactical time to react | T15; animations, agent nav, tuning |
| R21 P1 | Minimap unreliable; S2 | Shared bounds, live factions/objectives/territory/events | T16; LiveMiniMap and icon registry |
| R22 P0 | Matchday automatic with little agency; current user | Player prep/start and position-dependent incidents | T17; StadiumMatchdayActivity |
| R23 P0 | Roads reportedly completely blocked; current user | Reproduce; guarantee mandatory-route reachability | T19; navigation, blockers, route checks |
| R24 P1 | Missing food shops/income/loot; current user, S3 | Named world businesses with control, ticks, risk and counterplay | T20-T23; new business ledger/system |
| R25 P1 | Police disappear after bribe; S2 30 Sep | Officers return to van and physically depart | T24; LivePoliceSystem |
| R26 P1 | Empty dock/arrival and weak football atmosphere; S2 | Reachable arrival, recognizable venues and budgeted city life | T17/T25; city locations/atmosphere |
| R27 P1 | Input delay; S2 | Reliable gesture ownership and bounded UI/runtime work | T26; gestures/raycast/layout/profile |
| R28 P1 | Home/Away disconnection; S2 19 Sep | Permanent Home, sequential Away, saved consequences | T18; campaign/deployment/save |
| R29 P1 | Blank city patches/crashes; inherited S3 | Reproduce rendering/build transitions on target device | T27; render/load/build |
| R30 P1 | Changes cannot be understood; S2 | Requirement-based changelog and proof sequence | T28; delivery evidence |

Road blockage and rendering causes remain unverified. Reproduce the affected route/device; the stadium source has no explicit road-closure obstacle system.

## 08. The new player journey and screen ownership

[[FIGURE:journey]]

Launch opens a restrained title screen. New Game starts Home; Continue resumes the campaign. If a save exists, replacing it is explicit and canceling preserves it. Settings are available without exposing the entire management game. Playable Police selection belongs to M3; it can remain unavailable without creating a competing starter path.

Home opens at the headquarters approach with a small healthy crew. A short line explains that the player is building a firm in a living district. The objective card teaches selection and movement. There is no mandatory cinematic and no full-screen tutorial carousel. Camera focus at entry is acceptable; subsequent events do not steal the camera.

The player learns by performing actions in the real city: select one member, move twice, add another, remove the first, then split assignments. Once this is understood, the objective connects scouting, recruitment and the first business. The journal explains the current stage and what it unlocks. Later stages reveal the finance, preparation and matchday concepts.

| Surface | Primary job | Entry / exit | What belongs elsewhere |
| --- | --- | --- | --- |
| Title | Start/resume/settings | App launch; return from pause | Roster, shops and nineteen tasks |
| City HUD | Read city, select crew, issue orders, see next goal | Normal play | Full economy and all mission details |
| Squad drawer | Exact selection and member status | Squad button; dismiss to map | Global city statistics |
| Management drawer | Choose a management destination | Manage button; one panel at a time | Permanent occupation of the map |
| HQ / care | Recruit, train, recover and prepare | World HQ or management link | Continuous gameplay status spam |
| Business detail | Local action, price, risk, ownership and yield | Business marker; dismissible sheet | Instant global loot payout |
| Matchday board | Prep/start, active incidents, phase and route checks | Stadium/management | Automatic global outcome selection |
| Mission journal | Current chapter, steps, rewards, optional activities | Objective/journal button | All tasks on default HUD |
| Pause/results | Intentional pause or real campaign outcome | Explicit pause/end condition | Popup after every local fight |

## 09. Compact HUD specification

[[FIGURE:hud_wireframe]]

Always visible: compact cash and police heat, a short current objective, pause, squad access and the compact minimap. With selected crew, show selection count/status and an understandable command row. On phones with less width, the objective wraps within its reserved card and the command row remains inside the safe area.

The management drawer owns the journal, operations, businesses/finance, HQ/care, destinations, camera settings, extended statistics and event history. Opening another management destination closes the drawer so two unrelated detail panes do not accumulate. Core commands remain immediately available during guided play; progressive learning must not become a payment or arbitrary unlock gate.

The existing art theme stays. Replace the permanent Command Desk with a small objective card and optional history. Replace six top navigation choices with Manage and essential status. Hide the nine-resource strip until the player opens details; preserve heat and money in the compact HUD. Keep a discoverable squad button and exact portrait selection.

### Interface rules

- Opening/closing a drawer preserves selection and orders.
- UI input is consumed by that control; it never issues an accidental MOVE underneath.
- A routine local interaction does not set the global time scale to zero.
- A contextual sheet includes the action, cost, risk and likely result in readable language.
- A failed action reports why: no selection, busy member, insufficient money, contested site or unreachable route.
- A command sheet closes without canceling an assigned duty; Cancel Target cancels only the pending target mode.
- Any long mission list uses a scroll viewport and leaves room for its last item.
- Navigation and button transitions do not bounce or jump.
- Minimap visibility is independent of opening/closing the squad drawer.
- Extended statistics and debug-like counters stay out of the default player flow.

Use readable labels, safe areas and primary touch targets of about 44-48 logical units or more. Confirm actual size, legibility and touch comfort on target phones.

## 10. RTS input contract and state transitions

| Input | Result | Invariant |
| --- | --- | --- |
| Tap unselected crew/world portrait | Add that member | Existing selection remains |
| Tap selected crew/world portrait | Remove only that member | Other selected members remain |
| Tap walkable ground | MOVE selected available members | Selection is unchanged |
| Tap ground with no selection | No order; brief useful hint if needed | No hidden auto-selection |
| Drag camera | Pan only | No MOVE at release |
| Pinch | Zoom only | No tap or selection side effect |
| Hold/box-select gesture | Show rectangle and select enclosed crew | Camera pan suppressed only for that gesture |
| Select command then target | Validate and issue that order | Unselected groups keep orders |
| Double-tap roster portrait | Deliberately focus that member | No permanent follow lock |
| Close context sheet | Return to city | Selection/duties remain |

SelectionState stores membership. PendingTargetState stores the command awaiting a target. UnitOrderState stores MOVE/GUARD/PATROL/ATTACK/RETREAT or a local assignment. CombatState handles temporary local threats. CameraState remains independent. OverlayState controls input blocking, not unit orders. A command can replace an order without changing selection.

### Exact client sequence

Tap Brick; order A; Brick remains selected. Order B; Brick moves again. Tap Dex; both are selected. Order C; both move. Tap Brick; only Dex remains selected. Order D; Dex moves while Brick keeps his previous task. Tap Brick again; he joins selection. The same sequence works in the city and roster.

### Failure handling

Validate each member separately. If three of four accept, report 3/4 and explain the busy/unreachable member. Do not show a successful order marker if nobody accepted. Reject neutral police as ATTACK targets unless explicitly hostile. Keep the camera fixed at the player's chosen location through order, combat and result. Dead/removed units may leave selection; unrelated living units must not.

## 11. Commands, autonomy and tactical purpose

| Command | Player target | Behavior | Interruption / feedback |
| --- | --- | --- | --- |
| MOVE | Walkable destination | Path in formation; arrive; stop | New manual order replaces; marker and accepted count |
| ATTACK | Living hostile unit/firm | Approach and fight in the city | No arbitrary offscreen target; hostile target marker |
| GUARD | Area or defended venue | Move, hold radius, defend nearby threats, return | Leash prevents city-wide chase; manual order wins |
| PATROL | Two valid endpoints | Traverse route, defend locally, resume route | Reject invalid/zero route; endpoint/route markers |
| RETREAT | Safe regroup/extraction | Disengage and move away | Incoming hits do not immediately cancel withdrawal |
| SCOUT | Recon location/task | Travel, observe, gain local intelligence | Separate from attacking; show intel effect |
| CAPTURE / CONTROL | Eligible territory/business | Reach, hold uncontested, complete progress | Pauses/cancels on contest/departure; visible ownership |
| DEFEND | Important venue | Use GUARD behavior tied to that asset | Can share existing guard implementation |

Guard and patrol are core commands. They should be available without ads, purchases or upgrades. Training may improve strength, stamina or skill; it must not sell the ability to give a basic RTS order.

The existing AgentController and gesture systems already contain duties, threat response and route visuals. Inspect and exercise them before replacement. A useful acceptance test moves one group to guard a shop while a second patrols a stadium route. A nearby rival triggers local defense; the groups return/resume. A manual retreat then succeeds while a third member performs a task elsewhere.

Combat should leave time to notice risk, redirect support and withdraw. Use tuning and animation evidence rather than imposing a universal fixed fight duration. Running, acceleration, turning and stopping must be reviewed using the actual current animation assets. This pass cannot certify animation quality from method tests.

## 12. Progression model and the first ten minutes

The following is a recovery design using current systems. Values are tunable defaults. Existing advanced campaigns retain progress and can skip the new teaching sequence through migration.

| Stage | Player goal | Systems introduced | Save point |
| --- | --- | --- | --- |
| 0 Orientation | Select and move one member twice | Crew, camera, MOVE | Each learned action |
| 1 Split control | Add/remove a member and move a subset | Portraits, selection count, separate orders | Lesson completion |
| 2 Read the district | Scout the nearby venue and recruit/prepare | Intel, HQ recruitment | Scout/recruit result |
| 3 First income | Control a named business and see income | Business state and cash flow | Control and income tick |
| 4 Protect the network | Guard one site and patrol another route | Autonomous defense and split squads | Duty lesson |
| 5 Prepare matchday | Tickets, supplies, social/transport preparation | Optional details become relevant | Prep flags |
| 6 Direct matchday | Start it and resolve simultaneous local incidents | Stadium phases and response choices | Phase/outcome |
| 7 Secure Home | Capture/defend key ground and finish core chapter | Territory, aftermath and reward | Home completion |
| 8 First Away | Choose travelers/reserves and enter Riverside | Away prep and sequential campaign | Launch/return |

Minute 0-1: the player sees three healthy members, starter funds and one instruction. There is no high-heat alarm or automatic matchday crisis. The player performs repeated movement and keeps selection.

Minute 1-3: demonstrate adding/removing crew. The player scouts a useful local venue; the UI explains where HQ care/recruitment lives. The lesson is saved so Continue does not repeat it.

Minute 3-6: send one group to the first food business, choose control/service, hold the site and earn the first payout. Leave a defender and move another group. The player can explain the relation between presence, control, income and recruitment.

Minute 6-10: finish preparation and choose Start Matchday. The city becomes busier; two incidents appear with location and stakes. The player responds by moving or assigning real crew. If preparation takes longer, no hidden timer punishes the player before matchday starts.

These times are pacing intentions, not measured completion guarantees. Advanced users can access details deliberately. Tutorial reveal must not hide required safety/retreat controls or make management inaccessible after a save migration.

## 13. Home chapter scripts

### H0 - Arrive and take control

Story: the firm has a small foothold near HQ. Objective: prove direct command. Instructions: tap Brick, tap the marked nearby street, then issue a second MOVE. Available actions: selection, MOVE, camera, pause. Task: add Dex, remove Brick and move Dex alone. Success is driven by accepted real commands, not a Continue tutorial button. No battle is required. Save each completed lesson. Reward: an explained squad drawer and the next venue objective.

Failure/recovery: if the target is blocked, choose a sampled reachable point and show the reason. If a crew member becomes unavailable, another living member can complete the lesson. Returning to the menu does not erase progress. A fresh game supplies enough units for split control.

### H1 - Learn the district and prepare people

Story: a nearby contact identifies the pub, recruitment point and first business. Objective: complete the real scouting operation and ensure a viable roster. Player selects a scout and sends them to the operation. Other members can remain near HQ. The operation requires travel and observed progress. The UI shows the gained intel and the next useful action. Recruitment is a choice with visible cost and deployable result; care is explained if anyone is injured.

Success: scouting complete and a viable crew remains. Reward: first business control opportunity and a clear finance explanation. Recruitment, training and hospital stay accessible from HQ rather than separate competing front-end screens. The base remains the same place throughout the campaign.

### H2 - Establish business income

Story: a food venue needs the area kept open for customers. Objective: establish control of one named business and receive a simulation income tick. Player reads yield, local risk and control time, sends available crew, and holds the site uncontested. Another group can scout or prepare while this runs.

Capture is not a click that instantly grants money. Arrival, presence and contest rules determine completion. After control, a visible marker and ledger show the owner and next income. The player may leave a guard or accept the risk of rival pressure. Optional looting offers immediate bounded money but raises heat and disrupts that venue's revenue. Loot is never necessary to learn the economy.

Success: saved business control plus first income. Reward: enough visible economic understanding to make a preparation choice, followed by guard/patrol teaching. Failure: contest or departure interrupts work; the player can reposition and retry without losing unrelated orders.

### H3 - Split the firm and protect a route

Story: rival movement threatens different parts of the district. Objective: assign a guard at one venue and a patrol on another street while a separate member completes a task. Player selects each subgroup deliberately. Duty markers and roster status show who is doing what.

Threats are local. A guard defends and returns; patrol defends and resumes. Manual MOVE/RETREAT takes priority. Success: useful assignments have been issued and their actual behavior is demonstrated in the integration acceptance script. Reward: matchday preparation becomes the current goal.

### H4 - Prepare the stadium district

Objective: collect tickets, supplies and a safe transport plan; build sufficient social support through relevant existing operations. The journal shows only unmet core prerequisites and explains their effects. First aid is optional if nobody needs it. Unrelated civilian extortion, taxi use and vehicle sabotage are side activities rather than compulsory Home-exit chores.

Player decisions: retain cash for care, choose who rests, leave defenders, plan which route reaches the stadium and which group covers the pub/business. Success: preparation flags plus viable crew. Reward: explicit Start Matchday becomes available. The everyday city continues while preparing.

### H5 - Manage the first matchday

Objective: handle two local situations with separate crew assignments while keeping a mandatory route reachable. One situation concerns supporters/stadium flow; another concerns pub/business/rival pressure. The player chooses positions and duties, not just one global response button. The normal RTS camera remains free.

Success: the required first-matchday responses resolve through actual presence/actions and the phase reaches aftermath. Consequences affect heat, morale, cash and the state of the district. Failure is explained locally and leaves a retry/recovery path. The player can manage a distant group while a local choice is open.

### H6 - Secure the base and unlock Away

Objective: capture the core Home ground and survive the organized rival push using the existing defense system. The business income and preparation systems provide alternatives to grinding repeated fights. Completion grants the Home reward once and unlocks Riverside.

Home completion is permanent. Coming back from Riverside does not require repeating H0-H6. Side operations remain available when appropriate. If roster losses make the chapter impossible, recovery/recruitment must remain affordable or provide a bounded campaign recovery choice without ads.

## 14. Away campaign and permanent Home

Reuse the existing five-part campaign structure. The following scripts clarify player purpose; they do not promise new city maps. The existing low-poly environment can support differentiated districts, objectives and pressures.

| Chapter | Preparation and purpose | Tactical objective mix | Success / consequence |
| --- | --- | --- | --- |
| 2 Riverside | Choose travelers, preserve Home defenders, scout waterside approach | Establish foothold, protect route, deal with local rival groups | Saved reward and injuries; unlock North End |
| 3 North End | More police risk; carry recovery resources | Scout patrols, hold route nodes, use reserves | Greater reputation; shortages and heat matter |
| 4 East Docks | Plan multiple assignments and extraction | Control dock approaches, support logistics, respond in parallel | District reward; maintain roster and Home economy |
| 5 Old Town | Use trained specialists and sufficient reserves | Read command groups, secure linked areas, protect extraction | Campaign outcome once; replay is deliberate |

### Launch contract

The prep screen explains destination, objectives, required living crew, travel costs if any, injury risk and who stays Home. The player chooses exact members. A lock explains the required previous chapter; it never silently redirects to a dashboard. Do not accidentally count nondeployed reserves as active fighters for an away requirement.

### Return contract

Commit money, injuries, casualties, mission completion and local consequences to the same campaign. Keep Home businesses/ownership and onboarding flags. Do not repeat old rewards when returning or reopening a result. Retry restores the documented pre-battle roster/state through the existing retry contract, not arbitrary new starter grants.

### Remaining content work

The recovery source replaces the legacy Away checklists with six connected Riverside goals and seven goals in each later district. The core combines scouting, relevant preparation, local firms, territory and an explicit eight-second extraction. Launch requires three living travellers for Riverside/North End and four for Docks/Old Town; nondeployed members remain Home reserves. An intentional empty selection remains empty. Actual extraction, injuries and reward-once behavior are separate from chapter balance: full natural playthroughs of each district still require acceptance. See the implementation status for current test evidence.

## 15. Management decisions and resources

| Decision / screen | Resource affected | Tactical effect | Feedback and failure |
| --- | --- | --- | --- |
| Recruit at HQ/contact | Cash, roster capacity | More independent assignments and reserves | Debit once; show new member or capacity/cash reason |
| Train | Cash, stamina/time | Stronger or more effective crew | Show stat change and readiness |
| Recover / hospital | Cash/supplies, injuries | Restore deployable members | Show cost and resulting health; bounded recovery route |
| Assign guard/patrol | Crew time and exposure | Protect income or route | Duty and local coverage visible |
| Control business | Presence/time | Recurring income and strategic foothold | Owner, contest and next tick |
| Loot business | Heat, venue disruption | Immediate limited funds with consequences | Reward, cooldown and lost income explained |
| Scout / observe | Crew time, intel | Read threats and unlock preparation | Intel gained and location result |
| Tickets / transport | Cash, intel, preparation | Access, safer regroup and route planning | Ready/not ready and missing prerequisite |
| Social/pub activity | Morale/social/time | Support and matchday resilience | Visible morale/support effect |
| Away preparation | Crew/cash/health | Traveler strength versus Home defense | Deployment review and return consequences |

Keep the current resources that have an actual decision attached. Extended counters belong in details until relevant. Resources must have visible sources, uses and consequences; nine unexplained numbers at launch are not useful management.

The economy should support peaceful preparation as well as conflict. Business control/income, mission rewards and deliberate reinvestment must be viable. A player should not be forced into constant civilian extortion to fund a basic playable session.

## 16. Shops, income, control and looting

Burger takeaway, bakery and pizza are owner-requested additions. The original request's burger/baker wording is interpreted as burger takeaway and bakery, consistent with the earlier recovery specification. Pub, cafe and convenience archetypes round out the neighborhood. Their exact names, rewards and times are tunable recovery content.

| Archetype | Identity and purpose | Risk / strategic distinction |
| --- | --- | --- |
| Burger takeaway | Small steady trade; clear first business | Low commitment; approachable learning site |
| Bakery | Predictable neighborhood income | Modest yield, useful defended route |
| Pizza shop | Stronger matchday demand | More valuable when stadium crowds arrive |
| Pub / sports bar | Supporter gathering, morale and busier matchday | More rival attention |
| Cafe | Information/social contact plus modest trade | Useful low-heat preparation |
| Convenience shop | Supplies and local cash flow | Contested location with easy resupply value |

[[FIGURE:business_loop]]

### Control action

Select available crew, choose Control on the world venue, route them to a reachable approach and hold within the configured radius. A contested site cannot complete while living hostile units are inside its contest radius. Progress belongs to that assignment; leaving, death or a new manual order cancels the assignment cleanly. Finishing changes saved ownership, updates the marker/ledger and starts income. It does not teleport money on every panel open.

### Income action

Use a saved simulation-time accumulator. Controlled, undamaged venues pay on the configured interval. Paused gameplay does not generate income. Reopening UI, loading a scene and calling Ensure repeatedly do not reset or immediately replay the interval. Existing network investments remain a separate explicit source; do not count them twice in totals. Show gross yield, disruption and the last/next payout.

Illustrative defaults: small venues pay roughly GBP 90-180 per 90 seconds of active simulation. First control takes approximately 12-20 seconds of on-site presence. These are development settings, not final balance promises. Keep them centralized and test affordability across the Home path.

### Optional loot action

Use an abstract fictional timed raid on an eligible venue. Show payout, heat cost and revenue disruption before committing. Selected units must reach the site and remain exposed. On completion award bounded cash once, increase heat, damage/disrupt that business and start a saved cooldown. The same click cannot pay twice, and scene reload cannot erase the cooldown. If the player leaves or is interrupted, the operation does not pay.

Loot should be a strategic tradeoff rather than an infinite cash button or a required tutorial step. Guarding one source of steady income while sending a second group on a risky raid creates a meaningful allocation decision. Keep the abstraction in game terms; no real-world procedure is needed.

### Rival counterplay

An unguarded controlled venue with hostile pressure can be contested and lost after a visible grace period. A nearby living defender or assigned guarding unit resists the takeover. Display an alert and map marker early enough to respond. Retaking it restores control through the same presence rules. Test that unrelated squads continue their tasks throughout.

## 17. Matchday with player agency

Everyday Home life continues. Matchday adds a structured pressure cycle after preparation and explicit player start. The first session must not begin with a major escalation ten seconds after loading.

| Phase | City changes | Player decision | Persistent state |
| --- | --- | --- | --- |
| Preparation | Normal city and venue activity | Read routes, prepare crew, choose positions, start | Prep flags/start state |
| Arrivals | Groups reach station/stadium/pub | Place escort, guard venue, scout rivals | Phase time and initial incidents |
| Build-up | Crowds and venue demand rise | Split response, protect income, avoid overcommitment | Local incident progress |
| Rival pressure / kickoff | Several threats/opportunities coexist | Guard/patrol/redirect/retreat using real units | Outcomes, heat and losses |
| Dispersal | Fans leave, pressure changes | Keep exit open and recover crew | Final phase/outcome |
| Aftermath | Compact result and normal district | Spend reward, care, continue Home/Away | Reward-once and completion |

### Example concurrent situations

Incident A: supporters need a safe stadium approach. A visible marker asks for a crew presence/guard assignment; progress increases only while valid members cover the location. Incident B: rival pressure threatens a food business or pub elsewhere. A separate group can defend it while A continues. Incident C can be optional police observation or a route check, within the simulation/device budget.

The player sees location, remaining response opportunity, status and stake. A dismissible world choice helps choose intent; actual movement and positioning carry it out. Ignoring a situation produces an explained local consequence at its deadline, not an arbitrary global Wasted result. Early matchday pressure must be survivable with the starter progression.

### Automation boundary

NPC arrivals, ambient movement and local AI are automatic. Player success depends on assignments, timing, risk and preparation. Passive observation should yield a different, visible result from competent active play. No event should automatically select units, clear their unrelated orders, lock the camera or force the whole city to wait for a popup.

### Persistence

Save started day, phase, elapsed phase time, incident outcomes and reward state. Resume the same matchday after Continue. Do not spawn a second director or pay the same incident twice. At aftermath, stop starting fresh peak-phase flashpoints; allow remaining local situations to settle and groups to disperse.

## 18. Roads, police, city readability and performance

### Road access is a gameplay invariant

The user reports complete road blockage. The exact blocked route is unverified because the original recording is missing. Test complete NavMesh paths from HQ and the current crew to stadium, recruitment, care, active business and extraction. Both outbound and return paths must be complete for the crew's actual navigation type and area mask, not merely accepted by SetDestination. A one-way route that strands the crew is not a valid mandatory approach. Sample safe approaches rather than putting markers inside buildings.

If a later closure is added, validate alternatives before enabling its blocker. A closure that isolates a mandatory objective must be refused or replaced by a soft visual closure that still permits the required route. Show barriers/route state on the city and minimap. Do not add solid crowds or permanent police collision walls on every exit. Traffic and crowd budgets must leave practical walking space.

### Police as a living faction

NPC police remain present in M2. Neutral patrols do not become automatic attack targets. A confrontation holds only the relevant group, allowing the player to manage other units. Bribe completion charges once, releases affected units, walks officers to the van and drives it away before removal. The existing source has a BribeExitRoutine; retain and verify it rather than assuming the behavior is absent.

Playable Police mode, station progression and its commercial integration remain M3. Existing NPC pressure must still be understandable and playable during M2. Startup heat should follow the quiet teaching design; old saves must not have earned heat silently erased by migration.

### Readability

Retain occluder handling, faction markers, minimap layers and roster focus. Review whether hiding whole building renderers gives acceptable continuity; gradual transparency may be a later visual refinement. Selected units must be selectable and trackable even when geometry lies between camera and crew. Use names/status sparingly so every venue does not create a stacked wall of labels.

### Runtime discipline

Reuse UI objects; refresh only when needed or at bounded intervals. Avoid full scene searches and label rebuilding every frame for each shop/incident. Cap interactive actors and maintain offscreen records. Keep decorative crowds visually varied without giving every spectator costly physics/navigation. Profile real Android/iOS before asserting frame rate or input-latency improvements.

## 19. Repository-to-requirement traceability

| System | Existing code owner | Recovery responsibility |
| --- | --- | --- |
| Entry and Continue | GameManager; MainMenuController; SimpleShellLayout | Preserve clear New Game/Continue and overwrite handling |
| Campaign data | GameData; PlayerData; SaveDataInLocal | Add compatible progress/business/matchday fields and isolated tests |
| Learning and chapter goals | CampaignMissions; recovery progression addition | Teach actual orders; show connected key steps |
| Default HUD | CityMissionHUD; LandscapeBattleHUD | Compact essentials, contextual commands, optional drawer |
| World input | AgentSelectionManager; RtsGestureController | Preserve additive selection, target modes and UI blocking |
| Orders and AI | AgentController; RtsOrderVisual | Guard/patrol/autodefense/manual overrides |
| City composition | CityGameplay; CityLandmarks; CityLifeActivity | Place reachable businesses and retain existing city |
| Business economy | CityDevelopmentSystem; new business ledger/system | No duplicate payouts, actual presence, saved control/loot |
| Matchday | StadiumMatchdayActivity; recovery director | Player start, concurrent positional incidents, save/resume |
| Police | LivePoliceSystem; PoliceCarChaser | Local choices and physical departure |
| Strategic overview | LiveMiniMap; icon registry; RtsBuildingVisibility | Accurate markers, route awareness and crew visibility |
| QA | Editor/Landscape checks and playtests | Meaningful source and live navigation tests; current evidence |

The audited workspace already contained uncommitted cache/save/new-game and layout changes, plus M1 tests/reports. They must be integrated and preserved. Do not reset the repository or replace the Unity project. The generated solution references older machine paths; use the installed editor and ProjectVersion rather than trusting stale csproj paths.

Unity version at audit: 6000.0.80f1. Automated saves use the existing editor override and disposable files under Artifacts/Recovery. Normal user saves/preferences must not be wiped to make tests pass.

## 20. Implementation work packages and order

| Package | Change | Depends on | Completion evidence |
| --- | --- | --- | --- |
| WP0 Evidence | Scope guide, issue register, code map, preserved source visuals | Supplied sources | This document and source appendices |
| WP1 Starter/progression | Viable small crew, low heat, versioned learning, core Home steps | WP0 | Fresh/resume/migration checks |
| WP2 Interface | Compact objective/status, contextual orders, optional management | WP1 | Current screenshots at phone/tablet ratios |
| WP3 RTS regression | Existing selection/duties/camera plus clear command availability | WP2 | Repeated movement and concurrent duty tests |
| WP4 Business loop | Named sites, control/contest, income, loot/cooldown and defense | WP1/WP3 | Presence, duplicate payout and persistence checks |
| WP5 Matchday agency | Preparation/start, saved phases, positional incidents, aftermath | WP1/WP3/WP4 | Active versus idle scenario, resume and route checks |
| WP6 Campaign/content | Home completion, Away unlock/return, optional side tasks | WP4/WP5 | End-to-end Home/Away scenario |
| WP7 Acceptance | Build, device touch/performance, evidence and client review | WP1-WP6 | Explicit pass/pending per platform |

Use small patches and reuse existing systems. A build should not claim every package complete because a feature class exists. Current implementation status is inserted in the next chapter from the project's status file when this PDF is built.

Local engineering can progress while physical-device access is pending. Store publishing, PlayFab production configuration and purchases require their own M3/release work and appropriate account access. They are not prerequisites for making the current Home/RTS slice reviewable.

### Rollback and compatibility

Keep additions versioned and preserve old fields. Existing campaign completion should bypass new teaching, not revoke unlocked Away destinations. A failed migration must not overwrite a good save with an empty roster. Test repeated Ensure/Load calls for idempotence. Avoid scene rebakes or asset replacements unless the relevant defect requires them.

## 21. Current implementation and verification

[[IMPLEMENTATION_STATUS]]

### Current implementation captures

The following images are current native editor fixtures for the expanded redesign: the named Home world and facilities, Home construction sheet, explained contextual Orders menu, and playable Police dispatch. They use isolated saves and accelerated/durable test crew. They do not establish physical device performance, natural campaign balance or client acceptance.

[[FIGURE:recovery_home]]

[[FIGURE:recovery_business]]

[[FIGURE:recovery_orders]]

[[FIGURE:recovery_matchday]]

[[FIGURE:police_world]]

## 22. Acceptance matrix for Codex and human review

| Test | Procedure | Pass condition |
| --- | --- | --- |
| T01 Fresh start | Start from no campaign and from a defeated save | Healthy viable crew, starter funds, quiet Home, no Wasted loop |
| T02 Save safety | Continue, overwrite cancel, New Game, reload | Correct resume; cancel preserves; new reset is complete and once-only |
| T03 Guided progression | Perform selection/move/split/scout/business sequence | One useful next instruction; saved learning; no all-systems dump |
| T04 HUD | Check 1280x720, wide phone and tablet landscape | City visible; cash/heat/objective clear; no overlap or inaccessible controls |
| T05 Stability | Repeatedly navigate and tap controls | No bounce/jump or changing hit area |
| T06 Scroll | Read to last mission/business row at small ratio | Last action and text accessible |
| T07 Feedback | Recruit/train/recover/spend/claim | Success or precise failure; charge/reward once |
| T08 Coherence | Compare title and gameplay imagery | Same intended city/style without heavier assets |
| T09 Selection | Brick A/B; add Dex; remove Brick; move Dex | Correct persistent membership and commands |
| T10 Split groups | Guard A, patrol B, run operation C | Independent orders; camera remains free |
| T11 Duty defense | Threaten guard/patrol then retreat | Defend locally, return/resume; retreat wins |
| T12 Targeting | Invalid ground/neutral police/busy crew | No false success; useful reason and retained selection |
| T13 Nonblocking city | Open/dismiss local decisions during other tasks | Simulation and other group control continue; no fight camera |
| T14 Visibility | Move behind tall building and focus roster | Trackable, selectable crew; occluder restores |
| T15 Movement/combat | Observe starts, turns, stops, hits and withdrawal | Readable natural motion and opportunity to react |
| T16 Minimap | Compare live positions, territory and events | Correct coordinate correspondence, layers and focus |
| T17 Matchday | Prepare/start; cover two distant incidents | Actual assignments affect outcome; normal city view |
| T18 Campaign | Complete Home, launch Away, return/reload | Sequential unlock and same persistent base/roster |
| T19 Paths | Validate HQ/crew to every mandatory destination | Complete reachable paths; closures cannot isolate objectives |
| T20 Control | Travel/hold; leave; contest; retake | Presence required; clean interruption; owner changes once |
| T21 Income | Tick, pause, reload, reopen panel | Correct saved cadence; no duplicate payout |
| T22 Loot | Begin, cancel, finish, immediately retry, reload | Bounded reward once; heat/disruption/cooldown preserved |
| T23 Rival pressure | Leave business exposed, then defend another | Warning and local contest; defenders matter |
| T24 Bribe departure | Pay and observe officers/van | Charge once; release; physical exit, no visible disappearance |
| T25 Arrival/city life | Walk dock, pub, station and stadium | Believable accessible locations, optimized life |
| T26 Touch/latency | Pan/pinch/tap/box-select on target phones | No gesture leakage/missed commands; measured behavior recorded |
| T27 Device/build | Install clean/update; load scenes; long session | No blank patches/crash/softlock in supported test matrix |
| T28 Delivery clarity | Review changelog, video and this matrix | Every claim maps to behavior and actual evidence |

Editor checks can prove ledger invariants, serialization and actual NavMesh/order behavior. Visual/editor screenshots can prove layout at a capture size. Neither establishes phone touch, thermal performance, iOS compatibility or client approval. Record those as pending until performed.

## 23. Review build demonstration and handover contract

Demonstrate one continuous journey rather than jumping between unrelated panels:

1. New Game enters quiet Home with living crew and money. Show one objective.
2. Select Brick, move A, move B, add Dex, remove Brick and move Dex alone.
3. Assign one group to guard and another to patrol. Move the camera freely.
4. Scout a named food business, control it, receive income and make a management choice.
5. Show optional loot with visible tradeoffs and cooldown; demonstrate cancellation before payout.
6. Prepare and start matchday. Show two situations and actual separate responses.
7. Prove stadium, care and extraction routes remain reachable.
8. Save/reload and show the same crew, ownership, lessons and matchday state.
9. Complete Home, unlock Riverside and return to the permanent base.

For each requirement provide the build identifier, platform, exact steps, expected/actual result, pass/partial/pending, relevant screenshot or short clip and known limitations. Avoid phrases such as all fixed when only a source change or old test report exists.

Release gates still needed after local work: Android touch/device playthrough, iPhone safe-area/touch and build environment, sustained city/matchday profiling, clean-install/update save tests, final content balance and explicit client M2 review. A locally produced APK is a review artifact, not store approval.

## 24. AI execution checklist and open tuning decisions

Every future agent must first read AGENTS.md, Docs/AI_SCOPE_GUARDRAILS.txt and Docs/Recovery/Implementation_Status.md. Trace the relevant requirement to code, inspect current uncommitted changes, reproduce the problem, make a compatible correction, run a meaningful check, capture visible effects and update status.

Do not resurrect rejected fight-camera or reselection behavior. Do not add another permanent panel to solve clutter. Do not make patrol, guard, retreat or the first playable session depend on ads/IAP. Do not force all side jobs before every Away. Do not claim missing video analysis, original-manual verification, device results or milestone acceptance.

The following remain tuning/product choices rather than reasons to stop authorized local work: final starter quantity beyond the minimum viable split crew, exact business yields/control times, matchday phase timing, incident response windows, target device models/frame budget, later Police mode entry presentation and final shop naming. Use documented reversible defaults and expose consequences clearly.

The user's road-block report and blank-patch claims require the actual affected route/device to establish precise reproduction. Continue path validation and local fixes now; record what remains unverified. Current implementation must remain bounded to the one-city mobile production target rather than expanding into multiplayer or a new open-world engine.

## 25. Source appendices and image inventory

Appendix A reproduces all eight pages of the supplied Phase 2 proposal. Its four embedded image objects include the cover/concept repeat and the Home, waterfront and tactical city references. These are conceptual visual references, as the original document states.

Appendix B reproduces all fourteen pages of the supplied earlier recovery specification. Its five embedded images are three stills on page 2, an Entering Home Territory loading frame on page 3 and the WhatsApp screenshot on page 4. The incorrect inherited modal/defeat captions are qualified in chapter 02. The originals and their source provenance remain intact. This preserves all nine embedded images from both supplied PDFs in this one document.

The earlier specification's claims about a 119-second video, October messages and exact M1 approval date remain inherited source claims. The current user independently reports that M1 was cleared. The raw September text was reviewed; its final client message is truncated. No missing text has been invented.

Research URLs, accessed 01 November 2026: [GameSpot hands-on preview](https://www.gamespot.com/articles/hooligans-storm-over-europe-preview/1100-2871935/); [reference identity and manual discovery](https://www.myabandonware.com/game/hooligans-f6e); [reference video link recorded in the earlier specification](https://youtu.be/W_IuAkQ_5ho). The first supports the gameplay patterns summarized in chapter 04. The second supports identity and the existence of a manual link. The third is an unreviewed lead, not analyzed footage.

The implementation appendix and current captures distinguish work performed in this recovery from legacy evidence. The preserved original PDFs remain source material even where this compiled plan resolves or qualifies their statements.

