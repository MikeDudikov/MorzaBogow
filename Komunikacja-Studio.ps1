#requires -Version 7.5
<##
Lokalny dziennik koordynatora. Nie wysyla wiadomosci do Claude Code,
nie uruchamia modeli i nie zastępuje uprawnien ani bramki jakosci.
##>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Init','Read','Append')][string]$Action,
    [string]$ProjectRoot = $PSScriptRoot,
    [string]$MessageFile,
    [long]$ExpectedRevision,
    [long]$AfterRevision = 0,
    [ValidateRange(0,10000)][int]$Last = 0
)
$ErrorActionPreference = 'Stop'
$lockStream = $null
$temporaryPath = $null
function ConvertTo-CanonicalObject($Value) {
    if ($Value -is [System.Collections.IDictionary]) {
        $sorted = [Collections.Specialized.OrderedDictionary]::new([StringComparer]::Ordinal)
        $keys = [string[]]@($Value.Keys)
        [Array]::Sort($keys, [StringComparer]::Ordinal)
        foreach ($key in $keys) { $sorted[$key] = ConvertTo-CanonicalObject $Value[$key] }
        return $sorted
    }
    if ($Value -is [array]) {
        $items = @($Value | ForEach-Object { ,(ConvertTo-CanonicalObject $_) })
        return ,$items
    }
    return $Value
}
try {
    $root = [IO.Path]::GetFullPath($ProjectRoot)
    if (!(Test-Path -LiteralPath $root -PathType Container)) { throw 'PROJECT_ROOT_NOT_FOUND' }
    $directory = Join-Path $root 'work/studio-communication'
    $journalPath = Join-Path $directory 'journal.json'
    $lockPath = Join-Path $directory '.journal.lock'
    # Odrzuc przekierowania do innego katalogu/plikow. Sciezki wyjscia sa stale.
    foreach ($candidate in @($root, (Join-Path $root 'work'), $directory, $journalPath, $lockPath)) {
        if (Test-Path -LiteralPath $candidate) {
            if (((Get-Item -LiteralPath $candidate -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'REPARSE_POINT_NOT_ALLOWED' }
        }
    }
    $message = $null
    if ($Action -eq 'Append') {
        if (!$PSBoundParameters.ContainsKey('ExpectedRevision') -or $ExpectedRevision -lt 0) { throw 'EXPECTED_REVISION_REQUIRED' }
        if (!$MessageFile) { throw 'MESSAGE_FILE_REQUIRED' }
        $message = Get-Content -LiteralPath $MessageFile -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable -DateKind String
        if ($message -isnot [System.Collections.IDictionary]) { throw 'MESSAGE_MUST_BE_OBJECT' }
        $allowedKeys = @('id','kind','taskId','from','to','inReplyTo','text','data')
        if (@($message.Keys | Where-Object { $_ -cnotin $allowedKeys }).Count) { throw 'UNKNOWN_MESSAGE_FIELD' }
        foreach ($key in @('id','taskId','from','to')) {
            if ($message[$key] -isnot [string] -or $message[$key] -cnotmatch '^[a-zA-Z0-9][a-zA-Z0-9_.:/-]{0,119}$') { throw "INVALID_MESSAGE_FIELD: $key" }
        }
        $kinds = @('ASSIGN','ACK','PROGRESS','BLOCKED','DECISION','HANDOFF','REVIEW','READY','ACCEPTED','CANCEL','FREEZE','UNFREEZE','CHECKPOINT')
        if ($message.kind -cnotin $kinds) { throw 'INVALID_MESSAGE_KIND' }
        if ($message.text -isnot [string] -or [string]::IsNullOrWhiteSpace($message.text) -or $message.text.Length -gt 16000) { throw 'INVALID_MESSAGE_TEXT' }
        if ($message.ContainsKey('data') -and $message.data -isnot [System.Collections.IDictionary]) { throw 'DATA_MUST_BE_OBJECT' }
        if ($message.kind -eq 'ACK' -and !$message.inReplyTo) { throw 'ACK_REQUIRES_REPLY' }
        if ($null -ne $message.inReplyTo -and ($message.inReplyTo -isnot [string] -or $message.inReplyTo -cnotmatch '^[a-zA-Z0-9][a-zA-Z0-9_.:/-]{0,119}$')) { throw 'INVALID_REPLY' }
        # Odrzuc dane, ktore zostalyby obciete podczas serializacji.
        $null = $message | ConvertTo-Json -Depth 30 -WarningAction Stop
    }
    if ($AfterRevision -lt 0) { throw 'INVALID_AFTER_REVISION' }
    if ($Action -eq 'Init') {
        [IO.Directory]::CreateDirectory($directory) | Out-Null
    } elseif (!(Test-Path -LiteralPath $journalPath -PathType Leaf)) { throw 'NOT_INITIALIZED' }
    # Blokada chroni tylko zapis dziennika. Nie jest blokada Unity ani wyboru lidera.
    $lockMode = if ($Action -eq 'Read') { [IO.FileMode]::Open } else { [IO.FileMode]::OpenOrCreate }
    $lockStream = [IO.File]::Open($lockPath, $lockMode, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $exists = Test-Path -LiteralPath $journalPath -PathType Leaf
    if ($exists) {
        $state = Get-Content -LiteralPath $journalPath -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable -DateKind String
        if ($state -isnot [System.Collections.IDictionary] -or $state.schemaVersion -ne 1 -or $state.events -isnot [array] -or $state.revision -ne $state.events.Count) { throw 'INVALID_JOURNAL' }
        $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        for ($index = 0; $index -lt $state.events.Count; $index++) {
            $event = $state.events[$index]
            if ($event.revision -ne $index + 1 -or !$event.message.id -or !$ids.Add([string]$event.message.id)) { throw 'INVALID_JOURNAL_SEQUENCE' }
        }
    } else {
        $state = @{ schemaVersion=1; revision=0; createdUtc=[DateTime]::UtcNow.ToString('o'); events=@() }
    }
    $appendResult = 'NOT_APPLICABLE'
    $recordRevision = $null
    if ($Action -eq 'Append') {
        $duplicate = @($state.events | Where-Object { $_.message.id -ceq $message.id })
        if ($duplicate.Count) {
            $oldPayload = ConvertTo-CanonicalObject $duplicate[0].message | ConvertTo-Json -Depth 40 -Compress
            $newPayload = ConvertTo-CanonicalObject $message | ConvertTo-Json -Depth 40 -Compress
            if ($oldPayload -cne $newPayload) { throw 'DUPLICATE_MESSAGE_ID_WITH_DIFFERENT_CONTENT' }
            $appendResult = 'ALREADY_RECORDED'
            $recordRevision = $duplicate[0].revision
        } elseif ($state.revision -ne $ExpectedRevision) { throw "REVISION_CONFLICT: expected=$ExpectedRevision actual=$($state.revision)" }
    }
    if ($Action -eq 'Append' -and $appendResult -ne 'ALREADY_RECORDED') {
        if ($message.inReplyTo) {
            $original = @($state.events | Where-Object { $_.message.id -ceq $message.inReplyTo })
            if ($original.Count -ne 1) { throw 'REPLY_TARGET_NOT_FOUND' }
            if ($message.kind -eq 'ACK' -and ($message.from -cne $original[0].message.to -or $message.to -cne $original[0].message.from -or $message.taskId -cne $original[0].message.taskId)) { throw 'ACK_RECIPIENT_MISMATCH' }
        }
        $state.revision = [long]$state.revision + 1
        $state.events += @{ revision=$state.revision; recordedUtc=[DateTime]::UtcNow.ToString('o'); message=$message }
        $appendResult = 'APPENDED'
        $recordRevision = $state.revision
    }
    if (($Action -eq 'Init' -and !$exists) -or $appendResult -eq 'APPENDED') {
        $temporaryPath = Join-Path $directory ([Guid]::NewGuid().ToString('N') + '.tmp')
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($state | ConvertTo-Json -Depth 60 -WarningAction Stop) + [Environment]::NewLine)
        $writer = [IO.File]::Open($temporaryPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $writer.Write($bytes,0,$bytes.Length); $writer.Flush($true) } finally { $writer.Dispose() }
        if ($exists) {
            for ($attempt = 0; ; $attempt++) {
                try {
                    [IO.File]::Replace($temporaryPath, $journalPath, [System.Management.Automation.Language.NullString]::Value)
                    break
                } catch {
                    # Czytnik/antywirus moze chwilowo odmowic DELETE_SHARE.
                    # Ponawiamy tylko te bledy I/O, nadal trzymajac blokade dziennika.
                    $cause = $_.Exception
                    while ($cause.InnerException) { $cause = $cause.InnerException }
                    $nativeCode = $cause.HResult -band 0xffff
                    if ($attempt -ge 4 -or $nativeCode -notin @(32,33,1175)) { throw }
                    Start-Sleep -Milliseconds (50 * [Math]::Pow(2,$attempt))
                }
            }
        } else { [IO.File]::Move($temporaryPath, $journalPath) }
        $temporaryPath = $null
    }
    $selected = @($state.events | Where-Object { $_.revision -gt $AfterRevision })
    if ($Last -gt 0) { $selected = @($selected | Select-Object -Last $Last) }
    $checkpoint = @($state.events | Where-Object { $_.message.kind -ceq 'CHECKPOINT' } | Select-Object -Last 1)
    $latestCheckpoint = if ($checkpoint.Count) { $checkpoint[0] } else { $null }
    [ordered]@{
        status='OK'; action=$Action; journalPath=$journalPath; revision=$state.revision
        appendResult=$appendResult; recordRevision=$recordRevision
        delivery='NOT_SENT_BY_THIS_SCRIPT'; runtimeVerified=$false
        latestCheckpoint=$latestCheckpoint; events=$selected
    } | ConvertTo-Json -Depth 64
    exit 0
} catch {
    [Console]::Error.WriteLine((@{status='ERROR'; error=$_.Exception.Message} | ConvertTo-Json -Compress))
    exit 2
} finally {
    if ($temporaryPath -and [IO.File]::Exists($temporaryPath)) { [IO.File]::Delete($temporaryPath) }
    if ($lockStream) { $lockStream.Dispose() }
}
