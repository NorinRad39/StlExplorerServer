<#
    Fonctions partagees par les scripts de deploiement.
    Ce fichier n'est pas lance directement : il est charge via ". $PSScriptRoot\_commun.ps1".
#>

$ErrorActionPreference = "Stop"

# Racine du depot (ce fichier vit dans deploy\)
$script:RacineDepot   = Split-Path -Parent $PSScriptRoot

# --- Acces au NAS -----------------------------------------------------------
$script:HoteNas        = "192.168.20.253"
$script:UtilisateurNas = "florent"
$script:CibleSsh       = "$($script:UtilisateurNas)@$($script:HoteNas)"
$script:CleSsh         = Join-Path $env:USERPROFILE ".ssh\nas_stlexplorer"
# Chemin du dossier de deploiement vu depuis le NAS (partage "Applications")
$script:DossierServeurNas = "/volume1/Applications/STLExplorer/Serveur"

$script:PartageNas    = "V:\STLExplorer"
$script:DossierAndroid = Join-Path $script:PartageNas "Android"
$script:DossierWindows = Join-Path $script:PartageNas "Windows"
$script:DossierServeur = Join-Path $script:PartageNas "Serveur"
$script:CheminManifeste = Join-Path $script:PartageNas "update.json"
$script:ProjetClient   = Join-Path $script:RacineDepot "StlExplorerClient\StlExplorerClient.csproj"

function Write-Etape($texte) {
    Write-Host ""
    Write-Host "=== $texte ===" -ForegroundColor Cyan
}

function Write-Ok($texte)    { Write-Host "[OK] $texte" -ForegroundColor Green }
function Write-Info($texte)  { Write-Host "     $texte" -ForegroundColor Gray }
function Write-Avert($texte) { Write-Host "[!]  $texte" -ForegroundColor Yellow }

