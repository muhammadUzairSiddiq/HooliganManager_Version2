# Production gameplay pass: implemented flow and evidence boundary

Source review: 03 November 2026 local time (test reports retain their UTC timestamps). This document describes the new source behavior across the existing Unity city, Home/Away campaign, Police career and optional monetization. It is an implementation snapshot for review and further agent work, not a production-release certificate. Costs, timings and rewards below are current development defaults, not historical client approvals or verified original-game values.

Read `Production_Gap_Comparison.md` for the requested comparison-first audit. Its findings describe the earlier source at that audit: broad road/building hiding, one-level completed-only Home construction and missing commerce/player transport. The new source described here supersedes those particular source observations. Preserve the historical Police **46/3**, initial strategy **43/4** and later strategy escort **48/3** failures; the evidence section separates them from the completed corrected reports, including current **Strategy52/0** and **Purchases40/0**. Native repeats and the isolated Android review build/package checks are complete. A prior 50-pass strategy run did not establish repeatability when its later repetition exposed the escort failure. Read `Implementation_Status.md` and the actual reports for final integrated results.

## 1. The connected player experience

The player now enters through a role choice rather than a dashboard containing every management system. A fresh firm begins in the same existing city, with a named Home territory, direct crew control, one next purpose and a short coach. The first investments visibly establish the headquarters, supply yard and training ground. The firm then scouts the district, establishes shop income, protects it with separate groups, prepares the stadium operation and deliberately starts matchday. Matchday competes for those same limited people: stadium coverage, pub security, supporter escort and a staffed trade stall. Home defense and territory lead into sequential Away operations; returning retains the campaign and base.

Police is a separate career. Officers can travel together in a player-controlled patrol van, disembark and perform on-foot patrol, assessment, detention and booking while another officer continues a different duty. Changing roles restores the firm's separate save. Ads and purchases are optional support, with explicit test configuration and failure paths that leave the core game available.

This pass changes presentation and connects strategic consequences over the existing backend and visual assets. It does not establish every original Storm Over Europe mission, unit role, animation, multiplayer feature or content set.

## 2. Entry, HUD and mobile orders

### Entry and information hierarchy

`MainMenuController` and `SimpleShellLayout` present **PLAY · CHOOSE ROLE**, **CONTINUE**, settings and secondary credits/exit. The role sheet offers a new firm, the existing firm campaign and **POLICE · PATROL & PROTECT**. Continue is visible but disabled without a firm save. Starting a new firm over an existing campaign requires the existing overwrite confirmation; choosing Police loads its own career instead of replacing the firm.

`ModernCityHud` owns the default firm city view. It hides the older permanent command rail, portrait rail, competing resource/status rows and management drawer. Its default information is Home/Away identity, cash/supplies/heat, one current purpose, **HOME** or **ARRIVAL**, **MANAGE**, **CREW**, **MAP DETAILS**, pause and the compact selection coach. During active matchday, its purpose card shows matchday direction and a **COMMAND** action. Advanced sheets are deliberate destinations, and the shared sheet owner replaces an open sheet rather than stacking unrelated screens.

**MANAGE** groups Home, businesses, operations, matchday, journal, Away/extraction, camera, help and optional support. The business/operations/matchday rows appear after HQ exists in Home; Away exposes field command. This is presentation staging, not a purchase or arbitrary progression gate on the core RTS APIs.

Vertical lists created by `LandscapeUI.Scroll` have a visible track and draggable scroll handle when content overflows, instead of relying on an invisible swipe area. The matchday summary explicitly says **SWIPE THIS LIST** to reveal approaches, supporter escort and the buildable stall. That hint is specific to the matchday list; do not assume every sheet contains identical hint text.

### The simple default and the complete order sheet

| Player action | Implemented behavior and purpose |
| --- | --- |
| Tap a crew member or its CREW entry | Toggle only that actor in the selection. Other selected members remain selected. |
| Tap a street | Move the selected available people. Selection persists, so another street tap reorders the same group. |
| Drag empty ground / pinch | Free camera pan / zoom; normal events do not require a close-up fight view. |
| CREW | See health and current duty, toggle individuals, select all, enter box selection or save/recall one of three session groups. Group recall changes selection, not existing jobs. These shortcut groups are not a new serialized squad system. |
| ORDERS → MOVE | Return to ordinary street targeting. |
| ORDERS → ATTACK | Choose the rival for this selected group. The coach identifies the armed target mode. |
| ORDERS → GUARD | Tap a post or draw an area to protect. The selected group defends locally and returns to its duty. |
| ORDERS → PATROL | Choose two route points and confirm. The group reacts locally and resumes its route. |
| ORDERS → REGROUP | Send this group to HQ; unrelated groups keep their assignments. |
| ORDERS → SCOUT | Tap a street; the existing scout action moves actual crew and earns local intel on arrival. This is separate from completing the named campaign scouting operation. |
| ORDERS → CAPTURE | Show the nearest eligible territory's local confirmation with occupation tax and heat, then explicitly send the selected group to clear and hold it. Passing through does not authorize capture. |
| ORDERS → STOP | Cancel the selected available group's current job with a local stop command. Accepted count is reported; other groups retain their jobs. |
| Cancel targeting / close | Cancel the previous gesture and return the default target mode to Move without clearing selection. |

The order sheet explains target gestures instead of keeping Move/Attack/Retreat as unexplained permanent peers. Its buttons cancel the previous gesture before arming another action. Opening a sheet consumes UI input; it must not leak taps into world movement. Accepted counts and blocked/busy/unreachable feedback remain part of the backend contract. Actual phone targeting accuracy and touch usability still need device observation.

