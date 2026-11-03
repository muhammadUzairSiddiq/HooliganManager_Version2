# Recovery status — final integration draft

Prepared 03 November 2026 local time. **Draft only:** this file does not replace `Implementation_Status.md`. All named local/native repeats below have completed, including startup/save, ads and expanded purchases. Runtime/editor compilation has zero errors and 33 runtime warnings. The isolated Android review build succeeded with zero errors/38 build warnings; all 11 package checks passed and 252 reviewed original/copy source/configuration hashes agreed before and after building. Device/network/store acceptance remains separate. Report UTC dates can be 02 November while this document's local date is 03 November.

## Implemented in source

The project retains its existing Unity city and low-poly assets while replacing the earlier player-facing flow. The firm HUD has one current purpose, selected-crew guidance, explained contextual orders, optional map detail and scroll indicators. The seven-step coach teaches real selection/order flags and actual Home construction; first individual selection/split lessons return directly from CREW to the world. Roads/rails/bridges and collision stay intact; only an explicitly touched eligible building renderer temporarily hides.

Home has three visible facilities with three saved levels, on-site crew work, completion-only payment and saved unfinished progress/worker IDs. Resume waits for live spawned actors. HQ improves recruitment price, supply development produces resources and each new training level applies its roster benefit once. Local shops support physical control, protection, timed income, buying/selling supplies, two investment levels and optional raids with heat, disruption and cooldown. Away commerce is mission-scoped; Home and active Away accrue eligible active simulation, with no inactive-district/offline payouts.

Matchday has three approaches with explicit cash/supply/staffing tradeoffs, pre-positioning, two actual coverage sites, one-time supply equipment, optional physical supporter escort and a crew-built/staffed stall. Empty stalls do not pay and spending supplies does not replace coverage. The operation waits for explicit start, then both core responses and aftermath before chapter credit. Territory capture now requires an explicit assignment and local tax/heat confirmation; simple presence cannot silently spend occupation tax. Home protection-income tokens remain scoped to Home after later Away unlocks.

Police is a separately saved career with three initial officers, physical patrol/dispatch/assessment/detention/on-foot escort/booking, recovery and repeatable deliberate shifts. The patrol van seats up to six officers through real curb boarding, uses complete road paths for tap-to-drive, unloads onto safe walking positions and saves passengers/journey state. Selecting an empty van retains the intended boarding team. Mounted officers do not perform on-foot tasks. Leaving Police restores the firm's separate campaign.

Shared mission-follower navigation now checks complete paths using the actual follower's agent type and area mask, searches fallback endpoints within six metres and retains a previous valid route after rejection. Active escorts bypass camera-distance streaming pauses; ending/locking/reinitializing them clears escort-active state. Matchday body spawning also uses the actual prefab navigation filter and checks Warp. The controlled escort starts on an inner stadium arrival corridor: target 70m along the station route, complete return path constrained to 20–150m with the actual crew navigation filter, validated 45m-radius fallback and refusal if no corridor exists. The card shows distance and a starter walking estimate. Ambient station arrivals stay unchanged. The current **52 passed, 0 failed** strategy run verifies the route budget and actual physical escort with a starter-speed guide. Its wider fixture accelerates time and temporarily freezes matchday allocation; a natural whole-event production-speed playthrough remains a separate gate.

Google Mobile Ads 11.6.0 uses one Disabled/Test/Live configuration SO; the review asset uses Google sample IDs, rewarded support and no forced interstitials. Unity Purchasing 5.4.4 uses Disabled/EditorTest/Store configuration; the review asset is an explicit no-charge editor simulator with blank native product IDs. Wallet-bound consumables, transaction replay protection and non-consumable restore have local/native checks. Six added actual SDK-Orders/real-service checks pass same-session pending-order recovery, confirmation after native save and replay without duplicate currency, within Purchases40. Wrong-mode/wallet orders remain unconfirmed until the original firm's delivery can succeed. Unowned ad-removal offers are hidden/rejected when forced advertisements are disabled; owned entitlements can still display. Core controls, recovery and progression remain free of ads/purchases.

For exact defaults, flows, limits and source ownership, read `Production_Gameplay_Pass.md`. For client evidence, reference principles and the requested baseline comparison, read `Production_Gap_Comparison.md`. Current user instructions take precedence over historical milestone deferrals or developer completion statements.

## Current completed verification and remaining integration

