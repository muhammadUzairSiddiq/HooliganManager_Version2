# Recovery verification

Read `Docs/AI_SCOPE_GUARDRAILS.txt` first. These tools never replace device testing.

The 05 November guided-operations cut adds `GuidedTutorialPlaytest.Begin`,
`PoliceOperationsPlaytest.Begin/BeginBatch` and
`RoadIncidentPlaytest.Begin/BeginBatch`. The tutorial fixture explicitly opts in
to exclusive first-use UI; older disposable gameplay fixtures suppress it through
their editor-only save override. Neither path changes a real campaign save.
Run the guide separately before broader checks to catch its real raycast/wait
failures. `RunNativeReview.ps1 -StartAt guided-tutorial-playtest -Only ...`
selects explicit suites. `RunIsolatedNativeReview.ps1` forwards these options and
preserves the original editor, including a game already in Play mode. Never
start a second editor/build on the isolated workspace while it is occupied.

After current verification, `BuildIsolatedReview.ps1 -GuidedOperations` writes
`Hooligan-RTS-Guided-Operations-Review.apk`, `guided-android-build.txt` and a
separate all-input provenance proof. It uses the same proven build method inside
the clone and copies to new delivery identities; the previous root APKs/reports
are preserved. `verify_review_apk.py --guided-operations` checks this distinct
package and requires successful tutorial, Police operations and road reports in
addition to the broader evidence. An APK is a review build, not store approval.

The tracking/presentation pass starts with five new native suites: presentation
feedback (including traffic lifecycle recovery), visible Home models, tactical
map, production-speed crew tracking and train/rival raids. `RunNativeReview.ps1`
now runs these before affected regressions. Always obtain a clean runtime AND
editor compile before starting it; an old loaded editor assembly is not evidence
for new source. A pending presentation fixture resumes its original preference
backup if a compilation error prevented Play.

`RunIsolatedNativeReview.ps1` can run the same bridge sequence in the separate
review workspace without closing the user's editor. It prepares the canonical
Home resource wrappers, copies their GUID-preserving files back, and copies fresh
reports/captures back after the sequence. Do not run it concurrently with a build
on that copy. Its failed-startup log must not be mistaken for test results.

Use `BuildIsolatedReview.ps1 -TrackingPolish` and then
`verify_review_apk.py --tracking-polish` for the new, separately named tracking
APK and evidence. The earlier Recovery APK/report/manifest stay unchanged.
Provenance now checks all Assets/import metas, ProjectSettings and Packages in
both directions. Library PID files are excluded: copying an active ILPP PID into
another project can cause Unity to kill the original project's compiler worker.

Crew telemetry reports editor viewport, actual actor/system counts, raw frame
samples and median/p95 only if its bounded sample completes. It is not Android
profiling, a full-city stress result or a release performance gate.

An interrupted build can leave Unity Performance Testing's generated
`Assets/Resources/PerformanceTestRunInfo.json` and
`PerformanceTestRunSettings.json` plus their two metas in the isolated copy.
The installed package's `Editor/TestRunBuilder.cs` creates them in
OnPreprocessBuild and deletes them in OnPostprocessBuild. They are not authored
project inputs. If the strict pre-build file-count check finds these four extras,
first confirm the original workspace has none and no isolated editor is running;
quarantine only those exact leftover files under the copy's `.utmp`, then repeat
the normal check. Do not exclude arbitrary assets or weaken the input comparison.
Successful postprocessing must restore the exact authored input set.

The latest production pass adds `ProductionStrategyPlaytest.BeginBatch`,
`PoliceVehiclePlaytest.BeginBatch`, `CommercePlaytest.BeginBatch` and
`PurchasesChecks.Run`. Their current reports are respectively
`production-strategy-playtest.txt`, `police-vehicle-playtest.txt`,
`commerce-playtest.txt` and `purchases-checks.txt` under `Artifacts/Recovery`.
Production strategy uses five durable fast crew, accelerated time, suppressed
unrelated pressure and seeded later preparation. It freezes the phase timer to
isolate allocation, then resumes the actual clock through aftermath. Commerce
seeds the completed Home chapter to exercise actual planner/deployment/return
and district revisit. Neither fixture is a naturally played whole campaign.

With this project's Unity editor already open and idle, run
`Tools/Recovery/RunNativeReview.ps1 -BuildAndroid` from the project root. It uses
the project-local bridge, waits for completed reports and restored edit mode,
and stops at any failure. It does not close or save the user's open scene. Do
not run a second Unity instance against the same project. The fixtures change
scenes only inside Play mode, using disposable saves.

After the final APK is built, run `verify_review_apk.py` with the bundled Python.
It independently checks ZIP integrity, ARM64 runtime, launchable manifest, test
App ID, signature, and build/source freshness. It records actual APK bytes and
SHA256 in `review-delivery-manifest.json`. A signed review APK does not certify
store credentials, network advertisements, payment checkout or physical devices.

If the open editor stalls in Google Mobile Ads' synchronous native dependency
resolver, use `Tools/Recovery/BuildIsolatedReview.ps1` in PowerShell. It copies
Assets, Packages, ProjectSettings and the import cache into a separate project
under `.utmp/review-build-workspace`, then invokes Unity in batch mode. It does
not close the original editor. It requires enough disk space for a real copy;
the two projects must not share a writable Library directory. Source/configuration
hashes must agree before and after the build. Only a successful fresh package is
copied back, with evidence in `isolated-build-source-check.json` and
`android-isolated-build.log`. `-UsePreparedWorkspace` is for an already completed
copy, and still performs the same hash checks. This workstation's Unity path is
configured at the top of the script. The GUI resolver stall is separate from
gameplay/native test results and cannot be represented as a successful GUI build.