### Visibility and identification

`RtsBuildingVisibility` now registers qualified building mesh names. It does not automatically hide a city mesh merely because a unit is behind it. An explicit eligible building touch temporarily switches off that renderer, leaves its collider unchanged, and restores the previous rendering state on a second touch, after **12 unscaled seconds**, or when the component disables. Only one building renderer is revealed at once. A storefront interaction has its own commerce priority; roads, bridges, platforms, parking, ground and terrain are excluded from the building rule. This component no longer disables road/rail renderers or colliders at startup.

`ZoneVolumeFactory` now creates a thin square site footprint rather than the old filled circular volume. `SiteOutlineVisibility` hides ordinary site footprints unless **MAP DETAILS** is enabled or that site is deliberately emphasized. `FocusedWorldPresentation` also respects that layer for older GangArea rings. Ordinary world labels obey the map-detail flag and a limited non-overlapping screen budget; command-priority labels remain available for the active task. Home retains its own named rectangular boundary and small plot posts. Selected-unit identity, current capture progress and useful command feedback remain; “fewer circles” does not mean every tactical indicator has been removed.

## 3. The seven-step coach and permanent Home

`FirstSessionGuide` reads saved real learning flags and Home results. Opening a menu does not complete a lesson. Its button outline points to the named actual UI control, and its primary action opens the relevant sheet or focuses Home. It is available only to guided Home firm campaigns, not Police or Away, and can be hidden/shown through Help without erasing learning.

During a guided campaign, the CREW sheet closes when an individual toggle first teaches selection or split selection. This immediately returns the player to the street/next action. Ordinary later selection toggles refresh the sheet, preserving its use for assembling a group. Merely opening the sheet still does not advance learning.

| Lesson | What the player sees | What advances it |
| --- | --- | --- |
| 1. Select | OPEN CREW; select one named member. | Deliberate selection recorded by the input/progression system. |
| 2. Move twice | Tap a nearby street, then another; selection stays. | Two accepted movement orders. This lesson does not independently require arrival at both destinations. |
| 3. Make a group | Add a second member through CREW. | Recorded multi-selection. |
| 4. Split the group | Deselect one member while keeping others. | Recorded split selection. |
| 5. Establish Home | HOME → BUILD HEADQUARTERS; crew walks to its plot. | Actual headquarters completion. |
| 6. Invest in production | Build the £300 supply yard; supplies support later decisions. | Actual supply-yard completion. |
| 7. Develop the crew | Build the £450 training ground. | Actual training-ground completion, then normal campaign guidance resumes. |

While construction is active the coach displays the real work status and explains arrival, remaining on site and cancellation by another order. The seven coach lessons are distinct from the **eleven** Home campaign goals: orientation, HQ, district scouting, viable crew, business control, first income, separate Guard/Patrol duties, preparation, matchday, territory and Home defense. A completed coach is not campaign completion.

### Build, invest and see the result

`HomeTerritorySystem` creates three reachable interactive plots inside **HOME TERRITORY**. The player selects crew, opens Home and chooses a build/upgrade. Accepted crew walks to the real plot; work advances only when all assigned actors are alive, available, on site within the work radius and idle. Nearby hostiles contest construction. Giving assigned people another order cancels the job without charging. Other groups remain under their own orders. Only one Home construction assignment runs at once.

| Facility | First level: cost/work | Gameplay reason to invest | Upgrade defaults |
| --- | --- | --- | --- |
| Firm headquarters | £0 / 12 seconds | Establishes the permanent base and individual recruitment/care access. | Level 2: £750 + 2 supplies / 40s. Level 3: £1,125 + 4 supplies / 50s. Recruitment price becomes £300 / £275 / £250 at HQ levels 1 / 2 / 3. |
| Supply yard | £300 / 18 seconds | Sets the existing supply-network level. Active play produces 2 / 4 / 6 supplies per 120 seconds at levels 1 / 2 / 3, up to the supply cap. | Level 2: £600 + 2 supplies / 40s. Level 3: £900 + 4 supplies / 50s. |
| Training ground | £450 / 20 seconds | Each newly completed level adds +5 max HP and +1 strength once to the then-current recruited roster; live actor stats refresh. Reopening/reloading does not repeat the grant. | Level 2: £800 + 2 supplies / 40s. Level 3: £1,200 + 4 supplies / 50s. |

HQ is required before the other initial facilities. All three first-level facilities must exist before any can be upgraded. Maximum level is three. Recruitment needs a selected living member within Home, enough cash and room in the active crew; it creates one usable member and charges only on success. A new recruit receives its own roster stats: the implementation does not promise retroactive training bonuses to every future recruit.

Construction uses the existing facility prefabs. A work model grows vertically as progress advances; completion restores the facility model, and additional levels add visible development models and a level caption. These reused props intentionally avoid adding road blockers; they are not a bespoke new building-art set.

### Corrected construction save contract

The old “unfinished construction restarts after reload” statement is **obsolete for this pass**. `HomeBuildingState` now saves level, target level, construction flag, elapsed work seconds and assigned worker IDs. Work saves periodically during construction and through normal save calls. On a Home reload, the system attempts to reassign the saved living workers and resumes the saved work toward the target level. Cash and supplies are checked again and charged once at completion, not at assignment or resume. Completed levels and the training-applied level persist.

