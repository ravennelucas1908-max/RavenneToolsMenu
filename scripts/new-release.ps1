# Publie une nouvelle version de Ravenne's Tools Menu.
#
# Usage :
#   powershell scripts/new-release.ps1                  # release de la version actuelle du .csproj
#   powershell scripts/new-release.ps1 -Version 1.1.0   # incrémente la version puis release
#
# Étapes : bump .csproj + installateur → commit → tag vX.Y.Z → push.
# Le workflow GitHub Actions "Release" compile alors l'installateur et publie la release.
param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $root "src\RavenneToolsMenu\RavenneToolsMenu.csproj"
$iss = Join-Path $root "installer\RavenneToolsMenu.iss"

$content = Get-Content $csproj -Raw
$current = [regex]::Match($content, '<Version>([\d.]+)</Version>').Groups[1].Value
if (-not $Version) { $Version = $current }

if ($Version -ne $current) {
    # bump .csproj
    $content = $content -replace "<Version>$current</Version>", "<Version>$Version</Version>"
    Set-Content -Path $csproj -Value $content -NoNewline -Encoding UTF8
    # bump installateur
    $issContent = Get-Content $iss -Raw
    $issContent = $issContent -replace '#define AppVersion "[\d.]+"', "#define AppVersion `"$Version`""
    Set-Content -Path $iss -Value $issContent -NoNewline -Encoding UTF8

    git -C $root add $csproj $iss
    git -C $root commit -m "v$Version"
}

$tag = "v$Version"
$existing = git -C $root tag -l $tag
if ($existing) {
    Write-Host "Le tag $tag existe déjà — pas de nouvelle release créée." -ForegroundColor Yellow
    exit 1
}

git -C $root tag $tag
git -C $root push origin main --tags
Write-Host "Tag $tag poussé — GitHub Actions compile l'installateur et publie la release :" -ForegroundColor Green
Write-Host "  https://github.com/ravennelucas1908-max/RavenneToolsMenu/actions"
