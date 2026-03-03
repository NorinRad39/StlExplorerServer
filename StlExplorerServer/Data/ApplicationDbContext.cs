using ClassLibStlExploServ;
using Microsoft.EntityFrameworkCore;

namespace StlExplorerServer.Data
{
    /// <summary>
    /// Contexte de base de données principal pour l'application. 
    /// Il fait le lien entre les classes de notre code (Entités) et les tables de la base de données.
    /// </summary>
    /// <remarks>
    /// En utilisant Entity Framework Core, cette classe gère la connexion, les transactions, et traduit 
    /// les requêtes LINQ en requêtes SQL compatibles avec la base de données configurée (ex: MySQL).
    /// </remarks>
    /// <example>
    /// Configuration typique dans <c>Program.cs</c> :
    /// <code>
    /// builder.Services.AddDbContext&lt;ApplicationDbContext&gt;(options =>
    ///     options.UseMySql(connectionString, serverVersion));
    /// </code>
    /// </example>
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        #region Entités (DbSets)

        /// <summary>
        /// Représente la table "Modeles" dans la base de données.
        /// Un <see cref="DbSet{TEntity}"/> permet d'interroger (SELECT), d'ajouter (INSERT), 
        /// de mettre à jour (UPDATE) et de supprimer (DELETE) des instances de <see cref="Modele"/>.
        /// </summary>
        public DbSet<Modele> Modeles { get; set; }

        /// <summary>
        /// Représente la table "Sujets" dans la base de données.
        /// </summary>
        public DbSet<Sujet> Sujets { get; set; }

        /// <summary>
        /// Représente la table "Familles" dans la base de données.
        /// </summary>
        public DbSet<Famille> Familles { get; set; }

        #endregion

        // Vous pouvez ajouter d'autres configurations fluides (Fluent API) ici en redéfinissant la méthode OnModelCreating si nécessaire
    }
}
