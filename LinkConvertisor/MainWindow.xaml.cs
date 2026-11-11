using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace LinkConvertisor
{
    public partial class MainWindow : Window
    {
        private enum ToastKind { Info, Success, Warning, Error }

        private static readonly HttpClient Http = CreateHttpClient();
        private readonly MediaService _media = new(Http);

        private Color _accentColor = PlatformInfo.Accent(Platform.None);
        private int _accentVersion;

        private MediaInfo? _current;          // dernier son analysé
        private string _analyzedInput = "";   // lien correspondant
        private Platform _platform = Platform.None;
        private bool _isBusy;
        private bool _syncingSettings;
        private int _toastVersion;
        private Action? _toastAction;
        private string _lastClipboardSuggestion = "";
        private CancellationTokenSource? _convertCts;

        // Qualités proposées : (valeur envoyée à yt-dlp, texte affiché)
        private static readonly (string Value, string Label)[] AudioQualities =
            { ("128", "128k"), ("192", "192k"), ("256", "256k"), ("320", "320k") };
        private static readonly (string Value, string Label)[] VideoQualities =
            { ("480", "480p"), ("720", "720p"), ("1080", "1080p"), ("best", "Max") };

        private static AppSettings Settings => App.Settings;
        private bool IsMp3 => rbMp4.IsChecked != true;

        public MainWindow()
        {
            // Couleur d'accent utilisée partout via DynamicResource
            SetAccentBrushes(_accentColor);
            InitializeComponent();

            SyncSettingsUi();
            SyncFormatUi();
            LoadTitleIcon();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            txtUrl.Focus();

            // Vérification silencieuse des mises à jour au démarrage
            if (Settings.AutoCheckUpdates && UpdateService.IsConfigured)
                await CheckForUpdates(silent: true);
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36");
            return client;
        }

        // =====================================================================
        //  Analyse du lien
        // =====================================================================

        private async Task<bool> Analyze()
        {
            if (_isBusy) return false;

            string input = txtUrl.Text.Trim();
            if (input.Length == 0)
            {
                ShowToast(Loc.T("ErrEmptyLink"), ToastKind.Warning);
                txtUrl.Focus();
                return false;
            }
            if (PlatformInfo.Detect(input) == Platform.None)
            {
                ShowToast(Loc.T("ErrUnknownLink"), ToastKind.Error);
                return false;
            }

            SetBusy(true, showOverlay: true);
            txtLoading.Text = Loc.T("Loading");
            try
            {
                if (!await EnsureTools()) return false;
                txtLoading.Text = Loc.T("Loading");

                var media = await _media.ResolveAsync(input, CancellationToken.None);
                var bitmap = await LoadThumbnail(media.Thumbnails);

                _current = media;
                _analyzedInput = input;
                ShowPreview(media, bitmap);
                return true;
            }
            catch (UserFacingException ex)
            {
                ShowToast(ex.Message, ToastKind.Error);
            }
            catch (HttpRequestException)
            {
                ShowToast(Loc.T("ErrNetwork"), ToastKind.Error);
            }
            catch (TaskCanceledException)
            {
                ShowToast(Loc.T("ErrNetwork"), ToastKind.Error);
            }
            catch (Exception ex)
            {
                ShowToast(Loc.T("ErrNotFound") + " (" + ex.Message + ")", ToastKind.Error);
            }
            finally
            {
                SetBusy(false);
            }
            return false;
        }

        /// <summary>Installe yt-dlp + ffmpeg au premier lancement (avec la progression dans l'overlay).</summary>
        private async Task<bool> EnsureTools()
        {
            if (ToolsService.IsReady) return true;

            txtLoading.Text = Loc.T("ToolsSetup");
            var status = new Progress<string>(s => txtLoading.Text = Loc.T("ToolsSetup") + "\n" + s);
            try
            {
                await ToolsService.EnsureAsync(status, CancellationToken.None);
                return true;
            }
            catch (Exception ex)
            {
                ShowToast(Loc.T("ErrTools") + ex.Message, ToastKind.Error);
                return false;
            }
        }

        private static async Task<BitmapSource?> LoadThumbnail(IEnumerable<string> urls)
        {
            foreach (string url in urls)
            {
                try
                {
                    byte[] data = await Http.GetByteArrayAsync(url);
                    using var ms = new MemoryStream(data);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch
                {
                    // 404 ou format illisible : on essaie la suivante
                }
            }
            return null;
        }

        private void ShowPreview(MediaInfo media, BitmapSource? bitmap)
        {
            emptyState.Visibility = Visibility.Collapsed;
            noArtGlyph.Visibility = bitmap == null ? Visibility.Visible : Visibility.Collapsed;
            searchBadge.Visibility = media.FromSearch ? Visibility.Visible : Visibility.Collapsed;

            picPreview.Source = bitmap;
            ambientImage.Source = bitmap;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            picPreview.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350)) { EasingFunction = ease });
            var scale = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(450)) { EasingFunction = ease };
            previewScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            previewScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
            ambientImage.BeginAnimation(OpacityProperty, new DoubleAnimation(bitmap == null ? 0 : 0.6, TimeSpan.FromMilliseconds(900)) { EasingFunction = ease });

            txtTitle.Text = media.Title;
            string platformName = PlatformInfo.Name(media.Platform);
            txtAuthor.Text = string.IsNullOrWhiteSpace(media.Author) ? platformName : $"{media.Author}  ·  {platformName}";
            txtDuration.Text = FormatDuration(media.Duration);
            metaPanel.Visibility = Visibility.Visible;

            progressPanel.Visibility = Visibility.Collapsed;
            btnConvert.IsEnabled = true;
        }

        private void SetBusy(bool busy, bool showOverlay = false)
        {
            _isBusy = busy;
            loadingOverlay.Visibility = busy && showOverlay ? Visibility.Visible : Visibility.Collapsed;
            btnAnalyze.IsEnabled = !busy;
            btnConvert.IsEnabled = !busy && _current != null;
            rbMp3.IsEnabled = rbMp4.IsEnabled = !busy;
            rbQ1.IsEnabled = rbQ2.IsEnabled = rbQ3.IsEnabled = rbQ4.IsEnabled = !busy;
        }

        // =====================================================================
        //  Conversion
        // =====================================================================

        private async Task ConvertCurrent()
        {
            if (_isBusy) return;

            // Le lien a changé depuis la dernière analyse : on ré-analyse d'abord
            if (_current == null || txtUrl.Text.Trim() != _analyzedInput)
            {
                if (!await Analyze()) return;
            }
            var media = _current!;

            string folder = Settings.OutputFolder;
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex)
            {
                ShowToast(Loc.T("ErrFolder") + ex.Message, ToastKind.Error);
                return;
            }

            string format = IsMp3 ? "mp3" : "mp4";
            string quality = IsMp3 ? Settings.AudioQuality : Settings.VideoQuality;

            SetBusy(true);
            _convertCts = new CancellationTokenSource();
            progressPanel.Visibility = Visibility.Visible;
            progressBar.Value = 0;
            txtProgress.Text = Loc.T("Starting");
            txtSpeed.Text = "";

            var progress = new Progress<ConvertProgress>(p =>
            {
                progressBar.Value = p.Percent;
                if (p.Stage == ConvertStage.Converting)
                {
                    txtProgress.Text = Loc.T("Converting");
                    txtSpeed.Text = "";
                }
                else
                {
                    string label = p.Part > 1 ? string.Format(Loc.T("DownloadingPart"), p.Part) : Loc.T("Downloading");
                    txtProgress.Text = $"{label}  {p.Percent:0}%";
                    txtSpeed.Text = FormatSpeed(p.Speed);
                }
            });

            try
            {
                string path = await _media.ConvertAsync(media, format, quality, folder, progress, _convertCts.Token);

                progressBar.Value = 100;
                ShowToast(Loc.T("Done"), ToastKind.Success, Loc.T("OpenFolder"), () => RevealInExplorer(path));
            }
            catch (OperationCanceledException)
            {
                ShowToast(Loc.T("Cancelled"), ToastKind.Warning);
            }
            catch (UserFacingException ex)
            {
                ShowToast(ex.Message, ToastKind.Error);
            }
            catch (Exception ex)
            {
                ShowToast(Loc.T("ErrConvert") + ex.Message, ToastKind.Error);
            }
            finally
            {
                _convertCts?.Dispose();
                _convertCts = null;
                progressPanel.Visibility = Visibility.Collapsed;
                SetBusy(false);
            }
        }

        private void CancelConvert_Click(object sender, RoutedEventArgs e) => _convertCts?.Cancel();

        private static void RevealInExplorer(string path)
        {
            try
            {
                if (File.Exists(path)) Process.Start("explorer.exe", $"/select,\"{path}\"");
                else Process.Start("explorer.exe", $"\"{path}\"");
            }
            catch { }
        }

        private void OpenOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(Settings.OutputFolder);
                Process.Start("explorer.exe", $"\"{Settings.OutputFolder}\"");
            }
            catch (Exception ex)
            {
                ShowToast(Loc.T("ErrFolder") + ex.Message, ToastKind.Error);
            }
        }

        // =====================================================================
        //  Format + qualité
        // =====================================================================

        private RadioButton[] QualityButtons => new[] { rbQ1, rbQ2, rbQ3, rbQ4 };

        private void SyncFormatUi()
        {
            _syncingSettings = true;
            rbMp3.IsChecked = Settings.Format != "mp4";
            rbMp4.IsChecked = Settings.Format == "mp4";
            _syncingSettings = false;
            RefreshQualityButtons();
        }

        private void RefreshQualityButtons()
        {
            var options = IsMp3 ? AudioQualities : VideoQualities;
            string selected = IsMp3 ? Settings.AudioQuality : Settings.VideoQuality;

            _syncingSettings = true;
            var buttons = QualityButtons;
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].Content = options[i].Label;
                buttons[i].Tag = options[i].Value;
                buttons[i].IsChecked = options[i].Value == selected;
            }
            // Valeur inconnue (ancien fichier de paramètres) : on prend la 3e option
            if (buttons.All(b => b.IsChecked != true)) buttons[2].IsChecked = true;
            _syncingSettings = false;

            txtConvert.Text = string.Format(Loc.T("ConvertTo"), IsMp3 ? "MP3" : "MP4");
        }

        private void Format_Changed(object sender, RoutedEventArgs e)
        {
            if (_syncingSettings || rbMp3 == null || rbQ1 == null) return;
            Settings.Format = IsMp3 ? "mp3" : "mp4";
            Settings.Save();
            RefreshQualityButtons();
        }

        private void Quality_Changed(object sender, RoutedEventArgs e)
        {
            if (_syncingSettings || sender is not RadioButton rb || rb.Tag is not string value) return;
            if (IsMp3) Settings.AudioQuality = value;
            else Settings.VideoQuality = value;
            Settings.Save();
        }

        // =====================================================================
        //  Plateforme détectée : badge + couleur d'accent
        // =====================================================================

        private void SetPlatform(Platform platform)
        {
            if (platform == _platform) return;
            _platform = platform;

            platformGlyph.Text = PlatformInfo.Glyph(platform);
            var logo = GetLogo(platform);
            platformLogo.Source = logo;
            platformLogo.Visibility = logo != null ? Visibility.Visible : Visibility.Collapsed;
            platformGlyph.Visibility = logo != null ? Visibility.Collapsed : Visibility.Visible;
            platformBadge.ToolTip = platform == Platform.None ? null : PlatformInfo.Name(platform);

            if (logo != null)
            {
                platformBadge.Background = Brushes.Transparent;
            }
            else if (platform == Platform.None)
            {
                platformBadge.SetResourceReference(Border.BackgroundProperty, "SubtleBrush");
                platformGlyph.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            }
            else
            {
                platformBadge.Background = new SolidColorBrush(PlatformInfo.Accent(platform));
                platformGlyph.SetResourceReference(TextBlock.ForegroundProperty, "AccentForegroundBrush");
            }

            AnimateAccent(PlatformInfo.Accent(platform));
        }

        // Logos chargés depuis Assets/Logos/<nom>.png (intégrés à l'exe), gardés en cache
        private static readonly Dictionary<Platform, ImageSource?> LogoCache = new();

        private static ImageSource? GetLogo(Platform platform)
        {
            if (platform == Platform.None) return null;
            if (LogoCache.TryGetValue(platform, out var cached)) return cached;

            ImageSource? logo = null;
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri($"pack://application:,,,/Assets/Logos/{PlatformInfo.FileKey(platform)}.png");
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                logo = bitmap;
            }
            catch
            {
                // Fichier absent : on garde la pastille colorée
            }

            LogoCache[platform] = logo;
            return logo;
        }

        // Icône de la barre de titre : la plus grande image contenue dans app.ico (si présent)
        private void LoadTitleIcon()
        {
            try
            {
                var decoder = BitmapDecoder.Create(new Uri("pack://application:,,,/app.ico"),
                    BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                titleIcon.Source = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
                titleFallback.Visibility = Visibility.Collapsed;
            }
            catch
            {
                // Pas d'app.ico : on garde le logo de secours
            }
        }

        // Transition douce : on remplace le pinceau à chaque étape (animer un pinceau partagé fait planter WPF)
        private async void AnimateAccent(Color target)
        {
            int version = ++_accentVersion;
            Color from = _accentColor;
            const int steps = 20;

            for (int i = 1; i <= steps; i++)
            {
                if (version != _accentVersion) return;
                double t = 1 - Math.Pow(1 - (double)i / steps, 3);
                _accentColor = Color.FromRgb(
                    (byte)(from.R + (target.R - from.R) * t),
                    (byte)(from.G + (target.G - from.G) * t),
                    (byte)(from.B + (target.B - from.B) * t));
                SetAccentBrushes(_accentColor);
                await Task.Delay(16);
            }
        }

        private bool? _accentIsLight;

        private void SetAccentBrushes(Color accent)
        {
            Resources["AccentBrush"] = new SolidColorBrush(accent);

            // Accent clair (ex : blanc) -> texte foncé par-dessus, sinon texte blanc
            bool isLight = 0.299 * accent.R + 0.587 * accent.G + 0.114 * accent.B > 186;
            if (isLight != _accentIsLight)
            {
                _accentIsLight = isLight;
                Resources["AccentForegroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(0x11, 0x11, 0x14) : Colors.White);
            }
        }

        // =====================================================================
        //  Actions principales
        // =====================================================================

        private async void BtnAnalyze_Click(object sender, RoutedEventArgs e) => await Analyze();

        private async void BtnConvert_Click(object sender, RoutedEventArgs e) => await ConvertCurrent();

        private async void TxtUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await Analyze();
            }
        }

        private void TxtUrl_TextChanged(object sender, TextChangedEventArgs e)
            => SetPlatform(PlatformInfo.Detect(txtUrl.Text));

        private async void Paste_Click(object sender, RoutedEventArgs e)
        {
            string text = TryGetClipboardText();
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowToast(Loc.T("ClipboardEmpty"), ToastKind.Warning);
                return;
            }
            txtUrl.Text = text.Trim();
            txtUrl.CaretIndex = txtUrl.Text.Length;
            await Analyze();
        }

        private void ToastAction_Click(object sender, RoutedEventArgs e) => _toastAction?.Invoke();

        // Raccourcis : Ctrl+S = convertir, Échap = fermer les paramètres
        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && settingsOverlay.Visibility == Visibility.Visible)
            {
                e.Handled = true;
                HideSettings();
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S && !_isBusy)
            {
                e.Handled = true;
                await ConvertCurrent();
            }
        }

        // Lien dans le presse-papiers quand on revient sur l'app : on le propose
        private void Window_Activated(object? sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(txtUrl.Text) || _isBusy) return;

                string text = TryGetClipboardText().Trim();
                if (text.Length == 0 || text.Length > 500 || text == _lastClipboardSuggestion) return;
                if (PlatformInfo.Detect(text) == Platform.None) return;

                _lastClipboardSuggestion = text;
                txtUrl.Text = text;
                txtUrl.CaretIndex = text.Length;
                ShowToast(Loc.T("LinkDetected"), ToastKind.Info, Loc.T("Analyze"), async () => await Analyze());
            }
            catch
            {
                // Fonction bonus : elle ne doit jamais faire planter l'app
            }
        }

        // Glisser-déposer d'un lien
        private void Window_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.Text)
                ? DragDropEffects.Link
                : DragDropEffects.None;
            e.Handled = true;
        }

        private async void Window_PreviewDrop(object sender, DragEventArgs e)
        {
            string? text = (e.Data.GetData(DataFormats.UnicodeText) ?? e.Data.GetData(DataFormats.Text)) as string;
            e.Handled = true;
            if (string.IsNullOrWhiteSpace(text) || _isBusy) return;

            txtUrl.Text = text.Trim().Split('\n')[0].Trim();
            await Analyze();
        }

        // Fermeture pendant une conversion : on arrête yt-dlp proprement
        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e) => _convertCts?.Cancel();

        // =====================================================================
        //  Paramètres
        // =====================================================================

        private void SyncSettingsUi()
        {
            _syncingSettings = true;
            rbDark.IsChecked = Settings.DarkTheme;
            rbLight.IsChecked = !Settings.DarkTheme;
            rbFr.IsChecked = Settings.Language != "en";
            rbEn.IsChecked = Settings.Language == "en";
            chkAutoUpdate.IsChecked = Settings.AutoCheckUpdates;
            txtOutputFolder.Text = Settings.OutputFolder;
            txtOutputFolder.ToolTip = Settings.OutputFolder;
            txtVersion.Text = $"LinkConvertisor v{UpdateService.CurrentVersion}";
            _syncingSettings = false;
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (_syncingSettings) return;

            bool dark = rbDark.IsChecked == true;
            string lang = rbEn.IsChecked == true ? "en" : "fr";

            bool themeChanged = dark != Settings.DarkTheme;
            if (themeChanged) Theme.Apply(dark);
            if (lang != Settings.Language) Loc.Apply(lang);

            Settings.DarkTheme = dark;
            // La couleur d'Apple Music dépend du thème : on la recalcule
            if (themeChanged) AnimateAccent(PlatformInfo.Accent(_platform));
            Settings.Language = lang;
            Settings.AutoCheckUpdates = chkAutoUpdate.IsChecked == true;
            Settings.Save();

            // Le texte du bouton "Convertir en …" vient du code : on le retraduit
            txtConvert.Text = string.Format(Loc.T("ConvertTo"), IsMp3 ? "MP3" : "MP4");
            _ = RefreshEngineText();
        }

        private void ChangeFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = Loc.T("OutputFolder"),
                InitialDirectory = Directory.Exists(Settings.OutputFolder) ? Settings.OutputFolder : ""
            };
            if (dialog.ShowDialog(this) != true) return;

            Settings.OutputFolder = dialog.FolderName;
            Settings.Save();
            SyncSettingsUi();
        }

        private async Task RefreshEngineText()
        {
            string? version = await ToolsService.GetYtDlpVersionAsync();
            txtEngine.Text = version == null || !ToolsService.IsReady
                ? Loc.T("EngineMissing")
                : string.Format(Loc.T("EngineVersion"), version);
        }

        private async void EngineUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            btnEngineUpdate.IsEnabled = false;
            SetBusy(true);
            try
            {
                var status = new Progress<string>(s => txtEngine.Text = s);
                ShowToast(Loc.T("EngineUpdating"), ToastKind.Info);
                if (ToolsService.IsReady) await ToolsService.UpdateYtDlpAsync(status, CancellationToken.None);
                else await ToolsService.EnsureAsync(status, CancellationToken.None);
                ShowToast(Loc.T("EngineUpdated"), ToastKind.Success);
            }
            catch (Exception ex)
            {
                ShowToast(Loc.T("ErrTools") + ex.Message, ToastKind.Error);
            }
            finally
            {
                SetBusy(false);
                btnEngineUpdate.IsEnabled = true;
                await RefreshEngineText();
            }
        }

        private async void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            settingsOverlay.Visibility = Visibility.Visible;
            settingsOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)));
            await RefreshEngineText();
        }

        private void CloseSettings_Click(object sender, RoutedEventArgs e) => HideSettings();

        private void SettingsOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Clic sur le fond sombre (pas sur la carte) = fermer
            if (e.OriginalSource == settingsOverlay) HideSettings();
        }

        private void HideSettings()
        {
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
            fade.Completed += (_, _) =>
            {
                if (settingsOverlay.Opacity == 0) settingsOverlay.Visibility = Visibility.Collapsed;
            };
            settingsOverlay.BeginAnimation(OpacityProperty, fade);
        }

        // =====================================================================
        //  Mises à jour de l'app
        // =====================================================================

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e) => await CheckForUpdates(silent: false);

        private async Task CheckForUpdates(bool silent)
        {
            if (!UpdateService.IsConfigured)
            {
                if (!silent) ShowToast(Loc.T("UpdateNotConfigured"), ToastKind.Warning);
                return;
            }

            btnCheckUpdates.IsEnabled = false;
            try
            {
                var update = await UpdateService.CheckAsync(Http);
                if (update == null)
                {
                    if (!silent) ShowToast(Loc.T("UpToDate"), ToastKind.Success);
                    return;
                }

                ShowToast(string.Format(Loc.T("UpdateAvailable"), "v" + update.Version), ToastKind.Info,
                    Loc.T("UpdateAction"), async () =>
                    {
                        try
                        {
                            ShowToast(Loc.T("UpdateDownloading"), ToastKind.Info);
                            await UpdateService.InstallAsync(Http, update);
                        }
                        catch
                        {
                            ShowToast(Loc.T("UpdateFailed"), ToastKind.Error);
                        }
                    });
            }
            catch
            {
                if (!silent) ShowToast(Loc.T("UpdateFailed"), ToastKind.Error);
            }
            finally
            {
                btnCheckUpdates.IsEnabled = true;
            }
        }

        // =====================================================================
        //  Fenêtre sans bordure
        // =====================================================================

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
            => Root.Clip = new RectangleGeometry(new Rect(e.NewSize), 18, 18);

        private void PicPreview_SizeChanged(object sender, SizeChangedEventArgs e)
            => picPreview.Clip = new RectangleGeometry(new Rect(e.NewSize), 12, 12);

        // =====================================================================
        //  Toast
        // =====================================================================

        private async void ShowToast(string message, ToastKind kind, string? actionText = null, Action? action = null)
        {
            int version = ++_toastVersion;

            var (glyph, color) = kind switch
            {
                ToastKind.Success => ("", Color.FromRgb(0x22, 0xC5, 0x5E)),
                ToastKind.Warning => ("", Color.FromRgb(0xF5, 0xA5, 0x24)),
                ToastKind.Error => ("", Color.FromRgb(0xF4, 0x3F, 0x5E)),
                _ => ("", _accentColor)
            };
            toastIcon.Text = glyph;
            toastIcon.Foreground = new SolidColorBrush(color);
            toastText.Text = message;

            _toastAction = action;
            toastAction.Content = actionText;
            toastAction.Visibility = action != null ? Visibility.Visible : Visibility.Collapsed;
            toast.IsHitTestVisible = true;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            toast.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(200)));
            toastShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(250)) { EasingFunction = ease });

            await Task.Delay(action != null ? 6000 : kind == ToastKind.Error ? 4500 : 2800);
            if (version != _toastVersion) return; // un autre toast a pris la place

            toast.IsHitTestVisible = false;
            toast.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(250)));
            toastShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(12, TimeSpan.FromMilliseconds(250)));
        }

        // =====================================================================
        //  Utilitaires
        // =====================================================================

        private static string TryGetClipboardText()
        {
            try { return Clipboard.ContainsText() ? Clipboard.GetText() : ""; }
            catch { return ""; }
        }

        private static string FormatDuration(double? seconds)
        {
            if (seconds is null or <= 0) return "--:--";
            var t = TimeSpan.FromSeconds(seconds.Value);
            return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
        }

        private static string FormatSpeed(double? bytesPerSecond)
        {
            if (bytesPerSecond is null or <= 0) return "";
            string unit = Loc.Language == "fr" ? "o" : "B";
            double v = bytesPerSecond.Value;
            return v >= 1048576 ? $"{v / 1048576:0.0} M{unit}/s" : $"{v / 1024:0} K{unit}/s";
        }
    }
}
