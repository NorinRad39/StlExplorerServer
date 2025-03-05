using Microsoft.AspNetCore.Mvc;
using StlExplorerServer.Services;

namespace StlExplorerServer.Controllers
{
    // Déclare ce contrôleur comme un contrôleur d'API
    [ApiController]
    // Définit la route de base pour ce contrôleur, en utilisant le nom du contrôleur dans l'URL
    [Route("api/[controller]")]
    public class MetadataController : ControllerBase
    {
        // Déclare une variable privée pour stocker une instance de IFolderScannerService
        private readonly IFolderScannerService _folderScannerService;

        // Constructeur du contrôleur qui accepte une instance de IFolderScannerService
        // L'injection de dépendances fournira automatiquement cette instance lors de la création du contrôleur
        public MetadataController(IFolderScannerService folderScannerService)
        {
            _folderScannerService = folderScannerService;
        }

        // Définit un endpoint HTTP POST pour scanner un dossier
        // L'URL pour accéder à cet endpoint sera "api/metadata/scan"
        [HttpPost("scan")]
        public IActionResult ScanFolder([FromBody] string path)
        {
            try
            {
                // Appelle le service pour scanner le dossier au chemin spécifié
                _folderScannerService.ScanFolder(path);
                // Retourne une réponse HTTP 200 OK avec un message de succès
                return Ok("Folder scanned successfully.");
            }
            catch (Exception ex)
            {
                // En cas d'erreur, retourne une réponse HTTP 500 avec le message d'erreur
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }


}
