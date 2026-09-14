using System.Net.Http.Json;

namespace StlExplorerClient
{
    public partial class ConfigPage : ContentPage
    {
        public const string ServerUrlKey = "ServerUrl";

        /// <summary>
        /// Adresse utilisée tant que l'utilisateur n'en a pas saisi d'autre.
        /// Le nom de domaine passe par le reverse proxy du NAS (HTTPS) et fonctionne
        /// aussi bien depuis le réseau local que depuis l'extérieur, contrairement à
        /// une adresse IP privée.
        /// </summary>
        public const string DefaultServerUrl = "https://stl.file4all.fr";

        private readonly HttpClient? _httpClient;
        private List<string> _dossiers = new();

        public ConfigPage(HttpClient? httpClient)
        {
            InitializeComponent();
            _httpClient = httpClient;

            // Charger l'URL actuelle du serveur
            ServerUrlEntry.Text = Preferences.Get(ServerUrlKey, _httpClient?.BaseAddress?.ToString() ?? DefaultServerUrl);

            // Le bouton Parcourir n'est disponible que sur Windows
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                BtnParcourir.IsVisible = true;

            // Même source que la vérification de mise à jour : AppInfo est trompeur
            // sur Windows non empaqueté (il renvoie 1.0.0.1 quelle que soit la build).
            VersionLabel.Text = $"Version installée : {StlExplorerClient.Services.UpdateService.VersionInstallee}";

            ChargerConfiguration();
        }

        /// <summary>
        /// Vérifie manuellement la présence d'une nouvelle version publiée sur le NAS.
        /// Le déroulé complet est autonome (UpdateService) : il ne dépend plus de la pile
        /// de navigation, qui n'expose pas MainPage de la même façon sur toutes les plateformes.
        /// </summary>
        private async void OnVerifierMajClicked(object sender, EventArgs e)
        {
            if (_httpClient == null)
            {
                await DisplayAlert("Mise à jour", "Client HTTP non initialisé.", "OK");
                return;
            }

            try
            {
                BtnVerifierMaj.IsEnabled = false;
                StatusLabel.TextColor = Color.FromArgb("#E0E0E0");
                StatusLabel.Text = "Vérification des mises à jour...";

                var progression = new Progress<double>(p =>
                    StatusLabel.Text = $"Téléchargement de la mise à jour... {(int)(p * 100)} %");

                await StlExplorerClient.Services.UpdateService.VerifierEtProposerAsync(
                    this, _httpClient, silencieux: false,
                    journal: m => StatusLabel.Text = m,
                    progression: progression);
            }
            finally
            {
                BtnVerifierMaj.IsEnabled = true;
            }
        }

        private async void OnAppliquerUrlClicked(object sender, EventArgs e)
        {
            var url = ServerUrlEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                UrlStatusLabel.TextColor = Colors.Red;
                UrlStatusLabel.Text = "L'URL ne peut pas être vide.";
                return;
            }

            // S'assurer que l'URL se termine sans slash pour la cohérence
            url = url.TrimEnd('/');

            if (!Uri.TryCreate(url, UriKind.Absolute, out var adresse))
            {
                UrlStatusLabel.TextColor = Colors.Red;
                UrlStatusLabel.Text = "URL invalide. Utilisez le format http://ip:port";
                return;
            }

            // Le trafic en HTTP circule en clair : acceptable sur le réseau local, mais
            // autant signaler qu'une adresse HTTPS existe (le reverse proxy du NAS).
            if (adresse.Scheme == Uri.UriSchemeHttp)
            {
                var continuer = await DisplayAlert("Connexion non chiffrée",
                    $"« {url} » utilise HTTP : les échanges avec le serveur circuleront en clair.\n\n"
                    + $"L'adresse sécurisée est {DefaultServerUrl}.\n\nUtiliser quand même HTTP ?",
                    "Utiliser HTTP", "Annuler");

                if (!continuer)
                {
                    UrlStatusLabel.TextColor = Color.FromArgb("#FFB74D");
                    UrlStatusLabel.Text = "Enregistrement annulé.";
                    return;
                }
            }

            Preferences.Set(ServerUrlKey, url);
            UrlStatusLabel.TextColor = Color.FromArgb("#4CAF50");
            UrlStatusLabel.Text = $"URL enregistrée : {url}";

