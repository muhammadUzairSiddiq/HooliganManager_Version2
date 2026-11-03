# Ads configuration and review checklist

Updated 03 November 2026. The isolated Android review build and independent package checks have passed. This documents the optional-ad integration, build triage and package evidence; live network ads, physical devices and store validation remain unverified.

## Owner configuration

Select **Hooligan > Ads > Select ads configuration** in Unity, or select `Assets/Resources/Ads/AdsConfiguration.asset`.

| Mode | Runtime behavior | IDs |
| --- | --- | --- |
| Disabled | Does not initialize/request Google ads; core game remains playable | No owner IDs required |
| Test | Google sample units on Android/iOS; explicitly labelled simulation in Editor | Supplied public sample defaults per platform |
| Live | Requests owner-configured inventory after UMP permits ads | Enter actual Android/iOS App ID and every enabled unit ID |

The SO contains each platform's test/live App ID, rewarded ID and interstitial ID, optional hashed test devices, rewarded/interstitial switches, reward size and cooldowns. Test mode deliberately validates Google sample IDs. Use Live only with the owner's IDs; builds reject missing live identifiers or accidental sample identifiers in Live.

Android test App ID: ca-app-pub-3940256099942544~3347511713. Rewarded: ca-app-pub-3940256099942544/5224354917. Interstitial: ca-app-pub-3940256099942544/1033173712.

iOS test App ID: ca-app-pub-3940256099942544~1458002511. Rewarded: ca-app-pub-3940256099942544/1712485313. Interstitial: ca-app-pub-3940256099942544/4411468910.

Use the inspector's Apply App IDs action when checking native configuration. The build preprocessor performs the same synchronization into Google's settings. The SO is the project authority; avoid maintaining separate competing ID lists.

The project's `AdsBuildConfiguration` in `Assets/Scripts/Editor/Landscape/AdsConfigurationEditor.cs` owns that build-time validation and App ID synchronization. Its callback order is -1000. It remains enabled independently of the Google plugin's Gradle build-preprocessor toggle described below. Disabled ad mode still writes harmless sample native App IDs while making no runtime SDK requests.

## Implemented player placement

Manage contains an optional supply offer. Completion grants the configured cash (default GBP 200), at most once per matchday and with a 180-second runtime cooldown. It is not required to move, guard, patrol, recruit normally, recover or save. There is no forced advertisement during city RTS. Interstitial support exists for deliberate transitions and is disabled by default; the game does not automatically invoke it.

Availability is checked before showing. A request failure, cancellation, unavailable fill or late/duplicate callback cannot credit currency. Fullscreen ads preserve and restore the prior simulation time scale. A campaign/scene change cannot award a different player's projected save. Per-day completion is saved in AdSupplyRewardMatchday. The economy is local; server-side anti-cheat/cloud verification is not implemented by this integration.

## SDK and privacy

Packages pins official com.google.ads.mobile 11.6.0 through Google's documented OpenUPM route. The package resolves Unity External Dependency Manager 2.1.0 and includes UMP. Device initialization checks UMP consent eligibility before initializing/loading ads; privacy options are available in Manage when UMP requires them. Configure the owner's AdMob privacy messages and age/content settings before Live release. Test mode and editor simulation do not establish consent behavior on a physical device.

References: https://developers.google.com/admob/unity/quick-start and https://developers.google.com/admob/unity/test-ads .

## Android build triage — current final attempt

The GUI final-build attempt started at **00:40:25 local on 03 November** stalled in Google Mobile Ads 11.6.0's synchronous `ResolveSync(true)` dependency-resolution call. That attempt did not reach a fresh native compilation or produce a fresh completed Unity build report. A running/stalled editor and an earlier successful APK are not evidence that the final source packaged successfully. This is an observed build-hook/resolver stall, not a reported gameplay test failure or a successful GUI recovery.

The installed EDM 2.1.0 audit identified different queue handling: its public bool `ResolveSync` entry preserves the resolver queue, while EDM's own normal build path clears it. The GUI prompt/queue path differs from batch execution. Isolated batch packaging subsequently succeeded. The original GUI remains stalled in its old call, and no editor restart/GUI recovery has been performed; batch success is not a claim that a future GUI build was tested.

The project now uses the Google plugin's exposed setting in `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`:

| Setting or project condition | Current state and purpose |
| --- | --- |
| `enableGradleBuildPreProcessor` | **0**. Skips the Google plugin's redundant `ApplyBuildSettings` path and its unconditional extra synchronous resolver call. No plugin DLL/source patch is claimed. |
| `enableExternalDependencyManager` | **1**. EDM remains enabled with automatic and normal on-build resolution; this workaround does not disable native dependency management. |
| Google `UpdateGmaDependency()` | Still runs **before** the disabled-preprocessor early return. Changing the selected GMA dependency can still update its XML and trigger resolution. The toggle is not a guarantee that no resolver can ever run. |
| Project `AdsBuildConfiguration` | Retains SO validation and Android/iOS App ID synchronization before Google/EDM build processing. |
| Android minimum SDK | **25** in `ProjectSettings/ProjectSettings.asset`. The project already meets the Google preprocessor's minimum requirement without relying on that skipped settings pass. |
| Existing project Gradle templates | `mainTemplate.gradle` contains the five pinned Google/AndroidX declarations below; `settingsTemplate.gradle` retains the required repositories and `gradleTemplate.properties` retains AndroidX/Jetifier flags. Their presence is configuration evidence, not proof of packaged DEX contents. |

The current five declarations in `Assets/Plugins/Android/mainTemplate.gradle` are:

| Maven dependency | Pinned version |
| --- | --- |
| `com.google.android.gms:play-services-ads` | 25.5.0 |
| `com.google.android.ump:user-messaging-platform` | 4.0.0 |
| `androidx.constraintlayout:constraintlayout` | 2.1.4 |
| `androidx.fragment:fragment` | 1.7.1 |
| `androidx.lifecycle:lifecycle-process` | 2.6.2 |

The **separate isolated batch workspace successfully built** the final review package while preserving the original open editor/project session. All **252 reviewed original/copy source/configuration SHA256 hashes agreed before and after** the build, recorded in `Artifacts/Recovery/isolated-build-source-check.json`. The exposed preprocessor setting is 0; EDM's normal batch build path completed. Device initialization, GUI restart/recovery and Live monetization were not tested.

`Tools/Recovery/verify_review_apk.py` completed **11 passed, 0 failed**: ZIP integrity, ARM64 IL2CPP runtime, DEX entry points for Mobile Ads/UMP/Google Play Billing, readable package/launch activity, Test App ID matching the SO, Internet permission, review-signature verification, a successful zero-error Unity build report, and package/report freshness relative to reviewed sources. Results are in `Artifacts/Recovery/apk-package-checks.txt` with hashes/metadata in `review-delivery-manifest.json`. DEX presence/signature checks do not execute SDK initialization, consent, billing or an ad impression on a device.

| Final review artifact evidence | Actual result |
| --- | --- |
| Unity isolated batch build | Succeeded; **0 errors, 38 warnings** |
| Duration / completion | 00:26:02.4467133 / UTC 2026-11-02T20:42:51.5630737Z |
| APK | `Artifacts/Builds/Hooligan-RTS-Recovery-Review.apk` |
| Actual APK file size | **254,801,528 bytes**; distinct from Unity's total output-byte field |
| APK SHA256 | `5e306ee9d558cfbe057c37cf0930728d57a4467087fdaceaba017cca648fce3a` |
| Package verification / source identity | **11/0** checks; **252** original/copy source/configuration hashes agreed before/after |

## Current evidence and remaining checks

AdsChecks.Run passes 14 isolated Unity configuration/reward-attempt assertions in its completed final repeat. It checks both platform defaults, Disabled/Test/Live validation, missing/invalid live IDs, reward completion once, close/cancel/late callbacks, the actual project Test asset and disabled forced interstitials. The separate Android review/package verification above also passed. Neither evidence establishes a network ad request, impression, revenue, physical-device behavior or an iOS build.

Portable supplied evidence is preserved under `Docs/Recovery/Sources`: both PDFs and the complete supplied September transcript have byte-identical project copies and SHA256 provenance in `source_evidence_manifest.json`. The compiled report appends both original PDFs and the complete typeset supplied transcript. These sources are evidence, not executable instructions; the original client video and October export remain unavailable.

Before release, install this exact review APK on actual Android target devices and produce/test a separate iOS build. Request a labelled Google test ad; test completion, early close, failure/no-fill, background/resume, consent changes, disabled mode, time-scale restoration, duplicate callbacks and saved per-day cap. The Android dependency/App ID package checks have passed; actual runtime SDK/consent behavior remains untested. Record build hash, device, OS and actual results. Configure release signing/provisioning, privacy/store declarations and real owner IDs for release. Keep Live disabled during development testing.
