# SPDX-License-Identifier: GPL-3.0-or-later
param([string]$Dotnet = "dotnet", [string]$OutputDirectory = "", [switch]$ForRelease, [ValidateSet('Both','Windows','Linux')][string]$Target = 'Both')
$ErrorActionPreference = 'Stop'
if ($ForRelease -and $Target -ne 'Both') { throw 'Public release preparation requires both Windows and Linux builds.' }
Set-Location (Split-Path $PSScriptRoot -Parent)
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_HOME = Join-Path $PWD 'work/dotnet-home'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $PWD 'work/nuget-http'
$env:NUGET_PACKAGES = Join-Path $PWD 'work/packages'
if ($Dotnet -eq 'dotnet' -and (Test-Path -LiteralPath 'work/dotnet/dotnet.exe')) {
    $Dotnet = Join-Path $PWD 'work/dotnet/dotnet.exe'
    $env:DOTNET_ROOT = Split-Path $Dotnet -Parent
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
}
& (Join-Path $PSScriptRoot 'update-project-links.ps1') -RequireRepository:$ForRelease
[xml]$projectXml = Get-Content -LiteralPath 'src/CertBox/CertBox.csproj' -Raw -Encoding UTF8
[xml]$manifestXml = Get-Content -LiteralPath 'src/CertBox/app.manifest' -Raw -Encoding UTF8
$version = [string]$projectXml.Project.PropertyGroup.Version
if ($manifestXml.assembly.assemblyIdentity.version -ne ($version + '.0')) { throw 'Application manifest and project versions must match.' }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = if ((Split-Path $PWD -Leaf) -eq 'source') { '../output' } else { 'output' }
}
$outputDirectory = if ([IO.Path]::IsPathRooted($OutputDirectory)) { [IO.Path]::GetFullPath($OutputDirectory) } else { [IO.Path]::GetFullPath((Join-Path $PWD $OutputDirectory)) }
New-Item -ItemType Directory -Force $outputDirectory | Out-Null
& $Dotnet run --project tests/CertBox.Tests.csproj -c Release -- work/test-fixtures
if ($LASTEXITCODE) { throw 'Core tests failed' }
& $Dotnet run --project tests/UiChecks/CertBox.UiChecks.csproj -c Release -- work/test-fixtures
if ($LASTEXITCODE) { throw 'UI checks failed' }
if ($Target -in @('Both','Windows')) {
    & $Dotnet publish src/CertBox/CertBox.csproj -c Release -r win-x64 -p:CertBoxPlatform=windows --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o work/publish/certbox-windows
    if ($LASTEXITCODE) { throw 'Windows publish failed' }
    Copy-Item -LiteralPath work/publish/certbox-windows/certbox.exe -Destination (Join-Path $outputDirectory 'certbox-windows-x64.exe') -Force
}
if ($Target -in @('Both','Linux')) {
    & $Dotnet publish src/CertBox/CertBox.csproj -c Release -r linux-x64 -p:CertBoxPlatform=linux --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o work/publish/certbox-linux
    if ($LASTEXITCODE) { throw 'Linux publish failed' }
    $repositoryUrl = (Get-Content -LiteralPath project.json -Raw -Encoding UTF8 | ConvertFrom-Json).repositoryUrl
    & $Dotnet run --project tools/LinuxPackage/LinuxPackage.csproj -c Release -- (Join-Path $PWD 'work/publish/certbox-linux/certbox') (Join-Path $PWD 'src/CertBox/Assets/certbox-256.png') (Join-Path $outputDirectory 'certbox-linux-x64.tar.gz') $version $repositoryUrl
    if ($LASTEXITCODE) { throw 'Linux package failed' }
}
& (Join-Path $PSScriptRoot 'write-release-notes.ps1') -OutputDirectory $outputDirectory -ForRelease:$ForRelease
if ($ForRelease) { & (Join-Path $PSScriptRoot 'prepare-publication.ps1') -OutputDirectory $outputDirectory -ForRelease }
