using ClassLibStlExploServ;
using Microsoft.Extensions.Logging;
using StlExplorerServer.Data;
using System.Linq;

namespace StlExplorerServer.Repositories
{
    /// <summary>
    /// Référentiel (Repository) pour gérer les opérations de base de données liées aux métadonnées des objets (Modele, Sujet, Famille).
    /// </summary>
    /// <remarks>
    /// Le pattern "Repository" permet d'isoler la logique d'accès aux données (Entity Framework Core dans ce cas) 
    /// du reste de l'application, rendant le code plus propre et plus facile à tester.
    /// </remarks>
    /// <example>
    /// Exemple d'injection et d'utilisation dans un service :
    /// <code>
    /// public class MonService
    /// {
    ///     private readonly IMetadataRepository _repository;
    ///     
    ///     public MonService(IMetadataRepository repository)
    ///     {
    ///         _repository = repository;
    ///     }
    ///     
    ///     public void Traiter()
    ///     {
    ///         var sujet = _repository.GetSujetByName("Voiture");
    ///     }
    /// }
    /// </code>
    /// </example>
    public class MetadataRepository : IMetadataRepository
    {
        #region Champs Privés (Dépendances)

        /// <summary>
        /// Le contexte de la base de données généré par Entity Framework Core.
        /// Il sert à interagir avec les tables de la base.
        /// </summary>
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// Le service de journalisation (Logger) pour enregistrer les informations ou les erreurs.
        /// </summary>
        private readonly ILogger<MetadataRepository> _logger;

        #endregion

        #region Constructeur

        /// <summary>
        /// Initialise une nouvelle instance de la classe <see cref="MetadataRepository"/>.
        /// </summary>
        /// <param name="context">Le contexte de la base de données injecté automatiquement.</param>
        /// <param name="logger">Le logger injecté automatiquement.</param>
        public MetadataRepository(ApplicationDbContext context, ILogger<MetadataRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        #endregion

        #region Méthodes pour Modele

        /// <summary>
        /// Enregistre un nouveau <see cref="Modele"/> (et ses relations, si elles existent) dans la base de données.
        /// </summary>
        /// <param name="modele">Le modèle de métadonnées à enregistrer.</param>
        /// <remarks>
        /// La méthode <c>_context.SaveChanges()</c> valide la transaction et insère réellement les données dans la base.
        /// </remarks>
        /// <example>
        /// <code>
        /// var nouveauModele = new Modele { Description = "Fichier.stl", SujetID = 1 };
        /// _repository.SaveModele(nouveauModele);
        /// </code>
        /// </example>
        public void SaveModele(Modele modele)
        {
            _context.Modeles.Add(modele);
            _context.SaveChanges();
        }

        #endregion

        #region Méthodes pour Sujet

        /// <summary>
        /// Recherche et récupère un <see cref="Sujet"/> par son nom exact.
        /// </summary>
        /// <param name="name">Le nom du sujet à rechercher (ex: "Voiture").</param>
        /// <returns>Le premier sujet correspondant à la recherche, ou <c>null</c> s'il n'existe pas en base.</returns>
        /// <remarks>
        /// Utilise <c>FirstOrDefault</c> (LINQ) qui retourne le premier élément correspondant, ou la valeur par défaut du type (ici <c>null</c>) si aucun élément n'est trouvé.
        /// </remarks>
        public Sujet? GetSujetByName(string name)
        {
            return _context.Sujets.FirstOrDefault(s => s.NomSujet == name);
        }

        /// <summary>
        /// Enregistre un nouveau <see cref="Sujet"/> dans la base de données.
        /// </summary>
        /// <param name="sujet">L'objet sujet à insérer.</param>
        /// <example>
        /// <code>
        /// var nouveauSujet = new Sujet { NomSujet = "Voiture", FamilleID = 2 };
        /// _repository.SaveSujet(nouveauSujet);
        /// </code>
        /// </example>
        public void SaveSujet(Sujet sujet)
        {
            _context.Sujets.Add(sujet);
            _context.SaveChanges();
        }

        #endregion

        #region Méthodes pour Famille

        /// <summary>
        /// Recherche et récupère une <see cref="Famille"/> par son nom exact.
        /// </summary>
        /// <param name="name">Le nom de la famille à rechercher (ex: "Véhicules").</param>
        /// <returns>La première famille correspondante, ou <c>null</c> si elle n'existe pas en base.</returns>
        public Famille? GetFamilleByName(string name)
        {
            return _context.Familles.FirstOrDefault(f => f.NomFamille == name);
        }

        /// <summary>
        /// Enregistre une nouvelle <see cref="Famille"/> dans la base de données.
        /// </summary>
        /// <param name="famille">La famille à insérer.</param>
        /// <example>
        /// <code>
        /// var nouvelleFamille = new Famille { NomFamille = "Véhicules" };
        /// _repository.SaveFamille(nouvelleFamille);
        /// </code>
        /// </example>
        public void SaveFamille(Famille famille)
        {
            _context.Familles.Add(famille);
            _context.SaveChanges();
        }

        #endregion
    }
}