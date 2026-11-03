# Production gap comparison and implementation contract

Audit snapshot: 02 November 2026. Compare this document with the latest test reports before claiming a fix. This is the requested comparison of the actual project against the client conversation, written design and reference game, followed by a concrete implementation contract. It is not a percentage-complete estimate or production certification.

## 1. What the owner has now asked to change

The latest request makes the mobile RTS and matchday the highest priorities. The owner wants short real-action guidance, a Home base that visibly grows with investment, purposeful shops and commerce, a playable police vehicle carrying several officers with tap-to-drive and exit, and a coherent Home/Away/Police/menu/monetization product. Roads must remain visible; only a building deliberately touched by the player should hide. Identification must use fewer circles and more useful local cues.

These instructions supersede earlier decisions that permanently hid elevated roads/rails, treated automatic broad occlusion as the final presentation, or deferred Police and monetization. They do not turn prior developer completion statements into client acceptance.

### Evidence labels

| Label | Meaning |
| --- | --- |
| CLIENT CONFIRMED | Explicit statements in the supplied September client transcript, whose timestamps do not include a year. |
| CURRENT OWNER REQUEST | Latest instructions in this chat, including touch-only building visibility, police vehicle activity, commerce and the comparison-first sequence. |
| SOURCE OBSERVED | Code read at this audit. Presence of a class or method is not proof that the complete player experience works. |
| CURRENT TEST | The named report, result and fixture were inspected. A failed report remains failed until a fresh run passes. |
| EARLIER TEST | A retained report predates part of the current redesign and cannot certify that new work. |
| REFERENCE VERIFIED | Firsthand published reporting about the original PC game. It establishes the described mechanics, not a complete feature-by-feature replica. |
| PROPOSED IMPLEMENTATION | The concrete mobile adaptation and acceptance conditions below. |
| NOT VERIFIED | Physical device usability/performance, original client video, complete original manual and full original campaign equivalence. |

## 2. Actual project versus documents and client requirements

