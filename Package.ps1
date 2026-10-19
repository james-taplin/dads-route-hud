param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Derail Valley',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build.ps1') -GamePath $GamePath -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Build failed; package was not created.' }
$taskInfoPath = Join-Path $PSScriptRoot 'info.json'
$taskInfo = Get-Content -LiteralPath $taskInfoPath -Raw | ConvertFrom-Json
if ($taskInfo.Id -ne 'DadsRouteHUD' -or $taskInfo.AssemblyName -ne 'DadsRouteHUD.dll') {
    throw 'info.json must identify DadsRouteHUD and DadsRouteHUD.dll.'
}
$taskVersion = [string]$taskInfo.Version
if ($taskVersion -notmatch '^\d+\.\d+\.\d+(?:[-+][A-Za-z0-9.-]+)?$') { throw 'Invalid package version in info.json.' }
$taskArtifacts = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Path $taskArtifacts -Force | Out-Null
$taskPackagePath = Join-Path $taskArtifacts "DadsRouteHUD-$taskVersion.zip"
$taskStagingPath = "$taskPackagePath.tmp"
$taskEntries = [ordered]@{
    'info.json' = $taskInfoPath
    'DadsRouteHUD.dll' = (Join-Path $PSScriptRoot 'build\DadsRouteHUD.dll')
    'QuantitiesNet.dll' = (Join-Path $PSScriptRoot 'build\QuantitiesNet.dll')
    'LICENSE' = (Join-Path $PSScriptRoot 'LICENSE')
    'QuantitiesNet.LICENSE' = (Join-Path $PSScriptRoot 'QuantitiesNet.LICENSE')
    'THIRD_PARTY_NOTICES.txt' = (Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.txt')
    'CREDITS.md' = (Join-Path $PSScriptRoot 'CREDITS.md')
    'docs/LICENSING.md' = (Join-Path $PSScriptRoot 'docs\LICENSING.md')
    'README.md' = (Join-Path $PSScriptRoot 'README.md')
    'docs/VALIDATION.md' = (Join-Path $PSScriptRoot 'docs\VALIDATION.md')
    'docs/PROJECT_CONTEXT.md' = (Join-Path $PSScriptRoot 'docs\PROJECT_CONTEXT.md')
    'docs/HANDOVER.md' = (Join-Path $PSScriptRoot 'docs\HANDOVER.md')
    'docs/NATIVE_SIGN_ASSOCIATION.md' = (Join-Path $PSScriptRoot 'docs\NATIVE_SIGN_ASSOCIATION.md')
    'docs/OPTIONAL_SIGNAL_ADAPTER.md' = (Join-Path $PSScriptRoot 'docs\OPTIONAL_SIGNAL_ADAPTER.md')
}
foreach ($taskSource in $taskEntries.Values) {
    if (-not (Test-Path -LiteralPath $taskSource -PathType Leaf)) { throw "Required package input is missing: $taskSource" }
}
Add-Type -AssemblyName System.IO.Compression
$taskStream = [IO.File]::Open($taskStagingPath, [IO.FileMode]::Create, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $taskArchive = [IO.Compression.ZipArchive]::new($taskStream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($taskEntryName in $taskEntries.Keys) {
            $taskEntry = $taskArchive.CreateEntry("DadsRouteHUD/$taskEntryName", [IO.Compression.CompressionLevel]::Optimal)
            $taskEntry.LastWriteTime = [DateTimeOffset]::new(2026, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            $taskEntryStream = $taskEntry.Open()
            try {
                $taskInput = [IO.File]::OpenRead($taskEntries[$taskEntryName])
                try { $taskInput.CopyTo($taskEntryStream) } finally { $taskInput.Dispose() }
            } finally { $taskEntryStream.Dispose() }
        }
    } finally { $taskArchive.Dispose() }
} finally { $taskStream.Dispose() }
Move-Item -LiteralPath $taskStagingPath -Destination $taskPackagePath -Force
$taskHash = (Get-FileHash -LiteralPath $taskPackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$taskPackagePath.sha256", "$taskHash  $([IO.Path]::GetFileName($taskPackagePath))`n", [Text.UTF8Encoding]::new($false))
Write-Output "Packaged $taskPackagePath"
Write-Output "SHA256 $taskHash"
