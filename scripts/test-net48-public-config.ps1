# Run with 32-bit Windows PowerShell on Windows after scripts/build.ps1.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outRoot = Join-Path $repoRoot 'artifacts/net48-public-config-tests'
New-Item -ItemType Directory -Path $outRoot -Force | Out-Null
$flags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$results = [Collections.Generic.List[object]]::new()
function Check([string]$Name,[scriptblock]$Body) {
    try { & $Body; $results.Add(@{name=$Name;passed=$true}) }
    catch { $results.Add(@{name=$Name;passed=$false;error=$_.Exception.GetType().Name}); throw }
}
function Reject([scriptblock]$Body) {
    $rejected=$false
    try { & $Body | Out-Null } catch { $rejected=$true }
    if (-not $rejected) { throw 'Expected rejection.' }
}
$saved=@{}
foreach($name in @('CLASSROOM_SERVICE_URL','CLASSROOM_UPDATE_SERVER_URL','CLASSROOM_UPDATE_PUBLIC_KEY_PATH')) {
    $saved[$name]=[Environment]::GetEnvironmentVariable($name)
    [Environment]::SetEnvironmentVariable($name,$null)
}
try {
    $photoDir=Join-Path $repoRoot 'PhotoTutor/src/bin/Release/net48'
    [Reflection.Assembly]::LoadFrom((Join-Path $photoDir 'Newtonsoft.Json.dll')) | Out-Null
    $photo=[Reflection.Assembly]::LoadFrom((Join-Path $photoDir '拍照解题助教.exe'))
    $updater=[Reflection.Assembly]::LoadFrom((Join-Path $repoRoot 'PhotoTutor/CloudUpdates/bin/Release/net48/课堂助教更新.exe'))
    Check 'photo default API credentials are blank without loading any user config' {
        $settings=[Activator]::CreateInstance($photo.GetType('PhotoTutor.Settings'),$true)
        foreach($name in @('DeepSeekApiKey','DoubaoApiKey','VoiceApiKey')) {
            if ($settings.GetType().GetProperty($name,$flags).GetValue($settings)) { throw 'Unexpected default credential.' }
        }
    }
    foreach($item in @(@{Assembly=$photo;Name='photo'})) {
        $method=$item.Assembly.GetType('ScienceSupport.CloudMath').GetMethod('FormulaEndpoint',$flags)
        Check "$($item.Name) formula service is disabled without own configuration" { Reject { $method.Invoke($null,@()) } }
        [Environment]::SetEnvironmentVariable('CLASSROOM_SERVICE_URL','http://example.invalid/')
        Check "$($item.Name) rejects non-HTTPS formula endpoints" { Reject { $method.Invoke($null,@()) } }
        [Environment]::SetEnvironmentVariable('CLASSROOM_SERVICE_URL','https://example.invalid/')
        Check "$($item.Name) accepts own HTTPS endpoint without sending a request" {
            if ($method.Invoke($null,@()) -ne 'https://example.invalid/api/formula') { throw 'Wrong endpoint.' }
        }
        [Environment]::SetEnvironmentVariable('CLASSROOM_SERVICE_URL',$null)
    }
    $program=$updater.GetType('ClassroomUpdater.Program')
    $hostProperty=$program.GetProperty('Host',$flags)
    Check 'updater has no author-host default' { if ($hostProperty.GetValue($null)) { throw 'Unexpected default update host.' } }
    [Environment]::SetEnvironmentVariable('CLASSROOM_UPDATE_SERVER_URL','http://example.invalid/')
    Check 'updater rejects insecure source' { Reject { $hostProperty.GetValue($null) } }
    [Environment]::SetEnvironmentVariable('CLASSROOM_UPDATE_SERVER_URL','https://example.invalid/')
    Check 'updater accepts own HTTPS source without network' { if ($hostProperty.GetValue($null) -ne 'https://example.invalid') { throw 'Wrong update host.' } }
    $verify=$program.GetMethod('Verify',$flags)
    Check 'updater rejects manifest without configured verification key' { Reject { $verify.Invoke($null,@('{"payload":"{}","signature":"AA=="}')) } }
    $rsa=[Security.Cryptography.RSACryptoServiceProvider]::new(2048)
    try {
        $publicFile=Join-Path $outRoot 'test-public.xml'
        [IO.File]::WriteAllText($publicFile,$rsa.ToXmlString($false))
        [Environment]::SetEnvironmentVariable('CLASSROOM_UPDATE_PUBLIC_KEY_PATH',$publicFile)
        $payload='{"photo":{"version":"1.6.4"}}'
        $signature=[Convert]::ToBase64String($rsa.SignData([Text.Encoding]::UTF8.GetBytes($payload),'SHA256'))
        $envelope=@{payload=$payload;signature=$signature}|ConvertTo-Json -Compress
        Check 'own correctly signed manifest verifies' {
            $verified=$verify.Invoke($null,@([string]$envelope))
            if ($verified['photo']['version'] -ne '1.6.4') { throw 'Signed payload mismatch.' }
        }
        Check 'tampering fails verification' {
            $tampered=@{payload=$payload.Replace('1.6.4','9.9.9');signature=$signature}|ConvertTo-Json -Compress
            Reject { $verify.Invoke($null,@([string]$tampered)) }
        }
    } finally { $rsa.Dispose() }
} finally {
    foreach($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name,$saved[$name]) }
    $results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outRoot 'results.json') -Encoding utf8
}
Write-Output "Public configuration checks passed: $($results.Count). No network requests or user config reads."
