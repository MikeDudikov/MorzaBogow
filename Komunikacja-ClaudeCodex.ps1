#requires -Version 7.5
[CmdletBinding()]
param(
    [ValidateSet('Read','Send')][string]$Action='Read',
    [Parameter(Mandatory)][string]$Python,
    [ValidateSet('claude','codex')][string]$Endpoint='codex',
    [ValidateSet('HELLO','STATUS','BLOCKED','HANDOFF','ACK')][string]$Kind='STATUS',
    [string]$TaskId,
    [string]$Text,
    [string]$ReplyTo,
    [string]$MessageId,
    [string]$ProjectRoot=$PSScriptRoot
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
try {
    $arguments=@('-B',(Join-Path $PSScriptRoot 'Tools/studio_bridge/studio_mcp.py'),'--root',$ProjectRoot,'--endpoint',$Endpoint,'call')
    if($Action -eq 'Read') { $arguments+=@('--tool','studio_receive') }
    else {
        if(!$TaskId -or !$Text) { throw 'Send wymaga TaskId i Text; ACK takze ReplyTo.' }
        $payload=@{kind=$Kind;taskId=$TaskId;text=$Text}
        if($ReplyTo){$payload.replyTo=$ReplyTo}
        if($MessageId){$payload.messageId=$MessageId}
        $arguments+=@('--tool','studio_send','--arguments',($payload | ConvertTo-Json -Compress))
    }
    & $Python @arguments
    if($LASTEXITCODE -ne 0){exit 2}
} catch {
    @{status='BLOCKED';reason=$_.Exception.Message} | ConvertTo-Json -Compress
    exit 2
}
