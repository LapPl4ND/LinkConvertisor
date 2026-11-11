using System;
using System.Windows.Media;

namespace LinkConvertisor
{
    public enum Platform { None, YouTube, Spotify, SoundCloud, Deezer, AppleMusic }

    /// <summary>Erreur avec un message déjà traduit, affichable tel quel.</summary>
    public class UserFacingException : Exception
    {
        public UserFacingException(string message) : base(message) { }
    }

    /// <summary>Nom, couleur et icône de chaque plateforme.</summary>
    public static class PlatformInfo
    {
        public static Platform Detect(string url)
        {
            string u = url.Trim().ToLowerInvariant();
            if (u.Contains("youtube.com") || u.Contains("youtu.be")) return Platform.YouTube;
            if (u.Contains("spotify.com") || u.Contains("spotify.link") || u.StartsWith("spotify:")) return Platform.Spotify;
            if (u.Contains("soundcloud.com") || u.Contains("snd.sc")) return Platform.SoundCloud;
            if (u.Contains("deezer.com") || u.Contains("deezer.page.link") || u.Contains("dzr.page.link")) return Platform.Deezer;
            if (u.Contains("music.apple.com") || u.Contains("itunes.apple.com")) return Platform.AppleMusic;
            return Platform.None;
        }

        public static string Name(Platform p) => p switch
        {
            Platform.YouTube => "YouTube",
            Platform.Spotify => "Spotify",
            Platform.SoundCloud => "SoundCloud",
            Platform.Deezer => "Deezer",
            Platform.AppleMusic => "Apple Music",
            _ => ""
        };

        public static Color Accent(Platform p) => p switch
        {
            Platform.YouTube => Color.FromRgb(0xFF, 0x00, 0x33),
            Platform.Spotify => Color.FromRgb(0x1D, 0xB9, 0x54),
            Platform.SoundCloud => Color.FromRgb(0xFF, 0x55, 0x00),
            Platform.Deezer => Color.FromRgb(0xA2, 0x38, 0xFF),
            // Blanc en thème sombre, quasi-noir en thème clair (sinon invisible sur fond clair)
            Platform.AppleMusic => App.Settings.DarkTheme ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0x1C, 0x1C, 0x1E),
            _ => Color.FromRgb(0xFF, 0x3D, 0x7F)
        };

        // Nom du fichier logo dans Assets/Logos (ex : youtube.png)
        public static string FileKey(Platform p) => p switch
        {
            Platform.YouTube => "youtube",
            Platform.Spotify => "spotify",
            Platform.SoundCloud => "soundcloud",
            Platform.Deezer => "deezer",
            Platform.AppleMusic => "applemusic",
            _ => ""
        };

        // Glyphes de la police Segoe Fluent Icons / MDL2
        public static string Glyph(Platform p) => p switch
        {
            Platform.YouTube => "",     // vidéo
            Platform.Spotify => "",     // audio
            Platform.SoundCloud => "",  // nuage
            Platform.Deezer => "",      // casque
            Platform.AppleMusic => "",  // audio
            _ => ""                     // lien
        };
    }
}
