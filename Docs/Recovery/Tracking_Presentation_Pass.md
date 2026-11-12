# Crew tracking, tactical map and presentation pass

Current owner request: 03 November 2026. This pass follows the accepted improvement
in the earlier review and addresses the owner's play observations. Current user
instructions take precedence over older no-animation presentation wording.

## Observed gaps and implementation contract

| Gap | Required resulting behavior | Verification |
| --- | --- | --- |
| Crew becomes impossible to locate after moving/splitting | Actual remaining world routes, named offscreen direction, per-person focus/follow and a useful crew tracker; group centering cannot strand the camera in empty space | Split two actors; focus/follow each, reroute, pause, die and cancel follow |
| Minimap is passive and unclear | Same coordinate mapping for geometry/icons/routes; actual names, additive selection, explicit reroute and ownership footprints; expanded view keeps mode clear | Native mapping/selection/validated command checks and three aspect-ratio captures |
| Slow and mechanical travel | Faster saved-preference travel pace, genuine walk/run clips and speed-matched blending; simulation clock and saved base stats preserved | Actual path travel, animation state, temporary-speed boost and pause checks |
| Home looks empty after completion | Existing real building models with visible scale and grounded bounds, saved levels; real green territory expands from development | Render/model bounds, actual construction, reload and expanded-map footprint agreement |
| Missing atmosphere/feedback | Appropriate licensed music/ambience, per-action audio, stable-hitbox decorative button response, bounded local effects | Actual clip/provenance/configuration checks, sound/motion settings and bounded bursts |
| Thin station/rival activity | Stopped-train arrival waves from distinct safe exits and groups; rival shop raids require real travel/presence and have saved consequences | Physical actors/routes, interruption/defense, bounded crowd and reward/cooldown checks |
| Laggy behavior | Remove measured/reproduced hot allocations/scans; bound rendering/effects and publish evidence | Editor profiling/sampling and actor/effect/path budgets; physical device results separately |

Earlier APK/test/PDF results belong to the previous source cut. They remain
historical until the affected checks are rerun. Do not certify this pass from
those green reports. Automated checks use isolated campaigns and restore user
preferences. Actual device comfort/performance, new-user understanding and store
release remain separate evidence.

## Implemented player flow

1. Tap a member and then a street. A stable member color connects that person's
   name, actual remaining navigation route and destination marker. World lines
   follow the street corners; an unreachable command cannot replace a valid route.
2. Use **CREW / FIND** for any individual. **FOCUS** centers that person;
   **FOLLOW** follows them as they move. Named edge buttons locate watched crew
   outside the current view. Centering a widely split selection chooses a real
   selected member instead of an empty average point.
3. The compact map shows up to three real routes, prioritizing a tracked member
   and then selected members. Opening the tactical map exposes names, current
   duties/HP, additive select/deselect, focus/track, route filters, Home/city
   framing and explicit one-order rerouting. A pan/pinch does not issue an order.
   Mounted Police officers are explicitly marked **IN VAN**, with foot selection
   disabled until exit; individual FOCUS/TRACK uses their van's live position.
4. The Home outline is derived from real plot/model bounds and earned levels.
   Existing office, tool-shop and gym models grow during actual construction;
   finished facilities and earned annexes remain after reload. The same animated
   bounds drive the world and tactical map. Road/rail/parking renderers remain
   visible. A separately sized trade-stall prefab prevents a full Home building
   from being reused as an oversized shop annex or Matchday stall.
5. Travel defaults to running at 1.65 times the saved base speed. Settings expose
   walk/run and three pace choices. Genuine walk/run clips follow movement speed;
   temporary speed boosts persist for their duration. Neither saved base stats
   nor the simulation clock are inflated to make travel look faster.
6. The existing short action-led guide now marks a connected nearby street for
   the movement lesson. Completing real actions supplies feedback; opening a
   sheet does not complete a lesson.

## World life and presentation

Stopped trains dispatch three staggered groups from distinct safe carriage/station
exits toward different real destinations. The physical stop is required; arrival
bodies are capped at 18, have a lifetime and release their crowd-budget tokens.
They do not become free player recruits. Existing rivals can walk a complete
route to raid a shop, perform timed work, and lose the opportunity to real
defenders, police or a new order. Successful raids save disruption/cooldown and
NPC loot state; they do not award the player cash for enemy work.

