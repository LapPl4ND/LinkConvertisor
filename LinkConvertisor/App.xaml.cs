using System.Windows;
using System.Windows.Threading;

namespace LinkConvertisor
{
    public partial class App : Application
    {
        /// <summary>Paramètres chargés au démarrage, accessibles partout via App.Settings.</summary>
        public static AppSettings Settings { get; private set; } = new AppSettings();

        protected override void OnStartup(StartupEventArgs e)
        {
            // Filet de sécurité : au lieu de crasher, l'app affiche l'erreur et continue
            DispatcherUnhandledException += OnUnhandledException;

            Settings = AppSettings.Load();
            Theme.Apply(Settings.DarkTheme);
            Loc.Apply(Settings.Language);

            base.OnStartup(e);
        }

        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                Loc.T("ErrUnexpected") + "\n\n" + e.Exception.Message + "\n\n(" + e.Exception.GetType().Name + ")",
                "LinkConvertisor", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
