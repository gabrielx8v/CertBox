# SPDX-License-Identifier: GPL-3.0-or-later
param([switch]$RequireRepository)
$ErrorActionPreference = 'Stop'
$sourceDirectory = Split-Path $PSScriptRoot -Parent
$project = Get-Content -LiteralPath (Join-Path $sourceDirectory 'project.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$repository = [string]$project.repositoryUrl
if ([string]::IsNullOrWhiteSpace($repository)) {
    if ($RequireRepository) { throw 'Set repositoryUrl in project.json before preparing a public release.' }
    return
}
$uri = $null
if (-not [Uri]::TryCreate($repository.Trim(), [UriKind]::Absolute, [ref]$uri) -or
    $uri.Scheme -ne 'https' -or $uri.Host -ne 'github.com' -or -not $uri.IsDefaultPort -or
    $uri.UserInfo -or $uri.Query -or $uri.Fragment -or
    $uri.AbsolutePath.Trim('/') -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'repositoryUrl must be an HTTPS GitHub repository URL.' }
$repository = $uri.AbsoluteUri.TrimEnd('/') -replace '\.git$', ''
if ($repository -notmatch '^https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$' -or $repository -match '/\.{1,2}$' -or $repository.EndsWith('/')) { throw 'Invalid repository name.' }
$labels = @{
    'README.md' = "[Projeto]($repository) · [Baixar executável]($repository/releases) · [Código-fonte]($repository) · [Suporte]($repository/issues)"
    'docs/README.en.md' = "[Project]($repository) · [Download executable]($repository/releases) · [Source]($repository) · [Support]($repository/issues)"
    'docs/README.es.md' = "[Proyecto]($repository) · [Descargar ejecutable]($repository/releases) · [Código fuente]($repository) · [Soporte]($repository/issues)"
}
foreach ($entry in $labels.GetEnumerator()) {
    $path = Join-Path $sourceDirectory $entry.Key
    $content = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $replacement = '<!-- project-links:start -->' + "`n" + $entry.Value + "`n" + '<!-- project-links:end -->'
    $content = [regex]::Replace($content, '(?s)<!-- project-links:start -->.*?<!-- project-links:end -->', [System.Text.RegularExpressions.MatchEvaluator]{ param($match) $replacement })
    [IO.File]::WriteAllText($path, $content, [Text.UTF8Encoding]::new($false))
}
