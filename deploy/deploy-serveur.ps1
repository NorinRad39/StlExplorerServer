<#
.SYNOPSIS
    Construit l'image Docker du serveur, la publie sur le NAS et declenche son installation.

.DESCRIPTION
    1. reconstruit l'image stlexplorer-server:latest ;
    2. l'exporte dans V:\STLExplorer\Serveur sous un nom versionne ;
    3. ecrit serveur.json, le marqueur que la tache planifiee du NAS surveille :
       des qu'il change, le NAS charge la nouvelle image et redemarre le conteneur
       (voir deploy\nas\stlexplorer-autoupdate.sh) ;
    4. si l'acces SSH au NAS est configure, declenche l'installation immediatement
       au lieu d'attendre le prochain passage de la tache planifiee.

.PARAMETER Version
    Version a publier, ex. 1.2.0. Par defaut : horodatage du jour (ex. 2026.09.13-2215).

.PARAMETER Notes
    Description de la mise a jour, ecrite dans le manifeste.

.PARAMETER SansInstallation
    Depose le paquet sur le NAS sans tenter l'installation immediate par SSH.

.EXAMPLE
    .\deploy\deploy-serveur.ps1 -Version 1.2.0 -Notes "Endpoint de mise a jour"
#>

param(
    [string]$Version,
    [string]$Notes = "",
    [switch]$SansInstallation,
    [switch]$SansCommit
)

. "$PSScriptRoot\_commun.ps1"

Assert-Prerequis -Cibles @("serveur")
Set-Location $RacineDepot

if (-not $Version) { $Version = (Get-Date).ToString("yyyy.MM.dd-HHmm") }

Write-Etape "Reconstruction de l'image stlexplorer-server:latest"
docker compose build stlexplorer-api
Stop-SurErreur "Echec du build de l'image Docker."

Write-Etape "Export de l'image"
if (-not (Test-Path $DossierServeur)) { New-Item -ItemType Directory -Path $DossierServeur | Out-Null }

$nomTar = "stlexplorer-server-$Version.tar"
$destination = Join-Path $DossierServeur $nomTar

# On exporte d'abord en local (rapide) avant de copier sur le partage reseau :
# ecrire directement 100 Mo en SMB est nettement plus lent et plus fragile.
$tarLocal = Join-Path $RacineDepot "stlexplorer-server.tar"
docker save -o $tarLocal stlexplorer-server:latest
Stop-SurErreur "Echec de l'export de l'image Docker."

Copy-Item $tarLocal $destination -Force
$taille = (Get-Item $destination).Length
Write-Ok "Image publiee : $destination ($(Format-Mo $taille))"

Write-Etape "Marqueur de mise a jour pour le NAS"
# Ce fichier est lu par la tache planifiee du NAS (stlexplorer-autoupdate.sh).
$marqueur = [ordered]@{
    version = $Version
    fichier = $nomTar
    image   = "stlexplorer-server:latest"
    taille  = $taille
    date    = (Get-Date).ToString("s")
    notes   = $Notes
}
($marqueur | ConvertTo-Json -Depth 4) |
    Set-Content -Path (Join-Path $DossierServeur "serveur.json") -Encoding UTF8
Write-Ok "serveur.json ecrit (version $Version)"

Update-Manifeste -Plateforme "serveur" `
                 -Version $Version `
                 -FichierRelatif "Serveur/$nomTar" `
                 -Taille $taille `
                 -Notes $Notes

Remove-AnciensPaquets -Dossier $DossierServeur -Filtre "stlexplorer-server-*.tar" -AGarder 1

if (-not $SansCommit) {
    $titre = if ($Notes) { "Deploiement serveur $Version : $Notes" } else { "Deploiement serveur $Version" }
    Invoke-CommitEtPush -Titre $titre
}

if ($SansInstallation) {
    Write-Etape "Termine (installation non declenchee)"
    Write-Host "Le NAS installera la version $Version au prochain passage de sa tache planifiee."
    exit 0
}

Write-Etape "Installation immediate sur le NAS (SSH)"

# La cle dediee est passee explicitement : aucun ~/.ssh/config a maintenir.
$optionsSsh = @("-i", $CleSsh, "-o", "BatchMode=yes", "-o", "ConnectTimeout=8")

# ssh ecrit sur stderr quand la connexion echoue ; sans cet assouplissement,
# $ErrorActionPreference='Stop' transformerait ce cas normal en erreur fatale.
$ErrorActionPreference = "Continue"
ssh @optionsSsh $CibleSsh "echo ok" 2>$null | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Avert "SSH non configure : installation immediate impossible."
    Write-Info "Le NAS prendra la mise a jour au prochain passage de sa tache planifiee,"
    Write-Info "ou tu peux l'importer manuellement dans Container Manager."
    Write-Info "Pour activer l'installation immediate : voir deploy\nas\README-NAS.md"
    exit 0
}

ssh @optionsSsh $CibleSsh "sudo $DossierServeurNas/stlexplorer-autoupdate.sh --maintenant"
$codeInstallation = $LASTEXITCODE
$ErrorActionPreference = "Stop"

if ($codeInstallation -ne 0) {
    Write-Avert "L'installation distante a renvoye une erreur (code $codeInstallation)."
    Write-Info "La tache planifiee du NAS reessaiera automatiquement."
    exit 0
}

Write-Etape "Termine"
Write-Ok "Serveur $Version installe sur le NAS."
