using Microsoft.Win32;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Resources;

namespace FROST.Setup;

public partial class MainWindow : Window
{
    // ── Language definitions ──────────────────────────────────────────────
    private record LangDef(string Code, string Flag, string NameKey);

    private static readonly LangDef[] Languages =
    [
        new("fr", "fr", "TxtLangFr"),
        new("en", "en", "TxtLangEn"),
        new("de", "de", "TxtLangDe"),
        new("es", "es", "TxtLangEs"),
        new("it", "it", "TxtLangIt"),
        new("nl", "nl", "TxtLangNl"),
        new("pl", "pl", "TxtLangPl"),
        new("pt", "pt", "TxtLangPt"),
        new("ru", "ru", "TxtLangRu"),
        new("sv", "sv", "TxtLangSv"),
        new("tr", "tr", "TxtLangTr"),
        new("ar", "ar", "TxtLangAr"),
    ];

    // ── State ─────────────────────────────────────────────────────────────
    private int _step = 0;
    private string _langCode = "fr";
    private Button? _activeLangBtn;
    private bool _installing = false;

    // ── Sidebar element arrays for easy indexing ──────────────────────────
    private Border[] _circles = null!;
    private TextBlock[] _labels = null!;
    private TextBlock[] _nums = null!;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _circles = [SideCircle0, SideCircle1, SideCircle2, SideCircle3, SideCircle4];
        _labels  = [SideLabel0,  SideLabel1,  SideLabel2,  SideLabel3,  SideLabel4];
        _nums    = [SideNum0,    SideNum1,    SideNum2,    SideNum3,    SideNum4];

