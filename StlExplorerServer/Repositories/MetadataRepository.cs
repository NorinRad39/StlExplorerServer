using ClassLibStlExploServ;
using StlExplorerServer.Data;
using System.Linq;

namespace StlExplorerServer.Repositories
{
    // ... Les commentaires ont été réduits pour plus de lisibilité ...
    public class MetadataRepository(ApplicationDbContext context) : IMetadonneesRepository
    {
        #region Méthodes pour Modele

        /// <summary>
        /// Recherche et récupère un <see cref="Modele"/> par son chemin physique exact.
        /// </summary>
        /// <param name="chemin">Le chemin du dossier à rechercher.</param>
        /// <returns>Le premier modèle correspondant, ou null s'il n'existe pas en base.</returns>
        public Modele? GetModeleByChemin(string chemin)
        {
            return context.Modeles.FirstOrDefault(m => m.CheminDossier == chemin);
        }

        /// <summary>
        /// Récupère tous les modèles enregistrés dans la base de données.
        /// </summary>
        public System.Collections.Generic.IEnumerable<Modele> GetAllModeles()
        {
            return context.Modeles.ToList();
        }

        /// <summary>
        /// Enregistre un nouveau <see cref="Modele"/> (et ses relations, si elles existent) dans la base de données.
        /// </summary>
        public void SaveModele(Modele modele)
        {
            context.Modeles.Add(modele);
            context.SaveChanges();
        }

        public void UpdateModele(Modele modele)
        {
            context.Modeles.Update(modele);
            context.SaveChanges();
        }

        #endregion

        #region Méthodes pour Sujet

        public Sujet? GetSujetByName(string name)
        {
            return context.Sujets.FirstOrDefault(s => s.NomSujet == name);
        }

        public void SaveSujet(Sujet sujet)
        {
            context.Sujets.Add(sujet);
            context.SaveChanges();
        }

        #endregion

        #region Méthodes pour Famille

        public Famille? GetFamilleByName(string name)
        {
            return context.Familles.FirstOrDefault(f => f.NomFamille == name);
        }

        public void SaveFamille(Famille famille)
        {
            context.Familles.Add(famille);
            context.SaveChanges();
        }

        #endregion
    }
}