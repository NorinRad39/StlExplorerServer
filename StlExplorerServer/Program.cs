using Microsoft.EntityFrameworkCore;
using StlExplorerServer.Data;
using StlExplorerServer.Repositories;
using StlExplorerServer.Services;

#region Initialisation de l'Application

/// <summary>
/// Point d'entrée de l'application Web.
/// La méthode CreateBuilder prépare l'application en chargeant notamment la configuration 
/// (fichiers appsettings.json), les variables d'environnement, et met en place le conteneur de dépendances.
/// </summary>
var builder = WebApplication.CreateBuilder(args);

#endregion

#region Configuration des Services (Injection de Dépendances)

/// <summary>
/// Le conteneur d'Injection de Dépendances (DI - Dependency Injection) permet d'enregistrer toutes les 
/// briques matérielles ou logicielles (Services) dont notre application aura besoin.
/// Lorsque un contrôleur demandera un service, ASP.NET Core le lui fournira automatiquement.
/// </summary>

// Ajoute les services nécessaires pour explorer et générer la documentation Swagger/OpenAPI (interface de test d'API).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Demande au framework de rechercher tous les "Controllers" dans le projet pour les activer.
builder.Services.AddControllers();

/// <summary>
/// Enregistrement de nos propres services métiers avec la méthode AddScoped.
/// "Scoped" signifie qu'une nouvelle instance du service est créée *pour chaque requête HTTP*.
/// C'est idéal pour les applications web.
/// </summary>
/// <remarks>
/// On associe une Interface (ex: IFolderScannerService) à son Implémentation réelle (ex: FolderScannerService).
/// Cela permet une meilleure flexibilité et d'écrire des tests informatiques plus facilement.
/// </remarks>
builder.Services.AddScoped<IFolderScannerService, FolderScannerService>();
builder.Services.AddScoped<IMetadonneesRepository, MetadataRepository>();

#endregion

#region Configuration de la Base de Données (Entity Framework)

/// <summary>
/// Configuration du contexte de la base de données (Le lien entre notre code C# et la vraie base de données).
/// </summary>
/// <remarks>
/// Ici, on utilise la librairie Pomelo pour se connecter à une base de données MySQL ou MariaDB.
/// La chaîne de connexion (DefaultConnection) est récupérée automatiquement depuis le fichier `appsettings.json`.
/// ServerVersion.AutoDetect permet à Entity Framework de s'adapter automatiquement à la version précise de votre serveur de base de données.
/// </remarks>
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    ));

#endregion

#region Configuration des Logs (Journalisation)

/// <summary>
/// Les Logs permettent d'écrire du texte dans la console pour savoir ce que fait le serveur
/// en temps réel (pratique pour voir les requêtes SQL générées ou les erreurs d'exécution).
/// </summary>
builder.Logging.ClearProviders(); // Nettoie les configurations de logs par défaut
builder.Logging.AddConsole();     // Affiche les messages directement dans la console noire
builder.Logging.AddDebug();       // Affiche les messages dans la fenêtre "Sortie" de Visual Studio

#endregion

#region Construction et Configuration du Pipeline HTTP

/// <summary>
/// C'est à ce moment que l'application valide tous les services enregistrés plus haut et crée
/// l'instance de l'application web (app) prête à configurer comment les requêtes web seront traitées.
/// </summary>
var app = builder.Build();

/* 
 * PIPELINE MIDDLEWARES : 
 * Tout ce qui suit "app." configure la façon dont une requête HTTP traversera le serveur (le canal ou Pipeline). 
 * L'ordre de ces déclarations est TRÈS important.
 */

// Si l'application tourne sur votre machine locale (en mode développement), on affiche la page Swagger
if (app.Environment.IsDevelopment())
{
    // Permet de générer le fichier JSON de l'API
    app.UseSwagger();
    // Génère l'interface web graphique conviviale accessible depuis votre navigateur web
    app.UseSwaggerUI();
}

// Redirige automatiquement toutes les requêtes http://... non sécurisées vers https://... (Sécurité)
// Commenté ici si le HTTPS (certificat SSL) n'est pas configuré sur votre environnement de test local.
//app.UseHttpsRedirection();

// Demande à l'application d'analyser l'URL entrante pour l'envoyer au bon contrôleur (ex: /api/Metadata)
app.MapControllers();

#endregion

#region Lancement de l'Application

/// <summary>
/// Démarre l'application. À partir de cette ligne, le serveur écoute les requêtes HTTP entrantes 
/// en boucle indéfiniment jusqu'à ce qu'on le stoppe manuellement.
/// </summary>
app.Run();

#endregion
