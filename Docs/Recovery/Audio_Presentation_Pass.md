# Audio and presentation implementation pass

Source snapshot: 03 November 2026. This pass implements the latest owner request for responsive button decoration and a maintained audio/event layer. The newer request allows decorative microbounce; the selectable button itself never changes position, scale, anchors or size. Earlier attachment instructions and older animation restrictions are historical evidence, not a reason to override that current request.

## Implemented behavior

- `GameFeedback.Emit(GameFeedbackEvent, Vector3? worldPoint = null)` is the semantic feedback entry point. `GameAudio.Play` and `PlayPresentationTheme` remain compatibility facades. Runtime result screens no longer allocate synthesized AudioClips.
- `Resources/Presentation/GameFeedbackProfile.asset` is the maintained mix ScriptableObject. It references 18 real event clips, authored menu/exploration music and a different authored matchday score. It exposes gains, cooldowns, optional world placement and the 1.5-second music crossfade. There are two music sources and eight reusable effect voices: two interface, four world/order and two high-priority notification voices. Shared-clip cooldowns prevent old and new call sites doubling the same sound.
- Menu and exploration use **Urban Theme** by MintoDog; an active matchday uses **Espionage** by brandon75689. These are actual supplied compositions, not crowd ambience renamed as a score. No claim of a newly commissioned score, adaptive stems, personal listening approval or client music acceptance is made. Music selection follows active scene and the existing stadium matchday state. Police exploration uses the exploration cue.
- Music imports stream compressed stereo Vorbis, preload disabled, background loading enabled. Short cues decompress on load, preload enabled, mono. Only 20 selected OGG files are imported (4,563,976 bytes); whole third-party packs and rejected candidate music stay outside Assets.
- Master volume retains `HM.MasterVolume`. Music/effects use `HM.Audio.Music` and `HM.Audio.Sfx`; absent values fall back to the existing campaign MusicVolume/SFXVolume. Changing a slider updates its matching campaign field and global preference; Save & Close saves preferences and the active campaign. Music/effects mute are separate persisted preferences. Stadium ambience and cheers follow the effects bus from creation, including an already-muted bus.
- Existing Settings artwork now contains master/music/effects sliders, music/effects mute, performance, tap walk/run, travel pace, reduced motion and an in-app Audio Credits button. `GameFeedback.OpenSettings()` exposes the same controls over a live city without leaving its scene or changing Time.timeScale; an explicitly paused game stays paused after close. The existing pause card also offers SOUND & MOTION. The public SettingsInputBlocked guard covers open state and the dismissal release window. The live backdrop stretches across the expanded safe-area frame and the 860 x 600 panel remains centered. Shared sheet ownership replaces prior management sheets. Movement controls call CrewMovementPreference and preserve saved base agent stats.
- Police detention emits Arrest once on the completed detention transition; a completed enemy shop raid emits TaskFailed for the player loss, rather than the positive loot cue.
- Landscape buttons get a brief rim response, a single hover/press sheen and a small label-only press/release response. All decorative Graphics have raycast disabled. The existing fixed button image remains the hit target. Authored scene components add the missing decorative rim at runtime; scene regeneration is not required. Resized Settings buttons also fit their caption, sheen, selection glow and rim to their new slot, keeping Save & Close and Performance text centered inside the actual hit areas. GamePopup retains its modal hit surface and receives a short opacity-only dialog reveal.
- Successful build/task/loot/arrest/reward events can show a brief non-interactive UI star pulse at their world position or a quiet screen anchor. Six reusable groups of six stars are allocated once, globally limited to one new burst every 0.35 seconds; each retires in 0.62 seconds. No colliders, world circles, GraphicRaycaster or command-blocking surfaces are added. Combat and ordinary movement do not create particles.
- Traffic Car/Wheel caches are rebuilt on enable and when lost, using Unity-aware destroyed-component checks. Missing interface/renderer references no longer crash LateUpdate. Disabled parts and inactive/destroyed targets are skipped; an invalid route retains the last safe pose and exports an empty traffic slot. Valid route interpolation remains active. These guards do not disable the traffic manager or suppress arbitrary exceptions.
- Panel reveal requires a CanvasGroup, reuses the existing component and reacquires a lost/missing Unity reference before accessing alpha. It uses Unity object checks rather than CLR null coalescing for component recovery.
- `GameFeedback.ReducedMotion` persists `HM.Presentation.ReducedMotion`; it suppresses decorative scale, sheen and success bursts. It does not suppress audio or meaningful state indicators. Other systems can read the same preference.

