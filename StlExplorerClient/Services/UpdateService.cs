using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StlExplorerClient.Services
{
    /// <summary>
    /// Décrit une version publiée pour une plateforme, telle qu'annoncée dans update.json.
    /// </summary>
    public class InfoMiseAJour
    {
        /// <summary>Version affichable, ex. « 1.2.0 ».</summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        /// <summary>Numéro de build entier (versionCode Android), qui doit toujours croître.</summary>
        [JsonPropertyName("build")]
        public int Build { get; set; }

        /// <summary>Chemin du paquet, relatif au dossier de mises à jour du NAS.</summary>
        [JsonPropertyName("fichier")]
        public string Fichier { get; set; } = "";

        /// <summary>Taille du paquet en octets (0 si inconnue).</summary>
        [JsonPropertyName("taille")]
        public long Taille { get; set; }

        /// <summary>Notes de version affichées à l'utilisateur.</summary>
        [JsonPropertyName("notes")]
        public string Notes { get; set; } = "";
    }

    /// <summary>Manifeste complet publié par les scripts de deploy\.</summary>
    public class ManifesteMisesAJour
    {
        [JsonPropertyName("android")]
        public InfoMiseAJour? Android { get; set; }

        [JsonPropertyName("windows")]
        public InfoMiseAJour? Windows { get; set; }
    }

    /// <summary>
    /// Vérifie, télécharge et lance l'installation des mises à jour de l'application.
    /// </summary>
    /// <remarks>
    /// Le manifeste et les paquets sont servis par l'API (voir UpdateController côté serveur),
    /// ce qui évite au téléphone d'avoir à accéder au partage réseau du NAS.
    /// </remarks>
    public static class UpdateService
    {
        /// <summary>Nom de la plateforme utilisé comme clé dans le manifeste.</summary>
        public static string Plateforme =>
            DeviceInfo.Platform == DevicePlatform.WinUI ? "windows" : "android";

        /// <summary>Version installée, ex. « 1.2.0 ».</summary>
        public static string VersionInstallee => AppInfo.Current.VersionString;

        /// <summary>Numéro de build installé (versionCode sur Android).</summary>
        public static int BuildInstalle =>
            int.TryParse(AppInfo.Current.BuildString, out var build) ? build : 0;

        /// <summary>
        /// Interroge le serveur et renvoie la mise à jour disponible, ou null si l'application
        /// est déjà à jour (ou si le serveur ne publie rien pour cette plateforme).
        /// </summary>
        public static async Task<InfoMiseAJour?> VerifierAsync(HttpClient client, CancellationToken token = default)
        {
            var manifeste = await client.GetFromJsonAsync<ManifesteMisesAJour>("/api/Update/manifest", token);

            var publie = DeviceInfo.Platform == DevicePlatform.WinUI
                ? manifeste?.Windows
                : manifeste?.Android;

            if (publie == null || string.IsNullOrWhiteSpace(publie.Fichier))
                return null;

            return EstPlusRecente(publie) ? publie : null;
        }

        /// <summary>
        /// Compare la version publiée à celle installée : le numéro de build fait foi quand il
        /// est renseigné (c'est lui qui pilote la mise à jour côté Android), sinon on compare
        /// les numéros de version.
        /// </summary>
        private static bool EstPlusRecente(InfoMiseAJour publie)
        {
            if (publie.Build > 0 && BuildInstalle > 0)
                return publie.Build > BuildInstalle;

            if (Version.TryParse(publie.Version, out var versionPubliee)
                && Version.TryParse(VersionInstallee, out var versionInstallee))
                return versionPubliee > versionInstallee;

            return !string.Equals(publie.Version, VersionInstallee, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Télécharge le paquet de mise à jour dans le cache de l'application et renvoie son chemin.
        /// </summary>
        /// <param name="progression">Reçoit l'avancement entre 0 et 1 (si la taille est connue).</param>
        public static async Task<string> TelechargerAsync(
            HttpClient client,
            InfoMiseAJour info,
            IProgress<double>? progression = null,
            CancellationToken token = default)
        {
            var nomFichier = System.IO.Path.GetFileName(info.Fichier.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(nomFichier))
                nomFichier = DeviceInfo.Platform == DevicePlatform.WinUI ? "setup.exe" : "update.apk";

            var destination = System.IO.Path.Combine(FileSystem.CacheDirectory, nomFichier);

            // Un téléchargement précédent interrompu ne doit pas être réutilisé tel quel.
            if (System.IO.File.Exists(destination))
                System.IO.File.Delete(destination);

            using var reponse = await client.GetAsync(
                $"/api/Update/fichier/{Plateforme}", HttpCompletionOption.ResponseHeadersRead, token);
            reponse.EnsureSuccessStatusCode();

            var taille = reponse.Content.Headers.ContentLength ?? info.Taille;

            using (var source = await reponse.Content.ReadAsStreamAsync(token))
            using (var fichier = System.IO.File.Create(destination))
            {
                var tampon = new byte[81920];
                long recu = 0;
                int lus;
                while ((lus = await source.ReadAsync(tampon, token)) > 0)
                {
                    await fichier.WriteAsync(tampon.AsMemory(0, lus), token);
                    recu += lus;
                    if (taille > 0)
                        progression?.Report(Math.Clamp((double)recu / taille, 0, 1));
                }
            }

            return destination;
        }

        /// <summary>
        /// Lance l'installation du paquet téléchargé. Sur Android, ouvre l'installateur système ;
        /// sur Windows, exécute le setup puis ferme l'application pour libérer les fichiers.
        /// </summary>
        public static void Installer(string cheminPaquet)
        {
#if ANDROID
            var fichier = new Java.IO.File(cheminPaquet);
            var contexte = Android.App.Application.Context;

            // Android exige une URI content:// (via FileProvider) pour installer un APK.
            var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                contexte, $"{contexte.PackageName}.updateprovider", fichier);

            var intent = new Android.Content.Intent(Android.Content.Intent.ActionView);
            intent.SetDataAndType(uri, "application/vnd.android.package-archive");
            intent.SetFlags(Android.Content.ActivityFlags.NewTask
                          | Android.Content.ActivityFlags.GrantReadUriPermission);
            contexte.StartActivity(intent);
#elif WINDOWS
            // /SILENT : l'installateur Inno Setup se déroule sans questions, avec sa barre de progression.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = cheminPaquet,
                Arguments = "/SILENT /NOCANCEL",
                UseShellExecute = true
            });

            // Le setup ne peut pas remplacer les fichiers tant que l'application tourne.
            Application.Current?.Quit();
#else
            throw new PlatformNotSupportedException(
                "L'installation automatique n'est disponible que sur Android et Windows.");
#endif
        }
    }
}
