using Microsoft.AspNetCore.Mvc;

namespace StlExplorerServer.Controllers
{
    /// <summary>
    /// Contrôleur de distribution des mises à jour des clients (Android et Windows).
    /// </summary>
    /// <remarks>
    /// Les paquets et leur manifeste sont déposés par les scripts de <c>deploy\</c> dans un
    /// dossier du NAS (par défaut <c>\\ds923.file4all.fr\Applications\STLExplorer</c>), monté
    /// dans le conteneur sur <c>/data/updates</c> :
    /// <code>
    ///   volumes:
    ///     - /volume1/Applications/STLExplorer:/data/updates:ro
    /// </code>
    /// Le client interroge <c>/api/Update/manifest</c> au démarrage, compare la version publiée
    /// à la sienne, puis télécharge le paquet via <c>/api/Update/fichier/{plateforme}</c>.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    public class UpdateController : ControllerBase
    {
        private readonly ILogger<UpdateController> logger;
        private readonly IConfiguration configuration;

        public UpdateController(ILogger<UpdateController> logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.configuration = configuration;
        }

        /// <summary>Dossier contenant update.json et les paquets (configurable, utile hors Docker).</summary>
        private string DossierMisesAJour =>
            configuration["UpdateSettings:Folder"] ?? "/data/updates";

        private string CheminManifeste => Path.Combine(DossierMisesAJour, "update.json");

        /// <summary>
        /// Renvoie le manifeste des versions publiées (contenu brut de update.json).
        /// </summary>
        [HttpGet("manifest")]
        public IActionResult GetManifest()
        {
            if (!System.IO.File.Exists(CheminManifeste))
            {
                logger.LogWarning("Manifeste de mise à jour introuvable : {Chemin}", CheminManifeste);
                return NotFound(new
                {
                    Message = "Aucun manifeste de mise à jour publié.",
                    Dossier = DossierMisesAJour
                });
            }

            try
            {
                var json = System.IO.File.ReadAllText(CheminManifeste);
                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lecture du manifeste de mise à jour impossible.");
                return StatusCode(500, $"Erreur interne : {ex.Message}");
            }
        }

        /// <summary>
        /// Télécharge le paquet publié pour une plateforme (« android » ou « windows »).
        /// Le nom de fichier provient du manifeste ; il est validé pour ne pas sortir du dossier.
        /// </summary>
        [HttpGet("fichier/{plateforme}")]
        public IActionResult GetFichier(string plateforme)
        {
            if (!System.IO.File.Exists(CheminManifeste))
                return NotFound("Aucun manifeste de mise à jour publié.");

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(
                    System.IO.File.ReadAllText(CheminManifeste));

                if (!doc.RootElement.TryGetProperty(plateforme.ToLowerInvariant(), out var noeud))
                    return NotFound($"Aucune mise à jour publiée pour « {plateforme} ».");

                if (!noeud.TryGetProperty("fichier", out var champFichier))
                    return NotFound("Le manifeste ne précise pas de fichier pour cette plateforme.");

                var relatif = champFichier.GetString();
                if (string.IsNullOrWhiteSpace(relatif))
                    return NotFound("Le manifeste ne précise pas de fichier pour cette plateforme.");

                var chemin = CombinerCheminSecurise(DossierMisesAJour, relatif);
                if (chemin == null)
                    return BadRequest($"Chemin de paquet refusé : {relatif}");

                if (!System.IO.File.Exists(chemin))
                {
                    logger.LogWarning("Paquet annoncé mais absent du disque : {Chemin}", chemin);
                    return NotFound($"Paquet introuvable : {relatif}");
                }

                var contentType = Path.GetExtension(chemin).ToLowerInvariant() switch
                {
                    ".apk" => "application/vnd.android.package-archive",
                    ".exe" => "application/octet-stream",
                    _ => "application/octet-stream"
                };

                logger.LogInformation("Téléchargement de la mise à jour {Plateforme} : {Chemin}", plateforme, chemin);
                return PhysicalFile(chemin, contentType, Path.GetFileName(chemin), enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors de la distribution de la mise à jour {Plateforme}.", plateforme);
                return StatusCode(500, $"Erreur interne : {ex.Message}");
            }
        }

        /// <summary>
        /// Combine un chemin relatif issu du manifeste avec le dossier des mises à jour,
        /// en refusant toute sortie de ce dossier (« .. », chemin absolu, lettre de lecteur).
        /// </summary>
        private static string? CombinerCheminSecurise(string racine, string cheminRelatif)
        {
            var segments = cheminRelatif
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
                return null;

            if (segments.Any(s => s == "." || s == ".." || s.Contains(':')))
                return null;

            var complet = Path.GetFullPath(Path.Combine(racine, Path.Combine(segments)));
            var prefixeRacine = Path.GetFullPath(racine).TrimEnd(Path.DirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;

            return complet.StartsWith(prefixeRacine, StringComparison.OrdinalIgnoreCase) ? complet : null;
        }
    }
}
