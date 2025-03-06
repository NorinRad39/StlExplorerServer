// Importe l'espace de noms nécessaire pour accéder aux référentiels de données
using System;
using System.Collections.Generic;
using System.IO;
using ClassLibStlExploServ;
using StlExplorerServer.Repositories;

namespace StlExplorerServer.Services
{
    /// <summary>
    /// Service pour scanner les dossiers et enregistrer les métadonnées.
    /// </summary>
    public class FolderScannerService : IFolderScannerService
    {
        private readonly IMetadataRepository _metadataRepository;

        /// <summary>
        /// Initialise une nouvelle instance de la classe FolderScannerService.
        /// </summary>
        /// <param name="metadataRepository">Le référentiel de métadonnées à utiliser.</param>
        public FolderScannerService(IMetadataRepository metadataRepository)
        {
            _metadataRepository = metadataRepository;
        }

        /// <summary>
        /// Scanne un dossier et enregistre les métadonnées des dossiers les plus bas dans l'arborescence.
        /// </summary>
        /// <param name="path">Le chemin du dossier à scanner.</param>
        /// <exception cref="DirectoryNotFoundException">Lancée lorsque le répertoire spécifié n'existe pas.</exception>
        public void ScanFolder(string path)
        {
            if (Directory.Exists(path))
            {
                // Obtenez les dossiers les plus bas dans l'arborescence
                var paketDirectories = GetPaketDirectories(new DirectoryInfo(path));

                // Parcourez chaque dossier et enregistrez les métadonnées
                foreach (var directory in paketDirectories)
                {
                    var packet = CreatePacketForDirectory(directory);
                    _metadataRepository.SaveMetadata(packet);
                }
            }
            else
            {
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
                Sujet = directory.Parent != null ? GetOrCreateSujet(directory.Parent) : null,
            };

            return packet;
        }


        /// <summary>
        /// Obtient ou crée un sujet pour le dossier parent donné.
        /// </summary>
        /// <param name="parentDirectory">Le dossier parent.</param>
        /// <returns>
        /// Une instance de Sujet, ou null si le dossier parent est null.
        /// </returns>
        private Sujet GetOrCreateSujet(DirectoryInfo parentDirectory)
        {
            if (parentDirectory == null)
            {
                return null;
            }

            var sujet = new Sujet
            {
                NomSujet = parentDirectory.Name,
                Famille = parentDirectory.Parent != null ? GetOrCreateFamille(parentDirectory.Parent) : null,
            };

            return sujet;
        }


        /// <summary>
        /// Obtient ou crée une famille pour le dossier parent du parent donné.
        /// </summary>
        /// <param name="grandParentDirectory">Le dossier parent du parent.</param>
        /// <returns>
        /// Une instance de Sujet, ou null si le dossier parent est null.
        /// </returns>
        private Famille GetOrCreateFamille(DirectoryInfo grandParentDirectory)
        {
            if (grandParentDirectory == null)
            {
                return null;
            }

            var famille = new Famille
            {
                NomFamille = grandParentDirectory.Name,
            };

            return famille;
        }
    }
}
