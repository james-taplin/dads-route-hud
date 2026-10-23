param([string]$PackagePath)
$ErrorActionPreference = 'Stop'
$taskRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $PackagePath) {
    $taskCurrentInfo = Get-Content -LiteralPath (Join-Path $taskRepo 'info.json') -Raw | ConvertFrom-Json
    $PackagePath = Join-Path $taskRepo "artifacts\DadsRouteHUD-$($taskCurrentInfo.Version).zip"
}
$taskFixture = Join-Path $taskRepo ('build\install-tests-' + [Guid]::NewGuid().ToString('N'))
$taskFixtureSource = Join-Path $taskFixture 'source'
New-Item -ItemType Directory -Path (Join-Path $taskFixtureSource 'artifacts') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRepo 'Install.ps1') -Destination (Join-Path $taskFixtureSource 'Install.ps1')
Add-Type -AssemblyName System.IO.Compression
$taskInputZip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($PackagePath))
try {
    $taskInfoReader = [IO.StreamReader]::new($taskInputZip.GetEntry('DadsRouteHUD/info.json').Open())
    try { $taskInfoText = $taskInfoReader.ReadToEnd() } finally { $taskInfoReader.Dispose() }
} finally { $taskInputZip.Dispose() }
$taskInfo = $taskInfoText | ConvertFrom-Json
$taskVersion = $taskInfo.Version
$taskFixturePackage = Join-Path $taskFixtureSource "artifacts\DadsRouteHUD-$taskVersion.zip"
Copy-Item -LiteralPath $PackagePath -Destination $taskFixturePackage
[IO.File]::WriteAllText((Join-Path $taskFixtureSource 'info.json'), $taskInfoText)
[IO.File]::WriteAllText("$taskFixturePackage.sha256", (Get-FileHash -LiteralPath $taskFixturePackage).Hash + '  fixture.zip')
$script:taskChecks = 0
function Assert-Check([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:taskChecks++
}
function New-TestGame([string]$Name) {
    $taskRoot = Join-Path $taskFixture $Name
    $taskMod = Join-Path $taskRoot 'Mods\DadsRouteHUD'
    $taskManaged = Join-Path $taskRoot 'DerailValley_Data\Managed\UnityModManager'
    New-Item -ItemType Directory -Path $taskMod,$taskManaged -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $taskMod 'DadsRouteHUD.dll'), 'previous DLL fixture')
    [IO.File]::WriteAllText((Join-Path $taskMod 'DadsRouteHUD.dll.123.cache'), 'previous cache fixture')
    [IO.File]::WriteAllText((Join-Path $taskMod 'Settings.xml'), '<Settings><preserved>true</preserved></Settings>')
    [IO.File]::WriteAllText((Join-Path $taskMod 'unrelated.txt'), 'untouched')
    [IO.File]::WriteAllText((Join-Path $taskManaged 'Params.xml'), '<Params><Mod Id="DadsRouteHUD" Enabled="false" /></Params>')
    return $taskRoot
}
function Get-TestManifest([string]$Root) {
    return @(Get-ChildItem -LiteralPath $Root -Recurse -File | ForEach-Object {
        $_.FullName.Substring($Root.Length + 1) + ' ' + (Get-FileHash -LiteralPath $_.FullName).Hash
    } | Sort-Object)
}

# A real production package installs into an isolated fixture, never the actual game.
$taskSuccessGame = New-TestGame 'success'
$taskSuccessMod = Join-Path $taskSuccessGame 'Mods\DadsRouteHUD'
$taskOriginalSettings = (Get-FileHash -LiteralPath (Join-Path $taskSuccessMod 'Settings.xml')).Hash
$taskOriginalParams = (Get-FileHash -LiteralPath (Join-Path $taskSuccessGame 'DerailValley_Data\Managed\UnityModManager\Params.xml')).Hash
& (Join-Path $taskFixtureSource 'Install.ps1') -GamePath $taskSuccessGame
Assert-Check ([Reflection.AssemblyName]::GetAssemblyName((Join-Path $taskSuccessMod 'DadsRouteHUD.dll')).Version.ToString(3) -eq $taskVersion) 'Fixture installed version mismatch'
Assert-Check ((Get-FileHash -LiteralPath (Join-Path $taskSuccessMod 'Settings.xml')).Hash -eq $taskOriginalSettings) 'Fixture settings changed'
Assert-Check ((Get-FileHash -LiteralPath (Join-Path $taskSuccessGame 'DerailValley_Data\Managed\UnityModManager\Params.xml')).Hash -eq $taskOriginalParams) 'Fixture UMM flags changed'
Assert-Check (-not (Test-Path -LiteralPath (Join-Path $taskSuccessMod 'DadsRouteHUD.dll.123.cache'))) 'Fixture cache was not cleared'
Assert-Check ((Get-Content -LiteralPath (Join-Path $taskSuccessMod 'unrelated.txt') -Raw) -eq 'untouched') 'Unrelated fixture file changed'
$taskSuccessReceipt = Get-ChildItem -LiteralPath (Join-Path $taskFixtureSource 'artifacts\backups') -Recurse -Filter 'installation-receipt.json' | Select-Object -First 1
Assert-Check ($null -ne $taskSuccessReceipt) 'Successful install has no receipt'
$taskRecorded = Get-Content -LiteralPath $taskSuccessReceipt.FullName -Raw | ConvertFrom-Json
Assert-Check ($taskRecorded.packageSHA256 -eq (Get-FileHash -LiteralPath $taskFixturePackage).Hash) 'Receipt omitted exact package hash'
Assert-Check ((Get-Content -LiteralPath (Join-Path $taskRecorded.backup 'DadsRouteHUD\DadsRouteHUD.dll') -Raw) -eq 'previous DLL fixture') 'Backup omitted previous DLL'

