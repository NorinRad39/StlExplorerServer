// Importe les espaces de noms nécessaires
using Org.BouncyCastle.Asn1.Cms; // Utilisé pour les opérations liées à CMS (Cryptographic Message Syntax)
using ClassLibStlExploServ; // Importe les classes et interfaces de la bibliothèque ClassLibStlExploServ
using StlExplorerServer.Data; // Importe le contexte de base de données

namespace StlExplorerServer.Repositories
{
    // Implémente l'interface IMetadataRepository pour définir la logique d'accès aux données
    public class MetadataRepository : IMetadataRepository
    {
        // Déclare une variable privée pour stocker le contexte de base de données
        private readonly ApplicationDbContext _context;

        // Constructeur qui accepte une instance de ApplicationDbContext
        // L'injection de dépendances fournira automatiquement cette instance lors de la création du référentiel
        public MetadataRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        // Méthode pour enregistrer un paquet de métadonnées dans la base de données
        public void SaveMetadata(Packet packet)
        {
            // Ajoute le paquet de métadonnées au contexte de la base de données
            _context.Packets.Add(packet);
            // Enregistre les modifications dans la base de données
            _context.SaveChanges();
        }
    }
}
