using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1.Cms;
using System.Collections.Generic;
using ClassLibStlExploServ;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;



namespace StlExplorerServer.Data
{
    public class ApplicationDbContext : DbContext
    {
        // Constructeur qui accepte des options de configuration pour le contexte
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {

        }
        // Définissez vos DbSet ici, par exemple :
        public DbSet<Packet> Packets { get; set; }
    }
}
