# Registers the Helper as the extension's native messaging host for the current user, in Chrome and Edge, for development.
# The installer does this for every user (HKLM); this one (HKCU) wins over it. -Unregister removes it again.
param(
    [string]$HelperPath = "$PSScriptRoot\..\src\Skjermvette.Agent.Helper\bin\Debug\net10.0-windows\Skjermvette.Agent.Helper.exe",
    [switch]$Unregister
)
$ErrorActionPreference = "Stop"

$name = "no.skjermvette.agent"
# From the "key" in static/manifest.json.
$extensionId = "dcobicgloeifebalmlahclbcedjnaido"
$keys = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\$name", "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\$name"
$manifest = Join-Path $env:LOCALAPPDATA "Skjermvette\Agent\$name.json"

if ($Unregister) {
    foreach ($key in $keys) { if (Test-Path $key) { Remove-Item $key } }
    if (Test-Path $manifest) { Remove-Item $manifest }
    "Unregistered $name."
    return
}

$helper = (Resolve-Path $HelperPath).Path
New-Item -ItemType Directory -Force (Split-Path $manifest) | Out-Null
$json = @{ name = $name; description = "Skjermvette"; path = $helper; type = "stdio"; allowed_origins = @("chrome-extension://$extensionId/") } | ConvertTo-Json
# Without a byte order mark, which Set-Content in Windows PowerShell would add.
[IO.File]::WriteAllText($manifest, $json)
foreach ($key in $keys) {
    if (-not (Test-Path $key)) { New-Item $key -Force | Out-Null }
    Set-Item $key $manifest
}
"Registered $name for $helper."
