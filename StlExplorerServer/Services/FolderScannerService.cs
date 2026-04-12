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

                var modeleDirectories = GetModeleDirectories(new DirectoryInfo(path)).ToList();
                AjouterLogScan($"🔍 Scan de {path} — {modeleDirectories.Count} dossier(s) modèle détecté(s)");
                int total = modeleDirectories.Count;
                int current = 0;
                foreach (var directory in modeleDirectories)
                {
                    // Nous déléguons la gestion des doublons dans la méthode dédiée
                    GetOrCreateModele(directory);
                    current++;
                    _progressionScan = total > 0 ? (int)(current * 100.0 / total) : 100;
                }
                _progressionScan = 100;
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
            var rootDirs = configuration.GetSection("ScannerSettings:RootDirectories").Get<string[]>();
            if (rootDirs == null || rootDirs.Length == 0)
            {
                logger.LogWarning("Aucun dossier racine configuré dans appsettings.json.");
                return;
            }

            var allModeles = rootDirs.SelectMany(dir =>
                Directory.Exists(dir) ? GetModeleDirectories(new DirectoryInfo(dir)) : Enumerable.Empty<DirectoryInfo>()
            ).ToList();

            int total = allModeles.Count;
            int current = 0;

            lock (_progressionLock) { _progressionScan = 0; }

            foreach (var directory in allModeles)
            {
                GetOrCreateModele(directory);
                current++;
                lock (_progressionLock)
                {
                    _progressionScan = total > 0 ? (int)(current * 100.0 / total) : 100;
                }
            }
            lock (_progressionLock) { _progressionScan = 100; }
        }

        /// <summary>
        /// Actualise la base de données à partir de l'état du disque :
        /// - Supprime les modèles absents du disque
        /// - Ajoute ou met à jour les modèles présents sur le disque
        /// </summary>
        /// <param name="path">Chemin racine à synchroniser</param>
        public void ActualiserBaseDepuisDossier(string path)
        {
            if (!Directory.Exists(path))
            {
                logger.LogError("Le répertoire spécifié n'existe pas : {Path}", path);
                throw new DirectoryNotFoundException($"Le répertoire spécifié n'existe pas : {path}");
            }

            logger.LogInformation("Synchronisation de la base avec le disque pour le chemin : {Path}", path);
            SynchroniserModelesAvecDisque(path);
            ScanFolder(path); // Ajoute ou met à jour les modèles présents

            // Invalider le cache pour ce chemin
            InvaliderCache(path);
        }

        /// <summary>
        /// Synchronisation intelligente : scan complet sur les nouveaux dossiers racines, mise à jour sur les autres.
        /// </summary>
        public void SynchronisationIntelligente()
        {
            var rootDirs = configuration.GetSection("ScannerSettings:RootDirectories").Get<string[]>();
            if (rootDirs == null || rootDirs.Length == 0)
            {
                logger.LogWarning("Aucun dossier racine configuré dans appsettings.json.");
                AjouterLogScan("⚠ Aucun dossier racine configuré.");
                return;
            }

            // Récupérer tous les chemins de modèles connus en base
            var cheminsModelesEnBase = new HashSet<string>(metadataRepository.GetAllModelesChemins(), StringComparer.OrdinalIgnoreCase);
            ReinitialiserLogScan();
           lock (_progressionLock) { _scanStartTime = DateTime.UtcNow; }
            AjouterLogScan($"🚀 Synchronisation intelligente — {rootDirs.Length} dossier(s) racine(s)");

            foreach (var dir in rootDirs)
            {
                if (!Directory.Exists(dir))
                {
                    logger.LogWarning("Dossier racine inexistant : {Dir}", dir);
                    AjouterLogScan($"⚠ Dossier inexistant : {dir}");
                    continue;
                }

                // Si aucun modèle de ce dossier n'est connu, scan complet
                bool isNouveau = !cheminsModelesEnBase.Any(c => c.StartsWith(dir, StringComparison.OrdinalIgnoreCase));
                if (isNouveau)
                {
                    logger.LogInformation("Nouveau dossier racine détecté, scan complet : {Dir}", dir);
                    lock (_progressionLock) { _phaseOperation = "Création de la base"; }
                    AjouterLogScan($"📂 Nouveau dossier racine → scan complet : {dir}");
                    ScanFolder(dir);
                }
                else
                {
                    logger.LogInformation("Mise à jour du dossier racine existant : {Dir}", dir);
                    lock (_progressionLock) { _phaseOperation = "Mise à jour de la base"; }
                    AjouterLogScan($"📂 Mise à jour du dossier existant : {dir}");
                    ActualiserBaseDepuisDossier(dir);
                }
                lock (_progressionLock) { _phaseOperation = string.Empty; }
            }
            AjouterLogScan("✅ Synchronisation terminée.");
        }

        /// <summary>
        /// Invalide le cache mémoire pour tous les dossiers ou un dossier spécifique.
        /// </summary>
        public void InvaliderCache(string? path = null)
        {
            lock (_cacheLock)
            {
                if (path == null)
                {
                    _cacheModeles.Clear();
                }
                else
                {
                    _cacheModeles.Remove(path);
                }
            }
        }

        /// <summary>
        /// Supprime de la base les modèles qui n'existent plus sur le disque.
        /// Ne supprime que les modèles dont le CheminDossier appartient au chemin racine donné,
        /// pour ne pas supprimer les modèles d'autres dossiers racines.
        /// </summary>
        /// <param name="path">Chemin racine à synchroniser</param>
        private void SynchroniserModelesAvecDisque(string path)
        {
            // Filtrer uniquement les modèles appartenant à ce dossier racine
            var modelesEnBase = metadataRepository.GetAllModeles()
                .Where(m => m.CheminDossier != null && m.CheminDossier.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var cheminsModelesDisque = new HashSet<string>(
                GetModeleDirectories(new DirectoryInfo(path)).Select(d => d.FullName),
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var modele in modelesEnBase)
            {
                if (!cheminsModelesDisque.Contains(modele.CheminDossier))
                {
                    logger.LogInformation("Suppression du modèle absent du disque : {Chemin}", modele.CheminDossier);
                    AjouterLogScan($"🗑 Supprimé (absent du disque) : {modele.Description}");
                    metadataRepository.DeleteModele(modele.ModeleID);
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
                AjouterLogScan($"🔄 {directory.Parent?.Parent?.Name} > {directory.Parent?.Name} > {directory.Name} ({imageFiles.Count} img)");
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
            AjouterLogScan($"✨ NOUVEAU : {directory.Parent?.Parent?.Name} > {directory.Parent?.Name} > {directory.Name} ({imageFiles.Count} img)");
            return modele;
        }

        #endregion

        // Cache mémoire simple pour les modèles (clé: chemin racine, valeur: liste de modèles)
        private static readonly Dictionary<string, List<Modele>> _cacheModeles = new();
        private static readonly object _cacheLock = new();
        private static readonly object _progressionLock = new();
        private static int _progressionScan = 0;
        private static string _phaseOperation = string.Empty;
       private static DateTime _scanStartTime = DateTime.MinValue;

       // Journal de scan (accessible via l'endpoint /api/Metadata/scan-log)
       private static readonly List<string> _scanLog = new();
       private static readonly object _scanLogLock = new();
       private const int MaxScanLogEntries = 500;

        /// <summary>
        /// Pourcentage d'avancement du scan, accessible en lecture statique
        /// par le <see cref="BackgroundScannerHostedService"/>.
        /// </summary>
        internal static int ProgressionScanStatique
        {
            get { lock (_progressionLock) { return _progressionScan; } }
        }

        /// <summary>
        /// Phase courante du scan ("Création de la base" ou "Mise à jour de la base").
        /// </summary>
        internal static string PhaseStatique
        {
            get { lock (_progressionLock) { return _phaseOperation; } }
        }

       /// <summary>
       /// Nombre de secondes écoulées depuis le début du scan en cours.
       /// Retourne 0 si aucun scan n'est en cours.
       /// </summary>
       internal static double ElapsedSecondsStatique
       {
           get
           {
               lock (_progressionLock)
               {
                   if (_scanStartTime == DateTime.MinValue) return 0;
                   return (DateTime.UtcNow - _scanStartTime).TotalSeconds;
               }
           }
       }

       /// <summary>
       /// Retourne les entrées du journal de scan à partir d'un index donné.
       /// Permet au client de ne récupérer que les nouvelles entrées.
       /// </summary>
       internal static List<string> GetScanLogDepuis(int fromIndex)
       {
           lock (_scanLogLock)
           {
               if (fromIndex >= _scanLog.Count) return new List<string>();
               return _scanLog.Skip(fromIndex).ToList();
           }
       }

       /// <summary>Nombre total d'entrées dans le journal de scan.</summary>
       internal static int ScanLogCount
       {
           get { lock (_scanLogLock) { return _scanLog.Count; } }
       }

       /// <summary>Ajoute une entrée horodatée dans le journal de scan.</summary>
       private static void AjouterLogScan(string message)
       {
           lock (_scanLogLock)
           {
               _scanLog.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
               if (_scanLog.Count > MaxScanLogEntries)
                   _scanLog.RemoveRange(0, _scanLog.Count - MaxScanLogEntries);
           }
       }

       /// <summary>Vide le journal de scan (appelé au début de chaque synchronisation).</summary>
       internal static void ReinitialiserLogScan()
       {
           lock (_scanLogLock) { _scanLog.Clear(); }
       }
    }

    #endregion
}