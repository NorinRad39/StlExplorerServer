using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace StlExplorerClient
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if WINDOWS
            // Rien n'empêchait la fenêtre de s'ouvrir plus haute que l'écran : le bas de
            // l'interface (navigation des images, journal de debug) passait alors sous la
            // barre des tâches. On la ramène dans la zone utile de l'écran.
            builder.ConfigureLifecycleEvents(evenements =>
                evenements.AddWindows(windows =>
                    windows.OnWindowCreated(AjusterFenetreALEcran)));
#endif

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

#if WINDOWS
        /// <summary>
        /// Limite la fenêtre à la zone utile de l'écran qui l'accueille (barre des tâches
        /// exclue) et la recentre si elle en débordait.
        /// </summary>
        private static void AjusterFenetreALEcran(Microsoft.UI.Xaml.Window fenetre)
        {
            var appWindow = fenetre.AppWindow;
            var ecran = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                appWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            if (ecran == null) return;

            // Toutes ces valeurs sont en pixels physiques, fenêtre comme écran.
            var zone = ecran.WorkArea;
            var largeur = Math.Min(appWindow.Size.Width, zone.Width);
            var hauteur = Math.Min(appWindow.Size.Height, zone.Height);

            var x = appWindow.Position.X;
            var y = appWindow.Position.Y;
            var deborde = x < zone.X || y < zone.Y
                          || x + largeur > zone.X + zone.Width
                          || y + hauteur > zone.Y + zone.Height;

            if (deborde)
            {
                x = zone.X + (zone.Width - largeur) / 2;
                y = zone.Y + (zone.Height - hauteur) / 2;
            }

            appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, largeur, hauteur));
        }
#endif
    }
}
