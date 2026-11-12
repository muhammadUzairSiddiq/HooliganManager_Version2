param([switch]$UsePreparedWorkspace,[switch]$TrackingPolish,[switch]$GuidedOperations)
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$taskCopy=Join-Path $taskRoot '.utmp/review-build-workspace'
$taskArtifacts=Join-Path $taskRoot 'Artifacts/Recovery'
$taskUnity='C:\Users\User\Desktop\D drive Data\Unity Game Engine\6000.0.80f1\Editor\Unity.exe'
$taskBuildApkName=if($TrackingPolish-or$GuidedOperations){'Hooligan-RTS-Tracking-Polish-Review.apk'}else{'Hooligan-RTS-Recovery-Review.apk'}
$taskBuildReportName=if($TrackingPolish-or$GuidedOperations){'tracking-android-build.txt'}else{'android-build.txt'}
$taskApkName=if($GuidedOperations){'Hooligan-RTS-Guided-Operations-Review.apk'}else{$taskBuildApkName}
$taskReportName=if($GuidedOperations){'guided-android-build.txt'}else{$taskBuildReportName}
$taskLogName=if($GuidedOperations){'guided-android-isolated-build.log'}elseif($TrackingPolish){'tracking-android-isolated-build.log'}else{'android-isolated-build.log'}
$taskProofName=if($GuidedOperations){'guided-isolated-build-source-check.json'}elseif($TrackingPolish){'tracking-isolated-build-source-check.json'}else{'isolated-build-source-check.json'}
$taskMethod=if($TrackingPolish-or$GuidedOperations){'RecoveryBuild.AndroidTrackingPolish'}else{'RecoveryBuild.Android'}
if(-not (Test-Path -LiteralPath $taskUnity)){throw 'Update the Unity editor path in this script for this workstation.'}
function Assert-ReviewWorkspaceStopped {
    $copyPath=$taskCopy.Replace('/','\')
    $editors=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
        $_.CommandLine-and $_.CommandLine.Replace('/','\').IndexOf($copyPath,[StringComparison]::OrdinalIgnoreCase)-ge 0
    })
    if($editors.Count){throw ('Isolated workspace is still open in Unity PID '+($editors.ProcessId-join ', ')+'. Stop that editor before copying or building.')}
}
function Protect-ReviewIlppPid {
    Assert-ReviewWorkspaceStopped
    $pidFile=Join-Path $taskCopy 'Library/ilpp.pid'
    if(-not (Test-Path -LiteralPath $pidFile)){return}
    $runnerPid=0
    $pidText=(Get-Content -LiteralPath $pidFile -Raw).Trim()
    if([int]::TryParse($pidText,[ref]$runnerPid)-and $runnerPid-gt 0) {
        if(Get-Process -Id $runnerPid -ErrorAction SilentlyContinue){
            throw ('Isolated Library/ilpp.pid references live PID '+$runnerPid+'. Refusing to touch it or launch an editor that could kill another project''s runner.')
        }
    }
    $quarantine=Join-Path (Split-Path -Parent $pidFile) ('ilpp.pid.stale-'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff'))
    Move-Item -LiteralPath $pidFile -Destination $quarantine
    Write-Output ('Quarantined stale isolated ILPP PID file as '+(Split-Path -Leaf $quarantine))
}
Assert-ReviewWorkspaceStopped
New-Item -ItemType Directory -Path $taskArtifacts -Force | Out-Null
if(-not $UsePreparedWorkspace) {
    foreach($leaf in @('Assets','ProjectSettings','Packages','Library')) {
        $cacheExclusions=if($leaf-eq 'Library'){@('/XD','Bee')}else{@()}
        & robocopy (Join-Path $taskRoot $leaf) (Join-Path $taskCopy $leaf) /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /MT:16 /NFL /NDL /NP /NJH /NJS @cacheExclusions /XF EditorInstance.json ilpp.pid
        if($LASTEXITCODE-ge 8){throw ('Isolated copy failed for '+$leaf)}
    }
}
Protect-ReviewIlppPid
function Get-ReviewSha256([string]$Path,[long]$Length=-1) {
    if($Length-lt 0){$Length=([IO.FileInfo]::new($Path)).Length}
    # ComputeHash consumes small chunks internally. A large sequential FileStream
    # buffer coalesces those reads instead of issuing thousands of 4 KB disk I/Os.
    # Tiny metadata files need only their own size (with a 4 KB minimum).
    $bufferSize=[int][Math]::Min(1MB,[Math]::Max(4KB,$Length))
    $stream=[IO.FileStream]::new($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read,$bufferSize,[IO.FileOptions]::SequentialScan)
    $sha=$null
    try {
        $sha=[Security.Cryptography.SHA256]::Create()
        return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')
    }
    finally {
        if($sha){$sha.Dispose()}
        $stream.Dispose()
    }
}
function Get-ReviewInputs([string]$Project) {
    $inputs=[ordered]@{}
    # Include imported art/audio and their import settings, not just C# timestamps.
    # Enumerate this project independently so stale/missing copy files are detected.
    $files=@(foreach($leaf in @('Assets','ProjectSettings','Packages')) {
        Get-ChildItem -LiteralPath (Join-Path $Project $leaf) -Recurse -File -Force
    })
    foreach($file in ($files|Sort-Object FullName -Unique)) {
        $relative=$file.FullName.Substring($Project.Length+1).Replace('\','/')
        $inputs[$relative]=Get-ReviewSha256 $file.FullName $file.Length
    }
    return $inputs
}
function Assert-ReviewInputs($Expected,$Actual) {
    if($Expected.Count-ne $Actual.Count){throw ('Build input file counts differ: '+$Expected.Count+' versus '+$Actual.Count)}
    foreach($relative in $Expected.Keys) {
        if(-not $Actual.Contains($relative)-or $Actual[$relative]-ne $Expected[$relative]){throw ('Build inputs differ: '+$relative)}
    }
    foreach($relative in $Actual.Keys) {
        if(-not $Expected.Contains($relative)){throw ('Unexpected build input: '+$relative)}
    }
}
$before=Get-ReviewInputs $taskRoot
Assert-ReviewInputs $before (Get-ReviewInputs $taskCopy)
New-Item -ItemType Directory -Path (Join-Path $taskCopy 'Artifacts/Recovery') -Force | Out-Null
$taskLog=Join-Path $taskArtifacts $taskLogName
$taskStarted=[DateTime]::UtcNow
Write-Output ('Starting isolated Android batch build at '+[DateTime]::Now.ToString('HH:mm:ss'))
$taskArgs=@('-batchmode','-nographics','-projectPath',('"'+$taskCopy+'"'),'-executeMethod',$taskMethod,'-quit','-logFile',('"'+$taskLog+'"'))
$taskOldLedger=$env:HM_RELEASE_VERSION_LEDGER
try {
    $env:HM_RELEASE_VERSION_LEDGER=Join-Path $taskRoot 'UserSettings/HooliganReleaseVersions.txt'
    $taskProcess=Start-Process -FilePath $taskUnity -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
}
finally { $env:HM_RELEASE_VERSION_LEDGER=$taskOldLedger }
# Wait for this editor, not an optional long-lived Gradle daemon child.
$taskProcess.WaitForExit()
if($taskProcess.ExitCode-ne 0){throw ('Isolated Unity build exited '+$taskProcess.ExitCode+'; inspect '+$taskLogName)}
$taskReport=Join-Path (Join-Path $taskCopy 'Artifacts/Recovery') $taskBuildReportName
if(-not (Test-Path -LiteralPath $taskReport)-or (Get-Item -LiteralPath $taskReport).LastWriteTimeUtc-lt $taskStarted){throw 'No fresh isolated build report.'}
$taskBody=Get-Content -LiteralPath $taskReport -Raw
if(-not $taskBody.Contains('Result: Succeeded')-or -not $taskBody.Contains('Errors: 0')){throw $taskBody}
Assert-ReviewInputs $before (Get-ReviewInputs $taskRoot)
Assert-ReviewInputs $before (Get-ReviewInputs $taskCopy)
New-Item -ItemType Directory -Path (Join-Path $taskRoot 'Artifacts/Builds') -Force | Out-Null
$taskBuiltApk=Join-Path (Join-Path $taskCopy 'Artifacts/Builds') $taskBuildApkName
if(-not (Test-Path -LiteralPath $taskBuiltApk)-or (Get-Item -LiteralPath $taskBuiltApk).LastWriteTimeUtc-lt $taskStarted){throw 'No fresh isolated APK.'}
Copy-Item -LiteralPath $taskBuiltApk -Destination (Join-Path (Join-Path $taskRoot 'Artifacts/Builds') $taskApkName) -Force
Copy-Item -LiteralPath $taskReport -Destination (Join-Path $taskArtifacts $taskReportName) -Force
[ordered]@{utc=[DateTime]::UtcNow.ToString('O');workspace=$taskCopy;method='isolated Unity batch mode';execute_method=$taskMethod;apk=$taskApkName;build_report=$taskReportName;source_input_scope='All files in Assets, ProjectSettings and Packages, including import metadata';source_inputs_sha256=$before;source_and_copy_unchanged=$true} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskArtifacts $taskProofName) -Encoding UTF8
Write-Output $taskBody
Write-Output ('Verified '+$before.Count+' unchanged source/configuration inputs and copied the completed APK.')
