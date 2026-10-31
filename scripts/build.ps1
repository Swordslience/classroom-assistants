param(
    [switch]$PhotoOnly,
    [string]$WindowsMetadataPath = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $repoRoot
try {
    $extra = @()
    if ($WindowsMetadataPath) {
        if (-not (Test-Path -LiteralPath $WindowsMetadataPath -PathType Leaf)) { throw 'WindowsMetadataPath does not exist.' }
        $extra += "-p:WindowsMetadataPath=$WindowsMetadataPath"
    }
    & dotnet build PhotoTutor/CloudUpdates/CloudUpdater.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Updater build failed.' }
    & dotnet build PhotoTutor/src/PhotoTutor.csproj -c Release --nologo @extra
    if ($LASTEXITCODE -ne 0) { throw 'PhotoTutor build failed.' }
    if (-not $PhotoOnly) {
        & dotnet build Native/SeewoPlugin/CompileOnly.csproj -c Release --nologo -p:IncludePrivateDeployment=false
        if ($LASTEXITCODE -ne 0) { throw 'Seewo build failed. Check that Seewo and its SDK dependencies are installed.' }
    }
} finally { Pop-Location }