If saved workers cannot be reassigned, the job clears its saved unfinished assignment and explains that the player must select available crew and restart; no construction payment was taken. A deliberate manual cancellation also clears unfinished work. This is not an offline construction timer: leaving the app does not manufacture elapsed work, and progress after the most recent actual save is not guaranteed to survive an abrupt process kill.

The first native strategy run saved the unfinished state correctly but failed the real scene-resume assertion and its dependent level/price/visual assertions. Source now waits for `BattleActive` and the spawned actor list before attempting reassignment, instead of using the earlier fixed startup delay. The current 52-pass strategy report verifies saved-work/resume, one-time payment and visible upgraded production in its isolated native fixture. A natural full campaign and abrupt-process/device save behavior remain separate acceptance work.

## 4. Shops, local resource trade and Away scope

The Home district has six named existing storefronts: burger takeaway, corner bakery, matchday pizza, supporters pub, street cafe and convenience shop. The locator now searches nearby matching existing buildings before the broader fallback. The bakery remains a reused existing model. Selecting a shop opens one venue card with route/time estimate, travel, control/guard, loot, buy, sell and invest. Seeing a venue or opening its card never grants cash, control or goods.

| Choice | Actual cost, requirement and result | Strategic stake |
| --- | --- | --- |
| Control | Selected crew travels and holds the venue uncontested for its 12–20s Home control duration. | Opens recurring income and local trade; a new order/death/unavailability cancels the assignment. |
| Defend | Leave living crew or give Guard/Patrol a useful local assignment. Unopposed rival pressure contests trade and can remove control after 25s. | Protection consumes people who could build, cover matchday or travel. |
| Income | Home base yield £90–£150 per 90s of eligible active simulation; pizza/pub base yield doubles during Home matchday. | Invested, trading venues support a durable economy. Disrupted/contested time does not earn backdated income. |
| Buy supplies | A selected available member must be within 8m; venue controlled and trading; no local hostile pressure. Pay £120 for one supply, capacity 99. | Converts earned cash into construction, upgrades or matchday support. |
| Sell supplies | Same physical/trading conditions. Sell one owned supply for £70. | Recovers cash at a deliberate loss; buying then selling cannot generate profit. This is a shared-supply resource trade, not a complete multi-item inventory market. |
| Improve venue | Level 1 improvement: £350 + 2 supplies / 20s crew work. Level 2 improvement: £650 + 3 supplies / 30s. Payment on successful completion; max two improvements. | Each level adds 35% to that venue's income and visible trade-space props. Work competes for crew time and funds. |
| Loot | Timed on-site raid (18s), declared venue reward/heat, 300s cooldown and 180s trade disruption. Home rewards range £240–£420 and heat +2/+3. | Cash now competes with police risk and lost protected trade; no instantaneous repeat cash button. |

Ownership, venue investment, disruption, loot cooldown and accrued income are serialized. Opening/reloading a sheet cannot repeat a completed transaction. Completed business investment persists; the live in-progress business assignment is not the Home construction resume system and is not claimed to resume mid-job across scenes.

Territory control is a separate deliberate investment from venue control. CAPTURE shows the territory's actual tax/heat (class defaults £300 and heat +2) in a local confirmation; `BeginCapture` records the accepted crew and their order revisions. Unrequested crew presence does not progress a capture or take money. New orders/unavailability cancel an explicit capture. Completion charges the shown occupation tax, records the territory/strategic benefit and uses local feedback in the Gameplay city rather than a global territory-win popup. This matters when staffing a nearby matchday task: standing there must not secretly authorize another purchase. The first strategy run's stall exact-cost assertion failed while the built stall and physical activity continued; the source correction removes incidental automatic territory capture. The current 52-pass strategy report verifies explicit assignment/cancellation, declared payment once and no global popup.

Protection income now uses the same Home mission scope for checking and recording its once-per-day action token even after a later Away destination is unlocked. Returning to Home at campaign level three cannot collect the same Home protection payment twice by changing the token's mission number. The current 52-pass strategy report includes a passing assertion for this case.

Away districts create their own **three** burger/pizza/convenience venue records, identified by mission scope such as `M2:burger`. Those do not share ownership, cooldown or improvement with Home or another Away district. Away base income is 80% of its Home archetype; raid reward increases by £25 per mission number above Home; control work adds 2s. During active Away simulation, Home plus the active Away district accrue income once; inactive Away districts do not gain simulated time or cooldown progress. This is active simulation, not wall-clock/offline revenue.

### Campaign decisions and return

The existing campaign has Home plus Riverside, North End, East Docks and Old Town. Home completion remains permanent. Riverside/North End require three living selected travellers; later trips require four. The player chooses actual travellers and Home reserves. An intentional empty selection remains empty, and Continue restores the active deployment IDs rather than silently replacing them with reserves.

Away uses the existing six connected Riverside goals and seven later-district goals: scout/prepare, address the local firms, secure territory and physically extract. Chapter readiness alone does not award success. All surviving deployed members must regroup at ARRIVAL and hold a clear extraction for eight seconds. Rewards/progression occur once; injuries, roster, Home state and scoped commerce return to the same firm save. At heat 3+ with no Home reserves, active Away play warns after 120s and can disrupt one Home venue after 180s. Leaving a defender therefore has an explicit economic benefit. Full natural chapter pacing and scenario variety remain review work; a seeded fixture is not an end-to-end campaign playthrough.

## 5. Matchday is an allocation problem

