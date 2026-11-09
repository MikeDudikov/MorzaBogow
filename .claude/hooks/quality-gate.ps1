#requires -Version 7.0
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$lockHandle = $null
$runDir = $null

function Read-Json([string]$path) {
    return Get-Content -LiteralPath $path -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
}
function Snapshot {
    $rows = [Collections.Generic.List[string]]::new()
    foreach ($name in @('Assets','Packages','ProjectSettings','.claude','qa','docs','PROMPT_WDROZENIOWY.md','ASSET_STATUS.json','GAME_VISION.md','GAME_DESIGN.md','MYTHIC_SCENARIOS.md','CLAUDE.md','VISUAL_TECH_SPEC.md','ECONOMY_DESIGN.md','ECONOMY_BALANCE.md','START_PROMPT_PL.md','README_PL.md')) {
        $target = Join-Path $projectRoot $name
        if (!(Test-Path -LiteralPath $target)) { continue }
        $files = if (Test-Path -LiteralPath $target -PathType Leaf) { @(Get-Item -LiteralPath $target) } else { @(Get-ChildItem -LiteralPath $target -Recurse -File -Force) }
        foreach ($file in $files) {
            $rel = [IO.Path]::GetRelativePath($projectRoot, $file.FullName).Replace('\','/')
            if ($rel.StartsWith('qa/reports/')) { continue }
            if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Symlink/reparse point niedozwolony w snapshot: $rel" }
            $rows.Add($rel + ':' + (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)
        }
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes((@($rows | Sort-Object -CaseSensitive) -join "`n"))
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
}
$script:cleanSources = $null
function Remove-CodeNoise([string]$s) {
    # Blanks string and character literals and removes comments, so nothing inside them can look like code or braces.
    $out = [Text.StringBuilder]::new($s.Length)
    $i = 0
    $n = $s.Length
    while ($i -lt $n) {
        $c = $s[$i]
        $d = if ($i + 1 -lt $n) { $s[$i + 1] } else { [char]0 }
        if ($c -ceq '/' -and $d -ceq '/') {
            while ($i -lt $n -and $s[$i] -cne "`n") { $i++ }
            continue
        }
        if ($c -ceq '/' -and $d -ceq '*') {
            $end = $s.IndexOf('*/', $i + 2)
            $i = if ($end -lt 0) { $n } else { $end + 2 }
            [void]$out.Append(' ')
            continue
        }
        if ($c -ceq '"') {
            $verbatim = ($i -ge 1 -and $s[$i - 1] -ceq '@') -or ($i -ge 2 -and $s[$i - 1] -ceq '$' -and $s[$i - 2] -ceq '@')
            $i++
            while ($i -lt $n) {
                if ($verbatim) {
                    if ($s[$i] -ceq '"') {
                        if ($i + 1 -lt $n -and $s[$i + 1] -ceq '"') { $i += 2; continue }
                        $i++
                        break
                    }
                    $i++
                    continue
                }
                if ($s[$i] -ceq '\') { $i += 2; continue }
                if ($s[$i] -ceq '"') { $i++; break }
                $i++
            }
            [void]$out.Append('""')
            continue
        }
        if ($c -ceq "'") {
            $i++
            while ($i -lt $n) {
                if ($s[$i] -ceq '\') { $i += 2; continue }
                if ($s[$i] -ceq "'") { $i++; break }
                $i++
            }
            [void]$out.Append("''")
            continue
        }
        [void]$out.Append($c)
        $i++
    }
    return [regex]::Replace($out.ToString(), '(?ms)^\s*#if\s+false\b.*?^\s*#endif\b[^\r\n]*', '')
}
function Get-CleanSources {
    if ($null -eq $script:cleanSources) {
        $script:cleanSources = @()
        $assets = Join-Path $projectRoot 'Assets'
        if (Test-Path -LiteralPath $assets -PathType Container) {
            foreach ($file in Get-ChildItem -LiteralPath $assets -Recurse -File -Filter '*.cs') {
                $script:cleanSources += Remove-CodeNoise (Get-Content -LiteralPath $file.FullName -Raw -Encoding utf8)
            }
        }
    }
    return $script:cleanSources
}
function Get-BlockEnd([string]$source, [int]$open, [char]$opening, [char]$closing) {
    # $open is the index of the opening character; returns the index of its match, or -1.
    $depth = 0
    for ($index = $open; $index -lt $source.Length; $index++) {
        if ($source[$index] -ceq $opening) { $depth++ }
        elseif ($source[$index] -ceq $closing) {
            $depth--
            if ($depth -eq 0) { return $index }
        }
    }
    return -1
}
function Get-MethodBodies([string]$classBody, [string]$methodName) {
    # Only real declarations with a return type count, so call sites are never mistaken for methods.
    $keywords = @('return','new','await','if','else','while','for','foreach','switch','case','throw','in','is','as','using','lock','yield','typeof','nameof','do','not','and','or')
    $pattern = '(?<![\w.])(?<type>[A-Za-z_][\w.]*(?:<[^<>()]*(?:<[^<>()]*>[^<>()]*)*>)?(?:\[[,\s]*\])*\??)\s+' + [regex]::Escape($methodName) + '\s*(?:<[^<>()]*>)?\s*\('
    $bodies = @()
    foreach ($match in [regex]::Matches($classBody, $pattern)) {
        if ($keywords -ccontains $match.Groups['type'].Value) { continue }
        $paren = $match.Index + $match.Length - 1
        $close = Get-BlockEnd $classBody $paren '(' ')'
        if ($close -lt 0) { continue }
        $after = $classBody.Substring($close + 1).TrimStart()
        $start = $classBody.Length - $after.Length
        if ($after.StartsWith('=>')) {
            $end = $classBody.IndexOf(';', $start)
            if ($end -ge 0) { $bodies += $classBody.Substring($start, $end - $start) }
        } elseif ($after.StartsWith('{')) {
            $end = Get-BlockEnd $classBody $start '{' '}'
            if ($end -ge 0) { $bodies += $classBody.Substring($start, $end - $start + 1) }
        }
    }
    return ,$bodies
}
function Test-SourceAssertion($testCase) {
    # Unity's NUnit XML reports asserts="0" even for tests that execute assertions, so the
    # counter alone cannot prove an assertion. Require one in the test's real method source.
    $fullName = $testCase.GetAttribute('fullname')
    $classAttr = $testCase.GetAttribute('classname')
    $methodName = $testCase.GetAttribute('methodname')
    if (!$classAttr -or !$methodName) {
        $parts = ($fullName -replace '\(.*$', '').Split('.')
        if ($parts.Count -lt 2) { return $false }
        $methodName = $parts[$parts.Count - 1]
        $classAttr = ($parts[0..($parts.Count - 2)] -join '.')
    }
    $methodName = $methodName -replace '[<(].*$', ''
    $classPath = ($classAttr -replace '[<(].*$', '').Split('.')
    $className = $classPath[$classPath.Count - 1].Split('+')[-1]
    $namespace = if ($classPath.Count -gt 1) { $classPath[0..($classPath.Count - 2)] -join '.' } else { '' }

    $assertion = '\b(?:Assert|LogAssert|CollectionAssert|StringAssert)\s*\.\s*(?!Pass\b|Ignore\b|Inconclusive\b)\w+\s*[(<]'
    $classPattern = '\b(?:class|struct)\s+' + [regex]::Escape($className) + '\b[^{;]*\{'
    $found = 0
    foreach ($source in (Get-CleanSources)) {
        if ($namespace -and $source -cnotmatch ('\bnamespace\s+' + [regex]::Escape($namespace) + '\b')) { continue }
        foreach ($match in [regex]::Matches($source, $classPattern)) {
            $open = $match.Index + $match.Length - 1
            $close = Get-BlockEnd $source $open '{' '}'
            if ($close -lt 0) { continue }
            foreach ($body in (Get-MethodBodies $source.Substring($open, $close - $open + 1) $methodName)) {
                $found++
                if ($body -cnotmatch $assertion) { return $false }
            }
        }
    }
    return $found -gt 0
}
function Check-NUnit([string]$path, [int]$minTests, [string[]]$expected) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw 'Brak świeżego XML testów' }
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.MaxCharactersInDocument = 52428800
    $reader = [Xml.XmlReader]::Create($path, $settings)
    try {
        $doc = [Xml.XmlDocument]::new(); $doc.XmlResolver = $null; $doc.Load($reader)
    } finally { $reader.Dispose() }
    $root = $doc.DocumentElement
    if ($root.Name -cne 'test-run' -or $root.GetAttribute('result') -cne 'Passed') { throw 'NUnit: brak test-run Passed' }
    $cases = @($doc.SelectNodes('//test-case'))
    if ($cases.Count -lt $minTests) { throw "Za mało testów: $($cases.Count) < $minTests" }
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($case in $cases) {
        if ($case.GetAttribute('result') -cne 'Passed') { throw ('Test niewykonany lub nieudany: ' + $case.GetAttribute('fullname')) }
        $name = $case.GetAttribute('fullname')
        if (!$name -or !$names.Add($name)) { throw 'Brak lub duplikat fullname testu' }
        $asserts = 0
        if (![int]::TryParse($case.GetAttribute('asserts'), [ref]$asserts)) { throw "Brak licznika asercji: $name" }
        if ($asserts -lt 1 -and !(Test-SourceAssertion $case)) { throw "Test bez asercji: $name" }
    }
    foreach ($suite in $doc.SelectNodes('//test-suite')) {
        if ($suite.GetAttribute('result') -cne 'Passed') { throw 'Nieskuteczny setup/teardown lub zestaw' }
    }
    foreach ($attr in @('failed','skipped','inconclusive')) {
        $value = -1
        if (![int]::TryParse($root.GetAttribute($attr), [ref]$value) -or $value -ne 0) { throw "NUnit $attr != 0 lub brak licznika" }
    }
    foreach ($attr in @('total','passed')) {
        $value = -1
        if (![int]::TryParse($root.GetAttribute($attr), [ref]$value) -or $value -ne $cases.Count) { throw "Niespójny licznik $attr" }
    }
    foreach ($name in $expected) { if (!$names.Contains($name)) { throw "Brak wymaganego testu: $name" } }
    return @{ total=$cases.Count; passed=$cases.Count; names=@($names | Sort-Object); sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
}
try {
    $eventData = [Console]::In.ReadToEnd() | ConvertFrom-Json -AsHashtable
    if ($eventData.hook_event_name -eq 'PreToolUse') {
        if ($eventData.tool_name -ne 'TaskUpdate') { throw 'Nieobsługiwane narzędzie' }
        if (!$eventData.tool_input.ContainsKey('status') -or $eventData.tool_input.status -ne 'completed') { exit 0 }
        $taskId = [string]$eventData.tool_input.taskId
    } elseif ($eventData.hook_event_name -eq 'TaskCompleted') {
        $taskId = [string]$eventData.task_id
    } else { throw 'Nieobsługiwane zdarzenie' }
    if ($taskId -cnotmatch '^[A-Za-z0-9_-]{1,80}$') { throw 'Nieprawidłowy task ID' }
    $sessionId = [string]$eventData.session_id
    if (!$sessionId) { throw 'Brak session_id' }
    $policyPath = Join-Path $projectRoot '.claude/qa/policy.json'
    $policy = Read-Json $policyPath
    if ($policy.schemaVersion -ne 1) { throw 'Nieobsługiwana wersja polityki' }
    $plan = Read-Json (Join-Path $projectRoot ".claude/qa/tasks/$taskId.json")
    if ([string]$plan.taskId -cne $taskId -or [string]$plan.sessionId -cne $sessionId) { throw 'Raport/plan z innego zadania lub sesji' }
    if (@($plan.requirements).Count -eq 0 -or @($plan.profiles).Count -eq 0) { throw 'Brak wymagań lub profili w planie' }
    $profileNames = @($plan.profiles.Keys)
    foreach ($required in $policy.requiredProfiles) { if ($required -notin $profileNames) { throw "Brak obowiązkowego profilu: $required" } }
    $reportRoot = Join-Path $projectRoot 'qa/reports'
    [IO.Directory]::CreateDirectory($reportRoot) | Out-Null
    # Exclusive lock: do not run two Unity instances on the same project.
    $lockHandle = [IO.File]::Open((Join-Path $reportRoot '.gate.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $runId = [Guid]::NewGuid().ToString('N')
    $runDir = Join-Path $reportRoot "$taskId-$runId"
    [IO.Directory]::CreateDirectory($runDir) | Out-Null
    $before = Snapshot
    $started = [DateTime]::UtcNow.ToString('o')
    $runs = @()
    foreach ($profileName in ($profileNames | Sort-Object)) {
        if (!$policy.profiles.ContainsKey($profileName) -or $profileName -cnotmatch '^[A-Za-z0-9_-]+$') { throw 'Profil spoza polityki' }
        $profile = $policy.profiles[$profileName]
        $expected = @($plan.profiles[$profileName].expectedTests)
        if ($expected.Count -lt 1 -or @($expected | Where-Object { !($_ -is [string]) -or !$_ }).Count) { throw 'Wymagane jawne nazwy expectedTests' }
        $minTests = [int]$profile.minTests
        if ($minTests -lt 1) { throw 'minTests musi być dodatnie' }
        $timeout = [int]$profile.timeoutSeconds
        if ($timeout -lt 1 -or $timeout -gt 1800) { throw 'Nieprawidłowy timeout' }
        $xmlPath = Join-Path $runDir "$profileName.xml"
        $logPath = Join-Path $runDir "$profileName.log"
        $psi = [Diagnostics.ProcessStartInfo]::new()
        $psi.FileName = [string]$profile.executable
        $psi.WorkingDirectory = $projectRoot
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        foreach ($argValue in $profile.arguments) {
            $expanded = ([string]$argValue).Replace('{project}',$projectRoot).Replace('{results}',$xmlPath).Replace('{log}',$logPath)
            $psi.ArgumentList.Add($expanded)
        }
        $process = [Diagnostics.Process]::new(); $process.StartInfo = $psi
        try {
            if (!$process.Start()) { throw 'Nie uruchomiono runnera' }
            $stdout = $process.StandardOutput.ReadToEndAsync()
            $stderr = $process.StandardError.ReadToEndAsync()
            if (!$process.WaitForExit($timeout * 1000)) {
                $process.Kill($true); $process.WaitForExit(); throw "Timeout profilu $profileName"
            }
            [IO.File]::WriteAllText((Join-Path $runDir "$profileName.stdout.txt"), $stdout.GetAwaiter().GetResult())
            [IO.File]::WriteAllText((Join-Path $runDir "$profileName.stderr.txt"), $stderr.GetAwaiter().GetResult())
            if ($process.ExitCode -ne 0) { throw "Runner zakończył się kodem $($process.ExitCode)" }
        } finally { $process.Dispose() }
        if (!(Test-Path -LiteralPath $logPath -PathType Leaf) -or (Get-Item -LiteralPath $logPath).Length -eq 0) { throw 'Brak logu runnera' }
        $summary = Check-NUnit $xmlPath $minTests $expected
        $runs += @{ profile=$profileName; command=@($psi.FileName)+@($psi.ArgumentList); exitCode=0; results=$summary; logSha256=(Get-FileHash -LiteralPath $logPath -Algorithm SHA256).Hash }
    }
    if ($before -cne (Snapshot)) { throw 'Pliki projektu lub polityka zmieniły się podczas testów. Uruchom ponownie.' }
    @{ schemaVersion=1; status='PASS'; taskId=$taskId; sessionId=$sessionId; runId=$runId; snapshotSha256=$before; startedUtc=$started; finishedUtc=[DateTime]::UtcNow.ToString('o'); requirements=$plan.requirements; runs=$runs } |
        ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $runDir 'report.json') -Encoding utf8
    # A previous PASS is never used as authorization. Every hook invocation reruns suites.
    exit 0
} catch {
    $reason = $_.Exception.Message
    if ($runDir) {
        try { @{status='BLOCKED';reason=$reason;utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runDir 'blocked.json') -Encoding utf8 } catch {}
    }
    [Console]::Error.WriteLine("QUALITY GATE BLOCKED: $reason")
    exit 2
} finally {
    if ($lockHandle) { $lockHandle.Dispose() }
}
