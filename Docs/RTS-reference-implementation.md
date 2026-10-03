# Hooligan Manager — implementation checklist

Updated 3 October 2026. ✅ = implemented and checked. ⏳ = still requires external validation. The latest animation and heatmap requests override the earlier idle, walk/run and BUBBLE requirements. Playable Police Mode remains deferred.

## Latest requested changes
- ✅ Use the supplied **Dwarf Walk** for all character movement, including baked moving crowds.
- ✅ Use the supplied **Bouncing Fight Idle** as default idle. No asset named “Fight to Idle” is present; this is the available fight-idle clip.
- ✅ Remove the experimental neutral idle and foot adjustments; remove the Walk/Run setting.
- ✅ Replace BUBBLE with **HEATMAP**, full-screen and above all gameplay UI, including the expanded minimap.
- ✅ Orange/red live crowd density, circular faction displays, support/crew totals and city-hold percentages.
- ✅ Blink **YOU** on the player faction; show live green gains and red losses above the bubbles.
- ✅ Paid packages: £2,500/+30, £7,500/+100, £20,000/+300. Each previous purchase adds £500 to subsequent prices.
- ✅ Purchases debit once, respect cash/capacity, persist support and reinforce streamed supporter groups.
- ✅ Keep **PANEL** and **HEATMAP** below the top panel in both visibility states.
- ✅ Start the top panel hidden; PANEL opens it, a red X closes it.
- ✅ Place **MANAGE / SELECT ALL / DESELECT** together inside the squad panel. Remove CANCEL.
- ✅ Tap SELECT ALL selects the live crew; hold it to enter rectangle selection. DESELECT clears selection and returns drag to camera control.
- ✅ Paid growth, rally/outreach support rewards, territory-based support capacity, and existing income/development operations form the gradual expansion loop.
- ✅ Triple initial rival strength, increase starting turf area 2.5 times and retain timed growth without raising the active fighter cap.

## Reported regressions and optimisation
- ✅ Preserve corpse size using bind-pose world skinning; ground the final body.
- ✅ Re-enable a culled animator before death. Real death transitions checked on all three imported models.
- ✅ NPC/supporter kills do not increase player police heat; managed-crew actions retain their consequences.
- ✅ Halve the spectator cap to 500; use varied groups of 3–15.
- ✅ Dark-green crew and brighter green supporters; white civilians and blue police.
- ✅ Most crowd groups move; vary animation phases and group formations.
- ✅ Stationary cheering groups stay near the stadium; moving groups have shorter waits between destinations.
- ✅ Recurrent rival/home, rival/rival and police encounters across multiple locations.
- ✅ Camera-ahead preloading, wider retention radius, bounded role-specific actor pools and preserved streamed actor state.
- ✅ Shared low-poly crowd poses under 1,000 vertices; one material per interactive skinned renderer; distant traffic/actors culled.
- ✅ Four-times recruitment package quantities.
- ✅ Six saved minimap layers with checkmarks, reduced labels and a visible camera footprint.
- ✅ Heatmap reuses UI objects and a 192×108 density texture; updates only while open.

## Current verification
- ✅ **93 gameplay/UI checks passed, 0 failed** — includes heatmap deltas, selection gestures, real deaths, heat attribution, streaming limits, duties and occlusion.
- ✅ **81 character/asset checks passed, 0 failed** — includes Dwarf Walk/fight-idle bindings and reduced crowd meshes.
- ✅ Home services/recruitment save-serialization checks pass.
- ✅ Inspected heatmap, HUD, minimap, duty-marker and death screenshots. Latest Unity log contains no C# errors or runtime exceptions.
- ⏳ Physical Android profiling, real touch acceptance, sustained memory/thermal testing and final store-release validation. No Android device was available for this pass; PC checks do not certify mobile performance.

## Earlier scope retained
## Characters and presentation
- ✅ Replace crew and rivals with the imported gang model.
- ✅ Use the imported civilian model for pedestrians and supporters.
- ✅ Use the capped police model; blue shirt, trousers and cap.
- ✅ White civilian shirts, green home shirts, distinct rival shirts.
- ✅ Import shared humanoid Mixamo animations; verify deformation on all three rigs.
- ✅ Dwarf Walk, fight idle, punches, hit reactions, death, rallying, cheering and clapping states.
- ✅ Fix giant corpse scaling; world-vertex regression passes.
- ✅ Update portraits to the new models.
- ✅ Remove retired high-poly models and their generated meshes after dependency audit.
- ✅ Remove procedural block spectators; crowd poses derive from the supplied civilian model.
- ✅ Blood and bounded persistent fallen-body effects.
- ✅ Final death-transition, reduced-crowd and animation-binding checks.

## RTS controls and interface
- ✅ Remove the bottom Move/Attack/Guard/Patrol/Retreat strip.
- ✅ Direct interaction choices; no yellow-dot intermediary.
- ✅ One-word button captions; descriptions in the status/title areas.
- ✅ Exclusive camera, rectangle selection, guard and patrol input modes.
- ✅ Rectangle selection returns to camera mode after selection.
- ✅ Guard area assignment, defence and return; persistent green boundary and pin.
- ✅ Patrol A/B route validation, reversal and return after combat; endpoint pins and route line.
- ✅ Scout destination marker and local intel reward.
- ✅ Persistent attack target marker.
- ✅ PAY settlement: charge once, stand down the whole firm, prevent renewed chasing.
- ✅ Dwarf Walk for every movement state; supersedes the earlier Walk/Run option.
- ✅ End-to-end hold/drag and occluder restoration tests.
- ✅ Guard/patrol marker screenshot and scout/attack marker checks.

## City, base and simulation
- ✅ Preserve the existing city, progression, management, missions, away trips and NPC police.
- ✅ Retain Home Territory recruitment, training, recovery, upgrades and finances.
- ✅ Business defence and rival recapture logic.
- ✅ Rival reinforcements of 5–10 at individual 120–240-second intervals, subject to population caps.
- ✅ Walking arrivals and independent rival encounters.
- ✅ Slower trains, timed station stops and supporter arrivals.
- ✅ Camera framing and minimap footprint overlay.
- ✅ Obstructing building renderers hide and restore; agents remain selectable through them.
- ✅ Remove elevated road/support rendering; lower the rail corridor and train together.
- ✅ Stadium flag animation and recorded ambience/cheering; attribution in credits.
- ✅ Ten-officer barrier groups and larger supporter groups across stadium, pub and rail areas.
- ✅ Recurrent, concurrent supporter clashes and police intervention.
- ✅ Crowd population/group checks, ground-rail and bridge-removal checks; train schedule configured for slower travel and station stops.
- ✅ HQ services and recruitment verified through save serialization.

## Validation
- ✅ RTS regression: 93 passed, 0 failed.
- ✅ Role model / animation / dependency checks: 81 passed, 0 failed.
- ✅ Expanded integrated playtest and final Unity error review.
- ⏳ Physical Android profiling, touch acceptance and long-session thermal/memory testing. No device performance claim has been made.

The supplied interactive models retain their authored geometry (about 1,862–2,034 triangles each). The non-interactive spectator poses are separately reduced for crowd rendering. They are not replacements for the detailed interactive rigs.

Evidence: `Artifacts/CityQA/rts-controls-playtest.txt`, `role-character-checks.txt`, `retired-character-assets.txt`, and the current gameplay screenshots in the same folder.
