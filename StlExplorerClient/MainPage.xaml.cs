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

        public MainPage()
        {
            InitializeComponent();

            // Le panel d'actions (création de dossier, ajout de fichiers) n'est visible que sur Windows
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                WindowsActionsPanel.IsVisible = true;

            LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try 
            {
                var handler = new HttpClientHandler();
#if ANDROID
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                _httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://10.0.2.2:5182") };
#elif WINDOWS
                _httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5182") };
#else
                _httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5182") };
#endif
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
                await DisplayAlert("Erreur", "Impossible de contacter le serveur : " + ex.Message, "OK");
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
        // Galerie d'images
        // ============================================
        private async void LoadGalleryForModele(string nomModele)
        {
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
                        DisplayCurrentImage();
                        return;
                    }
                }
                catch { /* La galerie sera vidée ci-dessous */ }
            }

            _currentImages.Clear();
            _currentImageIndex = -1;
            ModeleImage.Source = null;
            ImageCounterLabel.Text = "";
        }

        private void DisplayCurrentImage()
        {
            if (_currentImageIndex >= 0 && _currentImageIndex < _currentImages.Count)
            {
                var imagePath = _currentImages[_currentImageIndex];
                var encodedPath = Uri.EscapeDataString(imagePath);
                var imageUrl = $"{_httpClient?.BaseAddress}api/Metadata/image?chemin={encodedPath}";
                ModeleImage.Source = ImageSource.FromUri(new Uri(imageUrl));
                ImageCounterLabel.Text = $"{_currentImageIndex + 1} / {_currentImages.Count}";
            }
            else
            {
                ImageCounterLabel.Text = "";
            }
        }

        private void OnImagePrecedenteClicked(object sender, EventArgs e)
        {
            if (_currentImages.Count > 0)
            {
                _currentImageIndex--;
                if (_currentImageIndex < 0) _currentImageIndex = _currentImages.Count - 1; // Boucler
                DisplayCurrentImage();
            }
        }

        private void OnImageSuivanteClicked(object sender, EventArgs e)
        {
            if (_currentImages.Count > 0)
            {
                _currentImageIndex++;
                if (_currentImageIndex >= _currentImages.Count) _currentImageIndex = 0; // Boucler
                DisplayCurrentImage();
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
                    // Modèle existant : proposer d'ajouter des fichiers
                    _modeleCourant = existant;
                    BtnCreerDossier.IsVisible = false;
                    BtnAjouterFichiers.IsVisible = true;
                }
                else
                {
                    // Nouveau modèle : proposer de créer le dossier
                    _modeleCourant = null;
                    BtnCreerDossier.IsVisible = true;
                    BtnAjouterFichiers.IsVisible = false;
                }
            }
            else
            {
                _modeleCourant = null;
                BtnCreerDossier.IsVisible = false;
                BtnAjouterFichiers.IsVisible = false;
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
        }

        private void OnSujetClearClicked(object sender, EventArgs e)
        {
            SujetEntry.Text = "";
            SujetListContainer.IsVisible = false;
        }

        private void OnModeleClearClicked(object sender, EventArgs e)
        {
            ModeleEntry.Text = "";
            ModeleListContainer.IsVisible = false;
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
        // Navigation vers la page de configuration
        // ============================================
        private async void OnConfigClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new ConfigPage(_httpClient));
        }
    }
}
