using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StlExplorerServer.Services;
using System;

namespace StlExplorerServer.Controllers
{
    /// <summary>
    /// Contrôleur d'API pour gérer les requêtes HTTP liées aux métadonnées des modèles STL.
    /// </summary>
    /// <remarks>
    /// Dans une architecture Web API, un contrôleur agit comme le point d'entrée. 
    /// Il intercepte les requêtes provenant d'un client (comme un navigateur ou une application frontend), 
    /// fait appel à un service pour traiter la logique métier, puis renvoie une réponse HTTP (200 OK, 500 Erreur, etc.).
    /// 
    /// L'attribut <c>[ApiController]</c> active des comportements spécifiques aux API, comme la validation automatique 
    /// des modèles et la liaison de données simplifiée.
    /// L'attribut <c>[Route("api/[controller]")]</c> indique que l'URL pour accéder à ce contrôleur 
    /// commencera par "api/Metadata" ("Metadata" étant le nom de la classe sans "Controller").
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    public class MetadataController : ControllerBase
    {
        #region Champs Privés (Dépendances)

        /// <summary>
        /// Service qui gère la logique de scan des dossiers.
        /// </summary>
        private readonly IFolderScannerService _folderScannerService;

        /// <summary>
        /// Outil de journalisation pour tracer ce qu'il se passe sur le serveur.
        /// </summary>
        private readonly ILogger<MetadataController> _logger;

        #endregion

        #region Constructeur

        /// <summary>
        /// Initialise une nouvelle instance de la classe <see cref="MetadataController"/>
        /// </summary>
        /// <param name="folderScannerService">
        /// Instance pointant vers l'implémentation de <see cref="IFolderScannerService"/>. 
        /// Fournie automatiquement par l'Injection de Dépendances (DI) configurée dans Program.cs.
        /// </param>
        /// <param name="logger">
        /// Composant permettant d'écrire des logs (informations, erreurs) dans la console ou des fichiers.
        /// </param>
        public MetadataController(IFolderScannerService folderScannerService, ILogger<MetadataController> logger)
        {
            _folderScannerService = folderScannerService;
            _logger = logger;
        }

        #endregion

        #region Points de Terminaison (Endpoints)

        /// <summary>
        /// Déclenche le scan d'un répertoire spécifique sur le serveur afin de peupler la base de données.
        /// </summary>
        /// <param name="path">
        /// Le chemin (chemin d'accès absolu ou relatif) du dossier à scanner.
        /// L'attribut <c>[FromBody]</c> indique que la valeur de cette chaîne doit être extraite du corps de la requête HTTP.
        /// </param>
        /// <returns>
        /// Un objet <see cref="IActionResult"/> qui représente le résultat de la requête Http :
        /// <c>Status 200 (OK)</c> en cas de succès, ou <c>Status 500 (Internal Server Error)</c> en cas de plantage.
        /// </returns>
        /// <remarks>
        /// On utilise ici <c>[HttpPost]</c> (et non HttpGet) car le scan est une action qui va potentiellement 
        /// modifier l'état du serveur en créant de nouvelles entrées dans la base de données de métadonnées.
        /// L'URL finale de cette méthode sera : <c>POST http://votre-serveur/api/Metadata/scan</c>
        /// </remarks>
        /// <example>
        /// Exemple de requête HTTP côté client (en JavaScript) :
        /// <code>
        /// fetch('api/Metadata/scan', {
        ///     method: 'POST',
        ///     headers: {
        ///         'Content-Type': 'application/json'
        ///     },
        ///     body: JSON.stringify("C:\\MesDossiers\\Pieces3D")
        /// });
        /// </code>
        /// </example>
        [HttpPost("scan")]
        public IActionResult ScanFolder([FromBody] string path)
        {
            _logger.LogInformation("Requête reçue pour scanner le dossier : {Path}", path);

            try
            {
                // Appelle le service métier pour faire le travail lourd de lecture des dossiers.
                _folderScannerService.ScanFolder(path);

                _logger.LogInformation("Dossier scanné avec succès.");
                
                // Renvoie une réponse HTTP 200 (OK) au client avec un petit message.
                return Ok("Folder scanned successfully.");
            }
            catch (Exception ex)
            {
                // On logge l'erreur complète pour les développeurs, mais on renvoie juste le message au client.
                _logger.LogError(ex, "Erreur interne du serveur lors du scan du dossier : {Path}", path);
                
                // StatusCode 500 indique qu'une erreur inattendue (exception) vient du serveur.
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        #endregion
    }
}