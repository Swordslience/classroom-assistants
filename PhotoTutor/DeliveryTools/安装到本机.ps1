param([string]$ProgramDirectory = (Join-Path $PSScriptRoot '程序'), [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\PhotoTutor'), [string]$DesktopDirectory = ([Environment]::GetFolderPath('DesktopDirectory')), [switch]$SkipLegacyConfiguration)
$ErrorActionPreference = 'Stop'
$taskNames = @('拍照解题助教.exe','拍照解题助教.exe.config','DirectShowLib.dll','Newtonsoft.Json.dll','WpfMath.dll','XamlMath.Shared.dll','课堂助教更新.exe')
$taskDestination = [IO.Path]::GetFullPath($Destination)
$taskSource = [IO.Path]::GetFullPath($ProgramDirectory)
if ($taskSource.TrimEnd('\') -eq $taskDestination.TrimEnd('\')) { throw '请从解压的交付包运行安装脚本。' }
$taskManifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot '程序校验.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($taskName in $taskNames) {
    $taskFile = Join-Path $taskSource $taskName
    $taskExpected = $taskManifest.files | Where-Object name -EQ $taskName
    if (-not $taskExpected -or -not (Test-Path -LiteralPath $taskFile -PathType Leaf) -or (Get-FileHash -LiteralPath $taskFile -Algorithm SHA256).Hash -ne $taskExpected.sha256) { throw "交付文件校验失败：$taskName；尚未替换程序。" }
}
$taskExe = Join-Path $taskDestination '拍照解题助教.exe'
if (Test-Path -LiteralPath $taskExe) {
    if ([version](Get-Item -LiteralPath $taskExe).VersionInfo.FileVersion -gt [version]$taskManifest.version) { throw '本机程序比交付包更新，已停止安装。' }
}
foreach ($taskProcess in @(Get-Process -Name '拍照解题助教' -ErrorAction SilentlyContinue)) {
    if ($taskProcess.Path -and ([IO.Path]::GetFullPath($taskProcess.Path) -eq $taskExe)) { throw '请先正常关闭已经安装的拍照解题助教，再运行安装。' }
}
New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
$taskBackup = Join-Path $taskDestination ('安装回退\' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '_' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $taskBackup -Force | Out-Null
$taskChanged = @()
$taskHadOriginal = @{}
$taskShell = New-Object -ComObject WScript.Shell
$taskLinkPath = Join-Path $DesktopDirectory '深度求索.lnk'
$taskLegacyLinkPath = Join-Path $DesktopDirectory '拍照解题助教.lnk'
$taskHadLink = Test-Path -LiteralPath $taskLinkPath
$taskLegacyConfiguration = $null
$taskConfigurationLink = if (Test-Path -LiteralPath $taskLinkPath) { $taskLinkPath } else { $taskLegacyLinkPath }
if ((-not $SkipLegacyConfiguration) -and (Test-Path -LiteralPath $taskConfigurationLink)) {
    $taskOldLink = $taskShell.CreateShortcut($taskConfigurationLink)
    if ($taskOldLink.TargetPath) { $taskLegacyConfiguration = Join-Path (Split-Path -LiteralPath $taskOldLink.TargetPath) '接口配置_仅自用.json' }
}
try {
    foreach ($taskName in $taskNames) {
        $taskFile = Join-Path $taskDestination $taskName
        $taskHadOriginal[$taskName] = Test-Path -LiteralPath $taskFile
        if ($taskHadOriginal[$taskName]) { Copy-Item -LiteralPath $taskFile -Destination (Join-Path $taskBackup $taskName) }
        $taskChanged += $taskName
        Copy-Item -LiteralPath (Join-Path $taskSource $taskName) -Destination ($taskFile + '.install-new') -Force
        if (Test-Path -LiteralPath $taskFile) { [IO.File]::Replace(($taskFile + '.install-new'),$taskFile,(Join-Path $taskBackup ($taskName + '.atomic-old'))) } else { [IO.File]::Move(($taskFile + '.install-new'),$taskFile) }
    }
    foreach ($taskName in $taskNames) {
        $taskExpected = $taskManifest.files | Where-Object name -EQ $taskName
        if ((Get-FileHash -LiteralPath (Join-Path $taskDestination $taskName) -Algorithm SHA256).Hash -ne $taskExpected.sha256) { throw "安装后校验失败：$taskName" }
    }
    $taskConfigTarget = Join-Path $taskDestination '接口配置_仅自用.json'
    if ($taskLegacyConfiguration -and (Test-Path -LiteralPath $taskLegacyConfiguration) -and (-not (Test-Path -LiteralPath $taskConfigTarget))) { Copy-Item -LiteralPath $taskLegacyConfiguration -Destination $taskConfigTarget }
    New-Item -ItemType Directory -Path $DesktopDirectory -Force | Out-Null
    if (Test-Path -LiteralPath $taskLinkPath) { Copy-Item -LiteralPath $taskLinkPath -Destination (Join-Path $taskBackup '原桌面入口.lnk') }
    $taskTemporaryLink = Join-Path $taskDestination ('入口_' + [Guid]::NewGuid().ToString('N') + '.lnk')
    $taskShortcut = $taskShell.CreateShortcut($taskTemporaryLink)
    $taskShortcut.TargetPath = $taskExe
    $taskShortcut.WorkingDirectory = $taskDestination
    $taskShortcut.IconLocation = $taskExe + ',0'
    $taskShortcut.Description = '深度求索 ' + $taskManifest.version
    $taskShortcut.Save()
    Copy-Item -LiteralPath $taskTemporaryLink -Destination $taskLinkPath -Force
    Remove-Item -LiteralPath $taskTemporaryLink
    $taskCheck = $taskShell.CreateShortcut($taskLinkPath)
    if ($taskCheck.TargetPath -ne $taskExe -or -not (Test-Path -LiteralPath $taskExe)) { throw '桌面入口核验失败。' }
    if (Test-Path -LiteralPath $taskLegacyLinkPath) { $taskLegacyLink = $taskShell.CreateShortcut($taskLegacyLinkPath); if ($taskLegacyLink.TargetPath -eq $taskExe) { Remove-Item -LiteralPath $taskLegacyLinkPath -ErrorAction SilentlyContinue } }
    Write-Host ('已安装版本 ' + $taskManifest.version + '，桌面入口已指向固定安装目录。') -ForegroundColor Green
    Write-Host ('安装位置：' + $taskDestination)
    Write-Host ('回退备份：' + $taskBackup)
} catch {
    $taskFailure = $_
    $taskRollbackErrors = @()
    [array]::Reverse($taskChanged)
    foreach ($taskName in $taskChanged) {
        $taskFile = Join-Path $taskDestination $taskName
        try {
            if ($taskHadOriginal[$taskName]) {
                $taskSaved = Join-Path $taskBackup $taskName
                # A locked file may be the reason installation stopped. If it was never
                # replaced, do not let copying it onto itself block all earlier rollback.
                $taskUnchanged = (Test-Path -LiteralPath $taskFile) -and ((Get-FileHash -LiteralPath $taskFile -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $taskSaved -Algorithm SHA256).Hash)
                if (-not $taskUnchanged) { Copy-Item -LiteralPath $taskSaved -Destination $taskFile -Force }
            } elseif (Test-Path -LiteralPath $taskFile) { Remove-Item -LiteralPath $taskFile }
        } catch { $taskRollbackErrors += $taskName }
        try { if (Test-Path -LiteralPath ($taskFile + '.install-new')) { Remove-Item -LiteralPath ($taskFile + '.install-new') } } catch { }
    }
    if (Test-Path -LiteralPath (Join-Path $taskBackup '原桌面入口.lnk')) { Copy-Item -LiteralPath (Join-Path $taskBackup '原桌面入口.lnk') -Destination $taskLinkPath -Force }
    if ((-not $taskHadLink) -and (Test-Path -LiteralPath $taskLinkPath)) { $taskFailedLink = $taskShell.CreateShortcut($taskLinkPath); if ($taskFailedLink.TargetPath -eq $taskExe) { Remove-Item -LiteralPath $taskLinkPath } }
    if ($taskRollbackErrors.Count -gt 0) { throw ('安装未完成；以下文件需要从回退备份恢复：' + ($taskRollbackErrors -join '、') + '。备份目录：' + $taskBackup) }
    throw $taskFailure
}