        LoadLanguage("fr");
        BuildLanguageGrid();
        TxtPath.Text = DefaultInstallPath();
        TxtDiskSpace.Text = (string?)TryResource("DiskSpaceRequired") ?? "~8 MB";
        ApplyStep();
    }

    // ══════════════════════════════════════════════════════════════════════
    // LANGUAGE
    // ══════════════════════════════════════════════════════════════════════

    private void LoadLanguage(string code)
    {
        _langCode = code;
        var uri = new Uri($"pack://application:,,,/Languages/{code}.xaml");
        var dict = new ResourceDictionary { Source = uri };

        var merged = Application.Current.Resources.MergedDictionaries;
        // Remove any existing language dict
        var old = merged.FirstOrDefault(d => d.Source?.OriginalString.Contains("/Languages/") == true);
        if (old != null) merged.Remove(old);
        merged.Add(dict);

        // Update window title / subtitle in sidebar
        TxtSubtitle.Text = TryResource("InstallerLabel") as string ?? "Installateur";
        TxtDiskSpace.Text = TryResource("DiskSpaceRequired") as string ?? "~8 MB";

        // RTL support for Arabic
        FlowDirection = code == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    private void BuildLanguageGrid()
    {
        LangGrid.Children.Clear();
        foreach (var lang in Languages)
        {
            var btn = new Button { Style = (Style)FindResource("LangBtnStyle"), Tag = lang.Code };

            var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            // Flag image
            var flagUri = new Uri($"pack://application:,,,/Resources/Flags/{lang.Flag}.png");
            StreamResourceInfo? sri = null;
            try { sri = Application.GetResourceStream(flagUri); } catch { }

            if (sri != null)
            {
                var img = new Image
                {
                    Width = 44,
                    Height = 30,
                    Margin = new Thickness(0, 0, 0, 6),
                    Source = new System.Windows.Media.Imaging.BitmapImage(flagUri),
                };
                panel.Children.Add(img);
            }

            // Language name
            var label = new TextBlock
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA5, 0xB5)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            label.SetResourceReference(TextBlock.TextProperty, lang.NameKey);
            panel.Children.Add(label);

            btn.Content = panel;
            btn.Click += LangBtn_Click;

            // Pre-select French
            if (lang.Code == "fr") ApplyLangSelection(btn);

            LangGrid.Children.Add(btn);
        }
    }

    private void LangBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        ApplyLangSelection(btn);
        LoadLanguage((string)btn.Tag);
        BtnNext.IsEnabled = true;
    }

    private void ApplyLangSelection(Button btn)
    {
        // Reset previous
        if (_activeLangBtn != null)
        {
            _activeLangBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x42));
            _activeLangBtn.Background = Brushes.Transparent;
        }
        // Highlight new
        btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x8F, 0xEF, 0xFF));
        btn.Background = new SolidColorBrush(Color.FromArgb(0x24, 0x8F, 0xEF, 0xFF));
        _activeLangBtn = btn;
    }

    // ══════════════════════════════════════════════════════════════════════
    // NAVIGATION
    // ══════════════════════════════════════════════════════════════════════

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_step < 4) GoToStep(_step + 1);
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (_step > 0) GoToStep(_step - 1);
    }

    private void GoToStep(int target)
    {
        // Step 3 = Install; trigger the actual install
        if (target == 3 && _step == 2)
        {
            StartInstall();
            return;
        }

        _step = target;
        ApplyStep();
    }

    private void ApplyStep()
    {
        // Hide all content panels
        StepLang.Visibility    = Visibility.Collapsed;
        StepWelcome.Visibility = Visibility.Collapsed;
        StepPath.Visibility    = Visibility.Collapsed;
        StepInstall.Visibility = Visibility.Collapsed;
        StepFinish.Visibility  = Visibility.Collapsed;

        // Show current
        var panels = new[] { StepLang, StepWelcome, StepPath, StepInstall, StepFinish };
        FadeIn(panels[_step]);
        panels[_step].Visibility = Visibility.Visible;

        // Update sidebar
        UpdateSidebar();

        // Footer buttons
        BtnBack.Visibility = _step > 0 && _step < 4 ? Visibility.Visible : Visibility.Hidden;
        BtnNext.IsEnabled  = _step != 0 || _activeLangBtn != null;

        // Last step: swap Next for Finish
        if (_step == 4)
        {
            BtnBack.Visibility = Visibility.Hidden;
            BtnNext.Content    = TryResource("BtnFinish") ?? "Terminer";
            BtnNext.IsEnabled  = true;
            BtnNext.Click     -= BtnNext_Click;
            BtnNext.Click     -= BtnFinish_Click;
            BtnNext.Click     += BtnFinish_Click;
        }
        // Install step: swap Next for Install label (disabled; install starts automatically)
        else if (_step == 3)
        {
            BtnNext.IsEnabled = false;
            BtnBack.Visibility = Visibility.Hidden;
        }
        else
        {
            BtnNext.Click -= BtnFinish_Click;
            BtnNext.Click -= BtnNext_Click;
            BtnNext.Click += BtnNext_Click;
            BtnNext.Content = _step == 2
                ? (TryResource("BtnInstall") ?? "Installer →")
                : (TryResource("BtnNext")    ?? "Suivant →");
        }
    }

    private void UpdateSidebar()
    {
        var frost  = new SolidColorBrush(Color.FromRgb(0x8F, 0xEF, 0xFF));
        var dim    = new SolidColorBrush(Color.FromRgb(0x25, 0x2A, 0x38));
        var green  = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
        var txtFrost  = new SolidColorBrush(Color.FromRgb(0x8F, 0xEF, 0xFF));
        var txtMuted  = new SolidColorBrush(Color.FromRgb(0x4A, 0x51, 0x68));
        var txtPrimary = new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB));

        for (int i = 0; i < 5; i++)
        {
            if (i < _step)
            {
                // Completed
                _circles[i].Background = green;
                _nums[i].Text = "✓";
                _nums[i].Foreground = Brushes.White;
                _labels[i].Foreground = txtPrimary;
                _labels[i].FontWeight = FontWeights.Normal;
            }
            else if (i == _step)
            {
                // Active
                _circles[i].Background = frost;
                _nums[i].Text = (i + 1).ToString();
                _nums[i].Foreground = new SolidColorBrush(Color.FromRgb(0x0D, 0x0F, 0x14));
                _labels[i].Foreground = txtFrost;
                _labels[i].FontWeight = FontWeights.SemiBold;
            }
            else
            {
                // Pending
                _circles[i].Background = dim;
                _nums[i].Text = (i + 1).ToString();
                _nums[i].Foreground = txtMuted;
                _labels[i].Foreground = txtMuted;
                _labels[i].FontWeight = FontWeights.Normal;
            }
        }
    }

    private static void FadeIn(UIElement el)
    {
        el.Opacity = 0;
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        el.BeginAnimation(OpacityProperty, anim);
    }

    // ══════════════════════════════════════════════════════════════════════
    // BROWSE
    // ══════════════════════════════════════════════════════════════════════

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = TryResource("PathTitle") as string ?? "Choisir le dossier d'installation",
            InitialDirectory = TxtPath.Text,
        };
        if (dialog.ShowDialog() == true)
            TxtPath.Text = dialog.FolderName;
    }

    // ══════════════════════════════════════════════════════════════════════
    // INSTALLATION
    // ══════════════════════════════════════════════════════════════════════

    private void StartInstall()
    {
        if (_installing) return;
        _installing = true;

        _step = 3;
        ApplyStep();

        TxtInstallStatus.Text = TryResource("InstallPreparing") as string ?? "Préparation...";
        ProgressBarCtrl.Value = 0;
        TxtPercent.Text = "0 %";

        string installDir = TxtPath.Text;
        bool desktop   = ChkDesktop.IsChecked  == true;
        bool startMenu = ChkStartMenu.IsChecked == true;

        var progress = new Progress<(double pct, string status)>(report =>
        {
            ProgressBarCtrl.Value = report.pct;
            TxtPercent.Text = $"{(int)report.pct} %";
            TxtInstallStatus.Text = report.status;
        });

        Task.Run(() => RunInstall(installDir, desktop, startMenu, progress))
            .ContinueWith(t =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (t.IsFaulted)
                    {
                        MessageBox.Show(
                            $"L'installation a échoué :\n{t.Exception?.InnerException?.Message}",
                            "FROST Setup", MessageBoxButton.OK, MessageBoxImage.Error);
                        _installing = false;
                        GoToStep(2);
                    }
                    else
                    {
                        _step = 4;
                        ApplyStep();
                    }
                });
            });
    }

    private void RunInstall(string installDir, bool desktop, bool startMenu,
                            IProgress<(double, string)> progress)
    {
        string copying   = TryResourceSafe("InstallCopying")    ?? "Copie des fichiers...";
        string shortcuts = TryResourceSafe("InstallShortcuts")  ?? "Création des raccourcis...";
        string registering = TryResourceSafe("InstallRegistering") ?? "Enregistrement...";

        // 1. Create directory
        progress.Report((5, TryResourceSafe("InstallPreparing") ?? "Préparation..."));
        Directory.CreateDirectory(installDir);
        Thread.Sleep(300);

        // 2. Extract FROST.exe
        progress.Report((15, copying));
        string exeDest = Path.Combine(installDir, "FROST.exe");
        ExtractPayload(exeDest, progress, 15, 80);

        // 3. Shortcuts
        progress.Report((82, shortcuts));
        string exePath = exeDest;
        if (desktop)
        {
            string link = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "FROST.lnk");
            CreateShortcut(link, exePath, installDir);
        }
        if (startMenu)
        {
            string smDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "FROST");
            Directory.CreateDirectory(smDir);
            CreateShortcut(Path.Combine(smDir, "FROST.lnk"), exePath, installDir);
        }
        Thread.Sleep(300);

        // 4. Uninstaller shortcut + Registry
        progress.Report((92, registering));
        DeleteLegacyUninstallScript(installDir);
        CreateShortcut(Path.Combine(installDir, "Desinstaller FROST.lnk"), exePath, installDir, "--uninstall");
        WriteUninstallKey(installDir, exePath);
        Thread.Sleep(200);

        progress.Report((100, "✓"));
        Thread.Sleep(400);
    }

    private static void ExtractPayload(string destPath, IProgress<(double, string)> progress,
                                       double startPct, double endPct)
    {
        var asm    = Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream("FROST.exe");

        if (stream == null)
            throw new InvalidOperationException("FROST.exe n'est pas embarqué dans l'installateur.");

        long total = stream.Length;
        using var fs = File.Create(destPath);
        byte[] buffer = new byte[81920];
        long copied = 0;
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            fs.Write(buffer, 0, read);
            copied += read;
            double pct = startPct + (endPct - startPct) * copied / total;
            progress.Report((pct, $"Copie de FROST.exe... {(int)(copied * 100.0 / total)} %"));
        }
    }

    private static void CreateShortcut(string linkPath, string targetPath, string workDir, string arguments = "")
    {
        try
        {
            var wshType = Type.GetTypeFromProgID("WScript.Shell");
            if (wshType == null) return;
            dynamic shell    = Activator.CreateInstance(wshType)!;
            dynamic shortcut = shell.CreateShortcut(linkPath);
            shortcut.TargetPath       = targetPath;
            shortcut.Arguments        = arguments;
            shortcut.WorkingDirectory = workDir;
            shortcut.IconLocation     = targetPath;
            shortcut.Save();
        }
        catch { /* non-fatal */ }
    }

    private static void DeleteLegacyUninstallScript(string installDir)
    {
        try
        {
            string scriptPath = Path.Combine(installDir, "Uninstall-FROST.ps1");
            if (File.Exists(scriptPath))
                File.Delete(scriptPath);
        }
        catch { /* non-fatal */ }
    }

    private static void WriteUninstallKey(string installDir, string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\FROST");
            if (key == null) return;
            key.SetValue("DisplayName",     "FROST");
            key.SetValue("DisplayVersion",  "1.0.6");
            key.SetValue("Publisher",       "Dylan Fournier");
            key.SetValue("InstallLocation", installDir);
            key.SetValue("DisplayIcon",     exePath);
            key.SetValue("UninstallString", $"\"{exePath}\" --uninstall");
            key.SetValue("QuietUninstallString", $"\"{exePath}\" --uninstall --quiet");
            key.SetValue("NoModify",        1, RegistryValueKind.DWord);
            key.SetValue("NoRepair",        1, RegistryValueKind.DWord);
        }
        catch { /* non-fatal */ }
    }

    private void BtnFinish_Click(object sender, RoutedEventArgs e)
    {
        if (ChkLaunch.IsChecked == true)
        {
            string exe = Path.Combine(TxtPath.Text, "FROST.exe");
            if (File.Exists(exe))
            {
                try { System.Diagnostics.Process.Start(exe); } catch { }
            }
        }
        Application.Current.Shutdown();
    }

    // ══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════════════════════════════════

    private static string DefaultInstallPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FROST");

    private object? TryResource(string key)
    {
        try { return Application.Current.Resources[key]; } catch { return null; }
    }

    private string? TryResourceSafe(string key) => TryResource(key) as string;

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_installing)
        {
            var res = MessageBox.Show(
                "L'installation est en cours. Voulez-vous vraiment quitter ?",
                "FROST Setup", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;
        }
        Application.Current.Shutdown();
    }
}
