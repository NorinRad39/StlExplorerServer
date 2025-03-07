using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1.Cms;
using System.Collections.Generic;
using ClassLibStlExploServ;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;




namespace StlExplorerServer.Data
{
    /// <summary>
    /// Contexte de base de données pour l'application, gérant les entités Packet, Sujet et Famille.
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        /// <summary>
        /// Initialise une nouvelle instance de la classe ApplicationDbContext.
        /// </summary>
        /// <param name="options">Les options de configuration pour le contexte.</param>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSet pour chaque entité

        /// <summary>
        /// DbSet pour les entités Packet.
        /// </summary>
        public DbSet<Packet> Packets { get; set; }

        /// <summary>
        /// DbSet pour les entités Sujet.
        /// </summary>
        public DbSet<Sujet> Sujets { get; set; }

        /// <summary>
        /// DbSet pour les entités Famille.
        /// </summary>
        public DbSet<Famille> Familles { get; set; }

        // Vous pouvez ajouter d'autres DbSet ici si nécessaire
    }
}
