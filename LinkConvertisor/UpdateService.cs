using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace LinkConvertisor
{
    public record UpdateInfo(Version Version, string PageUrl, string? ExeUrl);

    /// <summary>
    /// Mise à jour automatique via les "Releases" GitHub.
    ///
    /// Pour l'activer :
    ///   1. Mets ton pseudo GitHub et le nom du dépôt ci-dessous.
    ///   2. Augmente &lt;Version&gt; dans le .csproj (ex : 1.1.0).
    ///   3. Publie le .exe, puis crée une Release sur GitHub avec le tag "v1.1.0"
    ///      et ajoute LinkConvertisor.exe en pièce jointe (Assets).
    /// </summary>
    public static class UpdateService
    {
        // ↓↓↓ À MODIFIER ↓↓↓
        private const string RepoOwner = "LapPl4ND";
        private const string RepoName = "LinkConvertisor";
        // ↑↑↑ À MODIFIER ↑↑↑

        public static bool IsConfigured => !RepoOwner.StartsWith("TON_");

        public static Version CurrentVersion
        {
            get
            {
                var v = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);
                return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
            }
        }

        /// <summary>Renvoie la nouvelle version si elle existe, sinon null.</summary>
        public static async Task<UpdateInfo?> CheckAsync(HttpClient http)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            string tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var remote)) return null;
            remote = new Version(remote.Major, remote.Minor, Math.Max(remote.Build, 0));

            if (remote <= CurrentVersion) return null;

            string page = root.GetProperty("html_url").GetString() ?? "";
            string? exe = root.TryGetProperty("assets", out var assets)
                ? assets.EnumerateArray()
                        .Select(a => a.GetProperty("browser_download_url").GetString())
                        .FirstOrDefault(u => u != null && u.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                : null;

            return new UpdateInfo(remote, page, exe);
        }

        /// <summary>
        /// Télécharge le nouveau .exe, remplace l'ancien et relance l'app.
        /// S'il n'y a pas de .exe dans la release (ou en mode debug), ouvre simplement la page GitHub.
        /// </summary>
        public static async Task InstallAsync(HttpClient http, UpdateInfo info)
        {
            string? currentExe = Environment.ProcessPath;
            bool canSelfUpdate = info.ExeUrl != null
                                 && currentExe != null
                                 && currentExe.EndsWith("LinkConvertisor.exe", StringComparison.OrdinalIgnoreCase)
                                 && !Debugger.IsAttached;

            if (!canSelfUpdate)
            {
                Process.Start(new ProcessStartInfo(info.PageUrl) { UseShellExecute = true });
                return;
            }

            string tempExe = Path.Combine(Path.GetTempPath(), "LinkConvertisor_update.exe");
            byte[] data = await http.GetByteArrayAsync(info.ExeUrl);
            await File.WriteAllBytesAsync(tempExe, data);

            // Petit script qui attend la fermeture de l'app, remplace le .exe puis le relance
            int pid = Environment.ProcessId;
            string script = Path.Combine(Path.GetTempPath(), "LinkConvertisor_update.cmd");
            string content = string.Join("\r\n",
                "@echo off",
                "chcp 65001 >nul",
                ":wait",
                "timeout /t 1 /nobreak >nul",
                $"tasklist /fi \"PID eq {pid}\" | find \"{pid}\" >nul && goto wait",
                $"move /y \"{tempExe}\" \"{currentExe}\" >nul",
                $"start \"\" \"{currentExe}\"",
                "del \"%~f0\"");
            await File.WriteAllTextAsync(script, content, new UTF8Encoding(false));

            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });

            Application.Current.Shutdown();
        }
    }
}