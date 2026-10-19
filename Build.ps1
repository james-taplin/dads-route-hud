param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Derail Valley',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskBuildRoot = Join-Path $taskRoot 'build'
New-Item -ItemType Directory -Path $taskBuildRoot -Force | Out-Null
$taskDotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $taskDotnet)) { $taskDotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$taskProject = Join-Path $taskRoot 'HeadsUpDisplay.csproj'
$GamePath = (Resolve-Path -LiteralPath $GamePath -ErrorAction Stop).Path
$taskManagedRoot = Join-Path $GamePath 'DerailValley_Data\Managed'
if (-not (Test-Path -LiteralPath (Join-Path $taskManagedRoot 'Assembly-CSharp.dll'))) {
    throw "Derail Valley managed assemblies not found in $taskManagedRoot"
}
if (-not (Test-Path -LiteralPath (Join-Path $taskRoot '.submodules\QuantitiesNet\Quantity.cs'))) {
    throw 'QuantitiesNet source is missing. Run git submodule update --init --recursive.'
}
$taskBuildEnvironment = @{
    DOTNET_CLI_HOME = (Join-Path $taskBuildRoot 'dotnet-home')
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_NOLOGO = '1'
    DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
    DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    NUGET_HTTP_CACHE_PATH = (Join-Path $taskBuildRoot 'nuget-http-cache')
    NUGET_PACKAGES = $(if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' })
}
$taskPreviousEnvironment = @{}
foreach ($taskKey in $taskBuildEnvironment.Keys) {
    $taskPreviousEnvironment[$taskKey] = [Environment]::GetEnvironmentVariable($taskKey, 'Process')
    [Environment]::SetEnvironmentVariable($taskKey, $taskBuildEnvironment[$taskKey], 'Process')
}
Push-Location $taskRoot
try {
    $taskSdkOutput = & $taskDotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'The .NET 8 SDK is required; see global.json.' }
    $taskSdk = ($taskSdkOutput | Out-String).Trim()
    & $taskDotnet build $taskProject --configuration $Configuration --nologo --verbosity minimal --ignore-failed-sources '-p:NuGetAudit=false' '-p:UseSharedCompilation=false' "-p:GamePath=$GamePath" 2>&1 |
        Tee-Object -FilePath (Join-Path $taskBuildRoot 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "Build failed. See $taskBuildRoot\build.log" }
} finally {
    Pop-Location
    foreach ($taskKey in $taskPreviousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($taskKey, $taskPreviousEnvironment[$taskKey], 'Process')
    }
}
$taskOutput = Join-Path $taskRoot "bin\$Configuration\net48"
$taskFiles = @('DadsRouteHUD.dll', 'QuantitiesNet.dll')
foreach ($taskFile in $taskFiles) {
    $taskSource = Join-Path $taskOutput $taskFile
    if (-not (Test-Path -LiteralPath $taskSource)) { throw "Build did not produce $taskSource" }
    Copy-Item -LiteralPath $taskSource -Destination (Join-Path $taskBuildRoot $taskFile) -Force
}
$taskReceipt = [ordered]@{
    sdk = $taskSdk
    configuration = $Configuration
    gamePath = (Resolve-Path -LiteralPath $GamePath).Path
    outputs = @($taskFiles | ForEach-Object { Get-FileHash -LiteralPath (Join-Path $taskBuildRoot $_) -Algorithm SHA256 | Select-Object Path, Hash })
    sources = @(Get-ChildItem -LiteralPath $taskRoot -Recurse -File -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '\\(bin|obj|build|lib|\.git)\\' } |
        Sort-Object FullName | ForEach-Object { Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256 | Select-Object Path, Hash })
    references = @(Get-Content -LiteralPath (Join-Path $taskBuildRoot 'resolved-references.txt') |
        Sort-Object -Unique | ForEach-Object { Get-FileHash -LiteralPath $_ -Algorithm SHA256 | Select-Object Path, Hash })
    scope = 'Local build only. No game launch, installation, upload or publication.'
}
$taskReceipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskBuildRoot 'build-receipt.json') -Encoding utf8
Write-Output "Built $taskBuildRoot\DadsRouteHUD.dll and QuantitiesNet.dll"
