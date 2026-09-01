using System.Net.Http.Json;
using System.Net.Http.Headers;
using ClassLibStlExploServ;

namespace StlExplorerClient
{
    // DTOs pour le statut et la progression du scan
    file record ScanStatusDto(bool ScanEnCours);
    file record ScanProgressDto(int Progression, string Phase, int ElapsedSeconds);
   file record ScanLogDto(List<string> Entries, int TotalCount);

    public partial class MainPage : ContentPage
    {
        private List<ModeleResume> _allModeles = new();
        private List<string> _currentImages = new();
        private int _currentImageIndex = -1;
        private HttpClient? _httpClient;
        private ModeleResume? _modeleCourant;

        // Vrai pendant qu'un champ est rempli par le code (et non par l'utilisateur) :
        // évite à la fois le vidage en cascade des champs enfants et l'ouverture des listes déroulantes.
        private bool _majProgrammatique;
        private List<Fichier3D> _currentFichiers3D = new();
        private bool _viewer3DVisible;

        private bool _dataLoaded;
        private CancellationTokenSource? _scanPollCts;
       private int _lastScanLogIndex;
       private readonly List<string> _debugLogs = new();
       private const int MaxDebugLogEntries = 200;

        // Défilement rapide entre les modèles du sujet courant
        private List<ModeleResume> _modelesNavigation = new();
        private int _navIndex = -1;
        private bool _pageActive;

        // Client HTTP dédié aux téléversements (timeout long, contrairement au client principal)
        private HttpClient? _uploadHttpClient;

        // Incrémenté à chaque changement de modèle : sert à ignorer les aperçus obsolètes
        private int _apercuJeton;

        // Le viewer 3D affiche une seule alerte fatale par session (WebGL absent, module JS en échec...)
        // pour ne pas spammer l'utilisateur si l'erreur se reproduit à chaque fichier 3D ouvert.
        private bool _erreurViewer3DAffichee;

        public MainPage()
        {
            InitializeComponent();

            // Le panel d'actions (création de dossier, ajout de fichiers) n'est visible que sur Windows
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                WindowsActionsPanel.IsVisible = true;

            // Le viewer 3D (Three.js) signale ses erreurs via une fausse navigation
            // stlviewerlog://... interceptée ici, plutôt qu'un écran vide sans indice.
            Viewer3DWebView.Navigating += OnViewer3DNavigating;

            Loaded += OnPageLoaded;
        }

        /// <summary>
        /// Intercepte les messages de diagnostic envoyés par viewer3d.html (voir reportLog() côté JS)
        /// sous forme de fausse navigation vers le pseudo-schéma stlviewerlog://, et les redirige
        /// vers le journal de debug de l'application (utile notamment sur Android, où un échec du
        /// viewer 3D — WebGL absent, module Three.js pas encore chargé, CDN injoignable — se traduisait
        /// jusqu'ici par un écran vide sans aucun message).
        /// </summary>
        private void OnViewer3DNavigating(object? sender, WebNavigatingEventArgs e)
        {
            const string prefixe = "stlviewerlog://";
            if (string.IsNullOrEmpty(e.Url) || !e.Url.StartsWith(prefixe, StringComparison.OrdinalIgnoreCase))
                return;

            e.Cancel = true;

            string message;
            try { message = Uri.UnescapeDataString(e.Url.Substring(prefixe.Length)); }
            catch { message = e.Url.Substring(prefixe.Length); }

            const string marqueurFatal = "FATAL:";
            bool fatal = message.StartsWith(marqueurFatal, StringComparison.OrdinalIgnoreCase);
            if (fatal) message = message.Substring(marqueurFatal.Length).Trim();

            LogDebug($"🧊 [Viewer3D] {message}");

            if (fatal && !_erreurViewer3DAffichee)
            {
                _erreurViewer3DAffichee = true;
                _ = DisplayAlert("Aperçu 3D indisponible", message, "OK");
            }
        }

       /// <summary>
       /// Ajoute une entrée horodatée dans le journal de debug visible par l'utilisateur.
       /// </summary>
       private void LogDebug(string message)
       {
           var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
           _debugLogs.Add(entry);
           if (_debugLogs.Count > MaxDebugLogEntries)
               _debugLogs.RemoveAt(0);
           MainThread.BeginInvokeOnMainThread(() =>
           {
               DebugLogLabel.Text = string.Join("\n", _debugLogs);
               _ = DebugLogScrollView.ScrollToAsync(0, double.MaxValue, false);
           });
       }

       private void OnToggleDebugLogClicked(object? sender, EventArgs e)
       {
           DebugLogScrollView.IsVisible = !DebugLogScrollView.IsVisible;
           BtnToggleDebugLog.Text = DebugLogScrollView.IsVisible ? "▲" : "▼";
       }

       private void OnClearDebugLogClicked(object sender, EventArgs e)
       {
           _debugLogs.Clear();
           DebugLogLabel.Text = "";
       }

       /// <summary>
       /// Calcule le temps restant estimé du scan à partir du pourcentage et du temps écoulé.
       /// </summary>
       private static string CalculerEta(int pourcentage, int elapsedSeconds)
       {
           if (pourcentage <= 0 || elapsedSeconds <= 3)
               return "estimation...";

           double totalEstime = elapsedSeconds / (pourcentage / 100.0);
           double restant = totalEstime - elapsedSeconds;

           if (restant < 5) return "< 5s";
           if (restant < 60) return $"~{(int)restant}s restant";
           int minutes = (int)(restant / 60);
           int secondes = (int)(restant % 60);
           return $"~{minutes}m {secondes:D2}s restant";
       }

        private async void OnPageLoaded(object? sender, EventArgs e)
        {
#if WINDOWS
            BrancherClavierWindows();
#endif
            if (!_dataLoaded)
            {
                _dataLoaded = true;
                await LoadDataAsync();
            }
        }

