using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LinkConvertisor
{
    /// <summary>Un son prêt à être converti.</summary>
    /// <param name="Source">Ce qu'on donne à yt-dlp (le lien YouTube / SoundCloud final).</param>
    /// <param name="FromSearch">true si le son vient de Spotify / Deezer / Apple Music et a été retrouvé sur YouTube.</param>
    public record MediaInfo(
        string Title,
        string Author,
        IReadOnlyList<string> Thumbnails,
        double? Duration,
        Platform Platform,
        string Source,
        bool FromSearch);

    public enum ConvertStage { Downloading, Converting }

    public record ConvertProgress(ConvertStage Stage, double Percent, int Part, double? Speed);

    /// <summary>
    /// Analyse un lien puis le convertit en MP3 / MP4 avec yt-dlp + ffmpeg.
    ///
    /// YouTube et SoundCloud : téléchargés directement.
    /// Spotify, Deezer, Apple Music : ces plateformes ne permettent pas de télécharger leur audio (DRM),
    /// donc on récupère "Artiste - Titre" et on cherche le même son sur YouTube.
    /// </summary>
    public class MediaService
    {
        private readonly HttpClient _http;

        private static readonly Regex SpotifyRegex =
            new(@"spotify\.com/(?:intl-[a-z]{2}/)?(track|album|playlist|artist|episode|show)/([A-Za-z0-9]+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DeezerRegex =
            new(@"deezer\.com/(?:[a-z]{2}(?:-[a-z]{2})?/)?(track|album|playlist|artist)/(\d+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // music.apple.com/fr/song/nom/123  ou  music.apple.com/fr/album/nom/456?i=123
        private static readonly Regex AppleSongRegex =
            new(@"apple\.com/(?:[a-z]{2}/)?song/(?:[^/?#]+/)?(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex AppleTrackParamRegex =
            new(@"[?&]i=(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex YouTubeIdRegex =
            new(@"^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);

        public MediaService(HttpClient http) => _http = http;

        // =====================================================================
        //  Analyse
        // =====================================================================

        public async Task<MediaInfo> ResolveAsync(string input, CancellationToken ct)
        {
            string url = Normalize(input);
            var platform = PlatformInfo.Detect(url);

            switch (platform)
            {
                case Platform.YouTube:
                case Platform.SoundCloud:
                    return await ResolveDirect(url, platform, ct);

                case Platform.Spotify:
                    var sp = await SpotifyMeta(url, ct);
                    return await ResolveBySearch(sp.Title, sp.Artist, sp.Cover, Platform.Spotify, ct);

                case Platform.Deezer:
                    var dz = await DeezerMeta(url, ct);
                    return await ResolveBySearch(dz.Title, dz.Artist, dz.Cover, Platform.Deezer, ct);

                case Platform.AppleMusic:
                    var am = await AppleMeta(url, ct);
                    return await ResolveBySearch(am.Title, am.Artist, am.Cover, Platform.AppleMusic, ct);

                default:
                    throw new UserFacingException(Loc.T("ErrUnknownLink"));
            }
        }

        private async Task<MediaInfo> ResolveDirect(string url, Platform platform, CancellationToken ct)
        {
            var json = await DumpJson(url, ct);

            string title = Str(json, "track") ?? Str(json, "title") ?? PlatformInfo.Name(platform);
            string author = Str(json, "artist") ?? Str(json, "uploader") ?? Str(json, "channel") ?? "";
            string source = Str(json, "webpage_url") ?? url;

            return new MediaInfo(title, author, Thumbnails(json), Num(json, "duration"), platform, source, false);
        }

        private async Task<MediaInfo> ResolveBySearch(string title, string artist, string? cover,
                                                      Platform platform, CancellationToken ct)
        {
            string query = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} - {title}";

            JsonElement json;
            try
            {
                json = await DumpJson("ytsearch1:" + query, ct);
            }
            catch (UserFacingException)
            {
                throw new UserFacingException(Loc.T("ErrSearch"));
            }

            string source = Str(json, "webpage_url") ?? throw new UserFacingException(Loc.T("ErrSearch"));

            // On garde la vraie pochette (carrée) de la plateforme d'origine si on l'a
            var thumbs = new List<string>();
            if (cover != null) thumbs.Add(cover);
            thumbs.AddRange(Thumbnails(json));

            return new MediaInfo(title, artist, thumbs, Num(json, "duration"), platform, source, true);
        }

        /// <summary>Demande à yt-dlp les infos du lien (sans rien télécharger).</summary>
        private static async Task<JsonElement> DumpJson(string target, CancellationToken ct)
        {
            var result = await ToolsService.RunAsync(new[]
            {
                "-j", "--no-playlist", "--playlist-items", "1", "--no-warnings", "--skip-download", target
            }, null, ct);

            string? line = result.Output.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("{"));
            if (result.ExitCode != 0 || line == null)
                throw new UserFacingException(TranslateError(result.Errors, Loc.T("ErrNotFound")));

            using var doc = JsonDocument.Parse(line);
            return doc.RootElement.Clone();
        }

        private static List<string> Thumbnails(JsonElement json)
        {
            var list = new List<string>();

            // YouTube : on force les miniatures JPG (le WebP n'est pas toujours lisible par WPF)
            string? id = Str(json, "id");
            string extractor = (Str(json, "extractor_key") ?? "").ToLowerInvariant();
            if (extractor.StartsWith("youtube") && id != null && YouTubeIdRegex.IsMatch(id))
            {
                list.Add($"https://i.ytimg.com/vi/{id}/maxresdefault.jpg");
                list.Add($"https://i.ytimg.com/vi/{id}/hqdefault.jpg");
            }

            string? thumb = Str(json, "thumbnail");
            if (thumb != null)
            {
                // SoundCloud : version originale, plus grande
                if (thumb.Contains("-t500x500.")) list.Add(thumb.Replace("-t500x500.", "-original."));
                list.Add(thumb);
            }
            return list;
        }

        // =====================================================================
        //  Conversion
        // =====================================================================

        /// <summary>Télécharge + convertit. Renvoie le chemin du fichier créé.</summary>
        /// <param name="format">"mp3" ou "mp4"</param>
        /// <param name="quality">mp3 : kbps ("320"…) — mp4 : hauteur max ("1080"…) ou "best"</param>
        public async Task<string> ConvertAsync(MediaInfo media, string format, string quality, string outputFolder,
                                               IProgress<ConvertProgress> progress, CancellationToken ct)
        {
            bool mp3 = format == "mp3";

            string fileName = media.FromSearch
                ? EscapeTemplate(SanitizeFileName(string.IsNullOrWhiteSpace(media.Author) ? media.Title : $"{media.Author} - {media.Title}"))
                : "%(title)s";

            var args = new List<string>
            {
                "--no-playlist", "--no-warnings", "--newline", "--progress", "--windows-filenames", "--no-mtime",
                "--ffmpeg-location", ToolsService.Dir,
                "-P", outputFolder,
                "-o", fileName + ".%(ext)s",
                "--progress-template",
                "download:LC|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.speed)s",
                "--print", "after_move:LCFILE|%(filepath)s",
                "--embed-metadata", "--embed-thumbnail", "--convert-thumbnails", "jpg",
            };

            if (mp3)
            {
                args.AddRange(new[]
                {
                    "-f", "bestaudio/best",
                    "-x", "--audio-format", "mp3", "--audio-quality", quality + "K",
                    // Pochette recadrée en carré (les miniatures YouTube sont en 16:9)
                    "--ppa", "ThumbnailsConvertor+FFmpeg_o:-c:v mjpeg -qmin 1 -qscale:v 1 -vf crop=\"'if(gt(ih,iw),iw,ih)':'if(gt(iw,ih),ih,iw)'\"",
                });
            }
            else
            {
                // H.264 + AAC en priorité : lisible partout (lecteur Windows, téléphone, montage…)
                string sort = quality == "best" ? "res,vcodec:h264,acodec:aac" : $"res:{quality},vcodec:h264,acodec:aac";
                args.AddRange(new[]
                {
                    "-f", "bv*+ba/b", "-S", sort,
                    "--merge-output-format", "mp4", "--remux-video", "mp4",
                });
            }

            // Spotify / Deezer / Apple : on écrit le vrai artiste + titre dans les tags du fichier
            if (media.FromSearch)
            {
                args.AddRange(new[] { "--parse-metadata", MetadataLiteral(media.Title) + ":(?P<meta_title>.+)" });
                if (!string.IsNullOrWhiteSpace(media.Author))
                    args.AddRange(new[] { "--parse-metadata", MetadataLiteral(media.Author) + ":(?P<meta_artist>.+)" });
            }

            args.Add(media.Source);

            string? finalPath = null;
            double lastPercent = 0;
            int part = 1;

            var result = await ToolsService.RunAsync(args, line =>
            {
                line = line.Trim();
                if (line.StartsWith("LCFILE|"))
                {
                    finalPath = line["LCFILE|".Length..];
                    return;
                }
                if (!line.StartsWith("LC|")) return;

                var p = line.Split('|');
                if (p.Length < 5) return;
                double? done = ParseNum(p[1]);
                double? total = ParseNum(p[2]) ?? ParseNum(p[3]);
                if (done == null || total is null or <= 0) return;

                double percent = Math.Clamp(done.Value / total.Value * 100, 0, 100);
                if (percent + 25 < lastPercent) part++; // MP4 : la vidéo puis l'audio sont téléchargés séparément
                lastPercent = percent;

                var stage = percent >= 100 ? ConvertStage.Converting : ConvertStage.Downloading;
                progress.Report(new ConvertProgress(stage, percent, part, ParseNum(p[4])));
            }, ct);

            if (result.ExitCode != 0)
                throw new UserFacingException(TranslateError(result.Errors, Loc.T("ErrConvert")));

            if (finalPath == null || !File.Exists(finalPath))
            {
                // Plan B : le fichier le plus récent du dossier
                finalPath = new DirectoryInfo(outputFolder).GetFiles("*." + format)
                    .OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()?.FullName;
            }

            return finalPath ?? outputFolder;
        }

        // =====================================================================
        //  Spotify / Deezer / Apple Music : récupérer "Artiste - Titre"
        // =====================================================================

        private record TrackMeta(string Title, string Artist, string? Cover);

        private async Task<TrackMeta> SpotifyMeta(string url, CancellationToken ct)
        {
            if (url.Contains("spotify.link")) url = await ResolveRedirect(url, ct);

            var m = SpotifyRegex.Match(url);
            if (!m.Success) throw new UserFacingException(Loc.T("ErrUnknownLink"));
            if (!m.Groups[1].Value.Equals("track", StringComparison.OrdinalIgnoreCase))
                throw new UserFacingException(string.Format(Loc.T("ErrOnlyTracks"), "Spotify"));

            string trackUrl = "https://open.spotify.com/track/" + m.Groups[2].Value;

            // Titre + pochette via l'oEmbed officiel
            string? title = null, cover = null;
            try
            {
                using var oembed = JsonDocument.Parse(await _http.GetStringAsync("https://open.spotify.com/oembed?url=" + E(trackUrl), ct));
                title = Str(oembed.RootElement, "title");
                cover = Str(oembed.RootElement, "thumbnail_url")?.Replace("ab67616d00001e02", "ab67616d0000b273");
            }
            catch (HttpRequestException) { }

            // L'artiste n'est pas dans l'oEmbed : on le lit dans le <title> de la page
            //   "Nom du son - song and lyrics by Artiste | Spotify"
            string artist = "";
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, trackUrl);
                request.Headers.AcceptLanguage.ParseAdd("en");
                using var response = await _http.SendAsync(request, ct);
                string html = await response.Content.ReadAsStringAsync(ct);

                var t = Regex.Match(html, @"<title>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (t.Success)
                {
                    string pageTitle = WebUtility.HtmlDecode(t.Groups[1].Value).Trim();
                    var parts = Regex.Match(pageTitle, @"^(.+?) - (?:song|single)(?: and lyrics)? by (.+?) \| Spotify$", RegexOptions.IgnoreCase);
                    if (parts.Success)
                    {
                        title ??= parts.Groups[1].Value;
                        artist = parts.Groups[2].Value;
                    }
                }

                if (artist.Length == 0)
                {
                    // Plan B : <meta property="og:description" content="Artiste · Album · Song · 2020">
                    var d = Regex.Match(html, @"og:description""\s+content=""([^""]+)""", RegexOptions.IgnoreCase);
                    if (d.Success)
                    {
                        var bits = WebUtility.HtmlDecode(d.Groups[1].Value).Split('·').Select(b => b.Trim()).ToArray();
                        if (bits.Length >= 2 && !bits[0].StartsWith("Listen", StringComparison.OrdinalIgnoreCase))
                            artist = bits[0];
                    }
                }
            }
            catch (HttpRequestException) { }

            if (string.IsNullOrWhiteSpace(title)) throw new UserFacingException(Loc.T("ErrNotFound"));
            return new TrackMeta(title, artist, cover);
        }

        private async Task<TrackMeta> DeezerMeta(string url, CancellationToken ct)
        {
            var m = DeezerRegex.Match(url);

            // Lien de partage court (link.deezer.com/s/…, deezer.page.link/…) : on cherche le vrai lien derrière
            if (!m.Success) m = await ResolveDeezerShortLink(url, ct);
            if (!m.Success) throw new UserFacingException(Loc.T("ErrUnknownLink"));
            if (!m.Groups[1].Value.Equals("track", StringComparison.OrdinalIgnoreCase))
                throw new UserFacingException(string.Format(Loc.T("ErrOnlyTracks"), "Deezer"));

            using var doc = JsonDocument.Parse(await _http.GetStringAsync("https://api.deezer.com/track/" + m.Groups[2].Value, ct));
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out _)) throw new UserFacingException(Loc.T("ErrNotFound"));

            string title = Str(root, "title") ?? throw new UserFacingException(Loc.T("ErrNotFound"));
            string artist = root.TryGetProperty("artist", out var a) ? Str(a, "name") ?? "" : "";
            string? cover = root.TryGetProperty("album", out var al) ? Str(al, "cover_xl") : null;
            return new TrackMeta(title, artist, cover);
        }

        private async Task<TrackMeta> AppleMeta(string url, CancellationToken ct)
        {
            var song = AppleSongRegex.Match(url);
            var param = AppleTrackParamRegex.Match(url);
            string? id = song.Success ? song.Groups[1].Value : param.Success ? param.Groups[1].Value : null;
            if (id == null) throw new UserFacingException(string.Format(Loc.T("ErrOnlyTracks"), "Apple Music"));

            using var doc = JsonDocument.Parse(await _http.GetStringAsync($"https://itunes.apple.com/lookup?id={id}&entity=song", ct));
            var track = doc.RootElement.GetProperty("results").EnumerateArray()
                           .FirstOrDefault(r => Str(r, "wrapperType") == "track");
            if (track.ValueKind != JsonValueKind.Object) throw new UserFacingException(Loc.T("ErrNotFound"));

            string title = Str(track, "trackName") ?? throw new UserFacingException(Loc.T("ErrNotFound"));
            string artist = Str(track, "artistName") ?? "";
            string? cover = Str(track, "artworkUrl100")?.Replace("100x100bb", "1000x1000bb");
            return new TrackMeta(title, artist, cover);
        }

        // Lien générique "deezer.com/.../track/123" (ou "deezer://www.deezer.com/track/123") trouvé dans une URL ou du HTML
        private static readonly Regex DeezerAnyRegex =
            new(@"deezer\.com(?:\\?/[a-z]{2}(?:-[a-z]{2})?)?\\?/(track|album|playlist|artist)\\?/(\d+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private async Task<Match> ResolveDeezerShortLink(string url, CancellationToken ct)
        {
            using var response = await _http.GetAsync(url, ct);

            // 1) Redirection HTTP classique : l'URL finale contient le lien
            string final = response.RequestMessage?.RequestUri?.ToString() ?? url;
            var m = DeezerRegex.Match(Uri.UnescapeDataString(final));
            if (m.Success) return m;

            // 2) L'URL finale contient le vrai lien en paramètre (ex : ?link=https%3A%2F%2Fwww.deezer.com%2Ftrack%2F123)
            m = DeezerAnyRegex.Match(Uri.UnescapeDataString(final));
            if (m.Success) return m;

            // 3) Page de redirection en JavaScript : le lien est écrit quelque part dans le HTML
            //    (on préfère un lien de titre, la page peut aussi contenir des liens vers l'artiste/l'album)
            string html = WebUtility.HtmlDecode(Uri.UnescapeDataString(await response.Content.ReadAsStringAsync(ct)));
            var all = DeezerAnyRegex.Matches(html);
            return all.FirstOrDefault(x => x.Groups[1].Value.Equals("track", StringComparison.OrdinalIgnoreCase))
                   ?? (all.Count > 0 ? all[0] : Match.Empty);
        }

        private async Task<string> ResolveRedirect(string url, CancellationToken ct)
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            return response.RequestMessage?.RequestUri?.ToString() ?? url;
        }

        // =====================================================================
        //  Erreurs yt-dlp -> message lisible
        // =====================================================================

        private static string TranslateError(string stderr, string fallbackPrefix)
        {
            string s = stderr.ToLowerInvariant();

            if (s.Contains("confirm your age") || s.Contains("age-restricted") || s.Contains("inappropriate for some users"))
                return Loc.T("ErrAgeRestricted");
            if (s.Contains("private video") || s.Contains("video unavailable") || s.Contains("has been removed")
                || s.Contains("is not available") || s.Contains("http error 404"))
                return Loc.T("ErrUnavailable");
            if (s.Contains("not a bot") || s.Contains("http error 403") || s.Contains("nsig") || s.Contains("signature")
                || s.Contains("unable to extract") || s.Contains("requested format is not available"))
                return Loc.T("ErrOutdated");
            if (s.Contains("getaddrinfo") || s.Contains("failed to resolve") || s.Contains("unable to download webpage")
                || s.Contains("timed out") || s.Contains("connection"))
                return Loc.T("ErrNetwork");

            string? last = stderr.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith("ERROR:"));
            return last == null ? fallbackPrefix : fallbackPrefix.TrimEnd(' ', ':', '.') + " — " + last["ERROR:".Length..].Trim();
        }

        // =====================================================================
        //  Utilitaires
        // =====================================================================

        private static string Normalize(string input)
        {
            string url = input.Trim();

            // spotify:track:ID  ->  https://open.spotify.com/track/ID
            if (url.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = url.Split(':');
                if (parts.Length >= 3) return $"https://open.spotify.com/{parts[1]}/{parts[2]}";
            }

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;

            return url;
        }

        /// <summary>Texte fixe utilisable comme "FROM" dans --parse-metadata.</summary>
        private static string MetadataLiteral(string value)
        {
            value = value.Replace("%", "%%").Replace(":", "\\:");
            // Un seul mot (ex : "Muse") serait pris pour un nom de champ : on passe par une valeur par défaut
            if (Regex.IsMatch(value, @"^[A-Za-z_]+$")) return $"%(lc_none|{value})s";
            return value;
        }

        private static string EscapeTemplate(string value) => value.Replace("%", "%%");

        public static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            string clean = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim().TrimEnd('.');
            if (clean.Length > 120) clean = clean[..120].Trim();
            return clean.Length == 0 ? "LinkConvertisor" : clean;
        }

        private static string E(string s) => Uri.EscapeDataString(s);

        private static string? Str(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? (string.IsNullOrWhiteSpace(v.GetString()) ? null : v.GetString())
                : null;

        private static double? Num(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetDouble()
                : null;

        private static double? ParseNum(string s) =>
            double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}