`StadiumMatchdayActivity`, `MatchdayTactics` and `MatchdayStrategySystem` now connect preparation, deliberate start, two physical coverage sites and optional crew-dependent activities. No calendar wait or board opening automatically starts or completes the operation.

Before **START MATCHDAY**, the player learns direct/split control, prepares transport and stadium access through Operations, and retains at least two living roster members. They may inspect the three approaches, pre-position separate groups at stadium/pub and assign optional work before the clock starts. Starting checks the selected approach's funds/supplies and the city routes; it commits the plan's cost once. A committed approach cannot be changed to obtain another plan's benefit without its cost.

| Approach | Upfront cost | Coverage and outcome tradeoff |
| --- | --- | --- |
| Balanced Coverage | £0, no supplies | One available crew at each location; 20s coverage; £100 per covered site. Keeps cash/supplies available and leaves crew for other work. |
| Protect the Routes | £200 + 2 supplies | 15s coverage per site; missing coverage still costs morale but adds no heat. Spend limited resources to reduce exposure and police consequences. |
| Matchday Trade | £150 + 2 supplies | Pub requires **two** crew and pays £225 when covered. Stadium still needs one; staffed stall pays more. Higher return competes with escort, defense and spare response capacity. |

Coverage is not purchased remotely. Each site checks the actual nearby available living crew within 11m and absence of local hostile pressure. Stadium has a 270s opportunity; pub has 420s, measured from explicit start. The board shows actual crew count, required count, progress, remaining opportunity and reward. **SEND SELECTED** assigns only the current group to guard that site and preserves another group's duty. The player can pre-position before start; coverage progress/rewards wait for the live operation.

An active unfinished response can be equipped once for **2 supplies**, cutting its required hold by 5s, to a minimum of 10s. This helps crew finish sooner; supplies alone never count as coverage. Each covered site pays its chosen reward and morale +2 once. A missed site costs morale 5, normally heat +1, and the missed pub disrupts trade for 90s. Penalty IDs persist, so replay/Continue cannot repeat the charge. Aftermath offers a deliberate retry of missed coverage, preserving already-paid coverage and one-time penalties.

### Optional work competes with the two required sites

**Supporter escort:** select an available crew member and assign them to the **inner stadium arrival corridor**. This controlled pickup is no longer the distant station. `FindEscortMeetingPoint` follows the actual stadium-to-station navigation path outward by a target 70m, then requires a complete return route to the stadium between 20m and 150m using the crew prefab's actual NavMesh agent type/area mask. If that candidate fails, it searches validated points on a 45m radius around the stadium with the same complete-return-path and distance limits. If no valid corridor exists, it logs an error, exposes zero route distance and refuses the escort assignment. Ambient supporters still make their existing station journeys; this change only places the optional directly guided task within a starter walker's matchday budget.

The card exposes `EscortRouteMetres` and a walking-time estimate at approximately **1.3m/s** (starter `Data.Speed` 2.5 × walking gait .52). A typical 70m route is about 54s before the final arrival hold, whereas the old 700–900m station route could exceed the event at walking speed. The current native strategy suite completed **52 passed, 0 failed**, including the route-distance/time budget and a real physical escort with a starter-speed guide. The wider fixture still accelerates simulation and temporarily freezes matchday allocation time; that contract check is not a measured phone timing result or a complete naturally timed matchday playthrough.

Meeting the pickup spawns an actual supporter follower/group representative. The guide must travel to the stadium and the follower must arrive as well, then hold clear for five seconds. Completion pays £175, reputation +2 and morale +3 once. Escort position, assigned guide and completion are saved. If a guide is lost, the player can assign a replacement to the waiting group's location. It is a physical escort, not an immediate button payout.

The shared follower implementation is now `EnemyController.TryBeginEscort`, also used by the on-foot Police suspect/bribe routes. It samples with that follower's actual NavMesh agent type and allowed areas, requires a complete path, and searches nearby connected endpoints within a maximum six metres of the requested destination when the initial endpoint is invalid. A rejected new target reports `LastEscortFailure` and preserves the prior route instead of replacing it with a partial path. A successfully assigned active escort bypasses camera-distance streaming pauses so moving the camera cannot freeze the mission follower at a junction. `EndEscort`, cinematic idle locking, released hold-fire/changed crew assignment and pooled actor initialization clear the active escort state. Matchday actor spawning likewise samples the prefab's actual navigation filter and verifies a Warp after initialization. The current 52-pass strategy run completes the corrected follower/corridor sequence; its earlier junction failure remains preserved as dated evidence.

**Build and staff a matchday stall:** assign a real builder, keep £250 + 3 supplies available and do 20s uncontested on-site work. Those resources are checked again and charged at completion; they are not reserved in a separate wallet at assignment. Completion charges once and creates a visible reused stall model for this matchday. A built empty stall earns nothing. While matchday is live, at least one available living nearby crew member and no local hostile pressure advances the saved 45s income clock; payout is £60, or £90 with Matchday Trade. Sending that person elsewhere pauses the income clock. Building/staffing the stall competes with stadium, the two-person trade-plan pub, escort and venue defense. Saved stall work/builder data can resume using the existing reassignment logic; a new order cancels live building work.

The operation runs through arrivals, build-up, rival pressure, kickoff and dispersal into aftermath. Both required responses and aftermath are necessary before campaign matchday credit. Optional escort/stall provide resources and decisions, not compulsory extra chapter gates. Next day is a deliberate action after review; staying in aftermath cannot repeat the operation reward. The free camera and unselected groups remain active. These mechanics provide strategic choices in source; natural production-speed response balance, crowd readability and sustained pressure still require a human playthrough.

