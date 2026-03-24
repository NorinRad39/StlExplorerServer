using Microsoft.Extensions.DependencyInjection;

namespace StlExplorerClient
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            // Taille fixe de la fenêtre (Windows uniquement, ignoré sur mobile)
            window.Width = 750;
            window.Height = 950;
            window.MinimumWidth = 750;
            window.MinimumHeight = 950;
            window.MaximumWidth = 750;
            window.MaximumHeight = 950;

            return window;
        }
    }
}