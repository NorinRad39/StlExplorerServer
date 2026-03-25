using System.Net.Http.Json;

namespace StlExplorerClient
{
    public partial class ConfigPage : ContentPage
    {
        public const string ServerUrlKey = "ServerUrl";
        public const string DefaultServerUrl = "http://localhost:5182";

        private readonly HttpClient? _httpClient;
        private List<string> _dossiers = new();

        public ConfigPage(HttpClient? httpClient)
        {
            InitializeComponent();
            _httpClient = httpClient;

            // Charger l'URL actuelle du serveur
            ServerUrlEntry.Text = Preferences.Get(ServerUrlKey, _httpClient?.BaseAddress?.ToString() ?? DefaultServerUrl);

            ChargerConfiguration();
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

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                UrlStatusLabel.TextColor = Colors.Red;
                UrlStatusLabel.Text = "URL invalide. Utilisez le format http://ip:port";
                return;
            }

            Preferences.Set(ServerUrlKey, url);
            UrlStatusLabel.TextColor = Color.FromArgb("#4CAF50");
            UrlStatusLabel.Text = $"URL enregistrée : {url}";

            await DisplayAlert("Redémarrage requis",
                "L'adresse du serveur a été enregistrée. Veuillez relancer l'application pour appliquer le changement.",
                "OK");
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
                StatusLabel.Text = "Scan en cours...";

                var response = await _httpClient.PostAsync("/api/Metadata/scanAll", null);

                if (response.IsSuccessStatusCode)
                {
                    StatusLabel.TextColor = Color.FromArgb("#4CAF50");
                    StatusLabel.Text = "Scan termine avec succes.";
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
