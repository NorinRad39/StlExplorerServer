# Contexte du Projet StlExplorerServer

Ce fichier sert de référence pour le développement du projet `StlExplorerServer`, notamment pour les assistants IA (GitHub Copilot).

## Vue d'ensemble
Ce projet est une application client-serveur destinée à indexer, organiser et prévisualiser des fichiers d'impression 3D (STL) hébergés sur un NAS (Synology DS923+).

## Architecture
- **Serveur (Backend) :** API Web .NET 8 (ASP.NET Core).
- **Hébergement :** Le serveur tournera dans un conteneur Docker sur le NAS.
- **Base de Données :** MariaDB (hébergée sur le NAS).
- **Clients :**
  - Application Windows (WPF ou MAUI, à définir).
  - Application Android (MAUI ou Native, à définir).

## Structure des Données (Hiérarchie)
Les fichiers sont organisés physiquement et logiquement selon la hiérarchie suivante :

1.  **Famille** (ex: `Marvel`)
    *   Niveau le plus haut de classification.
2.  **Sujet** (ex: `Galactus`)
    *   Sous-catégorie appartenant à une Famille.
3.  **Modèle** (ex: `Galactus - Statue wicked`)
    *   Anciennement appelé "Packet".
    *   Correspond au dossier final contenant les fichiers.
    *   Contient :
        *   Fichiers STL (.stl).
        *   Archives (.zip) pouvant contenir des STL.
        *   Photos/Images de référence.

**Exemple de chemin :** `Marvel\Galactus\Galactus - Statue wicked`
- Famille : Marvel
- Sujet : Galactus
- Modèle : Galactus - Statue wicked (contient les fichiers STL et images)

## Fonctionnalités Clés (Roadmap)
1.  **Indexation (Scan) :**
    - Parcourir les dossiers du NAS.
    - Créer automatiquement l'arborescence (Famille -> Sujet -> Modèle) dans la base de données.
    - Gérer les modifications (ajouts de nouveaux fichiers).
2.  **Recherche :**
    - Recherche par mots-clés (tags, noms).
3.  **Prévisualisation :**
    - Générer des vignettes pour les images.
    - **Défi technique :** Générer des aperçus 3D pour les fichiers STL.
    - **Défi technique :** Lire et prévisualiser le contenu des fichiers ZIP sans extraction complète coûteuse.
4.  **Gestion :**
    - Création automatique de dossiers lors de l'ajout de nouveaux fichiers via l'application.

## Historique des Modifications Importantes
- **Refactoring (aaaa-mm-jj) :** Renommage de l'entité `Packet` en `Modele` pour mieux refléter le domaine métier.
    - Code mis à jour (Classes, DBContext, Repositories).
    - Migration Entity Framework `RenamePacketToModele` créée.
    - Fixation temporaire de la version MariaDB dans `Program.cs` pour permettre la génération de migrations hors ligne.

## Directives Techniques pour l'IA
- Toujours respecter la hiérarchie `Famille > Sujet > Modele`.
- Le terme "Packet" est obsolète, utiliser "Modele".
- L'application doit être optimisée pour tourner sur un NAS (ressources limitées, notamment RAM pour le traitement d'images/3D).
- La compatibilité Cross-Platform (Windows/Android) est une priorité pour les futurs développements clients.
