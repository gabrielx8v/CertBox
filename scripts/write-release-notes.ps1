# SPDX-License-Identifier: GPL-3.0-or-later
param([string]$OutputDirectory = '', [switch]$ForRelease)
$ErrorActionPreference = 'Stop'
$sourceDirectory = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'update-project-links.ps1') -RequireRepository:$ForRelease
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $sourceDirectory '../output' }
[xml]$projectXml = Get-Content -LiteralPath (Join-Path $sourceDirectory 'src/CertBox/CertBox.csproj') -Raw -Encoding UTF8
$version = [string]$projectXml.Project.PropertyGroup.Version
$artifacts = @()
foreach ($name in @('certbox-windows-x64.exe', 'certbox-linux-x64.tar.gz')) {
    $path = Join-Path $OutputDirectory $name
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $file = Get-Item -LiteralPath $path
    if ($name.EndsWith('.exe') -and $file.VersionInfo.ProductVersion -ne $version) { throw 'Windows executable version differs from source; rebuild both platforms.' }
    $artifacts += [pscustomobject]@{ name = $name; version = $version; platform = $(if ($name.EndsWith('.exe')) { 'win-x64' } else { 'linux-x64' }); bytes = $file.Length; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
}
if (-not $artifacts.Count) { throw 'No current executable artifacts found.' }
if ($ForRelease -and $artifacts.Count -ne 2) { throw 'Both platform artifacts are required for this release.' }
$releaseDirectory = Join-Path $sourceDirectory 'release'
New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
$lines = @($artifacts | ForEach-Object { $_.sha256 + '  ' + $_.name })
[IO.File]::WriteAllText((Join-Path $releaseDirectory 'SHA256SUMS.txt'), ($lines -join "`n") + "`n", $utf8)
[IO.File]::WriteAllText((Join-Path $releaseDirectory 'ARTIFACTS.json'), (ConvertTo-Json -InputObject $artifacts -Depth 4) + "`n", $utf8)
$project = Get-Content -LiteralPath (Join-Path $sourceDirectory 'project.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$repository = ([string]$project.repositoryUrl).TrimEnd('/') -replace '\.git$', ''
$table = @('| Arquivo | Plataforma | MB |', '| --- | --- | ---: |')
$table += @($artifacts | ForEach-Object { '| ' + $_.name + ' | ' + $_.platform + ' | ' + [Math]::Round($_.bytes / 1000000, 2) + ' |' })
$text = @"
# CertBox $version

**Desenvolvido com IA / Developed with AI / Desarrollado con IA.**
Código, interface e ícone produzidos com uso de IA. Bibliotecas externas mantêm
sua autoria e suas licenças. O programa não usa IA ao processar seus arquivos.

Aplicativo gráfico para Windows x64 e Linux x64, com português, inglês e espanhol.
Processamento local de certificados X.509 e exportação em CRT, PEM e KEY.

$($table -join "`n")

Windows: EXE portátil sem assinatura Authenticode. O Windows pode exibir um
aviso de aplicativo desconhecido por falta de assinatura/reputação. Confira
a origem e os hashes de SHA256SUMS.txt e mantenha as proteções do sistema ativas.

Linux: extraia o TAR.GZ e abra certbox no gerenciador de arquivos. Requer X11
ou XWayland e bibliotecas comuns do sistema; o runtime .NET já está incluído.
**A versão Linux não foi testada manualmente.**
**The Linux version has not been manually tested.**
**La versión Linux no ha sido probada manualmente.**

Verificações automatizadas do runtime reduzido passaram em Windows e em Debian
via WSL, sem exibir janelas. Incluem leitura e exportação de certificados/chaves,
construção dos controles, idiomas, preferências e documentos incorporados.
Não abrangem arraste real, diálogos nativos, conferência visual ou outros desktops.

## Código e licença

[Código desta versão]($repository/tree/v$version) ·
[Compilação]($repository/blob/v$version/BUILDING.md) ·
[Dependências Linux]($repository/blob/v$version/docs/LINUX.md).
GPLv3 ou posterior (GPL-3.0-or-later), sem garantia, nos termos da licença.
Texto integral e avisos das bibliotecas estão nas configurações do programa.

## Privacidade e suporte

Certificados, chaves e senhas não são enviados à internet. Somente o idioma é
persistido nas configurações de cada usuário.
[Privacidade]($repository/blob/v$version/docs/PRIVACY.pt.md) ·
[Privacy]($repository/blob/v$version/docs/PRIVACY.en.md) ·
[Privacidad]($repository/blob/v$version/docs/PRIVACY.es.md) ·
[Suporte]($repository/issues).
Use somente dados fictícios para relatar problemas. O indicador de prazo não
confirma confiança pública, revogação ou validade TLS.
"@
[IO.File]::WriteAllText((Join-Path $releaseDirectory 'RELEASE_NOTES.md'), $text.Replace("`r`n", "`n") + "`n", $utf8)
