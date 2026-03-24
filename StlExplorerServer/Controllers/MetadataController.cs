using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StlExplorerServer.Services;
using ClassLibStlExploServ;
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

        /// <summary>
        /// Récupère un résumé léger de tous les modèles (sans chemins d'images).
        /// Utilise une projection SQL pour un chargement rapide des listes déroulantes.
        /// </summary>
        [HttpGet("modelesResume")]
        public async Task<IActionResult> GetAllModelesResume(
            [FromServices] StlExplorerServer.Repositories.IMetadonneesRepository repository)
        {
            var modeles = await repository.GetAllModelesResumeAsync();
            return Ok(modeles);
        }

        /// <summary>
        /// Récupère uniquement les chemins d'images d'un modèle spécifique.
        /// Endpoint léger pour le chargement à la demande de la galerie.
        /// </summary>
        [HttpGet("modele/{id}/images")]
        public async Task<IActionResult> GetImagesForModele(
            int id,
            [FromServices] StlExplorerServer.Repositories.IMetadonneesRepository repository)
        {
            var images = await repository.GetImagesForModeleAsync(id);
            return Ok(images);
        }

        /// <summary>
        /// Sert une image à partir de son chemin absolu sur le NAS.
        /// Le chemin est passé en paramètre de requête (URL-encodé).
        /// </summary>
        /// <param name="chemin">Chemin absolu (UNC ou local) vers le fichier image.</param>
        /// <returns>Le fichier image en streaming HTTP.</returns>
        [HttpGet("image")]
        public IActionResult GetImage([FromQuery] string chemin)
        {
            if (string.IsNullOrWhiteSpace(chemin))
                return BadRequest("Le paramètre 'chemin' est requis.");

            if (!System.IO.File.Exists(chemin))
            {
                logger.LogWarning("Image introuvable : {Chemin}", chemin);
                return NotFound($"Image introuvable : {chemin}");
            }

            var contentType = System.IO.Path.GetExtension(chemin).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            var stream = System.IO.File.OpenRead(chemin);
            return File(stream, contentType);
        }

        /// <summary>
        /// Crée l'arborescence d'un nouveau modèle sur le NAS (Famille > Sujet > Modèle)
        /// et enregistre les entités correspondantes en base de données.
        /// </summary>
        [HttpPost("creerModele")]
        public IActionResult CreerModele(
            [FromBody] CreerModeleRequete requete,
            [FromServices] StlExplorerServer.Repositories.IMetadonneesRepository repository,
            [FromServices] Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(requete.NomFamille) ||
                string.IsNullOrWhiteSpace(requete.NomSujet) ||
                string.IsNullOrWhiteSpace(requete.NomModele))
                return BadRequest("Les 3 champs (Famille, Sujet, Modèle) sont requis.");

            try
            {
                var roots = configuration.GetSection("ScannerSettings:RootDirectories").Get<string[]>();
                if (roots == null || roots.Length == 0)
                    return BadRequest("Aucun dossier racine configuré.");

                var cheminDossier = System.IO.Path.Combine(roots[0], requete.NomFamille, requete.NomSujet, requete.NomModele);

                if (repository.GetModeleByChemin(cheminDossier) != null)
                    return Conflict("Ce modèle existe déjà en base de données.");

                System.IO.Directory.CreateDirectory(cheminDossier);
                logger.LogInformation("Dossier créé : {Chemin}", cheminDossier);

                // Famille : récupérer ou créer
                var famille = repository.GetFamilleByName(requete.NomFamille);
                if (famille == null)
                {
                    famille = new Famille { NomFamille = requete.NomFamille };
                    repository.SaveFamille(famille);
                }

                // Sujet : récupérer (dans cette famille) ou créer
                var sujet = repository.GetSujetByNameAndFamilleId(requete.NomSujet, famille.FamilleID);
                if (sujet == null)
                {
                    sujet = new Sujet { NomSujet = requete.NomSujet, FamilleID = famille.FamilleID };
                    repository.SaveSujet(sujet);
                }

                // Modèle : créer
                var modele = new Modele
                {
                    Description = requete.NomModele,
                    CheminDossier = cheminDossier,
                    SujetID = sujet.SujetID
                };
                repository.SaveModele(modele);

                // Hydrater les navigations pour la réponse JSON
                modele.Sujet = sujet;
                sujet.Famille = famille;

                logger.LogInformation("Modèle créé : {Description} (ID={Id})", modele.Description, modele.ModeleID);
                return Ok(modele);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors de la création du modèle.");
                return StatusCode(500, $"Erreur interne : {ex.Message}");
            }
        }

        /// <summary>
        /// Téléverse des fichiers dans le dossier d'un modèle existant.
        /// Les images (jpg, png, etc.) sont automatiquement ajoutées à la liste CheminsImages du modèle.
        /// </summary>
        [HttpPost("uploadFichiers/{modeleId}")]
        public async Task<IActionResult> UploadFichiers(
            int modeleId,
            [FromForm] List<IFormFile> fichiers,
            [FromServices] StlExplorerServer.Repositories.IMetadonneesRepository repository)
        {
            var modele = repository.GetModeleById(modeleId);
            if (modele == null)
                return NotFound("Modèle introuvable.");

            if (string.IsNullOrWhiteSpace(modele.CheminDossier))
                return BadRequest("Le modèle n'a pas de chemin de dossier.");

            if (fichiers == null || fichiers.Count == 0)
                return BadRequest("Aucun fichier envoyé.");

            try
            {
                var extensionsImages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

                foreach (var fichier in fichiers)
                {
                    var cheminFichier = System.IO.Path.Combine(modele.CheminDossier, fichier.FileName);
                    using var stream = new FileStream(cheminFichier, FileMode.Create);
                    await fichier.CopyToAsync(stream);

                    var ext = System.IO.Path.GetExtension(fichier.FileName);
                    if (extensionsImages.Contains(ext) && !modele.CheminsImages.Contains(cheminFichier))
                    {
                        modele.CheminsImages.Add(cheminFichier);
                    }

                    logger.LogInformation("Fichier enregistré : {Chemin}", cheminFichier);
                }

                repository.UpdateModele(modele);
                return Ok(new { Message = $"{fichiers.Count} fichier(s) enregistré(s).", ModeleId = modeleId });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors de l'upload des fichiers.");
                return StatusCode(500, $"Erreur interne : {ex.Message}");
            }
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Récupère la liste des dossiers racines configurés dans appsettings.json.
        /// </summary>
        [HttpGet("configuration/rootDirectories")]
        public IActionResult GetRootDirectories(
            [FromServices] Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            var roots = configuration.GetSection("ScannerSettings:RootDirectories").Get<string[]>() ?? [];
            return Ok(roots);
        }

        /// <summary>
        /// Met à jour la liste des dossiers racines dans appsettings.json.
        /// </summary>
        [HttpPut("configuration/rootDirectories")]
        public IActionResult SetRootDirectories([FromBody] string[] directories)
        {
            try
            {
                var appSettingsPath = System.IO.Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(), "appsettings.json");
                var json = System.IO.File.ReadAllText(appSettingsPath);
                var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(json)!;

                var arrayNode = new System.Text.Json.Nodes.JsonArray();
                foreach (var dir in directories)
                    arrayNode.Add(dir);

                jsonNode["ScannerSettings"]!["RootDirectories"] = arrayNode;

                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                System.IO.File.WriteAllText(appSettingsPath, jsonNode.ToJsonString(options));

                logger.LogInformation(
                    "Configuration des dossiers racines mise à jour : {Count} dossier(s)", directories.Length);
                return Ok("Configuration mise à jour.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors de la mise à jour de la configuration.");
                return StatusCode(500, $"Erreur : {ex.Message}");
            }
        }

        #endregion
    }
}