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

#if WINDOWS
            // Taille par défaut généreuse et fenêtre librement redimensionnable
            window.Width = 800;
            window.MinimumWidth = 550;
            window.MinimumHeight = 600;

            // Adapter la hauteur à l'écran disponible
            var displayInfo = DeviceDisplay.Current.MainDisplayInfo;
            var screenHeight = displayInfo.Height / displayInfo.Density;
            window.Height = Math.Min(screenHeight - 80, 1400);
#endif

            return window;
        }
    }
}