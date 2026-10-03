# RTS controls review — 30 September 2026

This describes source changes in this workspace. It is not a claim that a new APK has been delivered or that every milestone is complete.

## Changed in this pass

| Requirement | Behaviour |
| --- | --- |
| Keep selection | MOVE retains the selection. Subsequent ground taps issue another MOVE. Tapping a member toggles only that member, including when standing over an interaction pad. |
| Split the crew | Add Brick and Dex by tapping each. Tap Brick to remove him; subsequent commands affect Dex. Defeating a gang no longer resets every member's orders across the city. Nearby fight assistance no longer overrides already assigned members just because they are selected. |
| Clear commands | Selected crew expose MOVE, ATTACK, GUARD, PATROL and RETREAT in the city HUD. ATTACK waits for a chosen rival; ground taps do not choose an arbitrary enemy. |
| GUARD | Tap GUARD, then a walkable area. The member goes there and defends within 12 world units, returning after the threat leaves. |
| PATROL | Tap PATROL, then the other end of the route. Each selected member travels between their starting point and assigned endpoint, engages local threats, and resumes the route afterward. |
| Manual control | A new MOVE, ATTACK, GUARD, PATROL or RETREAT replaces the previous duty. Taking another hit does not cancel a manual withdrawal MOVE or RETREAT. Neutral police are not automatically attacked; hostile police can trigger local defence. |
| Movement feedback | Commands report how many selected members accepted the order. Busy or unreachable orders report a failure instead of a false success. Arrival uses navigation stopping distance. |
| Camera | Rapid world taps issue orders without recentering. Squad double-tap and the explicit camera controls remain available. No fight-camera feature was added. |
| Matchday decisions | The automatic stadium popup becomes a persistent world bubble. Its choices can be closed and reopened while the city continues running. Choices show cost, morale and heat effects. |
| Police decisions | City police confrontations start with a world bubble. FIGHT/BRIBE choices can be dismissed and reopened. The confronted group remains held until the confrontation is resolved; other groups remain controllable. |
| Routine fight results | Completing the rival phase posts to the feed instead of opening another decision window. Campaign outcomes remain separate. |
| Minimap | Selected members have larger markers and names on the expanded map. Inactive crew/rivals are excluded from unit markers. |
| Input work | Selection updates reuse portrait objects and keep their order stable. UI tap suppression lasts for the consumed frame instead of a 160 ms interval. Navigation paths are reused and are no longer calculated twice per MOVE. |

## Already present before this pass

- Larger character presentation and distinct faction colours.
- A free city camera, live feed, minimap, territory and operation markers.
- Stadium phases, multiple home/rival/police groups, independent group tasks and concurrent flashpoints.
- Police heat, arrests, rival growth, and home/away mission systems.
- Cash-based health, power, speed, stamina and intelligence upgrades.

The presence of these systems in source is not proof that their balance, performance and all mission flows have passed device acceptance testing.

## Scope and remaining verification

- Base GUARD/PATROL are available without payment in this implementation. Rewarded ads, IAP, paid guard/patrol ranks and their balancing are not implemented by this pass.
- Patrol is a two-endpoint route with local defence, not a drawn multi-waypoint route. Busy activity-locked members reject manual movement until released by their activity.
- Stadium actors use the existing distance-based streaming system. Offscreen group records and markers persist, but this pass does not add detailed offscreen combat attrition or replace the matchday simulation.
- The police system still has one response/search sequence alongside independent stadium patrol groups; this pass does not add multiple independent arrest investigations.
- Test Android touch gestures, camera drag/pinch, double-tap squad focus, small-screen readability and frame/input timing on the target device. Editor command tests alone cannot establish mobile latency.
- Inspect home and away campaign completion, large crowds, all popup paths and sustained simultaneous combat before declaring the milestones accepted.

## Repeatable verification

Unity playtest result: **37 passed, 0 failed**. The checks cover selection, real movement and arrival, group splitting, guard/patrol defence and resumption, hostile versus neutral police, manual withdrawal, HUD commands, local decisions and preservation of unrelated orders after a gang defeat. The command HUD capture was also inspected for readable labels. These are editor checks, not Android device certification.

Run **Tools → Hooligan → Run RTS Control Playtest** in Unity edit mode. It uses a disposable campaign file, enters the real Gameplay scene, checks navigation/selection/duties/interactions, writes `Artifacts/CityQA/rts-controls-playtest.txt`, captures `Artifacts/CityQA/rts-controls-hud.png`, then exits play mode. It temporarily bypasses the local first-install cache wipe so testing cannot erase the campaign through that bootstrap.

Player acceptance sequence: select Brick → MOVE to A → arrive → tap B → add Dex → move together → tap Brick → move Dex alone → GUARD an area → PATROL a route → observe local defence and return → override with MOVE → manage another group while a matchday or police bubble is open.
