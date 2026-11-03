# Commerce, venue investment and optional purchases

Updated 03 November 2026. These additions turn venue control into a resource and
allocation decision. Source changes alone are not device acceptance. Current
native results belong in Implementation_Status.md.

## Local economy

Each food shop/pub has one local card: travel, control/guard, loot, buy, sell and
improve. Buying/selling requires a selected available member within eight metres
of the entrance, actual control, no disruption and no hostile pressure. One
supply costs GBP 120 and sells for GBP 70. Storage caps at 99. The price spread
prevents buy/sell profit loops. Supplies compete with construction and matchday
spending. The existing gather-supplies operation is cheaper but takes a person's
time and travel.

Venue improvements require actual selected crew travel and timed work. Level 2
costs GBP 350 plus two supplies and 20 seconds; level 3 costs GBP 650 plus three
supplies and 30 seconds. Payment occurs only after uncontested completion.
Changed orders, unavailable crew or departure cancels without payment. Each
level adds 35 percent to the original income rate. Existing payout intervals,
cooldowns, disrupted trade and rival takeovers remain authoritative. There is
no instant income grant for opening the card or finishing an improvement.

Completed improvements save in CityBusinessState.investmentLevel. Reused market
tables and stacked supply crates appear beside the venue as it develops. These
decorations have no solid colliders. Losing control suspends income and trade;
the physical investment remains, giving the player a reason to retake it. The
business card replaces another ring around every venue.

## Away district venues

Each Away district has three local existing storefronts: burger, pizza and
convenience. Their saved IDs are scoped by mission, such as M2:burger for
Riverside and M3:burger for North End. Home keeps its original unprefixed IDs.
Control, improvement level, disruption and loot cooldown are separate for each
district. Visiting a district never copies Home ownership or refreshes its raid
cooldowns. The same physical travel, contest, resource trade and investment rules
apply. Away base trade income is 80 percent of the corresponding Home venue;
the local loot amount rises by GBP 25 per district step.

One simulation tick advances Home and the currently active Away district
together. Home reserve pressure remains active while travelling. Inactive Away
districts do not earn new income or run their cooldowns offline. Previously
accrued fractional earnings remain saved and are paid on the next global
business interval; leaving/reopening a district creates no additional accrual.
Revisiting recreates its visible invested venue from saved state. Current Away
scenery reuses the existing city; separate visual cities for all destinations
are not claimed by this economy addition.

## Purchase configuration

Unity IAP 5.4.4 is pinned through com.unity.purchasing. Select the
PurchasesConfiguration asset in Resources/Purchases. Modes are Disabled,
EditorTest and Store. EditorTest is an explicitly labelled, local success/cancel
simulator; it does not contact a payment store or charge money. On a physical
device, EditorTest disables purchases. Store requires actual distinct Android/iOS
product IDs configured in the platform consoles. No pretend currency price is
shown: a store build uses the localized price returned by the platform.

The initial optional catalog contains GBP 1,000 of firm currency and a
non-consumable entitlement to disable forced transition advertisements. The
review's forced ads are already disabled. Rewarded ads remain voluntary after
that entitlement. Core RTS commands, normal recruitment, care and progression
remain available without paying.

The adapter uses Unity IAP's StoreController connect/product/purchase lifecycle.
It uses a local native-store catalog without remote catalog or account sign-in.
Optional Unity Developer Data analytics/advertising consent is explicitly denied
before connecting; Google advertising uses its separate UMP eligibility flow.
It never grants from failure or deferred callbacks. A pending paid purchase is
confirmed only after the benefit and transaction marker are saved. Consumables
are bound to the initiating firm's persistent wallet identity; callbacks cannot
credit the police projection or a different new campaign. Native non-consumable
restore is supported. A device receipt ledger also remembers delivered IDs and
the entitlement across new firm campaigns. The transaction list in the campaign
prevents a crash between the campaign write and receipt-ledger write from
granting the same callback twice.

