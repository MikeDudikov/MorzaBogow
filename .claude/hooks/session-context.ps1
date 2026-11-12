#requires -Version 7.0
$ErrorActionPreference='Stop'
try {
    $context=[Console]::In.ReadToEnd() | ConvertFrom-Json -AsHashtable
    if (!$context.session_id) { throw 'Brak session_id' }
    @{hookSpecificOutput=@{hookEventName='SessionStart';additionalContext=('QUALITY_GATE_SESSION_ID='+[string]$context.session_id+'; Read CLAUDE.md and docs/NOWY_PLAN_GRY.md, docs/KATALOG_MECHANIK.md, docs/PARAMETRY_ROBOCZE.md, docs/MAPY_I_SCENARIUSZE.md, docs/PLAN_WDROZENIA.md, docs/HISTORIA_I_ZRODLA.md. Every session, independently reopen reliable sources for historical facts and Greek myth traditions; distinguish material evidence, myth and fictional game rules, and write a source report. Build a single-player mythological game using existing systems. Preserve aesthetics. Test plans must use this exact sessionId.')}} | ConvertTo-Json -Depth 5 -Compress
    exit 0
} catch { [Console]::Error.WriteLine($_.Exception.Message); exit 2 }