## 6. Police: travel together, work on foot, preserve a separate career

`PoliceCampaignStore` initializes three controllable officers, a £2,500 career budget, trust 70 and shift-one calls. Officer funds, patrol checks, call/suspect state, positions, Guard/Patrol geometry and vehicle data belong to the police store. The firm save is restored on exit. This is the owner's requested playable Police adaptation; it is not attributed to an original Storm Over Europe police career.

### Patrol van user flow

1. Select one or several available officers, then **BOARD SELECTED**. They walk to a reachable curb beside the parked van. Selecting/focusing an **empty** van keeps that officer selection, so inspecting the vehicle does not erase the intended boarding team. Selecting an occupied van clears on-foot selection for driving. Capacity is six, while the initial career has three officers. Boarding is cancelled for an officer who receives a newer order or becomes unavailable. The van cannot drive away during pending boarding.
2. Arrived officers mount, disappear inside the existing van model and stop taking normal on-foot orders. The vehicle becomes the selected controllable object when boarding finishes and no officer remains selected. There is no instant remote pickup.
3. Tap the van or **PATROL VAN**, then tap a road. `PolicePatrolVehicle` samples the road NavMesh, requires a complete road path and follows its corners at default speed 11. Another road tap redirects it. Failed routing preserves the previous valid drive order and reports the reason. At least one living passenger is required.
4. Choose **EXIT OFFICERS**. The van stops and searches nearby valid walking NavMesh curb positions. Successfully exited officers become visible/controllable and selected. A passenger without a safe exit remains aboard; the status tells the player to choose another road. **STOP / CANCEL BOARDING** provides explicit interruption.
5. Use on-foot officers to perform actual patrol/assessment/detention. Merely parking a full van on a marker cannot satisfy those duties. Dispatch and patrol requests can route the selected occupied van toward the target, then explain that the officers must exit and receive the on-foot assignment.

Vehicle position, heading, passenger IDs, driving flag and destination are included in the police capture. Continue recreates the vehicle, reseats living saved passengers and attempts the saved road journey. Pending walkers are not claimed as saved passengers. The van is a transport system, not a traffic-crash, suspect-passenger or vehicle-combat simulation; detained suspects still follow their escort **on foot**.

### Shift purposes and consequences

| Duty | Actual action and result | Why divide officers |
| --- | --- | --- |
| Three patrol checks | Exit and hold each distinct connected patrol point on foot for 6s. | One officer can maintain a route while another handles a call; van occupants do not tick the check. |
| Shoplifting report | Send selected on-foot officers; remain on site for 8s to assess and identify the suspect. | An unassessed report is not an automatic arrest target. |
| Identified-suspect detention | Select a living officer within 6m and detain for 5s; new order or separation interrupts it. | Committing an officer to custody prevents using the same person as a remote responder. |
| Escort and booking | The assigned officer and physical follower return to station; both arrive and hold booking for 4s. If the escort officer is lost, bring another living selected officer near the existing detention and explicitly take over. | Other patrols continue; a handover preserves the original arrest instead of creating a second arrest/reward. |
| Station-route call | Physically assess/hold the arrival route for 15s on foot. | Creates a second concurrent responsibility alongside the suspect workflow. |
| Delayed response | Initial opportunity 600s; overdue unresolved pre-detention calls cost trust 5 and remain available, then use a 300s further window. | An explained local consequence replaces a surprise global failure. |
| Complete shift | All three checks and both resolved calls are needed. Completion adds £750 and trust +5 once; resolved calls also add their declared trust. | No board-only completion. Next shift is deliberate and safely resets live duty state while preserving the career. |
| Lost team | Explicit recovery heals the roster with a trust cost; basic recovery is free of ads/purchases. | A recoverable shift avoids stranding the player or destroying the firm save. |

The Police HUD presents one current objective, officer selection, a dispatch sheet and transport controls. Resolved calls remove active markers; handover guidance points at the waiting suspect and then the station. The dated guard-fixture failures are historical; the subsequent Police49 native run passed its revised sequence. Applicable final-source Police regression remains distinct from that history.

## 7. Ads and purchases: actual integration and test configuration

### Ads

Google Mobile Ads **11.6.0** is pinned. `Assets/Resources/Ads/AdsConfiguration.asset` remains the owner's single ScriptableObject for **Disabled / Test / Live**, per-platform app/rewarded/interstitial IDs, cooldowns, reward amount and toggles. Current review defaults use Google's public sample IDs in Test mode, a labelled editor simulator, rewarded enabled and forced interstitials disabled. Disabled makes no SDK requests. Live rejects sample IDs and needs actual owner AdMob IDs. Device SDK initialization waits for UMP's ad-request eligibility; required privacy options are exposed.

The optional supply-support placement grants the configured **cash amount (default £200)** after reward completion, once per matchday with a default 180s session cooldown. The label does not mean the callback directly adds `CitySupplies`; cash can be spent on trade or preparation. A callback captures the initiating firm data/day and ignores a changed campaign or already-claimed reward. Failed, unready or skipped ads do not pay. Forced ads are checked only at explicit supported transitions and the no-interruptions entitlement suppresses them; they are not inserted into live RTS actions. Network test-ad display and consent behavior on an actual device are unverified here.

### IAP

