# SPDX-License-Identifier: GPL-3.0-or-later
param([string]$PublishedDirectory = 'work/publish/verification-windows', [string]$Dotnet = 'dotnet', [ValidateSet('win-x64','linux-x64')][string]$RuntimeIdentifier = 'win-x64', [string]$WslDistribution = 'Debian', [string]$LinuxLibraryDirectory = '')
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_HOME = Join-Path $PWD 'work/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PWD 'work/packages'
if ($Dotnet -eq 'dotnet' -and (Test-Path -LiteralPath 'work/dotnet/dotnet.exe')) {
    $Dotnet = Join-Path $PWD 'work/dotnet/dotnet.exe'
    $env:DOTNET_ROOT = Split-Path $Dotnet -Parent
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
}
$published = (Resolve-Path -LiteralPath $PublishedDirectory).ProviderPath
$fixtures = (Resolve-Path -LiteralPath 'work/test-fixtures').ProviderPath
$name = 'CertBox.PublishedChecks'
$platform = if ($RuntimeIdentifier -eq 'linux-x64') { 'linux' } else { 'windows' }
& $Dotnet restore "tests/PublishedChecks/$name.csproj" -r $RuntimeIdentifier -p:CertBoxPlatform=$platform --source work/packages
if ($LASTEXITCODE) { throw 'Published-check runner restore failed' }
& $Dotnet build "tests/PublishedChecks/$name.csproj" -c Release -r $RuntimeIdentifier -p:CertBoxPlatform=$platform --no-restore
if ($LASTEXITCODE) { throw 'Published-check runner build failed' }
$stage = Join-Path $PWD ('work/published-checks/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
foreach ($file in Get-ChildItem -LiteralPath $published -File) { Copy-Item -LiteralPath $file.FullName -Destination $stage }
$hostName = if ($RuntimeIdentifier -eq 'linux-x64') { $name } else { "$name.exe" }
Copy-Item -LiteralPath "tests/PublishedChecks/bin/Release/net10.0/$RuntimeIdentifier/$hostName" -Destination $stage
$deps = Get-Content -LiteralPath (Join-Path $published 'certbox.deps.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$targetName = $deps.runtimeTarget.name
$app = $deps.targets.$targetName.PSObject.Properties.Name | Where-Object { $_ -like 'certbox/*' } | Select-Object -First 1
$appVersion = $app.Substring('certbox/'.Length)
$component = $name + '/1.0.0'
$deps.targets.$targetName | Add-Member -MemberType NoteProperty -Name $component -Value @{
    dependencies = @{ certbox = $appVersion }; runtime = @{ "$name.dll" = @{} }
}
$deps.libraries | Add-Member -MemberType NoteProperty -Name $component -Value @{ type = 'project'; serviceable = $false; sha512 = '' }
$deps | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $stage "$name.deps.json") -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $published 'certbox.runtimeconfig.json') -Destination (Join-Path $stage "$name.runtimeconfig.json")

# Compile against the *trimmed implementation assemblies*, not the full reference
# framework. Otherwise removed type-forwarders can invalidate the test runner.
$sdk = & $Dotnet --list-sdks | Select-Object -Last 1
if ($sdk -notmatch '^(\S+)\s+\[(.+)\]') { throw 'Could not locate the C# compiler' }
$compiler = Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn/bincore/csc.dll'
$compilerArgs = @($compiler, '-nologo', '-nostdlib+', '-target:exe', '-nullable:enable', '-langversion:latest', "-out:$stage/$name.dll")
foreach ($file in Get-ChildItem -LiteralPath $published -Filter '*.dll' -File) {
    try { [System.Reflection.AssemblyName]::GetAssemblyName($file.FullName) | Out-Null; $compilerArgs += '-r:' + $file.FullName }
    catch [System.BadImageFormatException] { }
}
$compilerArgs += (Join-Path $PWD 'tests/PublishedChecks/Program.cs')
& $Dotnet @compilerArgs
if ($LASTEXITCODE) { throw 'Test runner uses an API absent from the published runtime' }
$resultFolder = Join-Path $stage 'results'
$report = Join-Path $stage 'results.txt'
New-Item -ItemType Directory -Path $resultFolder | Out-Null
if ($RuntimeIdentifier -eq 'linux-x64') {
    function WslPath([string]$path) {
        $absolute = [IO.Path]::GetFullPath($path)
        if ($absolute -notmatch '^([A-Za-z]):\\(.*)$') { throw 'WSL test paths must be on a local Windows drive.' }
        return '/mnt/' + $Matches[1].ToLowerInvariant() + '/' + $Matches[2].Replace('\','/')
    }
    $wslArgs = @('-d',$WslDistribution,'--exec')
    if ($LinuxLibraryDirectory) { $wslArgs += @('/usr/bin/env', ('LD_LIBRARY_PATH=' + (WslPath $LinuxLibraryDirectory))) }
    $wslArgs += @((WslPath (Join-Path $stage $hostName)), (WslPath $fixtures), (WslPath $resultFolder), (WslPath $report), $RuntimeIdentifier)
    & wsl.exe @wslArgs
    $exitCode = $LASTEXITCODE
} else {
    $testArgs = @(('"' + $fixtures + '"'), ('"' + $resultFolder + '"'), ('"' + $report + '"'), $RuntimeIdentifier)
    $process = Start-Process -FilePath (Join-Path $stage $hostName) -ArgumentList $testArgs -WindowStyle Hidden -Wait -PassThru -RedirectStandardError (Join-Path $stage 'stderr.txt') -RedirectStandardOutput (Join-Path $stage 'stdout.txt')
    $exitCode = $process.ExitCode
}
if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report -Encoding UTF8 }
if ($exitCode -ne 0) { if (Test-Path -LiteralPath (Join-Path $stage 'stderr.txt')) { Get-Content -LiteralPath (Join-Path $stage 'stderr.txt') }; throw "Published checks failed: $exitCode" }
Write-Output "Published runtime verified without opening a window. Report: $report"
