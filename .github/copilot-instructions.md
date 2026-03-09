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

- **Indexation (Scanner de dossiers) :**
  - Utilise la stricte "Profondeur Fixe" détaillée ci-dessus (Niveau 1 = Famille, Niveau 2 = Sujet, Niveau 3 = Modèle).
- **Prévisualisation (Défis techniques) :**
  - Aperçus 3D pour les STL.
  - Lecture/prévisualisation optimisée des fichiers ZIP sans extraction complète (pour économiser les ressources NAS).
- **Interface Client (À développer) :**
  - **Configuration :** Page/onglet dédié pour définir les chemins racines identifiés comme "Dossiers de Familles".
  - **Formulaire d'ajout :** Lors du dépôt de nouveaux fichiers, proposer un champ de texte avec autocomplétion pour la sélection des `Familles`. L'autocomplétion doit s'appuyer sur les données existantes du serveur ; si la sélection n'existe pas, la nouvelle famille est créée dynamiquement.

---

## 4. Directives Strictes pour GitHub Copilot (IA)

- **Terminologie :** Le terme "Packet" est totalement obsolète dans ce projet. Le domaine métier exige l'utilisation stricte du terme `Modele` (ou `Modèle`).
- **Structure de la donnée :** Respecter impérativement l'architecture `Famille > Sujet > Modele` dans toutes les requêtes LINQ, créations d'entités, ou algorithmes de parcours de fichiers.
- **Optimisation :** L'application tournera sur un NAS. Le code généré, notamment pour le traitement des images/fichiers 3D, doit être économe en mémoire (RAM) et optimisé pour des IOPS potentiellement lents.