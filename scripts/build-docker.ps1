<#
.SYNOPSIS
    Reconstruit l'image Docker du serveur StlExplorer et l'exporte, prete a deployer sur le NAS.

.DESCRIPTION
    Reconstruit l'image stlexplorer-server:latest puis l'exporte dans
    stlexplorer-server.tar a la racine du depot, prete a etre importee
    dans Container Manager sur le NAS.

.EXAMPLE
    .\scripts\build-docker.ps1
#>

$ErrorActionPreference = "Stop"

# Toujours travailler depuis la racine du depot (ce script vit dans scripts/)
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Write-Etape($texte) {
    Write-Host ""
    Write-Host "=== $texte ===" -ForegroundColor Cyan
}

function Assert-DerniereCommande($message) {
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ $message (code $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

Write-Etape "Verification de Docker"

docker info *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Docker ne repond pas. Verifie que Docker Desktop est demarre." -ForegroundColor Red
    exit 1
}

Write-Etape "Reconstruction de l'image stlexplorer-server:latest"
docker compose build stlexplorer-api
Assert-DerniereCommande "Echec du build de l'image Docker."

Write-Etape "Export de l'image vers stlexplorer-server.tar"
$tarPath = Join-Path $repoRoot "stlexplorer-server.tar"
docker save -o $tarPath stlexplorer-server:latest
Assert-DerniereCommande "Echec de l'export de l'image Docker."

$tailleMo = [Math]::Round((Get-Item $tarPath).Length / 1MB, 1)
Write-Host "✅ Image exportee : $tarPath ($tailleMo Mo)" -ForegroundColor Green
Write-Host "   -> a importer dans Container Manager sur le NAS."
