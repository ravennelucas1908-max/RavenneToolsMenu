# Finalise la mise en place GitHub après le grant du scope "workflow" :
#   gh auth refresh -h github.com -s workflow
# Puis exécute ce script : il pousse les workflows CI/Release et déclenche la release v1.0.0.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path "$root\.github\workflows\release.yml")) {
    Write-Error "Les workflows sont introuvables dans .github/workflows."
    exit 1
}

git -C $root add .github
git -C $root commit -m "CI + Release : installateur automatique via GitHub Actions" -m "Vérifie les mises à jour via GitHub Releases (auto-update intégré)."
git -C $root push origin main

$version = [regex]::Match((Get-Content "$root\src\RavenneToolsMenu\RavenneToolsMenu.csproj" -Raw), '<Version>([\d.]+)</Version>').Groups[1].Value
$tag = "v$version"
if (-not (git -C $root tag -l $tag)) {
    git -C $root tag $tag
    git -C $root push origin $tag
    Write-Host "Tag $tag poussé — la release se construit sur GitHub Actions :" -ForegroundColor Green
} else {
    Write-Host "Le tag $tag existe déjà." -ForegroundColor Yellow
}
Write-Host "  https://github.com/ravennelucas1908-max/RavenneToolsMenu/actions"
