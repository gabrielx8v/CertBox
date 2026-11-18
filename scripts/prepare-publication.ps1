# SPDX-License-Identifier: GPL-3.0-or-later
param([string]$OutputDirectory = '', [switch]$ForRelease, [string]$Destination = '')
$ErrorActionPreference = 'Stop'
$sourceDirectory = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $sourceDirectory '../output' }
& (Join-Path $PSScriptRoot 'write-release-notes.ps1') -OutputDirectory $OutputDirectory -ForRelease:$ForRelease
if (-not $Destination) { $Destination = Join-Path $sourceDirectory 'work/github-current' }
$Destination = [IO.Path]::GetFullPath($Destination)
$workDirectory = Join-Path $sourceDirectory 'work'
if ((Test-Path -LiteralPath $workDirectory) -and ((Get-Item -LiteralPath $workDirectory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Refusing a reparse point as the work directory.' }
$workRoot = [IO.Path]::GetFullPath((Join-Path $sourceDirectory 'work')) + [IO.Path]::DirectorySeparatorChar
if (-not $Destination.StartsWith($workRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Publication staging must stay inside source/work.' }
if (Test-Path -LiteralPath $Destination) {
    $existing = Get-Item -LiteralPath $Destination -Force
    if (($existing.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Refusing a reparse point as staging destination.' }
    if (-not (Test-Path -LiteralPath (Join-Path $Destination '.certbox-publication'))) { throw 'Refusing to replace a directory not created by this script.' }
    # The absolute path is checked against source/work before this deletion.
    # Reject any junction inside the generated staging tree before traversing it.
    $pending = [Collections.Generic.Queue[string]]::new(); $pending.Enqueue($Destination)
    while ($pending.Count) {
        foreach ($item in Get-ChildItem -LiteralPath $pending.Dequeue() -Force) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Refusing reparse points in staging.' }
            if ($item.PSIsContainer) { $pending.Enqueue($item.FullName) }
        }
    }
    Remove-Item -LiteralPath $Destination -Recurse -Force
}
$repository = Join-Path $Destination 'repository'
$assets = Join-Path $Destination 'release-assets'
New-Item -ItemType Directory -Force -Path $repository,$assets | Out-Null
[IO.File]::WriteAllText((Join-Path $Destination '.certbox-publication'), 'Generated current-version publication staging. No historical versions.')
$files = @('.gitignore','README.md','LICENSE','NOTICE.md','THIRD_PARTY_NOTICES.md','DEPENDENCIES.md','DEPENDENCIES.json','BUILDING.md','RELEASE.md','CHANGELOG.md','CONTRIBUTING.md','SECURITY.md','global.json','NuGet.Config','project.json')
foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $sourceDirectory $file) -Destination (Join-Path $repository $file) }
foreach ($folder in @('src','tests','scripts','tools','docs','licenses','.github')) {
    $queue = [Collections.Generic.Queue[string]]::new(); $queue.Enqueue((Join-Path $sourceDirectory $folder))
    while ($queue.Count) {
        foreach ($item in Get-ChildItem -LiteralPath $queue.Dequeue() -Force) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Refusing to publish a reparse point.' }
            if ($item.PSIsContainer) { if ($item.Name -notin @('bin','obj','.git')) { $queue.Enqueue($item.FullName) }; continue }
            if ($item.Name -eq 'settings.json' -or $item.Name.StartsWith('.env') -or $item.Extension -in @('.key','.pem','.pfx','.p12','.exe','.zip','.user')) { throw 'Private input, personal settings or build output found in a source directory; not preparing publication.' }
            $relative = $item.FullName.Substring($sourceDirectory.Length + 1)
            $target = Join-Path $repository $relative
            New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
            Copy-Item -LiteralPath $item.FullName -Destination $target
        }
    }
}
# Assign directly: Windows PowerShell 5.1 emits the JSON array as one pipeline item.
$artifacts = Get-Content -LiteralPath (Join-Path $sourceDirectory 'release/ARTIFACTS.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($artifact in $artifacts) { Copy-Item -LiteralPath (Join-Path $OutputDirectory $artifact.name) -Destination (Join-Path $assets $artifact.name) }
foreach ($name in @('SHA256SUMS.txt','RELEASE_NOTES.md','ARTIFACTS.json')) { Copy-Item -LiteralPath (Join-Path $sourceDirectory ('release/' + $name)) -Destination (Join-Path $assets $name) }
Write-Output "Current version only, prepared locally: $Destination"