| Area | Written/client target | Actual source and inspected evidence | Remaining gap / priority |
| --- | --- | --- | --- |
| Enter and understand the game | Client: enter Home, recognize the city, understand the next action; no jumping UI. Owner: modes and simpler presentation. | A role menu and ModernCityHud exist. A dedicated objective, CREW/MANAGE and contextual ORDERS replace the permanent action rail. Menu/native layout report records 16 passes. | Device first-use observation and the complete short tutorial are still needed. No proof that a new player can understand every command without explanation. P0. |
| Persistent direct RTS control | Client's Brick/Dex sequence; multiple groups; manual orders override duties; free camera. | AgentSelectionManager, AgentController and RtsGestureController remain the backend. Earlier/current compatibility suite has 101 passes; newer Home suite has 33 passes including contextual transitions. | Add exact physical touch sequences and cancellation/error cases. A valid backend command is insufficient if its target mode is unclear on phone. P0. |
| Guard/Patrol Continue | Written design: save exact duty and restore it without a different route/post. | Latest inspected Police report at 2026-11-02 17:49:27 UTC has 46 passes and 3 failures. The failed guard assignment causes guard-geometry save/resume assertions to fail. | Confirm a valid connected guard post in the fixture, rerun the entire sequence, and preserve the failed result as evidence until replaced. Do not relabel 32/0 or 41/0 from an earlier report as the latest result. P0 verification. |
| Roads and visibility | Latest owner: roads never hidden; hide only a building that is touched. Client: buildings must not block controlled crew. | RtsBuildingVisibility.Start explicitly disables bridge, road-column, railway and elevated-road renderers/colliders. Its LateUpdate automatically hides broad city meshes based on crew and enemies. The old RTS suite positively expects some rail removal. | Direct contradiction with the latest request. Replace the rule and the old acceptance assertions. Restore road/rail/bridge visibility and collision. Only explicit building input may change a qualified building's visual state. P0. |
| Fewer circles and readable identities | Owner: too many circles, but still identify what matters. Client: own crew/rivals/police/territory easy to distinguish. | FocusedWorldPresentation hides GangArea ZoneRing only when map detail is off. Territory, business, response, Home and other marker circles have separate owners. World label budgeting reduces some text. | One policy must govern all annotation families. Preserve selected-unit rings and one relevant target; replace passive venue circles with small sign/icon/ownership cues. P0. |
| Obvious Home territory | Client: permanent interactive base, not menu/task list. Owner: highlight and name a place to build. | HomeTerritorySystem makes a named boundary and HQ/Supply Yard/Training Ground plots, reuses visible models and saves completed flags. Home33 passes construction/control/recruitment scenarios. | A one-level three-plot base is a useful foundation, not a complete investment/growth game. Need staged visible upgrades, contextual building interaction, meaningful reinvestment and completion migration. P1. |
| Construction continuity | Design proposes saved timer/assignment and levels. | HomeBuildingState stores site and completed only. An unfinished job restarts after reload without cost; completed facilities persist. | Either implement in-progress resume and upgrades, or keep that limitation visible. Do not use an unqualified statement that all construction progress persists. P1. |
| Short real-action tutorial | Client: exact player actions and first mission flow. Latest owner: brief instruction on what to press/build. | RecoveryProgression tracks selection, accepted movement, multi-selection/split and duty issuance. ModernCityHud displays one next purpose and Show Me. | Needs a compact state sequence that points at actual button/object, waits for successful action and arrival/result, and recovers after interruption. Pressing/opening a sheet cannot substitute for doing the task. P0. |
| Strategy beyond fighting | Client: different groups doing different jobs; resources and outcomes; fight only one task. | Businesses, supply production, construction, care, preparation, social operations, capture and Away resources exist. Native tests isolate several loops. | The connected production-speed loop has not been played naturally. Resource sources, uses and opportunity costs must be visible during allocation. P0/P1. |
| Matchday | Client and owner: the central identity; multiple firms/supporters/police, simultaneous incidents, free-camera strategy. | StadiumMatchdayActivity creates groups/phases and saved records. MatchdayStrategySystem has two timed presence responses, deadlines, penalties/retry and aftermath credit. Earlier city fixture passed these in isolated conditions. | Two staffed markers are not a complete strategic football event. Need role/route/venue choices, natural walking-time balance, useful simultaneous pressure and clear phase/outcome direction. Must pass without an all-automatic success path. P0. |
| Away progression | Client: sequential trips; Home permanent; no repeating all Home tasks each trip. | CampaignMissions has Home plus four districts. Actual travellers/reserves, payment once and physical extraction exist. Earlier 82-check fixture seeds district prerequisites and checks return. | Full natural Home and every Away chapter remain unverified. Distinct scenario identity, preparation and losses must survive actual play, not just seeded completion. P1. |
| Shops and commerce | Owner: earn/spend/invest, buy/sell/loot, useful city shops. | CityBusinessSystem implements control, timed income, optional raid, disruption/cooldown and contest/retake. HQ recruitment and construction spend cash. Existing businesses reuse six storefronts. | No complete shared goods inventory, stock-based purchase/sale, resale pricing or shop-specific trade progression is established by this audit. Do not call control/loot a finished buy/sell economy. P1. |
| Playable Police | Client confirmed the category for later milestone; owner now authorizes patrol/arrest tasks. | PoliceCityMode and PoliceCampaignStore form a separate officer career: patrol checks, dispatch, identified suspect, detention, walking escort, booking, shift/recovery and save isolation. | Current guard-geometry test failure must be resolved. Police vehicles and broader career content are additional work, not proven by the foot-patrol loop. P0/P1. |
| Player police car and multiple occupants | Latest owner: several officers enter a car, player taps to drive and exits them. | NPC PoliceCarChaser handles patrol/response/bribe departures. Playable PoliceCityMode at this snapshot assigns foot officers; its source has no player boarding/drive/disembark loop. | NPC van departure is not a controllable player vehicle. Need capacity, passenger identities, tap route, safe exit, task interruption and save handling. P1. |
| Ads | Owner: SO with No Ads/Test/Real and test IDs; rewarded support. | Google Mobile Ads 11.6.0 and EDM4U 2.1.0 are imported. Actual SO selects Disabled/Test/Live, app/unit IDs and optional placements. Ads14 checks config and callback rules. | Device test ad, consent/failure/background behavior and exact native package must be validated. Editor simulation is not an impression or revenue. P1 integration/release. |
| IAP | Latest owner includes packages/IAP. | No Unity Purchasing package or completed store purchase/restore/receipt flow was established in inspected source/manifest. | Product catalog, optional purchase benefit, sandbox provider callbacks, restore and transaction deduplication are required. Never present an editor Buy button as a real purchase. P1 integration. |
| Production readiness | Owner asks for a production-ready game. | Previous Android report says Succeeded, 0 errors, 37 warnings, 1,336,332,669 bytes, UTC 2026-11-01 19:36:02; it predates latest requests. A newer build/test cycle is in progress. No connected device was recorded. | Old APK and editor tests cannot certify current release. Content completeness, player comprehension, touch, congestion, performance, install/update, ads/IAP sandbox and signing/store work remain. P0 truthfulness / release gate. |

