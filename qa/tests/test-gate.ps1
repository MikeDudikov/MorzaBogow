#requires -Version 7.0
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$sourceRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$scratch=Join-Path $sourceRoot ('work/gate-selftest-'+[Guid]::NewGuid().ToString('N'))
$exe=(Get-Process -Id $PID).Path
[IO.Directory]::CreateDirectory($scratch) | Out-Null
foreach ($dir in @('.claude/hooks','.claude/qa/tasks','qa/tests','Assets')) { [IO.Directory]::CreateDirectory((Join-Path $scratch $dir)) | Out-Null }
Copy-Item -LiteralPath (Join-Path $sourceRoot '.claude/hooks/quality-gate.ps1') -Destination (Join-Path $scratch '.claude/hooks/quality-gate.ps1')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fake-runner.ps1') -Destination (Join-Path $scratch 'qa/tests/fake-runner.ps1')
Set-Content -LiteralPath (Join-Path $scratch 'Assets/state.txt') -Value 'initial'
Set-Content -LiteralPath (Join-Path $scratch 'GAME_VISION.md') -Value 'initial vision requirements'
$results=@()
function Invoke-Case([string]$name,[string]$mode,[int]$expectedCode,[string]$special='') {
    $plan=@{taskId='1';sessionId='selftest';requirements=@('GATE-SELFTEST');profiles=@{EditMode=@{expectedTests=@('Fixture.RealAssertion')}}}
    if ($special -eq 'missingtest') { $plan.profiles.EditMode.expectedTests=@('Missing.Test') }
    if ($special -eq 'session') { $plan.sessionId='wrong' }
    $plan | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $scratch '.claude/qa/tasks/1.json')
    $limit=if ($mode -eq 'timeout') {1} else {10}
    $policy=@{schemaVersion=1;requiredProfiles=@('EditMode');profiles=@{EditMode=@{executable=$exe;arguments=@('-NoLogo','-NoProfile','-File',(Join-Path $scratch 'qa/tests/fake-runner.ps1'),'-Results','{results}','-Log','{log}','-Mode',$mode);timeoutSeconds=$limit;minTests=1}}}
    if ($special -eq 'missingexe') { $policy.profiles.EditMode.executable=Join-Path $scratch 'not-installed.exe' }
    if ($special -eq 'profile') { $policy.requiredProfiles=@('EditMode','PlayMode') }
    $policy | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $scratch '.claude/qa/policy.json')
    $event=@{hook_event_name='TaskCompleted';task_id='1';session_id='selftest'}
    if ($special -eq 'pre') { $event=@{hook_event_name='PreToolUse';tool_name='TaskUpdate';tool_input=@{taskId='1';status='completed'};session_id='selftest'} }
    if ($special -eq 'owner') { $event=@{hook_event_name='PreToolUse';tool_name='TaskUpdate';tool_input=@{taskId='1';owner='qa'};session_id='selftest'} }
    if ($special -eq 'traversal') { $event.task_id='../1' }
    if ($special -eq 'noplan') { Remove-Item -LiteralPath (Join-Path $scratch '.claude/qa/tasks/1.json') }
    if ($special -eq 'stale') {
        [IO.Directory]::CreateDirectory((Join-Path $scratch 'qa/reports/old')) | Out-Null
        Set-Content -LiteralPath (Join-Path $scratch 'qa/reports/old/report.json') -Value '{"status":"PASS"}'
        Set-Content -LiteralPath (Join-Path $scratch 'qa/reports/old/EditMode.xml') -Value '<test-run result="Passed"/>'
    }
    $psi=[Diagnostics.ProcessStartInfo]::new(); $psi.FileName=$exe; $psi.UseShellExecute=$false; $psi.CreateNoWindow=$true
    $psi.RedirectStandardInput=$true; $psi.RedirectStandardOutput=$true; $psi.RedirectStandardError=$true
    foreach ($argValue in @('-NoLogo','-NoProfile','-NonInteractive','-File',(Join-Path $scratch '.claude/hooks/quality-gate.ps1'))) { $psi.ArgumentList.Add($argValue) }
    $proc=[Diagnostics.Process]::new(); $proc.StartInfo=$psi
    try {
        $null=$proc.Start(); $out=$proc.StandardOutput.ReadToEndAsync(); $err=$proc.StandardError.ReadToEndAsync()
        $proc.StandardInput.Write(($event | ConvertTo-Json -Depth 5 -Compress)); $proc.StandardInput.Close()
        if (!$proc.WaitForExit(30000)) { $proc.Kill($true); throw 'Self-test exceeded 30 seconds' }
        $message=$err.GetAwaiter().GetResult(); $null=$out.GetAwaiter().GetResult()
        if ($proc.ExitCode -ne $expectedCode) { throw "$name expected $expectedCode got $($proc.ExitCode): $message" }
        $script:results += @{name=$name;expectedExit=$expectedCode;actualExit=$proc.ExitCode;status='PASS'}
        Write-Host "PASS: $name"
    } finally { $proc.Dispose() }
}
Invoke-Case 'valid executed suite' 'pass' 0
Invoke-Case 'PreToolUse completion' 'pass' 0 'pre'
Invoke-Case 'TaskUpdate owner only does not close' 'pass' 0 'owner'
foreach ($mode in @('failed','hiddenfailed','skipped','zero','noasserts','malformed','dtd','duplicate','counter','noxml','nolog','crash','timeout','mutate','mutatevisual','mutateeconomy','mutatevision')) { Invoke-Case $mode $mode 2 }
foreach ($special in @('missingtest','session','missingexe','profile','traversal','noplan')) { Invoke-Case $special 'pass' 2 $special }
Invoke-Case 'stale PASS cannot authorize' 'noxml' 2 'stale'
$report=@{status='PASS';scope='Gate self-tests with synthetic runner; NOT Unity/game tests';count=$results.Count;utc=[DateTime]::UtcNow.ToString('o');cases=$results;scratch=$scratch}
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $sourceRoot 'qa/SELF_TEST_REPORT.json') -Encoding utf8
Write-Host "Gate self-tests passed: $($results.Count). Scratch retained: $scratch"