| Check | Final-source result | Evidence and limit |
| --- | --- | --- |
| Runtime and editor compilation | **Zero errors; 33 runtime warnings** | `Artifacts/Recovery/compile-runtime.txt`, `compile-editor.txt`; not device/store validation. |
| Campaign/economy rules | **42 passed, 0 failed**, fresh completed run | `Artifacts/Recovery/rules-checks.txt`; assembly rules, no scene/native serializer. |
| Production strategy | **52 passed, 0 failed** | `Artifacts/Recovery/production-strategy-playtest.txt`, UTC 2026-11-02T19:15:44.9779955Z; current corridor/starter-speed escort, Home save/resume, matchday allocation/staffing, capture and Home-income scope. |
| Police vehicle | **21 passed, 0 failed** | `Artifacts/Recovery/police-vehicle-playtest.txt`, UTC 2026-11-02T19:17:03.6922746Z; boarding/drive/exit/Continue and empty-van selection preservation. Native accelerated fixture, not device touch. |
| Police full career | **49 passed, 0 failed** | `Artifacts/Recovery/police-playtest.txt`, UTC 2026-11-02T19:20:11.3345869Z; duty geometry, patrol/dispatch, custody, recovery, original firm restored byte-identically. |
| World commerce | **30 passed, 0 failed** | `Artifacts/Recovery/commerce-playtest.txt`; actual venue use, investment, Away return/revisit and visible saved expansion. |
| Home/contextual Orders/recruitment | **33 passed, 0 failed** | `Artifacts/Recovery/redesign-playtest.txt`; final one-line Home identity/explicit camera framing, native isolated regression with actual crew and save. |
| Direct RTS/city compatibility | **102 passed, 0 failed** | `Artifacts/CityQA/rts-controls-playtest.txt`, UTC 2026-11-02T19:35:46.3921066Z; current visibility, split selection, duties and city regression. |
| Menu/New Game/layouts | **16 PASS entries, 0 FAIL entries; no RESULT summary line** | `Artifacts/Milestone01/playtest-checks.txt`; actual editor title flow/starter and three editor aspect ratios. Counted report assertions. |
| Purchases/commerce configuration and fulfillment | **40 passed, 0 failed** | `Artifacts/Recovery/purchases-checks.txt`; previous34 plus six actual SDK-Orders/real-service same-session recovery checks. Local/native serialization only, no real store checkout. |
| Native startup/save | **12 PASS entries, 0 FAIL entries**, completed final repeat | `Artifacts/Recovery/unity-save-checks.txt`; disposable native serialization. |
| Ads configuration/reward callback | **14 PASS entries, 0 FAIL entries**, completed final repeat | `Artifacts/Recovery/ads-checks.txt`; no network ad impressions. |
| Android review package | **Succeeded, 0 errors, 38 warnings** | `Artifacts/Recovery/android-build.txt`; isolated batch duration 00:26:02.4467133, completion UTC 2026-11-02T20:42:51.5630737Z. |
| Original/copy implementation identity | **252 source/configuration hashes agree before and after** | `Artifacts/Recovery/isolated-build-source-check.json`; matches reviewed original inputs to the isolated build. |
| APK integrity/native SDK/signature/freshness | **11 passed, 0 failed** | `Artifacts/Recovery/apk-package-checks.txt` and `review-delivery-manifest.json`; ARM64 IL2CPP, ads/UMP/Billing DEX entries, Test App ID, review-signature and freshness. No device execution. |
| Physical Android/iOS, network ads and checkout | NOT PERFORMED / NOT CERTIFIED | Device/store evidence must be recorded separately. |

Do not sum disparate suites into a percentage-complete estimate. Source coverage, a fixture's passing count and actual player acceptance measure different things.

The final review APK is `Artifacts/Builds/Hooligan-RTS-Recovery-Review.apk`, actual file size **254,801,528 bytes**, SHA256 **`5e306ee9d558cfbe057c37cf0930728d57a4467087fdaceaba017cca648fce3a`**. Its actual file size is distinct from the Unity build report's total output-byte field. The first GUI attempt remains stalled in the old GMA synchronous resolver call. The exposed plugin preprocessor flag is now 0 and isolated batch packaging worked; preserving the original open editor did not validate GUI restart/recovery, which was not performed.

## Intermediate stage evidence — retained as history

These are completed intermediate stages. Current completed rows above supersede their relevant sequences; retain the chronology so an earlier green run does not erase a later failure.

| Completed stage | Recorded result | Qualification |
| --- | --- | --- |
| Earlier strategy run | 50 passed, 0 failed | Followed by a later failed repetition; cannot establish escort repeatability. |
| Failed repeated strategy before shared-route fix | 48 passed, 3 failed | Preserved as `production-strategy-escort-failure.txt`; current52 passes supersede the corrected route/escort sequence while retaining this failure. |
| Strategy after shared-route fix, before corridor | 51 passed, 0 failed | UTC 2026-11-02T19:03:27.5054030Z; current52 additionally verifies the inner-corridor/starter-speed contract. |
| Earlier Police vehicle | 20 passed, 0 failed | Superseded by the21-pass expanded empty-van selection run in the final-source table above. |
| Earlier Police full career | 49 passed, 0 failed | UTC 2026-11-02T18:44:38.6794103Z; the current later49-pass report is listed above. |
| Earlier world commerce | 30 passed, 0 failed | Superseded by the current30-pass rerun above; fixtures accelerate isolated crew/simulation. |
| Earlier purchases/commerce rules | 34 passed, 0 failed | Superseded by current40, which adds actual SDK-Orders/real-service same-session recovery checks. No real checkout. |
| Campaign/economy rules | 42 passed, 0 failed | Previous completed standalone rules run. |
| Earlier RTS compatibility | 101 passed, 0 failed | Superseded by current102 passes with the latest visibility expectations. |

