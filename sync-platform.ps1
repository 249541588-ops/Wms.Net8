[CmdletBinding()]
param(
    [string]$PlatformRepo,
    [string]$LibDir
)

$ErrorActionPreference = "Stop"

# $PSScriptRoot 在 powershell -File 调用时可能为空，用 $PSCommandPath 兜底（同 pack.ps1 R2）
if (-not $PSScriptRoot) {
    $PSScriptRoot = Split-Path -Parent -Path $PSCommandPath
}
if (-not $PSScriptRoot) {
    $PSScriptRoot = Split-Path -Parent -Path $MyInvocation.MyCommand.Path
}

# 默认值：wms-platform 在兄弟目录（..\wms-platform）
if (-not $PlatformRepo) {
    $PlatformRepo = Join-Path (Split-Path -Parent $PSScriptRoot) "wms-platform"
}
if (-not $LibDir) {
    $LibDir = Join-Path $PSScriptRoot "lib\platform"
}

$sourceArtifacts = Join-Path $PlatformRepo "artifacts"

# 1. 检查底层 artifacts 目录
if (-not (Test-Path $sourceArtifacts)) {
    Write-Host "[sync] Platform artifacts not found at: $sourceArtifacts" -ForegroundColor Red
    Write-Host "[sync] 请先在 wms-platform 目录运行 .\pack.ps1 打包底层 DLL。" -ForegroundColor Yellow
    Write-Host "[sync] 或用 -PlatformRepo 参数指定 wms-platform 的位置。" -ForegroundColor Yellow
    exit 1
}

$sourceVersionFile = Join-Path $sourceArtifacts "version.json"
if (-not (Test-Path $sourceVersionFile)) {
    Write-Host "[sync] version.json not found in artifacts: $sourceVersionFile" -ForegroundColor Red
    exit 1
}

$sourceVersion = Get-Content $sourceVersionFile -Raw | ConvertFrom-Json

# 2. 确保 lib/platform 目录存在
if (-not (Test-Path $LibDir)) {
    New-Item -ItemType Directory -Path $LibDir -Force | Out-Null
    Write-Host "[sync] Created lib dir: $LibDir"
}

# 3. 对比版本，一致则 skip
$destVersionFile = Join-Path $LibDir "version.json"
if (Test-Path $destVersionFile) {
    $destVersion = Get-Content $destVersionFile -Raw | ConvertFrom-Json
    if ($sourceVersion.version -eq $destVersion.version `
        -and $sourceVersion.commit -eq $destVersion.commit `
        -and $sourceVersion.buildTime -eq $destVersion.buildTime) {
        Write-Host "[sync] Already up to date (v$($sourceVersion.version), build $($sourceVersion.buildTime))." -ForegroundColor Green
        return
    }
}

# 4. 同步
Write-Host "[sync] Updating platform DLLs to v$($sourceVersion.version)..." -ForegroundColor Cyan
Write-Host "[sync]   buildTime: $($sourceVersion.buildTime)"
Write-Host "[sync]   commit:    $($sourceVersion.commit)"
Write-Host "[sync]   branch:    $($sourceVersion.branch)"
Write-Host ""

# 先清理旧 DLL（避免删除底层项目后旧 DLL 残留）
Get-ChildItem "$LibDir\*.dll" -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem "$LibDir\*.pdb" -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem "$LibDir\*.xml" -ErrorAction SilentlyContinue | Remove-Item -Force

# 复制新 DLL + version.json
$dllCount = 0
Get-ChildItem "$sourceArtifacts\*.dll" | ForEach-Object {
    Copy-Item $_.FullName -Destination $LibDir -Force
    Write-Host "  -> $($_.Name)"
    $dllCount++
}

Get-ChildItem "$sourceArtifacts\*.pdb" -ErrorAction SilentlyContinue | ForEach-Object {
    Copy-Item $_.FullName -Destination $LibDir -Force
}

Copy-Item $sourceVersionFile -Destination $LibDir -Force

Write-Host ""
Write-Host "[sync] Done. $dllCount DLL(s) synced to $LibDir" -ForegroundColor Green