## Clip mapping

| Event | Imported clip | Meaning |
| --- | --- | --- |
| UiPress | click_001 | Button press |
| UiOpen / UiClose | open_001 / close_001 | Sheet/modal transition |
| UnitSelected | select_001 | Unit selection |
| MoveAccepted | footstep00 | Accepted travel order, not an every-agent footstep loop |
| CommandAccepted / CommandBlocked | confirmation_001 / error_004 | Order accepted or refused |
| BuildStarted / BuildComplete | metalClick / confirmation_003 | Physical work begins or completes |
| TaskComplete / TaskFailed | jingles_STEEL00 / jingles_STEEL14 | Brief authored result cue |
| CombatImpact | chop | Low-gain short impact; bounded by shared cooldown |
| Loot | handleCoins | Loot/currency action |
| Arrest | metalLatch | Arrest feedback |
| Reward | jingles_STEEL01 | Optional reward/result |
| Warning | error_003 | Important warning |
| TrainArrival | confirmation_002 | Compact train-arrival notification, not a whistle recording |
| UnitFootstep | footstep01 | Actual movement footstep; caller cadence and shared cooldown bound playback |

These are event identities and development mix choices, not claims about the original PC game's exact sound design. This pass does not add constant weapon, traffic or footstep loops over every unit.

## Provenance and redistribution

All new imports below use **CC0 1.0 Universal**: https://creativecommons.org/publicdomain/zero/1.0/ . Primary artist submissions were checked before import. Pack license files are preserved next to the imported clips. Original audio bytes are unchanged; only filenames of the two music files were made readable. Unity import encoding/mix settings are project adaptations. CC0 does not require attribution; author credit is nevertheless visible in the existing credits screen.

| Asset / author | Primary source | Download |
| --- | --- | --- |
| Urban Theme — MintoDog | https://opengameart.org/content/urban-theme | https://opengameart.org/sites/default/files/urban_theme_bpm115_0.ogg |
| Espionage — brandon75689 (submitted by HaelDB) | https://opengameart.org/content/espionage | https://opengameart.org/sites/default/files/espionage.ogg |
| Interface Sounds — Kenney | https://opengameart.org/content/interface-sounds ; https://kenney.nl/assets/interface-sounds | https://opengameart.org/sites/default/files/kenney_interfaceSounds.zip |
| 50 RPG sound effects — Kenney | https://opengameart.org/content/50-rpg-sound-effects ; https://kenney.nl/assets/rpg-audio | https://opengameart.org/sites/default/files/RPGsounds_Kenney.zip |
| 85 Short music jingles — Kenney | https://opengameart.org/content/85-short-music-jingles | https://opengameart.org/sites/default/files/jingleSounds_Kenney.zip |

Espionage offers alternative licenses; this project elects the submission's explicitly listed CC0 option. Urban Theme's primary page explicitly describes it as loopable urban music. Espionage's primary description calls it a loopable slow stealth song. Runtime choices are a reviewable interpretation of those recordings, not proof of stylistic client approval.

Existing crowd recordings remain **Gregor Quendel, Free Crowd Cheering Sounds**, CC BY 4.0; source https://opengameart.org/content/free-crowd-cheering-sounds ; author https://www.gregorquendel.com/ ; license https://creativecommons.org/licenses/by/4.0/ . The existing stadium_ambience.mp3 and stadium_cheer.mp3 are retained unchanged, with their credit preserved. See Docs/Third-party-audio.md for original recording names. No external-license claim is made about the older prototype WAV cues retained for compatibility.

