param([string]$ProgramDirectory = (Join-Path $env:LOCALAPPDATA 'Programs\PhotoTutor'), [string]$OutputDirectory = ([Environment]::GetFolderPath('DesktopDirectory')))
$ErrorActionPreference = 'Stop'
$taskFolder = Join-Path $OutputDirectory ('拍照助教诊断_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $taskFolder -Force | Out-Null
$taskExe = Join-Path $ProgramDirectory '拍照解题助教.exe'
$taskLinkPath = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) '深度求索.lnk'
if (-not (Test-Path -LiteralPath $taskLinkPath)) { $taskLinkPath = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) '拍照解题助教.lnk' }
$taskTargetExists = $false
$taskTargetVersion = $null
if (Test-Path -LiteralPath $taskLinkPath) {
    $taskShell = New-Object -ComObject WScript.Shell
    $taskLink = $taskShell.CreateShortcut($taskLinkPath)
    $taskTargetExists = Test-Path -LiteralPath $taskLink.TargetPath
    if ($taskTargetExists) { $taskTargetVersion = (Get-Item -LiteralPath $taskLink.TargetPath).VersionInfo.FileVersion }
}
$taskRecord = [ordered]@{collectedAt=(Get-Date).ToString('o');windowsVersion=[Environment]::OSVersion.VersionString;os64Bit=[Environment]::Is64BitOperatingSystem;ocrLanguages=@();desktopShortcutExists=(Test-Path -LiteralPath $taskLinkPath);shortcutTargetExists=$taskTargetExists;shortcutTargetVersion=$taskTargetVersion;programExists=(Test-Path -LiteralPath $taskExe);programVersion=$null;programSha256=$null;signatureStatus=$null;applicationEvents=@();defenderEvents=@();logAccessErrors=@();containsPhotos=$false;containsSettings=$false}
try { $taskRecord.ocrLanguages = @([Windows.Media.Ocr.OcrEngine,Windows.Foundation,ContentType=WindowsRuntime]::AvailableRecognizerLanguages | Select-Object -ExpandProperty LanguageTag) } catch { $taskRecord.logAccessErrors += 'Windows OCR capabilities' }
if (Test-Path -LiteralPath $taskExe) {
    $taskRecord.programVersion = (Get-Item -LiteralPath $taskExe).VersionInfo.FileVersion
    $taskRecord.programSha256 = (Get-FileHash -LiteralPath $taskExe -Algorithm SHA256).Hash
    $taskRecord.signatureStatus = [string](Get-AuthenticodeSignature -LiteralPath $taskExe).Status
}
foreach ($taskLog in @('Application','Microsoft-Windows-Windows Defender/Operational')) {
    try {
        $taskIds = if ($taskLog -eq 'Application') { @(1000,1001,1026) } else { @(1116,1117,1121) }
        $taskEvents = @(Get-WinEvent -FilterHashtable @{LogName=$taskLog;StartTime=(Get-Date).AddDays(-7);Id=$taskIds} -MaxEvents 500 -ErrorAction SilentlyContinue | Where-Object {$_.ToXml() -match '拍照解题助教|PhotoTutor|课堂助教更新'} | Select-Object -First 25)
        $taskRows = @(foreach ($taskEvent in $taskEvents) {
            $taskSafeData = [ordered]@{}
            $taskXml = [xml]$taskEvent.ToXml()
            foreach ($taskItem in $taskXml.Event.EventData.Data) {
                if ($taskItem.Name -in @('AppName','AppVersion','ModuleName','ModuleVersion','ExceptionCode','FaultingOffset','Threat Name')) { $taskSafeData[$taskItem.Name] = [IO.Path]::GetFileName([string]$taskItem.'#text') }
            }
            [ordered]@{time=$taskEvent.TimeCreated.ToString('o');eventId=$taskEvent.Id;recordId=$taskEvent.RecordId;provider=$taskEvent.ProviderName;data=$taskSafeData}
        })
        if ($taskLog -eq 'Application') { $taskRecord.applicationEvents=$taskRows } else { $taskRecord.defenderEvents=$taskRows }
    } catch { $taskRecord.logAccessErrors += $taskLog }
}
$taskRuntime = Join-Path $env:LOCALAPPDATA 'PhotoTutor\diagnostics'
if (Test-Path -LiteralPath $taskRuntime) { Get-ChildItem -LiteralPath $taskRuntime -File | Where-Object Name -In @('runtime.jsonl','runtime.jsonl.1') | Copy-Item -Destination $taskFolder }
$taskRecord | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskFolder '故障诊断.json') -Encoding UTF8
Compress-Archive -LiteralPath $taskFolder -DestinationPath ($taskFolder + '.zip')
Write-Host ('诊断已导出：' + $taskFolder + '.zip') -ForegroundColor Green
Write-Host '只收集版本、入口状态和故障元数据；不收集题图、学生材料和接口配置。'