## 3. Current test ledger and the failed result

This is an audit snapshot, not a live dashboard. Check timestamps and current report contents after fixes. Reports under `Artifacts/Recovery` are generated with disposable save files.

| Report inspected | Result at audit | What it proves and what it does not |
| --- | --- | --- |
| redesign-playtest.txt | 33 passed, 0 failed | Actual Home crew arrival, cost/cancellation, completed facilities, supply benefit, recruitment, saved completion and contextual order transitions in an accelerated fixture. Not a full natural campaign/device tutorial. |
| police-playtest.txt | 46 passed, 3 failed | Many actual shift, escort, recovery and mode-isolation behaviors pass. The report is still failed. Failed assertions: custom rectangular guard assignment with off-center post; versioned exact guard geometry save; exact guard geometry after Continue. |
| Artifacts/CityQA/rts-controls-playtest.txt | 101 passed, 0 failed in retained report | Core selection/duties, gestures, NPC neutrality, bribe/departure and actor checks. Its former rail-hiding assertion conflicts with the new owner requirement and must be replaced, not used to reject the new design. |
| unity-save-checks.txt | 12 passed, 0 failed | Native serializer contracts in isolated data. It is not all-scene full-session persistence certification. |
| rules-checks.txt | 42 passed, 0 failed | Campaign/economy rules outside a Unity scene. Does not prove actual travel, rendering or device input. |
| Artifacts/Milestone01/playtest-checks.txt | 16 passed, 0 failed | Role-menu/New Game and editor aspect ratios. Not physical phone touch or first-user comprehension. |
| ads-checks.txt | 14 passed, 0 failed | Config IDs and reward-attempt/callback validation. No network test ad, purchase, impression or consent interaction on device. |

The three Police failures currently form a cascade from a rejected fixture guard order. This audit does not conclude that the serializer itself is corrupt. The acceptance sequence must first establish a connected accepted guard post, then test the saved and resumed geometry. If the fixture is corrected, rerun and record the new report; a proposed fixture explanation is not a pass.

`Implementation_Status.md` still contains earlier Home24/Police32 values at the audit snapshot. They must be updated from fresh completed reports, including failures. A current build must not silently inherit an old green result. Previous large-scene city integration is earlier evidence, particularly where it suppresses director pressure or seeds prerequisites.

## 4. Reference comparison: what is actually established

Fresh research rechecked the two firsthand PC accounts below. No complete official manual was successfully obtained in this audit. Search results for unrelated Steam manuals were excluded. Do not claim the full original mission script, economy values, interface or every original system was analyzed.

### Verified reference principles

