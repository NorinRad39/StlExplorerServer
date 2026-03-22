# Contexte et Directives du Projet StlExplorerServer

Ce document sert de manuel de référence central pour le projet. Il est rédigé pour être facilement lisible par les développeurs humains tout en servant de fichier d'instructions (Copilot Instructions) chargé automatiquement par l'IA de GitHub Copilot.

## 1. Vue d'ensemble du Projet
Ce projet est une application client-serveur destinée à indexer, organiser et prévisualiser des fichiers d'impression 3D (STL) hébergés sur un NAS (Synology DS923+).

**Architecture :**
- **Serveur (Backend) :** API Web .NET 8 (ASP.NET Core).
- **Hébergement :** Prévu dans un conteneur Docker sur le NAS.
- **Base de Données :** MariaDB (hébergée sur le NAS).
- **Clients (Futurs) :** Application Windows (WPF/MAUI) et application Android (MAUI/Native). La compatibilité Cross-Platform est une priorité.

---

## 2. Règles Métier et Hierarchie des Données

Les fichiers sont organisés physiquement (sur le disque) et logiquement (en base de données) selon une hiérarchie stricte à 3 niveaux :

1.  **Famille** (Niveau 1) : Niveau le plus haut (ex: `Marvel`).
2.  **Sujet** (Niveau 2) : Sous-catégorie (ex: `Galactus`).
3.  **Modèle** (Niveau 3) : Dossier final contenant les fichiers du modèle 3D.
    - *Note système :* Anciennement appelé "Packet".
    - Contient : Fichiers STL (.stl), Archives (.zip), et Photos/Images.
    - **Règle de profondeur fixe :** Tout sous-dossier au-delà du niveau 3 (ex: "fichiers_repares", "evides") est considéré comme appartenant au Modèle parent de niveau 3. L'algorithme de scan ne doit pas s'en servir comme niveau hiérarchique principal.

*Exemple de chemin :* `Chemin_Racine\Marvel\Galactus\Galactus - Statue wicked`

---

## 3. Fonctionnalités et Roadmap Technique

**État actuel de l'API (Serveur) :**
- L'API est connectée à une base de données MariaDB distante.
- Le système de configuration lit les dossiers à scanner depuis `appsettings.json` (section `ScannerSettings:RootDirectories`).
- **Indexation opérationnelle :** L'API mappe automatiquement l'arborescence (Famille > Sujet > Modele) et enregistre le chemin absolu du dossier de chaque Modèle, ainsi que la liste des chemins des fichiers images (`.jpg`, `.png`, etc.) qu'il contient.
- **Consultation :** Des endpoints HTTP (ex: `/api/Metadata/modeles`) fournissent la liste complète des entités au format JSON pour être consommés par un client.

**Prochaines étapes (Client & Futur) :**
- **Interface Client (Consultation) :** Créer la première version de l'application cliente (WPF ou MAUI) qui va interroger l'API pour récupérer le JSON des Modèles et afficher le catalogue avec les miniatures (en utilisant les chemins d'images fournis par le serveur).
- **Modification/Création :** Ajouter à l'API les points d'entrée nécessaires (POST/PUT/DELETE) pour permettre au client de renommer, déplacer des dossiers, ou de téléverser et créer de nouveaux Modèles depuis l'interface sans passer par l'explorateur Windows.
- **Prévisualisation 3D (Défis techniques) :** Aperçus 3D pour les STL et lecture optimisée des fichiers ZIP sans extraction complète (pour économiser les ressources NAS).

---

## 4. Directives Strictes pour GitHub Copilot (IA)

- **Terminologie :** 
  - Le terme "Packet" est totalement obsolète dans ce projet. Utiliser strictement le terme `Modele` (ou `Modèle`).
  - Préférer le nommage francisé pour les interfaces et services liés au domaine métier (ex: `IMetadonneesRepository` plutôt que `IMetadataRepository`).
- **Structure de la donnée :** Respecter impérativement l'architecture `Famille > Sujet > Modele` dans toutes les requêtes LINQ, créations d'entités, ou algorithmes de parcours de fichiers.
- **Stockage des Chemins :** Les modèles contiennent expressément des propriétés pour le chemin du dossier (`CheminDossier`) et les chemins des images (`CheminsImages`). Ils doivent être hydratés par le scanner applicatif.
- **Optimisation :** L'application tournera sur un NAS. Le code généré, notamment pour le traitement des images/fichiers 3D, doit être économe en mémoire (RAM) et optimisé pour des IOPS potentiellement lents.

---

## 5. Aide-mémoire : Configuration et Migrations (EF Core)

**Étape 1 : Le fichier `appsettings.json`**
Votre fichier `appsettings.json` du projet API doit contenir les paramètres du scanner et la connexion à base de données.
*(Rappel : Les chemins UNC réseau doivent utiliser des doubles antislashs échappés dans le JSON `\\\\192...`).*

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

**Étape 2 : Migrations MariaDB**
Lorsque le modèle de données C# évolue (ajout de propriétés comme `CheminsImages` à `Modele`), la base de données MariaDB doit être mise à jour.
Voici les commandes à exécuter dans le **terminal intégré** (à la racine du projet qui contient le DbContext, ex: `StlExplorerServer`) :

1. **Créer une nouvelle migration** (génère le code de construction, sans toucher à la BDD actuelle) :
   `dotnet ef migrations add <NomDeLaMigration>`
   *(Exemple pour la première fois : `dotnet ef migrations add InitialCreate`)*

2. **Appliquer la migration** (exécute le SQL pour mettre à jour la base MariaDB) :
   `dotnet ef database update`