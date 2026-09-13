<#
.SYNOPSIS
    Publie d'un coup le serveur, l'APK Android et le setup Windows.

.DESCRIPTION
    Enchaine deploy-serveur.ps1, deploy-android.ps1 et deploy-windows.ps1 avec la
    meme version et les memes notes. Le serveur passe en premier : il sert le
    manifeste que les clients interrogeront ensuite.

    Le numero de version est calcule UNE SEULE FOIS puis impose aux trois scripts :
    sans cela, chacun incrementerait de son cote et les plateformes se retrouveraient
    sur des versions differentes.

.PARAMETER Version
    Version a publier, ex. 1.2.0. Si omis, la version du csproj est incrementee automatiquement.

.PARAMETER Increment
    Partie du numero a incrementer quand -Version est omis : patch (defaut), minor ou major.

.PARAMETER SansIncrement
    Republie la version actuelle sans l'incrementer (utile apres une copie interrompue).
    Attention : les clients deja a jour ne verront alors aucune nouvelle version.

.PARAMETER Notes
    Notes de version communes, affichees aux utilisateurs.

.PARAMETER Sauf
    Cibles a ignorer : serveur, android, windows.

.EXAMPLE
    .\deploy\deploy-tout.ps1 -Notes "Apercu 3D corrige sur Android"
    Publie tout en incrementant automatiquement (1.1.1 -> 1.1.2).

.EXAMPLE
    .\deploy\deploy-tout.ps1 -Increment minor -Notes "Nouvelle page de recherche"

.EXAMPLE
    .\deploy\deploy-tout.ps1 -Sauf windows
#>

param(
    [string]$Version,
    [ValidateSet("patch", "minor", "major")] [string]$Increment = "patch",
    [switch]$SansIncrement,
    [string]$Notes = "",
    [ValidateSet("serveur", "android", "windows")] [string[]]$Sauf = @()
)

. "$PSScriptRoot\_commun.ps1"

Assert-PartageNas

$cibles = @("serveur", "android", "windows") | Where-Object { $Sauf -notcontains $_ }

# Version et numero de build fixes ici pour toutes les cibles : chaque script appele
# recevra -Version et -Build explicites et n'incrementera donc pas de son cote.
$actuelle = Get-VersionClient
$Version = Resolve-VersionAPublier -Version $Version -Niveau $Increment -SansIncrement:$SansIncrement

if ($Version) {
    $build = $actuelle.Build + 1
    Write-Host "Version a publier : $Version (build $build) — precedente : $($actuelle.Version)" -ForegroundColor Cyan
} else {
    $build = $actuelle.Build
    Write-Avert "Republication a l'identique de la version $($actuelle.Version) (build $build)."
}

Write-Host "Cibles : $($cibles -join ', ')" -ForegroundColor Cyan

$resultats = @()

foreach ($cible in $cibles) {
    $script = Join-Path $PSScriptRoot "deploy-$cible.ps1"

    $parametres = @{ Notes = $Notes }
    if ($Version) {
        $parametres.Version = $Version
        # Le serveur n'a pas de numero de build (il ne lit pas le csproj).
        if ($cible -ne "serveur") { $parametres.Build = $build }
    } elseif ($cible -ne "serveur") {
        $parametres.SansIncrement = $true
    }

    Write-Etape "Deploiement : $cible"
    & $script @parametres

    $resultats += [pscustomobject]@{
        Cible  = $cible
        Statut = if ($LASTEXITCODE -eq 0) { "OK" } else { "ECHEC ($LASTEXITCODE)" }
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Avert "Le deploiement '$cible' a echoue, les suivants sont abandonnes."
        break
    }
}

Write-Etape "Recapitulatif"
$resultats | Format-Table -AutoSize
