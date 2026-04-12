using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using StlExplorerServer.Data;

namespace StlExplorerServer.Services
{
    /// <summary>
    /// Service hébergé responsable du scan automatique en arrière-plan et de la surveillance
    /// des dossiers configurés via <see cref="FileSystemWatcher"/>.
    /// </summary>
    /// <remarks>
    /// Ce service :
    /// - Lance un scan initial au démarrage du serveur (après un court délai).
    /// - Surveille les dossiers racines pour détecter les changements (ajout, suppression, renommage, déplacement).
    /// - Déclenche une synchronisation automatique avec debounce (5 secondes) après un changement détecté.
    /// - Empêche les scans simultanés via un sémaphore.
    /// - Crée un scope DI à chaque scan pour utiliser correctement les services scoped (Repository, DbContext).
    /// </remarks>
    public class BackgroundScannerHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BackgroundScannerHostedService> _logger;

        private readonly List<FileSystemWatcher> _watchers = [];
        private readonly SemaphoreSlim _scanSemaphore = new(1, 1);
        private CancellationTokenSource? _debounceCts;

        // État du scan (volatile pour accès thread-safe sans verrou)
        private volatile bool _scanEnCours;
        private volatile int _progressionScan;

        /// <summary>
        /// Indique si un scan est actuellement en cours.
        /// </summary>
        public bool ScanEnCours => _scanEnCours;

        /// <summary>
        /// Pourcentage d'avancement du scan en cours (0 à 100).
        /// Lit la valeur statique mise à jour par <see cref="FolderScannerService"/>.
        /// </summary>
        public int ProgressionScan => _scanEnCours
            ? FolderScannerService.ProgressionScanStatique
            : _progressionScan;

        /// <summary>
        /// Phase courante du scan ("Création de la base" ou "Mise à jour de la base").
        /// </summary>
        public string PhaseOperation => _scanEnCours
            ? FolderScannerService.PhaseStatique
            : string.Empty;

       /// <summary>
       /// Nombre de secondes écoulées depuis le début du scan.
       /// </summary>
       public double ElapsedSeconds => _scanEnCours
           ? FolderScannerService.ElapsedSecondsStatique
           : 0;

        public BackgroundScannerHostedService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<BackgroundScannerHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        #region Cycle de vie du service hébergé

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Appliquer les migrations EF Core AVANT le scan pour éviter les erreurs
            // "Unknown column" si le schéma n'est pas encore à jour.
            await AppliquerMigrationsAsync(stoppingToken);

            // Scan initial au démarrage si des dossiers sont configurés
            var rootDirs = _configuration.GetSection("ScannerSettings:RootDirectories").Get<string[]>();
            if (rootDirs != null && rootDirs.Length > 0)
            {
                _logger.LogInformation("Scan automatique au démarrage du serveur...");
                await LancerSynchronisationAsync(stoppingToken);
            }
            else
            {
                _logger.LogWarning("Aucun dossier racine configuré. Le scan automatique au démarrage est désactivé.");
            }

            // Démarrer la surveillance des dossiers
            DemarrerSurveillance();