        private async Task LoadDataAsync()
        {
            try 
            {
                var handler = new HttpClientHandler
                {
                    // Désactiver le proxy système pour éviter les timeouts sur le réseau local/NAS
                    UseProxy = false
                };

                // Lire l'URL du serveur depuis les préférences (configurable dans la page Config)
                var defaultUrl =
#if ANDROID
                    "http://10.0.2.2:5180";
#else
                    "http://localhost:5180";
#endif
                var serverUrl = Preferences.Get(ConfigPage.ServerUrlKey, defaultUrl).TrimEnd('/');

#if ANDROID
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#endif
                _httpClient = new HttpClient(handler)
                {
                    BaseAddress = new Uri(serverUrl + "/"),
                    Timeout = TimeSpan.FromSeconds(30) // Timeout global raisonnable
                };
                LogDebug($"Connexion au serveur : {serverUrl}");

                // Démarrer le polling immédiatement (avant le chargement des données)
                // pour que la barre de progression s'affiche dès que possible.
                if (_scanPollCts == null)
                    DemarrerPollingStatutScan();

                // Charger les modèles avec retry si le serveur est occupé (scan en cours au démarrage)
                await ChargerModelesAvecRetryAsync();
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Erreur connexion serveur : {ex.Message}");
                try
                {
                    await DisplayAlert("Erreur", "Impossible de contacter le serveur : " + ex.Message, "OK");
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine($"Erreur serveur : {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Charge la liste des modèles avec un mécanisme de retry automatique.
        /// Si le serveur est occupé par un scan, arrête les tentatives et laisse
        /// le polling recharger les modèles automatiquement à la fin du scan.
        /// </summary>
        private async Task ChargerModelesAvecRetryAsync()
        {
            if (_httpClient == null) return;

            const int maxRetries = 6;  // 6 × 5s = 30s max d'attente
            for (int tentative = 1; tentative <= maxRetries; tentative++)
            {
                // Vérifier d'abord si un scan est en cours (endpoint léger, pas de DB)
                try
                {
                    using var statusCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                    var status = await _httpClient.GetFromJsonAsync<ScanStatusDto>(
                        "/api/Metadata/scan-status", statusCts.Token);

                    if (status?.ScanEnCours == true)
                    {
                        LogDebug("⏳ Scan en cours — chargement des modèles reporté (automatique à la fin du scan).");
                        return; // Le polling appellera RafraichirModelesAsync() quand le scan se termine
                    }
                }
                catch { /* scan-status indisponible, on tente le chargement quand même */ }

                // Pas de scan en cours → tenter de charger les modèles
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    var response = await _httpClient.GetFromJsonAsync<List<ModeleResume>>(
                        "/api/Metadata/modelesResume", cts.Token);

                    if (response != null)
                    {
                        _allModeles = response;
                        LogDebug($"✅ {response.Count} modèle(s) chargé(s)");
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            UpdateFamilles();
                            UpdateSujets();
                            UpdateModeles();
                            RafraichirEtatInterface();
                        });
                        return; // Succès → on sort
                    }
                    else
                    {
                        LogDebug("⚠ Réponse vide du serveur (aucun modèle).");
                        return;
                    }
                }
                catch (Exception ex) when (tentative < maxRetries)
                {
                    LogDebug($"⏳ Tentative {tentative}/{maxRetries} — serveur pas prêt : {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }

            LogDebug("❌ Impossible de charger les modèles après plusieurs tentatives.");
        }

        /// <summary>
        /// Recharge uniquement la liste des modèles sans réinitialiser le client HTTP.
        /// Utilisé après la fin d'un scan pour mettre à jour l'interface.
        /// </summary>
        private async Task RafraichirModelesAsync()
        {
            if (_httpClient == null) return;
            try
            {
                var response = await _httpClient.GetFromJsonAsync<List<ModeleResume>>("/api/Metadata/modelesResume");
                if (response != null)
                {
                    _allModeles = response;
                   LogDebug($"🔄 Modèles rafraîchis : {response.Count} modèle(s)");
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        UpdateFamilles();
                        UpdateSujets();
                        UpdateModeles();
                        RafraichirEtatInterface();
                    });
                }
            }
           catch (Exception ex)
           {
               LogDebug($"❌ Erreur rafraîchissement modèles : {ex.Message}");
           }
        }

        /// <summary>
        /// Démarre le polling toutes les 2 secondes pour surveiller l'état du scan.
        /// </summary>
        private void DemarrerPollingStatutScan()
        {
            _scanPollCts = new CancellationTokenSource();
            _ = PollScanStatutAsync(_scanPollCts.Token);
        }

        private async Task PollScanStatutAsync(CancellationToken token)
        {
            bool dernierEtatScanEnCours = false;
           LogDebug("📡 Polling du statut de scan démarré (toutes les 2s)");
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await timer.WaitForNextTickAsync(token);
                    if (_httpClient == null) continue;

                    var status = await _httpClient.GetFromJsonAsync<ScanStatusDto>(
                        "/api/Metadata/scan-status", token);
                    bool scanEnCours = status?.ScanEnCours ?? false;

                    if (scanEnCours)
                    {
                        var progress = await _httpClient.GetFromJsonAsync<ScanProgressDto>(
                            "/api/Metadata/scan-progress", token);
                        int pct = progress?.Progression ?? 0;
                        int elapsed = progress?.ElapsedSeconds ?? 0;
                        string phase = string.IsNullOrWhiteSpace(progress?.Phase)
                            ? "Scan en cours..."
                            : progress.Phase;
                        string eta = CalculerEta(pct, elapsed);

                       if (!dernierEtatScanEnCours)
                       {
                           LogDebug($"🔍 Scan détecté : {phase}");
                           _lastScanLogIndex = 0; // Réinitialiser pour récupérer tout le log du nouveau scan
                       }

                        // Récupérer les nouvelles entrées du journal de scan serveur
                        try
                        {
                            var scanLog = await _httpClient.GetFromJsonAsync<ScanLogDto>(
                                $"/api/Metadata/scan-log?fromIndex={_lastScanLogIndex}", token);
                            if (scanLog?.Entries != null)
                            {
                                foreach (var entry in scanLog.Entries)
                                    LogDebug($"📋 {entry}");
                                _lastScanLogIndex = scanLog.TotalCount;
                            }
                        }
                        catch { /* Le scan-log est optionnel, on continue si ça échoue */ }

                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            ScanProgressPanel.IsVisible = true;
                            ScanProgressBar.Progress = pct / 100.0;
                            ScanPercentLabel.Text = $"{pct}%";
                            ScanPhaseLabel.Text = phase;
                            ScanEtaLabel.Text = eta;
                        });
                    }
                    else
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                            ScanProgressPanel.IsVisible = false);

                        // Le scan vient de se terminer → récupérer les dernières entrées du log
                        if (dernierEtatScanEnCours)
                       {
                            try
                            {
                                var scanLog = await _httpClient.GetFromJsonAsync<ScanLogDto>(
                                    $"/api/Metadata/scan-log?fromIndex={_lastScanLogIndex}", token);
                                if (scanLog?.Entries != null)
                                {
                                    foreach (var entry in scanLog.Entries)
                                        LogDebug($"📋 {entry}");
                                    _lastScanLogIndex = scanLog.TotalCount;
                                }
                            }
                            catch { /* optionnel */ }

                           LogDebug("✅ Scan terminé — rechargement des modèles...");
                            await RafraichirModelesAsync();
                       }
                    }

