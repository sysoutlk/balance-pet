# 把桌宠加入 / 移出当前用户的开机自启（HKCU Run，不写入系统目录）。
#
#   .\install-autostart.ps1             加入开机自启
#   .\install-autostart.ps1 -Uninstall  取消开机自启
param(
    [switch]$Uninstall
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exe = Join-Path $root 'dsh-pet.exe'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if ($Uninstall) {
    Remove-ItemProperty -Path $runKey -Name 'DshPet' -ErrorAction SilentlyContinue
    Write-Host '已取消开机自启。'
    return
}
if (-not (Test-Path -LiteralPath $exe)) {
    throw "找不到 $exe，请先在仓库根目录运行 build.cmd"
}
Set-ItemProperty -Path $runKey -Name 'DshPet' -Value ('"' + $exe + '"')
Write-Host "已加入开机自启: $exe"
