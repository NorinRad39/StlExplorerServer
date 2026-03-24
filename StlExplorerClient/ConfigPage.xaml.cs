using System.Net.Http.Json;

namespace StlExplorerClient
{
    public partial class ConfigPage : ContentPage
    {
        private readonly HttpClient? _httpClient;
        private List<string> _dossiers = new();

        public ConfigPage(HttpClient? httpClient)
        {
            InitializeComponent();
            _httpClient = httpClient;
            ChargerConfiguration();
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
