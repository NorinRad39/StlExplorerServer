using Microsoft.EntityFrameworkCore;
using StlExplorerServer.Data;
using StlExplorerServer.Repositories;
using StlExplorerServer.Services;

#region Initialisation de l'Application

/// <summary>
/// Point d'entr�e de l'application Web.
/// La m�thode CreateBuilder pr�pare l'application en chargeant notamment la configuration 
/// (fichiers appsettings.json), les variables d'environnement, et met en place le conteneur de d�pendances.
/// </summary>
var builder = WebApplication.CreateBuilder(args);

#endregion

#region Configuration des Services (Injection de D�pendances)

/// <summary>
/// Le conteneur d'Injection de D�pendances (DI - Dependency Injection) permet d'enregistrer toutes les 
/// briques mat�rielles ou logicielles (Services) dont notre application aura besoin.
/// Lorsque un contr�leur demandera un service, ASP.NET Core le lui fournira automatiquement.
/// </summary>

// Ajoute les services n�cessaires pour explorer et g�n�rer la documentation Swagger/OpenAPI (interface de test d'API).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

/// <summary>
/// Limites de débit de Kestrel.
/// </summary>
/// <remarks>
/// Par défaut, Kestrel interrompt une réponse dont le débit tombe sous 240 octets/s
/// et applique la même règle aux corps de requête. Ce garde-fou vise les attaques par
/// connexions lentes ; ici il pénalise les usages légitimes : téléchargement d'un STL
/// de plusieurs centaines de Mo via le VPN, ou téléversement d'un dossier complet
/// depuis un poste distant. Le serveur n'étant pas exposé sur Internet, on le désactive.
/// </remarks>
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MinResponseDataRate = null;
    options.Limits.MinRequestBodyDataRate = null;
});

// Demande au framework de rechercher tous les "Controllers" dans le projet pour les activer.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = 
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

/// <summary>
/// Enregistrement de nos propres services m�tiers avec la m�thode AddScoped.
/// "Scoped" signifie qu'une nouvelle instance du service est cr��e *pour chaque requ�te HTTP*.
/// C'est id�al pour les applications web.
/// </summary>
/// <remarks>
/// On associe une Interface (ex: IFolderScannerService) � son Impl�mentation r�elle (ex: FolderScannerService).
/// Cela permet une meilleure flexibilit� et d'�crire des tests informatiques plus facilement.
/// </remarks>
builder.Services.AddScoped<IFolderScannerService, FolderScannerService>();
builder.Services.AddScoped<IMetadonneesRepository, MetadataRepository>();

// Service h�berg� (singleton) pour le scan en arri�re-plan, la surveillance FileSystemWatcher
// et la gestion s�curis�e des scopes DI (r�sout le probl�me de DbContext dispos�).
builder.Services.AddSingleton<BackgroundScannerHostedService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<BackgroundScannerHostedService>());

#endregion

#region Configuration de la Base de Donn�es (Entity Framework)

/// <summary>
/// Configuration du contexte de la base de donn�es (Le lien entre notre code C# et la vraie base de donn�es).
/// </summary>
/// <remarks>
/// Ici, on utilise la librairie Pomelo pour se connecter � une base de donn�es MySQL ou MariaDB.
/// La cha�ne de connexion (DefaultConnection) est r�cup�r�e automatiquement depuis le fichier `appsettings.json`.
/// ServerVersion.AutoDetect permet � Entity Framework de s'adapter automatiquement � la version pr�cise de votre serveur de base de donn�es.
/// </remarks>
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MariaDbServerVersion(new Version(10, 11, 0)), // Contournement de l'erreur AutoDetect avec Pomelo et .NET
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)
    ));

#endregion

#region Configuration des Logs (Journalisation)

/// <summary>
/// Les Logs permettent d'�crire du texte dans la console pour savoir ce que fait le serveur
/// en temps r�el (pratique pour voir les requ�tes SQL g�n�r�es ou les erreurs d'ex�cution).
/// </summary>
builder.Logging.ClearProviders(); // Nettoie les configurations de logs par d�faut
builder.Logging.AddConsole();     // Affiche les messages directement dans la console noire
builder.Logging.AddDebug();       // Affiche les messages dans la fen�tre "Sortie" de Visual Studio

#endregion

#region Fichier de configuration modifiable (compatible Docker)

// Le fichier appsettings.json est int�gr� dans l'image Docker et est en lecture seule.
// On ajoute un fichier de surcharge modifiable dans un r�pertoire mont� en volume (/app/config/).
// Ce fichier a la priorit� la plus haute dans la cha�ne de configuration ASP.NET Core.
var overrideConfigDir = Path.Combine(Directory.GetCurrentDirectory(), "config");
Directory.CreateDirectory(overrideConfigDir);
var overrideConfigPath = Path.Combine(overrideConfigDir, "override.json");
if (!File.Exists(overrideConfigPath))
    File.WriteAllText(overrideConfigPath, "{}");
builder.Configuration.AddJsonFile(overrideConfigPath, optional: true, reloadOnChange: false);

#endregion

#region Construction et Configuration du Pipeline HTTP

/// <summary>
/// C'est � ce moment que l'application valide tous les services enregistr�s plus haut et cr�e
/// l'instance de l'application web (app) pr�te � configurer comment les requ�tes web seront trait�es.
/// </summary>
var app = builder.Build();

// Les migrations EF Core seront g�r�es par BackgroundScannerHostedService � la place.

/* 
 * PIPELINE MIDDLEWARES : 
 * Tout ce qui suit "app." configure la fa�on dont une requ�te HTTP traversera le serveur (le canal ou Pipeline). 
 * L'ordre de ces d�clarations est TR�S important.
 */

// Si l'application tourne sur votre machine locale (en mode d�veloppement), on affiche la page Swagger
if (app.Environment.IsDevelopment())
{
    // Permet de g�n�rer le fichier JSON de l'API
    app.UseSwagger();
    // G�n�re l'interface web graphique conviviale accessible depuis votre navigateur web
    app.UseSwaggerUI();
}

// Redirige automatiquement toutes les requ�tes http://... non s�curis�es vers https://... (S�curit�)
// Comment� ici si le HTTPS (certificat SSL) n'est pas configur� sur votre environnement de test local.
//app.UseHttpsRedirection();

// Sert les fichiers statiques depuis le dossier wwwroot (ex: viewer3d.html pour la pr�visualisation 3D)
app.UseStaticFiles();

// Demande � l'application d'analyser l'URL entrante pour l'envoyer au bon contr�leur (ex: /api/Metadata)
app.MapControllers();

#endregion

#region Lancement de l'Application

/// <summary>
/// D�marre l'application. � partir de cette ligne, le serveur �coute les requ�tes HTTP entrantes 
/// en boucle ind�finiment jusqu'� ce qu'on le stoppe manuellement.
/// </summary>
app.Run();

#endregion
