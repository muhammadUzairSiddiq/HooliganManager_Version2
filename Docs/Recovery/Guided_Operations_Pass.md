# Guided operations pass — 05 November 2026

The latest owner request changes first-time teaching from the previous nonmodal
coach to exclusive, paused mobile lessons. The owner also requests a discoverable
Away planner, a useful expandable map, named injury news, stadium crowd flow,
physical road recovery and meaningful Police custody/integrity choices.

This is an implementation contract and evidence ledger, not client acceptance or
a declaration of store readiness. The previous 553 passing editor assertions and
03 November APK describe their earlier source cut.

## First-run presentation and input

The owner's follow-up rejects the large lesson panel. The revised guide uses a
floating top heading, one short action, a circular spotlight and animated tap,
double-tap, drag or two-finger hand cues. Back, Next and per-part Skip keep stable
safe-area hitboxes within each lesson and sit above low targets when necessary.
The circle stays inside the display; the hand points down at low buttons.
These controls navigate lessons without executing the highlighted
game action or awarding campaign results. News and minimap are hidden until
their own interactive introduction. The earlier objective coach is suppressed
while the exclusive guide is active. See Implementation_Status.md for current
verification rather than assuming this source description is a native pass.

`GuidedCityTutorial` owns a separate high-order safe-area canvas. A dark veil has
one highlighted cutout, a code-native animated hand and one short instruction.
The transparent target proxy forwards only the approved real UI action. The
full-display shield and the shared world-input gate suppress other UI and world
taps, camera gestures and minimap interaction. Decoration never moves hitboxes.

Instructions pause `Time.timeScale`. Accepted travel, construction, boarding and
other physical work resume the prior simulation rate while input stays exclusive.
The next instruction waits for the actual gameplay predicate, not elapsed display
time. Movement checks the selected actors' actual accepted order revisions,
NavMesh destinations, path readiness and physical arrival. Slow/failed actions
retain a per-part skip rather than granting success. Disable/scene teardown
releases the tutorial's owned pause.

Seven initial parts cover individual selection, camera pan and zoom, repeated movement, adding and
splitting a group, headquarters, supply production and training construction.
Further parts cover actual Guard/Patrol targeting and confirmation, other RTS
options, paid individual recruitment, the stadium command board, news and Away.
The member flow also teaches deselect-all without cancelling actual orders, and
the normal world double-tap selects/focuses its member while keeping other crew
selected. Firm and Police guides both teach the actual minimap and news controls.
After genuine campaign preparation unlocks Matchday, a saved pending guide teaches
separate response assignments and the actual START button. The introductory tour
does not invent transport/stadium preparation or start a locked Matchday.

Tutorial position is additive saved state. Fresh campaigns opt in; legacy saves
retain their earned state and can deliberately replay. Firm and Police use their
own campaign state. Skipping advances only the lesson position, without resources,
learned flags, arrests or campaign rewards. Existing isolated gameplay fixtures
suppress this UI through their editor-only disposable save override; a dedicated
tutorial fixture explicitly opts in and exercises its real raycast path.

## Operational presentation

The latest owner correction supersedes the former HUD shortcuts. Home, Away and
Police are distinct mode choices. Away stays locked behind the existing earned
Home completion rule. Matchday is reached through the actual 3D MATCHDAY sign
beside the stadium and local world actions; its simulation remains in the city.
The tutorial highlights that sign rather than a HUD/command-board destination.
Selection commands leave
space for the bottom-right collapsible NEWS feed. News distinguishes the existing
recoverable DOWN/INJURED model from permanent death and identifies the actual member.
Compact-map taps must expand through the actual drag catcher; dragging must not
open the map or issue an accidental order. Expanded projection preserves world
aspect and uses the actual actors and navigation routes.

## Strategic Police and Matchday

Road incidents use visible cone/barricade models and connected queue positions.
They preserve rendered roads and mandatory bypass routes. Physical pedestrians
and traffic queue rather than disappearing. Hooligan mode dispatches NPC Police
and recovery; Police mode requires player-assigned officers and recovery support.
The recovery vehicle must travel and work beside the actual incident.

Police custody requires physical assessment, detention, escort/van loading,
travel and station booking. Bribes are explicit optional decisions, paid once,
with saved integrity/trust and support/discipline consequences. Bounded real rival
clashes sit within a larger instanced stadium crowd instead of adding hundreds
of expensive navigation agents. Train visitors arrive for identifiable purposes,
visit the district and physically return for departures under a body budget.

## Verification

Source compilation, dedicated native tutorial/map/news/road/Police checks and
visual captures are required for this cut. Record exact reports and any failures
in `Implementation_Status.md` after running. At authoring time these new features
are unverified. Device touch, audio listening, long sessions, Android/iOS thermal
performance, ad/IAP sandbox and store submissions remain separate checks.

Reference mechanics must link to actual firsthand reporting. Video metadata is
not watched footage; never invent timestamps, complete clone parity or client
approval. See the reference evidence document for this pass.
