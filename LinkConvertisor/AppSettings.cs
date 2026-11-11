using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace LinkConvertisor
{
    /// <summary>
    /// Paramètres de l'app, sauvegardés dans %AppData%\LinkConvertisor\settings.json
    /// </summary>
    public class AppSettings
    {
        public bool DarkTheme { get; set; } = true;
        public string Language { get; set; } =
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? "fr" : "en";

        public string Format { get; set; } = "mp3";        // "mp3" ou "mp4"
        public string AudioQuality { get; set; } = "320";  // kbps : 128 / 192 / 256 / 320
        public string VideoQuality { get; set; } = "1080"; // 480 / 720 / 1080 / best

        public string OutputFolder { get; set; } = DefaultOutputFolder;
        public bool AutoCheckUpdates { get; set; } = true;

        public static string DefaultOutputFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "LinkConvertisor");

        /// <summary>Dossier de l'app : paramètres + outils (yt-dlp, ffmpeg).</summary>
        public static readonly string FolderPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LinkConvertisor");
        private static readonly string FilePath = Path.Combine(FolderPath, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
                    if (string.IsNullOrWhiteSpace(s.OutputFolder)) s.OutputFolder = DefaultOutputFolder;
                    return s;
                }
            }
            catch
            {
                // Fichier corrompu : on repart des valeurs par défaut
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(FolderPath);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // Pas grave si la sauvegarde échoue
            }
        }
    }
}
