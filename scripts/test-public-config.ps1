# Run with PowerShell 7 on Windows after scripts/build.ps1.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$legacyShell = Join-Path $env:SystemRoot 'SysWOW64/WindowsPowerShell/v1.0/powershell.exe'
& $legacyShell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'test-net48-public-config.ps1')
if ($LASTEXITCODE -ne 0) { throw 'The x86 .NET Framework configuration checks failed.' }
$native=[Reflection.Assembly]::LoadFrom((Join-Path $repoRoot 'Native/SeewoPlugin/bin/Release/net6.0-windows/ClassroomAI.Seewo.dll'))
$flags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$results=[Collections.Generic.List[object]]::new()
function Check([string]$Name,[scriptblock]$Body) {
    try { & $Body; $results.Add(@{name=$Name;passed=$true}) }
    catch { $results.Add(@{name=$Name;passed=$false;error=$_.Exception.GetType().Name}); throw }
}
function Reject([scriptblock]$Body) {
    $rejected=$false
    try { & $Body | Out-Null } catch { $rejected=$true }
    if (-not $rejected) { throw 'Expected rejection.' }
}
$saved=[Environment]::GetEnvironmentVariable('CLASSROOM_SERVICE_URL')
try {
    [Environment]::SetEnvironmentVariable('CLASSROOM_SERVICE_URL',$null)
    Check 'Seewo default API credentials are blank without loading any user config' {
        $config=[Activator]::CreateInstance($native.GetType('ClassroomAI.Seewo.Connection'),$true)
        foreach($name in @('DeepSeekApiKey','ApiKey','VoiceApiKey')) {
            if ($config.GetType().GetProperty($name,$flags).GetValue($config)) { throw 'Unexpected default credential.' }
        }
    }
    $method=$native.GetType('ScienceSupport.CloudMath').GetMethod('FormulaEndpoint',$flags)
    Check 'Seewo formula service disabled without own configuration' { Reject { $method.Invoke($null,@()) } }
    [Environment]::SetEnvironmentVariable('CLASSROOM_SERVICE_URL','http://example.invalid/')
    Check 'Seewo rejects non-HTTPS formula endpoints' { Reject { $method.Invoke($null,@()) } }
    [Environment]::SetEnvironmentVariable('CLASSROOM_SERVICE_URL','https://example.invalid/')
    Check 'Seewo accepts own HTTPS endpoint without sending a request' {
        if ($method.Invoke($null,@()) -ne 'https://example.invalid/api/formula') { throw 'Wrong endpoint.' }
    }
} finally { [Environment]::SetEnvironmentVariable('CLASSROOM_SERVICE_URL',$saved) }
$outRoot=Join-Path $repoRoot 'artifacts/public-config-tests'
New-Item -ItemType Directory -Path $outRoot -Force | Out-Null
$legacyResults=Get-Content -LiteralPath (Join-Path $repoRoot 'artifacts/net48-public-config-tests/results.json') -Raw | ConvertFrom-Json
$all=@($legacyResults)+@($results.ToArray())
$all | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outRoot 'results.json') -Encoding utf8
Write-Output "Public configuration checks passed: $($all.Count). No network requests or user config reads."