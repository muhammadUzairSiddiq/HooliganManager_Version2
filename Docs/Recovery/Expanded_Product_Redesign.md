# Hooligan Manager: expanded product redesign

Version 3.0 - updated 02 November 2026. Working design for the expanded request. Read with `AI_SCOPE_GUARDRAILS.txt`, the recovery plan and the current implementation status.

This document defines the intended product. It is not a declaration that every feature is implemented, that the original game has been replicated in full, or that an editor test establishes a production release. The implementation status and current test reports identify what actually works. The current user explicitly requests a new presentation, a visible Home base to develop, playable Police mode and configurable monetization. These instructions expand the recovery beyond its earlier milestone boundary.

## 1. The product in one paragraph

Choose to lead a firm or command a police shift. Enter one readable, living city, select individual people, split groups and give orders directly in the world. A named, highlighted Home territory or police station gives the player a clear place to start and return. Build a small operational base, acquire income and supplies, develop specialists, prepare routes and manage simultaneous events around the football ground. Make tradeoffs between staffing, money, readiness and exposure. Fighting is one available tactic. Progress follows visible objectives with concrete world actions and saved consequences. The city remains alive and the camera remains under player control.

## 2. Authority and evidence

| Label | Meaning and source |
| --- | --- |
| CLIENT CONFIRMED | Supplied WhatsApp transcript, 16-30 September; timestamps contain no year. |
| CURRENT USER REQUEST | Expanded instruction on 01 November 2026: rework presentation as a new game over reusable backend; explicit Home area; simpler controls; playable Police; ad config and test ads; full reference-based design. |
| REFERENCE VERIFIED | Firsthand published reporting about the original PC game, linked below. It supports design principles but does not prove every scenario, number or interaction. |
| PROPOSED DESIGN | Concrete implementation decisions in this document, including construction, mobile context menus, shift tasks, economy defaults and ad placements. |
| SOURCE CODE OBSERVED | Behavior read in the repository, which still needs runtime verification in the current build. |
| CURRENT TEST | Reproducible test of a known build with its fixture and limits recorded. |
| UNVERIFIED | Original client video, original October messages, a complete original manual, full reference campaign walkthrough and physical device acceptance have not been supplied or completed. |

### Client requirements that must remain intact

- 17 September, 17:34: client confirms stadium/pub surrounding activity and social activity in Milestone 2, playable Police and monetization in Milestone 3, QA/polish/store upload in Milestone 4. The current request authorizes starting Police and ads now; the earlier division does not prohibit that work.
- 19 September, 14:29: Home is the permanent, interactive base. Management, strategy and social activities share missions; fighting is only one part. Home must not become a list of menus, and Home tasks must not reset before each Away trip.
- 19 September, 15:00: matchday must bring arrivals, busy pubs, rival fans, police and strategic events into the living district.
- 20 September: player must be able to enter, recognize Home, see the city, understand the next action and assess actual changes. Jumping UI is unacceptable.
- 25 September, 02:03: selecting Brick and moving twice keeps him selected; adding Dex retains Brick; tapping Brick removes only Brick. No separate individual-task mode.
- 25 September, 12:49: Move, Attack, Guard and Patrol have real purposes. A patrol defends against an intrusion and resumes. Multiple groups and incidents coexist around the stadium in the normal free-camera view.
- 30 September: bribe officers physically leave; buildings stop hiding crew; individual members remain trackable; minimap corresponds to the city; movement needs natural acceleration, turning and stopping. The experience is management, RTS strategy, living city and football atmosphere.

Historical developer statements such as "completed" and "implemented" are not client acceptance. The user's report of M1 clearance is retained. The supplied September transcript confirms playable Police as a category, but does not provide the full patrol/arrest mission specification now described by the user. That detailed mode is an expanded design below.

## 3. What matching Storm Over Europe means