            // Rester en vie jusqu'à l'arrêt du serveur
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) { /* Arrêt normal */ }

            ArreterSurveillance();
        }

        /// <summary>
        /// Applique les migrations EF Core avec retry, garantissant que le schéma de la base
        /// est à jour avant tout scan. Remplace l'ancien Task.Run fire-and-forget de Program.cs.
        /// </summary>
        private async Task AppliquerMigrationsAsync(CancellationToken stoppingToken)
        {
            const int maxRetries = 15;
            for (int i = 0; i < maxRetries; i++)
            {
                stoppingToken.ThrowIfCancellationRequested();
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await db.Database.MigrateAsync(stoppingToken);
                    _logger.LogInformation("Migrations appliquées avec succès.");
                    return;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "Tentative {Attempt}/{MaxRetries} — MariaDB pas encore prêt : {Message}",
                        i + 1, maxRetries, ex.Message);
                    if (i == maxRetries - 1)
                    {
                        _logger.LogCritical(
                            "Impossible de se connecter à MariaDB après {MaxRetries} tentatives. Le scan ne sera pas lancé.",
                            maxRetries);
                        return;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        #endregion

        #region Scan manuel / automatique

        /// <summary>
        /// Lance la synchronisation intelligente en arrière-plan.
        /// Retourne false immédiatement si un scan est déjà en cours.
        /// </summary>
        /// <remarks>
        /// Crée un scope DI interne pour instancier les services scoped (Repository, DbContext)
        /// de manière sûre dans un contexte background (pas de requête HTTP).
        /// </remarks>
        public async Task<bool> LancerSynchronisationAsync(CancellationToken cancellationToken = default)
        {
            if (!await _scanSemaphore.WaitAsync(0, cancellationToken))
            {
                _logger.LogWarning("Un scan est déjà en cours, demande ignorée.");
                return false;
            }

            _scanEnCours = true;
            _progressionScan = 0;

            try
            {
                await Task.Run(() =>
                {
                    // Créer un scope DI dédié pour ce scan (services scoped : Repository, DbContext)
                    using var scope = _scopeFactory.CreateScope();
                    var scanner = scope.ServiceProvider.GetRequiredService<IFolderScannerService>();

                    scanner.SynchronisationIntelligente();
                    scanner.InvaliderCache();

                }, cancellationToken);

                _progressionScan = 100;
                _logger.LogInformation("Synchronisation terminée avec succès.");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Synchronisation annulée.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la synchronisation en arrière-plan.");
                return false;
            }
            finally
            {
                _scanEnCours = false;
                _scanSemaphore.Release();
            }
        }

        /// <summary>
        /// Invalide le cache mémoire des métadonnées scannées.
        /// </summary>
        public void InvaliderCache()
        {
            using var scope = _scopeFactory.CreateScope();
            var scanner = scope.ServiceProvider.GetRequiredService<IFolderScannerService>();
            scanner.InvaliderCache();
        }

        #endregion

        #region Surveillance FileSystemWatcher

        /// <summary>
        /// Configure et démarre un FileSystemWatcher pour chaque dossier racine configuré.
        /// </summary>
        private void DemarrerSurveillance()
        {
            var rootDirs = _configuration.GetSection("ScannerSettings:RootDirectories").Get<string[]>();
            if (rootDirs == null || rootDirs.Length == 0) return;

            foreach (var dir in rootDirs)
            {
                if (!Directory.Exists(dir))
                {
                    _logger.LogWarning("Dossier surveillé inexistant, surveillance ignorée : {Dir}", dir);
                    continue;
                }

                try
                {
                    var watcher = new FileSystemWatcher(dir)
                    {
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true,
                        NotifyFilter = NotifyFilters.DirectoryName
                                     | NotifyFilters.FileName
                                     | NotifyFilters.LastWrite
                                     | NotifyFilters.Size
                    };

                    watcher.Created += OnChangementDetecte;
                    watcher.Deleted += OnChangementDetecte;
                    watcher.Renamed += OnRenommageDetecte;
                    watcher.Changed += OnChangementDetecte;
                    watcher.Error += OnWatcherError;

                    _watchers.Add(watcher);
                    _logger.LogInformation("Surveillance FileSystemWatcher démarrée pour : {Dir}", dir);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Impossible de démarrer la surveillance pour : {Dir}", dir);
                }
            }
        }

        /// <summary>
        /// Arrête et libère tous les FileSystemWatchers.
        /// </summary>
        private void ArreterSurveillance()
        {
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
            _logger.LogInformation("Surveillance FileSystemWatcher arrêtée.");
        }

        private void OnChangementDetecte(object sender, FileSystemEventArgs e)
        {
            _logger.LogInformation("Changement détecté ({ChangeType}) : {Path}", e.ChangeType, e.FullPath);
            DeclencherScanDiffere();
        }

        private void OnRenommageDetecte(object sender, RenamedEventArgs e)
        {
            _logger.LogInformation("Renommage détecté : {OldPath} → {NewPath}", e.OldFullPath, e.FullPath);
            DeclencherScanDiffere();
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            _logger.LogError(e.GetException(), "Erreur du FileSystemWatcher.");
        }

        /// <summary>
        /// Déclenche un scan différé avec debounce.
        /// Plusieurs changements rapprochés (ex: copie de nombreux fichiers) ne déclenchent
        /// qu'un seul scan, 5 secondes après le dernier changement détecté.
        /// </summary>
        private void DeclencherScanDiffere()
        {
            // Annuler le timer précédent s'il existe
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    // Attendre 5 secondes de calme avant de lancer le scan
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                    _logger.LogInformation("Synchronisation automatique déclenchée après changement(s) détecté(s).");
                    await LancerSynchronisationAsync(CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                    // Debounce annulé car un nouveau changement a été détecté — normal
                }
            }, token);
        }

        #endregion
    }
}
