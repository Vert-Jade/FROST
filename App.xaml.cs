using System.Windows;
using System;

namespace FROST
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.DispatcherUnhandledException += (sender, args) =>
            {
                string msg = args.Exception.InnerException?.Message ?? args.Exception.Message;
                FROST.MainWindow.Log($"CRASH CRITIQUE : {msg}\n{args.Exception.StackTrace}");
                MessageBox.Show("Le lancement a échoué car un fichier est introuvable ou mal nommé :\n\n" + msg, 
                                "Erreur Critique - FROST", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
                Application.Current.Shutdown();
            };

            // Chargement dynamique de la langue par défaut depuis la racine
            try
            {
                ResourceDictionary dict = new ResourceDictionary();
                dict.Source = new Uri("pack://application:,,,/Langues/fr.xaml", UriKind.Absolute);
                Application.Current.Resources.MergedDictionaries.Add(dict);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossible de charger la langue par défaut (fr.xaml).\n" + ex.Message, "Erreur Critique", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}