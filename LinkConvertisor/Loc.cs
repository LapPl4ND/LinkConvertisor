using System.Collections.Generic;
using System.Windows;

namespace LinkConvertisor
{
    /// <summary>
    /// Traductions FR / EN.
    /// Dans le XAML : Text="{DynamicResource S.Cle}"   —   Dans le code : Loc.T("Cle")
    /// </summary>
    public static class Loc
    {
        public static string Language { get; private set; } = "fr";

        private static readonly Dictionary<string, string> Fr = new()
        {
            // Interface
            ["Minimize"] = "Réduire",
            ["Close"] = "Fermer",
            ["Settings"] = "Paramètres",
            ["Heading"] = "Convertis n'importe quel son",
            ["Subheading"] = "YouTube, Spotify, SoundCloud, Deezer et Apple Music, en MP3 ou en MP4.",
            ["Placeholder"] = "Colle un lien…",
            ["Paste"] = "Coller",
            ["PasteTip"] = "Coller depuis le presse-papiers",
            ["Analyze"] = "Analyser",
            ["EmptyTitle"] = "Aucun son pour l'instant",
            ["EmptyHint"] = "Colle un lien, ou glisse-le directement dans la fenêtre",
            ["Loading"] = "Analyse du lien…",
            ["Quality"] = "Qualité",
            ["ConvertTo"] = "Convertir en {0}",
            ["OpenFolderTip"] = "Ouvrir le dossier de sortie",
            ["Cancel"] = "Annuler",
            ["FoundOnYouTube"] = "Trouvé sur YouTube",

            // Progression
            ["Starting"] = "Démarrage…",
            ["Downloading"] = "Téléchargement…",
            ["DownloadingPart"] = "Téléchargement ({0}/2)…",
            ["Converting"] = "Conversion…",
            ["ToolsSetup"] = "Première utilisation : installation du moteur…",
            ["ToolsDownloading"] = "Téléchargement de {0}… {1}",

            // Paramètres
            ["SettingsTitle"] = "Paramètres",
            ["Theme"] = "Thème",
            ["ThemeDark"] = "Sombre",
            ["ThemeLight"] = "Clair",
            ["Language"] = "Langue",
            ["OutputFolder"] = "Dossier de sortie",
            ["Change"] = "Changer",
            ["Engine"] = "Moteur de conversion",
            ["EngineUpdate"] = "Mettre à jour",
            ["EngineMissing"] = "yt-dlp + ffmpeg (pas encore installés)",
            ["EngineVersion"] = "yt-dlp {0} + ffmpeg",
            ["Updates"] = "Mises à jour",
            ["AutoCheck"] = "Vérifier au démarrage",
            ["CheckNow"] = "Vérifier maintenant",

            // Messages
            ["ErrEmptyLink"] = "Colle d'abord un lien.",
            ["ErrUnknownLink"] = "Lien non reconnu. Plateformes gérées : YouTube, Spotify, SoundCloud, Deezer, Apple Music.",
            ["ErrOnlyTracks"] = "Pour {0}, seuls les liens de titres sont gérés (pas les albums ni les playlists).",
            ["ErrNotFound"] = "Impossible de trouver ce son.",
            ["ErrSearch"] = "Aucun résultat trouvé sur YouTube pour ce titre.",
            ["ErrUnavailable"] = "Cette vidéo n'est pas disponible (privée, supprimée ou bloquée).",
            ["ErrAgeRestricted"] = "Cette vidéo demande une connexion (restriction d'âge) : impossible de la convertir.",
            ["ErrOutdated"] = "YouTube a changé quelque chose. Mets à jour le moteur dans les paramètres puis réessaie.",
            ["ErrConvert"] = "La conversion a échoué : ",
            ["ErrNetwork"] = "Connexion impossible. Vérifie ta connexion internet.",
            ["ErrTools"] = "Impossible d'installer le moteur de conversion : ",
            ["ErrFolder"] = "Impossible de créer le dossier de sortie : ",
            ["ErrClipboard"] = "Impossible d'accéder au presse-papiers.",
            ["ErrUnexpected"] = "Une erreur inattendue s'est produite :",
            ["ClipboardEmpty"] = "Le presse-papiers est vide.",
            ["NeedAnalyze"] = "Analyse d'abord un lien.",
            ["Done"] = "Conversion terminée !",
            ["Cancelled"] = "Conversion annulée.",
            ["OpenFolder"] = "Ouvrir le dossier",
            ["LinkDetected"] = "Lien détecté dans le presse-papiers.",
            ["EngineUpdated"] = "Moteur à jour !",
            ["EngineUpdating"] = "Mise à jour du moteur…",
            ["UpdateAvailable"] = "Nouvelle version {0} disponible !",
            ["UpdateAction"] = "Mettre à jour",
            ["UpToDate"] = "Tu as déjà la dernière version.",
            ["UpdateFailed"] = "Impossible de vérifier les mises à jour.",
            ["UpdateDownloading"] = "Téléchargement de la mise à jour…",
            ["UpdateNotConfigured"] = "Mises à jour pas encore configurées (voir UpdateService.cs).",
        };

