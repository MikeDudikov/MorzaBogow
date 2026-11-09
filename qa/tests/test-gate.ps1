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
function Invoke-Case([string]$name,[string]$mode,[int]$expectedCode,[string]$special='',[string]$reason='') {
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
        if ($reason -and $message -notlike "*$reason*") { throw "$name blocked for the wrong reason: $message" }
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
# Unity reports asserts="0" for real tests, so assertion proof falls back to the test's source.
$fixtureDir=Join-Path $scratch 'Assets/Tests'
[IO.Directory]::CreateDirectory($fixtureDir) | Out-Null
$fixturePath=Join-Path $fixtureDir 'Fixture.cs'
function Invoke-SourceCase([string]$name,[string]$source,[int]$expectedCode,[string]$mode='sourceassert') {
    Set-Content -LiteralPath $fixturePath -Value $source
    Invoke-Case $name $mode $expectedCode '' $(if ($expectedCode -eq 2) { 'Test bez asercji' } else { '' })
}
$real='Assert.That(1, Is.EqualTo(1));'
Invoke-SourceCase 'zero counter with assertion in source' "public sealed class Fixture { public void RealAssertion() { $real } }" 0
Invoke-SourceCase 'expression-bodied test with assertion' "public sealed class Fixture { public void RealAssertion() => $real }" 0
Invoke-SourceCase 'generic assertion' 'public sealed class Fixture { public void RealAssertion() { Assert.Throws<System.Exception>(() => { }); } }' 0
Invoke-SourceCase 'assertion only in another method' "public sealed class Fixture { public void RealAssertion() { var value = 1; } public void Other() { $real } }" 2
Invoke-SourceCase 'assertion only in comments' "public sealed class Fixture { public void RealAssertion() { /* $real */ // Assert.Fail();`n } }" 2
Invoke-SourceCase 'same method name in another class' "public sealed class Helper { public void RealAssertion() { $real } } public sealed class Fixture { public void RealAssertion() { } }" 2
Invoke-SourceCase 'open brace inside a string' "public sealed class Fixture { public void RealAssertion() { var s = `"{`"; } public void Other() { $real } }" 2
Invoke-SourceCase 'url inside a string' "public sealed class Fixture { public void RealAssertion() { var s = `"http://x`"; } public void Other() { $real } }" 2
Invoke-SourceCase 'assertion text inside a string' 'public sealed class Fixture { public void RealAssertion() { var s = "Assert.That(1)"; } }' 2
Invoke-SourceCase 'call site is not a declaration' "public sealed class Fixture { public void Other() { if (RealAssertion(1)) { $real } } }" 2
Invoke-SourceCase 'Assert.Pass is not evidence' 'public sealed class Fixture { public void RealAssertion() { Assert.Pass(); } }' 2
Invoke-SourceCase 'lowercase lookalike is not evidence' 'public sealed class Fixture { public void RealAssertion() { assert.foo(); } }' 2
Invoke-SourceCase 'disabled code is not evidence' "public sealed class Fixture { public void RealAssertion() {`n#if false`n$real`n#endif`n } }" 2
Invoke-SourceCase 'class named only in a comment' "// class Fixture { public void RealAssertion() { $real } }`npublic sealed class Other { }" 2
Invoke-SourceCase 'overload without assertion' "public sealed class Fixture { public void RealAssertion() { } public void RealAssertion(int value) { $real } }" 2
Invoke-SourceCase 'TestName points at an empty method' "public sealed class Fixture { public void Empty() { } public void RealAssertion() { $real } }" 2 'sourcetestname'
Invoke-Case 'missing asserts attribute blocks' 'sourcemissingattr' 2
Remove-Item -LiteralPath $fixturePath
Invoke-Case 'zero counter without test source' 'sourceassert' 2 '' 'Test bez asercji'
$report=@{status='PASS';scope='Gate self-tests with synthetic runner; NOT Unity/game tests';count=$results.Count;utc=[DateTime]::UtcNow.ToString('o');cases=$results;scratch=$scratch}
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $sourceRoot 'qa/SELF_TEST_REPORT.json') -Encoding utf8
Write-Host "Gate self-tests passed: $($results.Count). Scratch retained: $scratch"
