using System;
using System.Collections.Generic;
using System.IO;
using ClassLibStlExploServ;
using Microsoft.Extensions.Logging;
using StlExplorerServer.Repositories;


namespace StlExplorerServer.Services
{
    /// <summary>
    /// Service pour scanner les dossiers et enregistrer les métadonnées.
    /// </summary>
    public class FolderScannerService : IFolderScannerService
    {
        private readonly IMetadataRepository _metadataRepository;
        private readonly ILogger<FolderScannerService> _logger;

        /// <summary>
        /// Initialise une nouvelle instance de la classe FolderScannerService.
        /// </summary>
        /// <param name="metadataRepository">Le référentiel de métadonnées à utiliser.</param>
        /// <param name="logger">Le logger à utiliser.</param>
        public FolderScannerService(IMetadataRepository metadataRepository, ILogger<FolderScannerService> logger)
        {
            _metadataRepository = metadataRepository;
            _logger = logger;
        }

        /// <summary>
        /// Scanne un dossier et enregistre les métadonnées des dossiers les plus bas dans l'arborescence.
        /// </summary>
        /// <param name="path">Le chemin du dossier à scanner.</param>
        /// <exception cref="DirectoryNotFoundException">Lancée lorsque le répertoire spécifié n'existe pas.</exception>
        public void ScanFolder(string path)
        {
            // Vérifie si le répertoire existe
            if (Directory.Exists(path))
            {
                // Récupère les dossiers les plus bas dans l'arborescence
                var paketDirectories = GetPaketDirectories(new DirectoryInfo(path));

                // Parcourt chaque dossier et crée un paquet de métadonnées
                foreach (var directory in paketDirectories)
                {
                    var packet = CreatePacketForDirectory(directory);
                    _metadataRepository.SaveMetadata(packet);
                }
            }
            else
            {
                // Lance une exception si le répertoire n'existe pas
                throw new DirectoryNotFoundException($"Le répertoire spécifié n'existe pas : {path}");
            }
        }

        /// <summary>
        /// Récupère les dossiers les plus bas dans l'arborescence.
        /// </summary>
        /// <param name="directory">Le répertoire à parcourir.</param>
        /// <returns>Une collection de dossiers les plus bas.</returns>
        private IEnumerable<DirectoryInfo> GetPaketDirectories(DirectoryInfo directory)
        {
            var subDirectories = directory.GetDirectories();
            if (!subDirectories.Any())
            {
                yield return directory;
            }
            else
            {
                foreach (var subDirectory in subDirectories)
                {
                    foreach (var leaf in GetPaketDirectories(subDirectory))
                    {
                        yield return leaf;
                    }
                }
            }
        }

        /// <summary>
        /// Crée un paquet de métadonnées pour un dossier donné.
        /// </summary>
        /// <param name="directory">Le répertoire pour lequel créer les métadonnées.</param>
        /// <returns>Une instance de Packet contenant les métadonnées.</returns>
        private Packet CreatePacketForDirectory(DirectoryInfo directory)
        {
            var packet = new Packet
            {
                Description = directory.Name,
                Sujet = GetOrCreateSujet(directory.Parent)
            };

            return packet;
        }

        /// <summary>
        /// Obtient ou crée un sujet pour le dossier parent donné.
        /// </summary>
        /// <param name="parentDirectory">Le dossier parent.</param>
        /// <returns>Une instance de Sujet, ou null si le dossier parent est null.</returns>
        private Sujet GetOrCreateSujet(DirectoryInfo parentDirectory)
        {
            if (parentDirectory == null)
            {
                return null;
            }

            // Vérifie si le sujet existe déjà
            var existingSujet = _metadataRepository.GetSujetByName(parentDirectory.Name);
            if (existingSujet != null)
            {
                return existingSujet;
            }

            var sujet = new Sujet
            {
                NomSujet = parentDirectory.Name,
                Famille = GetOrCreateFamille(parentDirectory.Parent)
            };

            _metadataRepository.SaveSujet(sujet);
            return sujet;
        }

        /// <summary>
        /// Obtient ou crée une famille pour le dossier parent du parent donné.
        /// </summary>
        /// <param name="grandParentDirectory">Le dossier parent du parent.</param>
        /// <returns>Une instance de Famille, ou null si le dossier parent est null.</returns>
        private Famille GetOrCreateFamille(DirectoryInfo grandParentDirectory)
        {
            if (grandParentDirectory == null)
            {
                return null;
            }

            // Vérifie si la famille existe déjà
            var existingFamille = _metadataRepository.GetFamilleByName(grandParentDirectory.Name);
            if (existingFamille != null)
            {
                return existingFamille;
            }

            var famille = new Famille
            {
                NomFamille = grandParentDirectory.Name
            };

            _metadataRepository.SaveFamille(famille);
            return famille;
        }
    }
}