## Imported byte inventory

SHA-256 values below refer to the original imported OGG bytes, before Unity encoding.

| File | Original archive path / source filename | SHA-256 |
| --- | --- | --- |
| click_001.ogg | Audio/click_001.ogg | `ccfb7fa0cccdd9faec0eb16033c732b1e308d139d80f799161495d58f7adcdb9` |
| open_001.ogg | Audio/open_001.ogg | `a27c6bb0df7da1e6af5dd5937593c98bc58b6e513f42fe6a3254cd6a6949c648` |
| close_001.ogg | Audio/close_001.ogg | `44af8249b933e0fd35ec957a638bb1a0b01f85b53fbe9674bf77bd3ca3168ef4` |
| select_001.ogg | Audio/select_001.ogg | `aec0c31ea934a35936ae0d2ab8fac8123c93aa5647f935853a58dbaf90278b7a` |
| confirmation_001.ogg | Audio/confirmation_001.ogg | `063564703b6094d70718a3e787a55cc9141611e4ecd6b6637f8828f79b4a8c3a` |
| error_004.ogg | Audio/error_004.ogg | `0b574cea597d96507e782ae9764f88482ce49f46e931e57054bf7150047f2d69` |
| confirmation_003.ogg | Audio/confirmation_003.ogg | `3091bf0be0497f825769ee0733ca7bdc3bcd59bd6c6e8f2ba8f93d580ff38022` |
| error_003.ogg | Audio/error_003.ogg | `885b28175c7b511118bcffab95675b48bbac9005cf6514656ebdad979276b3ab` |
| footstep00.ogg | OGG/footstep00.ogg | `6fe61ef1fc3bcf0e253bf2eb64759db6cb69e2fe452f4d88cc597ecf78a3d601` |
| metalClick.ogg | OGG/metalClick.ogg | `9851a69d0c613e13bceef08060ecc4148f098ef487927cbebe270d642398a3b3` |
| chop.ogg | OGG/chop.ogg | `d00c2b3c9fff07e376145c8c8c45c90e5084ec192f6ce0387db233f7b86f1486` |
| handleCoins.ogg | OGG/handleCoins.ogg | `8a91f969e932df709df80ee124d86a51389eed9b67f22e5e716bc2bbf60d8dab` |
| metalLatch.ogg | OGG/metalLatch.ogg | `ba9ba60b172b3ebc131a940f25793cd2e207aca7af73dc80d637277f060f1708` |
| jingles_STEEL00.ogg | OGG/jingles_STEEL/jingles_STEEL00.ogg | `8799eb726d784ae0aa1a9df80648a873ac60015bc00aea12c8e2dd1431a2bd7d` |
| jingles_STEEL14.ogg | OGG/jingles_STEEL/jingles_STEEL14.ogg | `a24f6f6f76063d8e0442b15e660896ec4dd11a7be45d19f71088408e002c99f8` |
| jingles_STEEL01.ogg | OGG/jingles_STEEL/jingles_STEEL01.ogg | `099b192f120f7eb7c24613be277428628b9ec9c81e1f8420254abce336fbe7e5` |
| UrbanTheme.ogg | urban_theme_bpm115_0.ogg | `564fe32a9421a771e10de249a38362789e7a116140ad8d74d8d9ebcb398d99cc` |
| Espionage.ogg | espionage.ogg | `1b267502fd678b9e74c01947339797ce76d6270978422c641bd1a8b277ac9d4c` |
| confirmation_002.ogg | Audio/confirmation_002.ogg | `33b17a9a9a2397c62b285c52c33a907fdffb476909c99e42dde603f6a7a8b12c` |
| footstep01.ogg | OGG/footstep01.ogg | `313472dba31fd0c855376069fa368bb5a198c27251cc8398ef464578b7047a4c` |

## Validation and remaining review