# Inject one failed candidate file copy, then allow production rollback to run normally.
$taskRollbackGame = New-TestGame 'rollback'
$taskBeforeRollback = Get-TestManifest $taskRollbackGame
$taskFailureMessage = & {
    $global:taskInstallerTestFailure = $true
    function global:Copy-Item {
        [CmdletBinding()]
        param([string]$LiteralPath, [string]$Destination, [switch]$Recurse, [switch]$Force)
        if ($global:taskInstallerTestFailure -and [IO.Path]::GetFileName($LiteralPath) -eq 'QuantitiesNet.dll' -and [IO.Path]::GetFileName([IO.Path]::GetDirectoryName($LiteralPath)) -eq 'candidate') {
            $global:taskInstallerTestFailure = $false
            throw 'Injected candidate copy failure'
        }
        Microsoft.PowerShell.Management\Copy-Item @PSBoundParameters
    }
    try { & (Join-Path $taskFixtureSource 'Install.ps1') -GamePath $taskRollbackGame; throw 'Failure injection did not run' }
    catch { $_.Exception.Message }
    finally { Remove-Item -LiteralPath Function:\Copy-Item; Remove-Variable -Name taskInstallerTestFailure -Scope Global }
}
$taskFailureText = $taskFailureMessage | Out-String
Assert-Check ($taskFailureText -like '*Previous package files/caches restored*') ("Production rollback did not report restoration: " + $taskFailureText)
Assert-Check (-not (Compare-Object $taskBeforeRollback (Get-TestManifest $taskRollbackGame))) 'Rollback changed original fixture files/settings/cache'

# A valid ZIP hash/allowlist with inconsistent metadata must fail before any game writes.
$taskMismatchVersion = '9.9.9'
$taskMismatchPath = Join-Path $taskFixtureSource "artifacts\DadsRouteHUD-$taskMismatchVersion.zip"
$taskInfo.Version = $taskMismatchVersion
$taskMismatchInfo = $taskInfo | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $taskFixtureSource 'info.json'), $taskMismatchInfo)
$taskSourceArchive = [IO.Compression.ZipFile]::OpenRead($taskFixturePackage)
$taskMismatchArchive = [IO.Compression.ZipFile]::Open($taskMismatchPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($taskEntry in $taskSourceArchive.Entries) {
        $taskOutput = $taskMismatchArchive.CreateEntry($taskEntry.FullName).Open()
        try {
            if ($taskEntry.FullName -eq 'DadsRouteHUD/info.json') {
                $taskBytes = [Text.Encoding]::UTF8.GetBytes($taskMismatchInfo)
                $taskOutput.Write($taskBytes, 0, $taskBytes.Length)
            } else {
                $taskInput = $taskEntry.Open()
                try { $taskInput.CopyTo($taskOutput) } finally { $taskInput.Dispose() }
            }
        } finally { $taskOutput.Dispose() }
    }
} finally { $taskSourceArchive.Dispose(); $taskMismatchArchive.Dispose() }
[IO.File]::WriteAllText("$taskMismatchPath.sha256", (Get-FileHash -LiteralPath $taskMismatchPath).Hash + '  fixture.zip')
$taskMismatchGame = New-TestGame 'mismatch'
$taskBeforeMismatch = Get-TestManifest $taskMismatchGame
$taskRejected = $false
try { & (Join-Path $taskFixtureSource 'Install.ps1') -GamePath $taskMismatchGame }
catch { $taskRejected = $_.Exception.Message -like '*metadata/assembly version mismatch*' }
Assert-Check $taskRejected 'Mismatched packaged assembly was not rejected in preflight'
Assert-Check (-not (Compare-Object $taskBeforeMismatch (Get-TestManifest $taskMismatchGame))) 'Preflight rejection modified fixture game files'
Write-Output "$script:taskChecks installer fixture checks passed; actual game was untouched. Evidence: $taskFixture"
