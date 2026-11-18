param([switch]$Disable, [switch]$Quiet)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
try {
    $startup = [Environment]::GetFolderPath('Startup')
    if (-not $startup) { throw '找不到启动文件夹 / Startup folder unavailable.' }
    $linkPath = Join-Path $startup 'Marshall Companion.lnk'
    $oldLink = Join-Path $startup 'Acton Companion.lnk'
    if ($Disable) {
        foreach ($link in @($linkPath,$oldLink)) { if (Test-Path -LiteralPath $link) { Remove-Item -LiteralPath $link -Force } }
        if (-not $Quiet) { [Windows.Forms.MessageBox]::Show('已关闭自启动。当前程序仍会运行，可从托盘退出。 / Login startup disabled. Exit the running app from its tray menu.','Marshall Companion') | Out-Null }
    } else {
        $installDir = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MarshallCompanion\App'
        [IO.Directory]::CreateDirectory($installDir) | Out-Null
        foreach ($name in @('MarshallCompanion.exe','MarshallCompanion.exe.config','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll','index.html','emblem.png','Start.ps1','Start.cmd','Autostart.ps1','Enable-Autostart.cmd','Disable-Autostart.cmd')) {
            $source = Join-Path $PSScriptRoot $name
            $target = Join-Path $installDir $name
            if ([IO.Path]::GetFullPath($source) -ne [IO.Path]::GetFullPath($target)) { Copy-Item -LiteralPath $source -Destination $target -Force }
        }
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($linkPath)
        $shortcut.TargetPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
        $shortcut.Arguments = '-NoLogo -NoProfile -STA -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + (Join-Path $installDir 'Start.ps1') + '" -Tray'
        $shortcut.WorkingDirectory = $installDir
        $shortcut.Save()
        if (Test-Path -LiteralPath $oldLink) { Remove-Item -LiteralPath $oldLink -Force }
        if (-not $Quiet) { [Windows.Forms.MessageBox]::Show('已开启登录自启动。下次登录在托盘运行。 / Login startup enabled. The app will start in the tray.','Marshall Companion') | Out-Null }
    }
} catch {
    if ($Quiet) { [Console]::Error.WriteLine($_.Exception.Message) }
    else { [Windows.Forms.MessageBox]::Show(('设置失败 / Setup failed: ' + $_.Exception.Message),'Marshall Companion') | Out-Null }
    exit 1
}
exit 0
