namespace StlExplorerClient
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        // ============================================
        // Logique pour le champ "Famille"
        // ============================================
        private void OnFamilleTextChanged(object sender, TextChangedEventArgs e)
        {
            // TODO : Filtrer la liste 'FamilleCollectionView.ItemsSource' en fonction de e.NewTextValue
            FamilleListContainer.IsVisible = !string.IsNullOrWhiteSpace(e.NewTextValue);
        }

        private void OnFamilleDropdownClicked(object sender, EventArgs e)
        {
            // Basculer l'affichage de la liste complète
            FamilleListContainer.IsVisible = !FamilleListContainer.IsVisible;
        }

        private void OnFamilleSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is string selection)
            {
                FamilleEntry.Text = selection;
                FamilleListContainer.IsVisible = false;
                FamilleCollectionView.SelectedItem = null; // Réinitialise la sélection
            }
        }

        // ============================================
        // Logique pour le champ "Sujet"
        // ============================================
        private void OnSujetTextChanged(object sender, TextChangedEventArgs e)
        {
            SujetListContainer.IsVisible = !string.IsNullOrWhiteSpace(e.NewTextValue);
        }

        private void OnSujetDropdownClicked(object sender, EventArgs e)
        {
            SujetListContainer.IsVisible = !SujetListContainer.IsVisible;
        }

        private void OnSujetSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is string selection)
            {
                SujetEntry.Text = selection;
                SujetListContainer.IsVisible = false;
                SujetCollectionView.SelectedItem = null;
            }
        }

        // ============================================
        // Logique pour le champ "Modèle"
        // ============================================
        private void OnModeleTextChanged(object sender, TextChangedEventArgs e)
        {
            ModeleListContainer.IsVisible = !string.IsNullOrWhiteSpace(e.NewTextValue);
        }

        private void OnModeleDropdownClicked(object sender, EventArgs e)
        {
            ModeleListContainer.IsVisible = !ModeleListContainer.IsVisible;
        }

        private void OnModeleSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is string selection)
            {
                ModeleEntry.Text = selection;
                ModeleListContainer.IsVisible = false;
                ModeleCollectionView.SelectedItem = null;
            }
        }
    }
}
