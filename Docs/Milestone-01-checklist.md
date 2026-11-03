# Milestone 01 review checklist — 18 October 2026

Scope: landscape + cross-platform foundation, with a playable review build. Green ticks below identify implemented work and the evidence available; they do not replace physical-device acceptance testing.

## 1. Landscape presentation and core gameplay flow

- ✅ Main menu, dashboard and gameplay scenes are included in build settings; orientation is Landscape Left.
- ✅ NEW GAME now starts a fresh campaign rather than opening the mode selector and accidentally resuming a defeated save.
- ✅ Replacing a save requires the existing confirmation. KEEP CURRENT SAVE remains available; CONTINUE retains its resume behavior.
- ✅ Fresh campaigns reset previous missions, battle sessions, deployment and casualties while preserving account/audio preferences.
- ✅ Empty club presets receive at least one living, deployable crew member and £5,000. Higher configured starting resources are preserved. No rewarded ad is needed to start.

## 2. Landscape HUD

- ✅ Existing resource/heat strip, cash, objectives, squad selection, command interactions and minimap remain integrated.
- ✅ Cash, resources and heat are visible by default; the existing PANEL control can collapse them.
- ✅ Shared overlay scaling fits both width and height instead of cropping fixed-width content on narrower landscape screens.
- ✅ Minimap and gameplay HUD use the same safe-area-aware scale.
- ✅ New-game and overwrite labels remain explicit instead of being shortened to one word.

## 3. Touch camera, selection and RTS controls

- ✅ Existing touch camera, selection, drag selection and RTS gesture controllers remain wired into gameplay; their gameplay logic is unchanged in this pass.
- ✅ Live new-game checks load the camera and selection controllers, select the starter crew and focus the camera on it.
- ✅ Existing RTS playtest suite: **93 passed, 0 failed**, including movement, selection, guard/patrol, camera behavior, local choices and minimap layers. Report: `Artifacts/Milestone01/rts-controls-playtest.txt`.
- ⬜ Physical-device acceptance: pan, pinch zoom, tap selection, drag selection, movement/attack, guard/patrol and UI-touch blocking on Android and iOS.

## 4. Shared Android/iOS configuration and safe areas

- ✅ Shared Landscape Left orientation; portrait is disabled. Android minimum API 25 and iOS minimum version 15.0 are configured.
- ✅ Safe-area and display-size changes trigger layout updates. Unchanged display metrics skip redundant viewport transform writes.
- ✅ Automated layout checks cover 1280×720, 1920×1080, 2400×1080, 2532×1170, 2960×1440 and 1024×768, each with and without simulated notch/home-indicator insets.
- ⬜ Validate actual notches, home indicators, readable touch targets and sustained performance on target devices. Consistent proportional sizing is implemented; identical physical size across all devices is not asserted.

## 5. Playable test build and evidence

- ✅ Isolated Unity regression checks reproduce an empty/defeated campaign and verify fresh crew, starting cash, save/reload, cleared campaign progress and retained preferences.
- ✅ Live Unity checks exercise the actual New Game button and overwrite confirmation from a defeated save, then verify living crew on the gameplay navigation mesh.
- ✅ Gameplay screenshots are saved at 1280×720, 2400×1080 and 1024×768 in `Artifacts/Milestone01/`.
- ⬜ Android APK build result: see delivery note below.
- ⬜ iOS build and device test remain pending: this Windows Unity installation has no iOS build-support module or Xcode signing environment.

Evidence: `Artifacts/Milestone01/regression-checks.txt`, `Artifacts/Milestone01/playtest-checks.txt`, and the gameplay PNGs. Re-run through the Unity editor bridge with `milestone-one-checks` and `milestone-one-play`; these use isolated save files.

The screenshot harness restores normal game speed after capture. The existing RTS test was updated for the visible-by-default resource panel and to cache enemy position/avatar information before testing destruction; neither test adjustment changes combat behavior. UI optimization here is reduced redundant layout work, not a measured device FPS improvement.

## Device acceptance steps

1. Install the updated APK. With an existing defeated save, choose NEW GAME, confirm START NEW GAME, and verify home gameplay loads with living crew and at least £5,000.
2. Return to the menu and select CONTINUE. Verify the same campaign resumes without resetting progress or adding starter resources again.
3. Start another new campaign, choose KEEP CURRENT SAVE, and verify no progress changes.
4. Check the full resource strip, cash, heat, objectives, squad controls, minimap and pause control at each target phone/tablet size. Open and close PANEL and both side rails.
5. Exercise touch camera and RTS gestures, then open recruitment, mission, pause and confirmation panels. Confirm all controls stay inside the usable screen and world input does not leak through dialogs.
6. Complete Android and iOS device checks before milestone approval. No later-milestone economy or rewarded-ad implementation is claimed here.

## Android delivery

Build pending at checklist creation; update this section with the final build result before delivery.
