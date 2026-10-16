# 在桌面创建（或更新）「余额桌宠」快捷方式，指向仓库根目录的 balance-pet.exe。
param(
    [string]$Name = '余额桌宠'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exe = Join-Path $root 'balance-pet.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "找不到 $exe，请先在仓库根目录运行 build.cmd"
}
$desktop = [Environment]::GetFolderPath('Desktop')
$linkPath = Join-Path $desktop ($Name + '.lnk')
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($linkPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $root
$shortcut.IconLocation = $exe + ',0'
$shortcut.Description = '多平台余额桌面宠物'
$shortcut.Save()
Write-Host "已创建快捷方式: $linkPath"
