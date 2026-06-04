using Microsoft.Win32;
using System.Diagnostics;
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
    private const string FrostAppUserModelId = "VertJade.FROST";

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLinkComObject
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        [PreserveSig] int GetPath(IntPtr pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        [PreserveSig] int GetIDList(out IntPtr ppidl);
        [PreserveSig] int SetIDList(IntPtr pidl);
        [PreserveSig] int GetDescription(IntPtr pszName, int cchMaxName);
        [PreserveSig] int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        [PreserveSig] int GetWorkingDirectory(IntPtr pszDir, int cchMaxPath);
        [PreserveSig] int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        [PreserveSig] int GetArguments(IntPtr pszArgs, int cchMaxPath);
        [PreserveSig] int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        [PreserveSig] int GetHotkey(out short pwHotkey);
        [PreserveSig] int SetHotkey(short wHotkey);
        [PreserveSig] int GetShowCmd(out int piShowCmd);
        [PreserveSig] int SetShowCmd(int iShowCmd);
        [PreserveSig] int GetIconLocation(IntPtr pszIconPath, int cchIconPath, out int piIcon);
        [PreserveSig] int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        [PreserveSig] int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        [PreserveSig] int Resolve(IntPtr hwnd, uint fFlags);
        [PreserveSig] int SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        [PreserveSig] int GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        [PreserveSig] int Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        [PreserveSig] int Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName, bool fRemember);
        [PreserveSig] int SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName);
        [PreserveSig] int GetCurFile(out IntPtr ppszFileName);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("00000138-0000-0000-C000-000000000046")]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PropertyKey pkey);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant pv);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant pv);
        [PreserveSig] int Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;

        public PropertyKey(Guid formatId, uint propertyId)
        {
            FormatId = formatId;
            PropertyId = propertyId;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant : IDisposable
    {
        [FieldOffset(0)]
        private ushort _valueType;

        [FieldOffset(8)]
        private IntPtr _pointerValue;

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant pvar);

        public static PropVariant FromString(string value)
        {
            return new PropVariant
            {
                _valueType = 31,
                _pointerValue = Marshal.StringToCoTaskMemUni(value)
            };
        }

        public void Dispose()
        {
            PropVariantClear(ref this);
        }
    }

    private static readonly PropertyKey AppUserModelIdPropertyKey = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        5);

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
        LoadExistingOptions();
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
        StepPath.Visibility    = Visibility.Collapsed;
        StepOptions.Visibility = Visibility.Collapsed;
        StepInstall.Visibility = Visibility.Collapsed;
        StepFinish.Visibility  = Visibility.Collapsed;

        // Show current
        var panels = new[] { StepLang, StepPath, StepOptions, StepInstall, StepFinish };
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
        {
            TxtPath.Text = dialog.FolderName;
            LoadExistingOptions();
        }
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
        bool desktop     = ChkDesktop.IsChecked == true;
        bool startup     = ChkStartup.IsChecked == true;
        bool closeToTray = ChkCloseToTray.IsChecked == true;

        var progress = new Progress<(double pct, string status)>(report =>
        {
            ProgressBarCtrl.Value = report.pct;
            TxtPercent.Text = $"{(int)report.pct} %";
            TxtInstallStatus.Text = report.status;
        });

        Task.Run(() => RunInstall(installDir, desktop, startup, closeToTray, progress))
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

    private void RunInstall(string installDir, bool desktop, bool startup, bool closeToTray,
                            IProgress<(double, string)> progress)
    {
        string copying   = TryResourceSafe("InstallCopying")    ?? "Copie des fichiers...";
        string closing   = TryResourceSafe("InstallClosingFrost") ?? "Fermeture de FROST...";
        string shortcuts = TryResourceSafe("InstallShortcuts")  ?? "Création des raccourcis...";
        string registering = TryResourceSafe("InstallRegistering") ?? "Enregistrement...";

        // 1. Create directory
        progress.Report((5, TryResourceSafe("InstallPreparing") ?? "Préparation..."));
        Directory.CreateDirectory(installDir);
        Thread.Sleep(300);

        // 2. Preserve user data and replace only the executable payload.
        string exeDest = Path.Combine(installDir, "FROST.exe");
        CloseRunningFrostInstances(exeDest, progress, closing);
        progress.Report((15, copying));
        ExtractPayload(exeDest, progress, 15, 80);

        // 3. Shortcuts
        progress.Report((82, shortcuts));
        string exePath = exeDest;
        string desktopShortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "FROST.lnk");
        string startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "FROST");

        DeleteLegacyUninstallScript(installDir);
        EnsureUninstallerShortcut(installDir, exePath);
        WriteInstallerOptions(installDir, closeToTray);

        if (desktop)
        {
            CreateShortcut(desktopShortcut, exePath, installDir);
        }
        else
        {
            TryDeleteFile(desktopShortcut);
        }

        CleanupLegacyPinShortcuts(startMenuDir);
        ConfigureStartup(startup, exePath);
        Thread.Sleep(300);

        // 4. Uninstaller shortcut + Registry
        progress.Report((92, registering));
        EnsureUninstallerShortcut(installDir, exePath);
        WriteUninstallKey(installDir, exePath);
        Thread.Sleep(200);

        progress.Report((100, "✓"));
        Thread.Sleep(400);
    }

    private static void ExtractPayload(string destPath, IProgress<(double, string)> progress,
                                       double startPct, double endPct)
    {
        string? destDir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrWhiteSpace(destDir))
            Directory.CreateDirectory(destDir);

        string tempPath = destPath + ".installing";
        Exception? lastError = null;
        const int maxAttempts = 10;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                TryDeleteFile(tempPath);

                using Stream stream = OpenPayloadStream();
                long total = stream.Length;
                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
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
                    fs.Flush(true);
                }

                WaitForFileRelease(destPath, TimeSpan.FromSeconds(3));
                if (File.Exists(destPath))
                    File.Delete(destPath);

                File.Move(tempPath, destPath);
                return;
            }
            catch (Exception ex) when (IsRetriableFileError(ex) && attempt < maxAttempts)
            {
                lastError = ex;
                TryDeleteFile(tempPath);
                Thread.Sleep(500);
            }
        }

        throw new IOException(
            "Impossible de remplacer FROST.exe. Fermez FROST puis relancez l'installation.",
            lastError);
    }

    private static Stream OpenPayloadStream()
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FROST.exe");
        return stream ?? throw new InvalidOperationException("FROST.exe n'est pas embarqué dans l'installateur.");
    }

    private static void CloseRunningFrostInstances(string targetExePath,
                                                  IProgress<(double, string)> progress,
                                                  string status)
    {
        string targetFullPath = NormalizePath(targetExePath);
        bool foundRunningInstance = false;

        foreach (Process process in Process.GetProcessesByName("FROST"))
        {
            try
            {
                string? processPath = TryGetProcessPath(process);
                if (string.IsNullOrWhiteSpace(processPath) ||
                    !PathsEqual(processPath, targetFullPath))
                {
                    continue;
                }

                if (!foundRunningInstance)
                {
                    progress.Report((12, status));
                    foundRunningInstance = true;
                }

                if (process.CloseMainWindow() && process.WaitForExit(3500))
                    continue;

                process.Kill(entireProcessTree: true);
                process.WaitForExit(3500);
            }
            catch
            {
                // Best effort: the retry loop below still guards the file replacement.
            }
            finally
            {
                process.Dispose();
            }
        }

        if (foundRunningInstance)
            WaitForFileRelease(targetExePath, TimeSpan.FromSeconds(12));
    }

    private static string? TryGetProcessPath(Process process)
    {
        try { return process.MainModule?.FileName; }
        catch { return null; }
    }

    private static void WaitForFileRelease(string path, TimeSpan timeout)
    {
        if (!File.Exists(path))
            return;

        Stopwatch stopwatch = Stopwatch.StartNew();
        Exception? lastError = null;

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return;
            }
            catch (Exception ex) when (IsRetriableFileError(ex))
            {
                lastError = ex;
                Thread.Sleep(250);
            }
        }

        throw new IOException(
            "FROST.exe est encore utilisé par Windows. Fermez FROST puis relancez l'installation.",
            lastError);
    }

    private static bool IsRetriableFileError(Exception ex) =>
        ex is IOException || ex is UnauthorizedAccessException;

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    private static bool CreateShortcut(string linkPath, string targetPath, string workDir, string arguments = "")
    {
        bool created = TryCreateShortcutWithShellLink(linkPath, targetPath, workDir, arguments)
            || TryCreateShortcutWithWScript(linkPath, targetPath, workDir, arguments);

        if (created)
            SetShortcutAppUserModelId(linkPath);

        return created && File.Exists(linkPath);
    }

    private static bool TryCreateShortcutWithShellLink(string linkPath, string targetPath, string workDir, string arguments)
    {
        object? shellLink = null;

        try
        {
            string? directory = Path.GetDirectoryName(linkPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            shellLink = new ShellLinkComObject();
            var link = (IShellLinkW)shellLink;
            link.SetPath(targetPath);
            link.SetArguments(arguments);
            link.SetWorkingDirectory(workDir);
            link.SetIconLocation(targetPath, 0);

            var persistFile = (IPersistFile)shellLink;
            return persistFile.Save(linkPath, true) >= 0 && File.Exists(linkPath);
        }
        catch { /* non-fatal */ }
        finally
        {
            if (shellLink != null && Marshal.IsComObject(shellLink))
                Marshal.FinalReleaseComObject(shellLink);
        }

        return false;
    }

    private static bool TryCreateShortcutWithWScript(string linkPath, string targetPath, string workDir, string arguments)
    {
        try
        {
            string? directory = Path.GetDirectoryName(linkPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var wshType = Type.GetTypeFromProgID("WScript.Shell");
            if (wshType == null)
                return false;

            dynamic shell = Activator.CreateInstance(wshType)!;
            dynamic shortcut = shell.CreateShortcut(linkPath);
            shortcut.TargetPath = targetPath;
            shortcut.Arguments = arguments;
            shortcut.WorkingDirectory = workDir;
            shortcut.IconLocation = $"{targetPath},0";
            shortcut.Save();
            return File.Exists(linkPath);
        }
        catch { /* non-fatal */ }

        return false;
    }

    private static void EnsureUninstallerShortcut(string installDir, string exePath)
    {
        string shortcutPath = Path.Combine(installDir, "Desinstaller FROST.lnk");
        if (CreateShortcut(shortcutPath, exePath, installDir, "--uninstall"))
            return;

        throw new IOException("Le raccourci de désinstallation FROST n'a pas pu être créé.");
    }

    private static void SetShortcutAppUserModelId(string linkPath)
    {
        object? shellLink = null;

        try
        {
            if (!File.Exists(linkPath))
                return;

            shellLink = new ShellLinkComObject();
            var persistFile = (IPersistFile)shellLink;
            if (persistFile.Load(linkPath, 0) < 0)
                return;

            var propertyStore = (IPropertyStore)shellLink;
            PropertyKey key = AppUserModelIdPropertyKey;
            PropVariant value = PropVariant.FromString(FrostAppUserModelId);
            try
            {
                if (propertyStore.SetValue(ref key, ref value) >= 0)
                {
                    propertyStore.Commit();
                    persistFile.Save(linkPath, true);
                }
            }
            finally
            {
                value.Dispose();
            }
        }
        catch { /* non-fatal */ }
        finally
        {
            if (shellLink != null && Marshal.IsComObject(shellLink))
                Marshal.FinalReleaseComObject(shellLink);
        }
    }

    private static void CleanupLegacyPinShortcuts(string startMenuDir)
    {
        string taskbarDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "Internet Explorer",
            "Quick Launch",
            "User Pinned",
            "TaskBar");
        string taskbarShortcut = Path.Combine(taskbarDir, "FROST.lnk");

        TryDeleteFile(taskbarShortcut);
        TryDeleteDirectory(startMenuDir);
    }

    private static void ConfigureStartup(bool shouldStartWithWindows, string exePath)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            if (key == null)
                return;

            if (shouldStartWithWindows)
                key.SetValue("FROST", $"\"{exePath}\"", RegistryValueKind.String);
            else
                key.DeleteValue("FROST", throwOnMissingValue: false);
        }
        catch { /* non-fatal */ }
    }

    private static void WriteInstallerOptions(string installDir, bool closeToTray)
    {
        try
        {
            string content = $"CloseToTray={closeToTray}";
            string appDataDir = DefaultInstallPath();
            Directory.CreateDirectory(appDataDir);
            File.WriteAllText(Path.Combine(appDataDir, "installer_options.ini"), content);

            if (!string.IsNullOrWhiteSpace(installDir) && !PathsEqual(installDir, appDataDir))
            {
                Directory.CreateDirectory(installDir);
                File.WriteAllText(Path.Combine(installDir, "installer_options.ini"), content);
            }
        }
        catch { /* non-fatal */ }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
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
            key.SetValue("DisplayVersion",  "1.0.9");
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

    private void LoadExistingOptions()
    {
        try
        {
            string installDir = TxtPath.Text;
            bool existingInstall = File.Exists(Path.Combine(installDir, "FROST.exe"));

            ChkDesktop.IsChecked = !existingInstall || File.Exists(GetDesktopShortcutPath());
            ChkStartup.IsChecked = IsStartupRegistered();

            if (TryReadCloseToTrayOption(out bool closeToTray))
                ChkCloseToTray.IsChecked = closeToTray;
            else if (!existingInstall)
                ChkCloseToTray.IsChecked = false;
        }
        catch { /* non-fatal */ }
    }

    private static string GetDesktopShortcutPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "FROST.lnk");

    private static bool IsStartupRegistered()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                writable: false);
            return key?.GetValue("FROST") is string value &&
                value.Contains("FROST.exe", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadCloseToTrayOption(out bool closeToTray)
    {
        closeToTray = false;

        try
        {
            string installerOptionsPath = Path.Combine(DefaultInstallPath(), "installer_options.ini");
            if (File.Exists(installerOptionsPath) &&
                TryReadCloseToTrayFromInstallerOptions(installerOptionsPath, out closeToTray))
            {
                return true;
            }

            string gridConfigPath = Path.Combine(DefaultInstallPath(), "grid_config.txt");
            if (!File.Exists(gridConfigPath))
                return false;

            string[] parts = File.ReadAllText(gridConfigPath).Split(';');
            return parts.Length >= 54 && bool.TryParse(parts[53], out closeToTray);
        }
        catch
        {
            closeToTray = false;
            return false;
        }
    }

    private static bool TryReadCloseToTrayFromInstallerOptions(string path, out bool closeToTray)
    {
        closeToTray = false;

        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            string[] parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 &&
                string.Equals(parts[0], "CloseToTray", StringComparison.OrdinalIgnoreCase) &&
                bool.TryParse(parts[1], out closeToTray))
            {
                return true;
            }
        }

        return false;
    }

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