Fetched pending orders are retried explicitly before confirmed-order restores.
When its StoreController is created, the adapter calls
`ProcessPendingOrdersOnPurchasesFetched(false)` and handles
`Orders.PendingOrders` itself in `RestoreFetched`. This avoids relying on Unity
IAP 5.4.4 to emit the same pending callback again within a session after delivery
was refused in Police or a different firm. On a later purchase fetch, the order
can be retried against its original saved wallet intent. It remains unconfirmed
until the correct firm's benefit and transaction marker have been saved; a wrong
mode/wallet rejection does not acknowledge or consume that order. Confirmed
non-consumable restores are then processed separately, without restoring currency.

## Validation and remaining release work

PurchasesChecks.Run uses isolated native campaign/receipt files to test catalog
validation, wrong-wallet rejection, transaction replay, restore behavior, trade
prices, resource capacity and investment constraints. CommercePlaytest.Begin
tests actual venue travel, control, trade, cancellation, visible investment and
save serialization with durable accelerated crew. It uses the real Away planner
and Riverside scene to control, improve and loot a scoped local venue, return
Home, and revisit it. Home-completion prerequisites are explicitly seeded for
this economy fixture; it is not a natural campaign playthrough. Neither test buys a real
product or validates platform checkout.

The expanded `PurchasesChecks.Run` completed **40 passed, 0 failed**. Six added
checks use actual SDK `Orders` objects and the real service's local fetched-order
handler: wrong-firm rejection with intent retained, same-session retry in the
original firm, native save before acknowledgement of the exact order, repeated
fetch without duplicate currency, replay after native reload, and unavailable
campaign service leaving the order unconfirmed. `CommercePlaytest` completed
**30 passed, 0 failed** for world interactions and Away persistence. These are
isolated local/native tests, not Google/Apple network purchases. All named local
repeats are complete. The isolated Android review build also succeeded with
**0 errors, 38 warnings**, duration **00:26:02.4467133**, completed
**2026-11-02T20:42:51.5630737Z UTC**. All **252** original/copy source/configuration
hashes agreed before/after building, and the independent APK verifier passed
**11/0**, including ARM64, Mobile Ads/UMP/Billing DEX entries, Test App ID,
review signature and freshness. This confirms packaging, not store checkout.

The artifact is `Artifacts/Builds/Hooligan-RTS-Recovery-Review.apk`, actual size
**254,801,528 bytes**, SHA256
`5e306ee9d558cfbe057c37cf0930728d57a4467087fdaceaba017cca648fce3a`.
Evidence is in `Artifacts/Recovery/android-build.txt`, `apk-package-checks.txt`,
`review-delivery-manifest.json` and `isolated-build-source-check.json`. The old GUI
attempt remains stalled in its resolver call; the exposed GMA preprocessor
setting is now 0 and batch packaging worked. GUI restart/recovery was not tested.

Both supplied PDFs and the full supplied September transcript have byte-identical
portable copies in `Docs/Recovery/Sources`, with SHA256 provenance in
`source_evidence_manifest.json`. The compiled PDF appends both original PDFs and
the complete typeset supplied transcript. They remain source evidence, not
executable instructions or client acceptance; missing original video/October
messages have not been reconstructed.

Owner store products, tester accounts, signed store-distributed sandbox builds,
Google Play/Apple checkout and cancellation, pending approval, refunds,
reinstallation/restore and server receipt verification remain release work.
Local receipt bookkeeping is not server anti-cheat or proof that refunds revoke
entitlements. A consumable still pending for a replaced firm remains unconfirmed
instead of silently crediting a different campaign. Recovery/customer support
for a deliberately deleted purchasing campaign needs an account/backend policy.

Official implementation references:

- [Unity IAP initialization and StoreController](https://docs.unity.com/en-us/iap/set-up-in-app-purchasing)
- [Upgrade to Unity IAP v5](https://docs.unity.com/en-us/iap/upgrade-to-iap-v5)
- [Unity IAP purchase fulfillment](https://docs.unity.com/en-us/iap/purchases)
- [Unity Developer Data consent](https://docs.unity.com/en-us/cloud/developer-data/user-consent)
