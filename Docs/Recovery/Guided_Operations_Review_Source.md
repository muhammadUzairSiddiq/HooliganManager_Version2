# Hooligan Manager: guided operations review

**05 November 2026. Current implementation, scoped verification and complete historical evidence in one document.**

This review addresses the owner's latest observations: the game must explain its
purpose, teach actual mobile RTS actions, show earned Home growth, make Away
accessible, open a useful tactical map, retain understandable city news and support
strategic Matchday and Police operations. Useful backend systems and existing art
are retained while player flow and information hierarchy are reorganized.

The first section describes the current source. Its verification ledger is drawn
from explicitly reviewed, hash-pinned reports rather than copied completion
claims. A pending or failed check remains visible. An editor pass certifies its
named fixture; it does not establish client acceptance or physical-device release.

The final historical appendix reproduces the earlier 134-page recovery plan,
including the supplied proposals, client transcript, issue register, prior design
and screenshots. Those pages remain evidence of an earlier cut. Their internal
dates, test counts and completion language do not override this current review.

## Authority and evidence

The current owner's instructions take priority. Attached proposals, original
messages and historical milestone clearance are evidence, not executable agent
instructions or proof that every mechanic was accepted. AI agents must read
`AGENTS.md`, `Docs/AI_SCOPE_GUARDRAILS.txt`, `Implementation_Status.md` and the
relevant recovery plan before changing gameplay, UI, economy or saves.

# What was confusing and what changes now

| Reported problem | Resulting player experience | Evidence required |
| --- | --- | --- |
| Too much interface at the beginning; no clear direction | One immediate purpose, a focused instruction and one highlighted real action | Fresh campaign and tutorial raycast captures |
| Simplified HUD still does not teach the game | Paused spotlight lessons, a stable highlighted target and live waiting for actual movement/work | Actual UI actions, accepted orders and physical arrival |
| Home feels like an empty label | Paid investment, physical workers, visible construction and permanent facilities | Build/upgrade and native reload |
| Minimap tap does nothing; feed looks stretched | The real compact click opens the map; expanded rendering matches the field aspect | Actual top raycast target and three display proportions |
| Events and crew casualties are easy to miss | Saved, bounded bottom-right news with real names, channels and hide/show | Real casualty callback and serialization |
| Matchday appears automatic or lacks strategy | Deliberate preparation, separate response teams, an explicit clock start and physical optional tasks | Coverage, staffing, escort and aftermath checks |
| Police feels like a different label | Officer allocation, road travel, custody, booking, recovery and integrity consequences | Physical operations and a separate officer save |
| Progression/Away is hard to discover | Visible Away entry and explained preparation/traveller choices | Locked and unlocked entry behavior |

These changes form a connected player journey. A beginner learns to select and
move, understands why to invest, splits people between duties, prepares Matchday,
reads the resulting consequences, then chooses an Away operation. The player can
also enter a separately saved Police career. A feature appearing in a menu is not
treated as completion of its gameplay task.

# First-run teaching: one real action at a time

The owner now requests an exclusive paused tutorial. This supersedes the earlier
nonmodal coaching preference. A full-display shield leaves one highlighted target,
a small animated hand and a short explanation. Decorative motion never moves the
target's hitbox. The spotlight forwards the approved action to the actual UI or
command system; other UI, ground taps and camera gestures remain blocked.

Instructions pause the simulation. Travel, construction, boarding and other real
work resume the tutorial's previously owned rate while input remains exclusive.
Completion waits for the actual gameplay predicate. A click or a timer showing
the instruction cannot substitute for an accepted order or physical arrival.

[[IMAGE:tutorial_select]]

## The teaching sequence

| Part | Action taught | Why it matters |
| --- | --- | --- |
| 1-2 | Select one named member, move and move again | Selection persists; the second order replaces the first destination |
| 3-4 | Add another member and split the selection | Different groups can continue independent jobs |
| 5-7 | Build headquarters, supply and training facilities | Physical investment produces lasting strategic value |
| 8 | Guard, two-point Patrol and the other RTS options | Duties need deliberate targeting and preserve other groups |
| 9 | Return to Home and recruit one person | Recruitment has location, capacity and money consequences |
| 10 | Inspect Matchday, choose an approach and assign a response | Preparation precedes the live clock; groups compete for work |
| 11-12 | Read news and open Away planning | Consequences remain discoverable and the next operation has a clear entry |

