# Calls one tool of the official Meshy MCP server (@meshy-ai/meshy-mcp-server) over stdio, for sessions that do not
# have the server loaded as native tools. The key is read inside the server's process from the Windows user variable
# MESHY_API_KEY; it is never passed on the command line, printed or written anywhere by this script.
#
#   pwsh Tools/meshy_mcp/meshy-mcp.ps1 -Tool meshy_check_balance
#   pwsh Tools/meshy_mcp/meshy-mcp.ps1 -Tool meshy_get_task_status -Arguments '{"task_id":"...","task_type":"text-to-3d"}'
#   pwsh Tools/meshy_mcp/meshy-mcp.ps1 -List            # tool names and their input schemas
#
# Billable tools spend Meshy credits exactly like the REST API. The result text of the tool is printed as returned.
param(
    [string]$Tool,
    [string]$Arguments = '{}',
    [switch]$List,
    [int]$TimeoutSeconds = 600,
    [string]$Version = '0.6.10'
)
$ErrorActionPreference = 'Stop'
if (-not $List -and -not $Tool) { throw 'Podaj -Tool <nazwa> albo -List.' }

$psi = [System.Diagnostics.ProcessStartInfo]::new('pwsh')
foreach ($a in @('-NoLogo', '-NoProfile', '-NonInteractive', '-Command',
        "`$env:MESHY_API_KEY = [Environment]::GetEnvironmentVariable('MESHY_API_KEY', 'User'); npx -y @meshy-ai/meshy-mcp-server@$Version")) {
    $psi.ArgumentList.Add($a)
}
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.UseShellExecute = $false
$psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
$server = [System.Diagnostics.Process]::Start($psi)
$null = $server.StandardError.ReadToEndAsync()   # drained so the server never blocks on a full stderr pipe

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$pending = $null
function Send([string]$json) { $server.StandardInput.WriteLine($json); $server.StandardInput.Flush() }
function Receive([int]$id) {
    while ((Get-Date) -lt $deadline) {
        if ($null -eq $script:pending) { $script:pending = $server.StandardOutput.ReadLineAsync() }
        if (-not $script:pending.Wait(1000)) { continue }
        $line = $script:pending.Result
        $script:pending = $null
        if ($null -eq $line) { throw 'Serwer Meshy MCP zakończył pracę bez odpowiedzi.' }
        if (-not $line.StartsWith('{')) { continue }
        $message = $line | ConvertFrom-Json -Depth 64
        if ($message.PSObject.Properties.Name -contains 'id' -and $message.id -eq $id) { return $message }
    }
    throw "Brak odpowiedzi serwera Meshy MCP w $TimeoutSeconds s."
}

try {
    Send '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"morza-bogow","version":"1"}}}'
    $null = Receive 1
    Send '{"jsonrpc":"2.0","method":"notifications/initialized"}'
    if ($List) {
        Send '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
        $answer = Receive 2
        foreach ($t in $answer.result.tools) {
            "== $($t.name)"
            ($t.inputSchema | ConvertTo-Json -Depth 16 -Compress)
        }
        exit 0
    }
    $call = @{ jsonrpc = '2.0'; id = 2; method = 'tools/call'; params = @{ name = $Tool; arguments = ($Arguments | ConvertFrom-Json -Depth 64) } }
    Send ($call | ConvertTo-Json -Depth 64 -Compress)
    $answer = Receive 2
    if ($answer.error) { Write-Output ("BŁĄD MCP: " + ($answer.error | ConvertTo-Json -Depth 16 -Compress)); exit 1 }
    foreach ($part in $answer.result.content) { if ($part.type -eq 'text') { $part.text } }
    if ($answer.result.isError) { exit 1 }
}
finally {
    try { $server.Kill($true) } catch {}
}
