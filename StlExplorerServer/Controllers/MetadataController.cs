using Microsoft.AspNetCore.Mvc;
using StlExplorerServer.Services;
using Microsoft.Extensions.Logging;

namespace StlExplorerServer.Controllers
{
    // Déclare ce contrôleur comme un contrôleur d'API
    [ApiController]
    // Définit la route de base pour ce contrôleur, en utilisant le nom du contrôleur dans l'URL
    [Route("api/[controller]")]
    public class MetadataController : ControllerBase
    {
        private readonly IFolderScannerService _folderScannerService;
        private readonly ILogger<MetadataController> _logger;

        // Constructeur du contrôleur qui accepte une instance de IFolderScannerService et ILogger
        public MetadataController(IFolderScannerService folderScannerService, ILogger<MetadataController> logger)
        {
            _folderScannerService = folderScannerService;
            _logger = logger;
        }

        // Définit un endpoint HTTP POST pour scanner un dossier
        [HttpPost("scan")]
        public IActionResult ScanFolder([FromBody] string path)
        {
            _logger.LogInformation("Requête reçue pour scanner le dossier : {Path}", path);

            try
            {
                // Appelle le service pour scanner le dossier au chemin spécifié
                _folderScannerService.ScanFolder(path);
                _logger.LogInformation("Dossier scanné avec succès.");
                return Ok("Folder scanned successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur interne du serveur lors du scan du dossier : {Path}", path);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}