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
    public class MetadataController(IFolderScannerService folderScannerService, ILogger<MetadataController> logger) : ControllerBase
    {
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
            logger.LogInformation("Requête reçue pour scanner le dossier : {Path}", path);

            try
            {
                folderScannerService.ScanFolder(path);

                logger.LogInformation("Dossier scanné avec succès.");
                
                return Ok("Folder scanned successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur interne du serveur lors du scan du dossier : {Path}", path);
                
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        #endregion
    }
}