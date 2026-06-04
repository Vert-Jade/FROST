using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace FROST;

public partial class UninstallWindow : Window
{
    private bool _isRunning;

    public UninstallWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FlowDirection = App.CurrentLanguageCode == "ar"
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
        TxtInternalFolderPath.Text = FrostUninstallService.AppDataDirectory;
    }

    private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
            return;

        _isRunning = true;
        IntroPanel.Visibility = Visibility.Collapsed;
        FinishPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        BtnUninstall.IsEnabled = false;
        BtnCancel.IsEnabled = false;

        FrostUninstallTexts texts = BuildTexts();
        bool removeUserData = ChkRemoveUserData.IsChecked == true;
        var progress = new Progress<(double Pct, string Status)>(report =>
        {
            UninstallProgress.Value = report.Pct;
            TxtUninstallPercent.Text = $"{(int)report.Pct} %";
            TxtUninstallStatus.Text = report.Status;
        });

        try
        {
            await Task.Run(() => FrostUninstallService.Run(removeUserData, progress, texts));
            ShowFinish(success: true, titleKey: "TxtUninstallCompleteTitle", descKey: "TxtUninstallCompleteDesc");
        }
        catch (Exception ex)
        {
            TxtFinishDesc.Text = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                GetText("TxtUninstallFailedDesc", "FROST n'a pas pu être complètement désinstallé.\n\n{0}"),
                ex.Message);
            ShowFinish(success: false, titleKey: "TxtUninstallFailedTitle", descKey: null);
        }
        finally
        {
            _isRunning = false;
        }
    }

    private void ShowFinish(bool success, string titleKey, string? descKey)
    {
        ProgressPanel.Visibility = Visibility.Collapsed;
        FinishPanel.Visibility = Visibility.Visible;

        FinishIconCircle.Background = new SolidColorBrush(success ? Color.FromRgb(0x0C, 0x20, 0x18) : Color.FromRgb(0x2A, 0x12, 0x16));
        FinishIconCircle.BorderBrush = new SolidColorBrush(success ? Color.FromRgb(0x22, 0xC5, 0x5E) : Color.FromRgb(0xEF, 0x44, 0x44));
        FinishIconText.Text = success ? "✓" : "!";
        FinishIconText.Foreground = new SolidColorBrush(success ? Color.FromRgb(0x22, 0xC5, 0x5E) : Color.FromRgb(0xEF, 0x44, 0x44));

        TxtFinishTitle.Text = GetText(titleKey, success ? "Désinstallation terminée" : "Désinstallation incomplète");
        if (descKey != null)
        {
            TxtFinishDesc.Text = GetText(descKey, "FROST a été supprimé de ce PC.");
        }
    }

    private FrostUninstallTexts BuildTexts()
    {
        return new FrostUninstallTexts(
            GetText("StatusUninstallPreparing", FrostUninstallTexts.Default.Preparing),
            GetText("StatusUninstallClosingApp", FrostUninstallTexts.Default.ClosingApp),
            GetText("StatusUninstallShortcuts", FrostUninstallTexts.Default.Shortcuts),
            GetText("StatusUninstallRegistry", FrostUninstallTexts.Default.Registry),
            GetText("StatusUninstallFiles", FrostUninstallTexts.Default.Files),
            GetText("StatusUninstallDone", FrostUninstallTexts.Default.Done),
            GetText("StatusUninstallScheduled", FrostUninstallTexts.Default.Scheduled));
    }

    private static string GetText(string key, string fallback)
    {
        return Application.Current?.Resources[key] as string ?? fallback;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRunning)
            Application.Current.Shutdown();
    }

    private void BtnCloseFinish_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRunning)
            Application.Current.Shutdown();
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}