                    dernierEtatScanEnCours = scanEnCours;
                }
                catch (OperationCanceledException) { break; }
               catch (Exception ex)
               {
                   LogDebug($"⚠ Erreur polling : {ex.Message}");
               }
            }
        }



        private void ClearChildEntries(params Entry[] entries)
        {
            if (_majProgrammatique) return;
            foreach (var entry in entries)
                entry.Text = "";
        }

        // ============================================
        // Mises à jour des CollectionViews (Suggestions)
        // ============================================
        private void UpdateFamilles(string filter = "")
        {
            var familles = _allModeles
                .Where(m => !string.IsNullOrEmpty(m.NomFamille))
                .Select(m => m.NomFamille!)
                .Distinct();

            if (!string.IsNullOrWhiteSpace(filter))
                familles = familles.Where(f => f.Contains(filter, StringComparison.OrdinalIgnoreCase));

            FamilleCollectionView.ItemsSource = familles.OrderBy(f => f).ToList();
        }

        private void UpdateSujets(string filter = "")
        {
            var familleFiltre = FamilleEntry.Text;
            var query = _allModeles.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(familleFiltre))
                query = query.Where(m => m.NomFamille == familleFiltre);

            var sujets = query
                .Where(m => !string.IsNullOrEmpty(m.NomSujet))
                .Select(m => m.NomSujet!)
                .Distinct();

            if (!string.IsNullOrWhiteSpace(filter))
                sujets = sujets.Where(s => s.Contains(filter, StringComparison.OrdinalIgnoreCase));

            SujetCollectionView.ItemsSource = sujets.OrderBy(s => s).ToList();
        }

        private void UpdateModeles(string filter = "")
        {
            var familleFiltre = FamilleEntry.Text;
            var sujetFiltre = SujetEntry.Text;
            var query = _allModeles.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(familleFiltre))
                query = query.Where(m => m.NomFamille == familleFiltre);
            if (!string.IsNullOrWhiteSpace(sujetFiltre))
                query = query.Where(m => m.NomSujet == sujetFiltre);

            var modeles = query
                .Where(m => !string.IsNullOrEmpty(m.Description))
                .Select(m => m.Description!)
                .Distinct();

            if (!string.IsNullOrWhiteSpace(filter))
                modeles = modeles.Where(m => m.Contains(filter, StringComparison.OrdinalIgnoreCase));

            ModeleCollectionView.ItemsSource = modeles.OrderBy(m => m).ToList();
        }

        // ============================================
        // Logique pour le champ "Famille"
        // ============================================
        private void OnFamilleTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateFamilles(e.NewTextValue);
            FamilleListContainer.IsVisible = !_majProgrammatique && !string.IsNullOrWhiteSpace(e.NewTextValue);
            ClearChildEntries(SujetEntry, ModeleEntry);
            UpdateSujets();
            UpdateModeles();
            RafraichirEtatInterface();
        }

        private void OnFamilleDropdownClicked(object sender, EventArgs e)
        {
            UpdateFamilles(); // Rafraîchit les choix sans filtre texte
            FamilleListContainer.IsVisible = !FamilleListContainer.IsVisible;
        }

        private void OnFamilleSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is string selection)
            {
                FamilleEntry.Text = selection;
                FamilleListContainer.IsVisible = false;
                FamilleCollectionView.SelectedItem = null;
                UpdateSujets();
                SujetListContainer.IsVisible = true;
                RafraichirEtatInterface();
            }
        }

        // ============================================
        // Logique pour le champ "Sujet"
        // ============================================
        private void OnSujetTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSujets(e.NewTextValue);
            SujetListContainer.IsVisible = !_majProgrammatique && !string.IsNullOrWhiteSpace(e.NewTextValue);
            ClearChildEntries(ModeleEntry);
            UpdateModeles();
            RafraichirEtatInterface();
        }

        private void OnSujetDropdownClicked(object sender, EventArgs e)
        {
            UpdateSujets();
            SujetListContainer.IsVisible = !SujetListContainer.IsVisible;
        }

        private void OnSujetSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is string selection)
            {
                _majProgrammatique = true;
                SujetEntry.Text = selection;
                _majProgrammatique = false;
                SujetListContainer.IsVisible = false;
                SujetCollectionView.SelectedItem = null;

                // Déduire la famille si elle est vide (sans vider les champs ni rouvrir les listes)
                _majProgrammatique = true;
                var premierModele = _allModeles.FirstOrDefault(m => m.NomSujet == selection);
                if (premierModele != null && string.IsNullOrEmpty(FamilleEntry.Text))
                {
                    FamilleEntry.Text = premierModele.NomFamille;
                }
                _majProgrammatique = false;
                FamilleListContainer.IsVisible = false;
                UpdateModeles();
                ModeleListContainer.IsVisible = true;
                RafraichirEtatInterface();
            }
        }

        // ============================================
        // Logique pour le champ "Modèle"
        // ============================================
        private void OnModeleTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateModeles(e.NewTextValue);
            ModeleListContainer.IsVisible = !_majProgrammatique && !string.IsNullOrWhiteSpace(e.NewTextValue);
            LoadGalleryForModele(e.NewTextValue);
            RafraichirEtatInterface();
        }

        private void OnModeleDropdownClicked(object sender, EventArgs e)
        {
            UpdateModeles();
            ModeleListContainer.IsVisible = !ModeleListContainer.IsVisible;
        }

        private void OnModeleSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is string selection)
            {
                _majProgrammatique = true;
                ModeleEntry.Text = selection;
                _majProgrammatique = false;
                ModeleListContainer.IsVisible = false;
                ModeleCollectionView.SelectedItem = null;

                // Déduire Famille et Sujet si vides (sans vider les champs ni rouvrir les listes)
                _majProgrammatique = true;
                var modele = _allModeles.FirstOrDefault(m => m.Description == selection);
                if (modele != null)
                {
                    if (string.IsNullOrEmpty(SujetEntry.Text)) SujetEntry.Text = modele.NomSujet;
                    if (string.IsNullOrEmpty(FamilleEntry.Text)) FamilleEntry.Text = modele.NomFamille;
                }
                _majProgrammatique = false;
                FamilleListContainer.IsVisible = false;
                SujetListContainer.IsVisible = false;

                LoadGalleryForModele(selection);
                RafraichirEtatInterface();
            }
        }

        // ============================================
        // Galerie d'images et contenu du modèle
        // ============================================
        private async void LoadGalleryForModele(string nomModele)
        {
            RetourAuxImages();

            // Jeton d'aperçu : si l'utilisateur change de modèle pendant le chargement,
            // la réponse tardive du serveur ne doit pas écraser le nouvel aperçu.
            var jeton = ++_apercuJeton;

            var modele = _allModeles.FirstOrDefault(m => m.Description == nomModele);
            if (modele != null && _httpClient != null)
            {
                try
                {
                    var images = await _httpClient.GetFromJsonAsync<List<string>>(
                        $"/api/Metadata/modele/{modele.ModeleID}/images");
                    if (jeton != _apercuJeton) return;

                    if (images != null && images.Count > 0)
                    {
                        _currentImages = images;
                        _currentImageIndex = 0;
                        await DisplayCurrentImageAsync();
                    }
                    else
                    {
                        ClearGallery();
                    }
                }
                catch
                {
                    if (jeton != _apercuJeton) return;
                    ClearGallery();
                }

                LoadContenuForModele(modele.ModeleID, jeton);
                return;
            }

            ClearGallery();
            ClearContenuPanels();
        }

        private void ClearGallery()
        {
            _currentImages.Clear();
            _currentImageIndex = -1;
            ModeleImage.Source = null;
            ImageCounterLabel.Text = "";
        }

        private async void LoadContenuForModele(int modeleId, int jeton)
        {
            if (_httpClient == null) { ClearContenuPanels(); return; }

            try
            {
                var contenu = await _httpClient.GetFromJsonAsync<ContenuModele>(
                    $"/api/Metadata/modele/{modeleId}/contenu");
                if (jeton != _apercuJeton) return;

                if (contenu != null)
                {
                    var items = new List<string>();
                    foreach (var d in contenu.Dossiers) items.Add($"📁 {d}");
                    foreach (var f in contenu.Fichiers) items.Add($"📄 {f}");

                    if (items.Count > 0)
                    {
                        ContenuDossierCollectionView.ItemsSource = items;
                        ContenuDossierPanel.IsVisible = true;
                    }
                    else
                    {
                        ContenuDossierPanel.IsVisible = false;
                    }

                    _currentFichiers3D = contenu.Fichiers3D;
                    if (_currentFichiers3D.Count > 0)
                    {
                        Fichiers3DCollectionView.ItemsSource = _currentFichiers3D;
                        Fichiers3DPanel.IsVisible = true;
                    }
                    else
                    {
                        Fichiers3DPanel.IsVisible = false;
                    }
                    return;
                }
            }
            catch { /* Panneaux vidés ci-dessous */ }

            ClearContenuPanels();
        }

        private void ClearContenuPanels()
        {
            ContenuDossierCollectionView.ItemsSource = null;
            ContenuDossierPanel.IsVisible = false;
            Fichiers3DCollectionView.ItemsSource = null;
            Fichiers3DPanel.IsVisible = false;
            _currentFichiers3D.Clear();
        }

        /// <summary>
        /// Télécharge et affiche l'image courante via HttpClient.
        /// Utilise ImageSource.FromStream au lieu de ImageSource.FromUri
        /// pour éviter les problèmes de chemins UNC encodés et de cache plateforme.
        /// </summary>
        private async Task DisplayCurrentImageAsync()
        {
            var jeton = _apercuJeton;

            if (_currentImageIndex >= 0 && _currentImageIndex < _currentImages.Count && _httpClient != null)
            {
                var imagePath = _currentImages[_currentImageIndex];
                var encodedPath = Uri.EscapeDataString(imagePath);
                try
                {
                    var response = await _httpClient.GetAsync($"api/Metadata/image?chemin={encodedPath}");
                    if (jeton != _apercuJeton) return;

                    if (response.IsSuccessStatusCode)
                    {
                        var bytes = await response.Content.ReadAsByteArrayAsync();
                        ModeleImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
                    }
                    else
                    {
                        ModeleImage.Source = null;
                    }
                }
                catch
                {
                    ModeleImage.Source = null;
                }
                ImageCounterLabel.Text = $"{_currentImageIndex + 1} / {_currentImages.Count}";
            }
            else
            {
                ImageCounterLabel.Text = "";
            }
        }

        private async void OnImagePrecedenteClicked(object sender, EventArgs e)
        {
            if (_currentImages.Count > 0)
            {
                _currentImageIndex--;
                if (_currentImageIndex < 0) _currentImageIndex = _currentImages.Count - 1; // Boucler
                await DisplayCurrentImageAsync();
            }
        }

        private async void OnImageSuivanteClicked(object sender, EventArgs e)
        {
            if (_currentImages.Count > 0)
            {
                _currentImageIndex++;
                if (_currentImageIndex >= _currentImages.Count) _currentImageIndex = 0; // Boucler
                await DisplayCurrentImageAsync();
            }
        }

        // ============================================
        // Gestion Windows : Création de dossiers et ajout de fichiers
        // ============================================

        /// <summary>
        /// Met à jour la visibilité des boutons "Créer le dossier" et "Ajouter des fichiers"
        /// en fonction de l'état des 3 champs de recherche.
        /// </summary>
        private void UpdateWindowsActions()
        {
            if (DeviceInfo.Platform != DevicePlatform.WinUI)
                return;

            var famille = FamilleEntry.Text?.Trim();
            var sujet = SujetEntry.Text?.Trim();
            var modele = ModeleEntry.Text?.Trim();

            bool champsPleins = !string.IsNullOrWhiteSpace(famille)
                             && !string.IsNullOrWhiteSpace(sujet)
                             && !string.IsNullOrWhiteSpace(modele);

            // Téléversement d'un dossier déjà nommé : Famille et Sujet renseignés, Modèle laissé vide.
            // Le nom du dossier choisi deviendra le nom du modèle.
            BtnTeleverserDossier.IsVisible = !string.IsNullOrWhiteSpace(famille)
                                          && !string.IsNullOrWhiteSpace(sujet)
                                          && string.IsNullOrWhiteSpace(modele);

            if (champsPleins)
            {
                // Vérifier si cette combinaison exacte existe dans les données chargées
                var existant = _allModeles.FirstOrDefault(m =>
                    string.Equals(m.Description, modele, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(m.NomSujet, sujet, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(m.NomFamille, famille, StringComparison.OrdinalIgnoreCase));

                if (existant != null)
                {
                    // Modèle existant : proposer d'ajouter des fichiers, renommer, ouvrir
                    _modeleCourant = existant;
                    BtnCreerDossier.IsVisible = false;
                    BtnAjouterFichiers.IsVisible = true;
                    BtnRenommerModele.IsVisible = true;
                    BtnOuvrirExplorateur.IsVisible = true;
                }
                else
                {
                    // Nouveau modèle : proposer de créer le dossier
                    _modeleCourant = null;
                    BtnCreerDossier.IsVisible = true;
                    BtnAjouterFichiers.IsVisible = false;
                    BtnRenommerModele.IsVisible = false;
                    BtnOuvrirExplorateur.IsVisible = false;
                }
            }
            else
            {
                _modeleCourant = null;
                BtnCreerDossier.IsVisible = false;
                BtnAjouterFichiers.IsVisible = false;
                BtnRenommerModele.IsVisible = false;
                BtnOuvrirExplorateur.IsVisible = false;
            }
        }

        /// <summary>
        /// Convertit un chemin renvoyé par le serveur en chemin utilisable depuis Windows.
        /// Le serveur tourne dans Docker sur le NAS : ses chemins commencent par « /data »,
        /// qui correspond au partage réseau \\ds923.file4all.fr\Maquette.
        /// Les chemins déjà en UNC sont renvoyés tels quels.
        /// </summary>
        private static string AdapterCheminPourWindows(string chemin)
        {
            if (string.IsNullOrWhiteSpace(chemin))
                return chemin;

            // Normaliser d'abord : le serveur Linux renvoie « /data/... » et non « \data\... »
            var normalise = chemin.Replace('/', '\\');

            if (normalise.StartsWith(@"\data\", StringComparison.OrdinalIgnoreCase)
                || normalise.Equals(@"\data", StringComparison.OrdinalIgnoreCase))
            {
                return @"\\ds923.file4all.fr\Maquette" + normalise.Substring(5);
            }

            return normalise;
        }

        /// <summary>
        /// Ouvre un dossier du NAS dans l'explorateur Windows (le chemin serveur est adapté au passage).
        /// </summary>
        private void OuvrirDansExplorateur(string? cheminServeur)
        {
            if (string.IsNullOrWhiteSpace(cheminServeur)) return;

            var chemin = AdapterCheminPourWindows(cheminServeur);
            LogDebug($"[DEBUG] Chemin dossier demandé : {chemin}");
#if WINDOWS
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", chemin);
            }
            catch (Exception ex)
            {
                LogDebug($"Erreur ouverture explorateur : {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// Appelle le serveur pour créer l'arborescence Famille > Sujet > Modèle sur le NAS
        /// et enregistrer les entités en base de données.
        /// </summary>
        private async void OnCreerDossierClicked(object sender, EventArgs e)
        {
            if (_httpClient == null) return;

            var requete = new CreerModeleRequete
            {
                NomFamille = FamilleEntry.Text?.Trim() ?? "",
                NomSujet = SujetEntry.Text?.Trim() ?? "",
                NomModele = ModeleEntry.Text?.Trim() ?? ""
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/Metadata/creerModele", requete);

                if (response.IsSuccessStatusCode)
                {
                    var modeleCree = await response.Content.ReadFromJsonAsync<Modele>();
                    if (modeleCree != null)
                    {
                        var resume = new ModeleResume
                        {
                            ModeleID = modeleCree.ModeleID,
                            Description = requete.NomModele,
                            NomFamille = requete.NomFamille,
                            NomSujet = requete.NomSujet,
                            CheminDossier = modeleCree.CheminDossier
                        };
                        _allModeles.Add(resume);
                        _modeleCourant = resume;
                    }

                    await DisplayAlert("Succès",
                        $"Dossier créé :\n{requete.NomFamille} > {requete.NomSujet} > {requete.NomModele}",
                        "OK");

                    // Ouvre le dossier tout juste créé dans l'explorateur Windows
                    OuvrirDansExplorateur(modeleCree?.CheminDossier);

                    RafraichirEtatInterface();
                }
                else
                {
                    var erreur = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Erreur", $"Le serveur a répondu : {erreur}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erreur", "Impossible de créer le dossier : " + ex.Message, "OK");
            }
        }

        /// <summary>
        /// Ouvre un sélecteur de fichiers, puis téléverse les fichiers choisis
        /// dans le dossier du modèle courant via l'API serveur.
        /// </summary>
        private async void OnAjouterFichiersClicked(object sender, EventArgs e)
        {
            if (_httpClient == null || _modeleCourant == null) return;

            try
            {
                var resultats = await FilePicker.Default.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "Sélectionner les fichiers à ajouter au modèle"
                });

                var selection = resultats?.ToList();
                if (selection == null || selection.Count == 0) return;

                var fichiers = selection
                    .OfType<FileResult>()
                    .Where(f => !string.IsNullOrWhiteSpace(f.FullPath) && System.IO.File.Exists(f.FullPath))
                    .Select(f => new FichierACopier(f.FullPath, f.FileName))
                    .ToList();

                if (fichiers.Count == 0)
                {
                    await DisplayAlert("Erreur", "Impossible de lire les fichiers sélectionnés.", "OK");
                    return;
                }

                DefinirEtatCopie(true);
                AfficherProgressionCopie(0, "Préparation...");

                await CopierFichiersVersModeleAsync(
                    _modeleCourant.ModeleID, _modeleCourant.CheminDossier, fichiers);

                LogDebug($"✅ {fichiers.Count} fichier(s) ajouté(s) au modèle « {_modeleCourant.Description} ».");
                await DisplayAlert("Succès",
                    $"{fichiers.Count} fichier(s) ajouté(s) au modèle « {_modeleCourant.Description} ».",
                    "OK");

                // Recharger uniquement la galerie d'images (léger, pas de rechargement complet)
                LoadGalleryForModele(_modeleCourant.Description ?? "");
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Erreur ajout de fichiers : {ex.Message}");
                await DisplayAlert("Erreur", "Impossible d'ajouter les fichiers : " + ex.Message, "OK");
            }
            finally
            {
                DefinirEtatCopie(false);
            }
        }

        // ============================================
        // Rafraîchissement global de l'interface
        // ============================================

        /// <summary>
        /// Met à jour d'un coup les boutons d'action (Windows) et le panneau de défilement
        /// des modèles du sujet courant.
        /// </summary>
        private void RafraichirEtatInterface()
        {
            UpdateWindowsActions();
            UpdateModeleNavigation();
        }

        // ============================================
        // Défilement rapide des modèles d'un sujet
        // ============================================

        /// <summary>
        /// Recalcule la liste des modèles parcourables (ceux du sujet, et de la famille si elle est
        /// renseignée) puis met à jour l'étiquette et la visibilité du panneau de défilement.
        /// </summary>
        private void UpdateModeleNavigation()
        {
            var famille = FamilleEntry.Text?.Trim();
            var sujet = SujetEntry.Text?.Trim();
            var modele = ModeleEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(sujet))
            {
                _modelesNavigation = new List<ModeleResume>();
                _navIndex = -1;
                ModeleNavPanel.IsVisible = false;
                return;
            }

            var query = _allModeles.Where(m =>
                string.Equals(m.NomSujet, sujet, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(famille))
                query = query.Where(m =>
                    string.Equals(m.NomFamille, famille, StringComparison.OrdinalIgnoreCase));

            _modelesNavigation = query
                .Where(m => !string.IsNullOrEmpty(m.Description))
                .OrderBy(m => m.Description)
                .ToList();

            if (_modelesNavigation.Count == 0)
            {
                _navIndex = -1;
                ModeleNavPanel.IsVisible = false;
                return;
            }

            _navIndex = _modelesNavigation.FindIndex(m =>
                string.Equals(m.Description, modele, StringComparison.OrdinalIgnoreCase));

            ModeleNavPanel.IsVisible = true;
            ModeleNavLabel.Text = _navIndex >= 0
                ? $"{_navIndex + 1} / {_modelesNavigation.Count} — {_modelesNavigation[_navIndex].Description}"
                : $"{_modelesNavigation.Count} modèle(s) — ◀ ▶ pour les parcourir";
        }

        /// <summary>
        /// Passe au modèle suivant (delta = +1) ou précédent (delta = -1) du sujet courant,
        /// en bouclant en fin de liste. L'aperçu (images et contenu) se charge automatiquement,
        /// sans avoir à sélectionner le modèle dans la liste déroulante.
        /// </summary>
        private void NaviguerModele(int delta)
        {
            if (_modelesNavigation.Count == 0) return;

            var index = _navIndex < 0
                ? (delta > 0 ? 0 : _modelesNavigation.Count - 1)
                : (_navIndex + delta + _modelesNavigation.Count) % _modelesNavigation.Count;

            var cible = _modelesNavigation[index];

            // Remplissage programmatique : pas de vidage en cascade ni d'ouverture de la liste
            _majProgrammatique = true;
            ModeleEntry.Text = cible.Description;   // déclenche le chargement de l'aperçu
            _majProgrammatique = false;

            _navIndex = index;
            ModeleNavLabel.Text = $"{index + 1} / {_modelesNavigation.Count} — {cible.Description}";
        }

        private void OnModelePrecedentClicked(object sender, EventArgs e) => NaviguerModele(-1);

        private void OnModeleSuivantClicked(object sender, EventArgs e) => NaviguerModele(1);

        // ============================================
        // Raccourcis clavier (Windows)
        // ============================================

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _pageActive = true;
#if WINDOWS
            BrancherClavierWindows();
#endif
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _pageActive = false;
        }

#if WINDOWS
        private bool _clavierBranche;

        /// <summary>
        /// Branche les flèches du clavier sur la navigation :
        /// ◀ ▶ changent de modèle, ▲ ▼ font défiler les images du modèle affiché.
        /// </summary>
        private void BrancherClavierWindows()
        {
            if (_clavierBranche) return;

            var window = Application.Current?.Windows.FirstOrDefault();
            if (window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window fenetre) return;
            if (fenetre.Content is not Microsoft.UI.Xaml.UIElement racine) return;

            racine.PreviewKeyDown += OnClavierWindows;
            _clavierBranche = true;
            LogDebug("⌨ Flèches clavier actives : ◀ ▶ modèle, ▲ ▼ image.");
        }

        private void OnClavierWindows(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (!_pageActive) return;

            // Ne pas voler les flèches à la saisie de texte (déplacement du curseur dans les champs)
            if (e.OriginalSource is Microsoft.UI.Xaml.Controls.TextBox)
                return;

            if (sender is Microsoft.UI.Xaml.FrameworkElement element
                && Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(element.XamlRoot)
                    is Microsoft.UI.Xaml.Controls.TextBox)
                return;

            switch (e.Key)
            {
                case Windows.System.VirtualKey.Left:
                    NaviguerModele(-1);
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Right:
                    NaviguerModele(1);
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Up:
                    OnImagePrecedenteClicked(this, EventArgs.Empty);
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Down:
                    OnImageSuivanteClicked(this, EventArgs.Empty);
                    e.Handled = true;
                    break;
            }
        }
#endif

        // ============================================
        // Téléversement d'un dossier complet (Windows)
        // ============================================

        /// <summary>
        /// Un fichier à copier : son chemin complet sur le poste et son chemin relatif
        /// à la racine du dossier du modèle (pour recréer les sous-dossiers).
        /// </summary>
        private sealed record FichierACopier(string Complet, string Relatif);

        /// <summary>
        /// Copie un dossier déjà nommé (avec tout son contenu) dans Famille &gt; Sujet.
        /// Le nom du dossier choisi devient le nom du modèle.
        /// </summary>
        private async void OnTeleverserDossierClicked(object sender, EventArgs e)
        {
            string? cheminSource = null;
#if WINDOWS
            try
            {
                cheminSource = await ChoisirDossierWindowsAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erreur", "Sélection du dossier impossible : " + ex.Message, "OK");
                return;
            }
#endif
            if (string.IsNullOrWhiteSpace(cheminSource)) return;

            await TeleverserDossierAsync(cheminSource);
        }

#if WINDOWS
        /// <summary>
        /// Ouvre le sélecteur de dossier Windows et retourne le chemin choisi (null si annulé).
        /// </summary>
        private static async Task<string?> ChoisirDossierWindowsAsync()
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            picker.FileTypeFilter.Add("*");

            // Le picker WinUI a besoin du handle de la fenêtre pour s'afficher
            var window = Application.Current?.Windows.FirstOrDefault();
            if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window mauiWindow)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mauiWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }
#endif

        /// <summary>
        /// Crée (ou retrouve) le modèle correspondant au dossier choisi, puis y copie
        /// l'intégralité de son contenu en conservant l'arborescence des sous-dossiers.
        /// </summary>
        private async Task TeleverserDossierAsync(string cheminSource)
        {
            if (_httpClient == null) return;

            var famille = FamilleEntry.Text?.Trim() ?? "";
            var sujet = SujetEntry.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(famille) || string.IsNullOrWhiteSpace(sujet))
            {
                await DisplayAlert("Erreur", "Renseignez d'abord la Famille et le Sujet.", "OK");
                return;
            }

            // Le dossier est déjà nommé : son nom devient le nom du modèle
            var nomModele = new System.IO.DirectoryInfo(cheminSource.TrimEnd('\\', '/')).Name;
            if (string.IsNullOrWhiteSpace(nomModele))
            {
                await DisplayAlert("Erreur", "Impossible de déterminer le nom du dossier choisi.", "OK");
                return;
            }

            // Inventaire du dossier source (fichiers + sous-dossiers)
            List<FichierACopier> fichiers;
            long tailleTotale = 0;
            try
            {
                fichiers = System.IO.Directory
                    .EnumerateFiles(cheminSource, "*", System.IO.SearchOption.AllDirectories)
                    .Select(f => new FichierACopier(
                        f, System.IO.Path.GetRelativePath(cheminSource, f).Replace('\\', '/')))
                    .ToList();

                foreach (var f in fichiers)
                    tailleTotale += new System.IO.FileInfo(f.Complet).Length;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erreur", "Lecture du dossier impossible : " + ex.Message, "OK");
                return;
            }

            if (fichiers.Count == 0)
            {
                await DisplayAlert("Dossier vide", "Le dossier sélectionné ne contient aucun fichier.", "OK");
                return;
            }

            // Ce modèle existe-t-il déjà dans cette Famille > Sujet ?
            var existant = _allModeles.FirstOrDefault(m =>
                string.Equals(m.Description, nomModele, StringComparison.OrdinalIgnoreCase)
                && string.Equals(m.NomSujet, sujet, StringComparison.OrdinalIgnoreCase)
                && string.Equals(m.NomFamille, famille, StringComparison.OrdinalIgnoreCase));

            var tailleMo = tailleTotale / (1024.0 * 1024.0);
            var question = existant == null
                ? $"Copier « {nomModele} »\n({fichiers.Count} fichier(s), {tailleMo:F1} Mo)\n\nvers {famille} > {sujet} ?"
                : $"Le modèle « {nomModele} » existe déjà dans {famille} > {sujet}.\n\n"
                  + $"Fusionner le contenu ({fichiers.Count} fichier(s), {tailleMo:F1} Mo) ?\n"
                  + "Les fichiers portant le même nom seront remplacés.";

            if (!await DisplayAlert("Téléverser un dossier", question, "Copier", "Annuler"))
                return;

            try
            {
                DefinirEtatCopie(true);
                AfficherProgressionCopie(0, "Préparation...");

                // 1. Créer le modèle côté serveur (dossier sur le NAS + entrées en base)
                int modeleId;
                string? cheminDistant;

                if (existant != null)
                {
                    modeleId = existant.ModeleID;
                    cheminDistant = existant.CheminDossier;
                    LogDebug($"📁 Fusion dans le modèle existant « {nomModele} » (ID={modeleId})");
                }
                else
                {
                    var requete = new CreerModeleRequete
                    {
                        NomFamille = famille,
                        NomSujet = sujet,
                        NomModele = nomModele
                    };

                    var reponse = await _httpClient.PostAsJsonAsync("/api/Metadata/creerModele", requete);
                    if (!reponse.IsSuccessStatusCode)
                    {
                        var erreur = await reponse.Content.ReadAsStringAsync();
                        await DisplayAlert("Erreur", $"Création du modèle refusée : {erreur}", "OK");
                        return;
                    }

                    var modeleCree = await reponse.Content.ReadFromJsonAsync<Modele>();
                    if (modeleCree == null)
                    {
                        await DisplayAlert("Erreur", "Réponse inattendue du serveur lors de la création.", "OK");
                        return;
                    }

                    modeleId = modeleCree.ModeleID;
                    cheminDistant = modeleCree.CheminDossier;
                    LogDebug($"📁 Modèle créé : {famille} > {sujet} > {nomModele} (ID={modeleId})");
                }

                // 2. Copier tout le contenu du dossier
                await CopierFichiersVersModeleAsync(modeleId, cheminDistant, fichiers);

                // 3. Rafraîchir les listes et sélectionner le modèle importé
                await RafraichirModelesAsync();
                _majProgrammatique = true;
                ModeleEntry.Text = nomModele;
                _majProgrammatique = false;
                RafraichirEtatInterface();

                LogDebug($"✅ Dossier « {nomModele} » copié ({fichiers.Count} fichier(s), {tailleMo:F1} Mo).");
                await DisplayAlert("Succès",
                    $"« {nomModele} » a été copié dans {famille} > {sujet}.\n"
                    + $"{fichiers.Count} fichier(s), {tailleMo:F1} Mo.",
                    "OK");
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Erreur téléversement : {ex.Message}");
                await DisplayAlert("Erreur", "Le téléversement a échoué : " + ex.Message, "OK");
            }
            finally
            {
                DefinirEtatCopie(false);
            }
        }

        /// <summary>
        /// Copie une liste de fichiers dans le dossier d'un modèle, puis demande au serveur
        /// de réindexer les images. Deux voies possibles :
        /// 1. copie réseau directe vers le partage du NAS (rapide, sans limite de taille) ;
        /// 2. sinon envoi HTTP par lots via l'API (fonctionne depuis n'importe quel poste).
        /// </summary>
        private async Task CopierFichiersVersModeleAsync(
            int modeleId, string? cheminDistant, List<FichierACopier> fichiers)
        {
            long tailleTotale = 0;
            foreach (var f in fichiers)
            {
                try { tailleTotale += new System.IO.FileInfo(f.Complet).Length; }
                catch { /* Taille indisponible : sans importance pour la progression */ }
            }

            var destinationReseau = string.IsNullOrWhiteSpace(cheminDistant)
                ? null
                : AdapterCheminPourWindows(cheminDistant);

            bool copieFaite = false;

            if (!string.IsNullOrWhiteSpace(destinationReseau)
                && System.IO.Directory.Exists(destinationReseau))
            {
                try
                {
                    LogDebug($"📤 Copie réseau directe vers {destinationReseau}");
                    await CopierFichiersReseauAsync(destinationReseau, fichiers);
                    copieFaite = true;
                }
                catch (Exception ex)
                {
                    LogDebug($"⚠ Copie réseau impossible ({ex.Message}) — envoi via l'API à la place.");
                }
            }

            if (!copieFaite)
            {
                LogDebug("📤 Envoi des fichiers via l'API...");
                await TeleverserFichiersHttpAsync(modeleId, fichiers, tailleTotale);
            }

            // Le serveur doit réindexer les images (indispensable après une copie réseau directe)
            AfficherProgressionCopie(1, "Réindexation des images...");
            try
            {
                var reindex = await _httpClient!.PostAsync($"/api/Metadata/reindexerModele/{modeleId}", null);
                if (!reindex.IsSuccessStatusCode)
                    LogDebug($"⚠ Réindexation refusée par le serveur ({(int)reindex.StatusCode}).");
            }
            catch (Exception ex)
            {
                LogDebug($"⚠ Réindexation impossible : {ex.Message}");
            }
        }

        /// <summary>
        /// Copie les fichiers directement sur le partage réseau du NAS, en recréant les
        /// sous-dossiers. Exécutée hors du thread d'interface pour ne pas figer l'application.
        /// </summary>
        private Task CopierFichiersReseauAsync(string destination, List<FichierACopier> fichiers)
        {
            return Task.Run(() =>
            {
                int copies = 0;
                foreach (var fichier in fichiers)
                {
                    var cible = System.IO.Path.Combine(destination, fichier.Relatif.Replace('/', '\\'));
                    var dossierCible = System.IO.Path.GetDirectoryName(cible);
                    if (!string.IsNullOrEmpty(dossierCible))
                        System.IO.Directory.CreateDirectory(dossierCible);

                    System.IO.File.Copy(fichier.Complet, cible, overwrite: true);
                    copies++;

                    if (copies % 5 == 0 || copies == fichiers.Count)
                        AfficherProgressionCopie((double)copies / fichiers.Count,
                            $"Copie {copies}/{fichiers.Count} — {System.IO.Path.GetFileName(fichier.Complet)}");
                }
            });
        }

        /// <summary>
        /// Envoie les fichiers à l'API par lots (≈ 64 Mo ou 40 fichiers par requête) en
        /// transmettant le chemin relatif de chacun, pour que le serveur recrée l'arborescence.
        /// </summary>
        private async Task TeleverserFichiersHttpAsync(
            int modeleId, List<FichierACopier> fichiers, long tailleTotale)
        {
            var client = ObtenirClientUpload()
                ?? throw new InvalidOperationException("Client HTTP non initialisé.");

            const long tailleLotMax = 64L * 1024 * 1024;   // 64 Mo par requête
            const int fichiersParLotMax = 40;

            var lot = new List<FichierACopier>();
            long tailleLot = 0;
            long envoye = 0;

            async Task EnvoyerLotAsync()
            {
                if (lot.Count == 0) return;

                using var contenu = new MultipartFormDataContent();
                var flux = new List<System.IO.Stream>();
                try
                {
                    foreach (var fichier in lot)
                    {
                        var stream = System.IO.File.OpenRead(fichier.Complet);
                        flux.Add(stream);

                        var streamContent = new StreamContent(stream);
                        streamContent.Headers.ContentType =
                            new MediaTypeHeaderValue("application/octet-stream");

                        contenu.Add(streamContent, "fichiers", System.IO.Path.GetFileName(fichier.Complet));
                        contenu.Add(new StringContent(fichier.Relatif, System.Text.Encoding.UTF8),
                                    "cheminsRelatifs");
                    }

                    var reponse = await client.PostAsync($"/api/Metadata/uploadDossier/{modeleId}", contenu);
                    if (!reponse.IsSuccessStatusCode)
                    {
                        var erreur = await reponse.Content.ReadAsStringAsync();
                        throw new Exception($"le serveur a répondu {(int)reponse.StatusCode} : {erreur}");
                    }
                }
                finally
                {
                    foreach (var s in flux) s.Dispose();
                }

                envoye += tailleLot;
                AfficherProgressionCopie(
                    tailleTotale > 0 ? (double)envoye / tailleTotale : 1,
                    $"Envoi {envoye / (1024.0 * 1024.0):F0} Mo / {tailleTotale / (1024.0 * 1024.0):F0} Mo");

                lot.Clear();
                tailleLot = 0;
            }

            foreach (var fichier in fichiers)
            {
                long taille;
                try { taille = new System.IO.FileInfo(fichier.Complet).Length; }
                catch { taille = 0; }

                if (lot.Count > 0 && (lot.Count >= fichiersParLotMax || tailleLot + taille > tailleLotMax))
                    await EnvoyerLotAsync();

                lot.Add(fichier);
                tailleLot += taille;
            }

            await EnvoyerLotAsync();
        }

        /// <summary>
        /// Client HTTP dédié aux envois de fichiers : le timeout de 30 s du client principal
        /// est bien trop court pour de gros fichiers 3D.
        /// </summary>
        private HttpClient? ObtenirClientUpload()
        {
            if (_uploadHttpClient != null) return _uploadHttpClient;
            if (_httpClient?.BaseAddress == null) return null;

            _uploadHttpClient = new HttpClient(new HttpClientHandler { UseProxy = false })
            {
                BaseAddress = _httpClient.BaseAddress,
                Timeout = TimeSpan.FromMinutes(30)
            };
            return _uploadHttpClient;
        }

        /// <summary>
        /// Active ou désactive les boutons pendant une copie et affiche la barre de progression dédiée.
        /// </summary>
        private void DefinirEtatCopie(bool enCours)
        {
            UploadProgressPanel.IsVisible = enCours;
            BtnTeleverserDossier.IsEnabled = !enCours;
            BtnCreerDossier.IsEnabled = !enCours;
            BtnAjouterFichiers.IsEnabled = !enCours;
            BtnRenommerModele.IsEnabled = !enCours;
            BtnActualiserBase.IsEnabled = !enCours;

            if (!enCours)
            {
                UploadProgressBar.Progress = 0;
                UploadPhaseLabel.Text = "";
                UploadPercentLabel.Text = "";
            }
        }

        /// <summary>
        /// Met à jour la barre de progression de la copie (0 → 1).
        /// Peut être appelée depuis un thread de fond.
        /// </summary>
        private void AfficherProgressionCopie(double progression, string phase)
        {
            var valeur = Math.Clamp(progression, 0, 1);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UploadProgressPanel.IsVisible = true;
                UploadProgressBar.Progress = valeur;
                UploadPhaseLabel.Text = phase;
                UploadPercentLabel.Text = $"{(int)(valeur * 100)}%";
            });
        }

        // ============================================
        // Boutons de vidage (croix ✕)
        // ============================================
        private void OnFamilleClearClicked(object sender, EventArgs e)
        {
            FamilleEntry.Text = "";
            FamilleListContainer.IsVisible = false;
            ClearContenuPanels();
            RetourAuxImages();
        }

        private void OnSujetClearClicked(object sender, EventArgs e)
        {
            SujetEntry.Text = "";
            SujetListContainer.IsVisible = false;
            ClearContenuPanels();
            RetourAuxImages();
        }

        private void OnModeleClearClicked(object sender, EventArgs e)
        {
            ModeleEntry.Text = "";
            ModeleListContainer.IsVisible = false;
            ClearContenuPanels();
            RetourAuxImages();
        }

        // ============================================
        // Agrandissement de l'image (overlay plein écran)
        // ============================================
        private void OnImageTapped(object sender, TappedEventArgs e)
        {
            if (ModeleImage.Source != null)
            {
                EnlargedImage.Source = ModeleImage.Source;
                ImageOverlay.IsVisible = true;
            }
        }

        private void OnImageOverlayTapped(object sender, TappedEventArgs e)
        {
            ImageOverlay.IsVisible = false;
        }

        // ============================================
        // Viewer 3D (WebView + Three.js)
        // ============================================
        private async void OnFichier3DSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is not Fichier3D fichier) return;
            Fichiers3DCollectionView.SelectedItem = null;

            if (_httpClient == null || _modeleCourant == null) return;

            await ShowViewer3D(fichier);
        }

        private async Task ShowViewer3D(Fichier3D fichier)
        {
            if (_httpClient == null || _modeleCourant == null) return;

            var ext = System.IO.Path.GetExtension(fichier.Nom).ToLowerInvariant();
            if (ext != ".stl" && ext != ".obj")
            {
                await DisplayAlert("Info",
                    $"L'aperçu 3D n'est disponible que pour les fichiers STL et OBJ.\n(Fichier : {fichier.Nom})",
                    "OK");
                return;
            }

            // Construire l'URL du fichier 3D
            var encodedNom = Uri.EscapeDataString(fichier.Nom);
            var url = $"{_httpClient.BaseAddress}api/Metadata/modele/{_modeleCourant.ModeleID}/fichier3d?nom={encodedNom}";
            if (!string.IsNullOrEmpty(fichier.NomArchive))
                url += $"&archive={Uri.EscapeDataString(fichier.NomArchive)}";

            // Charger le viewer si pas encore fait
            if (!_viewer3DVisible)
            {
                var viewerUrl = $"{_httpClient.BaseAddress}viewer3d.html";
                var tcs = new TaskCompletionSource<bool>();
                void OnNavigated(object? s, WebNavigatedEventArgs args)
                {
                    Viewer3DWebView.Navigated -= OnNavigated;
                    tcs.TrySetResult(true);
                }
                Viewer3DWebView.Navigated += OnNavigated;
                Viewer3DWebView.Source = viewerUrl;

                // Attendre le chargement (timeout 10s)
                await Task.WhenAny(tcs.Task, Task.Delay(10000));
            }

            // Basculer l'affichage
            ModeleImage.IsVisible = false;
            Viewer3DWebView.IsVisible = true;
            ImageNavPanel.IsVisible = false;
            BtnRetourImages.IsVisible = true;
            _viewer3DVisible = true;

            // Appeler le JS pour charger le modèle
            var js = $"window.loadModel('{url.Replace("'", "\\'")}', '{ext}');";
            await Viewer3DWebView.EvaluateJavaScriptAsync(js);
        }

        private void RetourAuxImages()
        {
            if (!_viewer3DVisible) return;
            Viewer3DWebView.IsVisible = false;
            ModeleImage.IsVisible = true;
            ImageNavPanel.IsVisible = true;
            BtnRetourImages.IsVisible = false;
            _viewer3DVisible = false;
        }

        private void OnRetourImagesClicked(object sender, EventArgs e)
        {
            RetourAuxImages();
        }

        // ============================================
        // Renommage du modèle (Windows)
        // ============================================
        private async void OnRenommerModeleClicked(object sender, EventArgs e)
        {
            if (_httpClient == null || _modeleCourant == null) return;

            var nouveauNom = await DisplayPromptAsync(
                "Renommer le modèle",
                "Nouveau nom du dossier :",
                initialValue: _modeleCourant.Description ?? "",
                accept: "Renommer",
                cancel: "Annuler");

            if (string.IsNullOrWhiteSpace(nouveauNom)) return;
            if (nouveauNom.Trim() == _modeleCourant.Description) return;

            try
            {
                var requete = new RenommerModeleRequete { NouveauNom = nouveauNom.Trim() };
                var response = await _httpClient.PutAsJsonAsync(
                    $"/api/Metadata/renommerModele/{_modeleCourant.ModeleID}", requete);

                if (response.IsSuccessStatusCode)
                {
                    _modeleCourant.Description = nouveauNom.Trim();
                    _majProgrammatique = true;
                    ModeleEntry.Text = nouveauNom.Trim();
                    _majProgrammatique = false;

                    UpdateModeles();
                    await DisplayAlert("Succès", $"Modèle renommé en « {nouveauNom.Trim()} ».", "OK");
                }
                else
                {
                    var erreur = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Erreur", $"Le serveur a répondu : {erreur}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erreur", "Impossible de renommer : " + ex.Message, "OK");
            }
        }

        // ============================================
        // Ouvrir le dossier dans l'explorateur (Windows)
        // ============================================
        private void OnOuvrirExplorateurClicked(object sender, EventArgs e)
        {
            if (_modeleCourant == null) return;
            OuvrirDansExplorateur(_modeleCourant.CheminDossier);
        }

        // ============================================
        // Navigation vers la page de configuration
        // ============================================
        private async void OnConfigClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new ConfigPage(_httpClient));
        }

        private async void OnActualiserBaseClicked(object sender, EventArgs e)
        {
            if (_httpClient == null)
            {
                await DisplayAlert("Erreur", "Client HTTP non initialisé.", "OK");
                return;
            }
            try
            {
                BtnActualiserBase.IsEnabled = false;
               LogDebug("🔄 Demande d'actualisation de la base...");
                var response = await _httpClient.PostAsync("/api/Metadata/refreshAll", null);
                if (response.IsSuccessStatusCode)
                {
                   LogDebug("✅ Synchronisation lancée en tâche de fond.");
                   await DisplayAlert("Succès",
                       "Synchronisation lancée en tâche de fond.\nLa barre de progression s'affichera automatiquement.",
                       "OK");
               }
               else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
               {
                   LogDebug("ℹ Un scan est déjà en cours.");
                   await DisplayAlert("Info",
                       "Un scan est déjà en cours. Patientez, la barre de progression affiche l'avancement.",
                       "OK");
                }
                else
                {
                    var erreur = await response.Content.ReadAsStringAsync();
                   LogDebug($"❌ Erreur serveur ({(int)response.StatusCode}) : {erreur}");
                    await DisplayAlert("Erreur", $"Le serveur a répondu : {erreur}", "OK");
                }
            }
            catch (Exception ex)
            {
               LogDebug($"❌ Erreur réseau : {ex.Message}");
                await DisplayAlert("Erreur", "Impossible d'actualiser la base : " + ex.Message, "OK");
            }
            finally
            {
                BtnActualiserBase.IsEnabled = true;
            }
        }
    }
}