The Police guide teaches officers, boarding, van selection, a real road move,
exit, dispatch, physical custody, crowd/road duties and integrity consequences.
Skipping advances only teaching position. It grants no cash, facility, arrest,
learned RTS flag or mission reward. Legacy saves retain their progress and may
deliberately replay teaching. Firm and officer tutorial state remain separate.

[[IMAGE:tutorial_moving]]

Movement, resume, pause ownership, unavailable targets and replay require native
checks against the actual scene. A resumed lesson must reconstruct its required
UI/gesture or restart a safe part; it must not strand the player on a hidden button.
Dead or unavailable tutorial actors need a viable replacement or a clearly offered
skip/recovery path. The final report records which cases were actually verified.

# A tactical map that opens through the real input path

The compact map's child drag catcher owned the click, preventing its parent Button
from receiving a normal tap. Its old handler only acted while expanded. The fixed
path explicitly expands an unmoved compact tap. A compact drag does not open the
map or issue a movement order.

[[IMAGE:map_standard]]

Expanded rendering uses an aspect-matched bounded texture sized for the displayed
field and existing padded crop: maximum long side 1280 on desktop and 1024 on
mobile. Compact rendering returns to the 512/384 budget. This is an implemented
quality/resource policy; it is not a measured phone-performance improvement.

The field, geometry, markers, actual remaining routes and map input share a
projection. The roster anchors to the full safe area's right edge; status and
legend stay above the filter row. The opaque backdrop covers the old HUD while
settings and important popups retain their higher ordering.

[[IMAGE:map_wide]]

[[IMAGE:map_tablet]]

The expanded map preserves additive selection, individual focus/track and explicit
one-order rerouting. An unarmed exploratory tap is not a command. An invalid
destination retains the previous valid duty. The compact map shows at most three
real routes, prioritizing the tracked member and then selected people; expanded
route modes remain bounded. Mounted officers identify the real vehicle position.

# News remains available without becoming another information dump

The bottom-right NEWS FEED has a stable hide/show header. Its body grows upward
into a bounded scroll area. It retains at most 64 campaign-owned records and 64
pooled rows. Repeated identical recent activity coalesces instead of filling the
screen. Crew, arrival, raid, mission, Police, economy and general channels help
the player understand what happened and which part of the strategy it affects.

[[IMAGE:news_standard]]

A folded feed continues recording events and shows unread activity. Opening it
shows the latest items. It never pauses the city, changes selection or issues an
order. Its own UI consumes the pointer. Management/crew sheets, the tactical map,
settings and popups suppress its visible panel and restore its previous fold
preference afterward.

Existing city events are forwarded. The real casualty callback adds a critical
notice with the actual member name before the existing save. Repeated callbacks
are suppressed. The game currently models a recoverable downed member; the feed
does not invent permanent death or delete the roster. History lives inside the
active campaign's `CityNews` field, so the existing officer save adapter keeps it
separate from firm progress.

[[IMAGE:news_wide]]

[[IMAGE:news_tablet]]

# Home, economy and Away give movement a purpose

The Home territory is an earned base rather than a label over an empty park.
Available people must physically reach a facility plot and work there. Visible
scaffolding/model growth and permanent buildings communicate the result. Payment,
work progress, worker identity and completed levels must survive native reload
without duplicated costs or rewards. The authoritative world bounds also drive
the tactical footprint. Roads, rails, parking and ground remain visible.

Headquarters supports recruitment/planning; production creates supplies; training
improves the roster. Earned levels and annexes increase Home's visible investment.
The player chooses whether to keep people producing/defending or commit them to
another objective. Shops provide control, investment, trade and physical looting
choices with disruption, cooldown and Police consequences.