The GameSpot hands-on preview describes directly controlled core members, specialist roles, pub recruitment/morale, persistent unit experience, exploration, cash from shops and scenario goals that require route planning. The important lesson is a network of useful places and limited people with distinct capabilities, rather than one fighting blob. Police act as opposition in that account. A permanent construction base, modern mobile gestures, business yield model and playable police career are adaptations requested for this product. [GameSpot hands-on preview](https://www.gamespot.com/articles/hooligans-storm-over-europe-preview/1100-2871935/)

Worthplaying's firsthand review describes contextual store actions, visible pub ownership, a difference between commanded recruits and ambient followers, and objectives involving routes, escorts and rescues. Its account reinforces object-specific choices and several ways to solve a mission. These principles guide the redesign; its particular campaign scenes and assets are not copied into the current project's completion claims. [Worthplaying review](https://worthplaying.com/article/2002/2/5/reviews/2202-pc-review-hooligans-storm-over-europe/)

### Reference alignment and gap register

| Reference pillar | Product implementation target | Evidence at this design revision |
| --- | --- | --- |
| Small controllable core | Named persistent members; additive selection; independent orders; role and readiness | Existing roster/input backend now uses a dedicated contextual HUD. Native RTS checks pass; additional Orders transition checks are pending. Device usability remains unverified. |
| Places matter | Pub recruits/rallies; food shops trade; care restores crew; station/roads support travel | Named businesses, highlighted Home and crew-built facilities are implemented. Native Home construction/control/recruitment checks pass; natural discovery and full progression still need human play. |
| Specialist tactics | Role differences affect scouting, logistics, care, crowd leadership and defense | Saved management roles/stats exist. A complete specialist capability tree is not certified. |
| State and development | Health, stamina, morale and experience change allocation choices | Existing data. The full original eight-condition model is not reproduced and must not be claimed. |
| Scenario purpose | Scout, resource, escort, hold, extract and return objectives | Recovery Home/Away backend; a full campaign played naturally remains an acceptance requirement. |
| Commanded core versus followers | Player orders trained crew; supporters provide bounded local support | Existing ambient/support systems. No implication that every visible fan is controllable. |
| Local police pressure | Readable neutral patrol, escalation, arrest, bribe and physical departure | NPC pressure and a separate playable Police career are implemented. The native Police suite covers patrol, dispatch, detention, escort, booking and firm-save isolation. |
| Urban view and routes | One map, freely panned/zoomed, readable factions, bidirectional navigation | Existing camera/map/NavMesh backend. World readability and actual congestion remain essential tests. |
| Tactical choices beyond combat | Income versus loot, guards versus expeditions, scouts versus brute force | Recovery business/resource systems. Consequences must be visible at the time of choice. |
| Football identity | Stadium rhythms, club colors, songs/audio, arrivals, pubs and dispersal | Current crowd/activity systems. Player agency and scene composition need review. |
| Original full campaign/multiplayer | A separately planned content program, not assumed by a reference label | Not implemented or verified by this redesign. |

Do not equate the number of menu entries, automatic brawls or unit counters with reference fidelity. The comparison is whether the player allocates people, explores, chooses routes, exploits roles and sees consequences.

## 4. Historical baseline: why the earlier recovery felt small

SOURCE CODE OBSERVED before the expanded redesign: `CityGameplay.Start` initialized nine destination labels, multiple social crowds, operations, actions, development, train scheduling and rival activity at entry. `CityMissionHUD` constructed eleven management destinations even when its drawer was closed. Move/Attack/Guard/Patrol remained peers on the lower edge. Home was essentially the spawn point, without a visually dominant owned, buildable district. Those findings describe the superseded presentation. The dedicated HUD, contextual Orders and highlighted Home implementation now have native review evidence in `Implementation_Status.md`; complete natural progression and device usability remain acceptance work.

The expanded redesign addresses that baseline through these requirements:

1. Establish one visually obvious starting territory with a meaningful construction action.
2. Put the next goal at a visible object, not only in a sentence at the top.
3. Make tap-to-move the default and expose other tactics through one explained contextual entry.
4. Reveal services when they gain a purpose; retain advanced access without filling the default scene.
5. Teach allocation through a visible result: one member works while another moves, guards or scouts.
6. Tie chapter completion to changed places and people, not a hidden checklist.

## 5. Title, mode choice and save flow

Title shows the existing city's imagery with two clear choices: **Hooligan** and **Police**. Each card explains the role in one line. Hooligan: "Build your Home base, lead your firm and prepare for matchday." Police: "Command patrols, respond to incidents and make arrests." A chosen mode opens Continue or New Campaign/Shift. Settings are shared and secondary; credits/exit can be in settings.

Do not make Away Trips a separate mode equivalent to Home. Away is progression inside the Hooligan campaign. Do not show a disabled Police button when a playable implementation is present. Do not route New Game through a dashboard of unrelated screens.

Each mode has its own persistent state and resume destination. Entering Police cannot overwrite or revive the firm roster. Returning to Hooligan cannot spend police funds or apply police outcomes. A destructive reset names the affected mode and requires the existing overwrite confirmation. Audio, frame-rate preference and accessibility settings can be shared.

The Continue caption says where and what: "Hooligan - Home Territory - establish your base" or "Police - Shift 2 - 1 incident active." It should not expose a save filename or backend account details.

## 6. Default screen and progressive information

PROPOSED DESIGN: the default world view has five information groups, with most of the display reserved for the city:

- A small mode/base identity and primary resource. Firm shows cash and police heat; Police shows shift score/readiness and active dispatch count.
- One current objective with progress and a deliberate **Show me** control.
- One small squad access control, selected member count and a short control hint.
- A compact minimap with only relevant markers by default.
- A primary contextual action, when one is available, plus **Orders** for selected units. A target operation replaces the hint with exactly what to tap next and a cancel control.

Move is not a permanent button competing with every other command. Tap a street to move. Orders opens Guard, Patrol, Regroup and other relevant tactics with explanations. A rival tap opens an explicit attack decision for selected members. A business tap opens Control/Income/Loot choices. Home tap opens its own build/develop/crew tasks. The player can learn the interface from the object they are looking at.

### Information levels

| Level | Visible now | Example |
| --- | --- | --- |
| Immediate | Next action, selected people, active target instruction | "2 selected - tap a street to move" |
| Local | Relevant object card or order options | "Burger takeaway: control in 12s; trade GBP 90/90s" |
| Planning | Home/development/crew/dispatch sheet | Compare recovery cost, business staffing and Away travellers |
| Detail | Full journal, statistics, log, map layers | Inspect completed chapters or earlier incidents |

Menus do not need to disappear from the game permanently; they should be reached from a meaningful decision. First-time access can be stage-filtered, with clear locked reasons. Core commands always remain available and never require an ad, payment or tutorial unlock.

## 7. Home territory: a place to establish and develop

CURRENT USER REQUEST: Home must be a complete highlighted, named area that the player develops. PROPOSED DESIGN: use an existing clear city parcel near the starting crew. Its polygon/outline, soft ground tint, entrance sign and minimap outline share the same ownership color. Label **HOME TERRITORY** with the firm name. Put a clear HQ marker inside it. The highlight identifies ownership; it must not be a solid wall or collider.

The Home visual hierarchy is distinct from rival turf rings. Before establishment it says **ESTABLISH HOME BASE**. After establishment it says **YOUR HOME TERRITORY**. Development plots have distinct existing-model props or signs, not six identical floating rings. Ground paint remains subtle enough to see people, streets and order markers.

### Home state model

Use additive saved fields, with a schema version:

- Stable territory ID and map-relative/scene-authored anchor.
- Established flag and construction progress/time.
- Headquarters level and unlocked facilities.
- Per-facility construction state, remaining simulation seconds, cost paid and level.
- Intro and world-interaction lesson flags.
- Stable links to business control, home resource snapshot and defense completion.

The current implementation saves completed facility states. In-progress Home construction is not restored after a scene reload; its crew assignment and elapsed work restart without a charge. The fuller state model above remains a design target, not a claim that queued construction, multiple building levels or mid-build resume already pass current tests.

Use simulation time. Reopening a panel does not finish construction. Pause/background does not create unlimited income/construction. Saving after payment cannot charge again when resuming. An existing campaign migrates sensibly: a cleared Home is recognized as established and gets no new reward payment. Do not reset old businesses or demand that advanced players repeat the new first minute.

### Establishment and build rules

1. The player taps Home; its local card gives purpose, cost, time and result.
2. Select a living available member and confirm the relevant build action. The member travels into the highlighted area and performs visible site work.
3. Work progresses only with valid presence, and shows a local progress label and a compact task status in the squad.
4. New orders, death or departure cancel/pause according to the declared rule; never silently take over an unrelated group.
5. Completion changes visible props/signage and saved facility state. Pay any one-time introductory reward exactly once.
6. Home's action changes to the next purposeful task: recruit, train, recover, develop a facility or plan an unlocked trip.

Illustrative facilities: HQ office/meeting space, crew training yard, recovery corner, supporter club and logistics store. Reuse existing art and code paths; construction needs a visual result and tactical effect, not a menu-only stat. Exact costs and durations belong in central tuning. Keep the first construction affordable from the starter funds without an ad or crime.

## 8. First session: a playable script

These are proposed direction stages. Completion requires actual actions and results, not opening a tutorial page.

| Stage | What the player sees | Player action | Visible result and transition |
| --- | --- | --- | --- |
| H0 Arrive | Crew beside one highlighted Home plot; quiet city; "Tap a member" | Select Brick; tap a nearby street; tap another street | Persistent ring and move marker; goal advances only after accepted movement |
| H1 Own the base | Home entrance and work plot are highlighted | Tap Home, send selected member, establish HQ | Work animation/progress; HQ sign/prop; Home now visibly owned |
| H2 Split people | One base task and scout location | Add Dex; remove Brick; move Dex while Brick remains working/guarding | Both task states visible; no global re-selection |
| H3 Find trade | One named nearby food venue and its approach | Open venue card; compare steady trade to loot; send one member to Control | Timed uncontested work; ownership changes at the actual storefront |
| H4 Make income useful | Owned venue, next payout and HQ choice | Wait or perform another task; spend some earned money on readiness | Income receipt, saved balance, actual role/stat/care benefit |
| H5 Defend a route | Rival movement nearby and two useful sites | Guard a venue; patrol another route; direct a third member | Local defense, return/resume; retained duties after manual selection changes |
| H6 Prepare football day | Station/pub/stadium tasks become relevant | Gather supplies/tickets/support and prepare transport | Icons change to Ready; matchday board explains why readiness matters |
| H7 Start matchday | Two response markers and people arriving | Explicit Start; position separate groups; react to local threats | Coverage/outcomes depend on living people and the timeline |
| H8 Secure Home | Rival turf and organized HQ threat | Capture a useful route and defend HQ using prepared groups | Home chapter reward once; no return to a menu dump |
| H9 Plan Away | Riverside destination at HQ | Choose exact travellers and Home reserves; review risks/cost; launch | Home persists; active travellers travel; Away objective appears |

The world can show optional life at every stage, but optional activities must not visually compete with the taught objective. Early threats should be legible and survivable. There should be no new major event that steals the camera or arrests the whole starter crew before basic control is learned.

## 9. Complete RTS interaction contract

| Input/context | Immediate behavior | Feedback |
| --- | --- | --- |
| Tap a crew member/portrait | Toggle that member only | Ring, selected count, name/status |
| Tap walkable ground | Move the current available selection | Destination marker, accepted count, retained selection |
| Tap an invalid destination | Keep the existing order | Short reason and a safe prompt to choose another street |
| Drag empty world | Pan camera | No unit order on release |
| Pinch | Zoom | No accidental tap/order |
| Tap Orders | Open one small tactics sheet | Each command states target and effect |
| Hold world with selection | Optional shortcut to the same Orders sheet | Discoverable alternate, never the only route |
| Tap hostile rival | Show target-specific action | Attack selected group; one rival/group identified |
| Tap venue | Show that venue's functions | Control, defend, supplies or optional raid, with consequence |
| Tap Home | Open local base actions | Build/develop/crew/travel as relevant |
| Double-tap portrait | Focus deliberately on that person | Free camera remains available afterward |
| Close sheet | Return to world | Selection and assigned duties remain |
| Cancel targeting | Cancel only pending target mode | No change to existing unit duties |

### Command definitions and examples

**Move:** select Dex, tap the pub approach. Dex moves and remains selected; another street tap redirects Dex. Brick guarding Home keeps guarding. No second mode or Move confirmation is necessary.

**Attack:** select two available crew; tap a hostile firm and choose Attack. The command names the target. Neutral police/civilians are not silently converted into attack targets by a generic button. Existing local self-defense still works. Declined/busy crew are counted accurately.

**Guard:** choose Guard from Orders or Defend from a venue. Tap/drag the defended area. Members reach posts, protect the area within a bounded leash and return after a local threat. Explain "Stay here and defend nearby." A successful order draws a subtle boundary and roster duty.

**Patrol:** choose Patrol; instruction says "Tap route start" then "Tap route end." Show A/B and the actual valid route preview. Confirm a valid route. Patrol fights locally when appropriate and resumes. Invalid or tiny routes are rejected before replacing the last duty.

**Regroup:** use plain player-facing wording for the previous Retreat function. "Return selected crew to Home/arrival and disengage." Show which safe destination is used. Hits do not immediately cancel that order. Other groups keep working.

**Capture/Control:** choose an eligible target in the world, see the hold condition and send selected crew. Ownership changes from real presence, uncontested time and any declared cost. The marker and minimap change together. A button does not instantly create ownership.

**Scout:** send a selected member to observe a named location. Intelligence changes what the player can prepare/see; it must not be a progress counter with no use.

**Police Arrest/Disperse/Patrol:** reuse direct selection and navigation semantics but present role-appropriate commands. An arrest is a timed local interaction with an eligible suspect, not a button that removes an arbitrary civilian across the map.

## 10. Unit identity and specialization

The player controls the named core. Ambient fans are visible support and atmosphere, not accidental selectable units. Color is reinforced by ring/shape/status so the city is readable without relying on shirt color alone.

PROPOSED DESIGN roles use the existing management profile as a foundation:

| Role | Allocation value | Tradeoff |
| --- | --- | --- |
| Leader | Rally supporters, reduce local panic and coordinate a small group | A valuable person unavailable for another task while rallying |
| Scout | Read rival/police activity and prepare safe routes | Less effective in a prolonged fight |
| Defender | Hold venue/route with a stronger defensive radius or resilience | Slower logistics/scout work |
| Logistics | Faster supplies/construction/transport preparation | Less direct combat output |
| Care/support | Efficient local recovery and readiness | Must be protected and staffed |

Not every reference unit type or original ability already exists. Add capability checks before advertising role-dependent features. Show one meaningful role and readiness summary on the squad card; full stats belong in details. Upgrades should improve an action's effectiveness, not sell basic Move/Guard/Patrol access.

## 11. Economy, shops and allocation

The Home economy must make noncombat progression viable. An owned trading venue produces steady bounded cash during active simulation. A raid gives immediate money but heat, cooldown and lost trading time. Guarding a profitable venue reduces takeover risk; taking every living member Away leaves Home exposed. Care and readiness compete with expansion for funds.

Each venue has a stable ID, visible storefront/sign, approach, owner, income, control time, loot value/heat, disruption, cooldown and threat status. Burger, bakery, pizza, pub, cafe and convenience are distinct local sites. The reused bakery shell should be described honestly as repurposed art, not newly commissioned art.

The local venue card has one primary action and optional details. An uncontrolled venue: **Take control** with hold/cost. Controlled: **Defend this business** and next income. Raid: secondary choice with payout and risk visible before confirmation. A cooldown explains its remaining time. Do not offer a normal-action button that secretly runs a timed assignment elsewhere.

Show financial consequences in the places they are caused. A missed pub response says trade is disrupted; a rival contest says send defenders; a successful control changes owner/name color at the shop. The full ledger is useful for comparing businesses, but it cannot substitute for local identity.

## 12. Matchday: player-run strategy in a living district

The active player decides when to start after preparation. The introduction is a short local brief, not a cutscene that changes the control scheme. Save phase and total elapsed time, responses, consequences and paid outcomes.

Arrival brings groups through station/stadium routes. Build-up increases pub and street activity. Rival pressure creates concurrent local incidents. Kickoff changes movement demand. Dispersal moves groups toward exits and stops creating new peak pressure. Aftermath presents a compact result and next choice.

Required situations have a place, remaining opportunity, stake and visible response condition. A first scenario can ask one group to keep the stadium approach clear and another to defend pub trade. An optional observation task offers a lower-heat alternative or useful intel. The city does not automatically select members or carry out successful responses for the player.

The recovery's current 20-second coverage, 270/420-second deadlines and phase lengths are development defaults, not proof of the reference game's rules or balanced mobile play. Compare total walking time at production speed with each deadline. Give the player time to read, allocate and arrive. A path test alone is insufficient if a route is clogged in practice.

Competent play and passive observation must produce different understandable outcomes. Missing a response applies a declared local consequence and an available recovery path. Aftermath retries retain prior successful responses and never replay rewards or penalties. The campaign record waits for the actual aftermath result. A full mission does not finish simply because two markers were staffed for twenty seconds at the start.

## 13. Away scenarios and permanent Home

Away objectives should create a reason to travel and an allocation puzzle, not repeat every city service. Use the existing four destinations as chapters with distinct tactical purposes:

| District | Mission purpose | Required tactical mix | Optional activities |
| --- | --- | --- | --- |
| Riverside | Establish the first foothold and learn expedition logistics | Scout, supplies/transport, rival network, one secure route, physical extraction | Taxi, local raids, social tasks |
| North End | Operate around stronger police pressure | Scout/observe, supplies, two route nodes, rival groups, extraction | Supporter rally, care and risky shortcuts |
| East Docks | Split personnel across logistics and protection | Scout, supplies/community support, linked dock approaches, rival groups, extraction | Extra territory and venue income |
| Old Town | Read and dismantle a coordinated network | Scout/observe, sustain people, linked territory, command groups, extraction | Additional objectives and risk for optional reward |

The chosen travellers are saved separately from the Home reserves. A deliberate NONE selection is valid preparation UI and makes Launch unavailable with a reason. Launch requires a viable team and debits once. Continue uses the same travellers. On-map recruits obtained Away join that trip explicitly. Loss of the travellers cannot silently deploy reserves into an already lost scene.

Extraction is a world action at a visible arrival/exit zone. All surviving deployed members must regroup there and hold the clear exit for the configured period. Requesting extraction moves only the selected subgroup. Other groups continue until the player gives them an order. Save chapter reward/unlock once, then return to the same Home and roster consequences.

## 14. Playable Police mode

CURRENT USER REQUEST and CLIENT-CONFIRMED later-milestone category. This is an explicit product extension, not a claim about the original game's playable factions.

The player is the police commander, with a named station base, selectable officers, patrol coverage and local dispatches. They choose routes, allocate officers to incidents, protect public access, detain eligible suspects and return them to processing. The city, civilians, rival firms and supporters remain active. No automatic camera takeover turns a dispatch into a separate fight scene.

### First shift script

1. Enter a station-adjacent clear area. A station boundary and three named officers are visible. One objective: select an officer and move to a nearby beat.
2. Give Patrol between two markers. The shift observes actual officer presence over time, not only clicking Patrol.
3. A minor disturbance appears at a named site. Tap its dispatch card; it says location, officer requirement and outcome. Assign a different officer while the patrol continues.
4. At the incident, an eligible suspect is marked. The player chooses Arrest; officer approaches and holds proximity for processing. Interruption, suspect escape or invalid target produces a clear reason.
5. The detention/escort or return-to-station stage completes through actual world action. The shift score and task record update once. Reopening the incident cannot duplicate an arrest.
6. A second dispatch appears elsewhere, requiring a choice between patrol coverage and response. Unassigned officers keep their orders.
7. The shift ends through a deliberate station action after its required objectives, with a concise report and saved career progression.

### Police task families

| Task | Player action | Completion evidence | Consequence |
| --- | --- | --- | --- |
| Beat patrol | Choose officers and route | Required actual coverage time/waypoints | Local safety/shift score |
| Disturbance response | Reach named dispatch and maintain presence | Officers at the site with incident requirements met | Escalation prevented or reduced |
| Suspect arrest | Select officer, choose eligible suspect | Timed approach/proximity, detention once | Arrest count; transport/processing workload |
| Crowd stewarding | Guard a stadium/pub exit | Maintained usable exit/coverage | Safe dispersal; fewer incidents |
| Escort/transfer | Move detained suspect or protected group to destination | Actual safe arrival | Processing completed; officers free again |
| Station return | Regroup/extract assigned officers | Present at station/shift conditions | Shift result, recovery and next shift |

Failure must be recoverable. An officer downed or an incident missed does not destroy the Hooligan campaign. Explain what was lost, what remains active and whether the player can retry or finish a reduced-score shift. Never reward attacking neutral people as a normal police objective. Arrest eligibility is game state, not a skin color or arbitrary nearby actor.

## 15. Monetization configuration and player flow

CURRENT USER REQUEST: provide a ScriptableObject where the owner enters test/real IDs and chooses No Ads, Test Ads or Live Ads. Core RTS, save/retry and basic recovery remain available without an ad.

The settings asset must make the active mode unmistakable. Store separate Android/iOS application IDs and ad-unit IDs, supported formats, optional initialization, rewarded placement configuration and test-device configuration. No Ads initializes no provider and never leaves a blocking ad spinner. Test Ads use the SDK's official test units on device. Editor simulation is labeled simulation; it is not evidence that a real ad network served an impression. Live Ads must not silently fall back to test IDs as if revenue were live.

The first useful placement is voluntary rewarded support from a Home/station planning screen. State the reward before the click and grant it only on the SDK's completed-reward callback. Failure/cancellation gives no reward and returns control. A repeated callback/late duplicate cannot grant twice. Do not put an interstitial over movement, combat, emergency dispatch or an ongoing targeting gesture. Interstitials, if enabled, belong only at deliberate completed scenario/shift boundaries and have frequency limits.

The provider integration needs its actual SDK/package, initialization and platform manifests/settings, callback cleanup and main-thread dispatch. A config-only mock is not production ads. Account setup, consent configuration, privacy disclosures, store declarations, network availability and physical device test impressions remain explicit release work.

## 16. World readability and atmosphere

Use the current low-poly city and imported characters while creating a new readable composition. Home, station, owned venues, rival turf and active objectives have distinct visual roles. Reserve large labels for the active objective or player-selected place. Nearby minor venue names appear on discovery/tap. Avoid simultaneous world labels, status text, interaction bubbles and a full-screen feed for the same event.

Selected crew stay readable through geometry using the existing occlusion system, rings and minimap. Avoid hiding entire irrelevant city geometry just because an ambient NPC is behind it; prioritize player-controlled members and the relevant local scene when tuning. Character visibility must not depend on permanently drawing giant labels over every pedestrian.

Football identity comes from movement patterns and activity: groups arrive together, queues/approaches remain usable, pub crowds gather, different firms occupy distinct spaces, stewards/police patrol, club flags/colors and appropriate audio reinforce the setting. Matchday is different from a normal weekday even when no fight is on screen.

Preserve complete routes both outbound and back for the actual controlled agent type/area mask. Shops and Home construction props must not create road blockers. Keep actual crowd/traffic congestion under review. A NavMesh route can technically exist while being uncomfortable on a phone.

## 17. Technical implementation map

Existing code is a backend to reuse after exercising its real behavior. Additive changes should respect existing saves and unrelated working-tree changes.

| Concern | Existing backend | Redesign responsibility |
| --- | --- | --- |
| Entry and resume | MainMenuController, GameManager, SimpleShellLayout | Two clear roles, separate resume paths, no menu dump |
| Roster and persistent state | PlayerData, AgentData, GameData | Add Home construction and mode state with migration; preserve account preferences |
| Selection/orders | AgentSelectionManager, AgentController, RtsGestureController | Context-first interface, exact persistent toggle semantics, one target owner |
| World guidance | RecoveryProgression, CampaignMissions, CityMissionHUD | Physical Home stage, focus destination, staged exposure and concise hints |
| Home/business economy | CityDevelopmentSystem, CityBusinessSystem | Visible local development and strategic effects, existing ledger reuse |
| Crowd and matchday | StadiumMatchdayActivity, MatchdayStrategySystem | Timed player allocation, visible consequences, group persistence |
| Police | Existing NPC police plus new playable mode | Independent officer control, dispatch/arrest/shift loop and save isolation |
| Map/visibility | LiveMiniMap, RtsBuildingVisibility, WorldButtonLayer | Home/station bounds, relevant symbols, world/UI correspondence |
| Monetization | New provider/config layer | Explicit modes, official test units, rewarded completion once |
| QA/build | Existing editor suites and isolated save override | Tests for new user flow, actual controls and platform build; no reused green tick |

Separate state owners: selected members; pending command/target; standing duty; temporary combat; local assignment; camera; open sheet; active mission; mode save; ad request. A UI sheet must not secretly own crew orders or reset Home progress. A provider callback must not modify a different mode after a scene switch.

## 18. Expanded acceptance script

| ID | Action and expected result | Evidence required |
| --- | --- | --- |
| E01 | Title clearly offers Hooligan and Police with different role descriptions | Current screenshot and actual click paths |
| E02 | Each mode's New/Continue route affects only its own campaign/shift | Native save isolation/reload check |
| E03 | Fresh Hooligan entry shows a named Home boundary/HQ, crew and one next goal | Current phone-ratio capture; actual starter stats |
| E04 | No lesson is completed by startup auto-selection or panel opening | Native new-game assertions |
| E05 | Tap crew, move twice, add/remove one member exactly matches the client sequence | Actual input/playmode and physical touch check |
| E06 | Ground tap moves by default; no permanent unexplained Move/Attack/Retreat rail | Visual audit and interaction test |
| E07 | Orders clearly explains Guard/Patrol/Regroup and target/cancel state | Screen captures and no tap leakage |
| E08 | Home establishment uses physical work and changes a saved visible base | Runtime construction, interruption and reload |
| E09 | Home costs and build timers cannot duplicate payment/progress across reload | Native serializer/runtime assertions |
| E10 | Existing cleared Home migrates as established without starter/reward duplication | Legacy isolated save case |
| E11 | Only the current task is emphasized; advanced services remain discoverable | First-minute observed play and screenshots |
| E12 | Separate selected groups work/guard/patrol without overwriting other orders | Native integration plus human play |
| E13 | Controlled business visibly changes owner and produces actual timed income | World capture plus saved economy evidence |
| E14 | Raid/revenue/heat tradeoff is shown before assignment and saved after | Runtime action, cancellation/cooldown checks |
| E15 | Matchday start remains deliberate; arrivals/activity differ visibly from weekday | Before/after captures and player-driven start |
| E16 | Two incidents depend on actual allocation and preserve free camera | Native scene test and manual play |
| E17 | Missed deadline consequence and recovery are understandable; reward/penalty once | Runtime boundary/retry/save tests |
| E18 | Mandatory destinations and exits have complete routes both ways | Actual crew-filter path checks and congestion playtest |
| E19 | Crew remains visible and trackable through buildings and on the minimap | World/map comparison at multiple camera angles |
| E20 | Away exact travellers/reserves persist; launch debits once; physical extraction returns Home | Native scene transition test |
| E21 | Police starts with actual controllable officers and a clear station objective | Current mode launch and city screenshot |
| E22 | Patrol coverage and a simultaneous dispatch progress through actual officers | Runtime route/presence assertions |
| E23 | Arrest requires an eligible nearby suspect and cannot pay twice | Approach/interruption/eligibility/reload checks |
| E24 | Police shift completion persists separately from firm cash/roster | Native save roundtrip and role switching |
| E25 | No Ads, Test Ads and Live Ads config branches are distinguishable and safe | Config validation plus provider adapter tests |
| E26 | Rewarded placement pays only once after completion; cancel/failure pays nothing | Callback tests; real SDK test ad on device |
| E27 | Long session and background/resume preserve assignments and save stability | Physical Android/iOS soak test |
| E28 | Full Home and each Away chapter are played naturally with production stats | Recorded end-to-end checklist and balance notes |
| E29 | Current review build installs/updates, meets safe-area/input/frame/memory budgets | Device matrix with exact build hash |
| E30 | Client reviews the intended loop and reports acceptance against this script | Explicit client response; never inferred from developer claims |

## 19. Production-readiness definition

The product is release-ready only after the intended modes and campaign content are complete, tested naturally, packaged with the real SDK configuration, and validated on target physical devices. Record exact build hashes, supported devices/OS versions, install/update paths, save compatibility, input response, memory/frame pacing, background/resume, audio behavior and network/ad failure handling. App/store identifiers, signing, privacy/consent material, age/content declarations and store assets require real project ownership and release configuration.

Separate facts in every handover: implemented source, passed current tests, produced review package, device-tested behavior, known limitations and remaining content. Do not replace this with a completion percentage. A static successful route, a mocked ad callback, a compiled Police script or a green isolated fixture cannot establish the full shipped player experience.

## 20. Immediate execution order

1. Rework title/mode entry and default world controls; make Home physically obvious.
2. Add establishment/development and staged guidance using existing movement, economy and saved data.
3. Validate the first-minute input sequence, construction, useful income and split duties in real Unity play.
4. Implement playable Police as a separate officer/dispatch/arrest loop; connect its entry and save flow.
5. Integrate the official ads provider behind the requested settings asset; use real test units on devices and explicit editor simulation.
6. Exercise simultaneous matchday assignments, Home/Away continuity and production-speed travel windows.
7. Build the review package, capture the new experience and update the main implementation status from fresh evidence.
8. Play and balance every chapter, run device soak/release checks and obtain client acceptance before using a production-ready label.

This expanded design follows the latest request while retaining useful backend work. It replaces the old expectation that a cleaner HUD alone would satisfy the intended game.
