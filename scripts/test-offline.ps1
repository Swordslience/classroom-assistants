$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$photo = Join-Path $repoRoot 'PhotoTutor/src/bin/Release/net48/拍照解题助教.exe'
$updater = Join-Path $repoRoot 'PhotoTutor/CloudUpdates/bin/Release/net48/课堂助教更新.exe'
$outputRoot = Join-Path $repoRoot 'artifacts/offline-tests'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
if (-not (Test-Path -LiteralPath $photo)) { throw 'Build PhotoTutor first.' }
if (-not (Test-Path -LiteralPath $updater)) { throw 'Build the updater first.' }
$cases = @(
    @{Exe=$photo; Args=@('--formula-boundary-test',(Join-Path $outputRoot 'formula'))},
    @{Exe=$photo; Args=@('--regions-test',(Join-Path $outputRoot 'regions'))},
    @{Exe=$updater; Args=@('--self-test')}
)
foreach ($case in $cases) {
    & $case.Exe @($case.Args)
    if ($LASTEXITCODE -ne 0) { throw "Offline check failed: $($case.Args[0]) (exit $LASTEXITCODE)" }
}
Write-Output 'Offline checks passed. No paid API calls or plugin installation performed.'