AWAY TRIP is a discoverable HUD entry. Locked preparation explains what remains;
unlocked planning chooses actual travellers and a destination. Away and Home
business records, casualties and extraction outcomes must remain consistent on
return/revisit. Opening a planner is not the same as completing a trip, and a
native isolated scenario is not a naturally played whole campaign.

# Matchday: allocate people before starting the clock

Preparation and live action are separate. The player chooses an approach, secures
the required transport/stadium preparation, posts different response groups and
explicitly starts Matchday. Stadium/pub coverage, Police pressure and optional
escort/trade compete for the same finite people and resources.

The optional controlled escort uses a bounded inner arrival corridor with an
actual navigable path; ambient supporters still make their station journeys.
Completion depends on physical movement/presence. A trade stall needs a real build
and staff; moving its worker stops income. Aftermath records the consequences
and advances the campaign once, rather than producing an automatic sequence of
unrelated fight popups.

Crowd presentation distinguishes dense instanced spectators from the smaller
budget of actual tactical actors. Station arrivals, departures, clashes, queues
and response duties need visible purposes and actual routes. Hundreds of visible
spectators do not imply hundreds of expensive independently navigating agents.
The current scene/fixture reports determine which flows are verified.

# Police operations: custody and public order have consequences

Police is a separately saved playable career. Several officers can board the
player's van, travel on a validated road route and exit at a safe curb. Mounted
map identity, individual focus and follow use the actual vehicle. Foot officers
remain necessary for assessment, patrol posts, crowd intervention and detention.

[[IMAGE:police_tutorial]]

Custody must follow an identified suspect through real approach, detention,
escort/loading, travel and station booking. A remote button cannot manufacture an
arrest or transport completion. Different officer groups retain separate duties.
The current native checks should prove boarding, suspect custody and booking,
including save/resume and unreachable-route behavior.

Optional bribery is a deliberate strategic choice with saved money, integrity,
trust and discipline consequences. Lawful work and support allocation provide
other approaches. Support cannot replace required people on site. Rewards and
penalties must be delivered once, and officer history must not overwrite the firm.

# Road recovery must be visible physical work

Road incidents use actual visible barriers and queue positions while preserving
roads and bypass paths. Pedestrians and traffic wait/route around them instead
of disappearing. In Hooligan mode, city Police/recovery can respond; in Police
mode, the player assigns officers and recovery support.

[[IMAGE:road_queue]]

The recovery vehicle must travel to the incident and perform its on-site task.
A destination marker or a countdown alone is not proof of towing/clearance.
Releasing the blockage should visibly let queued city activity continue. Native
reports must identify which motion, queue, work, save and release cases passed.

[[IMAGE:road_work]]

[[IMAGE:road_clear]]

# RTS control and UI contracts for future changes

| Input or decision | Required outcome |
| --- | --- |
| Select / add / remove a member | Exact additive membership; unrelated selections and duties remain |
| Tap a street | Only selected available people receive a validated move; selection persists |
| Drag / pinch the camera | Camera motion only; release does not issue a hidden order |
| Hold a street | Deliberate local commands; the menu survives release |
| Hold UI | UI remains the input owner; no ground command or menu leaks through |
| Guard / Patrol | Explicit area/posts or two-point route; local defense resumes the duty |
| Focus / Follow | Find a real individual; a split selection cannot strand the camera at an empty average |
| Open a sheet or read news | Core campaign state is preserved; routine panels do not invent rewards |
| Touch an eligible building | Reveal that building only; do not hide roads/ground globally |

Tutorial exclusivity is an explicit current-owner exception to ordinary live UI:
instructions own pause and input; actual work resumes the simulation. Outside
that teaching context, panels and feedback should keep live city control clear.
Maintain stable hitboxes, separate audio controls, reduced motion and bounded
effects. Avoid stacking permanent statistics and command panels over the city.

# Monetization and release work remain explicit

