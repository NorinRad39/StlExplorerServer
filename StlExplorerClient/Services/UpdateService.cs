using System.Net.Http.Json;
using System.Reflection;
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
        public static string VersionInstallee => _versionInstallee.Value;

        private static readonly Lazy<string> _versionInstallee = new(LireVersionApplication);

        /// <summary>
        /// Lit la version dans les attributs de l'assembly de l'application.
        /// </summary>
        /// <remarks>
        /// On n'utilise pas AppInfo ici : sur Windows en mode non empaqueté, il renvoie
        /// une version sans rapport avec la build (« 1.0.0.1 », build « 1 »), qui ne
        /// correspond ni à l'AssemblyVersion, ni au FileVersion, ni au manifeste de
        /// package. L'application se croyait donc obsolète en permanence.
        /// Les attributs d'assembly, eux, sont renseignés depuis ApplicationDisplayVersion
        /// sur toutes les plateformes.
        /// </remarks>
        private static string LireVersionApplication()
        {
            var assembly = typeof(UpdateService).Assembly;

            var informationnelle = assembly
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            var version = NormaliserVersion(informationnelle);
            if (version != "0.0.0") return version;

            version = NormaliserVersion(assembly.GetName().Version?.ToString());
            if (version != "0.0.0") return version;

            return NormaliserVersion(AppInfo.Current.VersionString);
        }

        /// <summary>
        /// Numéro de build installé : le versionCode Android, seule plateforme où il est fiable.
        /// </summary>
        /// <remarks>
        /// Sur Windows, AppInfo renvoie « 1 » quelle que soit la version publiée. Renvoyer 0
        /// désactive la comparaison par build et laisse celle des numéros de version décider.
        /// </remarks>
        public static int BuildInstalle =>
            DeviceInfo.Platform == DevicePlatform.Android
            && int.TryParse(AppInfo.Current.BuildString, out var build)
                ? build
                : 0;

        /// <summary>
        /// Ramène un numéro de version à ses seuls chiffres, ex. « 1.1.3 ».
        /// </summary>
        /// <remarks>
        /// Indispensable sur Windows en mode non empaqueté : AppInfo y renvoie la version
        /// informationnelle de l'assembly, suffixée du hash du commit
        /// (« 1.1.3+b5244f81ad... »). Comparée telle quelle au « 1.1.3 » du manifeste,
        /// elle ne correspondait jamais — l'application proposait donc indéfiniment
        /// une mise à jour déjà installée.
        /// </remarks>
        private static string NormaliserVersion(string? version)
        {
            if (string.IsNullOrWhiteSpace(version)) return "0.0.0";

            // Retirer un eventuel suffixe semver : « 1.1.4+b5244f8 » ou « 1.1.4-preview ».
            var coupe = version.Split('+', '-')[0].Trim();
            if (coupe.Length == 0) return "0.0.0";

            // Ne garder que Majeur.Mineur.Correctif : Windows ajoute une 4e composante
            // (« 1.1.4.0 »), que le manifeste n'a pas. Sans cette troncature, la
            // comparaison verrait deux versions differentes pour une même livraison.
            var parties = coupe.Split('.');
            return parties.Length > 3 ? string.Join('.', parties[0], parties[1], parties[2]) : coupe;
        }

        /// <summary>
        /// Interroge le serveur et renvoie la mise à jour disponible, ou null si l'application
        /// est déjà à jour (ou si le serveur ne publie rien pour cette plateforme).
        /// </summary>
        public static async Task<InfoMiseAJour?> VerifierAsync(HttpClient client, CancellationToken token = default)
        {
            using var reponse = await client.GetAsync("/api/Update/manifest", token);

            // 404 = aucun manifeste publié sur le serveur : ce n'est pas une erreur,
            // simplement rien à proposer. Inutile d'alarmer l'utilisateur avec un
            // message HTTP brut.
            if (reponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            reponse.EnsureSuccessStatusCode();
            var manifeste = await reponse.Content.ReadFromJsonAsync<ManifesteMisesAJour>(cancellationToken: token);

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

            var versionPubliee = NormaliserVersion(publie.Version);

            if (Version.TryParse(versionPubliee, out var publiee)
                && Version.TryParse(VersionInstallee, out var installee))
                return publiee > installee;

            // Versions non comparables : ne rien proposer si elles sont identiques,
            // pour ne jamais boucler sur une mise à jour déjà installée.
            return !string.Equals(versionPubliee, VersionInstallee, StringComparison.OrdinalIgnoreCase);
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

            // Client dédié : celui de l'application expire au bout de 30 s, ce qui suffit
            // aux appels d'API mais pas au transfert d'un paquet de plusieurs dizaines de Mo.
            using var clientTelechargement = new HttpClient(new HttpClientHandler { UseProxy = false })
            {
                BaseAddress = client.BaseAddress,
                Timeout = TimeSpan.FromMinutes(30)
            };

            using var reponse = await clientTelechargement.GetAsync(
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
        /// Déroulé complet d'une mise à jour, utilisable depuis n'importe quelle page :
        /// vérification, proposition, téléchargement puis installation.
        /// </summary>
        /// <param name="page">Page qui affiche les boîtes de dialogue.</param>
        /// <param name="silencieux">
        /// Vrai lors de la vérification automatique au démarrage : on ne dérange
        /// l'utilisateur que si une mise à jour existe réellement.
        /// </param>
        /// <param name="journal">Reçoit les messages de suivi (journal de debug).</param>
        /// <param name="progression">Reçoit l'avancement du téléchargement, entre 0 et 1.</param>
        public static async Task VerifierEtProposerAsync(
            Page page,
            HttpClient client,
            bool silencieux,
            Action<string>? journal = null,
            IProgress<double>? progression = null)
        {
            void Tracer(string message) => journal?.Invoke(message);

            InfoMiseAJour? maj;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                maj = await VerifierAsync(client, cts.Token);
            }
            catch (Exception ex)
            {
                Tracer($"⚠ Vérification des mises à jour impossible : {ex.Message}");
                if (!silencieux)
                    await page.DisplayAlert("Mise à jour",
                        "Impossible de contacter le serveur de mises à jour :\n" + ex.Message, "OK");
                return;
            }

            if (maj == null)
            {
                Tracer($"✅ Application à jour (v{VersionInstallee}).");
                if (!silencieux)
                    await page.DisplayAlert("Mise à jour",
                        $"Aucune mise à jour disponible.\nVersion installée : {VersionInstallee}.", "OK");
                return;
            }

            Tracer($"⬆ Mise à jour disponible : v{maj.Version} (installée : v{VersionInstallee})");

            var tailleMo = maj.Taille > 0 ? $"\nTaille : {maj.Taille / (1024.0 * 1024.0):F1} Mo" : "";
            var notes = string.IsNullOrWhiteSpace(maj.Notes) ? "" : $"\n\n{maj.Notes}";
            var question = $"Version {maj.Version} disponible "
                         + $"(vous avez la {VersionInstallee}).{tailleMo}{notes}";

            if (!await page.DisplayAlert("Mise à jour disponible", question, "Installer", "Plus tard"))
                return;

            // La veille de l'écran coupe le téléchargement en cours (le système suspend
            // l'application et la connexion), ce qui laissait un paquet tronqué et une
            // mise à jour en échec. On garde l'écran allumé le temps du transfert.
            var veilleBloquee = false;
            try
            {
                try
                {
                    DeviceDisplay.Current.KeepScreenOn = true;
                    veilleBloquee = true;
                }
                catch (Exception ex)
                {
                    Tracer($"⚠ Impossible de bloquer la mise en veille : {ex.Message}");
                }

                Tracer($"📦 Téléchargement de la version {maj.Version}...");
                var paquet = await TelechargerAsync(client, maj, progression);
                Tracer($"📦 Téléchargé : {paquet}");
                Installer(paquet);
            }
            catch (Exception ex)
            {
                Tracer($"❌ Mise à jour impossible : {ex.Message}");
                await page.DisplayAlert("Erreur", "La mise à jour a échoué :\n" + ex.Message, "OK");
            }
            finally
            {
                if (veilleBloquee)
                {
                    try { DeviceDisplay.Current.KeepScreenOn = false; } catch { /* sans effet */ }
                }
            }
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