GameSpot describes a small directly commanded core, specialist capabilities, pub-based morale/recruitment, unit experience, urban exploration and route-dependent scenario objectives. Its examples involve existing venues and opposition; it does not establish the proposed permanent construction progression or a playable Police career. [GameSpot hands-on preview](https://www.gamespot.com/articles/hooligans-storm-over-europe-preview/1100-2871935/)

Worthplaying's firsthand review describes contextual shop actions, controlled recruits versus followers, visible pub ownership and objectives that include escorts/rescues rather than only combat. The useful mobile lesson is to choose a relevant action at the world object and allocate limited people around a scenario purpose. [Worthplaying review](https://worthplaying.com/article/2002/2/5/reviews/2202-pc-review-hooligans-storm-over-europe/)

### Reference principles versus mobile adaptations

| Original principle established above | Mobile product adaptation | Fidelity gap that must remain explicit |
| --- | --- | --- |
| Command a selected core in an urban RTS | Persistent tap selection, tap street to move, object-specific choices, optional advanced Orders | Physical touch and mode-switch errors need device evidence. Desktop inputs are not copied literally. |
| Use useful existing urban venues | Pub, food shops, care, station, supply sites and readable local ownership | Persistent steady-income control and Home construction are requested product adaptations, not asserted original construction mechanics. |
| Roles and unit development matter | Persistent crew with useful scout/leader/defender/logistics/care decisions | Saved roles alone do not implement the complete specialist or original condition model. |
| Missions require route/resource decisions | Guided Home, differentiated Away, matchday routes, escort, capture and extraction | Five current chapters are not the original full campaign. |
| Police create an opposing city pressure | Neutral/hostile NPC police plus an explicitly separate playable career | Player Police vehicles and career missions are owner-directed extensions, not source-verified original faction play. |
| Context makes each site actionable | Tap storefront/building to choose affordable/relevant action | The current product still has some ledger/task-sheet dependence; object discoverability must be tested. |
| Football atmosphere supports tactical purpose | Arrival, pub demand, rival pockets, police, kickoff and dispersal events | Busy decoration alone is insufficient; outcomes must change through direct player choices. |

The reference target is an allocation-and-scenario experience. A growing collection of buttons, circles or automatic fights cannot demonstrate it. Conversely, adding Home construction does not make a game more reference-faithful by itself; it is useful when it supports the owner's desired long-term development and clear strategic choices.

## 5. Priority 0 implementation contract: mobile RTS, visibility and guidance

### P0-A. Preserve roads; hide only the touched building

Implement a building-only selection/visibility controller with an explicit eligible-building component or classification. Road, rail, bridge, ground, water, foliage, traffic and city infrastructure renderers/colliders are excluded. A tap that has already been consumed by UI or used for unit selection/order must not also hide an object. An intentional building touch changes only that building's renderer group. Restore it on a deliberate second touch, clear selection or the defined reset action. Do not remove physical collision or NavMesh data to hide visuals.

Acceptance: before/after renderer and collider snapshots prove all roads/rails/bridges unchanged; only the touched eligible building changes. Tap ground to move still works. Touching UI or crew does not hide a building. Two buildings can be independently restored according to the chosen policy. Save/reload returns a safe default visual state. Record actual phone input and road visibility, including the stadium approach.

### P0-B. One discoverable, explained command flow

Keep tap-to-move. A selection coach names the selected member/count and shows one contextual next action. Tap a rival for an explicit target decision; tap a place for its local options. Orders is an optional explained sheet, with Guard/Patrol/Regroup target instructions and Cancel Target. Never rely only on an undiscovered long press. The base command ability is free.

Acceptance: complete the exact Brick/Dex client sequence with physical taps; give a guard, then a new move; a patrol, then explicit attack; attack, then regroup. Unselected groups retain duties. Drag and pinch create no orders. UI taps never fall through. Invalid routes preserve the prior order and state a specific reason. A command that zero members accept cannot draw a success marker.

### P0-C. Fewer annotation circles, not fewer identifiable people

Create one presentation policy for Home, selected crew, current command, territory, businesses, incidents and patrol checks. Default: one player Home boundary, selected member rings, one current objective/command cue and compact faction/ownership icons. Passive businesses use a storefront/sign/small status, not a permanent large circle. Show territory boundaries in a deliberate map/detail view or when that territory is selected. Active incidents can have compact urgency icons with a timer; the target expands only when inspected.

Acceptance: count visible annotation families in the initial scene, during two simultaneous incidents and in map-detail mode. There is no stack of ring/label/bubble/panel for the same object. Identification works by shape/status/name as well as color. Minimap ownership/event markers correspond to actual world state.

### P0-D. Short tutorial: press, act, observe, continue

Use five short real-action beats: select one member; move and arrive; establish free HQ; assign a second person separately; use a named business and spend an earned resource. Each beat points at the actual target/control, says what it does in one sentence and waits for the action/result. It must be recoverable after cancellation, death, save/reload and camera pan. No tour of every menu. Longer command lessons appear only on first relevant use and can be reopened.

Acceptance: no auto-selection/menu opening completes a lesson; actual crew arrival/work completes the relevant beat; an interrupted task gives a clear retry; an advanced save bypasses new introductory chores without resetting earned state. A tester unfamiliar with the project can identify Home and complete the first business loop without verbal developer help.

## 6. Priority 0 implementation contract: matchday as the central RTS scenario

The current phase/response backend should support a purposeful scenario, not merely two automatic timers. Define the first event through an explicit preparation brief: where supporters arrive, which venue supplies resources, which exit must stay usable, likely rival routes and how many people can be spared.

At least two situations must create an allocation choice. Example: guard pub trade with one group while another escorts a supporter group along a station-to-stadium route. A scout can reduce uncertainty or reveal a safer route. Rival pressure can contest one link, so the player may redirect, defend, regroup or accept a declared loss. Ordinary NPC behavior continues, but a player's success is not automatic.

Use actual people and path positions to resolve requirements. Show time to arrive versus opportunity, stage changes, incident status and stake. Keep the free camera; focus is only the player's request. Do not force every unit into one stadium fight or auto-select people when an event spawns. Dispersal changes destinations and stops fresh peak flashpoints. Aftermath summarizes saved consequences and offers recovery/next planning.

Acceptance: play at production movement speed and starter/earned stats without fixture warps. A passive run and a competent split-group run yield different understandable outcomes. Both required routes remain usable in practice under crowd/traffic load. Save halfway through pressure resumes the same group/member states and incident progress. Failure has a local consequence and recovery, with no duplicate payouts/penalties. At least one valid strategy uses logistics/positioning rather than defeating every rival.

## 7. Priority 1 implementation contract: visible Home investment and commerce

### Home development

Extend the current three useful plots into visible development, with a small number of meaningful levels. Use central tuning for construction/upgrade costs, active simulation time and benefit. A new level changes a building/model/sign or occupied footprint in a clear restrained way and has a tactical use: roster capacity, readiness efficiency, supply stock or safer deployment support. Do not add a tree of cosmetic counters with no decision attached.

State must identify stable site, completed level and paid/in-progress state if resume is supported. Document whether cancellation refunds, pays on completion or retains work. A halfway save cannot charge twice or erase a paid level. Old Home progression migrates without starter rewards. New props cannot block walking routes or hide the Home boundary.

Acceptance: invest, watch actual crew work, observe a visible level change and use the resulting benefit. Compare two mutually competing purchases at the current cash balance. Interrupt/reload and verify declared policy. Complete a Home/Away/Home cycle with the same facilities and resource consequences.

### Buy/sell/loot economy

Add one understandable shared goods ledger: stable item IDs, owned quantities, central price/stock definitions and transaction rules. Start small: supplies, food/readiness and a tradable nonessential resource. Each shop's card shows stock, buy price, sell price and the immediate consequence; transaction quantity cannot exceed available cash, stock or owned goods. Stock and cooldown are saved if they affect progression. Prevent a guaranteed unlimited same-price buy/sell loop.

Keep venue control/trade income and optional loot as distinct choices. Buying helps readiness; selling can free cash at a loss/opportunity cost; investing increases sustainable income; looting is a bounded heat/disruption tradeoff. Spending and receipt feedback happen once. Cash/goods belong to the active mode/campaign and cannot leak into the other role.

Acceptance: insufficient funds/stock/quantity is rejected without a debit; a purchase and sale persist through native save; double-tap/repeated callback cannot repeat a transaction unintentionally; sell cannot create negative inventory or free profit loops; loot remains a timed exposed crew assignment with its existing consequences. Complete preparation without requiring an ad or compulsory civilian crime.

## 8. Priority 1 implementation contract: player Police vehicle

Reuse the appropriate vehicle model/navigation backend, but own player transport state separately from NPC response cars. A visible police car/van has a bounded capacity, passenger list and clear input state: parked, boarding, occupied, driving, stopped, exiting. Selecting it must not accidentally change foot officers' orders or board everyone in the city.

Select officers, tap the car and choose Board. Only eligible officers approach and board once close enough. Occupants are tracked by stable officer IDs and do not remain selectable duplicate pedestrians underneath the car. Select/tap the occupied car, tap a reachable street to drive and show the route/destination. Exit stops safely and places each living officer at a distinct reachable connected point beside the vehicle; never inside a building, water or a hostile road collider. Resume normal selection and patrol/dispatch actions after exit.

Define interactions with an active detention/escort. A car move cannot lose a detained suspect or duplicate booking. If this iteration supports officer transport only, state that clearly and keep suspect escort on foot. Save/reload must restore vehicle/passenger identity safely, or deliberately disembark on a safe street with no roster duplication. Do not claim NPC bribe departure proves player driving.

Acceptance: two or more officers approach, board and disappear only at the door; the same car drives to a tapped reachable destination; unboarded officer retains a duty; several officers exit at separate safe points and remain controllable; capacity and already-boarded checks work; target cancellation and interrupted boarding preserve state; no duplicate officers after Continue; firm save bytes remain unchanged during the police test.

## 9. Priority 1 integration: ads and IAP

Keep the existing SO as the ad authority for Disabled/Test/Live. Use official sample units for device tests and clearly label editor simulation. Retain opt-in rewarded support, bounded per-day/cooldown rules, valid completion callback, consent eligibility and no forced ad during direct control. Disable/rejected/no-fill/cancel paths return to gameplay without money or a stranded spinner.

IAP requires a separate product catalog and actual purchasing provider integration. Products need stable store IDs, product type, explicit benefit, availability and platform catalog configuration. Consumables grant only after a valid completed transaction; durable entitlements restore correctly. Store transaction IDs are deduplicated. Editor/sandbox purchase simulation is labelled and cannot be described as a real charged purchase. Required commands, basic recovery and campaign continuation are not products.

Acceptance: configuration validation, sandbox purchase, cancellation, failure, repeated receipt, pending purchase, restore and mode-switch callback tests. A callback cannot credit a different projected save after switching Hooligan/Police. Record a real Google test ad and platform purchase sandbox transaction on physical target devices before claiming those integrations verified. Publisher accounts, live IDs, consent/store declarations and signing remain real release configuration.

## 10. Definition of a reviewable production candidate

Before calling a build production-ready, finish the current mobile RTS/matchday and requested vehicle/commerce loops, resolve current failures, and play Home and every Away/Police scenario naturally. Record exact source revision/build hash, package result, supported device/OS matrix, install/update behavior, safe areas, touch response, memory/frame pacing, background/resume saves, congestion, audio and provider failure behavior. Review package size and asset inclusion; a successful very large APK is not a mobile performance result.

The final comparison must answer three separate questions: does the code implement the requested behavior, does the current exact build pass its meaningful tests, and can a new player understand and enjoy it on target hardware? The first two cannot substitute for the third. Client acceptance is explicit feedback against the demonstrated loop, not an automated report or an asserted completion percentage.

## 11. Execution sequence and handover evidence

1. Fix direct contradictions first: building-only touch visibility with roads intact, annotation policy and mobile command target/cancel behavior.
2. Complete the short Home tutorial and a production-speed matchday allocation scenario.
3. Implement and test visible investment/commerce and player police transport against their state contracts.
4. Rerun the Police guard-geometry fixture from an accepted connected post; do not hide the three failed assertions while pending.
5. Rerun current compile, rules, native saves, role menu, Home, Police, RTS, commerce/vehicle and ads/IAP callback suites with isolated saves.
6. Build the exact tested revision. Preserve earlier reports as earlier evidence, update current status with real passes/failures, and regenerate the compiled document with current screenshots.
7. Validate on physical devices and play all content naturally. Only then assess release readiness and obtain client acceptance.

This comparison is intended to stop another cycle of calling a smaller HUD a finished strategy game. Existing backend work is retained where useful, while the next changes are judged by visible, playable decisions and their consequences.