Unity Purchasing **5.4.4** is pinned. `Assets/Resources/Purchases/PurchasesConfiguration.asset` uses **Disabled / EditorTest / Store** modes and per-platform product IDs. The review asset is **EditorTest**, with native product IDs intentionally blank. EditorTest is a labelled local no-charge simulator and is disabled in a device build; it cannot silently invoke real billing. Store mode requires unique configured product IDs and a mobile store build. Store prices come from platform metadata rather than a made-up local price.

| Product | Implementation | Important limit |
| --- | --- | --- |
| Optional firm support | Consumable, default +£1,000. A saved purchase intent binds it to its initiating firm wallet ID. | Does not credit a new/different campaign or Police. Restoring a consumed currency product cannot grant it again. |
| No forced ad interruptions | Non-consumable entitlement; blocks future eligible forced transitions. Restorable across firms through the local receipt ledger. | An unowned offer is hidden and requests are rejected unless AdsConfiguration has interstitials enabled and ads are not Disabled. The review has no forced interstitials, so it does not sell removal of absent interruptions. An already-owned entitlement can still display as owned; rewarded ads remain optional. |

The store adapter connects/fetches products, handles pending/failed/deferred orders, applies a wallet transaction token with its benefit, saves, then confirms delivery. Duplicate transaction callbacks and restores cannot duplicate currency. Pending intents wait for the original firm rather than leaking into Police or a new campaign. A separate local receipt ledger stores delivered IDs and the entitlement; isolated test paths avoid real player data. These are local/client fulfillment protections, **not server receipt validation or refund/revocation handling**. Google/Apple sandbox checkout, restore after reinstall/account changes, interruptions, refunds and device tests remain release gates.

For same-session recovery, creating the StoreController now disables automatic fetched-pending processing with `ProcessPendingOrdersOnPurchasesFetched(false)`. `RestoreFetched` explicitly retries `Orders.PendingOrders` before processing confirmed non-consumable restores. A purchase refused while Police or another firm is active therefore does not depend on the SDK emitting an already-seen pending event again: a later purchase fetch can retry it against the initiating wallet intent. Only successful delivery and save permit confirmation; a rejected wrong-mode/wallet order stays unconfirmed. Six actual SDK-Orders/real-service local regression checks now pass within the **40-pass** purchases report, including same-session retry, save-before-acknowledgement and replay after reload. These checks do not establish network checkout or device validation.

Core selection, orders, construction access, recovery, Police duties and campaign progression never require an ad or payment. This pass implements local optional monetization wiring, not live product approval or a verified store economy.

## 8. Reference alignment and deliberate mobile adaptation

Firsthand reference reporting establishes the original's directly commanded core, contextual urban venues and resource spending, different troop capabilities, police pressure and scenario objectives beyond simple fights. Our relevant alignment is a directly controlled group in a city, sending people to a place to perform a task, earning/spending resources, choosing how to divide force and preserving a tactical view. The current targeted mobile approach replaces desktop selection/context-menu gestures with tap selection, street move and explained contextual sheets. [GameSpot hands-on preview](https://www.gamespot.com/articles/hooligans-storm-over-europe-preview/1100-2871935/)