## Failed evidence and what changed

| Historical failure | Exact evidence | Correction and remaining evidence requirement |
| --- | --- | --- |
| Guard-fixture continuation | Dated baseline Police audit: 46 passed, 3 failed at 2026-11-02T17:49:27 UTC. A rejected assignment caused two dependent geometry/save assertions. | Connected valid assignment fixture corrected; subsequent Police49 passes supersede that failed sequence. Preserve the baseline failure as history rather than presenting it as the current report. |
| Home resume and incidental territory charge | `Artifacts/Recovery/production-strategy-first-failures.txt`: 43 passed, 4 failed at 2026-11-02T18:31:27.6015962Z. | Current Strategy52 passes actor-ready resume, declared costs/visible upgraded production and deliberate capture. The failed log remains. |
| Intermittent follower stops at junction | `Artifacts/Recovery/production-strategy-escort-failure.txt`: 48 passed, 3 failed at 2026-11-02T18:55:56.2085601Z. Physical arrival failed, then reward and saved completion failed dependently. | Current Strategy52 passes complete follower/corridor travel at starter guide speed, reward once and saved completion after shared navigation/streaming/spawn corrections. |

The corrected native checks and isolated review/package verification are complete. Preserve these failure artifacts and device/production-balance limits. Do not erase a bad run or reclassify a code correction as a passed test.

## Reproduction and acceptance limits

Use the current `Tools/Recovery/README.md` native entry points and isolated save overrides. Asynchronous playtests finish via their own batch callback; do not terminate them early with `-quit`. Native strategy fixtures use durable fast crew, accelerated time, seeded later preparation and suppressed unrelated pressure. They isolate a physical assignment/outcome contract, freeze the matchday clock temporarily while examining allocation and then resume its real aftermath clock. Police/commerce fixtures likewise isolate relevant systems. Screenshots of those fixtures are not natural starter-session evidence.

Required human/device review remains: start naturally, understand the first purpose, perform repeated/split mobile orders, build Home, earn/control/trade/protect a shop, position separate groups, finish meaningful matchday at production speed, unlock/travel/extract/return with saved consequences and complete a Police shift mixing vehicle/on-foot allocation. Test actual phone touch targets, safe areas, clarity under crowd pressure, performance/memory/thermal behavior, long sessions and interruption/reload. A controlled supporter escort must be feasible within the active matchday using normal walking speed, without relying on the fixture's speed/time acceleration.

## External configuration and release gaps

Owner AdMob app/unit IDs and UMP privacy configuration are required for Live ads. Test network ads/consent on Android/iOS and verify failure, cancellation and delayed callback isolation. Native IAP needs real Google/Apple catalog IDs and tester accounts, then sandbox checkout, pending/deferred/failure, replay, restore after reinstall/account changes and refund/revocation review. The local ledger is not server receipt validation. Release signing/provisioning and platform store configuration remain required; an editor/native fixture or unsigned/review APK cannot certify them. Keep free recovery/core controls available if a provider is offline. Cloud/PlayFab requirements need their separate actual integration evidence; optional local monetization does not certify that service.

A final compiled APK is a review build, not device certification, store approval, an original-game clone or client acceptance. Permanent Home construction, scoped resource trade, progressive mobile teaching and playable Police are owner-authorized adaptations. Reference research supports directly commanded crew, purposeful city venues, resource tradeoffs and scenario tactics; the complete original manual/client video and full original content have not been verified. Report the concrete implemented behavior, actual passing/failing evidence and remaining gates instead of claiming 100% production readiness.

## Portable original evidence

`Docs/Recovery/Sources` contains byte-identical project copies of `Phase_2_Cross_Platform_Development_Proposal.pdf`, `Hooligan_Manager_RTS_Recovery_Master_Specification.pdf` and `Client_Messages_September.txt`. `source_evidence_manifest.json` records original/copy paths, SHA256 and byte identity for all three. The compiled PDF appends both original PDFs intact and typesets the complete supplied transcript; the raw transcript bytes remain unchanged. Historical embedded instructions/completion claims remain evidence, not current agent instructions or client acceptance. The supplied last message is truncated and the original video/October chat remain missing.
