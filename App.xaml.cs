using System.Windows;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FROST
{
    public partial class App : Application
    {
        internal static string CurrentLanguageCode { get; private set; } = "fr";

        protected override void OnStartup(StartupEventArgs e)
        {
            this.DispatcherUnhandledException += (sender, args) =>
            {
                string msg = args.Exception.InnerException?.Message ?? args.Exception.Message;
                FROST.MainWindow.Log($"CRASH CRITIQUE : {msg}\n{args.Exception.StackTrace}");
                MessageBox.Show("Le lancement a échoué car un fichier est introuvable ou mal nommé :\n\n" + msg, 
                                "Erreur Critique - FROST", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
                Application.Current.Shutdown();
            };

            try
            {
                base.OnStartup(e);

                bool uninstallMode = e.Args.Any(arg => string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase));
                bool quietMode = e.Args.Any(arg => string.Equals(arg, "--quiet", StringComparison.OrdinalIgnoreCase));

                LoadLanguage(uninstallMode ? ResolveSavedLanguageCode() : "fr");

                if (uninstallMode && quietMode)
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    _ = Task.Run(() =>
                    {
                        int exitCode = 0;
                        try
                        {
                            FrostUninstallService.Run(removeUserData: false, progress: null, texts: FrostUninstallTexts.Default);
                        }
                        catch
                        {
                            exitCode = 1;
                        }

                        Dispatcher.Invoke(() => Shutdown(exitCode));
                    });
                    return;
                }

                MainWindow = uninstallMode
                    ? new UninstallWindow()
                    : new MainWindow();
                MainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "FROST n'a pas pu démarrer :\n\n" + ex.Message,
                    "Erreur Critique - FROST",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private static void LoadLanguage(string code)
        {
            CurrentLanguageCode = string.IsNullOrWhiteSpace(code) ? "fr" : code;
            ResourceDictionary dict = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Langues/{CurrentLanguageCode}.xaml", UriKind.Absolute)
            };

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        private static string ResolveSavedLanguageCode()
        {
            try
            {
                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FROST",
                    "grid_config.txt");

                if (!File.Exists(configPath))
                {
                    return "fr";
                }

                string[] parts = File.ReadAllText(configPath).Split(';');
                if (parts.Length < 6 || !int.TryParse(parts[5], out int langIdx))
                {
                    return "fr";
                }

                return langIdx switch
                {
                    1 => "en",
                    2 => "pt",
                    3 => "es",
                    4 => "it",
                    5 => "de",
                    6 => "ar",
                    7 => "nl",
                    8 => "pl",
                    9 => "ru",
                    10 => "sv",
                    11 => "tr",
                    _ => "fr"
                };
            }
            catch
            {
                return "fr";
            }
        }
    }
}
