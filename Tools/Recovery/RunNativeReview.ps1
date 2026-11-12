param([switch]$BuildAndroid,[string]$StartAt='presentation-feedback-playtest',[string[]]$Skip=@(),[string[]]$Only=@())
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path '.').Path
$taskCommand=Join-Path $taskRoot '.utmp/landscape-command.txt'
$taskPending=Join-Path $taskRoot '.utmp/recovery-command-pending.txt'
$taskStatus=Join-Path $taskRoot 'Artifacts/LandscapeUI/editor-status.txt'
function Read-RecoveryText([string]$Path) {
  $file=[System.IO.File]::Open($Path,[System.IO.FileMode]::Open,[System.IO.FileAccess]::Read,([System.IO.FileShare]::ReadWrite-bor[System.IO.FileShare]::Delete))
  try { $reader=[System.IO.StreamReader]::new($file);try{return $reader.ReadToEnd()}finally{$reader.Dispose()} } finally { $file.Dispose() }
}
function Send-RecoveryCommand([string]$Value) {
  $until=(Get-Date).AddSeconds(150)
  while(Test-Path -LiteralPath $taskCommand) { if((Get-Date)-gt $until){throw 'Editor did not consume its pending command.'}; Start-Sleep -Seconds 2 }
  [System.IO.File]::WriteAllText($taskPending,$Value)
  $sent=[DateTime]::UtcNow
  Move-Item -LiteralPath $taskPending -Destination $taskCommand
  return $sent
}
function Wait-EditorEdit {
  $sent=Send-RecoveryCommand 'editor-state'
  $until=(Get-Date).AddSeconds(150)
  while((Get-Date)-lt $until) {
    if((Test-Path -LiteralPath $taskStatus)-and(Get-Item -LiteralPath $taskStatus).LastWriteTimeUtc-ge $sent) {
      $status=Read-RecoveryText $taskStatus
      if($status.Contains('Playing=False')){return}
      if($status.Contains('Playing=True')){throw 'Editor is still playing; refusing to start another fixture.'}
    }
    Start-Sleep -Seconds 2
  }
  throw 'Editor state acknowledgement timed out.'
}
$taskChecks=@(
 @{Command='guided-tutorial-playtest';Report='Artifacts/Recovery/guided-tutorial-playtest.txt';Seconds=1300},
 @{Command='police-operations-playtest';Report='Artifacts/Recovery/police-operations-playtest.txt';Seconds=850},
 @{Command='road-incident-playtest';Report='Artifacts/Recovery/road-incident-playtest.txt';Seconds=750},
 @{Command='presentation-feedback-playtest';Report='Artifacts/Recovery/presentation-feedback-playtest.txt';Seconds=450},
 @{Command='home-visual-playtest';Report='Artifacts/Recovery/home-visual-playtest.txt';Seconds=650},
 @{Command='tactical-map-playtest';Report='Artifacts/Recovery/tactical-map-playtest.txt';Seconds=650},
 @{Command='crew-tracking-playtest';Report='Artifacts/Recovery/crew-tracking-playtest.txt';Seconds=750},
 @{Command='train-raid-playtest';Report='Artifacts/Recovery/train-raid-playtest.txt';Seconds=850},
 @{Command='production-strategy-playtest';Report='Artifacts/Recovery/production-strategy-playtest.txt';Seconds=1100},
 @{Command='police-vehicle-playtest';Report='Artifacts/Recovery/police-vehicle-playtest.txt';Seconds=800},
 @{Command='police-career-playtest';Report='Artifacts/Recovery/police-playtest.txt';Seconds=850},
 @{Command='commerce-playtest';Report='Artifacts/Recovery/commerce-playtest.txt';Seconds=1000},
 @{Command='redesign-playtest';Report='Artifacts/Recovery/redesign-playtest.txt';Seconds=650},
 @{Command='rts-controls-playtest';Report='Artifacts/CityQA/rts-controls-playtest.txt';Seconds=850},
 @{Command='milestone-one-play';Report='Artifacts/Milestone01/playtest-checks.txt';Seconds=650},
 @{Command='recovery-checks';Report='Artifacts/Recovery/unity-save-checks.txt';Seconds=120},
 @{Command='ads-checks';Report='Artifacts/Recovery/ads-checks.txt';Seconds=120},
 @{Command='purchase-checks';Report='Artifacts/Recovery/purchases-checks.txt';Seconds=120}
)
$taskStartIndex=0
for($taskIndex=0;$taskIndex-lt $taskChecks.Count;$taskIndex++){if($taskChecks[$taskIndex].Command-eq $StartAt){$taskStartIndex=$taskIndex;break}}
foreach($check in $taskChecks[$taskStartIndex..($taskChecks.Count-1)]) {
  if($Skip-contains $check.Command){continue}
  if($Only.Count-and $Only-notcontains $check.Command){continue}
  Wait-EditorEdit
  $report=Join-Path $taskRoot $check.Report
  $sent=Send-RecoveryCommand $check.Command
  Write-Output ('Started '+$check.Command+' at '+[DateTime]::Now.ToString('HH:mm:ss'))
  $until=(Get-Date).AddSeconds($check.Seconds)
  $completed=$false
  while((Get-Date)-lt $until) {
    if((Test-Path -LiteralPath $taskStatus)-and(Get-Item -LiteralPath $taskStatus).LastWriteTimeUtc-ge $sent) {
      $status=Read-RecoveryText $taskStatus
      if($status.Contains('ERROR')){throw $status}
    }
    if((Test-Path -LiteralPath $report)-and(Get-Item -LiteralPath $report).LastWriteTimeUtc-ge $sent) {
      $body=Read-RecoveryText $report
      $result=[regex]::Match($body,'(?im)^(?:RESULT|Result:)\s*(\d+) passed,\s*(\d+) failed')
      $static=$check.Command-in @('recovery-checks','ads-checks','milestone-one-play')
      if($result.Success-or($static-and $body-match '(?m)^PASS '-and $body-notmatch 'RUNNING')) {
        if($body-match '(?m)^FAIL '-or($result.Success-and[int]$result.Groups[2].Value-ne 0)){throw ('Failed '+$check.Command+'; inspect '+$check.Report)}
        $passes=([regex]::Matches($body,'(?m)^PASS ')).Count
        Write-Output ('Completed '+$check.Command+': '+$passes+' passed')
        $completed=$true;break
      }
    }
    Start-Sleep -Seconds 2
  }
  if(-not $completed){throw ('Timeout waiting for '+$check.Command)}
  Start-Sleep -Seconds 3
}
Wait-EditorEdit
if($BuildAndroid) {
  $sent=Send-RecoveryCommand 'recovery-build-android'
  Write-Output ('Started Android build at '+[DateTime]::Now.ToString('HH:mm:ss'))
  $buildReport=Join-Path $taskRoot 'Artifacts/Recovery/android-build.txt'
  $until=(Get-Date).AddMinutes(50)
  $done=$false
  while((Get-Date)-lt $until) {
    if((Test-Path -LiteralPath $buildReport)-and(Get-Item -LiteralPath $buildReport).LastWriteTimeUtc-ge $sent) {
      $body=Read-RecoveryText $buildReport
      Write-Output $body
      if(-not $body.Contains('Result: Succeeded')){throw 'Android build failed.'}
      $done=$true;break
    }
    Start-Sleep -Seconds 3
  }
  if(-not $done){throw 'Android build report timed out.'}
}
