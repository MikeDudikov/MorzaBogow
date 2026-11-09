#requires -Version 7.0
$ErrorActionPreference='Stop'
try {
    $context=[Console]::In.ReadToEnd() | ConvertFrom-Json -AsHashtable
    if (!$context.session_id) { throw 'Brak session_id' }
    @{hookSpecificOutput=@{hookEventName='SessionStart';additionalContext=('QUALITY_GATE_SESSION_ID='+[string]$context.session_id+'; Read GAME_VISION.md, GAME_DESIGN.md and CLAUDE.md. Test plans must use this exact sessionId.')}} | ConvertTo-Json -Depth 5 -Compress
    exit 0
} catch { [Console]::Error.WriteLine($_.Exception.Message); exit 2 }