        private static readonly Dictionary<string, string> En = new()
        {
            ["Minimize"] = "Minimize",
            ["Close"] = "Close",
            ["Settings"] = "Settings",
            ["Heading"] = "Convert any track",
            ["Subheading"] = "YouTube, Spotify, SoundCloud, Deezer and Apple Music, to MP3 or MP4.",
            ["Placeholder"] = "Paste a link…",
            ["Paste"] = "Paste",
            ["PasteTip"] = "Paste from clipboard",
            ["Analyze"] = "Analyze",
            ["EmptyTitle"] = "Nothing here yet",
            ["EmptyHint"] = "Paste a link, or drag it straight into the window",
            ["Loading"] = "Analyzing link…",
            ["Quality"] = "Quality",
            ["ConvertTo"] = "Convert to {0}",
            ["OpenFolderTip"] = "Open output folder",
            ["Cancel"] = "Cancel",
            ["FoundOnYouTube"] = "Found on YouTube",

            ["Starting"] = "Starting…",
            ["Downloading"] = "Downloading…",
            ["DownloadingPart"] = "Downloading ({0}/2)…",
            ["Converting"] = "Converting…",
            ["ToolsSetup"] = "First launch: installing the engine…",
            ["ToolsDownloading"] = "Downloading {0}… {1}",

            ["SettingsTitle"] = "Settings",
            ["Theme"] = "Theme",
            ["ThemeDark"] = "Dark",
            ["ThemeLight"] = "Light",
            ["Language"] = "Language",
            ["OutputFolder"] = "Output folder",
            ["Change"] = "Change",
            ["Engine"] = "Conversion engine",
            ["EngineUpdate"] = "Update",
            ["EngineMissing"] = "yt-dlp + ffmpeg (not installed yet)",
            ["EngineVersion"] = "yt-dlp {0} + ffmpeg",
            ["Updates"] = "Updates",
            ["AutoCheck"] = "Check on startup",
            ["CheckNow"] = "Check now",

            ["ErrEmptyLink"] = "Paste a link first.",
            ["ErrUnknownLink"] = "Link not recognized. Supported: YouTube, Spotify, SoundCloud, Deezer, Apple Music.",
            ["ErrOnlyTracks"] = "For {0}, only track links are supported (no albums or playlists).",
            ["ErrNotFound"] = "Couldn't find this track.",
            ["ErrSearch"] = "No match found on YouTube for this track.",
            ["ErrUnavailable"] = "This video isn't available (private, deleted or blocked).",
            ["ErrAgeRestricted"] = "This video requires sign-in (age-restricted), it can't be converted.",
            ["ErrOutdated"] = "YouTube changed something. Update the engine in Settings and try again.",
            ["ErrConvert"] = "Conversion failed: ",
            ["ErrNetwork"] = "Can't connect. Check your internet connection.",
            ["ErrTools"] = "Couldn't install the conversion engine: ",
            ["ErrFolder"] = "Couldn't create the output folder: ",
            ["ErrClipboard"] = "Can't access the clipboard.",
            ["ErrUnexpected"] = "An unexpected error occurred:",
            ["ClipboardEmpty"] = "The clipboard is empty.",
            ["NeedAnalyze"] = "Analyze a link first.",
            ["Done"] = "Conversion complete!",
            ["Cancelled"] = "Conversion cancelled.",
            ["OpenFolder"] = "Open folder",
            ["LinkDetected"] = "Link found in your clipboard.",
            ["EngineUpdated"] = "Engine is up to date!",
            ["EngineUpdating"] = "Updating engine…",
            ["UpdateAvailable"] = "Version {0} is available!",
            ["UpdateAction"] = "Update",
            ["UpToDate"] = "You're on the latest version.",
            ["UpdateFailed"] = "Couldn't check for updates.",
            ["UpdateDownloading"] = "Downloading update…",
            ["UpdateNotConfigured"] = "Updates aren't set up yet (see UpdateService.cs).",
        };

        /// <summary>Change la langue de toute l'interface.</summary>
        public static void Apply(string language)
        {
            Language = language == "en" ? "en" : "fr";
            var dict = Language == "en" ? En : Fr;
            foreach (var pair in dict)
                Application.Current.Resources["S." + pair.Key] = pair.Value;
        }

        /// <summary>Texte traduit pour le code C#.</summary>
        public static string T(string key)
        {
            var dict = Language == "en" ? En : Fr;
            return dict.TryGetValue(key, out var value) ? value : key;
        }
    }
}
