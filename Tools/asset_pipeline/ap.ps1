# Morza Bogów asset pipeline from Windows: runs ap.py with WSL Python (Windows has only the Store stub)
# and passes the service variables through WSLENV. Only variable names are listed here; the values come
# from the Windows environment of the caller and never reach the repository.
#   Tools\asset_pipeline\ap.ps1 capabilities
#   Tools\asset_pipeline\ap.ps1 create ArtWorkspace\briefs\ship_ram_blockout.json --run
$ErrorActionPreference = 'Stop'
$pass = 'MESHY_API_KEY/u:COMFYUI_URL/u:BLENDER_PATH/p:UNITY_PATH/p'
$env:WSLENV = if ($env:WSLENV) { "$($env:WSLENV):$pass" } else { $pass }
$script = (& wsl.exe -e wslpath -a (($PSScriptRoot -replace '\\', '/') + '/ap.py')).Trim()
# Windows paths in arguments: absolute ones through wslpath, relative ones with forward slashes.
$arguments = @($args | ForEach-Object {
    if ($_ -match '^[A-Za-z]:[\\/]') { (& wsl.exe -e wslpath -a ($_ -replace '\\', '/')).Trim() }
    elseif ($_ -match '\\') { $_ -replace '\\', '/' }
    else { $_ }
})
& wsl.exe --cd ((Get-Location).Path) -e python3 $script @arguments
exit $LASTEXITCODE
