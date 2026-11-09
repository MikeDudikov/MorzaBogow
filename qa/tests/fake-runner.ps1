# Test double for gate self-tests ONLY. Never use in the production policy.
param([string]$Results,[string]$Log,[string]$Mode='pass')
$ErrorActionPreference='Stop'
if ($Mode -eq 'timeout') { Start-Sleep -Seconds 5; exit 0 }
if ($Mode -eq 'crash') { exit 1 }
if ($Mode -eq 'mutate') { Add-Content -LiteralPath (Join-Path $PSScriptRoot '../../Assets/state.txt') -Value 'changed' }
if ($Mode -eq 'mutateeconomy') { Set-Content -LiteralPath (Join-Path $PSScriptRoot '../../ECONOMY_DESIGN.md') -Value 'Changed economic requirement during test' }
if ($Mode -eq 'mutatevisual') { Set-Content -LiteralPath (Join-Path $PSScriptRoot '../../VISUAL_TECH_SPEC.md') -Value 'Changed visual requirement during test' }
if ($Mode -eq 'mutatevision') { Set-Content -LiteralPath (Join-Path $PSScriptRoot '../../GAME_VISION.md') -Value 'Changed vision requirement during test' }
if ($Mode -ne 'nolog') { Set-Content -LiteralPath $Log -Value 'FAKE RUNNER SELF-TEST - NOT UNITY EVIDENCE' }
if ($Mode -eq 'noxml') { exit 0 }
if ($Mode -eq 'malformed') { Set-Content -LiteralPath $Results -Value '<test-run'; exit 0 }
if ($Mode -eq 'dtd') { Set-Content -LiteralPath $Results -Value '<!DOCTYPE test-run [<!ENTITY x "bad">]><test-run>&x;</test-run>'; exit 0 }
$count=if ($Mode -eq 'zero') {0} else {1}
$result=if ($Mode -eq 'failed') {'Failed'} elseif ($Mode -eq 'skipped') {'Skipped'} else {'Passed'}
$caseResult=if ($Mode -eq 'hiddenfailed') {'Failed'} else {$result}
$asserts=if ($Mode -eq 'noasserts') {0} else {1}
$case=if ($count) { '<test-case fullname="Fixture.RealAssertion" result="'+$caseResult+'" asserts="'+$asserts+'" />' } else {''}
if ($Mode -eq 'duplicate') { $case += $case; $count=2 }
$passed=if ($Mode -eq 'counter') {99} else {$count}
$xml='<test-run result="'+$result+'" total="'+$count+'" passed="'+$passed+'" failed="0" skipped="0" inconclusive="0"><test-suite result="Passed">'+$case+'</test-suite></test-run>'
Set-Content -LiteralPath $Results -Value $xml -Encoding utf8
