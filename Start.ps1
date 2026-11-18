param([switch]$Tray)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
try {
    $exe = Join-Path $PSScriptRoot 'MarshallCompanion.exe'
    if ($Tray) { Start-Process -FilePath $exe -ArgumentList '--tray' }
    else { Start-Process -FilePath $exe }
} catch {
    [System.Windows.Forms.MessageBox]::Show(('启动失败 / Startup failed: ' + $_.Exception.Message),'Marshall Companion') | Out-Null
}
