using StlExplorerServer.Repositories;
using StlExplorerServer.Services;
using StlExplorerServer.Data;
using Pomelo.EntityFrameworkCore.MySql;
using Microsoft.EntityFrameworkCore;

// Cr�e une nouvelle instance de WebApplicationBuilder pour configurer l'application
var builder = WebApplication.CreateBuilder(args);

// Ajoute des services au conteneur de d�pendances

// Ajoute les services n�cessaires pour explorer et g�n�rer la documentation Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Inscrit les services personnalis�s pour le scanner de dossiers et le r�f�rentiel de m�tadonn�es
builder.Services.AddScoped<IFolderScannerService, FolderScannerService>();
builder.Services.AddScoped<IMetadataRepository, MetadataRepository>();

// Configure le contexte de base de donn�es pour utiliser MariaDB avec la cha�ne de connexion sp�cifi�e
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    ));

// Construit l'application avec les services configur�s
var app = builder.Build();

// Configure le pipeline de requ�tes HTTP

// Mappe les contr�leurs pour g�rer les requ�tes HTTP
app.MapControllers();

// Si l'application est en mode d�veloppement, active Swagger pour la documentation de l'API
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Redirige les requ�tes HTTP vers HTTPS pour s�curiser les communications
app.UseHttpsRedirection();

// D�marre l'application et commence � �couter les requ�tes entrantes
app.Run();