Current native evidence was read from the actual reports after the root implementation pass completed the tests. These results apply to this presentation source cut; they do not validate an older APK or certify a store release.

| Check | Actual result | Evidence |
| --- | --- | --- |
| Runtime source compilation | 0 errors, 31 warnings | Artifacts/Recovery/compile-runtime.txt |
| Editor source compilation | 0 errors, 1 warning | Artifacts/Recovery/compile-editor.txt |
| Imported audio/resource/provenance audit | 5 passed, 0 failed | Artifacts/Recovery/presentation-static-audit.txt |
| Native audio, UI and lifecycle fixture | 43 passed, 0 failed | Artifacts/Recovery/presentation-feedback-playtest.txt |
| Settings capture | 1280 x 720 editor image; inspected after caption repair | Artifacts/Recovery/presentation-settings.png |
| Audio attribution capture | 1280 x 720 editor image; inspected | Artifacts/Recovery/presentation-audio-credits.png |

The five static checks cover all 20 original imported OGG hashes/headers and complete Vorbis page chains; original selected Kenney bytes match their primary submission archive members. All 18 event IDs are unique and complete, and all 21 profile clip references resolve. Both music clips use streaming stereo imports without preload. The legacy result facade no longer creates synthesized AudioClips. Urban Theme is 87.652 seconds; Espionage is 76 seconds; event cues are 0.043 to 1.375 seconds.

`PresentationFeedbackPlaytest.Begin` / `BeginBatch` completed **43 passed, 0 failed**. It uses Artifacts/Recovery/presentation-save.json, backs up/restores touched preferences and does not rewrite the editor scene. Its actual native checks cover:

- Real traffic components recovering deliberately lost interface/renderer caches while the existing interpolation sample advances; disabled/destroyed parts, inactive/destroyed targets and an invalid route remain safe. This is a controlled cache-loss regression, not proof that an entire GPU traffic system survives arbitrary live editor recompilation.
- Required/existing CanvasGroups, real-frame opacity progression, deliberate missing-component cache recovery and no matching CanvasGroup lifecycle exceptions during that check.
- Imported cue/music references, one persistent audio owner, two music sources and eight effects voices, existing campaign volume fallback, separate real slider/mute callbacks, immediate stadium mute and durable native volume values.
- Resized authored Save & Close and Performance captions centered and contained inside their actual button rectangles; decorative press/release animation with fixed interactive geometry; reduced motion.
- One hundred same-frame combat/reward events respecting voice/burst bounds, non-raycast success decorations, bounded expiry and a real two-clip music crossfade that retires the outgoing cue.
- Public live settings callbacks, complete backdrop coverage and centered panel geometry at logical 20:9 and 4:3 frame sizes, world-command guarding, bounded dismissal release, explicit-pause preservation, in-app credits and preference/audio-owner preservation across an actual menu reload.

The latest settings image visibly shows Save & Close centered on its resized red button and Performance text centered in its shorter row. Separate music/effects values, mutes, movement pace, reduced motion and Audio Credits remain legible in the inspected editor capture. The credits image visibly retains both new CC0 authors/source/license text and Gregor Quendel's existing CC BY 4.0 author/source/license attribution. These are editor captures, not physical-phone screenshots.

Prior native results are retained for audit: **37 passed, 0 failed** before the caption fix at Artifacts/Recovery/presentation-before-label-fix.txt, and **38 passed, 0 failed** before the CanvasGroup fix at Artifacts/Recovery/presentation-before-panel-group-fix.txt. The latter assertions did not detect the subsequent CanvasGroup exceptions found in editor logs. The current 43-check run includes the added real group/cache/exception checks and supersedes that earlier presentation evidence.

Listening approval, music taste, phone speaker/headphone auditions, phone audio focus/interruption behavior and Android/iOS performance remain **not performed**. An editor AudioSource or opacity-state check does not establish audible hardware playback, physical-device usability, client acceptance or production-release readiness. No Unity test was launched by this delegated authoring task; the root implementation pass ran the native suite.
