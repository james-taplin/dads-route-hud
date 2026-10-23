param(
    [Parameter(Mandatory = $true)][string]$GamePath
)
$ErrorActionPreference = 'Stop'
$taskGameRoot = (Resolve-Path -LiteralPath $GamePath).Path
$taskModRoot = Join-Path $taskGameRoot 'Mods\DadsRouteHUD'
$taskParams = Join-Path $taskGameRoot 'DerailValley_Data\Managed\UnityModManager\Params.xml'
$taskInfo = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Raw | ConvertFrom-Json
$taskPackage = Join-Path $PSScriptRoot "artifacts\DadsRouteHUD-$($taskInfo.Version).zip"
function Assert-GameClosed {
    if (Get-Process | Where-Object { $_.ProcessName -match '^Derail.?Valley$' -or ($_.Path -and $_.Path.StartsWith($taskGameRoot + '\', [StringComparison]::OrdinalIgnoreCase)) }) {
        throw 'Close Derail Valley before installing.'
    }
}
Assert-GameClosed
if (-not (Test-Path -LiteralPath $taskModRoot -PathType Container)) { throw 'Expected an existing DadsRouteHUD installation.' }
if ($taskInfo.Id -ne 'DadsRouteHUD' -or $taskInfo.AssemblyName -ne 'DadsRouteHUD.dll') { throw 'Wrong mod identity.' }
$taskPackageHash = (Get-FileHash -LiteralPath $taskPackage -Algorithm SHA256).Hash
if ($taskPackageHash -ne (Get-Content -LiteralPath "$taskPackage.sha256" -Raw).Split(' ')[0]) { throw 'Package hash mismatch.' }
$taskAllowed = @('info.json','DadsRouteHUD.dll','QuantitiesNet.dll','LICENSE','QuantitiesNet.LICENSE','THIRD_PARTY_NOTICES.txt','CREDITS.md','docs/LICENSING.md','README.md','docs/VALIDATION.md','docs/PROJECT_CONTEXT.md','docs/HANDOVER.md','docs/NATIVE_SIGN_ASSOCIATION.md','docs/OPTIONAL_SIGNAL_ADAPTER.md')
Add-Type -AssemblyName System.IO.Compression
$taskArchive = [IO.Compression.ZipFile]::OpenRead($taskPackage)
try {
    $taskNames = @($taskArchive.Entries | ForEach-Object { $_.FullName })
    if ($taskNames.Count -ne $taskAllowed.Count -or (Compare-Object ($taskAllowed | ForEach-Object { 'DadsRouteHUD/' + $_ }) $taskNames)) { throw 'Unexpected package contents.' }
    $taskBackup = Join-Path $PSScriptRoot ('artifacts\backups\upgrade-' + $taskInfo.Version + '-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $taskBackup | Out-Null
    # Validate package metadata and PE identity in staging before touching installed files/caches.
    $taskCandidateRoot = Join-Path $taskBackup 'candidate'
    foreach ($taskEntry in $taskArchive.Entries) {
        $taskRelative = $taskEntry.FullName.Substring('DadsRouteHUD/'.Length)
        $taskStagedFile = Join-Path $taskCandidateRoot $taskRelative
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($taskStagedFile)) -Force | Out-Null
        $taskInput = $taskEntry.Open()
        $taskOutput = [IO.File]::Create($taskStagedFile)
        try { $taskInput.CopyTo($taskOutput) } finally { $taskInput.Dispose(); $taskOutput.Dispose() }
    }
    $taskCandidateInfo = Get-Content -LiteralPath (Join-Path $taskCandidateRoot 'info.json') -Raw | ConvertFrom-Json
    $taskCandidateAssembly = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $taskCandidateRoot 'DadsRouteHUD.dll'))
    if ($taskCandidateInfo.Id -ne 'DadsRouteHUD' -or $taskCandidateInfo.AssemblyName -ne 'DadsRouteHUD.dll' -or
        $taskCandidateInfo.Version -ne $taskInfo.Version -or $taskCandidateAssembly.Name -ne 'DadsRouteHUD' -or
        $taskCandidateAssembly.Version.ToString(3) -ne $taskInfo.Version) { throw 'Packaged metadata/assembly version mismatch.' }
    Copy-Item -LiteralPath $taskModRoot -Destination (Join-Path $taskBackup 'DadsRouteHUD') -Recurse
    foreach ($taskFile in Get-ChildItem -LiteralPath $taskModRoot -Recurse -File) {
        $taskRelative = $taskFile.FullName.Substring($taskModRoot.Length + 1)
        if ((Get-FileHash -LiteralPath $taskFile.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $taskBackup "DadsRouteHUD\$taskRelative")).Hash) { throw "Backup mismatch: $taskRelative" }
    }
    Copy-Item -LiteralPath $taskParams -Destination (Join-Path $taskBackup 'Params.original.xml')
    $taskParamsHash = (Get-FileHash -LiteralPath $taskParams).Hash
    if ((Get-FileHash -LiteralPath (Join-Path $taskBackup 'Params.original.xml')).Hash -ne $taskParamsHash) { throw 'Params.xml backup mismatch.' }
    $taskSettings = Join-Path $taskModRoot 'Settings.xml'
    $taskSettingsHash = if (Test-Path -LiteralPath $taskSettings) { (Get-FileHash -LiteralPath $taskSettings).Hash } else { $null }
    Assert-GameClosed
    try {
    # UMM's EnableReloading loads a shadow copy. Remove only this mod's already-backed-up caches.
    foreach ($taskCache in Get-ChildItem -LiteralPath $taskModRoot -File -Filter 'DadsRouteHUD.dll.*.cache') {
        if ([IO.Path]::GetDirectoryName($taskCache.FullName) -ne $taskModRoot) { throw 'Cache escaped the installed mod directory.' }
        Remove-Item -LiteralPath $taskCache.FullName
    }
    foreach ($taskRelative in $taskAllowed) {
        $taskDestination = Join-Path $taskModRoot $taskRelative
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($taskDestination)) -Force | Out-Null
        $taskCandidateFile = Join-Path $taskCandidateRoot $taskRelative
        Copy-Item -LiteralPath $taskCandidateFile -Destination $taskDestination -Force
        $taskExpected = (Get-FileHash -LiteralPath $taskCandidateFile).Hash
        if ((Get-FileHash -LiteralPath $taskDestination).Hash -ne $taskExpected) { throw "Installed file mismatch: $taskRelative" }
    }
    if ((Get-FileHash -LiteralPath $taskParams).Hash -ne $taskParamsHash) { throw 'UMM Params.xml changed.' }
    if ($taskSettingsHash -and (Get-FileHash -LiteralPath $taskSettings).Hash -ne $taskSettingsHash) { throw 'Settings.xml changed.' }
    $taskVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $taskModRoot 'DadsRouteHUD.dll')).Version.ToString(3)
    if ($taskVersion -ne $taskInfo.Version) { throw 'Installed assembly version mismatch.' }
    if (Get-ChildItem -LiteralPath $taskModRoot -File -Filter 'DadsRouteHUD.dll.*.cache') { throw 'Shadow cache was not cleared.' }
    Assert-GameClosed
    } catch {
        $taskInstallFailure = $_.Exception.Message
        # Restore only the files this installer replaces, plus the caches it removed.
        # Settings/Params.xml are never written by this installer or by rollback.
        try {
            Assert-GameClosed
            foreach ($taskRelative in $taskAllowed) {
                $taskOriginal = Join-Path $taskBackup "DadsRouteHUD\$taskRelative"
                $taskDestination = Join-Path $taskModRoot $taskRelative
                if (Test-Path -LiteralPath $taskOriginal -PathType Leaf) {
                    Copy-Item -LiteralPath $taskOriginal -Destination $taskDestination -Force
                    if ((Get-FileHash -LiteralPath $taskDestination).Hash -ne (Get-FileHash -LiteralPath $taskOriginal).Hash) { throw "Rollback mismatch: $taskRelative" }
                } elseif (Test-Path -LiteralPath $taskDestination -PathType Leaf) {
                    Remove-Item -LiteralPath $taskDestination
                }
            }
            foreach ($taskCache in Get-ChildItem -LiteralPath (Join-Path $taskBackup 'DadsRouteHUD') -File -Filter 'DadsRouteHUD.dll.*.cache') {
                $taskRestoredCache = Join-Path $taskModRoot $taskCache.Name
                Copy-Item -LiteralPath $taskCache.FullName -Destination $taskRestoredCache -Force
                if ((Get-FileHash -LiteralPath $taskRestoredCache).Hash -ne (Get-FileHash -LiteralPath $taskCache.FullName).Hash) { throw 'Cache rollback mismatch.' }
            }
        } catch { throw "Install failed: $taskInstallFailure. Rollback also failed: $($_.Exception.Message). Verified backup: $taskBackup" }
        throw "Install failed: $taskInstallFailure. Previous package files/caches restored; backup: $taskBackup"
    }
    [ordered]@{
        version = $taskVersion
        package = $taskPackage
        packageSHA256 = $taskPackageHash
        backup = $taskBackup
        installedFiles = @($taskAllowed | ForEach-Object { Get-FileHash -LiteralPath (Join-Path $taskModRoot $_) | Select-Object Path, Hash })
        settingsSHA256 = $taskSettingsHash
        paramsSHA256 = $taskParamsHash
        cache = 'Backed up and cleared; UMM must regenerate on next game launch.'
        rollback = 'With the game closed, restore the backed-up DadsRouteHUD files. Settings and UMM flags were preserved.'
        runtimeAcceptance = 'Pending; game not launched.'
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskBackup 'installation-receipt.json') -Encoding utf8
    Write-Output "Installed $taskVersion; $($taskAllowed.Count) package files verified. Settings and UMM flags preserved. Backup: $taskBackup"
} finally { $taskArchive.Dispose() }