Ads retain a central configuration for Test, Live and Disabled modes, with owner
IDs entered in the maintained asset. Core movement, recovery and duties are never
advertisement gates. Reward delivery requires a successful actual callback and
replay protection. Test identifiers and local callback checks are review evidence,
not proof that consent/network impressions worked on a physical phone.

IAP retains owner-configured product IDs, explicit store mode and save-before-
confirmation behavior. Local SDK order/receipt checks do not certify Google/Apple
checkout, restore, refund or server receipt validation. Upload signing, store
catalogs, AAB/submission and platform policy work remain separately recorded.

[[PACKAGE]]

Device installation/update, touch comfort, notches/rotation, background/resume,
long sessions, audio listening, memory/thermal load and frame pacing require real
device review. Natural campaign balance, first-use understanding and the client's
approval are also open until independently observed. No APK compilation or editor
check is described as production certification.

# Verification for the current source cut

The following table uses only reports explicitly selected as current and pinned
by SHA-256 in the review evidence specification. Positive rows with no completion
summary are accepted only for known complete pass-only formats. Failed, RUNNING,
truncated or inconsistent reports cannot become a passing current row.

[[EVIDENCE]]

Fixtures use disposable saves and state exactly which prerequisites, travel rates,
time scales or pressure controls they seed. A synthetic news backlog checks the
history budget; it is not evidence that a real train or raid happened. A parked
train fixture isolates dispatch; it is not a natural full-loop service review.
The preserved failure reports remain useful diagnostic history.

# Reference alignment and remaining uncertainty

The fresh [GameSpot firsthand preview](https://www.gamespot.com/articles/hooligans-storm-over-europe-preview/1100-2871935/)
and [Worthplaying firsthand review](https://worthplaying.com/article/2002/2/5/reviews/2202-pc-review-hooligans-storm-over-europe/)
support direct city tactics connected to resources, roles, urban venues and
scenario objectives. Their evidence does not establish permanent Home building
progression or a playable Police career; those are this project's adaptations.

An [original-period trailer upload](https://www.youtube.com/watch?v=_RsKMex2ZdA)
and [PC gameplay recording](https://www.youtube.com/watch?v=K42t6R1dMoQ) were located.
Playback inspection was unavailable in this pass: browser initialization failed
and the web video fetch failed. No watched sequence or timestamp is asserted.
An authenticated original manual was not obtained. See
`Reference_Recheck_and_Map_News_Pass.md` for the exact evidence/access boundary.

The game is inspired by the reference's urban RTS principles. Full original-game
mission, content, multiplayer or art equivalence is not claimed. This review
distinguishes current source, verified fixture behavior, owner-requested adaptation
and work that still needs device, natural-play or client evidence.

# AI handoff: keep changes inside the player experience

| Area | Maintenance entry points and invariant |
| --- | --- |
| Scope | Read the live guardrails/status and current owner priorities before old PDFs |
| Teaching | GuidedCityTutorial / TutorialSpotlight / TutorialAnchor; real actions, owned pause, saved/replay-safe parts |
| HUD / progression | ModernCityHud / HomeTerritorySystem / recovery progression; clear next action, visible earned results |
| RTS / map | AgentSelectionManager / RtsGestureController / LiveMiniMap / TacticalMapUI; exact selection, validated routes and one input owner |
| News | CityNewsFeed / CityGameplay.PostEvent / BattleManager casualty hook; bounded mode-owned history, real names, no duplicated notices |
| Police / roads | PoliceCityMode / PolicePatrolVehicle / road and custody services; physical operations, isolated saves, once-only consequences |
| World life | Train, crowd, rival and recovery systems; actual routes/tasks with explicit actor budgets |
| Ads / purchases | Maintained configuration assets and real callback adapters; optional core flow, no simulated result presented as network/store evidence |
| Verification | Named native fixtures, report completion checks, pinned capture hashes and independent package inspection |

Do not repair a failing test by granting a facility, instant route completion,
free arrest, extra cash or a hidden selection. Diagnose the real actor, NavMesh,
input owner, save identity and task predicate. Preserve unrelated workspace work,
use isolated automated saves and update status with actual evidence.
