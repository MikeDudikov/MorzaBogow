#requires -Version 7.5
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Python, [switch]$Apply, [string]$ProjectRoot=$PSScriptRoot)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$handle=$null
try {
    $root=[IO.Path]::GetFullPath($ProjectRoot)
    $scriptPath=Join-Path $root 'Tools/studio_bridge/studio_mcp.py'
    $configPath=Join-Path $root '.mcp.json'
    if (!(Test-Path -LiteralPath $scriptPath -PathType Leaf)) { throw 'Brak skryptu MCP.' }
    $pythonPath=(Get-Command $Python -CommandType Application -ErrorAction Stop).Source
    if ($pythonPath -match '[\\/]Microsoft[\\/]WindowsApps[\\/]python[^\\/]*\.exe$') { throw 'Podaj rzeczywisty interpreter, nie alias Windows Store.' }
    $before=Get-FileHash -LiteralPath $configPath -Algorithm SHA256
    $config=Get-Content -LiteralPath $configPath -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable -DateKind String
    if (!$config.ContainsKey('mcpServers')) { $config.mcpServers=@{} }
    $entry=@{type='stdio';command=$pythonPath;args=@('-B',$scriptPath,'--root',$root,'--endpoint','claude','mcp')}
    if ($config.mcpServers.ContainsKey('morza-bogow-studio')) {
        $old=$config.mcpServers['morza-bogow-studio']
        if (!$old.ContainsKey('type') -or $old.type -cne 'stdio' -or $old.command -cne $entry.command -or ($old.args | ConvertTo-Json -Compress) -cne ($entry.args | ConvertTo-Json -Compress)) { throw 'Konflikt istniejacego wpisu MCP; nie nadpisuje.' }
        @{status='ALREADY_CONFIGURED_RUNTIME_UNVERIFIED';changed=$false} | ConvertTo-Json -Compress
        exit 0
    }
    if (!$Apply) {
        @{status='READY_TO_INSTALL';changed=$false;server='morza-bogow-studio';entry=$entry;note='Apply doda jeden wpis; aktywacje potwierdz w kliencie Claude Code.'} | ConvertTo-Json -Depth 8
        exit 0
    }
    $lockPath=Join-Path $root 'qa/reports/.gate.lock'
    [IO.Directory]::CreateDirectory((Split-Path $lockPath)) | Out-Null
    $handle=[IO.File]::Open($lockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    if ((Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash -cne $before.Hash) { throw 'Konfiguracja zmienila sie podczas przygotowania.' }
    $backup=Join-Path $root ('work/studio-bridge/config-backups/'+[Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($backup) | Out-Null
    Copy-Item -LiteralPath $configPath -Destination (Join-Path $backup 'mcp.json')
    $config.mcpServers['morza-bogow-studio']=$entry
    $tempPath=Join-Path $root ('.studio-mcp-'+[Guid]::NewGuid().ToString('N')+'.tmp')
    try {
        [IO.File]::WriteAllText($tempPath,($config | ConvertTo-Json -Depth 100 -WarningAction Stop),[Text.UTF8Encoding]::new($false))
        [IO.File]::Replace($tempPath,$configPath,[System.Management.Automation.Language.NullString]::Value)
    } finally { if(Test-Path -LiteralPath $tempPath){ Remove-Item -LiteralPath $tempPath } }
    @{status='CONFIGURED_RUNTIME_UNVERIFIED';changed=$true;backup=$backup;note='Zachowano inne MCP, hooki, modele i uprawnienia. Serwer nie zostal automatycznie uruchomiony.'} | ConvertTo-Json -Compress
} catch {
    @{status='BLOCKED';reason=$_.Exception.Message} | ConvertTo-Json -Compress
    exit 2
} finally { if($handle){$handle.Dispose()} }
