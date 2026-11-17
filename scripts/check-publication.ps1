# SPDX-License-Identifier: GPL-3.0-or-later
param([string]$Directory = '')
$ErrorActionPreference = 'Stop'
$sourceDirectory = Split-Path $PSScriptRoot -Parent
if (-not $Directory) { $Directory = Join-Path $sourceDirectory 'work/github-current' }
$repository = Join-Path $Directory 'repository'
$assets = Join-Path $Directory 'release-assets'
if (-not (Test-Path -LiteralPath (Join-Path $repository 'LICENSE'))) { throw 'Publication staging not found.' }
$forbidden = @(Get-ChildItem -LiteralPath $repository -Recurse -Force | Where-Object {
    $_.Name -in @('old versions','work','bin','obj','.git','LICENCIAMENTO.md','SIZE_ANALYSIS.md','VALIDACAO.txt','settings.json') -or $_.Extension -in @('.exe','.key','.pem','.pfx','.p12','.zip')
})
if ($forbidden.Count) { throw 'Private history, intermediates or certificate files found in public source staging.' }
$license = Get-Content -LiteralPath (Join-Path $repository 'LICENSE') -Raw -Encoding UTF8
if ($license.Length -lt 30000 -or $license -notmatch 'GNU GENERAL PUBLIC LICENSE') { throw 'Full GPL license missing.' }
foreach ($path in @('README.md','docs/README.en.md','docs/README.es.md')) {
    $text = Get-Content -LiteralPath (Join-Path $repository $path) -Raw -Encoding UTF8
    if ($text -notmatch '\bIA\b|\bAI\b') { throw 'AI disclosure missing from README.' }
}
[xml]$project = Get-Content -LiteralPath (Join-Path $repository 'src/CertBox/CertBox.csproj') -Raw -Encoding UTF8
[xml]$manifest = Get-Content -LiteralPath (Join-Path $repository 'src/CertBox/app.manifest') -Raw -Encoding UTF8
$version = [string]$project.Project.PropertyGroup.Version
$executable = Get-Item -LiteralPath (Join-Path $assets 'certbox-windows-x64.exe')
if ($executable.VersionInfo.ProductVersion -ne $version -or $manifest.assembly.assemblyIdentity.version -ne ($version + '.0')) { throw 'Source, manifest and executable versions differ.' }
$sum = Get-Content -LiteralPath (Join-Path $assets 'SHA256SUMS.txt') -Raw -Encoding UTF8
$artifacts = @(Get-Content -LiteralPath (Join-Path $assets 'ARTIFACTS.json') -Raw -Encoding UTF8 | ConvertFrom-Json)
if ($artifacts.Count -ne 2) { throw 'Expected Windows and Linux artifacts.' }
foreach ($artifact in $artifacts) {
    $file = Get-Item -LiteralPath (Join-Path $assets $artifact.name)
    $hash = (Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant()
    if ($hash -ne $artifact.sha256 -or $artifact.version -ne $version -or $sum -notmatch [regex]::Escape($hash + '  ' + $artifact.name)) { throw 'Release checksum or version mismatch.' }
}
foreach ($path in @('README.md','docs/README.en.md','docs/README.es.md')) {
    $text = Get-Content -LiteralPath (Join-Path $repository $path) -Raw -Encoding UTF8
    if ($text -notmatch 'Linux' -or $text -notmatch 'não foi testada manualmente|not been manually tested|no ha sido probada manualmente') { throw 'Linux manual-test disclosure missing.' }
}
if ((Get-AuthenticodeSignature -LiteralPath $executable.FullName).Status -ne 'NotSigned') { throw 'Expected the direct-download unsigned executable.' }
Write-Output 'PASS: current source only; GPL; AI and Linux test disclosures; Windows/Linux versions and hashes; unsigned EXE.'