Release Player builds increment the patch version and Android/iOS build
number. Development builds keep the current values. Isolated review builds
reserve from `UserSettings/HooliganReleaseVersions.txt` in the main workspace,
then restore the isolated copy's settings. Keep that counter when reusing
the workspace for subsequent releases.

`compile_recovery.py` uses the installed Unity compiler and the project's cached
Bee response files to compile runtime and editor assemblies into
`Artifacts/Recovery/`. It does not overwrite the editor's loaded assemblies.
The configured Unity path and Bee artifact folder reflect this workstation;
update them for another installation or regenerate response files in Unity.

`run_recovery_rules.py` compiles and runs `RecoveryRulesHarness.cs` against the
actual newly compiled runtime assembly. The assertions cover learning,
migration, business economy, cooldowns, preparation and Home/Away resources.
They run without a Unity scene and do not exercise Unity's save serializer.

Run both scripts from the project root using the bundled Python executable.
Read `Artifacts/Recovery/compile-runtime.txt`, `compile-editor.txt` and
`rules-checks.txt` for actual results.

In Unity edit mode, use **Hooligan > Recovery > Run startup and save checks**.
This uses `SaveDataInLocal.EditorSaveOverride` and an isolated JSON file, restores
preferences/static handlers, and writes `Artifacts/Recovery/unity-save-checks.txt`.
The existing editor bridge also accepts `recovery-checks`.

Then run the existing RTS Control Playtest, M1 live new-game checks and the human
acceptance sequence in the recovery plan. The RTS suite now seeds an already
started matchday so it can test crowds/duties; the new rules explicitly check
that a fresh matchday waits for learning and preparation.

Native entry points can also run from the Unity command line with `-batchmode`,
`-projectPath` and `-executeMethod`. Stop other Unity instances for this project
first. Use `-quit` only for synchronous edit-mode checks/builds; playtests exit
through their batch callback after restoring the isolated save override.

| Entry point | Evidence |
| --- | --- |
| `RecoveryChecks.Run` | `Artifacts/Recovery/unity-save-checks.txt` |
| `MilestoneOnePlaytest.BeginBatch` | `Artifacts/Milestone01/playtest-checks.txt` and three aspect-ratio captures |
| `RedesignPlaytest.BeginBatch` | `Artifacts/Recovery/redesign-playtest.txt`; current Home construction, contextual HUD, recruitment and native-save checks |
| `PoliceCityPlaytest.BeginBatch` | `Artifacts/Recovery/police-playtest.txt`; current patrol, dispatch, detention, escort, booking, recovery and separate-save checks |
| `AdsChecks.Run` | `Artifacts/Recovery/ads-checks.txt`; actual SO validation and reward-callback rules, without network ads |
| `RtsControlPlaytest.BeginBatch` | `Artifacts/CityQA/rts-controls-playtest.txt` |
| `RecoveryCityPlaytest.BeginBatch` | `Artifacts/Recovery/city-integration-checks.txt`; retained earlier recovery fixture and its earlier UI captures |
| `RecoveryBuild.Android` | review APK in `Artifacts/Builds/` and `Artifacts/Recovery/android-build.txt` |

The earlier city integration fixture uses five durable, fast crew and suppresses ambient
pressure to isolate economy/coverage rules. It exercises actual actors, travel,
control, interruption, loot, contest, saves, matchday, deployment, extraction and
return. Later chapter prerequisites are deliberately seeded; it does not claim a
natural full campaign playthrough. The separate RTS suite exercises local AI,
guard/patrol, police neutrality, crowd conflicts and camera controls.

Current Home/Police fixture captures are `Artifacts/Recovery/redesign-home-world.png`,
`redesign-home-building.png`, `redesign-orders.png`, `redesign-management.png`
and `police-dispatch.png`. The `recovery-home.png`, `recovery-businesses.png`
and `recovery-matchday.png` captures belong to the earlier city integration
revision; they do not establish the current Home presentation. Actual starter
captures at three editor aspect ratios are under `Artifacts/Milestone01/`.

Screenshots require the editor Game View; the integration suite selects a fixed
1280x720 view. `WaitForEndOfFrame` must not be used in Editor batch-mode tests.
The watchdog writes a failure if a test stalls. Never interpret a RUNNING report
or an old screenshot as current passing evidence.

Device touch, profiling, long-session balance and client acceptance remain human
review gates. Android package compilation does not certify Android/iOS devices.


Expanded redesign entry points: RedesignPlaytest.BeginBatch (Home, contextual UI,
construction, recruitment and save), PoliceCityPlaytest.BeginBatch (patrol,
detention, escort, booking, saves and officer recovery), AdsChecks.Run (SO and
callback rules, without network ads). Reports are redesign-playtest.txt,
police-playtest.txt and ads-checks.txt under Artifacts/Recovery. RedesignAssets.Ensure
prepares existing city props as Home facility prefabs. The title chooses Hooligan
or Police; original RTS commands remain backend APIs. Older reports remain
historical evidence until a fresh run succeeds against the current revision.
