param([string]$StartAt='presentation-feedback-playtest',[string[]]$Only=@())
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$taskCopy=Join-Path $taskRoot '.utmp/review-build-workspace'
$taskUnity='C:\Users\User\Desktop\D drive Data\Unity Game Engine\6000.0.80f1\Editor\Unity.exe'
$taskLogLeaf=if($Only.Count-eq 1-and $Only[0]-eq 'guided-tutorial-playtest'){'guided-isolated-native.log'}else{'tracking-isolated-native.log'}
$taskLog=Join-Path $taskRoot ('Artifacts/Recovery/'+$taskLogLeaf)
function Assert-ReviewWorkspaceStopped {
    $copyPath=$taskCopy.Replace('/','\')
    $editors=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
        $_.CommandLine-and $_.CommandLine.Replace('/','\').IndexOf($copyPath,[StringComparison]::OrdinalIgnoreCase)-ge 0
    })
    if($editors.Count){throw ('Isolated workspace is still open in Unity PID '+($editors.ProcessId-join ', ')+'. Stop that editor before copying or testing.')}
}
function Protect-ReviewIlppPid {
    Assert-ReviewWorkspaceStopped
    $pidFile=Join-Path $taskCopy 'Library/ilpp.pid'
    if(-not(Test-Path -LiteralPath $pidFile)){return}
    $runnerPid=0;$pidText=(Get-Content -LiteralPath $pidFile -Raw).Trim()
    if([int]::TryParse($pidText,[ref]$runnerPid)-and $runnerPid-gt 0-and(Get-Process -Id $runnerPid -ErrorAction SilentlyContinue)) {
        throw ('Isolated Library/ilpp.pid references live PID '+$runnerPid+'. Refusing to touch it or launch an editor that could kill another project''s runner.')
    }
    $quarantine=Join-Path (Split-Path -Parent $pidFile) ('ilpp.pid.stale-'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff'))
    Move-Item -LiteralPath $pidFile -Destination $quarantine
    Write-Output ('Quarantined stale isolated ILPP PID file as '+(Split-Path -Leaf $quarantine))
}
function Read-IsolatedStatus {
    $reader=[IO.File]::Open($taskStatus,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite-bor[IO.FileShare]::Delete))
    try{$textReader=[IO.StreamReader]::new($reader);try{return $textReader.ReadToEnd()}finally{$textReader.Dispose()}}finally{$reader.Dispose()}
}
function Close-IsolatedEditorWhenIdle {
    if($taskProcess.HasExited){return}
    $until=(Get-Date).AddSeconds(75)
    while((Test-Path -LiteralPath $taskCommand)-and(Get-Date)-lt $until){Start-Sleep -Seconds 1}
    if(Test-Path -LiteralPath $taskCommand){Write-Warning 'Isolated editor still has a pending command; leaving it untouched.';return}
    $sent=[DateTime]::UtcNow;[IO.File]::WriteAllText($taskCommand,'editor-state')
    while((Get-Date)-lt $until-and-not $taskProcess.HasExited) {
        if((Test-Path -LiteralPath $taskStatus)-and(Get-Item -LiteralPath $taskStatus).LastWriteTimeUtc-ge $sent) {
            $status=Read-IsolatedStatus
            if($status.Contains('Playing=False')) {
                if(Test-Path -LiteralPath $taskCommand){Start-Sleep -Seconds 1;continue}
                [IO.File]::WriteAllText($taskCommand,'quit-editor')
                if(-not $taskProcess.WaitForExit(60000)){Write-Warning 'Isolated editor acknowledged edit mode but did not quit.'}
                return
            }
            if($status.Contains('Playing=True')-and-not(Test-Path -LiteralPath $taskCommand)) {
                Start-Sleep -Seconds 2;$sent=[DateTime]::UtcNow;[IO.File]::WriteAllText($taskCommand,'editor-state')
            }
        }
        Start-Sleep -Seconds 1
    }
    if(-not $taskProcess.HasExited){Write-Warning 'Isolated editor did not acknowledge edit mode; no quit was requested.'}
}
Assert-ReviewWorkspaceStopped
Protect-ReviewIlppPid
foreach($leaf in @('Assets','ProjectSettings','Packages')) {
    & robocopy (Join-Path $taskRoot $leaf) (Join-Path $taskCopy $leaf) /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /MT:16 /NFL /NDL /NP /NJH /NJS
    if($LASTEXITCODE-ge 8){throw ('Native copy failed for '+$leaf)}
}
$taskCommands=Join-Path $taskCopy '.utmp'
New-Item -ItemType Directory -Path $taskCommands -Force | Out-Null
$taskCommand=Join-Path $taskCommands 'landscape-command.txt'
if(Test-Path -LiteralPath $taskCommand){Move-Item -LiteralPath $taskCommand -Destination ($taskCommand+'.stale-'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff'))}
$taskStatus=Join-Path $taskCopy 'Artifacts/LandscapeUI/editor-status.txt'
[IO.File]::WriteAllText($taskCommand,'prepare-presentation-assets')
$taskStarted=[DateTime]::UtcNow
$taskArgs=@('-batchmode','-force-d3d11','-projectPath',('"'+$taskCopy+'"'),'-logFile',('"'+$taskLog+'"'))
$taskProcess=Start-Process -FilePath $taskUnity -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
Write-Output ('Started isolated native editor PID '+$taskProcess.Id)
try {
    $until=(Get-Date).AddMinutes(12);$ready=$false
    while((Get-Date)-lt $until) {
        if($taskProcess.HasExited){throw ('Isolated editor exited '+$taskProcess.ExitCode)}
        if((Test-Path -LiteralPath $taskStatus)-and(Get-Item -LiteralPath $taskStatus).LastWriteTimeUtc-ge $taskStarted) {
            $status=Read-IsolatedStatus
            if($status.Contains('ERROR')){throw $status}
            if($status.Contains('OK prepare-presentation-assets')){$ready=$true;break}
        }
        Start-Sleep -Seconds 2
    }
    if(-not $ready){throw 'Isolated editor asset preparation timed out.'}
    # The isolated editor generates the canonical wrappers. Preserve their GUIDs
    # in the original workspace, where no scene or test is being manipulated.
    foreach($resourceFolder in @('HomeFacilities','CityIncidents')) {
        $from=Join-Path $taskCopy ('Assets/Resources/'+$resourceFolder);$to=Join-Path $taskRoot ('Assets/Resources/'+$resourceFolder)
        if(Test-Path -LiteralPath $from) {
            New-Item -ItemType Directory -Path $to -Force | Out-Null
            & robocopy $from $to /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /NFL /NDL /NP /NJH /NJS
            if($LASTEXITCODE-ge 8){throw ('Generated resource copy failed for '+$resourceFolder)}
            if((Test-Path -LiteralPath ($from+'.meta'))-and-not(Test-Path -LiteralPath ($to+'.meta'))){Copy-Item -LiteralPath ($from+'.meta') -Destination ($to+'.meta')}
        }
    }
    foreach($leaf in @('CrewRoute.mat','CrewRoute.mat.meta')) {
        Copy-Item -LiteralPath (Join-Path $taskCopy ('Assets/Resources/Presentation/'+$leaf)) -Destination (Join-Path $taskRoot ('Assets/Resources/Presentation/'+$leaf)) -Force
    }
    Write-Output 'Prepared visible Home models, CityIncidents models and route material with canonical GUIDs.'
    Push-Location $taskCopy
    try { & (Join-Path $taskRoot 'Tools/Recovery/RunNativeReview.ps1') -StartAt $StartAt -Only $Only }
    finally { Pop-Location }
    Write-Output 'Isolated native review finished.'
}
finally {
    Close-IsolatedEditorWhenIdle
    foreach($leaf in @('Recovery','CityQA','Milestone01')) {
        $from=Join-Path $taskCopy ('Artifacts/'+$leaf);$to=Join-Path $taskRoot ('Artifacts/'+$leaf)
        if(Test-Path -LiteralPath $from){
            & robocopy $from $to /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /NFL /NDL /NP /NJH /NJS /XO
            if($LASTEXITCODE-ge 8){Write-Warning ('Could not copy all '+$leaf+' native evidence.')}
        }
    }
    if(-not $taskProcess.HasExited){Write-Warning ('Isolated editor remains PID '+$taskProcess.Id+'; do not start a second process against that workspace.')}
}
