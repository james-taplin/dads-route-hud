param([Parameter(Mandatory = $true)][string]$GamePath)
$ErrorActionPreference = 'Stop'
$taskDotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $taskDotnet)) { $taskDotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$taskEnvironment = @{
    DOTNET_CLI_HOME = (Join-Path $PSScriptRoot 'build\dotnet-home')
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_NOLOGO = '1'
}
$taskPrevious = @{}
foreach ($taskKey in $taskEnvironment.Keys) {
    $taskPrevious[$taskKey] = [Environment]::GetEnvironmentVariable($taskKey, 'Process')
    [Environment]::SetEnvironmentVariable($taskKey, $taskEnvironment[$taskKey], 'Process')
}
function Invoke-CheckedDotnet([string[]]$Arguments) {
    & $taskDotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet validation failed: $($Arguments -join ' ')" }
}
function Invoke-ProjectCheck([string]$Project, [string[]]$ToolArguments = @()) {
    Invoke-CheckedDotnet -Arguments @('restore',$Project,'--ignore-failed-sources','-p:NuGetAudit=false')
    $taskRunArguments = @('run','--project',$Project,'--configuration','Release','--no-restore','-p:UseSharedCompilation=false')
    if ($ToolArguments.Count) { $taskRunArguments += '--'; $taskRunArguments += $ToolArguments }
    Invoke-CheckedDotnet -Arguments $taskRunArguments
}
Push-Location $PSScriptRoot
try {
    & (Join-Path $PSScriptRoot 'Build.ps1') -GamePath $GamePath
    $taskSuites = @('DrivingMath','RouteCore','RouteService','NativeGeometry','NativeNames','NativeSigns','OptionalSignals','CarList','NativeTelemetry')
    foreach ($taskSuite in $taskSuites) {
        Invoke-ProjectCheck -Project "tests\$taskSuite.Tests.csproj"
    }
    Invoke-ProjectCheck -Project 'tools\OptionalContracts\OptionalContracts.csproj' -ToolArguments @(
        (Join-Path $GamePath 'Mods'),(Join-Path $PSScriptRoot 'build\optional-contract-audit.json'))
    & (Join-Path $PSScriptRoot 'Package.ps1') -GamePath $GamePath
    Invoke-ProjectCheck -Project 'tools\ArtifactAudit\ArtifactAudit.csproj' -ToolArguments @($PSScriptRoot)
    & (Join-Path $PSScriptRoot 'tests\Install.Tests.ps1')
    & git diff --check
    if ($LASTEXITCODE -ne 0) { throw 'git diff --check failed.' }
    $taskInfo = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Raw | ConvertFrom-Json
    [ordered]@{
        version = $taskInfo.Version
        branch = (& git branch --show-current)
        head = (& git rev-parse HEAD)
        suites = $taskSuites
        packageSHA256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot "artifacts\DadsRouteHUD-$($taskInfo.Version).zip")).Hash
        allChecksPassed = $true
        scope = 'Local compile, production-boundary regressions, metadata/ZIP audit and isolated installer fixtures. No real installation, target DLL execution in audits, game launch or runtime acceptance.'
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'build\validation-receipt.json') -Encoding utf8
    Write-Output 'Validation complete. Runtime/visual acceptance remains separate; actual installation was not changed.'
} finally {
    Pop-Location
    foreach ($taskKey in $taskPrevious.Keys) { [Environment]::SetEnvironmentVariable($taskKey, $taskPrevious[$taskKey], 'Process') }
}
