<#
.SYNOPSIS
    Construit le setup Windows (Inno Setup) et le publie sur le NAS.

.DESCRIPTION
    1. (optionnel) met a jour le numero de version dans StlExplorerClient.csproj ;
    2. publie l'application Windows (autonome, win-x64) ;
    3. compile l'installateur avec Inno Setup 7 (installer\StlExplorer.iss) ;
    4. copie le setup dans V:\STLExplorer\Windows et met a jour update.json.

.PARAMETER Version
    Version a publier, ex. 1.2.0. Si omis, la version du csproj est incrementee automatiquement.

.PARAMETER Increment
    Partie du numero a incrementer quand -Version est omis : patch (defaut), minor ou major.

.PARAMETER SansIncrement
    Republie la version actuelle sans l'incrementer (utile apres une copie interrompue).
    Attention : les clients deja a jour ne verront alors aucune nouvelle version.

.PARAMETER Build
    Numero de build. Si omis avec -Version, il est incremente automatiquement.

.PARAMETER Notes
    Notes de version affichees a l'utilisateur dans la fenetre de mise a jour.

.EXAMPLE
    .\deploy\deploy-windows.ps1 -Version 1.2.0 -Notes "Apercu 3D corrige"
#>

param(
    [string]$Version,
    [ValidateSet("patch", "minor", "major")] [string]$Increment = "patch",
    [switch]$SansIncrement,
    [int]$Build = 0,
    [string]$Notes = ""
)

. "$PSScriptRoot\_commun.ps1"

Assert-Prerequis -Cibles @("windows")
Set-Location $RacineDepot

$iscc = Get-CheminInnoSetup

$Version = Resolve-VersionAPublier -Version $Version -Niveau $Increment -SansIncrement:$SansIncrement

if ($Version) {
    Write-Etape "Mise a jour du numero de version"
    $infos = Set-VersionClient -Version $Version -Build $Build
} else {
    $infos = Get-VersionClient
    Write-Info "Version republiee a l'identique : $($infos.Version) (build $($infos.Build))"
}

Write-Etape "Publication de l'application Windows (Release, win-x64)"
$dossierPublication = Join-Path $RacineDepot "publish\windows-setup"
if (Test-Path $dossierPublication) { Remove-Item $dossierPublication -Recurse -Force }

# PublishStandalone=true active le bloc de proprietes dedie du csproj (RID, self-contained).
# Ne pas passer -r / --self-contained ici : en global, ces options cassent la restauration
# des TFM Android du projet partage.
dotnet publish "StlExplorerClient\StlExplorerClient.csproj" `
    -f net10.0-windows10.0.19041.0 -c Release `
    -p:PublishStandalone=true -p:WindowsPackageType=None `
    -o $dossierPublication
Stop-SurErreur "Echec de la publication Windows."

if (-not (Test-Path (Join-Path $dossierPublication "StlExplorerClient.exe"))) {
    Write-Host "[ECHEC] StlExplorerClient.exe absent de $dossierPublication" -ForegroundColor Red
    exit 1
}

Write-Etape "Compilation de l'installateur (Inno Setup)"
& $iscc "installer\StlExplorer.iss" "/DVersionApp=$($infos.Version)" "/DDossierSource=$dossierPublication"
Stop-SurErreur "Echec de la compilation Inno Setup."

$setup = Join-Path $RacineDepot "publish\installer\STLExplorerSetup-$($infos.Version).exe"
if (-not (Test-Path $setup)) {
    Write-Host "[ECHEC] Setup introuvable : $setup" -ForegroundColor Red
    exit 1
}

Write-Etape "Copie vers le NAS"
if (-not (Test-Path $DossierWindows)) { New-Item -ItemType Directory -Path $DossierWindows | Out-Null }

$nomSetup = "STLExplorerSetup-$($infos.Version).exe"
$destination = Join-Path $DossierWindows $nomSetup
Copy-Item $setup $destination -Force

$taille = (Get-Item $destination).Length
Write-Ok "Setup publie : $destination ($(Format-Mo $taille))"


Write-Etape "Annonce de la mise a jour"
Update-Manifeste -Plateforme "windows" `
                 -Version $infos.Version `
                 -Build $infos.Build `
                 -FichierRelatif "Windows/$nomSetup" `
                 -Taille $taille `
                 -Notes $Notes

Remove-AnciensPaquets -Dossier $DossierWindows -Filtre "STLExplorerSetup-*.exe"

Write-Etape "Termine"
Write-Host "Les installations Windows proposeront la version $($infos.Version) a leur prochain demarrage."
