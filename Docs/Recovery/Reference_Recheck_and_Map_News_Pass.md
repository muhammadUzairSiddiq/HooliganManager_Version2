# Reference recheck, tactical map and news feed

Owner request received 04 November 2026; implementation review 05 November.
Current owner instructions override the earlier presentation plan. Source changes
below require a fresh native run; the earlier 31/0 map result does not certify them.

## Reference evidence checked again

[GameSpot's firsthand preview](https://www.gamespot.com/articles/hooligans-storm-over-europe-preview/1100-2871935/)
describes a directly controlled core firm, distinct roles, pub recruitment and
morale, experience, urban venues, looting, police opposition and scenario objectives.
Its examples connect routes, resources, vehicles and tactical preparation. The
page currently displays a 2006 timestamp despite discussing the 2002 release;
do not silently describe that displayed timestamp as its original publication date.

[Worthplaying's firsthand review, 05 February 2002](https://worthplaying.com/article/2002/2/5/reviews/2202-pc-review-hooligans-storm-over-europe/)
distinguishes paid controllable recruits from followers, describes contextual shop
actions with different risks, and reports arrests with escort to police vehicles.
Its early scenarios teach controls and resource use. Other objectives include
protecting an arriving team bus and freeing captured members. These observations
support a tactical city game with decisions beyond continuous fighting.

Fresh video search located [the United Games 2002 trailer upload](https://www.youtube.com/watch?v=_RsKMex2ZdA)
and [a no-commentary PC gameplay recording](https://www.youtube.com/watch?v=K42t6R1dMoQ).
These are located video pages/metadata, not watched evidence in this pass. The
browser inspection tool failed to initialize with a missing kernel-assets path;
the web tool also failed to fetch the trailer playback page. No visual sequence,
timing, exact HUD geometry or footage timestamp is asserted from these results.

The manual search located an unofficial player FAQ that explicitly disclaims
official endorsement. An authenticated original manual was not obtained. Do not
promote that FAQ, search snippets or an inaccessible video into verified manual
or hands-on visual evidence.

## Adaptation and scope

The persistent Home construction progression, playable Police career, Android
touch rules, monetization and the requested bottom-right news feed are project
adaptations/current owner requirements. The sources above do not establish those
as exact original-game systems. Preserve the existing art and useful backend;
judge the result through actual player actions. This pass does not reproduce all
original missions, content, art or multiplayer and does not certify store release.

## Actual map issue and source correction

The compact feed's DragCatcher implements IPointerClickHandler and receives the
real event instead of its parent Button. Its old click path only acted while
expanded, so a compact tap was consumed without opening anything. The new shared
pointer path explicitly opens on an unmoved compact tap. A compact drag remains
an exploration/input gesture and cannot open the map or issue movement.

Expanded rendering now sizes a separate aspect-matched texture to the displayed
field, including the existing padded-crop requirement. The maximum long side is
1280 pixels on desktop and 1024 on mobile; the compact feed returns to its previous
bounded 512/384 budget. The camera, world markers, real navigation paths and input
still use one projection. Resizing invalidates the cached rendered patch. This is
a quality/resource policy, not a measured device-performance improvement.

The roster anchors to the right edge of the full safe-area frame; status and legend
anchor above the bottom filter row. These elements no longer retain a fixed
1600-pixel horizontal layout while the map field expands on wide displays.
The opaque full-display backdrop and settings/popup ordering remain in use.

## Requested news behavior

CityNewsFeed owns a dedicated bottom-right panel with a stable fold/show header.
It retains at most 64 saved records and 64 pooled rows, with a bounded scrolling
viewport. Repeated identical recent messages coalesce. New records are readable
by channel: crew, arrivals, raids, mission, police, economy and general. A folded
feed keeps collecting events and shows an unread count; opening it shows the latest.

History belongs to PlayerData.CityNews. The existing Police save adapter routes
officer data separately from firm data. Existing CityGameplay.PostEvent messages
are forwarded. The real BattleManager.OnAgentDied callback adds a critical named
notice before the existing save. Repeated callbacks for that actor are suppressed.
The wording reports the existing recoverable downed state; it does not invent
permanent death or remove a roster member. Arrival/raid/mission messages describe
actual system events; the feed creates no free income, arrests or combat results.

News does not pause time, change selection or issue orders. Its own controls block
ground input. Full map, management/crew sheets, settings and popups suppress its
visual panel; closing those restores the player's fold preference. Saves are
debounced and guarded against writing through a different active campaign object.

## Verification prepared; results pending

The existing isolated TacticalMapPlaytest now exercises the real compact raycast
target and click route, rejects compact dragging, checks rendered aspect and equal
world-axis scale at three aspect ratios, and retains selection/focus/reroute checks.
It also checks the actual news toggle/hitbox, event forwarding, duplicate aggregation,
backlog/row caps, scroll/input blocking, a real casualty callback and native save
serialization. Synthetic backlog messages are fixture setup, not real train/raid
evidence. The fixture restores its news preference and uses its isolated save.

No Unity/native run, physical-device touch test, video observation, natural campaign
balance result or new package result has been claimed for these source changes.
