using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LinkConvertisor
{
    /// <summary>
    /// Gère le "moteur" de conversion : yt-dlp (téléchargement) + ffmpeg (conversion).
    /// Ils sont téléchargés automatiquement au premier lancement dans %AppData%\LinkConvertisor\tools,
    /// comme ça l'exe reste léger.
    /// </summary>
    public static class ToolsService
    {
        private const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        private const string FFmpegZipUrl = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

        public static readonly string Dir = Path.Combine(AppSettings.FolderPath, "tools");
        public static string YtDlp => Path.Combine(Dir, "yt-dlp.exe");
        public static string FFmpeg => Path.Combine(Dir, "ffmpeg.exe");
        public static string FFprobe => Path.Combine(Dir, "ffprobe.exe");

        public static bool IsReady => File.Exists(YtDlp) && File.Exists(FFmpeg) && File.Exists(FFprobe);

        // Client séparé, sans timeout : ffmpeg pèse ~150 Mo
        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LinkConvertisor/1.0");
            return client;
        }

        /// <summary>Installe ce qui manque. status reçoit un texte à afficher ("Téléchargement de ffmpeg… 42 %").</summary>
        public static async Task EnsureAsync(IProgress<string>? status, CancellationToken ct)
        {
            if (IsReady) return;
            Directory.CreateDirectory(Dir);

            if (!File.Exists(YtDlp))
                await DownloadFileAsync(YtDlpUrl, YtDlp, "yt-dlp", status, ct);

            if (!File.Exists(FFmpeg) || !File.Exists(FFprobe))
            {
                string zip = Path.Combine(Path.GetTempPath(), "LinkConvertisor_ffmpeg.zip");
                try
                {
                    await DownloadFileAsync(FFmpegZipUrl, zip, "ffmpeg", status, ct);
                    await Task.Run(() => ExtractFFmpeg(zip), ct);
                }
                finally
                {
                    TryDelete(zip);
                }
            }
        }

        /// <summary>Retélécharge la dernière version de yt-dlp (YouTube change souvent, ça corrige la plupart des erreurs).</summary>
        public static async Task UpdateYtDlpAsync(IProgress<string>? status, CancellationToken ct)
        {
            Directory.CreateDirectory(Dir);
            await DownloadFileAsync(YtDlpUrl, YtDlp, "yt-dlp", status, ct);
        }

        public static async Task<string?> GetYtDlpVersionAsync()
        {
            if (!File.Exists(YtDlp)) return null;
            try
            {
                var result = await RunAsync(new[] { "--version" }, null, CancellationToken.None);
                return result.ExitCode == 0 ? result.Output.Trim() : null;
            }
            catch
            {
                return null;
            }
        }

        // =====================================================================
        //  Lancement de yt-dlp
        // =====================================================================

        public record RunResult(int ExitCode, string Output, string Errors);

        /// <summary>Lance yt-dlp avec ces arguments. onLine reçoit chaque ligne (stdout et stderr) en direct.</summary>
        public static async Task<RunResult> RunAsync(IEnumerable<string> args, Action<string>? onLine, CancellationToken ct)
        {
            var psi = new ProcessStartInfo(YtDlp)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Dir
            };
            // Force l'UTF-8 côté Python (sinon les accents/emoji des titres sont cassés)
            psi.Environment["PYTHONIOENCODING"] = "utf-8";
            psi.Environment["PYTHONUTF8"] = "1";
            foreach (var a in args) psi.ArgumentList.Add(a);

            var output = new StringBuilder();
            var errors = new StringBuilder();

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lock (output) output.AppendLine(e.Data);
                onLine?.Invoke(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lock (errors) errors.AppendLine(e.Data);
                onLine?.Invoke(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw;
            }

            process.WaitForExit(); // vide les derniers événements de sortie
            return new RunResult(process.ExitCode, output.ToString(), errors.ToString());
        }

        // =====================================================================
        //  Utilitaires
        // =====================================================================

        private static async Task DownloadFileAsync(string url, string destination, string label,
                                                    IProgress<string>? status, CancellationToken ct)
        {
            string temp = destination + ".download";
            using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                long? total = response.Content.Headers.ContentLength;

                await using var input = await response.Content.ReadAsStreamAsync(ct);
                await using var file = File.Create(temp);

                var buffer = new byte[81920];
                long done = 0;
                int lastPercent = -1;
                int read;
                while ((read = await input.ReadAsync(buffer, ct)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), ct);
                    done += read;

                    int percent = total > 0 ? (int)(done * 100 / total.Value) : -1;
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        string amount = percent >= 0 ? percent + " %" : (done / 1048576) + " Mo";
                        status?.Report(string.Format(Loc.T("ToolsDownloading"), label, amount));
                    }
                }
            }

            File.Move(temp, destination, overwrite: true);
        }

        private static void ExtractFFmpeg(string zipPath)
        {
            using var zip = ZipFile.OpenRead(zipPath);
            foreach (string exe in new[] { "ffmpeg.exe", "ffprobe.exe" })
            {
                var entry = zip.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith("/bin/" + exe, StringComparison.OrdinalIgnoreCase));
                if (entry == null) throw new InvalidDataException(exe + " introuvable dans l'archive");
                entry.ExtractToFile(Path.Combine(Dir, exe), overwrite: true);
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