            await DisplayAlert("Redémarrage requis",
                "L'adresse du serveur a été enregistrée. Veuillez relancer l'application pour appliquer le changement.",
                "OK");
        }

        private async void OnParcourirDossierClicked(object sender, EventArgs e)
        {
#if WINDOWS
            try
            {
                var picker = new Windows.Storage.Pickers.FolderPicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
                picker.FileTypeFilter.Add("*");

                // Récupérer le handle de la fenêtre WinUI pour initialiser le picker
                var window = Application.Current?.Windows.FirstOrDefault();
                if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window mauiWindow)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mauiWindow);
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                }

                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    NouveauDossierEntry.Text = folder.Path;
                }
            }
            catch (Exception ex)
            {
                StatusLabel.TextColor = Colors.Red;
                StatusLabel.Text = "Erreur lors de la sélection : " + ex.Message;
            }
#endif
        }

        private async void ChargerConfiguration()
        {
            if (_httpClient == null) return;

            try
            {
                var dossiers = await _httpClient.GetFromJsonAsync<List<string>>(
                    "/api/Metadata/configuration/rootDirectories");
                if (dossiers != null)
                {
                    _dossiers = dossiers;
                    RafraichirListe();
                }
            }
            catch (Exception ex)
            {
                StatusLabel.TextColor = Colors.Red;
                StatusLabel.Text = "Erreur de chargement : " + ex.Message;
            }
        }

        private void RafraichirListe()
        {
            DirectoriesCollectionView.ItemsSource = null;
            DirectoriesCollectionView.ItemsSource = _dossiers;
        }

        private void OnAjouterDossierClicked(object sender, EventArgs e)
        {
            var chemin = NouveauDossierEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(chemin)) return;

            if (!_dossiers.Contains(chemin))
            {
                _dossiers.Add(chemin);
                RafraichirListe();
                NouveauDossierEntry.Text = "";
                StatusLabel.Text = "";
            }
        }

        private void OnSupprimerDossierClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string chemin)
            {
                _dossiers.Remove(chemin);
                RafraichirListe();
                StatusLabel.Text = "";
            }
        }

        private async void OnEnregistrerClicked(object sender, EventArgs e)
        {
            if (_httpClient == null) return;

            try
            {
                BtnEnregistrer.IsEnabled = false;
                var response = await _httpClient.PutAsJsonAsync(
                    "/api/Metadata/configuration/rootDirectories", _dossiers.ToArray());

                if (response.IsSuccessStatusCode)
                {
                    StatusLabel.TextColor = Color.FromArgb("#4CAF50");
                    StatusLabel.Text = "Configuration enregistree avec succes.";
                }
                else
                {
                    var erreur = await response.Content.ReadAsStringAsync();
                    StatusLabel.TextColor = Colors.Red;
                    StatusLabel.Text = "Erreur serveur : " + erreur;
                }
            }
            catch (Exception ex)
            {
                StatusLabel.TextColor = Colors.Red;
                StatusLabel.Text = "Erreur : " + ex.Message;
            }
            finally
            {
                BtnEnregistrer.IsEnabled = true;
            }
        }

        private async void OnRelancerScanClicked(object sender, EventArgs e)
        {
            if (_httpClient == null) return;

            try
            {
                BtnRelancerScan.IsEnabled = false;
                StatusLabel.TextColor = Color.FromArgb("#E0E0E0");
                StatusLabel.Text = "Synchronisation intelligente en cours...";

                var response = await _httpClient.PostAsync("/api/Metadata/sync-intelligent", null);

                if (response.IsSuccessStatusCode)
                {
                    StatusLabel.TextColor = Color.FromArgb("#4CAF50");
                    StatusLabel.Text = "Synchronisation intelligente lancée en tâche de fond.";
                }
               else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
               {
                   StatusLabel.TextColor = Color.FromArgb("#FFB74D");
                   StatusLabel.Text = "ℹ Un scan est déjà en cours. Patientez...";
               }
                else
                {
                    var erreur = await response.Content.ReadAsStringAsync();
                    StatusLabel.TextColor = Colors.Red;
                    StatusLabel.Text = "Erreur : " + erreur;
                }
            }
            catch (Exception ex)
            {
                StatusLabel.TextColor = Colors.Red;
                StatusLabel.Text = "Erreur : " + ex.Message;
            }
            finally
            {
                BtnRelancerScan.IsEnabled = true;
            }
        }
    }
}
