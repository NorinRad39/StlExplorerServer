using ClassLibStlExploServ;
using Microsoft.Extensions.Logging;
using StlExplorerServer.Data;
using System.Linq;


namespace StlExplorerServer.Repositories
{
    /// <summary>
    /// Référentiel pour gérer les opérations de base de données liées aux métadonnées.
    /// </summary>
    public class MetadataRepository : IMetadataRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MetadataRepository> _logger;

        /// <summary>
        /// Initialise une nouvelle instance de la classe MetadataRepository.
        /// </summary>
        /// <param name="context">Le contexte de la base de données.</param>
        /// <param name="logger">Le logger à utiliser.</param>
        public MetadataRepository(ApplicationDbContext context, ILogger<MetadataRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Enregistre les métadonnées d'un paquet dans la base de données.
        /// </summary>
        /// <param name="packet">Le paquet de métadonnées à enregistrer.</param>
        public void SaveMetadata(Packet packet)
        {
            _context.Packets.Add(packet);
            _context.SaveChanges();
        }

        /// <summary>
        /// Récupère un sujet par son nom.
        /// </summary>
        /// <param name="name">Le nom du sujet à rechercher.</param>
        /// <returns>Le sujet correspondant, ou null s'il n'existe pas.</returns>
        public Sujet GetSujetByName(string name)
        {
            return _context.Sujets.FirstOrDefault(s => s.NomSujet == name);
        }

        /// <summary>
        /// Enregistre un nouveau sujet dans la base de données.
        /// </summary>
        /// <param name="sujet">Le sujet à enregistrer.</param>
        public void SaveSujet(Sujet sujet)
        {
            _context.Sujets.Add(sujet);
            _context.SaveChanges();
        }

        /// <summary>
        /// Récupère une famille par son nom.
        /// </summary>
        /// <param name="name">Le nom de la famille à rechercher.</param>
        /// <returns>La famille correspondante, ou null si elle n'existe pas.</returns>
        public Famille GetFamilleByName(string name)
        {
            return _context.Familles.FirstOrDefault(f => f.NomFamille == name);
        }

        /// <summary>
        /// Enregistre une nouvelle famille dans la base de données.
        /// </summary>
        /// <param name="famille">La famille à enregistrer.</param>
        public void SaveFamille(Famille famille)
        {
            _context.Familles.Add(famille);
            _context.SaveChanges();
        }
    }
}