<#
.SYNOPSIS
    Regenere l'APK Android du client StlExplorer, pret a installer.

.DESCRIPTION
    Publie StlExplorerClient en Release pour Android et copie l'APK signe
    a la racine du depot sous un nom fixe (StlExplorerClient.apk), facile
    a retrouver pour un `adb install` ou un transfert manuel sur le telephone.

.EXAMPLE
    .\scripts\build-apk.ps1
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

Write-Etape "Publication de l'APK Android (Release)"

dotnet publish "StlExplorerClient\StlExplorerClient.csproj" -f net10.0-android -c Release
Assert-DerniereCommande "Echec de la publication Android."

$apkSource = Get-ChildItem "StlExplorerClient\bin\Release\net10.0-android\publish" -Filter "*-Signed.apk" |
    Select-Object -First 1

if (-not $apkSource) {
    Write-Host "❌ Aucun APK signe trouve apres la publication." -ForegroundColor Red
    exit 1
}

$apkDestination = Join-Path $repoRoot "StlExplorerClient.apk"
Copy-Item $apkSource.FullName $apkDestination -Force

$tailleMo = [Math]::Round((Get-Item $apkDestination).Length / 1MB, 1)
Write-Host "✅ APK genere : $apkDestination ($tailleMo Mo)" -ForegroundColor Green
