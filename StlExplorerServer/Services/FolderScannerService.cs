using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassLibStlExploServ;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using StlExplorerServer.Repositories;

namespace StlExplorerServer.Services
{
    #region Interface et Déclaration du Service

    /// <summary>
    /// Service responsable du scan des dossiers locaux pour créer l'architecture de données (Famille -> Sujet -> Modele).
    /// </summary>
    /// <remarks>
    /// Ce service parcourt une arborescence de fichiers sur le disque dur et mappe cette arborescence
    /// vers nos objets métiers métier <see cref="Famille"/>, <see cref="Sujet"/> et <see cref="Modele"/>.
    /// </remarks>
    public class FolderScannerService(IMetadonneesRepository metadataRepository, ILogger<FolderScannerService> logger, IConfiguration configuration) : IFolderScannerService
    {
        #region Méthodes Publiques

        /// <summary>
        /// Point d'entrée principal. Scanne un répertoire donné et insère les éléments trouvés en base de données.
        /// </summary>
        /// <param name="path">Le chemin complet du répertoire racine à analyser (ex: "C:\MesFichiersSTL").</param>
        /// <exception cref="DirectoryNotFoundException">Est lancée de manière préventive si le chemin n'existe pas sur le disque.</exception>
        /// <example>
        /// Utilisation typique :
        /// <code>
        /// monService.ScanFolder("D:\3D_Models\Vehicules");
        /// </code>
        /// </example>
        public void ScanFolder(string path)
        {
            if (Directory.Exists(path))
            {
                logger.LogInformation("Début du scan pour le chemin : {Path}", path);

                var modeleDirectories = GetModeleDirectories(new DirectoryInfo(path));

                foreach (var directory in modeleDirectories)
                {
                    // Nous déléguons la gestion des doublons dans la méthode dédiée
                    GetOrCreateModele(directory);
                }
            }
            else
            {
                logger.LogError("Le répertoire spécifié n'existe pas : {Path}", path);
                throw new DirectoryNotFoundException($"Le répertoire spécifié n'existe pas : {path}");
            }
        }

        /// <summary>
        /// Scanne tous les dossiers configurés dans le fichier de configuration de l'application.
        /// </summary>
        public void ScanAllConfiguredFolders()
        {
            // Lire la liste depuis le appsettings.json
            var rootDirs = configuration.GetSection("ScannerSettings:RootDirectories").Get<string[]>();

            if (rootDirs == null || rootDirs.Length == 0)
            {
                logger.LogWarning("Aucun dossier racine configuré dans appsettings.json.");
                return;
            }

            foreach (var dir in rootDirs)
            {
                try
                {
                    logger.LogInformation("Lancement du scan pour le dossier configuré : {Dir}", dir);
                    ScanFolder(dir); // Appelle votre méthode existante
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Erreur lors du scan du dossier {Dir}", dir);
                    // On continue avec le dossier suivant même si un a planté
                }
            }
        }

        #endregion

        #region Méthodes Privées (Logique Interne)

        /// <summary>
        /// Parcourt l'arborescence selon une règle stricte de profondeur (Profondeur Fixe à 3 niveaux).
        /// Famille (Niveau 1) -> Sujet (Niveau 2) -> Modele (Niveau 3).
        /// Les sous-dossiers au-delà du Niveau 3 (ex: 'reparé', 'evidé') appartiennent au Modele parent
        /// et ne sont JAMAIS considérés comme un niveau hiérarchique principal.
        /// </summary>
        /// <param name="rootDirectory">Le dossier racine à partir duquel commencer le scan (Niveau 1 - Famille).</param>
        /// <returns>Une collection de tous les dossiers de niveau 3 (Modele) trouvés.</returns>
        private static IEnumerable<DirectoryInfo> GetModeleDirectories(DirectoryInfo rootDirectory)
        {
            // Niveau 1 : Famille - On parcourt les dossiers de Familles
            foreach (var familleDir in rootDirectory.GetDirectories())
            {
                // Niveau 2 : Sujet - Pour chaque Famille, on parcourt les dossiers de Sujets
                foreach (var sujetDir in familleDir.GetDirectories())
                {
                    // Niveau 3 : Modele - Pour chaque Sujet, on parcourt les dossiers de Modèles
                    foreach (var modeleDir in sujetDir.GetDirectories())
                    {
                        // Les sous-dossiers au-delà (Niveau 4+) comme 'reparé', 'evidé' à l'intérieur de modeleDir
                        // ne seront JAMAIS parcourus par cette boucle, ils sont sains et saufs !
                        yield return modeleDir;
                    }
                }
            }
        }

