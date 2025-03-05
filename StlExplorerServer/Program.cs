using StlExplorerServer.Repositories;
using StlExplorerServer.Services;
using StlExplorerServer.Data;
using Pomelo.EntityFrameworkCore.MySql;
using Microsoft.EntityFrameworkCore;

// Crée une nouvelle instance de WebApplicationBuilder pour configurer l'application
var builder = WebApplication.CreateBuilder(args);

// Ajoute des services au conteneur de dépendances

// Ajoute les services nécessaires pour explorer et générer la documentation Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Inscrit les services personnalisés pour le scanner de dossiers et le référentiel de métadonnées
builder.Services.AddScoped<IFolderScannerService, FolderScannerService>();
builder.Services.AddScoped<IMetadataRepository, MetadataRepository>();

// Configure le contexte de base de données pour utiliser MariaDB avec la chaîne de connexion spécifiée
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    ));

// Construit l'application avec les services configurés
var app = builder.Build();

// Configure le pipeline de requêtes HTTP

// Mappe les contrôleurs pour gérer les requêtes HTTP
app.MapControllers();

// Si l'application est en mode développement, active Swagger pour la documentation de l'API
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Redirige les requêtes HTTP vers HTTPS pour sécuriser les communications
app.UseHttpsRedirection();

// Démarre l'application et commence à écouter les requêtes entrantes
app.Run();