The original review describes controllable purchased recruits versus ambient followers, venue ownership, cash/recruitment tradeoffs and route/protection/rescue objectives. These support the focus on real crew, purposeful places and varied objectives. Our mission-scoped trade, short action coach, permanent three-level Home construction, configurable ads/IAP and playable Police transport career are **owner-authorized adaptations**, not verified reproductions of original mechanics. Complete original manual/video analysis and full mission/role equivalence are not claimed. [Worthplaying firsthand review](https://worthplaying.com/article/2002/2/5/reviews/2202-pc-review-hooligans-storm-over-europe/)

## 9. Evidence, reproduction and remaining production gates

An earlier repetition exposed an intermittent follower navigation defect:
`production-strategy-escort-failure.txt` preserves 48 passes and 3 dependent
escort failures. The supporter stopped at a junction. Source now validates
escort routes with the actor's actual navigation type/areas, searches bounded
nearby connected endpoints, retains the previous route on rejection and keeps
active mission escorts independent of camera streaming. Spawn projection uses
the actual actor navigation type as well. A subsequent 51-pass run completed
that corrected sequence. Source then shortened the controlled escort to a
validated inner arrival corridor; its 52-check repetition has now passed.
The first 50-pass result did not establish repeatability by itself.

At this integration snapshot on 03 November local time, all named local/native repeats below had completed, including startup/save, ads and the expanded purchases regression. Runtime/editor compilation had zero errors and 33 runtime warnings; the rules suite passed 42 checks. The final isolated Android build succeeded with zero errors/38 build warnings, and all 11 independent APK checks passed. Counts are evidence for the named fixtures/package, not an aggregate pass score or full production acceptance. The main status document owns the integrated result table.

| Evidence inspected at authoring | Actual state | What it establishes |
| --- | --- | --- |
| Runtime/editor compilation | **Zero errors; 33 runtime warnings** | `Artifacts/Recovery/compile-runtime.txt` and `compile-editor.txt`; compilation is distinct from device/store acceptance. |
| `Artifacts/Recovery/production-strategy-playtest.txt` | **52 passed, 0 failed**, 2026-11-02T19:15:44.9779955Z | Home work/resume, coach/visibility, real matchday allocation/escort/staffing, deliberate capture, Home-income scope and the controlled 20–150m corridor with a starter-speed guide. Isolated accelerated fixture, not full campaign balance. |
| `Artifacts/Recovery/police-vehicle-playtest.txt` | **21 passed, 0 failed**, 2026-11-02T19:17:03.6922746Z | Actual road travel, physical boarding/exit, Continue and preserving the officer selection when focusing an empty van. No device touch certification. |
| `Artifacts/Recovery/police-playtest.txt` | **49 passed, 0 failed**, 2026-11-02T19:20:11.3345869Z | Full career, Guard/Patrol Continue, custody/recovery, original firm restored byte-identically. Supersedes the dated guard-fixture result; not a device check. |
| `Artifacts/Recovery/commerce-playtest.txt` | **30 passed, 0 failed** | Real venue interaction/investment and Riverside return/revisit, including visible saved expansion. Accelerated isolated fixture, not natural economy balance. |
| `Artifacts/Recovery/redesign-playtest.txt` | **33 passed, 0 failed** | Final one-line Home identity/explicit Home camera framing, contextual Orders, construction/recruitment and native saves; accelerated isolated fixture. |
| `Artifacts/CityQA/rts-controls-playtest.txt` | **102 passed, 0 failed**, 2026-11-02T19:35:46.3921066Z | Current direct RTS/city/autonomy/visibility regression. Replaces older RTS101 evidence; does not certify phone gestures/performance. |
| `Artifacts/Milestone01/playtest-checks.txt` | **16 PASS entries, 0 FAIL entries; no RESULT summary line** | Actual menu/New Game/starter behavior and 1280×720, 2400×1080, 1024×768 editor layout checks. Counted from the report, not an invented final-summary line. |
| `Artifacts/Recovery/rules-checks.txt` | **42 passed, 0 failed**, fresh completed run | Standalone campaign/economy rules against the compiled assembly; no scene or Unity serializer. |
| `Artifacts/Recovery/unity-save-checks.txt` | **12 PASS entries, 0 FAIL entries**, completed final repeat | Disposable native startup/save checks. No physical-device process interruption or durability certification. |
| `Artifacts/Recovery/ads-checks.txt` | **14 PASS entries, 0 FAIL entries**, completed final repeat | Configuration and callback rules; no network ad impressions or device consent validation. |
| `Artifacts/Recovery/purchases-checks.txt` | **40 passed, 0 failed**, completed expanded repeat | Previous 34 rules plus six actual SDK-Orders/real-service recovery checks; local/native serialization and scoped commerce rules, no live checkout/device validation. |
| Android review package | **Succeeded, 0 errors, 38 warnings** | Isolated Unity batch build; duration 00:26:02.4467133; completed UTC 2026-11-02T20:42:51.5630737Z. Review packaging, not device/store certification. |
| Original/isolated build input identity | **252 source/configuration SHA256 hashes agree before and after build** | `Artifacts/Recovery/isolated-build-source-check.json`; the copied build inputs match the reviewed original. |
| APK integrity/native SDK/signature/freshness | **11 passed, 0 failed** | `Artifacts/Recovery/apk-package-checks.txt` and `review-delivery-manifest.json`; includes ARM64, Mobile Ads/UMP/Billing DEX entries, Test App ID and review-signature verification. No device execution/network checkout. |

The delivered review artifact is `Artifacts/Builds/Hooligan-RTS-Recovery-Review.apk`, **254,801,528 bytes**, SHA256 **`5e306ee9d558cfbe057c37cf0930728d57a4467087fdaceaba017cca648fce3a`**. These are the actual APK file size/hash from the delivery manifest, not the Unity report's total output-byte value. The GUI attempt begun at 00:40:25 remains stalled in its old resolver call; the plugin's exposed preprocessor flag is now 0 and isolated batch packaging worked. No original-editor restart or GUI recovery was performed. That distinction does not invalidate the hash-matched batch artifact and does not certify a future GUI build.

Historical failures remain separate from those current results:

| Preserved historical evidence | Observed failure | Superseding evidence |
| --- | --- | --- |
| Dated Police baseline audit | **46 passed, 3 failed**, 2026-11-02T17:49:27 UTC; rejected guard assignment and dependent geometry/save assertions. | Current Police49 passes the corrected sequence; baseline comparison stays a dated snapshot. |
| `Artifacts/Recovery/production-strategy-first-failures.txt` | **43 passed, 4 failed**, 2026-11-02T18:31:27.6015962Z; Home resume/dependent upgrade assertions and combined stall exact-cost/model assertion. | Current Strategy52 passes actor-ready resume, declared costs/visible upgrade and deliberate capture without incidental tax. |
| `Artifacts/Recovery/production-strategy-escort-failure.txt` | **48 passed, 3 failed**, 2026-11-02T18:55:56.2085601Z; physical escort arrival, dependent reward and saved completion. | Current Strategy52 passes corrected follower/corridor travel, reward once and saved result. The earlier50 and pre-corridor51 runs remain intermediate history. |

The native strategy fixture deliberately uses five durable fast crew and accelerated time, suppresses unrelated rival/police pressure, seeds later preparation, freezes the matchday clock after explicit start to isolate allocations and then resumes the real aftermath clock. Police vehicle and commerce fixtures similarly isolate their contracts. These are meaningful native system checks but do not prove production-speed natural balance, a complete campaign, performance, human understanding, or client acceptance.

Run Unity checks from edit mode or the existing command-line flow with the project closed in other Unity instances. Use disposable `SaveDataInLocal.EditorSaveOverride` paths and allow each native fixture's batch cleanup to finish. Do not use `-quit` on asynchronous playtests; their batch callback exits after restoring the test override. Relevant entry points and reports are:

| Native entry point | Main report / purpose |
| --- | --- |
| `ProductionStrategyPlaytest.BeginBatch` | `Artifacts/Recovery/production-strategy-playtest.txt`; current Home levels/mid-work save/resume, tutorial, visibility, explicit tactical matchday and physical optional work. |
| `RedesignPlaytest.BeginBatch` | `Artifacts/Recovery/redesign-playtest.txt`; Home/contextual Orders/recruitment compatibility. |
| `PoliceCityPlaytest.BeginBatch` | `Artifacts/Recovery/police-playtest.txt`; full on-foot career and isolated Guard/Patrol Continue/shift/recovery. |
| `PoliceVehiclePlaytest.BeginBatch` | `Artifacts/Recovery/police-vehicle-playtest.txt`; real road route, boarding, driving, safe exit and Continue. |
| `CommercePlaytest.BeginBatch` | `Artifacts/Recovery/commerce-playtest.txt`; actual world venue crew/trade/investment interactions. |
| `PurchasesChecks.Run` | `Artifacts/Recovery/purchases-checks.txt`; configuration, wallet-bound fulfillment/restore safety, native serialization and scoped business rules. |
| `AdsChecks.Run` | `Artifacts/Recovery/ads-checks.txt`; ads SO and callback rules without network impressions. |
| `RecoveryChecks.Run` | `Artifacts/Recovery/unity-save-checks.txt`; native startup/save checks. |
| `RtsControlPlaytest.BeginBatch` | `Artifacts/CityQA/rts-controls-playtest.txt`; direct RTS/autonomy/camera/visibility regression. |
| `MilestoneOnePlaytest.BeginBatch` | `Artifacts/Milestone01/playtest-checks.txt`; actual title/new-game flow and editor aspect-ratio layouts. |

Before claiming a release candidate, naturally play the full Home→matchday→Away→return path at production speed, run a complete Police shift with mixed vehicle/on-foot allocation, and inspect phone-safe UI/targeting on actual target devices. The corridor's tested route budget does not replace a naturally timed whole-event playthrough with competing duties and live pressure. Before a store release, complete performance/long-session/save interruption testing, owner AdMob/store IDs and privacy configuration, live network test ads/consent, sandbox purchases/restore/refunds, release signing/provisioning and required client review. The APK's verified review signature is not store signing/provisioning acceptance. No device, store, frame-rate or commercial acceptance claim follows from this source document.

## 10. Source ownership for future agents

| Area | Source of implemented behavior |
| --- | --- |
| Menu / role entry | `Assets/Scripts/UI/MainMenuController.cs`, `Assets/Scripts/UI/Landscape/SimpleShellLayout.cs` |
| Firm HUD / coach | `Assets/Scripts/UI/Landscape/ModernCityHud.cs`, `FirstSessionGuide.cs` |
| Input / direct RTS | `Assets/Scripts/Battle/AgentSelectionManager.cs`, `RtsGestureController.cs`, `RtsOrderVisual.cs`, `AgentController.cs` |
| Home / production benefit | `Assets/Scripts/Battle/HomeTerritorySystem.cs`, `CityDevelopmentSystem.cs`; serialized fields in `PlayerData` |
| Venue economy / local actions | `Assets/Scripts/Battle/CityBusinessSystem.cs`, `CityCommerceSystem.cs` |
| Matchday | `Assets/Scripts/Battle/StadiumMatchdayActivity.cs`, `MatchdayTactics.cs`, `MatchdayStrategySystem.cs` |
| Visibility / map detail | `Assets/Scripts/Battle/RtsBuildingVisibility.cs`, `FocusedWorldPresentation.cs`, `ZoneVolumeFactory.cs`, `ZoneLabelUtil.cs`, `TerritoryControlPoint.cs` |
| Police career / transport | `Assets/Scripts/Battle/PoliceCityMode.cs`, `PolicePatrolVehicle.cs`, `Assets/Scripts/SaveDataInLocal/PoliceCampaignStore.cs` |
| Campaign / deployment | `Assets/Scripts/SaveDataInLocal/CampaignMissions.cs`, current GameManager/CityGameplay deployment and extraction flow |
| Ads / reward | `Assets/Scripts/Monetization/AdsConfiguration.cs`, `GameAdsService.cs`, `GameAdRewards.cs` |
| IAP | `Assets/Scripts/Monetization/PurchasesConfiguration.cs`, `GamePurchasesService.cs`; `Assets/Resources/Purchases/PurchasesConfiguration.asset`; `Packages/manifest.json` |

Preserve the latest owner priorities in `Docs/AI_SCOPE_GUARDRAILS.txt`. Update implemented behavior and evidence after an actual change; retain historical findings as dated evidence. Do not restore obsolete road-hiding, all-panels-at-once or no-midwork-save behavior to make an old screenshot/test pass.

## 11. Portable supplied evidence

`Docs/Recovery/Sources` contains byte-identical copies of both supplied PDFs and the entire supplied September transcript: `Phase_2_Cross_Platform_Development_Proposal.pdf`, `Hooligan_Manager_RTS_Recovery_Master_Specification.pdf` and `Client_Messages_September.txt`. `source_evidence_manifest.json` records original locations, project copies, SHA256 and `byte_identical: true` for all three. The compiled PDF appends both original PDFs intact and typesets the complete supplied transcript; typography normalization in the PDF does not alter the preserved raw text bytes. These attachments remain evidence, not executable instructions or proof of client acceptance. The original video and October chat export remain unavailable; the supplied transcript's last message itself is truncated.