        /// <summary>
        /// Construit un objet <see cref="Modele"/> à partir des informations du répertoire final.
        /// </summary>
        /// <param name="directory">Le dossier au bout de la branche.</param>
        /// <returns>Un objet Modele instancié et lié à son sujet parent.</returns>
        private Modele CreateModeleForDirectory(DirectoryInfo directory)
        {
            // On instancie notre entité métier
            var modele = new Modele
            {
                Description = directory.Name,
                // On délègue la création/récupération du Sujet parent à la méthode dédiée
                Sujet = GetOrCreateSujet(directory.Parent)
            };

            return modele;
        }

        /// <summary>
        /// Récupère en base de données le Sujet existant, ou en crée un nouveau s'il n'existe pas encore.
        /// </summary>
        /// <param name="parentDirectory">Le répertoire parent du modèle (qui correspond au Sujet).</param>
        /// <returns>Une instance de <see cref="Sujet"/>, existante ou nouvellement créée. Null si le dossier parent est null.</returns>
        private Sujet? GetOrCreateSujet(DirectoryInfo? parentDirectory)
        {
            // Sécurité : si on arrive à la racine du disque dur, le parentDirectory sera null.
            if (parentDirectory == null)
            {
                return null;
            }

            // On interroge la base de données : Ce sujet existe-t-il déjà ?
            var existingSujet = metadataRepository.GetSujetByName(parentDirectory.Name);
            if (existingSujet != null)
            {
                return existingSujet;
            }

            // Si le sujet est introuvable, il faut le créer
            var sujet = new Sujet
            {
                NomSujet = parentDirectory.Name,
                // On fait de même pour la Famille (le répertoire grand-parent)
                Famille = GetOrCreateFamille(parentDirectory.Parent)
            };

            // On l'enregistre dans la base de données après sa création
            metadataRepository.SaveSujet(sujet);
            
            return sujet;
        }

        /// <summary>
        /// Récupère en base de données la Famille existante, ou en crée une nouvelle si elle n'existe pas encore.
        /// </summary>
        /// <param name="grandParentDirectory">Le dossier grand-parent (qui correspond à la Famille).</param>
        /// <returns>Une instance de <see cref="Famille"/>, ou null si l'information n'existe pas.</returns>
        private Famille? GetOrCreateFamille(DirectoryInfo? grandParentDirectory)
        {
            if (grandParentDirectory == null)
            {
                return null;
            }

            // Vérifier en base si cette famille a déjà été traitée lors d'un passage précédent
            var existingFamille = metadataRepository.GetFamilleByName(grandParentDirectory.Name);
            if (existingFamille != null)
            {
                return existingFamille;
            }

            // Instanciation de la nouvelle Famille
            var famille = new Famille
            {
                NomFamille = grandParentDirectory.Name
            };

            metadataRepository.SaveFamille(famille);
            
            return famille;
        }

        /// <summary>
        /// Vérifie si un Modèle existe, et le crée en base de données si ce n'est pas le cas.
        /// </summary>
        /// <param name="directory">Le dossier au bout de la branche.</param>
        /// <returns>Un objet Modele (nouveau ou existant).</returns>
        private Modele GetOrCreateModele(DirectoryInfo directory)
        {
            // Chercher les images dans le dossier (Niveau 3) et ses sous-dossiers
            var imageFiles = directory.GetFiles("*.*", SearchOption.AllDirectories)
                .Where(file => file.Extension.ToLower() is ".jpg" or ".jpeg" or ".png" or ".webp")
                .Select(file => file.FullName)
                .ToList();

            // Évite la création en double si le scan est relancé
            var existingModele = metadataRepository.GetModeleByChemin(directory.FullName);
            if (existingModele != null)
            {
                // Mise à jour des images au cas où de nouvelles ont été ajoutées
                existingModele.CheminsImages = imageFiles;
                metadataRepository.UpdateModele(existingModele);
                return existingModele;
            }

            // On instancie notre entité métier
            var modele = new Modele
            {
                Description = directory.Name,
                CheminDossier = directory.FullName, // Renseignement du chemin physique (Très important !)
                CheminsImages = imageFiles, // Ajout des chemins d'images trouvés
                Sujet = GetOrCreateSujet(directory.Parent)
            };

            // On sauvegarde le modèle (l'appel à SaveModele a été déplacé ici)
            metadataRepository.SaveModele(modele);
            return modele;
        }

        #endregion
    }

    #endregion
}