The maintained feedback profile supplies authored, licensed music and semantic
selection/order/build/task/combat/loot/arrest/reward cues. Fixed-size buttons
animate only decorative content, and success effects use bounded non-interactive
pools. Settings expose master/music/effects, separate mutes, reduced motion and
audio credits. See Audio_Presentation_Pass.md for exact sources and limitations.
Local construction dust, short-lived raid fire/smoke and optional effects only at
verified water surfaces add activity without changing paths or adding world
selection circles.

Performance work removes repeated label sorting, per-tick civilian scene scans,
per-command material creation and per-result synthesized audio. The minimap
renders at a bounded cadence with crop reprojection between renders; actual
rendered-view projection is tested independently of logical map round trips.
World routes, labels, edge controls, sound voices and effects have explicit caps.
The traffic cache is rebuilt safely after hot reload instead of throwing on each
LateUpdate. This is concrete allocation/robustness work, not a claimed phone FPS.

## Integration findings retained for review

- The initial marker optimization incorrectly constructed a MaterialPropertyBlock
  in a static initializer. Unity rejected it on serialization/domain initialization.
  Creation now happens lazily in the actual main-thread command action. Native
  presentation checks exercise the shared selection/input path afterward.
- Traffic held a private interface-array cache that Unity cannot restore during
  script reload. OnEnable and a null-cache fallback rebuild it, with actual
  destroyed/disabled-part and invalid-route/target checks.
- Visual review found resized Settings buttons retaining their old child-label
  geometry. Their captions and decorative children now fit the resized bounds.
- Initial Home visual checks passed 25 assertions and failed two after an upgrade
  was cancelled. Its single frozen-enemy snapshot did not suppress subsequent
  periodic arrivals. The saved L1 correctly reloaded; that failure was not evidence
  that completed model reload itself was broken. The preserved report is
  home-visual-before-upgrade-fix.txt. A further 10/17 run exposed unstable travel
  with artificial base speed 18 and accelerated time. The corrected 27/0 fixture
  uses real base speed 2.5, normal-time travel and accelerated on-site work; actual
  L2 completion, geometry and native reload remain required. The same correction
  fixes the strategic fixture's four save/resume cascade failures (now 52/0).
- The first crew fixture's split actor route was calculated from another person's
  location. It now queries Dex's actual NavMesh type/area at his deployed position
  and verifies subsequent physical movement. The 42/1 report is preserved.
- The next crew run found that CityAtmosphere re-enabled shadows/FXAA after map
  setup. That subsystem now respects the map camera's ownership. Actual repeated
  atmospheric setup is checked; 47/0 passes. The 45/1 report is preserved.
- Expanded-map captures exposed underlying gameplay HUD text. Its opaque solid
  backdrop now covers the complete display and the map has priority below real
  settings/popups. Three aspect captures and 31/0 checks verify that hierarchy.
- The initial train/raid run was 24/4: all raids passed, while prior natural stops
  interfered with the fixture's manually started stop. The corrected test removes
  that race. Runtime fixes also validate each actual spawned actor's navigation
  and floor, route neutral travellers to a real connected shop, expose bounded
  dispatch diagnostics, and release failed spawn reservations. All 28 checks pass.

## Verification state

Runtime and editor source compile with zero errors (31 runtime warnings).
Campaign/economy rules pass 42/0 and imported audio audit passes 5/0. Current native
results are presentation 43/0, Home models/expansion 27/0, tactical map 31/0,
crew tracking 47/0, train/raid 28/0, strategic Matchday 52/0, transport plus mounted
map tracking 27/0, Police career 49/0, commerce 30/0 and contextual Home flow 33/0.
RTS/menu/save/monetization regression repeats and the separately named tracking
APK are still being finalized. No old report or earlier APK certifies them.

The completed editor timing observation samples 120 rendered frames on an MX130,
1280x720, six crew and 45 live enemies. Median is 28.096 ms and p95 59.484 ms;
raw values, viewport, pipeline and actual camera options are recorded in
crew-tracking-editor-telemetry.json/txt. Earlier samples are retained. This is not
a phone FPS target, profiler attribution or proof of an improvement percentage.
Both main and map renderer were Mobile_Renderer in the recorded repeat; an
earlier unrecorded renderer cannot be assumed to have been PC SSAO.

Representative current visual evidence lives under Artifacts/Recovery:
home-visual-complete.png, tactical-map-1280x720.png, crew-world-routes.png,
crew-split-follow.png, crew-finder.png, crew-compact-map-routes.png,
train-arrival-groups.png and presentation-settings.png. They are editor captures,
not Android/iOS screenshots. The earlier compiled PDF/APK remain historical.
