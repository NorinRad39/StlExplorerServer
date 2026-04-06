using System.Net.Http.Json;
using System.Net.Http.Headers;
using ClassLibStlExploServ;

namespace StlExplorerClient
{
    public partial class MainPage : ContentPage
    {
        private List<ModeleResume> _allModeles = new();
        private List<string> _currentImages = new();
        private int _currentImageIndex = -1;
        private HttpClient? _httpClient;
        private ModeleResume? _modeleCourant;
        private bool _suppressChildClear;
        private List<Fichier3D> _currentFichiers3D = new();
        private bool _viewer3DVisible;

        private bool _dataLoaded;

        public MainPage()
        {
            InitializeComponent();

            // Le panel d'actions (création de dossier, ajout de fichiers) n'est visible que sur Windows
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                WindowsActionsPanel.IsVisible = true;

            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object? sender, EventArgs e)
        {
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
                var handler = new HttpClientHandler();

                // Lire l'URL du serveur depuis les préférences (configurable dans la page Config)
                var defaultUrl =
#if ANDROID
                    "http://10.0.2.2:5182";
#else
                    "http://localhost:5182";
#endif
                var serverUrl = Preferences.Get(ConfigPage.ServerUrlKey, defaultUrl).TrimEnd('/');

#if ANDROID
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#endif
                _httpClient = new HttpClient(handler) { BaseAddress = new Uri(serverUrl + "/") };
                // Endpoint léger : projection SQL sans les CheminsImages
                var response = await _httpClient.GetFromJsonAsync<List<ModeleResume>>("/api/Metadata/modelesResume");
                if (response != null)
                {
                    _allModeles = response;
                    UpdateFamilles();
                    UpdateSujets();
                    UpdateModeles();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    await DisplayAlert("Erreur", "Impossible de contacter le serveur : " + ex.Message, "OK");
                }
                catch
                {
                    // WinUI peut échouer si la page n'est pas encore dans l'arbre visuel
                    System.Diagnostics.Debug.WriteLine($"Erreur serveur : {ex.Message}");
                }
            }
        }

