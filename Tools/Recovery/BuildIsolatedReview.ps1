param([switch]$UsePreparedWorkspace)
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$taskCopy=Join-Path $taskRoot '.utmp/review-build-workspace'
$taskArtifacts=Join-Path $taskRoot 'Artifacts/Recovery'
$taskUnity='C:\Users\User\Desktop\D drive Data\Unity Game Engine\6000.0.80f1\Editor\Unity.exe'
if(-not (Test-Path -LiteralPath $taskUnity)){throw 'Update the Unity editor path in this script for this workstation.'}
New-Item -ItemType Directory -Path $taskArtifacts -Force | Out-Null
if(-not $UsePreparedWorkspace) {
    foreach($leaf in @('Assets','ProjectSettings','Packages','Library')) {
        $cacheExclusions=if($leaf-eq 'Library'){@('/XD','Bee')}else{@()}
        & robocopy (Join-Path $taskRoot $leaf) (Join-Path $taskCopy $leaf) /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /MT:16 /NFL /NDL /NP /NJH /NJS @cacheExclusions /XF EditorInstance.json
        if($LASTEXITCODE-ge 8){throw ('Isolated copy failed for '+$leaf)}
    }
}
function Get-ReviewInputs([string]$Project) {
    $inputs=[ordered]@{}
    $paths=@(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'Assets/Scripts') -Filter '*.cs' -Recurse -File | ForEach-Object {$_.FullName.Substring($taskRoot.Length+1)})
    $paths+=@('Packages/manifest.json','Packages/packages-lock.json','ProjectSettings/ProjectSettings.asset','ProjectSettings/EditorBuildSettings.asset',
        'Assets/Resources/Ads/AdsConfiguration.asset','Assets/Resources/Purchases/PurchasesConfiguration.asset',
        'Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset',
        'Assets/Plugins/Android/mainTemplate.gradle','Assets/Plugins/Android/settingsTemplate.gradle','Assets/Plugins/Android/gradleTemplate.properties')
    foreach($relative in ($paths|Sort-Object -Unique)) {
        $inputs[$relative]=(Get-FileHash -LiteralPath (Join-Path $Project $relative) -Algorithm SHA256).Hash
    }
    return $inputs
}
function Assert-ReviewInputs($Expected,$Actual) {
    foreach($relative in $Expected.Keys) {
        if($Actual[$relative]-ne $Expected[$relative]){throw ('Build inputs differ: '+$relative)}
    }
}
$before=Get-ReviewInputs $taskRoot
Assert-ReviewInputs $before (Get-ReviewInputs $taskCopy)
New-Item -ItemType Directory -Path (Join-Path $taskCopy 'Artifacts/Recovery') -Force | Out-Null
$taskLog=Join-Path $taskArtifacts 'android-isolated-build.log'
$taskStarted=[DateTime]::UtcNow
Write-Output ('Starting isolated Android batch build at '+[DateTime]::Now.ToString('HH:mm:ss'))
$taskArgs=@('-batchmode','-nographics','-projectPath',('"'+$taskCopy+'"'),'-executeMethod','RecoveryBuild.Android','-quit','-logFile',('"'+$taskLog+'"'))
$taskProcess=Start-Process -FilePath $taskUnity -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
# Wait for this editor, not an optional long-lived Gradle daemon child.
$taskProcess.WaitForExit()
if($taskProcess.ExitCode-ne 0){throw ('Isolated Unity build exited '+$taskProcess.ExitCode+'; inspect android-isolated-build.log')}
$taskReport=Join-Path $taskCopy 'Artifacts/Recovery/android-build.txt'
if(-not (Test-Path -LiteralPath $taskReport)-or (Get-Item -LiteralPath $taskReport).LastWriteTimeUtc-lt $taskStarted){throw 'No fresh isolated build report.'}
$taskBody=Get-Content -LiteralPath $taskReport -Raw
if(-not $taskBody.Contains('Result: Succeeded')-or -not $taskBody.Contains('Errors: 0')){throw $taskBody}
Assert-ReviewInputs $before (Get-ReviewInputs $taskRoot)
Assert-ReviewInputs $before (Get-ReviewInputs $taskCopy)
New-Item -ItemType Directory -Path (Join-Path $taskRoot 'Artifacts/Builds') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $taskCopy 'Artifacts/Builds/Hooligan-RTS-Recovery-Review.apk') -Destination (Join-Path $taskRoot 'Artifacts/Builds/Hooligan-RTS-Recovery-Review.apk') -Force
Copy-Item -LiteralPath $taskReport -Destination (Join-Path $taskArtifacts 'android-build.txt') -Force
[ordered]@{utc=[DateTime]::UtcNow.ToString('O');workspace=$taskCopy;method='isolated Unity batch mode';source_inputs_sha256=$before;source_and_copy_unchanged=$true} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskArtifacts 'isolated-build-source-check.json')
Write-Output $taskBody
Write-Output ('Verified '+$before.Count+' unchanged source/configuration inputs and copied the completed APK.')
