<#
.SYNOPSIS
    Publie l'APK Android sur le NAS et annonce la mise a jour aux clients.

.DESCRIPTION
    1. (optionnel) met a jour le numero de version dans StlExplorerClient.csproj ;
    2. publie l'application en Release pour Android ;
    3. copie l'APK versionne dans V:\STLExplorer\Android ;
    4. met a jour update.json, ce qui declenche la proposition de mise a jour
       automatique dans les apps deja installees.

.PARAMETER Version
    Version a publier, ex. 1.2.0. Si omis, la version du csproj est incrementee automatiquement.

.PARAMETER Increment
    Partie du numero a incrementer quand -Version est omis : patch (defaut), minor ou major.

.PARAMETER SansIncrement
    Republie la version actuelle sans l'incrementer (utile apres une copie interrompue).
    Attention : les clients deja a jour ne verront alors aucune nouvelle version.

.PARAMETER Build
    Numero de build (versionCode Android). Si omis avec -Version, il est incremente automatiquement.

.PARAMETER Notes
    Notes de version affichees a l'utilisateur dans la fenetre de mise a jour.

.EXAMPLE
    .\deploy\deploy-android.ps1 -Version 1.2.0 -Notes "Apercu 3D corrige"
#>

param(
    [string]$Version,
    [ValidateSet("patch", "minor", "major")] [string]$Increment = "patch",
    [switch]$SansIncrement,
    [int]$Build = 0,
    [string]$Notes = "",
    [switch]$SansCommit
)

. "$PSScriptRoot\_commun.ps1"

Assert-Prerequis -Cibles @("android")
Set-Location $RacineDepot

$Version = Resolve-VersionAPublier -Version $Version -Niveau $Increment -SansIncrement:$SansIncrement

if ($Version) {
    Write-Etape "Mise a jour du numero de version"
    $infos = Set-VersionClient -Version $Version -Build $Build
} else {
    $infos = Get-VersionClient
    Write-Info "Version republiee a l'identique : $($infos.Version) (build $($infos.Build))"
}

Write-Etape "Publication de l'APK Android (Release)"
dotnet publish "StlExplorerClient\StlExplorerClient.csproj" -f net10.0-android -c Release
Stop-SurErreur "Echec de la publication Android."

$apkSource = Get-ChildItem "StlExplorerClient\bin\Release\net10.0-android\publish" -Filter "*-Signed.apk" |
    Select-Object -First 1
if (-not $apkSource) {
    Write-Host "[ECHEC] Aucun APK signe trouve apres la publication." -ForegroundColor Red
    exit 1
}

Write-Etape "Copie vers le NAS"
if (-not (Test-Path $DossierAndroid)) { New-Item -ItemType Directory -Path $DossierAndroid | Out-Null }

$nomApk = "STLExplorer-$($infos.Version).apk"
$destination = Join-Path $DossierAndroid $nomApk
Copy-Item $apkSource.FullName $destination -Force

$taille = (Get-Item $destination).Length
Write-Ok "APK publie : $destination ($(Format-Mo $taille))"


Write-Etape "Annonce de la mise a jour"
Update-Manifeste -Plateforme "android" `
                 -Version $infos.Version `
                 -Build $infos.Build `
                 -FichierRelatif "Android/$nomApk" `
                 -Taille $taille `
                 -Notes $Notes

Remove-AnciensPaquets -Dossier $DossierAndroid -Filtre "STLExplorer-*.apk"

Write-Etape "Termine"
Write-Host "Les apps Android installees proposeront la version $($infos.Version) a leur prochain demarrage."

if (-not $SansCommit) {
    $titre = if ($Notes) { "Deploiement Android $($infos.Version)" + " : $Notes" } else { "Deploiement Android $($infos.Version)" }
    Invoke-CommitEtPush -Titre $titre
}
