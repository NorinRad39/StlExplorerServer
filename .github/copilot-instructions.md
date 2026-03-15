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

---

## 5. Aide-mémoire : Migrations Base de Données (EF Core)

Étape 1 : Vérifier la chaîne de connexion
Avant de lancer les commandes, assurez-vous juste que votre fichier appsettings.json (dans le projet StlExplorerServer) contient bien les identifiants pour se connecter à votre MariaDB sur le NAS. Par exemple :

"ConnectionStrings": {
  "DefaultConnection": "Server=VOTRE_IP_NAS;Port=3306;Database=StlExplorerDb;User=VOTRE_USER;Password=VOTRE_MOT_DE_PASSE;"
}

Étape 2 : Lancer les commandes de Migration
Dans Visual Studio, vous avez deux façons de faire. Je vous recommande d'utiliser le terminal intégré.
1.	Allez dans le menu en haut : Affichage > Terminal.
2.	Assurez-vous d'être dans le dossier de votre projet serveur (vous devriez voir le chemin se terminer par StlExplorerServer).
3.	Tapez cette première commande pour générer le code de la migration (le plan de construction) et appuyez sur Entrée 
    
    dotnet ef migrations add InitialCreate

Note : Un dossier Migrations va apparaître dans votre projet avec des fichiers C# générés automatiquement.
4.	Tapez ensuite cette deuxième commande pour envoyer ce plan à MariaDB et créer réellement les tables :
    
    dotnet ef database update

Si tout se passe bien, vous verrez un message de succès (souvent "Done." ou "Fait."). Vous pourrez alors aller vérifier sur votre NAS (via phpMyAdmin ou DBeaver par exemple) : votre base de données StlExplorerDb sera créée avec les tables Familles, Sujets et Modeles !
Vous pourrez ensuite lancer votre API et tester le scan de vos dossiers.

Lorsque le modèle de données C# évolue (classes Famille, Sujet, Modele) ou lors du premier lancement, la base de données MariaDB doit être mise à jour.
Voici les commandes à exécuter dans le **terminal intégré** (à la racine du projet qui contient le DbContext, ex: `StlExplorerServer`) :

1. **Créer une nouvelle migration** (génère le code de construction, sans toucher à la BDD actuelle) :
   `dotnet ef migrations add <NomDeLaMigration>`
   *(Exemple pour la première fois : `dotnet ef migrations add InitialCreate`)*

2. **Appliquer la migration** (exécute le SQL pour mettre à jour la base MariaDB) :
   `dotnet ef database update`