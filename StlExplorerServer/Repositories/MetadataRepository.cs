using ClassLibStlExploServ;
using StlExplorerServer.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace StlExplorerServer.Repositories
{
    // ... Les commentaires ont été réduits pour plus de lisibilité ...
    public class MetadataRepository : IMetadonneesRepository
    {
        private readonly ApplicationDbContext context;

        public MetadataRepository(ApplicationDbContext context)
        {
            this.context = context;
        }

        #region Méthodes pour Modele

        /// <summary>
        /// Supprime un <see cref="Modele"/> de la base de données.
        /// </summary>
        public void DeleteModele(int modeleId)
        {
            var modele = GetModeleById(modeleId);
            if (modele != null)
            {
                context.Modeles.Remove(modele);
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Récupère tous les modèles associés à un Sujet donné.
        /// </summary>
        public IEnumerable<Modele> GetModelesBySujetId(int sujetId)
        {
            return context.Modeles.Where(m => m.SujetID == sujetId).ToList();
        }

        /// <summary>
        /// Récupère tous les chemins de dossiers des modèles enregistrés.
        /// </summary>
        public IEnumerable<string> GetAllModelesChemins()
        {
            return context.Modeles.Select(m => m.CheminDossier).ToList();
        }

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
                    CheminDossier = m.CheminDossier,
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

        /// <summary>
        /// Supprime un <see cref="Sujet"/> de la base de données.
        /// </summary>
        public void DeleteSujet(Sujet sujet)
        {
            context.Sujets.Remove(sujet);
            context.SaveChanges();
        }

        /// <summary>
        /// Récupère tous les sujets associés à une Famille donnée.
        /// </summary>
        public IEnumerable<Sujet> GetSujetsByFamilleId(int familleId)
        {
            return context.Sujets.Where(s => s.FamilleID == familleId).ToList();
        }

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

        /// <summary>
        /// Supprime une <see cref="Famille"/> de la base de données.
        /// </summary>
        public void DeleteFamille(Famille famille)
        {
            context.Familles.Remove(famille);
            context.SaveChanges();
        }

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