        private void ClearChildEntries(params Entry[] entries)
        {
            if (_suppressChildClear) return;
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
            FamilleListContainer.IsVisible = !string.IsNullOrWhiteSpace(e.NewTextValue);
            ClearChildEntries(SujetEntry, ModeleEntry);
            UpdateSujets();
            UpdateModeles();
            UpdateWindowsActions();
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
                UpdateWindowsActions();
            }
        }

        // ============================================
        // Logique pour le champ "Sujet"
        // ============================================
        private void OnSujetTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSujets(e.NewTextValue);
            SujetListContainer.IsVisible = !string.IsNullOrWhiteSpace(e.NewTextValue);
            ClearChildEntries(ModeleEntry);
            UpdateModeles();
            UpdateWindowsActions();
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
                SujetEntry.Text = selection;
                SujetListContainer.IsVisible = false;
                SujetCollectionView.SelectedItem = null;

                // Déduire la famille si elle est vide (sans déclencher le vidage en cascade)
                _suppressChildClear = true;
                var premierModele = _allModeles.FirstOrDefault(m => m.NomSujet == selection);
                if (premierModele != null && string.IsNullOrEmpty(FamilleEntry.Text))
                {
                    FamilleEntry.Text = premierModele.NomFamille;
                }
                _suppressChildClear = false;
                UpdateModeles();
                ModeleListContainer.IsVisible = true;
                UpdateWindowsActions();
            }
        }

        // ============================================
        // Logique pour le champ "Modèle"
        // ============================================
        private void OnModeleTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateModeles(e.NewTextValue);
            ModeleListContainer.IsVisible = !string.IsNullOrWhiteSpace(e.NewTextValue);
            LoadGalleryForModele(e.NewTextValue);
            UpdateWindowsActions();
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
                ModeleEntry.Text = selection;
                ModeleListContainer.IsVisible = false;
                ModeleCollectionView.SelectedItem = null;

                // Déduire Famille et Sujet si vides (sans déclencher le vidage en cascade)
                _suppressChildClear = true;
                var modele = _allModeles.FirstOrDefault(m => m.Description == selection);
                if (modele != null)
                {
                    if (string.IsNullOrEmpty(SujetEntry.Text)) SujetEntry.Text = modele.NomSujet;
                    if (string.IsNullOrEmpty(FamilleEntry.Text)) FamilleEntry.Text = modele.NomFamille;
                }
                _suppressChildClear = false;

                LoadGalleryForModele(selection);
                UpdateWindowsActions();
            }
        }

        // ============================================
        // Galerie d'images et contenu du modèle
        // ============================================
        private async void LoadGalleryForModele(string nomModele)
        {
            RetourAuxImages();

            var modele = _allModeles.FirstOrDefault(m => m.Description == nomModele);
            if (modele != null && _httpClient != null)
            {
                try
                {
                    var images = await _httpClient.GetFromJsonAsync<List<string>>(
                        $"/api/Metadata/modele/{modele.ModeleID}/images");
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
                catch { ClearGallery(); }

                LoadContenuForModele(modele.ModeleID);
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

        private async void LoadContenuForModele(int modeleId)
        {
            if (_httpClient == null) { ClearContenuPanels(); return; }

            try
            {
                var contenu = await _httpClient.GetFromJsonAsync<ContenuModele>(
                    $"/api/Metadata/modele/{modeleId}/contenu");
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
            if (_currentImageIndex >= 0 && _currentImageIndex < _currentImages.Count && _httpClient != null)
            {
                var imagePath = _currentImages[_currentImageIndex];
                var encodedPath = Uri.EscapeDataString(imagePath);
                try
                {
                    var response = await _httpClient.GetAsync($"api/Metadata/image?chemin={encodedPath}");
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
                            NomSujet = requete.NomSujet
                        };
                        _allModeles.Add(resume);
                        _modeleCourant = resume;
                    }

                    await DisplayAlert("Succès",
                        $"Dossier créé :\n{requete.NomFamille} > {requete.NomSujet} > {requete.NomModele}",
                        "OK");

                    UpdateWindowsActions();
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

                var fichiers = resultats?.ToList();
                if (fichiers == null || fichiers.Count == 0) return;

                using var contenu = new MultipartFormDataContent();
                foreach (var fichier in fichiers)
                {
                    var stream = await fichier.OpenReadAsync();
                    var streamContent = new StreamContent(stream);
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(
                        fichier.ContentType ?? "application/octet-stream");
                    contenu.Add(streamContent, "fichiers", fichier.FileName);
                }

                var response = await _httpClient.PostAsync(
                    $"/api/Metadata/uploadFichiers/{_modeleCourant.ModeleID}", contenu);

                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Succès",
                        $"{fichiers.Count} fichier(s) ajouté(s) au modèle « {_modeleCourant.Description} ».",
                        "OK");

                    // Recharger uniquement la galerie d'images (léger, pas de rechargement complet)
                    LoadGalleryForModele(_modeleCourant.Description ?? "");
                }
                else
                {
                    var erreur = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Erreur", $"Le serveur a répondu : {erreur}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erreur", "Impossible d'ajouter les fichiers : " + ex.Message, "OK");
            }
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
                    _suppressChildClear = true;
                    ModeleEntry.Text = nouveauNom.Trim();
                    _suppressChildClear = false;

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
#if WINDOWS
            if (_modeleCourant == null || string.IsNullOrWhiteSpace(_modeleCourant.CheminDossier)) return;
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", _modeleCourant.CheminDossier);
            }
            catch { /* Ignorer si l'ouverture échoue */ }
#endif
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
                var response = await _httpClient.PostAsync("/api/Metadata/refreshAll", null);
                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Succès", "La base de données a été actualisée.", "OK");
                    await LoadDataAsync(); // Recharge les données après actualisation
                }
                else
                {
                    var erreur = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Erreur", $"Le serveur a répondu : {erreur}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erreur", "Impossible d'actualiser la base : " + ex.Message, "OK");
            }
            finally
            {
                BtnActualiserBase.IsEnabled = true;
            }
        }
    }
}
