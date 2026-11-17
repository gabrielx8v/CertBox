# SPDX-License-Identifier: GPL-3.0-or-later
param([string]$Component = '')
$ErrorActionPreference = 'Stop'
$sourceDirectory = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$inventory = Get-Content -LiteralPath (Join-Path $sourceDirectory 'DEPENDENCIES.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$components = @($inventory.components)
if ($Component) { $components = @($components | Where-Object name -EQ $Component); if (-not $components.Count) { throw 'Unknown component; use a name from DEPENDENCIES.json.' } }
foreach ($entry in $components) {
    $name = $entry.name -replace '[^A-Za-z0-9.-]', '-'
    $destination = Join-Path $sourceDirectory ('work/upstream-sources/' + $name + '-' + $entry.version)
    if (Test-Path -LiteralPath $destination) { throw "Destination already exists; not replacing it: $destination" }
    & git -c core.longpaths=true clone --filter=blob:none --no-checkout $entry.repository $destination
    if ($LASTEXITCODE) { throw "Clone failed: $($entry.name)" }
    & git -C $destination -c core.longpaths=true checkout --detach $entry.commit
    if ($LASTEXITCODE) { throw "Checkout failed: $($entry.name)" }
    & git -C $destination -c core.longpaths=true submodule update --init --recursive
    if ($LASTEXITCODE) { throw "Submodule checkout failed: $($entry.name)" }
    Write-Output "Source checked out: $destination"
}