function Stop-SurErreur($message) {
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ECHEC] $message (code $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

<#
    Verifie que le partage NAS (lecteur V:) est monte, sinon arrete tout :
    deployer dans un dossier local cree par erreur ne servirait a rien.
#>
function Assert-PartageNas {
    if (-not (Test-Path $script:PartageNas)) {
        Write-Host "[ECHEC] Partage NAS introuvable : $($script:PartageNas)" -ForegroundColor Red
        Write-Host "        Verifie que le lecteur V: (\\ds923.file4all.fr\Applications) est monte." -ForegroundColor Red
        exit 1
    }
}

<#
    Emplacement du compilateur Inno Setup, ou $null s'il n'est pas installe.
#>
function Get-CheminInnoSetup {
    foreach ($candidat in @("C:\Program Files\Inno Setup 7\ISCC.exe",
                            "C:\Program Files (x86)\Inno Setup 6\ISCC.exe")) {
        if (Test-Path $candidat) { return $candidat }
    }
    return $null
}

<#
    Verifie tout ce dont les cibles demandees ont besoin, AVANT la moindre
    modification du depot ou du partage.

    L'ordre compte : la premiere version de ces scripts incrementait le numero de
    version puis decouvrait que Docker etait arrete. Le numero etait consomme pour
    rien, et la publication s'arretait a mi-chemin, laissant les cibles suivantes
    non publiees alors que le csproj avait deja bouge.
#>
function Assert-Prerequis {
    param([Parameter(Mandatory)] [string[]]$Cibles)

    Assert-PartageNas

    if ($Cibles -contains "serveur") {
        # docker ecrit sur stderr quand le demon est arrete : sans cet assouplissement,
        # $ErrorActionPreference='Stop' transformerait le controle en erreur fatale.
        $ancienne = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        docker info 2>$null | Out-Null
        $code = $LASTEXITCODE
        $ErrorActionPreference = $ancienne

        if ($code -ne 0) {
            Write-Host "[ECHEC] Docker ne repond pas. Demarre Docker Desktop, attends" -ForegroundColor Red
            Write-Host "        qu'il soit pret, puis relance ce script." -ForegroundColor Red
            exit 1
        }
        Write-Ok "Docker operationnel"
    }

    if ($Cibles -contains "windows") {
        if (-not (Get-CheminInnoSetup)) {
            Write-Host "[ECHEC] Compilateur Inno Setup (ISCC.exe) introuvable." -ForegroundColor Red
            exit 1
        }
        Write-Ok "Inno Setup disponible"
    }
}

<#
    Lit la version du client depuis StlExplorerClient.csproj, qui fait office de
    source de verite : ApplicationDisplayVersion (ex. 1.1.0) et ApplicationVersion
    (versionCode Android, entier croissant).
#>
function Get-VersionClient {
    [xml]$csproj = Get-Content $script:ProjetClient
    $groupe = $csproj.Project.PropertyGroup | Where-Object { $_.ApplicationDisplayVersion } | Select-Object -First 1

    if (-not $groupe) {
        throw "ApplicationDisplayVersion introuvable dans $($script:ProjetClient)"
    }

    [pscustomobject]@{
        Version = "$($groupe.ApplicationDisplayVersion)".Trim()
        Build   = [int]"$($groupe.ApplicationVersion)".Trim()
    }
}

<#
    Ecrit une nouvelle version dans le csproj du client (utilise par -Version).
    Le numero de build est incremente automatiquement : Android refuse d'installer
    une mise a jour dont le versionCode n'a pas augmente.
#>
function Set-VersionClient {
    param(
        [Parameter(Mandatory)] [string]$Version,
        [int]$Build = 0
    )

    $actuelle = Get-VersionClient
    if ($Build -le 0) { $Build = $actuelle.Build + 1 }

    # Lecture/ecriture via .NET : Get-Content et Set-Content de Windows PowerShell
    # utilisent la page de codes ANSI et corrompent les accents du fichier.
    $contenu = [System.IO.File]::ReadAllText($script:ProjetClient)
    $contenu = $contenu -replace '<ApplicationDisplayVersion>[^<]*</ApplicationDisplayVersion>', "<ApplicationDisplayVersion>$Version</ApplicationDisplayVersion>"
    $contenu = $contenu -replace '<ApplicationVersion>[^<]*</ApplicationVersion>', "<ApplicationVersion>$Build</ApplicationVersion>"
    [System.IO.File]::WriteAllText($script:ProjetClient, $contenu, (New-Object System.Text.UTF8Encoding($false)))

    Write-Ok "Version du client : $Version (build $Build)"
    [pscustomobject]@{ Version = $Version; Build = $Build }
}

<#
    Calcule le numero de version suivant a partir de l'actuel.
    Niveau : patch (1.1.1 -> 1.1.2), minor (1.1.1 -> 1.2.0) ou major (1.1.1 -> 2.0.0).
#>
function Get-VersionSuivante {
    param(
        [Parameter(Mandatory)] [string]$Version,
        [ValidateSet("patch", "minor", "major")] [string]$Niveau = "patch"
    )

    $parties = @($Version.Split('.'))
    while ($parties.Count -lt 3) { $parties += "0" }

    $majeur = [int]$parties[0]
    $mineur = [int]$parties[1]
    $patch  = [int]$parties[2]

    switch ($Niveau) {
        "major" { $majeur++; $mineur = 0; $patch = 0 }
        "minor" { $mineur++; $patch = 0 }
        default { $patch++ }
    }

    return "$majeur.$mineur.$patch"
}

<#
    Determine la version a publier pour les clients :
    -Version explicite si fournie, sinon increment automatique, sauf -SansIncrement
    (republication a l'identique, par exemple apres une copie interrompue).
#>
function Resolve-VersionAPublier {
    param(
        [string]$Version,
        [string]$Niveau = "patch",
        [switch]$SansIncrement
    )

    if ($Version) { return $Version }
    if ($SansIncrement) { return $null }   # null = conserver la version du csproj

    return Get-VersionSuivante -Version (Get-VersionClient).Version -Niveau $Niveau
}

<#
    Met a jour une section du manifeste update.json sur le NAS, en conservant
    les autres plateformes. C'est ce fichier que les clients interrogent via
    l'API (/api/Update/manifest) pour savoir si une mise a jour existe.
#>
function Update-Manifeste {
    param(
        [Parameter(Mandatory)] [ValidateSet("android", "windows", "serveur")] [string]$Plateforme,
        [Parameter(Mandatory)] [string]$Version,
        [int]$Build = 0,
        [Parameter(Mandatory)] [string]$FichierRelatif,
        [long]$Taille = 0,
        [string]$Notes = ""
    )

    $manifeste = @{}
    if (Test-Path $script:CheminManifeste) {
        try {
            $json = Get-Content $script:CheminManifeste -Raw
            if ($json.Trim()) {
                (ConvertFrom-Json $json).PSObject.Properties | ForEach-Object {
                    $manifeste[$_.Name] = $_.Value
                }
            }
        } catch {
            Write-Avert "Manifeste existant illisible, il est reconstruit : $_"
        }
    }

    $manifeste[$Plateforme] = [ordered]@{
        version = $Version
        build   = $Build
        fichier = $FichierRelatif
        taille  = $Taille
        date    = (Get-Date).ToString("s")
        notes   = $Notes
    }

    ($manifeste | ConvertTo-Json -Depth 6) | Set-Content -Path $script:CheminManifeste -Encoding UTF8
    Write-Ok "Manifeste mis a jour : $($script:CheminManifeste) [$Plateforme $Version]"
}

<#
    Supprime les anciens paquets d'un dossier du NAS, en ne gardant que les N plus
    recents (1 par defaut) : le manifeste ne reference que la derniere version,
    les precedentes n'encombrent que le partage.
#>
function Remove-AnciensPaquets {
    param(
        [Parameter(Mandatory)] [string]$Dossier,
        [Parameter(Mandatory)] [string]$Filtre,
        [int]$AGarder = 1
    )

    $anciens = Get-ChildItem -Path $Dossier -Filter $Filtre -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -Skip $AGarder

    foreach ($fichier in $anciens) {
        Remove-Item $fichier.FullName -Force -ErrorAction SilentlyContinue
        Write-Info "Ancien paquet supprime : $($fichier.Name)"
    }
}

function Format-Mo($octets) {
    return "{0:N1} Mo" -f ($octets / 1MB)
}
