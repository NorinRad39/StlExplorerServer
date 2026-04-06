# Contexte et Directives du Projet StlExplorerServer

Ce document sert de manuel de reference central pour le projet. Il est redige pour etre facilement lisible par les developpeurs humains tout en servant de fichier d'instructions (Copilot Instructions) charge automatiquement par l'IA de GitHub Copilot.

## 1. Vue d'ensemble du Projet

Ce projet est une application client-serveur destinee a indexer, organiser et previsualiser des fichiers d'impression 3D (STL) heberges sur un NAS (Synology DS923+).

**Architecture :**
- **Serveur (Backend) :** API Web .NET 10 (ASP.NET Core), projet `StlExplorerServer`.
- **Client :** Application .NET MAUI (Windows + Android), projet `StlExplorerClient`.
- **Bibliotheque partagee :** `ClassLibStlExploServ` -- contient les entites metier (`Modele`, `Sujet`, `Famille`, `ModeleResume`, `ContenuModele`, `Fichier3D`, DTOs).
- **Hebergement :** Prevu dans un conteneur Docker sur le NAS.
- **Base de Donnees :** MariaDB 10.11 (hebergee sur le NAS, accessible par IP).
- **ORM :** Entity Framework Core 9.0 + Pomelo.EntityFrameworkCore.MySql 9.0.
- **Bibliotheques :** SharpCompress (lecture d'archives RAR/7z), Swashbuckle (Swagger).

---

## 2. Regles Metier et Hierarchie des Donnees

Les fichiers sont organises physiquement (sur le disque) et logiquement (en base de donnees) selon une hierarchie stricte a 3 niveaux :

1.  **Famille** (Niveau 1) : Niveau le plus haut (ex: `Marvel`).
2.  **Sujet** (Niveau 2) : Sous-categorie (ex: `Galactus`).
3.  **Modele** (Niveau 3) : Dossier final contenant les fichiers du modele 3D.
    - *Note systeme :* Anciennement appele "Packet" -- ce terme est **totalement obsolete**.
    - Contient : Fichiers STL (.stl), Archives (.zip, .rar, .7z), et Photos/Images (.jpg, .png, .webp).
    - **Regle de profondeur fixe :** Tout sous-dossier au-dela du niveau 3 (ex: "fichiers_repares", "evides") est considere comme appartenant au Modele parent de niveau 3. L'algorithme de scan ne doit pas s'en servir comme niveau hierarchique principal.

*Exemple de chemin :* `Chemin_Racine\Marvel\Galactus\Galactus - Statue wicked`

---

## 3. Architecture Technique Detaillee

### 3.1 Services et Injection de Dependances (DI)

| Service | Duree de vie | Role |
|---|---|---|
| `IFolderScannerService` -> `FolderScannerService` | Scoped | Logique de scan de dossiers, synchronisation disque / BDD |
| `IMetadonneesRepository` -> `MetadataRepository` | Scoped | Acces aux donnees EF Core (CRUD Famille/Sujet/Modele) |
| `BackgroundScannerHostedService` | Singleton + HostedService | Scan en arriere-plan, FileSystemWatcher, debounce, gestion des scopes DI |
| `ApplicationDbContext` | Scoped | Contexte EF Core avec conversion JSON explicite pour `CheminsImages` |

**Regle critique DI :** Les services scoped (`DbContext`, `Repository`, `FolderScannerService`) ne doivent **jamais** etre utilises directement dans un contexte background (Task.Run, HostedService). Le `BackgroundScannerHostedService` cree un scope DI interne via `IServiceScopeFactory.CreateScope()` pour chaque operation background.

### 3.2 Scan en Arriere-Plan (`BackgroundScannerHostedService`)

- **Scan automatique au demarrage :** Apres un delai de 5 secondes, si des dossiers sont configures, un scan initial est lance.
- **Surveillance FileSystemWatcher :** Un `FileSystemWatcher` par dossier racine detecte les changements (ajout, suppression, renommage, modification).
- **Debounce 5 secondes :** Plusieurs changements rapproches ne declenchent qu'un seul scan, 5 secondes apres le dernier changement.
- **Protection anti-concurrence :** `SemaphoreSlim(1,1)` empeche les scans simultanes.
- **Progression :** `FolderScannerService.ProgressionScanStatique` (internal static, thread-safe via lock) est lu par le hosted service.

### 3.3 Conversion JSON pour `CheminsImages`

La propriete `List<string> CheminsImages` de `Modele` est stockee en colonne `longtext` dans MariaDB. La conversion JSON est configuree explicitement dans `ApplicationDbContext.OnModelCreating` via `HasConversion` avec `JsonSerializer.Serialize`/`Deserialize`. Ne pas supprimer cette configuration.

### 3.4 Client MAUI -- Chargement des Images

Le client MAUI charge les images via `HttpClient.GetAsync` + `ImageSource.FromStream(new MemoryStream(bytes))` (methode `DisplayCurrentImageAsync()`). **Ne pas utiliser** `ImageSource.FromUri` avec des chemins UNC encodes -- cela ne fonctionne pas de maniere fiable sur toutes les plateformes MAUI.

### 3.5 Endpoints API Existants

| Methode | Route | Description |
|---|---|---|
| POST | `/api/Metadata/scanAll` | Scan synchrone de tous les dossiers configures |
| POST | `/api/Metadata/refreshAll` | Synchronisation complete en tache de fond (via BackgroundScannerHostedService) |
| POST | `/api/Metadata/sync-intelligent` | Synchronisation intelligente en tache de fond |
| GET | `/api/Metadata/modeles` | Liste complete de tous les modeles |
| GET | `/api/Metadata/modelesResume` | Liste legere (projection) de tous les modeles |
| GET | `/api/Metadata/modele/{id}/images` | Chemins d'images d'un modele |
| GET | `/api/Metadata/image?chemin=...` | Sert une image depuis son chemin absolu NAS |
| POST | `/api/Metadata/creerModele` | Cree l'arborescence Famille > Sujet > Modele |
| POST | `/api/Metadata/uploadFichiers/{modeleId}` | Televerse des fichiers dans un modele |
| GET | `/api/Metadata/modele/{id}/contenu` | Liste fichiers, dossiers et fichiers 3D d'un modele |
| GET | `/api/Metadata/modele/{id}/fichier3d?nom=...&archive=...` | Sert un fichier 3D (direct ou extrait d'archive) |
| PUT | `/api/Metadata/renommerModele/{id}` | Renomme le dossier physique + mise a jour BDD |
| GET | `/api/Metadata/scan-status` | Verifie si un scan est en cours |
| GET | `/api/Metadata/scan-progress` | Pourcentage d'avancement du scan |
| POST | `/api/Metadata/invalidate-cache` | Vide le cache memoire |
| GET | `/api/Metadata/configuration/rootDirectories` | Liste des dossiers racines configures |
| PUT | `/api/Metadata/configuration/rootDirectories` | Met a jour les dossiers racines dans appsettings.json |

---

## 4. Directives Strictes pour GitHub Copilot (IA)

- **Terminologie :**
  - Le terme "Packet" est **totalement obsolete** dans ce projet. Utiliser strictement le terme `Modele`.
  - Preferer le nommage francise pour les interfaces et services lies au domaine metier (ex: `IMetadonneesRepository` plutot que `IMetadataRepository`).
- **Structure de la donnee :** Respecter imperativement l'architecture `Famille > Sujet > Modele` dans toutes les requetes LINQ, creations d'entites, ou algorithmes de parcours de fichiers.
- **Stockage des Chemins :** Les modeles contiennent expressement des proprietes pour le chemin du dossier (`CheminDossier`) et les chemins des images (`CheminsImages`). Ils doivent etre hydrates par le scanner applicatif.
- **Optimisation :** L'application tournera sur un NAS. Le code genere, notamment pour le traitement des images/fichiers 3D, doit etre econome en memoire (RAM) et optimise pour des IOPS potentiellement lents.
- **Scopes DI :** Ne jamais capturer un service scoped (`DbContext`, `Repository`) dans un contexte background sans creer un scope DI via `IServiceScopeFactory.CreateScope()`.
- **Images cote client :** Toujours utiliser `HttpClient` + `ImageSource.FromStream` pour charger les images dans le client MAUI. Ne jamais utiliser `ImageSource.FromUri` avec des chemins UNC.
- **Synchronisation disque / BDD :** La methode `SynchroniserModelesAvecDisque` doit filtrer les modeles par chemin racine (`StartsWith`) pour ne pas supprimer les modeles d'autres dossiers racines.

---

## 5. Aide-memoire : Configuration et Migrations (EF Core)

**Etape 1 : Le fichier `appsettings.json`**
Le fichier `appsettings.json` du projet API contient les parametres du scanner et la connexion a la base de donnees.
*(Rappel : Les chemins UNC reseau doivent utiliser des doubles antislashs echappes dans le JSON `\\192...`).*
*(Rappel : La connexion a la base MariaDB doit utiliser une IP, pas une URL DNS/reverse proxy.)*

```json
{
  "ScannerSettings": {
    "RootDirectories": [
      "\\\\192.168.20.253\\Maquette\\3D_Maquettes"
    ]
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=192.168.20.253;Port=3306;Database=StlExplorerServer;User=USER;Password=MOT_DE_PASSE;"
  }
}
```

**Etape 2 : Migrations MariaDB**
Lorsque le modele de donnees C# evolue, la base de donnees MariaDB doit etre mise a jour.
Commandes a executer dans le terminal integre (a la racine du projet `StlExplorerServer`) :

1. **Creer une nouvelle migration :**
   `dotnet ef migrations add <NomDeLaMigration>`

2. **Appliquer la migration :**
   `dotnet ef database update`

*Note :* Les migrations sont appliquees automatiquement au demarrage du serveur (`Program.cs` contient une boucle de retry pour laisser le temps a MariaDB de demarrer dans Docker).

---

## 6. Directives Techniques pour le Scan, le Cache et la Base

- **Scan en arriere-plan obligatoire :** Tout scan de dossiers doit passer par `BackgroundScannerHostedService` qui cree un scope DI interne. Il est interdit de lancer un scan dans un `Task.Run` qui capture des services scoped depuis le scope de la requete HTTP.
- **Prevention des scans concurrents :** Le `SemaphoreSlim(1,1)` dans `BackgroundScannerHostedService` empeche les scans simultanes. `LancerSynchronisationAsync()` retourne `false` si un scan est deja en cours.
- **Cache des metadonnees :** Les metadonnees scannees sont mises en cache en memoire (dictionnaire statique dans `FolderScannerService`). Le cache est invalide automatiquement apres chaque scan et manuellement via l'endpoint `POST /api/Metadata/invalidate-cache`.
- **Scan automatique au demarrage :** `BackgroundScannerHostedService.ExecuteAsync` lance un scan initial 5 secondes apres le demarrage si des dossiers sont configures.
- **Surveillance automatique :** `FileSystemWatcher` sur chaque dossier racine avec debounce de 5 secondes declenche une synchronisation automatique quand des fichiers/dossiers sont ajoutes, supprimes, renommes ou modifies.
- **Connexion a la base :** La connexion a la base MariaDB doit utiliser une IP dans la chaine de connexion (pas d'URL DNS/reverse proxy). La version serveur est fixee a `MariaDbServerVersion(10, 11, 0)` avec retry on failure.
- **Actualisation de la base :** `ActualiserBaseDepuisDossier(path)` supprime les modeles absents du disque puis re-scanne pour ajouter/mettre a jour. `SynchronisationIntelligente()` fait un scan complet pour les nouveaux dossiers racines et une mise a jour pour les dossiers deja connus.

---

## 7. Structure des Fichiers Cles

```
StlExplorerServer/
  Controllers/
    MetadataController.cs          # Controleur API principal (tous les endpoints)
  Data/
    ApplicationDbContext.cs         # DbContext EF Core (OnModelCreating avec HasConversion JSON)
  Migrations/                      # Migrations EF Core auto-generees
  Repositories/
    IMetadonneesRepository.cs      # Interface repository
    MetadataRepository.cs          # Implementation EF Core
  Services/
    IFolderScannerService.cs       # Interface scanner (ScanFolder, SynchronisationIntelligente, etc.)
    FolderScannerService.cs        # Logique de scan Famille > Sujet > Modele
    BackgroundScannerHostedService.cs  # Service heberge (singleton) : scan background, FileSystemWatcher, debounce
  Program.cs                       # Point d'entree, DI, pipeline HTTP, auto-migration
  appsettings.json                 # Configuration (RootDirectories, ConnectionStrings)

StlExplorerClient/
  MainPage.xaml                    # Interface principale (galerie, recherche, 3D viewer)
  MainPage.xaml.cs                 # Code-behind (HttpClient image loading, navigation)
  ConfigPage.xaml.cs               # Configuration serveur, gestion dossiers racines

ClassLibStlExploServ/
  ClassLibStlExploServ.cs          # Entites : Modele, Sujet, Famille, ModeleResume, ContenuModele, Fichier3D, DTOs
```

---

## 8. Etat du Projet (Avancement)

- [x] Indexation automatique Famille > Sujet > Modele et hydratation des chemins images
- [x] Scan et synchronisation intelligents (scan complet sur nouveaux dossiers racines, mise a jour sur les autres)
- [x] Cache memoire pour les metadonnees scannees
- [x] Endpoints API pour scan, refresh, synchronisation intelligente
- [x] Client MAUI pour consultation et declenchement du scan
- [x] Scan en arriere-plan via `BackgroundScannerHostedService` (singleton + `IServiceScopeFactory`)
- [x] Surveillance automatique des dossiers via `FileSystemWatcher` avec debounce 5s
- [x] Prevention des scans concurrents via `SemaphoreSlim`
- [x] Scan automatique au demarrage du serveur
- [x] Actualisation de la base (ajout/suppression/modification de fichiers et dossiers)
- [x] Conversion JSON explicite pour `CheminsImages` dans `ApplicationDbContext`
- [x] Chargement fiable des images cote client via `HttpClient` + `ImageSource.FromStream`
- [x] Renommage de modeles (dossier NAS + BDD)
- [x] Televersement de fichiers dans un modele (API POST multipart)
- [x] Creation d'un modele (arborescence Famille > Sujet > Modele sur le NAS + BDD)
- [x] Contenu d'un modele (fichiers, dossiers, fichiers 3D, detection dans archives)
- [x] Endpoint d'invalidation du cache
- [x] Configuration des dossiers racines via API (GET/PUT)
- [ ] Deplacement d'un modele d'un sujet/famille a un autre
- [ ] Suppression d'un modele, sujet ou famille (dossier NAS + BDD)
- [ ] Suppression de fichiers individuels dans un modele
- [ ] Telechargement d'un modele complet (zip)
- [ ] Generation de miniatures (thumbnails) pour les images et fichiers STL
- [ ] Recherche avancee et pagination cote API
- [ ] Authentification/autorisation et gestion des droits
- [ ] Endpoint de sante (health check)
- [ ] Statistiques d'utilisation
- [ ] Notifications et planification de scans periodiques
- [ ] Journalisation des actions utilisateur (audit log)
- [ ] Historique des modifications (versioning, rollback)
- [ ] Renommage de sujets et familles (dossier NAS + BDD)
