using ClassLibStlExploServ;

namespace StlExplorerServer.Repositories
{
    /// <summary>
    /// Interface pour le référentiel de métadonnées.
    /// </summary>
    public interface IMetadonneesRepository
    {
        /// <summary>
        /// Récupère un modèle par son chemin de dossier physique.
        /// </summary>
        /// <param name="chemin">Le chemin complet du dossier.</param>
        /// <returns>Le modèle correspondant, ou null s'il n'existe pas.</returns>
        Modele? GetModeleByChemin(string chemin);

        /// <summary>
        /// Récupère tous les modèles enregistrés dans la base de données.
        /// </summary>
        /// <returns>La liste de tous les modèles.</returns>
        IEnumerable<Modele> GetAllModeles();

        /// <summary>
        /// Enregistre les métadonnées d'un modèle.
        /// </summary>
        /// <param name="modele">Le modèle de métadonnées à enregistrer.</param>
        void SaveModele(Modele modele);

        /// <summary>
        /// Met à jour un modèle existant.
        /// </summary>
        void UpdateModele(Modele modele);

        /// <summary>
        /// Récupère un sujet par son nom.
        /// </summary>
        /// <param name="name">Le nom du sujet à rechercher.</param>
        /// <returns>Le sujet correspondant, ou null s'il n'existe pas.</returns>
        Sujet? GetSujetByName(string name);

        /// <summary>
        /// Enregistre un nouveau sujet.
        /// </summary>
        /// <param name="sujet">Le sujet à enregistrer.</param>
        void SaveSujet(Sujet sujet);

        /// <summary>
        /// Récupère une famille par son nom.
        /// </summary>
        /// <param name="name">Le nom de la famille à rechercher.</param>
        /// <returns>La famille correspondante, ou null si elle n'existe pas.</returns>
        Famille? GetFamilleByName(string name);

        /// <summary>
        /// Enregistre une nouvelle famille.
        /// </summary>
        /// <param name="famille">La famille à enregistrer.</param>
        void SaveFamille(Famille famille);
    }
}





