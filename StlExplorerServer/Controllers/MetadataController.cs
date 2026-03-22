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
        /// Déclenche le scan de tous les dossiers racines définis dans le fichier de configuration appsettings.json.
        /// </summary>
        /// <returns>
        /// Un objet <see cref="IActionResult"/> représentant le résultat de l'opération.
        /// </returns>
        [HttpPost("scanAll")]
        public IActionResult ScanAllConfiguredFolders([FromServices] Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            logger.LogInformation("Requête reçue pour scanner tous les dossiers configurés.");

            try
            {
                var roots = configuration.GetSection("ScannerSettings:RootDirectories").Get<string[]>();

                if (roots == null || roots.Length == 0)
                {
                    logger.LogWarning("Aucun dossier racine configuré dans appsettings.json.");
                    return BadRequest("No root directories configured.");
                }

                foreach (var path in roots)
                {
                    logger.LogInformation("Début du scan pour le dossier configuré : {Path}", path);
                    folderScannerService.ScanFolder(path);
                }

                logger.LogInformation("Tous les dossiers configurés ont été scannés avec succès.");
                return Ok("All configured folders scanned successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur interne du serveur lors du scan des dossiers configurés.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Récupère la liste de tous les modèles avec leurs chemins d'images.
        /// </summary>
        /// <returns>La liste des modèles.</returns>
        [HttpGet("modeles")]
        public IActionResult GetAllModeles([FromServices] StlExplorerServer.Repositories.IMetadonneesRepository repository)
        {
            var modeles = repository.GetAllModeles();
            return Ok(modeles);
        }

        #endregion
    }
}