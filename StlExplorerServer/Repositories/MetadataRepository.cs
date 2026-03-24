using ClassLibStlExploServ;
using StlExplorerServer.Data;
using Microsoft.EntityFrameworkCore;
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
            return context.Modeles
                .AsNoTracking()
                .Include(m => m.Sujet)
                .ThenInclude(s => s != null ? s.Famille : null)
                .ToList();
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

        public Modele? GetModeleById(int id)
        {
            return context.Modeles
                .Include(m => m.Sujet)
                .ThenInclude(s => s != null ? s.Famille : null)
                .FirstOrDefault(m => m.ModeleID == id);
        }

        /// <summary>
        /// Projection SQL légère : ne charge que les noms et IDs, sans les CheminsImages.
        /// </summary>
        public async Task<List<ModeleResume>> GetAllModelesResumeAsync()
        {
            return await context.Modeles
                .AsNoTracking()
                .Select(m => new ModeleResume
                {
                    ModeleID = m.ModeleID,
                    Description = m.Description,
                    SujetID = m.SujetID,
                    NomSujet = m.Sujet != null ? m.Sujet.NomSujet : null,
                    FamilleID = m.Sujet != null ? m.Sujet.FamilleID : 0,
                    NomFamille = m.Sujet != null && m.Sujet.Famille != null ? m.Sujet.Famille.NomFamille : null
                })
                .ToListAsync();
        }

        /// <summary>
        /// Charge uniquement la colonne CheminsImages pour un modèle donné.
        /// </summary>
        public async Task<List<string>> GetImagesForModeleAsync(int modeleId)
        {
            var images = await context.Modeles
                .AsNoTracking()
                .Where(m => m.ModeleID == modeleId)
                .Select(m => m.CheminsImages)
                .FirstOrDefaultAsync();
            return images ?? [];
        }

        #endregion

        #region Méthodes pour Sujet

        public Sujet? GetSujetByNameAndFamilleId(string name, int familleId)
        {
            return context.Sujets.FirstOrDefault(s => s.NomSujet == name && s.FamilleID == familleId);
        }

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