// Importe l'espace de noms nécessaire pour accéder aux référentiels de données
using StlExplorerServer.Repositories;

namespace StlExplorerServer.Services
{
    // Implémente l'interface IFolderScannerService pour définir la logique métier
    public class FolderScannerService : IFolderScannerService
    {
        // Déclare une variable privée pour stocker une instance de IMetadataRepository
        private readonly IMetadataRepository _metadataRepository;

        // Constructeur qui accepte une instance de IMetadataRepository
        // L'injection de dépendances fournira automatiquement cette instance lors de la création du service
        public FolderScannerService(IMetadataRepository metadataRepository)
        {
            _metadataRepository = metadataRepository;
        }

        // Méthode pour scanner un dossier et enregistrer les métadonnées
        public void ScanFolder(string path)
        {
            // Logique pour scanner le dossier à l'emplacement spécifié
            // Utilisez _metadataRepository pour interagir avec la base de données et enregistrer les métadonnées

            // Exemple de logique (à compléter selon vos besoins) :
            // 1. Parcourez les fichiers dans le dossier spécifié.
            // 2. Extrayez les métadonnées nécessaires de chaque fichier.
            // 3. Utilisez _metadataRepository.SaveMetadata(metadata) pour enregistrer les métadonnées dans la base de données.
        }
    }
}
