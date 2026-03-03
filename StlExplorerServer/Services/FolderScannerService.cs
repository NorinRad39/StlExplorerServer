using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // Nécessaire pour l'utilisation de .Any()
using ClassLibStlExploServ;
using Microsoft.Extensions.Logging;
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
    public class FolderScannerService : IFolderScannerService
    {
        #region Propriétés Privées (Dépendances)
        
        /// <summary>
        /// Référentiel (Repository) utilisé pour sauvegarder et récupérer nos données en base.
        /// </summary>
        private readonly IMetadataRepository _metadataRepository;

        /// <summary>
        /// Outil de journalisation (Logger) pour enregistrer des messages (erreurs, infos) lors de l'exécution.
        /// </summary>
        private readonly ILogger<FolderScannerService> _logger;

        #endregion

        #region Constructeur

        /// <summary>
        /// Initialise une nouvelle instance du service <see cref="FolderScannerService"/>.
        /// Utilise le principe d'"Injection de Dépendances" : c'est l'application qui fournit automatiquement 
        /// le repository et le logger lors de la création de ce service.
        /// </summary>
        /// <param name="metadataRepository">L'accès aux données (base de données).</param>
        /// <param name="logger">L'outil d'enregistrement des journaux (logs).</param>
        public FolderScannerService(IMetadataRepository metadataRepository, ILogger<FolderScannerService> logger)
        {
            _metadataRepository = metadataRepository;
            _logger = logger;
        }

        #endregion

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
            // Étape 1 : S'assurer que le chemin fourni est valide et existe bien sur le disque dur.
            if (Directory.Exists(path))
            {
                _logger.LogInformation("Début du scan pour le chemin : {Path}", path);

                // Étape 2 : Récupérer tous les dossiers "terminaux" (qui ne contiennent aucun sous-dossier).
                var modeleDirectories = GetModeleDirectories(new DirectoryInfo(path));

                // Étape 3 : Pour chaque dossier terminal, on crée l'architecture (Modele, Sujet, Famille).
                foreach (var directory in modeleDirectories)
                {
                    var modele = CreateModeleForDirectory(directory);
                    
                    // On sauvegarde le modèle en base de données de manière persistante.
                    _metadataRepository.SaveModele(modele);
                }
            }
            else
            {
                // Si le répertoire est introuvable, on alerte le programme appelant en "jetant" (throw) une erreur.
                _logger.LogError("Le répertoire spécifié n'existe pas : {Path}", path);
                throw new DirectoryNotFoundException($"Le répertoire spécifié n'existe pas : {path}");
            }
        }

        #endregion

        #region Méthodes Privées (Logique Interne)

        /// <summary>
        /// Parcourt l'arborescence de manière récursive (une fonction qui s'appelle elle-même)
        /// pour isoler les dossiers les plus bas ("feuilles" de l'arbre).
        /// </summary>
        /// <param name="directory">Le répertoire courant en train d'être analysé.</param>
        /// <returns>Une énumération des dossiers terminaux (qui ne contiennent plus d'autres dossiers).</returns>
        private IEnumerable<DirectoryInfo> GetModeleDirectories(DirectoryInfo directory)
        {
            // On récupère tous les sous-dossiers directs contenus dans ce dossier.
            var subDirectories = directory.GetDirectories();
            
            // Si le dossier ne contient AUCUN sous-dossier, on considère que c'est un dossier "Modele".
            if (!subDirectories.Any())
            {
                // yield return permet de renvoyer l'élément un par un sans devoir créer une liste entière au préalable
                yield return directory;
            }
            else
            {
                // Si le dossier contient des sous-dossiers, on descend d'un niveau...
                foreach (var subDirectory in subDirectories)
                {
                    // ... et on rappelle cette MÊME fonction pour le sous-dossier (principe de la récursivité).
                    foreach (var leaf in GetModeleDirectories(subDirectory))
                    {
                        yield return leaf;
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
            var existingSujet = _metadataRepository.GetSujetByName(parentDirectory.Name);
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
            _metadataRepository.SaveSujet(sujet);
            
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
            var existingFamille = _metadataRepository.GetFamilleByName(grandParentDirectory.Name);
            if (existingFamille != null)
            {
                return existingFamille;
            }

            // Instanciation de la nouvelle Famille
            var famille = new Famille
            {
                NomFamille = grandParentDirectory.Name
            };

            _metadataRepository.SaveFamille(famille);
            
            return famille;
        }

        #endregion
    }

    #endregion
}