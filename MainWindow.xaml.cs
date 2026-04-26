using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FROST
{
    public partial class MainWindow : Window
    {
        // APIs Windows pour le raccourci clavier global
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsDelegate lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int left; public int top; public int right; public int bottom; }
        private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);
        private delegate bool EnumWindowsDelegate(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lmmi);

        public class DisplayScreen
        {
            public string DeviceName { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public Rect Bounds { get; set; }
            public bool IsPrimary { get; set; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int HOTKEY_ID_F2 = 9002;
        private const int HOTKEY_ID_F3 = 9003;
        private const int HOTKEY_ID_F4 = 9004;
        private const uint MOD_NONE = 0x0000;
        private const int HOTKEY_ID_DEBUG = 9005;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_A = 0x41;
        private const string GitHubOwner = "Vert-Jade";
        private const string GitHubRepo = "FROST";
        private const string GitHubApiVersion = "2022-11-28";
        private const int GridConfigFormatVersion = 10;
        private const double MinPanelOpacity = 0.8;
        private const double MaxPanelOpacity = 1.0;
        private const int GameWindowRescanTickInterval = 15;
        private const double AnchorBoundsToleranceDip = 0.5;
        private const double RemovedOverlayCanvasMarginDip = 10.0;
        private const int MinTrackedGameClientWidth = 80;
        private const int MinTrackedGameClientHeight = 60;
        private const double WindowedPositiveWidthViewportBias = 0.635;
        private const double WindowedWidthLimitedAspectRatio = 95.0 / 72.0;
        private const double WindowedHeightLimitedAspectRatio = 1.95;
        private uint _vkStart = 0x71; // Touche F2 dynamique
        private uint _vkToggle = 0x72; // Touche F3 dynamique
        private uint _vkClear = 0x73; // Touche F4 dynamique

        // État de l'application
        private enum AppState { Idle, WaitingForPlayer, WaitingForMonster }
        private enum GameDisplayMode { Fullscreen, Windowed }

        private sealed class GridCalibrationProfile
        {
            public GridCalibrationProfile(double offsetX, double offsetY, double tileWidth, double tileHeight, double referenceWidth = 0, double referenceHeight = 0)
            {
                OffsetX = offsetX;
                OffsetY = offsetY;
                TileWidth = tileWidth;
                TileHeight = tileHeight;
                ReferenceWidth = referenceWidth;
                ReferenceHeight = referenceHeight;
            }

            public double OffsetX { get; set; }
            public double OffsetY { get; set; }
            public double TileWidth { get; set; }
            public double TileHeight { get; set; }
            public double ReferenceWidth { get; set; }
            public double ReferenceHeight { get; set; }
            public bool HasReferenceSize => ReferenceWidth > 0 && ReferenceHeight > 0;

            public GridCalibrationProfile Clone()
            {
                return new GridCalibrationProfile(OffsetX, OffsetY, TileWidth, TileHeight, ReferenceWidth, ReferenceHeight);
            }
        }

        private sealed class GridTransform
        {
            public double RuntimeScale { get; init; }
            public double RuntimeScaleX { get; init; }
            public double RuntimeScaleY { get; init; }
            public double ViewportX { get; init; }
            public double ViewportY { get; init; }
            public double ReferenceWidth { get; init; }
            public double ReferenceHeight { get; init; }
            public double ProfileOffsetX { get; init; }
            public double ProfileOffsetY { get; init; }
            public double ProfileTileWidth { get; init; }
            public double ProfileTileHeight { get; init; }
            public double TileWidth { get; init; }
            public double TileHeight { get; init; }
            public double AnchorX { get; init; }
            public double AnchorY { get; init; }

            public Point CellToPoint(Point cell)
            {
                double referenceX = ProfileOffsetX + ((cell.X - cell.Y) * (ProfileTileWidth / 2.0));
                double referenceY = ProfileOffsetY + ((cell.X + cell.Y) * (ProfileTileHeight / 2.0));
                return new Point(
                    ViewportX + (referenceX * RuntimeScaleX),
                    ViewportY + (referenceY * RuntimeScaleY));
            }

            public Point PointToCell(Point point, IReadOnlyList<Point> validCells)
            {
                Point closestCell = InvalidCell;
                double bestDistance = double.MaxValue;
                const double diamondThreshold = 1.15;

                foreach (Point cell in validCells)
                {
                    Point center = CellToPoint(cell);
                    double dx = Math.Abs(point.X - center.X);
                    double dy = Math.Abs(point.Y - center.Y);
                    double normalizedDistance = (dx / Math.Max(1.0, TileWidth / 2.0)) + (dy / Math.Max(1.0, TileHeight / 2.0));

                    if (normalizedDistance <= diamondThreshold && normalizedDistance < bestDistance)
                    {
                        bestDistance = normalizedDistance;
                        closestCell = cell;
                    }
                }

                return closestCell;
            }

            public double RuntimeDeltaXToProfile(double delta)
            {
                return IsPositiveFinite(RuntimeScaleX) ? delta / RuntimeScaleX : delta;
            }

            public double RuntimeDeltaYToProfile(double delta)
            {
                return IsPositiveFinite(RuntimeScaleY) ? delta / RuntimeScaleY : delta;
            }

            public static bool TryCreate(GridCalibrationProfile profile, Size clientSize, bool useWindowedResizeModel, out GridTransform transform)
            {
                double referenceWidth = profile.ReferenceWidth > 0 ? profile.ReferenceWidth : DefaultCalibrationReferenceWidth;
                double referenceHeight = profile.ReferenceHeight > 0 ? profile.ReferenceHeight : DefaultCalibrationReferenceHeight;
                double profileTileWidth = profile.TileWidth > 0 ? profile.TileWidth : DefaultTileWidth;
                double profileTileHeight = profile.TileHeight > 0 ? profile.TileHeight : DefaultTileHeight;

                transform = new GridTransform
                {
                    RuntimeScale = 1.0,
                    RuntimeScaleX = 1.0,
                    RuntimeScaleY = 1.0,
                    ReferenceWidth = referenceWidth,
                    ReferenceHeight = referenceHeight,
                    ProfileOffsetX = profile.OffsetX,
                    ProfileOffsetY = profile.OffsetY,
                    ProfileTileWidth = profileTileWidth,
                    ProfileTileHeight = profileTileHeight,
                    TileWidth = profileTileWidth,
                    TileHeight = profileTileHeight,
                    AnchorX = profile.OffsetX,
                    AnchorY = profile.OffsetY
                };

                if (!IsUsableAnchorSize(clientSize) ||
                    !IsPositiveFinite(referenceWidth) ||
                    !IsPositiveFinite(referenceHeight) ||
                    !IsPositiveFinite(profileTileWidth) ||
                    !IsPositiveFinite(profileTileHeight))
                {
                    return false;
                }

                double scaleX = clientSize.Width / referenceWidth;
                double scaleY = clientSize.Height / referenceHeight;
                double runtimeScaleX;
                double runtimeScaleY;
                double viewportX;
                double viewportY;

                if (useWindowedResizeModel)
                {
                    double clientAspect = clientSize.Width / clientSize.Height;

                    if (clientAspect < WindowedWidthLimitedAspectRatio)
                    {
                        double widthLimitedScale = clientSize.Width / (referenceHeight * WindowedWidthLimitedAspectRatio);
                        runtimeScaleX = widthLimitedScale;
                        runtimeScaleY = widthLimitedScale;
                        double viewportWidth = referenceWidth * widthLimitedScale;
                        double viewportHeight = referenceHeight * widthLimitedScale;
                        viewportX = (clientSize.Width - viewportWidth) / 2.0;
                        viewportY = (clientSize.Height - viewportHeight) / 2.0;
                    }
                    else
                    {
                        runtimeScaleX = scaleY;
                        runtimeScaleY = scaleY;
                        double viewportWidth = referenceWidth * scaleY;
                        double viewportHeight = referenceHeight * scaleY;
                        double horizontalDelta = clientSize.Width - viewportWidth;
                        viewportX = clientAspect > WindowedHeightLimitedAspectRatio
                            ? horizontalDelta / 2.0
                            : horizontalDelta > 0
                                ? horizontalDelta * WindowedPositiveWidthViewportBias
                                : horizontalDelta / 2.0;
                        viewportY = (clientSize.Height - viewportHeight) / 2.0;
                    }
                }
                else
                {
                    double uniformScale = Math.Min(scaleX, scaleY);
                    runtimeScaleX = uniformScale;
                    runtimeScaleY = uniformScale;
                    double viewportWidth = referenceWidth * uniformScale;
                    double viewportHeight = referenceHeight * uniformScale;
                    viewportX = (clientSize.Width - viewportWidth) / 2.0;
                    viewportY = (clientSize.Height - viewportHeight) / 2.0;
                }

                if (!IsPositiveFinite(runtimeScaleX) || !IsPositiveFinite(runtimeScaleY))
                {
                    return false;
                }

                double runtimeScale = (runtimeScaleX + runtimeScaleY) / 2.0;

                transform = new GridTransform
                {
                    RuntimeScale = runtimeScale,
                    RuntimeScaleX = runtimeScaleX,
                    RuntimeScaleY = runtimeScaleY,
                    ViewportX = viewportX,
                    ViewportY = viewportY,
                    ReferenceWidth = referenceWidth,
                    ReferenceHeight = referenceHeight,
                    ProfileOffsetX = profile.OffsetX,
                    ProfileOffsetY = profile.OffsetY,
                    ProfileTileWidth = profileTileWidth,
                    ProfileTileHeight = profileTileHeight,
                    TileWidth = profileTileWidth * runtimeScaleX,
                    TileHeight = profileTileHeight * runtimeScaleY,
                    AnchorX = viewportX + (profile.OffsetX * runtimeScaleX),
                    AnchorY = viewportY + (profile.OffsetY * runtimeScaleY)
                };
                return true;
            }
        }

        private sealed class GameWindowCandidate
        {
            public IntPtr Handle { get; init; }
            public Rect ClientBounds { get; init; }
            public bool IsForeground { get; init; }
            public string Title { get; init; } = "";
            public string ClassName { get; init; } = "";
            public string ProcessName { get; init; } = "";
        }

        private sealed class GitHubReleaseAssetInfo
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("browser_download_url")]
            public string BrowserDownloadUrl { get; set; } = "";

            [JsonPropertyName("size")]
            public long Size { get; set; }
        }

        private sealed class GitHubReleaseInfo
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; } = "";

            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("draft")]
            public bool Draft { get; set; }

            [JsonPropertyName("prerelease")]
            public bool Prerelease { get; set; }

            [JsonPropertyName("assets")]
            public List<GitHubReleaseAssetInfo> Assets { get; set; } = new List<GitHubReleaseAssetInfo>();
        }
        private AppState _currentState = AppState.Idle;
        private bool _isDebugMode = false;

        // Le système de Grille Absolue (Méthode Luframe)
        private const double DefaultGridOffsetX = 750.2863449307483;
        private const double DefaultGridOffsetY = 402.4546368631193;
        private const double DefaultTileWidth = 74.47475948174001;
        private const double DefaultTileHeight = 37.2042988554017;
        private const double DefaultWindowedGridOffsetX = 746.6484746744475;
        private const double DefaultWindowedGridOffsetY = 402.90519860862963;
        private const double DefaultWindowedTileWidth = 68.25004417942502;
        private const double DefaultWindowedTileHeight = 34.232765930595235;
        private const double DefaultCalibrationReferenceWidth = 1536.0;
        private const double DefaultCalibrationReferenceHeight = 864.0;
        private const double DefaultWindowedCalibrationReferenceWidth = 1530.4;
        private const double DefaultWindowedCalibrationReferenceHeight = 792.8000000000001;
        private const int DofusMapAnchorCellX = 16;
        private const int DofusMapAnchorCellY = 3;
        private static readonly Point InvalidCell = new Point(-999, -999);
        private static readonly IReadOnlyList<Point> ValidMapCells = BuildValidMapCells();
        private static readonly HashSet<(int X, int Y)> ValidMapCellLookup = BuildValidMapCellLookup();

        private double _gridOffsetX = DefaultWindowedGridOffsetX; // Profil par defaut stable
        private double _gridOffsetY = DefaultWindowedGridOffsetY;

        // Coordonnées matricielles (Colonnes, Lignes)
        private bool _isPlayerSet = false;
        private bool _isMonsterSet = false;
        private Point _playerCell;
        private Point _monsterCell;

        // Dimensions par défaut de la grille Dofus
        private double _tileWidth = DefaultWindowedTileWidth; // Profil par defaut stable
        private double _tileHeight = DefaultWindowedTileHeight;

        // Angle actuel basé sur le seuil de vie sélectionné (en degrés)
        private int _currentSeuil = 1;
        private double _currentAngle = 90;

        // Nombre de coups (CàC)
        private int _hitCount = 0;

        // Mécaniques Gousset
        private bool _isBossTarget = false;
        private bool _isOddTurn = true;

        private int _colorblindMode = 0;
        private int _langIdx = 0;
        private int _iconIdx = 0;
        private Color _themeColor = Color.FromRgb(77, 168, 218); // Bleu glace FROST (#4DA8DA)
        private bool _isLargeText = false;
        private const double PanelDefaultX = 0.0;
        private const double PanelDefaultY = 0.0;
        private const double CompactPanelDefaultWidth = 216.0;
        private const double CompactPanelDefaultHeight = 499.7;
        private const double WindowDefaultX = 0.8;
        private const double WindowDefaultY = 0.8;
        private const double WindowDefaultWidth = 1534.4;
        private const double WindowDefaultHeight = 814.4;
        private bool _isCompactMode = false;
        private string _selectedScreenDeviceName = "";
        private double _onbX = 0.0;
        private double _onbY = 0.0;
        private double _compactX = 0.0;
        private double _compactY = 0.0;
        private double _compactScale = 1.0;
        private const double ControlPanelMinWidthValue = 180.0;
        private const double ControlPanelMinHeightValue = 44.0;
        private const double ControlPanelMaxWidthValue = 520.0;
        private const double ControlPanelScreenPadding = 12.0;
        private const double CollapsedPanelHeightThreshold = 56.0;
        private const double ScreenSelectionButtonWidth = 164.0;
        private const double ScreenSelectionButtonHeight = 30.0;
        private const double OnboardingScreenSelectionButtonWidth = 136.0;
        private const double OnboardingScreenSelectionButtonHeight = 34.0;
        private const double ResponsiveCompactEnterWidth = 315.0;
        private const double ResponsiveCompactExitWidth = 350.0;
        private const double ResponsiveCompactEnterHeight = 430.0;
        private const double ResponsiveCompactExitHeight = 500.0;
        private double _panelX = PanelDefaultX;
        private double _panelY = PanelDefaultY;
        private double _panelWidth = CompactPanelDefaultWidth;
        private double _panelHeight = CompactPanelDefaultHeight;
        private double _windowX = WindowDefaultX;
        private double _windowY = WindowDefaultY;
        private double _windowWidth = WindowDefaultWidth;
        private double _windowHeight = WindowDefaultHeight;
        private double _creatorX = 22.666666666666742;
        private double _creatorY = -2.000000000000057;
        private double _creatorScale = 1.1518441101562502;
        private bool _isClampingControlPanel = false;
        private bool _isResponsiveCompactMode = false;
        private bool _isApplyingViewMode = false;
        private GameDisplayMode _selectedGameDisplayMode = GameDisplayMode.Windowed;
        private GridCalibrationProfile _fullscreenCalibration = CreateDefaultFullscreenCalibration();
        private GridCalibrationProfile _windowedCalibration = CreateDefaultWindowedCalibration();
        private bool _isAutoFittingControlPanel = false;
        private bool _isAutoFitControlPanelQueued = false;
        private bool _saveAfterAutoFitControlPanel = false;
        private bool _isApplyingInitialLayout = true;
        private bool _hasSavedGridConfig = false;
        private bool _canPersistGridConfig = false;
        private bool _suspendLayoutPersistence = false;
        private DispatcherTimer? _panelBoundsSaveTimer;


        private Point _lastHoveredCell = InvalidCell;
        private bool _isOverlayEnabled = true;
        private string _lastStatusText = "";
        private Brush _lastStatusForeground = Brushes.Gray;
        private IntPtr _windowHandle;
        private DispatcherTimer? _transparencyTimer;
        private bool _isClickThrough = false;
        private IntPtr _trackedGameWindowHandle = IntPtr.Zero;
        private Rect _trackedGameClientBounds = Rect.Empty;
        private int _gameWindowTickCounter = GameWindowRescanTickInterval;
        private bool _isApplyingTrackedWindowBounds = false;
        private bool _isAutoHiddenBecauseGameWindowUnavailable = false;
        private bool _shouldRestoreWindowAfterGameWindowReturns = false;
        private DateTime _trackedWindowInteractionSuppressionUntilUtc = DateTime.MinValue;
        private bool _pendingWindowedV9ReferenceRepair = false;
        private int _onboardingStep = 1;
        private const int NoticeWizardStepCount = 5;
        private bool _isMandatoryNoticeFlowActive = false;
        private bool _hasCompletedMandatoryNoticeFlow = false;
        private int _mandatoryNoticeStep = 1;
        private static readonly string AppDataDirectory = InitializeAppDataDirectory();
        private static readonly string GridConfigPath = System.IO.Path.Combine(AppDataDirectory, "grid_config.txt");
        private static readonly string GridConfigBackupPath = System.IO.Path.Combine(AppDataDirectory, "grid_config.backup.txt");
        private static readonly string DebugLogPath = System.IO.Path.Combine(AppDataDirectory, "frost_debug.log");
        private static readonly string LegacyGridConfigPath = System.IO.Path.Combine(AppContext.BaseDirectory, "grid_config.txt");
        private static readonly string UpdatesDirectory = System.IO.Path.Combine(AppDataDirectory, "Updates");
        private static readonly string GitHubLatestReleaseApiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
        private static readonly HttpClient UpdateHttpClient = CreateUpdateHttpClient();
        private static readonly JsonSerializerOptions GitHubJsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        private bool _isUpdateCheckInProgress = false;
        private bool _hasShownUpdatePromptThisSession = false;
        private string _lastDownloadedReleaseTag = "";
        private string _lastDownloadedInstallerName = "";
        private string _pendingUpdateReleaseTag = "";
        private string _pendingUpdateReleaseName = "";
        private string? _pendingUpdateInstallerPath;
        private DispatcherTimer? _updatePromptTimer;

        private static string InitializeAppDataDirectory()
        {
            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FROST"
            );
            Directory.CreateDirectory(path);
            return path;
        }

        private static HttpClient CreateUpdateHttpClient()
        {
            HttpClient client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FROST-Updater");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", GitHubApiVersion);
            return client;
        }

        public static void Log(string message)
        {
            try { File.AppendAllText(DebugLogPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n"); } catch { }
        }

        private static GridCalibrationProfile CreateCalibrationProfile(double offsetX, double offsetY, double tileWidth, double tileHeight, double referenceWidth = 0, double referenceHeight = 0)
        {
            return new GridCalibrationProfile(offsetX, offsetY, tileWidth, tileHeight, referenceWidth, referenceHeight);
        }

        private static GridCalibrationProfile CreateDefaultFullscreenCalibration()
        {
            return CreateCalibrationProfile(
                DefaultGridOffsetX,
                DefaultGridOffsetY,
                DefaultTileWidth,
                DefaultTileHeight,
                DefaultCalibrationReferenceWidth,
                DefaultCalibrationReferenceHeight);
        }

        private static GridCalibrationProfile CreateDefaultWindowedCalibration()
        {
            return CreateCalibrationProfile(
                DefaultWindowedGridOffsetX,
                DefaultWindowedGridOffsetY,
                DefaultWindowedTileWidth,
                DefaultWindowedTileHeight,
                DefaultWindowedCalibrationReferenceWidth,
                DefaultWindowedCalibrationReferenceHeight);
        }

        private static bool TryParseInvariantDouble(string value, out double result)
        {
            return double.TryParse(
                value,
                System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                System.Globalization.CultureInfo.InvariantCulture,
                out result);
        }

        private static bool TryParseCalibrationProfile(string[] parts, int startIndex, out GridCalibrationProfile profile)
        {
            profile = CreateDefaultFullscreenCalibration();

            if (parts.Length < startIndex + 4)
            {
                return false;
            }

            if (!TryParseInvariantDouble(parts[startIndex], out double offsetX) ||
                !TryParseInvariantDouble(parts[startIndex + 1], out double offsetY) ||
                !TryParseInvariantDouble(parts[startIndex + 2], out double tileWidth) ||
                !TryParseInvariantDouble(parts[startIndex + 3], out double tileHeight))
            {
                return false;
            }

            profile = CreateCalibrationProfile(offsetX, offsetY, tileWidth, tileHeight);
            return true;
        }

        private Size GetCurrentCalibrationAnchorSize()
        {
            if (_trackedGameClientBounds.Width > 0 && _trackedGameClientBounds.Height > 0)
            {
                return new Size(_trackedGameClientBounds.Width, _trackedGameClientBounds.Height);
            }

            return new Size(GetLayoutWidth(), GetLayoutHeight());
        }

        private static bool IsUsableAnchorSize(Size size)
        {
            return size.Width > 0 && size.Height > 0 &&
                   !double.IsNaN(size.Width) && !double.IsInfinity(size.Width) &&
                   !double.IsNaN(size.Height) && !double.IsInfinity(size.Height);
        }

        private static bool BoundsCloseEnough(Rect a, Rect b)
        {
            return Math.Abs(a.X - b.X) <= AnchorBoundsToleranceDip &&
                   Math.Abs(a.Y - b.Y) <= AnchorBoundsToleranceDip &&
                   Math.Abs(a.Width - b.Width) <= AnchorBoundsToleranceDip &&
                   Math.Abs(a.Height - b.Height) <= AnchorBoundsToleranceDip;
        }

        private bool TryBuildGridTransform(GridCalibrationProfile profile, GameDisplayMode mode, out GridTransform transform)
        {
            EnsureCalibrationProfileReferenceSize(profile);
            return GridTransform.TryCreate(profile, GetCurrentCalibrationAnchorSize(), mode == GameDisplayMode.Windowed, out transform);
        }

        private bool TryBuildGridTransform(GridCalibrationProfile profile, out GridTransform transform)
        {
            return TryBuildGridTransform(profile, _selectedGameDisplayMode, out transform);
        }

        private bool TryGetCurrentGridTransform(out GridTransform transform)
        {
            return TryBuildGridTransform(GetCalibrationProfile(_selectedGameDisplayMode), out transform);
        }

        private static void EnsureCalibrationProfileReferenceSize(GridCalibrationProfile profile)
        {
            if (!IsPositiveFinite(profile.ReferenceWidth))
            {
                profile.ReferenceWidth = DefaultCalibrationReferenceWidth;
            }

            if (!IsPositiveFinite(profile.ReferenceHeight))
            {
                profile.ReferenceHeight = DefaultCalibrationReferenceHeight;
            }
        }

        private void RunGridTransformSimulationTests()
        {
            try
            {
                Size[] simulationSizes =
                {
                    new Size(1536, 864),
                    new Size(1280, 720),
                    new Size(1152, 864),
                    new Size(1024, 768),
                    new Size(1600, 720),
                    new Size(900, 700),
                    new Size(1140, 864),
                    new Size(1132, 864),
                    new Size(1068, 900),
                    new Size(1068, 864),
                    new Size(1068, 828),
                    new Size(1068, 812),
                    new Size(1068, 804),
                    new Size(640, 360),
                    new Size(420, 315),
                    new Size(320, 240),
                    new Size(220, 160),
                    new Size(1045, 792),
                    new Size(928, 792),
                    new Size(1244, 980),
                    new Size(1244, 944),
                    new Size(1244, 920),
                    new Size(1244, 626),
                    new Size(1450, 626),
                    new Size(900, 626),
                    new Size(950, 720),
                    new Size(942, 720),
                    new Size(1244, 760),
                    new Size(1244, 500),
                    new Size(1450, 500),
                    new Size(700, 760),
                    new Size(520, 360)
                };

                List<Point> simulationCells = new List<Point>();
                Point centerCell = new Point(0, 0);
                if (IsValidMapCell(centerCell))
                {
                    simulationCells.Add(centerCell);
                }

                simulationCells.Add(ValidMapCells[0]);
                simulationCells.Add(ValidMapCells[ValidMapCells.Count / 2]);
                simulationCells.Add(ValidMapCells[ValidMapCells.Count - 1]);

                (string Name, GridCalibrationProfile Profile)[] profiles =
                {
                    ("Fullscreen", CreateDefaultFullscreenCalibration()),
                    ("Windowed", CreateDefaultWindowedCalibration())
                };

                int failures = 0;
                foreach ((string name, GridCalibrationProfile profile) in profiles)
                {
                    EnsureCalibrationProfileReferenceSize(profile);
                    GameDisplayMode simulationMode = name == "Windowed" ? GameDisplayMode.Windowed : GameDisplayMode.Fullscreen;
                    foreach (Size size in simulationSizes)
                    {
                        if (!GridTransform.TryCreate(profile, size, simulationMode == GameDisplayMode.Windowed, out GridTransform transform))
                        {
                            failures++;
                            Log($"[GridTransform Simulation] ECHEC {name} {size.Width:0}x{size.Height:0} : transform impossible.");
                            continue;
                        }

                        foreach (Point cell in simulationCells)
                        {
                            Point point = transform.CellToPoint(cell);
                            Point roundTrip = transform.PointToCell(point, ValidMapCells);
                            if (!IsSameCell(cell, roundTrip))
                            {
                                failures++;
                                Log($"[GridTransform Simulation] ECHEC {name} {size.Width:0}x{size.Height:0} cellule ({cell.X},{cell.Y}) -> ({roundTrip.X},{roundTrip.Y}).");
                            }
                        }

                        Log($"[GridTransform Simulation] OK {name} {size.Width:0}x{size.Height:0} scale=({transform.RuntimeScaleX:0.###},{transform.RuntimeScaleY:0.###}) viewport=({transform.ViewportX:0.#},{transform.ViewportY:0.#}) tile=({transform.TileWidth:0.#},{transform.TileHeight:0.#}).");
                    }
                }

                GridCalibrationProfile windowedCheckProfile = CreateDefaultWindowedCalibration();
                Point windowedCheckCell = simulationCells.Count > 0 ? simulationCells[0] : ValidMapCells[ValidMapCells.Count / 2];
                Size sameHeightBaseSize = new Size(1244, 626);
                Size[] sameHeightSizes =
                {
                    new Size(1450, 626),
                    new Size(900, 626)
                };

                if (GridTransform.TryCreate(windowedCheckProfile, sameHeightBaseSize, true, out GridTransform sameHeightBase))
                {
                    Point basePoint = sameHeightBase.CellToPoint(windowedCheckCell);
                    foreach (Size size in sameHeightSizes)
                    {
                        if (!GridTransform.TryCreate(windowedCheckProfile, size, true, out GridTransform sameHeightTransform))
                        {
                            failures++;
                            Log($"[GridTransform Simulation] ECHEC Windowed same-height {size.Width:0}x{size.Height:0} : transform impossible.");
                            continue;
                        }

                        Point point = sameHeightTransform.CellToPoint(windowedCheckCell);
                        double actualXDelta = point.X - basePoint.X;
                        double scaleDelta = Math.Abs(sameHeightTransform.RuntimeScaleX - sameHeightBase.RuntimeScaleX);
                        double yDelta = Math.Abs(point.Y - basePoint.Y);
                        if (scaleDelta > 0.001 || yDelta > 1.0)
                        {
                            failures++;
                            Log($"[GridTransform Simulation] ECHEC Windowed same-height {size.Width:0}x{size.Height:0} scaleDelta={scaleDelta:0.####} xDelta={actualXDelta:0.#} yDelta={yDelta:0.#}.");
                        }
                        else
                        {
                            Log($"[GridTransform Simulation] OK Windowed same-height {size.Width:0}x{size.Height:0} scale={sameHeightTransform.RuntimeScaleX:0.###} xDelta={actualXDelta:0.#}.");
                        }
                    }
                }
                else
                {
                    failures++;
                    Log("[GridTransform Simulation] ECHEC Windowed same-height : base transform impossible.");
                }

                Size[] sameWidthSizes =
                {
                    new Size(1068, 612),
                    new Size(1068, 720),
                    new Size(1068, 792),
                    new Size(1068, 804),
                    new Size(1068, 812),
                    new Size(1068, 828),
                    new Size(1068, 864),
                    new Size(1068, 900)
                };

                double previousWindowedScale = double.NegativeInfinity;
                foreach (Size size in sameWidthSizes)
                {
                    if (!GridTransform.TryCreate(windowedCheckProfile, size, true, out GridTransform sameWidthTransform))
                    {
                        failures++;
                        Log($"[GridTransform Simulation] ECHEC Windowed same-width {size.Width:0}x{size.Height:0} : transform impossible.");
                        continue;
                    }

                    if (IsPositiveFinite(previousWindowedScale) &&
                        sameWidthTransform.RuntimeScaleX + 0.001 < previousWindowedScale)
                    {
                        failures++;
                        Log($"[GridTransform Simulation] ECHEC Windowed same-width {size.Width:0}x{size.Height:0} scale regressif ({sameWidthTransform.RuntimeScaleX:0.###} < {previousWindowedScale:0.###}).");
                    }
                    else
                    {
                        Log($"[GridTransform Simulation] OK Windowed same-width {size.Width:0}x{size.Height:0} scale={sameWidthTransform.RuntimeScaleX:0.###} viewport=({sameWidthTransform.ViewportX:0.#},{sameWidthTransform.ViewportY:0.#}).");
                    }

                    previousWindowedScale = sameWidthTransform.RuntimeScaleX;
                }

                Log(failures == 0
                    ? "[GridTransform Simulation] Toutes les tailles de reference sont stables."
                    : $"[GridTransform Simulation] {failures} anomalie(s) detectee(s).");
            }
            catch (Exception ex)
            {
                Log($"[GridTransform Simulation] Exception : {ex.Message}");
            }
        }

        private void RepairLegacyAnchoredCalibrationProfilesIfNeeded(int savedConfigVersion)
        {
            if (savedConfigVersion >= GridConfigFormatVersion)
            {
                return;
            }

            GridCalibrationProfile fullscreenDefaults = CreateDefaultFullscreenCalibration();
            GridCalibrationProfile windowedDefaults = CreateDefaultWindowedCalibration();

            bool fullscreenLooksBroken = CalibrationRatioLooksBroken(_fullscreenCalibration, fullscreenDefaults);
            bool windowedLooksBroken = CalibrationRatioLooksBroken(_windowedCalibration, windowedDefaults);

            if (fullscreenLooksBroken)
            {
                _fullscreenCalibration = fullscreenDefaults;
            }

            if (windowedLooksBroken)
            {
                _windowedCalibration = windowedDefaults;
            }

            if (fullscreenLooksBroken || windowedLooksBroken)
            {
                Log("Migration configuration v4 : restauration des profils par defaut stables.");
            }
        }

        private void MigrateRemovedOverlayCanvasMarginIfNeeded(int savedConfigVersion)
        {
            if (savedConfigVersion >= 7)
            {
                return;
            }

            _fullscreenCalibration.OffsetX += RemovedOverlayCanvasMarginDip;
            _fullscreenCalibration.OffsetY += RemovedOverlayCanvasMarginDip;
            _windowedCalibration.OffsetX += RemovedOverlayCanvasMarginDip;
            _windowedCalibration.OffsetY += RemovedOverlayCanvasMarginDip;
            Log("Migration configuration v7 : compensation de la marge fixe retiree du canvas overlay.");
        }

        private static bool CalibrationRatioLooksBroken(GridCalibrationProfile profile, GridCalibrationProfile defaults)
        {
            if (profile.TileWidth <= 0 || profile.TileHeight <= 0 || defaults.TileWidth <= 0 || defaults.TileHeight <= 0)
            {
                return true;
            }

            double profileRatio = profile.TileWidth / profile.TileHeight;
            double defaultRatio = defaults.TileWidth / defaults.TileHeight;
            return Math.Abs(profileRatio - defaultRatio) > 0.04;
        }

        private static bool WindowedReferenceLooksTransient(GridCalibrationProfile profile)
        {
            if (!profile.HasReferenceSize || profile.ReferenceWidth <= 0 || profile.ReferenceHeight <= 0)
            {
                return false;
            }

            double aspect = profile.ReferenceWidth / profile.ReferenceHeight;
            return profile.ReferenceWidth < 900.0 ||
                   profile.ReferenceHeight < 500.0 ||
                   aspect < 0.9 ||
                   aspect > 3.0;
        }

        private void RepairTransientWindowedReferenceIfNeeded(int savedConfigVersion)
        {
            if (savedConfigVersion >= 8 || !WindowedReferenceLooksTransient(_windowedCalibration))
            {
                return;
            }

            _windowedCalibration = CreateDefaultWindowedCalibration();
            Log("Migration configuration v8 : restauration du profil de resize stable.");
        }

        private static bool NormalizeCalibrationProfileReference(GridCalibrationProfile profile, GameDisplayMode mode, double targetReferenceWidth, double targetReferenceHeight, Size? preservationSizeOverride = null)
        {
            EnsureCalibrationProfileReferenceSize(profile);

            if (!IsPositiveFinite(targetReferenceWidth) ||
                !IsPositiveFinite(targetReferenceHeight) ||
                (Math.Abs(profile.ReferenceWidth - targetReferenceWidth) <= 0.5 &&
                 Math.Abs(profile.ReferenceHeight - targetReferenceHeight) <= 0.5))
            {
                return false;
            }

            Size preservationSize = preservationSizeOverride ?? new Size(profile.ReferenceWidth, profile.ReferenceHeight);
            if (!IsUsableAnchorSize(preservationSize))
            {
                return false;
            }

            if (!GridTransform.TryCreate(profile, preservationSize, mode == GameDisplayMode.Windowed, out GridTransform sourceTransform))
            {
                return false;
            }

            GridCalibrationProfile normalizedProfile = profile.Clone();
            normalizedProfile.ReferenceWidth = targetReferenceWidth;
            normalizedProfile.ReferenceHeight = targetReferenceHeight;

            if (!GridTransform.TryCreate(normalizedProfile, preservationSize, mode == GameDisplayMode.Windowed, out GridTransform targetTransform) ||
                !IsPositiveFinite(targetTransform.RuntimeScaleX) ||
                !IsPositiveFinite(targetTransform.RuntimeScaleY))
            {
                return false;
            }

            profile.OffsetX = (sourceTransform.AnchorX - targetTransform.ViewportX) / targetTransform.RuntimeScaleX;
            profile.OffsetY = (sourceTransform.AnchorY - targetTransform.ViewportY) / targetTransform.RuntimeScaleY;
            profile.TileWidth = sourceTransform.TileWidth / targetTransform.RuntimeScaleX;
            profile.TileHeight = sourceTransform.TileHeight / targetTransform.RuntimeScaleY;
            profile.ReferenceWidth = targetReferenceWidth;
            profile.ReferenceHeight = targetReferenceHeight;
            return true;
        }

        private bool NeedsWindowedV9ReferenceRepair(int savedConfigVersion)
        {
            if (savedConfigVersion != 9)
            {
                return false;
            }

            EnsureCalibrationProfileReferenceSize(_windowedCalibration);
            return Math.Abs(_windowedCalibration.ReferenceWidth - DefaultCalibrationReferenceWidth) <= 0.5 &&
                   Math.Abs(_windowedCalibration.ReferenceHeight - DefaultCalibrationReferenceHeight) <= 0.5;
        }

        private bool RepairWindowedV9ReferenceIfNeeded(Size trackedClientSize)
        {
            if (!_pendingWindowedV9ReferenceRepair || !IsUsableAnchorSize(trackedClientSize))
            {
                return false;
            }

            double trackedAspect = trackedClientSize.Width / trackedClientSize.Height;
            if (trackedAspect < WindowedWidthLimitedAspectRatio || trackedAspect > WindowedHeightLimitedAspectRatio)
            {
                return false;
            }

            if (!NormalizeCalibrationProfileReference(
                    _windowedCalibration,
                    GameDisplayMode.Windowed,
                    trackedClientSize.Width,
                    trackedClientSize.Height,
                    trackedClientSize))
            {
                return false;
            }

            _pendingWindowedV9ReferenceRepair = false;
            Log($"Migration configuration v10 : reference de resize stable reappliquee sur {trackedClientSize.Width:0.#}x{trackedClientSize.Height:0.#}.");
            return true;
        }

        private void SetStatus(string text, Brush foreground)
        {
            _lastStatusText = text;
            _lastStatusForeground = foreground;
        }

        private static string GetUiText(string key, string fallback)
        {
            return Application.Current?.Resources[key] as string ?? fallback;
        }

        public MainWindow()
        {
            TryMigrateLegacyConfig();
            TryRestoreConfigFromBackupIfNeeded();
            InitializeComponent();
            LoadGridConfig();
            ApplyStartupCompactPanelDefaults();
            InitUIStates();
            RunWithoutLayoutPersistence(() =>
            {
                ApplySavedWindowBounds();
                ApplySavedControlPanelBounds();
            });
            LoadIcons();
            Log("=== FROST DÉMARRÉ ===");

            PreviewKeyDown += MainWindow_PreviewKeyDown;

            this.SizeChanged += (s, e) =>
            {
                SyncControlPanelWidthToWindow();
                ClampControlPanelToWindow();
                QueuePanelBoundsSave();
            };
            this.LocationChanged += (s, e) => QueuePanelBoundsSave();

            this.Loaded += (s, e) =>
            {
                bool shouldDelayInitialReveal = _hasSavedGridConfig && _hasCompletedMandatoryNoticeFlow;

                try
                {
                    if (shouldDelayInitialReveal)
                    {
                        Opacity = 0.0;
                    }

                    List<DisplayScreen> screens = GetScreens();
                    DisplayScreen targetScreen = screens.FirstOrDefault(sc => sc.DeviceName == _selectedScreenDeviceName)
                                                 ?? screens.FirstOrDefault(sc => sc.IsPrimary)
                                                 ?? screens.FirstOrDefault()!;
                    RunWithoutLayoutPersistence(() =>
                    {
                        ApplySavedWindowBounds();
                        if (targetScreen != null)
                        {
                            ClampWindowToScreen(targetScreen);
                        }
                        ApplySavedControlPanelBounds();
                        ClampControlPanelToWindow();
                    });
                    QueueAutoFitControlPanelHeight();
                    if (targetScreen != null)
                    {
                        MoveToScreen(targetScreen);
                    }
                }
                finally
                {
                    _isApplyingInitialLayout = false;

                    if (shouldDelayInitialReveal)
                    {
                        UpdateTrackedGameWindowAnchor();
                        if (!_isAutoHiddenBecauseGameWindowUnavailable && Opacity <= 0.01)
                        {
                            Opacity = 1.0;
                            Topmost = true;
                        }
                    }

                    if (!_hasSavedGridConfig)
                    {
                        StartOnboarding();
                    }
                    else if (!_hasCompletedMandatoryNoticeFlow)
                    {
                        StartMandatoryNoticeFlow();
                    }

                    QueuePreviouslyDownloadedUpdateIfNeeded();
                    StartAutoUpdateCheck();
                }
            };
        }

        private void ApplyStartupCompactPanelDefaults()
        {
            _isResponsiveCompactMode = false;

            if (!_hasSavedGridConfig)
            {
                _isCompactMode = true;
                _panelX = PanelDefaultX;
                _panelY = PanelDefaultY;
                _panelWidth = CompactPanelDefaultWidth;
                _panelHeight = CompactPanelDefaultHeight;
                _windowX = WindowDefaultX;
                _windowY = WindowDefaultY;
                _windowWidth = WindowDefaultWidth;
                _windowHeight = WindowDefaultHeight;
                return;
            }

            _panelX = IsFiniteNumber(_panelX) && _panelX >= 0 ? _panelX : PanelDefaultX;
            _panelY = IsFiniteNumber(_panelY) && _panelY >= 0 ? _panelY : PanelDefaultY;
            _panelWidth = ClampFinite(_panelWidth, ControlPanelMinWidthValue, ControlPanelMaxWidthValue);
            _panelHeight = IsUsablePanelHeight(_panelHeight)
                ? Math.Max(_panelHeight, ControlPanelMinHeightValue)
                : CompactPanelDefaultHeight;

            _windowX = IsFiniteNumber(_windowX) ? _windowX : WindowDefaultX;
            _windowY = IsFiniteNumber(_windowY) ? _windowY : WindowDefaultY;
            _windowWidth = IsPositiveFinite(_windowWidth) ? _windowWidth : WindowDefaultWidth;
            _windowHeight = ClampFinite(_windowHeight, Math.Max(80, ControlPanelMinHeightValue + ControlPanelScreenPadding), SystemParameters.VirtualScreenHeight);
        }

        private void RunWithoutLayoutPersistence(Action action)
        {
            bool wasSuspended = _suspendLayoutPersistence;
            _suspendLayoutPersistence = true;
            try
            {
                action();
            }
            finally
            {
                _suspendLayoutPersistence = wasSuspended;
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SaveCurrentLayoutAsDefault();
                e.Handled = true;
            }
        }

        private static void TryMigrateLegacyConfig()
        {
            try
            {
                if (File.Exists(GridConfigPath) || !File.Exists(LegacyGridConfigPath))
                    return;

                string? legacyDir = System.IO.Path.GetDirectoryName(LegacyGridConfigPath);
                if (!string.IsNullOrWhiteSpace(legacyDir) &&
                    legacyDir.Equals(AppDataDirectory, StringComparison.OrdinalIgnoreCase))
                    return;

                File.Copy(LegacyGridConfigPath, GridConfigPath, false);
                File.Delete(LegacyGridConfigPath);
            }
            catch { }
        }

        private static void TryRestoreConfigFromBackupIfNeeded()
        {
            try
            {
                if (File.Exists(GridConfigPath) || !File.Exists(GridConfigBackupPath))
                    return;

                File.Copy(GridConfigBackupPath, GridConfigPath, false);
            }
            catch { }
        }

        private static string NormalizeVersionTag(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            string normalized = raw.Trim();
            if (normalized.StartsWith("refs/tags/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("refs/tags/".Length);
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(1);

            int separatorIndex = normalized.IndexOfAny(new[] { '+', '-' });
            if (separatorIndex >= 0)
                normalized = normalized.Substring(0, separatorIndex);

            return normalized.Trim();
        }

        private static bool TryParseComparableVersion(string? raw, out Version version)
        {
            string normalized = NormalizeVersionTag(raw);
            return Version.TryParse(normalized, out version!);
        }

        private static Version GetCurrentApplicationVersion()
        {
            try
            {
                string? processPath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(processPath))
                {
                    FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(processPath);
                    if (TryParseComparableVersion(versionInfo.ProductVersion, out Version productVersion))
                        return productVersion;

                    if (TryParseComparableVersion(versionInfo.FileVersion, out Version fileVersion))
                        return fileVersion;
                }
            }
            catch { }

            return typeof(MainWindow).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
        }

        private static bool IsReleaseNewerThanCurrent(string? releaseTag)
        {
            if (!TryParseComparableVersion(releaseTag, out Version releaseVersion))
                return false;

            return releaseVersion > GetCurrentApplicationVersion();
        }

        private static string BuildDefaultInstallerName(string releaseTag)
        {
            string normalizedTag = NormalizeVersionTag(releaseTag);
            if (string.IsNullOrWhiteSpace(normalizedTag))
                normalizedTag = "latest";

            return $"FROST_v{normalizedTag}_setup.exe";
        }

        private static string SanitizeFileName(string fileName)
        {
            foreach (char invalidChar in System.IO.Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            return fileName;
        }

        private static GitHubReleaseAssetInfo? SelectInstallerAsset(GitHubReleaseInfo release)
        {
            if (release.Assets == null || release.Assets.Count == 0)
                return null;

            GitHubReleaseAssetInfo? asset = release.Assets.FirstOrDefault(a =>
                !string.IsNullOrWhiteSpace(a.Name) &&
                a.Name.EndsWith("_setup.exe", StringComparison.OrdinalIgnoreCase));

            asset ??= release.Assets.FirstOrDefault(a =>
                !string.IsNullOrWhiteSpace(a.Name) &&
                a.Name.Equals("setup.exe", StringComparison.OrdinalIgnoreCase));

            asset ??= release.Assets.FirstOrDefault(a =>
                !string.IsNullOrWhiteSpace(a.Name) &&
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            return asset;
        }

        private static string GetInstallerDownloadPath(string installerName)
        {
            Directory.CreateDirectory(UpdatesDirectory);
            string safeFileName = SanitizeFileName(string.IsNullOrWhiteSpace(installerName) ? BuildDefaultInstallerName("latest") : installerName);
            return System.IO.Path.Combine(UpdatesDirectory, safeFileName);
        }

        private void StartAutoUpdateCheck()
        {
            _ = CheckForUpdatesOnStartupAsync();
        }

        private async Task CheckForUpdatesOnStartupAsync()
        {
            if (_isUpdateCheckInProgress)
                return;

            _isUpdateCheckInProgress = true;
            try
            {
                GitHubReleaseInfo? release = await FetchLatestReleaseAsync();
                if (release == null || release.Draft || release.Prerelease)
                    return;

                if (!IsReleaseNewerThanCurrent(release.TagName))
                {
                    Log("Aucune mise à jour plus récente détectée.");
                    return;
                }

                GitHubReleaseAssetInfo? installerAsset = SelectInstallerAsset(release);
                if (installerAsset == null || string.IsNullOrWhiteSpace(installerAsset.BrowserDownloadUrl))
                {
                    Log("Aucun installeur exploitable trouvé dans la dernière release GitHub.");
                    return;
                }

                string installerName = string.IsNullOrWhiteSpace(installerAsset.Name)
                    ? BuildDefaultInstallerName(release.TagName)
                    : installerAsset.Name;
                string installerPath = GetInstallerDownloadPath(installerName);

                if (!IsInstallerAlreadyDownloaded(installerPath, installerAsset.Size))
                {
                    await DownloadInstallerAsync(installerAsset.BrowserDownloadUrl, installerPath, installerAsset.Size);
                    Log($"Mise à jour téléchargée : {installerPath}");
                }
                else
                {
                    Log($"Installeur déjà téléchargé : {installerPath}");
                }

                CleanupOldDownloadedInstallers(installerPath);

                _lastDownloadedReleaseTag = release.TagName ?? "";
                _lastDownloadedInstallerName = System.IO.Path.GetFileName(installerPath);
                _pendingUpdateReleaseTag = release.TagName ?? "";
                _pendingUpdateReleaseName = string.IsNullOrWhiteSpace(release.Name) ? (release.TagName ?? string.Empty) : release.Name;
                _pendingUpdateInstallerPath = installerPath;

                SaveGridConfig(rememberBounds: false);
                QueueUpdatePromptIfPossible();
            }
            catch (Exception ex)
            {
                Log($"Vérification de mise à jour impossible : {ex.Message}");
            }
            finally
            {
                _isUpdateCheckInProgress = false;
            }
        }

        private async Task<GitHubReleaseInfo?> FetchLatestReleaseAsync()
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, GitHubLatestReleaseApiUrl);
            using HttpResponseMessage response = await UpdateHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using Stream contentStream = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<GitHubReleaseInfo>(contentStream, GitHubJsonOptions);
        }

        private static bool IsInstallerAlreadyDownloaded(string installerPath, long expectedSize)
        {
            if (!File.Exists(installerPath))
                return false;

            if (expectedSize <= 0)
                return new FileInfo(installerPath).Length > 0;

            return new FileInfo(installerPath).Length == expectedSize;
        }

        private static async Task DownloadInstallerAsync(string downloadUrl, string destinationPath, long expectedSize)
        {
            string tempPath = destinationPath + ".download";

            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                using HttpResponseMessage response = await UpdateHttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                await using Stream downloadStream = await response.Content.ReadAsStreamAsync();
                await using FileStream fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await downloadStream.CopyToAsync(fileStream);
                await fileStream.FlushAsync();

                if (expectedSize > 0 && new FileInfo(tempPath).Length != expectedSize)
                    throw new IOException("La taille de l'installeur téléchargé ne correspond pas à la release GitHub.");

                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);

                File.Move(tempPath, destinationPath);
            }
            catch
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch { }

                throw;
            }
        }

        private static void CleanupOldDownloadedInstallers(string installerToKeep)
        {
            try
            {
                if (!Directory.Exists(UpdatesDirectory))
                    return;

                foreach (string installerPath in Directory.GetFiles(UpdatesDirectory, "*.exe"))
                {
                    if (installerPath.Equals(installerToKeep, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        File.Delete(installerPath);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void QueuePreviouslyDownloadedUpdateIfNeeded()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_lastDownloadedReleaseTag) ||
                    string.IsNullOrWhiteSpace(_lastDownloadedInstallerName) ||
                    !IsReleaseNewerThanCurrent(_lastDownloadedReleaseTag))
                {
                    return;
                }

                string installerPath = GetInstallerDownloadPath(_lastDownloadedInstallerName);
                if (!File.Exists(installerPath))
                    return;

                _pendingUpdateReleaseTag = _lastDownloadedReleaseTag;
                _pendingUpdateReleaseName = _lastDownloadedReleaseTag;
                _pendingUpdateInstallerPath = installerPath;
                QueueUpdatePromptIfPossible();
            }
            catch (Exception ex)
            {
                Log($"Impossible de préparer la mise à jour déjà téléchargée : {ex.Message}");
            }
        }

        private bool CanShowUpdatePrompt()
        {
            if (_hasShownUpdatePromptThisSession ||
                string.IsNullOrWhiteSpace(_pendingUpdateInstallerPath) ||
                !File.Exists(_pendingUpdateInstallerPath))
            {
                return false;
            }

            if (_isMandatoryNoticeFlowActive)
                return false;

            if (OnboardingOverlay?.Visibility == Visibility.Visible || SuccessOverlay?.Visibility == Visibility.Visible)
                return false;

            if (ControlPanel == null || ControlPanel.Visibility != Visibility.Visible || !ControlPanel.IsVisible)
                return false;

            if (PanelContent == null || PanelContent.Visibility != Visibility.Visible)
                return false;

            return true;
        }

        private void QueueUpdatePromptIfPossible()
        {
            if (_hasShownUpdatePromptThisSession)
                return;

            if (CanShowUpdatePrompt())
            {
                Dispatcher.BeginInvoke(new Action(PromptForDownloadedUpdate), DispatcherPriority.Background);
                return;
            }

            StartUpdatePromptTimer();
        }

        private void StartUpdatePromptTimer()
        {
            if (_updatePromptTimer != null)
                return;

            _updatePromptTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _updatePromptTimer.Tick += (_, _) =>
            {
                if (!CanShowUpdatePrompt())
                    return;

                StopUpdatePromptTimer();
                PromptForDownloadedUpdate();
            };
            _updatePromptTimer.Start();
        }

        private void StopUpdatePromptTimer()
        {
            if (_updatePromptTimer == null)
                return;

            _updatePromptTimer.Stop();
            _updatePromptTimer = null;
        }

        private void PromptForDownloadedUpdate()
        {
            if (!CanShowUpdatePrompt())
                return;

            _hasShownUpdatePromptThisSession = true;

            string releaseLabel = !string.IsNullOrWhiteSpace(_pendingUpdateReleaseName)
                ? _pendingUpdateReleaseName
                : _pendingUpdateReleaseTag;
            string title = Application.Current?.Resources["TxtUpdateReadyTitle"] as string ?? "Mise à jour FROST";
            string template = Application.Current?.Resources["TxtUpdateReadyMessage"] as string
                ?? "La mise à jour {0} a été téléchargée. Voulez-vous lancer l'installeur maintenant ? Votre configuration et la Notice déjà validée seront conservées.";
            string message = string.Format(System.Globalization.CultureInfo.CurrentCulture, template, releaseLabel);

            MessageBoxResult result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes)
            {
                LaunchDownloadedInstallerAndExit();
            }
        }

        private void LaunchDownloadedInstallerAndExit()
        {
            if (string.IsNullOrWhiteSpace(_pendingUpdateInstallerPath) || !File.Exists(_pendingUpdateInstallerPath))
                return;

            try
            {
                SaveGridConfig();
                Process.Start(new ProcessStartInfo
                {
                    FileName = _pendingUpdateInstallerPath,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(_pendingUpdateInstallerPath),
                    UseShellExecute = true
                });
                Log($"Installeur de mise à jour lancé : {_pendingUpdateInstallerPath}");
                Close();
            }
            catch (Exception ex)
            {
                _hasShownUpdatePromptThisSession = false;
                Log($"Impossible de lancer l'installeur de mise à jour : {ex.Message}");
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _windowHandle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(_windowHandle);
            source.AddHook(HwndHook);

            // Enregistrement des raccourcis
            RegisterHotKey(_windowHandle, HOTKEY_ID_F2, MOD_NONE, _vkStart);
            RegisterHotKey(_windowHandle, HOTKEY_ID_F3, MOD_NONE, _vkToggle);
            RegisterHotKey(_windowHandle, HOTKEY_ID_F4, MOD_NONE, _vkClear);

            // Enregistrement du raccourci global Debug : Ctrl + Alt + Shift + A
            RegisterHotKey(_windowHandle, HOTKEY_ID_DEBUG, MOD_CONTROL | MOD_ALT | MOD_SHIFT, VK_A);

            // Lancement du timer qui gère la transparence absolue
            _transparencyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _transparencyTimer.Tick += TransparencyTimer_Tick;
            _transparencyTimer.Start();
            Log("Hooks et raccourcis initialisés.");
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_F2) StartSequence();
                else if (id == HOTKEY_ID_F3) ToggleVisibility();
                else if (id == HOTKEY_ID_F4) ClearSequence();
                else if (id == HOTKEY_ID_DEBUG) ToggleDebugMode();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void Hotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (sender is TextBox txt && e.Key != Key.Escape && e.Key != Key.Enter && e.Key != Key.Tab)
            {
                uint vk = (uint)KeyInterop.VirtualKeyFromKey(e.Key);
                txt.Text = e.Key.ToString();

                UnregisterHotKey(_windowHandle, HOTKEY_ID_F2);
                UnregisterHotKey(_windowHandle, HOTKEY_ID_F3);
                UnregisterHotKey(_windowHandle, HOTKEY_ID_F4);

                if (txt.Tag?.ToString() == "Start") _vkStart = vk;
                if (txt.Tag?.ToString() == "Toggle") _vkToggle = vk;
                if (txt.Tag?.ToString() == "Clear") _vkClear = vk;

                RegisterHotKey(_windowHandle, HOTKEY_ID_F2, MOD_NONE, _vkStart);
                RegisterHotKey(_windowHandle, HOTKEY_ID_F3, MOD_NONE, _vkToggle);
                RegisterHotKey(_windowHandle, HOTKEY_ID_F4, MOD_NONE, _vkClear);

                SaveGridConfig();
                Log($"Raccourci mis à jour: {txt.Tag} -> {e.Key}");
            }
        }

        private void TransparencyTimer_Tick(object? sender, EventArgs e)
        {
            UpdateTrackedGameWindowAnchor();

            // Pendant le ciblage ou les ecrans guides, la fenetre doit intercepter les clics
            bool requiresWpfCapture = (_currentState != AppState.Idle) ||
               (OnboardingOverlay != null && OnboardingOverlay.Visibility == Visibility.Visible) ||
               (SuccessOverlay != null && SuccessOverlay.Visibility == Visibility.Visible) ||
               _isDebugMode;
            if (requiresWpfCapture)
            {
                SetWindowClickThrough(false);
                return;
            }

            if (_isAutoHiddenBecauseGameWindowUnavailable || Opacity <= 0.01)
            {
                SetWindowClickThrough(true);
                return;
            }

            if (DateTime.UtcNow < _trackedWindowInteractionSuppressionUntilUtc)
            {
                SetWindowClickThrough(true);
                return;
            }

            if (GetCursorPos(out POINT lpPoint))
            {
                try
                {
                    Point mouseScreen = new Point(lpPoint.X, lpPoint.Y);
                    Point mouseRelative = this.PointFromScreen(mouseScreen);

                    bool mouseOverPanel = IsMouseOverControlPanel(mouseRelative);
                    SetWindowClickThrough(!mouseOverPanel); // Hors du menu = 100% fantôme (clics au travers)
                }
                catch { SetWindowClickThrough(false); }
            }
        }

        private bool IsMouseOverControlPanel(Point mouseRelative)
        {
            if (ControlPanel == null || !ControlPanel.IsLoaded || !ControlPanel.IsVisible)
            {
                return false;
            }

            GeneralTransform transform = ControlPanel.TransformToAncestor(this);
            Rect panelBounds = transform.TransformBounds(new Rect(0, 0, ControlPanel.ActualWidth, ControlPanel.ActualHeight));
            panelBounds.Inflate(30, 30);
            return panelBounds.Contains(mouseRelative);
        }

        private void SetWindowClickThrough(bool clickThrough)
        {
            if (_isClickThrough == clickThrough) return;

            int extendedStyle = GetWindowLong(_windowHandle, GWL_EXSTYLE);
            if (clickThrough)
                SetWindowLong(_windowHandle, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
            else
                SetWindowLong(_windowHandle, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);

            _isClickThrough = clickThrough;
        }

        private bool IsInteractionLockedByMandatoryNotice()
        {
            if (!_isMandatoryNoticeFlowActive) return false;

            Log("Action ignorée tant que la Notice obligatoire n'est pas terminée.");
            return true;
        }

        private void RestoreWindowForInteractiveFlow()
        {
            bool needsRestore = _isAutoHiddenBecauseGameWindowUnavailable ||
                                Visibility != Visibility.Visible ||
                                Opacity <= 0.01;
            if (!needsRestore)
            {
                return;
            }

            _isAutoHiddenBecauseGameWindowUnavailable = false;
            _shouldRestoreWindowAfterGameWindowReturns = false;
            _trackedWindowInteractionSuppressionUntilUtc = DateTime.UtcNow.AddMilliseconds(250);

            DisplayScreen? targetScreen = GetSelectedOrPrimaryScreen();
            RunWithoutLayoutPersistence(() =>
            {
                if (Visibility != Visibility.Visible)
                {
                    Visibility = Visibility.Visible;
                }

                ApplySavedWindowBounds();
                if (targetScreen != null)
                {
                    ClampWindowToScreen(targetScreen);
                }

                ApplySavedControlPanelBounds();
                ClampControlPanelToWindow();
                UpdateLayout();
                ClampControlPanelToWindow();

                Opacity = 1.0;
                Topmost = true;
            });

            SetWindowClickThrough(false);
        }

        private bool EnsureMandatoryNoticeFlowVisible()
        {
            if (!_isMandatoryNoticeFlowActive)
            {
                return false;
            }

            RestoreWindowForInteractiveFlow();
            ShowMandatoryNoticeStep(_mandatoryNoticeStep);
            return true;
        }
        // --- CONVERSIONS MATHÉMATIQUES ABSOLUES ---
        private Point PointToCell(Point p)
        {
            if (TryGetCurrentGridTransform(out GridTransform transform))
            {
                return transform.PointToCell(p, ValidMapCells);
            }

            return InvalidCell;
        }

        private Point CellToPoint(Point cell)
        {
            if (TryGetCurrentGridTransform(out GridTransform transform))
            {
                return transform.CellToPoint(cell);
            }

            return InvalidCell;
        }

        private static IReadOnlyList<Point> BuildValidMapCells()
        {
            List<Point> cells = new List<Point>(560);
            for (int cellId = 0; cellId < 560; cellId++)
            {
                int cellX = (cellId % 14) + (cellId / 28);
                int cellY = (cellId / 28) - (cellId % 14);
                if (cellId % 28 >= 14)
                    cellX += 1;

                cells.Add(new Point(cellX - DofusMapAnchorCellX, cellY - DofusMapAnchorCellY));
            }

            return cells;
        }

        private static HashSet<(int X, int Y)> BuildValidMapCellLookup()
        {
            HashSet<(int X, int Y)> lookup = new HashSet<(int X, int Y)>();
            foreach (Point cell in ValidMapCells)
            {
                lookup.Add(((int)cell.X, (int)cell.Y));
            }

            return lookup;
        }

        private static bool IsValidMapCell(Point cell)
        {
            if (!IsFiniteNumber(cell.X) || !IsFiniteNumber(cell.Y))
                return false;

            int roundedX = (int)Math.Round(cell.X, MidpointRounding.AwayFromZero);
            int roundedY = (int)Math.Round(cell.Y, MidpointRounding.AwayFromZero);
            return Math.Abs(cell.X - roundedX) < 0.001 &&
                   Math.Abs(cell.Y - roundedY) < 0.001 &&
                   ValidMapCellLookup.Contains((roundedX, roundedY));
        }

        private static bool IsSameCell(Point a, Point b)
        {
            return a.X == b.X && a.Y == b.Y;
        }

        private GridCalibrationProfile CaptureCurrentCalibrationProfile()
        {
            GridCalibrationProfile profile = GetCalibrationProfile(_selectedGameDisplayMode).Clone();
            EnsureCalibrationProfileReferenceSize(profile);
            return profile;
        }

        private GridCalibrationProfile GetCalibrationProfile(GameDisplayMode mode)
        {
            return mode == GameDisplayMode.Windowed ? _windowedCalibration : _fullscreenCalibration;
        }

        private void ApplyCalibrationProfile(GridCalibrationProfile profile)
        {
            EnsureCalibrationProfileReferenceSize(profile);

            if (TryBuildGridTransform(profile, out GridTransform transform))
            {
                _gridOffsetX = transform.AnchorX;
                _gridOffsetY = transform.AnchorY;
                _tileWidth = transform.TileWidth;
                _tileHeight = transform.TileHeight;
                return;
            }

            _gridOffsetX = profile.OffsetX;
            _gridOffsetY = profile.OffsetY;
            _tileWidth = profile.TileWidth;
            _tileHeight = profile.TileHeight;
        }

        private void StoreCurrentCalibrationToSelectedMode()
        {
            GridCalibrationProfile currentProfile = CaptureCurrentCalibrationProfile();

            if (_selectedGameDisplayMode == GameDisplayMode.Windowed)
            {
                _windowedCalibration = currentProfile;
            }
            else
            {
                _fullscreenCalibration = currentProfile;
            }
        }

        private void ApplySelectedDisplayModeCalibration()
        {
            ApplyCalibrationProfile(GetCalibrationProfile(_selectedGameDisplayMode));
        }

        private void RefreshViewModeButtons()
        {
            bool isCompact = IsEffectiveCompactMode();

            ApplySelectionButtonState(BtnViewFull, !isCompact);
            ApplySelectionButtonState(BtnViewCompact, isCompact);
            ApplySelectionButtonState(BtnOnbViewFull, !isCompact);
            ApplySelectionButtonState(BtnOnbViewCompact, isCompact);
        }

        private void ApplySelectionButtonState(Button? button, bool isSelected)
        {
            if (button == null) return;

            SolidColorBrush themeBrush = new SolidColorBrush(_themeColor);
            SolidColorBrush defaultBorder = new SolidColorBrush(Color.FromRgb(50, 53, 64));
            SolidColorBrush defaultBackground = new SolidColorBrush(Color.FromRgb(35, 37, 46));
            SolidColorBrush selectedBackground = new SolidColorBrush(Color.FromRgb(45, 48, 59));

            button.BorderBrush = isSelected ? themeBrush : defaultBorder;
            button.Foreground = isSelected ? themeBrush : Brushes.White;
            button.Background = isSelected ? selectedBackground : defaultBackground;
        }

        private void StartSequence()
        {
            if (IsInteractionLockedByMandatoryNotice()) return;

            _currentState = AppState.WaitingForPlayer;
            _isPlayerSet = false;
            _isMonsterSet = false;
            SetStatus(Application.Current?.Resources["StatusStep1"] as string ?? "Étape 1 : Cliquez sur VOTRE personnage.", Brushes.DeepSkyBlue);

            OverlayCanvas.Visibility = Visibility.Visible;
            OverlayCanvas.IsHitTestVisible = true;
            OverlayCanvas.Children.Clear();
            OverlayCanvas.Background = new SolidColorBrush(Color.FromArgb((byte)1, (byte)0, (byte)0, (byte)0));
            OverlayCanvas.Cursor = Cursors.Hand;
            RefreshOverlay();
            Log("Séquence de ciblage démarrée.");
        }

        private void ToggleVisibility()
        {
            if (IsInteractionLockedByMandatoryNotice()) return;

            _isOverlayEnabled = !_isOverlayEnabled;
            _isAutoHiddenBecauseGameWindowUnavailable = false;

            if (!_isOverlayEnabled)
            {
                this.Visibility = Visibility.Hidden;
                SetStatus(Application.Current?.Resources["StatusHidden"] as string ?? "Affichage masqué.", Brushes.Gray);
                Log("Application masquée manuellement.");
            }
            else
            {
                this.Visibility = Visibility.Visible;
                SetStatus(Application.Current?.Resources["StatusActive"] as string ?? "Affichage actif.", Brushes.LightGreen);
                Log("Application affichée manuellement.");
            }

            RefreshOverlay();
        }

        private void ClearSequence()
        {
            if (IsInteractionLockedByMandatoryNotice()) return;

            _currentState = AppState.Idle;
            OverlayCanvas.Children.Clear();
            _isPlayerSet = false;
            _isMonsterSet = false;
            OverlayCanvas.Background = null;
            OverlayCanvas.IsHitTestVisible = false;
            OverlayCanvas.Visibility = Visibility.Visible;

            // Reset des coups au corps à corps (Changement de tour)
            _hitCount = 0;
            TxtHitCount.Text = "0";

            // Vide le champ de texte du log de combat
            if (TxtCombatLog != null) TxtCombatLog.Text = "";

            SetStatus(Application.Current?.Resources["StatusReady"] as string ?? "Prêt. En attente...", Brushes.Gray);
            OverlayCanvas.Cursor = Cursors.Arrow; // Remet la souris normale
            RefreshOverlay();
            Log("Séquence effacée.");
        }

        private void ToggleDebugMode()
        {
            _isDebugMode = !_isDebugMode;
            if (_isDebugMode)
            {
                this.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x1A, 0x1C, 0x23)); // Fond sombre
                this.ResizeMode = ResizeMode.CanResizeWithGrip;
                SetStatus("Mode Édition activé (Redimensionnez la fenêtre librement).", Brushes.Orange);
                RunGridTransformSimulationTests();
            }
            else
            {
                this.Background = null; // Invisible
                this.ResizeMode = ResizeMode.CanResize;
                SetStatus(Application.Current?.Resources["StatusReady"] as string ?? "Prêt. En attente...", Brushes.Gray);
            }
            Log($"Mode Édition de la fenêtre : {_isDebugMode}");
        }

        private void OverlayCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            if (_currentState == AppState.WaitingForPlayer)
            {
                Point clickedCell = PointToCell(e.GetPosition(OverlayCanvas));
                if (!IsValidMapCell(clickedCell))
                    return;

                _playerCell = clickedCell;
                _isPlayerSet = true;
                _lastHoveredCell = InvalidCell; // Réinitialise le survol

                _currentState = AppState.WaitingForMonster;
                SetStatus(Application.Current?.Resources["StatusStep2"] as string ?? "Étape 2 : Cliquez sur la CIBLE.", Brushes.Crimson);
                RefreshOverlay();
                Log($"Joueur placé en ({_playerCell.X}, {_playerCell.Y})");
            }
            else if (_currentState == AppState.WaitingForMonster)
            {
                if (!IsSameCell(_lastHoveredCell, InvalidCell))
                    _monsterCell = _lastHoveredCell;
                else
                {
                    Point clickedCell = PointToCell(e.GetPosition(OverlayCanvas));
                    if (!IsValidMapCell(clickedCell))
                        return;

                    _monsterCell = clickedCell;
                }

                _isMonsterSet = true;
                _currentState = AppState.Idle;
                OverlayCanvas.Background = null; // Libère le focus pour jouer !
                OverlayCanvas.IsHitTestVisible = false;
                OverlayCanvas.Cursor = Cursors.Arrow; // Remet la souris normale

                SetStatus(Application.Current?.Resources["StatusCalculated"] as string ?? "Cible calculée ! Focus libéré.", new SolidColorBrush(Color.FromRgb(108, 14, 186)));
                RefreshOverlay();
                Log($"Monstre placé en ({_monsterCell.X}, {_monsterCell.Y}). Calcul terminé.");
            }
        }

        private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // Logique de survol
            if (_currentState == AppState.WaitingForMonster)
            {
                Point cell = PointToCell(e.GetPosition(OverlayCanvas));
                if (!IsValidMapCell(cell))
                {
                    if (!IsSameCell(_lastHoveredCell, InvalidCell))
                    {
                        _lastHoveredCell = InvalidCell;
                        RefreshOverlay();
                    }
                    return;
                }

                if (!IsSameCell(_lastHoveredCell, cell))
                {
                    _lastHoveredCell = cell;
                    RefreshOverlay();
                }
            }
        }

        private void OverlayCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
        }

        private Color GetPlayerColor()
        {
            if (_colorblindMode == 1 || _colorblindMode == 2) return Color.FromRgb(46, 204, 113); // Protanopie/Deutéranopie (Vert)
            if (_colorblindMode == 3) return Color.FromRgb(255, 255, 255); // Tritanopie (Blanc)
            return Color.FromRgb(32, 207, 255); // Normal (Bleu FROST)
        }

        private Color GetBossColor()
        {
            if (_colorblindMode == 1 || _colorblindMode == 2) return Color.FromRgb(213, 94, 0); // Protanopie/Deutéranopie (Orange vif)
            if (_colorblindMode == 3) return Color.FromRgb(255, 0, 0); // Tritanopie (Rouge)
            return Color.FromRgb(255, 78, 102); // Normal (Rouge corail)
        }

        private Color GetTargetColor()
        {
            if (_colorblindMode == 1 || _colorblindMode == 2) return Color.FromRgb(0, 100, 255); // Protanopie/Deuteranopie
            if (_colorblindMode == 3) return Color.FromRgb(255, 0, 0); // Tritanopie
            return Color.FromRgb(155, 92, 255); // Normal (Violet)
        }

        private Color GetTPColor()
        {
            if (_colorblindMode == 1 || _colorblindMode == 2) return Color.FromRgb(255, 200, 0); // Protanopie/Deuteranopie
            if (_colorblindMode == 3) return Color.FromRgb(0, 200, 255); // Tritanopie
            return Color.FromRgb(255, 106, 179); // Normal (Rose)
        }

        private void UpdateLegend(Border b, Color c)
        {
            if (b != null)
            {
                b.Background = new SolidColorBrush(c);
                b.BorderBrush = new SolidColorBrush(Color.FromArgb(255, (byte)Math.Min(255, c.R + 40), (byte)Math.Min(255, c.G + 40), (byte)Math.Min(255, c.B + 40)));
                if (b.Effect is DropShadowEffect drop) drop.Color = c;
            }
        }

        private void UpdateLegendColors()
        {
            Color playerColor = GetPlayerColor();
            Color bossColor = GetBossColor();
            Color targetColor = GetTargetColor();
            Color tpColor = GetTPColor();

            UpdateLegend(LegendPlayer, playerColor);
            UpdateLegend(LegendBoss, bossColor);
            UpdateLegend(LegendTarget, targetColor);
            UpdateLegend(LegendTP, tpColor);

            UpdateLegend(NoticeLegendPlayer, playerColor);
            UpdateLegend(NoticeLegendBoss, bossColor);
            UpdateLegend(NoticeLegendTarget, targetColor);
            UpdateLegend(NoticeLegendTP, tpColor);
        }

        private void DrawMechanics(Point monsterCell, double opacity)
        {
            Color jokerViolet = GetTargetColor();
            Color barbiePink = GetTPColor();

            string targetLabel = Application.Current?.Resources["TxtLabelTarget"] as string ?? "Frappe";
            string tpPlayerLabel = Application.Current?.Resources["TxtLabelTPPlayer"] as string ?? "TP Joueur";
            string tpBossLabel = Application.Current?.Resources["TxtLabelTPBoss"] as string ?? "TP Comte";
            double dc = monsterCell.X - _playerCell.X;
            double dr = monsterCell.Y - _playerCell.Y;

            double angleStep = 90;
            double totalAngle = (_currentAngle + (_hitCount * angleStep)) % 360;
            if (totalAngle < 0) totalAngle += 360;

            double targetC = 0;
            double targetR = 0;

            if (totalAngle == 90) { targetC = dr; targetR = -dc; }
            else if (totalAngle == 180) { targetC = -dc; targetR = -dr; }
            else if (totalAngle == 270) { targetC = -dr; targetR = dc; }
            else if (totalAngle == 0) { targetC = dc; targetR = dr; }

            Point targetCell = new Point(_playerCell.X + targetC, _playerCell.Y + targetR);
            DrawMarker(CellToPoint(targetCell), jokerViolet, true, targetLabel, opacity);

            if (_isBossTarget)
            {
                if (_isOddTurn)
                {
                    double tpC = _monsterCell.X + (_monsterCell.X - _playerCell.X);
                    double tpR = _monsterCell.Y + (_monsterCell.Y - _playerCell.Y);
                    DrawMarker(CellToPoint(new Point(tpC, tpR)), barbiePink, false, tpPlayerLabel, opacity);
                }
                else
                {
                    double tpC = _playerCell.X + (_playerCell.X - _monsterCell.X);
                    double tpR = _playerCell.Y + (_playerCell.Y - _monsterCell.Y);
                    DrawMarker(CellToPoint(new Point(tpC, tpR)), barbiePink, false, tpBossLabel, opacity);
                }
            }
        }

        private void DrawMarker(Point p, Color color, bool isTarget = false, string label = "", double opacity = 1.0, string description = "")
        {
            double width = _tileWidth;
            double height = _tileHeight;

            // 1. Ombre portée sous la case
            Polygon shadow = new Polygon
            {
                Points = new PointCollection { new Point(width / 2, 0), new Point(width, height / 2), new Point(width / 2, height), new Point(0, height / 2) },
                Fill = Brushes.Black,
                Opacity = 0.4 * opacity,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(shadow, p.X - (width / 2));
            Canvas.SetTop(shadow, p.Y - (height / 2) + 3);
            OverlayCanvas.Children.Add(shadow);

            // 2. Dégradé de fond (style verre/tactique)
            LinearGradientBrush gradient = new LinearGradientBrush(
                Color.FromArgb(140, color.R, color.G, color.B), // Haut plus coloré
                Color.FromArgb(30, color.R, color.G, color.B),  // Bas plus transparent
                new Point(0.5, 0), new Point(0.5, 1));

            // 3. Case Principale
            Polygon tile = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(width / 2, 0),
                    new Point(width, height / 2),
                    new Point(width / 2, height),
                    new Point(0, height / 2)
                },
                Fill = gradient,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2.0,
                Opacity = opacity,
                StrokeLineJoin = PenLineJoin.Round,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(tile, p.X - (width / 2));
            Canvas.SetTop(tile, p.Y - (height / 2));
            OverlayCanvas.Children.Add(tile);

            // 4. Bordure intérieure brillante (Effet Hightech Dofus)
            Polygon innerTile = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(width / 2, 4),
                    new Point(width - 6, height / 2),
                    new Point(width / 2, height - 4),
                    new Point(6, height / 2)
                },
                Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                StrokeThickness = 1,
                Opacity = opacity,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(innerTile, p.X - (width / 2));
            Canvas.SetTop(innerTile, p.Y - (height / 2));
            OverlayCanvas.Children.Add(innerTile);

            // 5. Étiquette (Label) modernisée
            if (!string.IsNullOrEmpty(label) || !string.IsNullOrEmpty(description))
            {
                Border labelBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(232, 12, 16, 22)),
                    BorderBrush = new SolidColorBrush(color),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(7, 3, 7, 4),
                    Opacity = opacity,
                    IsHitTestVisible = false
                };

                StackPanel labelStack = new StackPanel { Orientation = Orientation.Vertical };
                if (!string.IsNullOrEmpty(label))
                {
                    labelStack.Children.Add(new TextBlock
                    {
                        Text = label,
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold,
                        FontSize = 10,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    });
                }

                if (!string.IsNullOrEmpty(description))
                {
                    labelStack.Children.Add(new TextBlock
                    {
                        Text = description,
                        Foreground = new SolidColorBrush(Color.FromArgb(230, 214, 222, 235)),
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 8.5,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        MaxWidth = Math.Max(80, width * 1.8),
                        TextWrapping = TextWrapping.Wrap
                    });
                }

                labelBorder.Child = labelStack;

                labelBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(labelBorder, p.X - (labelBorder.DesiredSize.Width / 2));
                Canvas.SetTop(labelBorder, p.Y - (height / 2) - labelBorder.DesiredSize.Height - 6);
                OverlayCanvas.Children.Add(labelBorder);
            }
        }

        private void RefreshOverlay()
        {
            OverlayCanvas.Children.Clear();

            string playerLabel = Application.Current?.Resources["TxtLabelPlayer"] as string ?? "Joueur";
            string bossLabel = Application.Current?.Resources["TxtLabelBoss"] as string ?? "Cible";

            if (_isPlayerSet)
                DrawMarker(CellToPoint(_playerCell), GetPlayerColor(), false, playerLabel, 1.0);

            if (_currentState == AppState.Idle && _isMonsterSet)
            {
                DrawMarker(CellToPoint(_monsterCell), GetBossColor(), false, bossLabel, 1.0);
                DrawMechanics(_monsterCell, 1.0);
            }
            else if (_currentState == AppState.WaitingForMonster && !IsSameCell(_lastHoveredCell, InvalidCell))
            {
                DrawMarker(CellToPoint(_lastHoveredCell), GetBossColor(), false, bossLabel, 0.4);
                DrawMechanics(_lastHoveredCell, 0.6);
            }
        }

        private void UpdateThresholdUI()
        {
            SolidColorBrush themeBrush = new SolidColorBrush(_themeColor);
            SolidColorBrush defaultBrush = new SolidColorBrush(Color.FromRgb(50, 53, 64));

            if (BtnSeuil1 != null) { BtnSeuil1.Opacity = _currentSeuil == 1 ? 1.0 : 0.4; BtnSeuil1.BorderBrush = _currentSeuil == 1 ? themeBrush : defaultBrush; }
            if (BtnSeuil2 != null) { BtnSeuil2.Opacity = _currentSeuil == 2 ? 1.0 : 0.4; BtnSeuil2.BorderBrush = _currentSeuil == 2 ? themeBrush : defaultBrush; }
            if (BtnSeuil3 != null) { BtnSeuil3.Opacity = _currentSeuil == 3 ? 1.0 : 0.4; BtnSeuil3.BorderBrush = _currentSeuil == 3 ? themeBrush : defaultBrush; }
            if (BtnSeuil4 != null) { BtnSeuil4.Opacity = _currentSeuil == 4 ? 1.0 : 0.4; BtnSeuil4.BorderBrush = _currentSeuil == 4 ? themeBrush : defaultBrush; }
            if (BtnSeuil5 != null) { BtnSeuil5.Opacity = _currentSeuil == 5 ? 1.0 : 0.4; BtnSeuil5.BorderBrush = _currentSeuil == 5 ? themeBrush : defaultBrush; }

            UpdateCompactThresholdButton(BtnSeuil1Compact, 1);
            UpdateCompactThresholdButton(BtnSeuil2Compact, 2);
            UpdateCompactThresholdButton(BtnSeuil3Compact, 3);
            UpdateCompactThresholdButton(BtnSeuil4Compact, 4);
            UpdateCompactThresholdButton(BtnSeuil5Compact, 5);

            void UpdateCompactThresholdButton(Button? button, int seuil)
            {
                if (button == null) return;

                button.Opacity = _currentSeuil == seuil ? 1.0 : 0.4;
                button.BorderBrush = _currentSeuil == seuil ? themeBrush : defaultBrush;
            }
            if (BtnSeuil1Compact != null) BtnSeuil1Compact.Opacity = _currentSeuil == 1 ? 1.0 : 0.4;
            if (BtnSeuil2Compact != null) BtnSeuil2Compact.Opacity = _currentSeuil == 2 ? 1.0 : 0.4;
            if (BtnSeuil3Compact != null) BtnSeuil3Compact.Opacity = _currentSeuil == 3 ? 1.0 : 0.4;
            if (BtnSeuil4Compact != null) BtnSeuil4Compact.Opacity = _currentSeuil == 4 ? 1.0 : 0.4;
            if (BtnSeuil5Compact != null) BtnSeuil5Compact.Opacity = _currentSeuil == 5 ? 1.0 : 0.4;
        }

        private void SetSeuil(int seuil, double angle)
        {
            _currentSeuil = seuil;
            _currentAngle = angle;

            UpdateThresholdUI();
            RefreshOverlay();
        }

        private void BtnThreshold_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagStr)
            {
                var parts = tagStr.Split('_');
                if (parts.Length == 2 && int.TryParse(parts[0], out int seuil) && double.TryParse(parts[1], out double angle))
                {
                    SetSeuil(seuil, angle);
                }
            }
        }

        private void TxtCombatLog_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtCombatLogPlaceholder != null)
            {
                TxtCombatLogPlaceholder.Visibility = string.IsNullOrEmpty(TxtCombatLog.Text) ? Visibility.Visible : Visibility.Collapsed;
            }

            if (TxtCombatLog == null) return;
            string text = TxtCombatLog.Text?.ToLower() ?? "";
            if (string.IsNullOrWhiteSpace(text)) return;

            // 1. Détection par l'état dans le log de combat (ex: "[16:12] Fee-De-Jade : Confusion horaire : 90 degrés")
            if (text.Contains("contre-horaire") || text.Contains("contre horaire") || text.Contains("270"))
                SetSeuil(2, 270);
            else if (text.Contains("horaire") && !text.Contains("contre"))
                SetSeuil(1, 90);
            else if (text.Contains("180") || text.Contains("pi"))
                SetSeuil(3, 180);
            else
            {
                // 2. Détection par pourcentage de points de vie saisis manuellement ou copiés
                // Cherche d'abord un nombre suivi de % (ex: "82%"), sinon prend le dernier nombre pour ignorer l'heure "[16:12]"
                System.Text.RegularExpressions.Match matchPct = System.Text.RegularExpressions.Regex.Match(text, @"(\d{1,3})\s*%");
                int val = -1;

                if (matchPct.Success)
                {
                    val = int.Parse(matchPct.Groups[1].Value);
                }
                else
                {
                    System.Text.RegularExpressions.MatchCollection matches = System.Text.RegularExpressions.Regex.Matches(text, @"\b(\d{1,3})\b");
                    if (matches.Count > 0)
                    {
                        val = int.Parse(matches[matches.Count - 1].Groups[1].Value);
                    }
                }

                if (val != -1)
                {
                    if (val >= 90 && val <= 100) SetSeuil(1, 90);
                    else if (val >= 75 && val <= 89) SetSeuil(2, 270);
                    else if (val >= 45 && val <= 74) SetSeuil(3, 180);
                    else if (val >= 30 && val <= 44) SetSeuil(4, 270);
                    else if (val >= 0 && val <= 29) SetSeuil(5, 90);
                }
            }
        }

        // Logique d'incrémentation/décrémentation du nombre de coups
        private void BtnHitMinus_Click(object sender, RoutedEventArgs e)
        {
            if (_hitCount > 0)
            {
                _hitCount--;
                TxtHitCount.Text = _hitCount.ToString();
                RefreshOverlay();
            }
        }

        private void BtnHitPlus_Click(object sender, RoutedEventArgs e)
        {
            if (_hitCount < 20) // Limite de sécurité
            {
                _hitCount++;
                TxtHitCount.Text = _hitCount.ToString();
                RefreshOverlay();
            }
        }

        private void BtnTargetType_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                _isBossTarget = btn.Tag.ToString() == "Boss";

                SolidColorBrush themeBrush = new SolidColorBrush(_themeColor);
                BtnTargetMob.Background = BtnTargetBoss.Background = new SolidColorBrush(Color.FromRgb(26, 28, 35));
                BtnTargetMob.Foreground = BtnTargetBoss.Foreground = new SolidColorBrush(Color.FromRgb(122, 128, 144));
                BtnTargetMob.BorderBrush = BtnTargetBoss.BorderBrush = new SolidColorBrush(Color.FromRgb(50, 53, 64));

                btn.Background = new SolidColorBrush(Color.FromRgb(45, 48, 59));
                btn.Foreground = themeBrush;
                btn.BorderBrush = themeBrush;

                GridTurnType.Visibility = _isBossTarget ? Visibility.Visible : Visibility.Collapsed;
                QueueAutoFitControlPanelHeight(saveAfterFit: true);
                RefreshOverlay();
            }
        }

        private void BtnTurn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                _isOddTurn = btn.Tag.ToString() == "Odd";

                SolidColorBrush themeBrush = new SolidColorBrush(_themeColor);
                BtnTurnOdd.Background = BtnTurnEven.Background = new SolidColorBrush(Color.FromRgb(26, 28, 35));
                BtnTurnOdd.Foreground = BtnTurnEven.Foreground = new SolidColorBrush(Color.FromRgb(122, 128, 144));
                BtnTurnOdd.BorderBrush = BtnTurnEven.BorderBrush = new SolidColorBrush(Color.FromRgb(50, 53, 64));

                btn.Background = new SolidColorBrush(Color.FromRgb(45, 48, 59));
                btn.Foreground = themeBrush;
                btn.BorderBrush = themeBrush;

                RefreshOverlay();
            }
        }
        // Boutons de l'UI raccordés aux mêmes méthodes que les raccourcis clavier
        private void BtnStart_Click(object sender, RoutedEventArgs e) => StartSequence();
        private void BtnToggle_Click(object sender, RoutedEventArgs e) => ToggleVisibility();
        private void BtnClear_Click(object sender, RoutedEventArgs e) => ClearSequence();

        // Fermeture de l'application
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Log("Fermeture de l'application cliquée.");
            Application.Current.Shutdown();
        }

        // Bascule vers la Notice d'utilisation
        private void BtnInfo_Click(object sender, RoutedEventArgs e)
        {
            if (_isMandatoryNoticeFlowActive) return;

            ShowNoticeSummaryView();
            Log("Notice ouverte.");
        }

        private void BtnNoticePage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int page))
            {
                ShowNoticePage(page, mandatoryFlow: false);
            }
        }

        private void BtnNoticeSummary_Click(object sender, RoutedEventArgs e)
        {
            if (_isMandatoryNoticeFlowActive) return;

            ShowNoticeSummaryView();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_isMandatoryNoticeFlowActive) return;

            NoticeContent.Visibility = Visibility.Collapsed;
            SettingsContent.Visibility = Visibility.Collapsed;
            PanelContent.Visibility = Visibility.Visible;
            QueueAutoFitControlPanelHeight();
        }

        private void ShowNoticeSummaryView()
        {
            PanelContent.Visibility = Visibility.Collapsed;
            SettingsContent.Visibility = Visibility.Collapsed;
            NoticeContent.Visibility = Visibility.Visible;

            if (NoticeWizardFooter != null) NoticeWizardFooter.Visibility = Visibility.Collapsed;
            if (BtnNoticeBackToApp != null) BtnNoticeBackToApp.Visibility = Visibility.Visible;
            if (BtnNoticeSummaryBack != null) BtnNoticeSummaryBack.Visibility = Visibility.Visible;
            if (NoticePageContainer != null) NoticePageContainer.Visibility = Visibility.Collapsed;
            if (NoticeSummary != null) NoticeSummary.Visibility = Visibility.Visible;

            RestorePanelHeight();
            QueueAutoFitControlPanelHeight();
        }

        private void ShowNoticePage(int page, bool mandatoryFlow)
        {
            PanelContent.Visibility = Visibility.Collapsed;
            SettingsContent.Visibility = Visibility.Collapsed;
            NoticeContent.Visibility = Visibility.Visible;

            if (NoticeSummary != null) NoticeSummary.Visibility = Visibility.Collapsed;
            if (NoticePageContainer != null) NoticePageContainer.Visibility = Visibility.Visible;
            if (BtnNoticeBackToApp != null) BtnNoticeBackToApp.Visibility = mandatoryFlow ? Visibility.Collapsed : Visibility.Visible;
            if (BtnNoticeSummaryBack != null) BtnNoticeSummaryBack.Visibility = mandatoryFlow ? Visibility.Collapsed : Visibility.Visible;
            if (NoticeWizardFooter != null) NoticeWizardFooter.Visibility = mandatoryFlow ? Visibility.Visible : Visibility.Collapsed;

            if (Page1 != null) Page1.Visibility = page == 1 ? Visibility.Visible : Visibility.Collapsed;
            if (Page2 != null) Page2.Visibility = page == 2 ? Visibility.Visible : Visibility.Collapsed;
            if (Page3 != null) Page3.Visibility = page == 3 ? Visibility.Visible : Visibility.Collapsed;
            if (Page4 != null) Page4.Visibility = page == 4 ? Visibility.Visible : Visibility.Collapsed;
            if (Page5 != null) Page5.Visibility = page == 5 ? Visibility.Visible : Visibility.Collapsed;

            RestorePanelHeight();
            QueueAutoFitControlPanelHeight();
        }

        private void BtnNoticeWizardNext_Click(object sender, RoutedEventArgs e)
        {
            if (!_isMandatoryNoticeFlowActive) return;

            if (_mandatoryNoticeStep < NoticeWizardStepCount)
            {
                ShowMandatoryNoticeStep(_mandatoryNoticeStep + 1);
                return;
            }

            CompleteMandatoryNoticeFlow();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (EnsureMandatoryNoticeFlowVisible()) return;

            PanelContent.Visibility = Visibility.Collapsed;
            NoticeContent.Visibility = Visibility.Collapsed;
            SettingsContent.Visibility = Visibility.Visible;
            RestorePanelHeight();
            Log("Paramètres ouverts.");
        }

        private void BtnBackSettings_Click(object sender, RoutedEventArgs e)
        {
            if (EnsureMandatoryNoticeFlowVisible()) return;

            SettingsContent.Visibility = Visibility.Collapsed;
            PanelContent.Visibility = Visibility.Visible;
            QueueAutoFitControlPanelHeight();
        }

        // --- WIZARD ONBOARDING ---
        private void StartOnboarding()
        {
            if (OnboardingTranslate != null)
            {
                OnboardingTranslate.X = _onbX;
                OnboardingTranslate.Y = _onbY;
            }
            OnboardingOverlay.Visibility = Visibility.Visible;
            ControlPanel.Visibility = Visibility.Collapsed; // Cache le menu temporairement
            ShowOnboardingStep(1);
        }

        private bool _isDraggingOnboarding = false;
        private Point _dragOnboardingStart;

        private void Onboarding_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDraggingOnboarding = true;
                _dragOnboardingStart = e.GetPosition(this);
                ((UIElement)sender).CaptureMouse();
            }
        }

        private void Onboarding_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingOnboarding)
            {
                Point currentPos = e.GetPosition(this);
                double deltaX = currentPos.X - _dragOnboardingStart.X;
                double deltaY = currentPos.Y - _dragOnboardingStart.Y;

                if (OnboardingTranslate != null)
                {
                    OnboardingTranslate.X += deltaX;
                    OnboardingTranslate.Y += deltaY;
                }

                _dragOnboardingStart = currentPos;
            }
        }

        private void Onboarding_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingOnboarding)
            {
                _isDraggingOnboarding = false;
                ((UIElement)sender).ReleaseMouseCapture();

                if (OnboardingTranslate != null)
                {
                    _onbX = OnboardingTranslate.X;
                    _onbY = OnboardingTranslate.Y;
                    SaveGridConfig();
                }
            }
        }

        private void ShowOnboardingStep(int step)
        {
            _onboardingStep = step;
            DotStep1.Fill = new SolidColorBrush(step >= 1 ? _themeColor : Color.FromRgb(50, 53, 64));
            DotStep2.Fill = new SolidColorBrush(step >= 2 ? _themeColor : Color.FromRgb(50, 53, 64));
            DotStep3.Fill = new SolidColorBrush(step >= 3 ? _themeColor : Color.FromRgb(50, 53, 64));
            DotStep4.Fill = new SolidColorBrush(step >= 4 ? _themeColor : Color.FromRgb(50, 53, 64));
            DotStep5.Fill = new SolidColorBrush(step >= 5 ? _themeColor : Color.FromRgb(50, 53, 64));
            if (DotStep6 != null) DotStep6.Fill = new SolidColorBrush(step >= 6 ? _themeColor : Color.FromRgb(50, 53, 64));
            if (DotStep7 != null) DotStep7.Fill = new SolidColorBrush(step >= 7 ? _themeColor : Color.FromRgb(50, 53, 64));

            BtnOnbNext.Content = step == 7 ? (Application.Current?.Resources["TxtFinish"] as string ?? "Terminer") : (Application.Current?.Resources["TxtNext"] as string ?? "Suivant ➔");

            if (GridOnbLang != null) GridOnbLang.Visibility = Visibility.Collapsed;
            if (GridOnbWelcome != null) GridOnbWelcome.Visibility = Visibility.Collapsed;
            if (GridOnbScreen != null) GridOnbScreen.Visibility = Visibility.Collapsed;
            if (GridOnbCb != null) GridOnbCb.Visibility = Visibility.Collapsed;
            if (GridOnbColor != null) GridOnbColor.Visibility = Visibility.Collapsed;
            if (GridOnbTheme != null) GridOnbTheme.Visibility = Visibility.Collapsed;
            if (GridOnbCredits != null) GridOnbCredits.Visibility = Visibility.Collapsed;
            if (OnbStepText != null) OnbStepText.Visibility = Visibility.Visible;

            if (step == 1) {
                if (OnbStepText != null) OnbStepText.Text = Application.Current?.Resources["TxtOnbLang"] as string ?? "Langue";
                if (GridOnbLang != null) GridOnbLang.Visibility = Visibility.Visible;
            } else if (step == 2) {
                if (OnbStepText != null) OnbStepText.Visibility = Visibility.Collapsed;
                if (GridOnbWelcome != null) GridOnbWelcome.Visibility = Visibility.Visible;
            } else if (step == 3) {
                if (OnbStepText != null) OnbStepText.Text = Application.Current?.Resources["TxtOnbScreen"] as string ?? "Choix de l'écran";
                if (GridOnbScreen != null) GridOnbScreen.Visibility = Visibility.Visible;
                RefreshOnbScreenList();
                RefreshViewModeButtons();
            } else if (step == 4) {
                if (OnbStepText != null) OnbStepText.Text = Application.Current?.Resources["TxtOnbCb"] as string ?? "Daltonisme";
                if (GridOnbCb != null) GridOnbCb.Visibility = Visibility.Visible;
            } else if (step == 5) {
                if (OnbStepText != null) OnbStepText.Text = Application.Current?.Resources["TxtOnbColor"] as string ?? "Couleur du Thème";
                if (GridOnbColor != null) GridOnbColor.Visibility = Visibility.Visible;
            } else if (step == 6) {
                if (OnbStepText != null) OnbStepText.Text = Application.Current?.Resources["TxtOnbIcons"] as string ?? "Style d'icônes";
                if (GridOnbTheme != null) GridOnbTheme.Visibility = Visibility.Visible;
            } else if (step == 7) {
                if (OnbStepText != null) OnbStepText.Visibility = Visibility.Collapsed;
                if (GridOnbCredits != null) GridOnbCredits.Visibility = Visibility.Visible;
            }
        }

        private void BtnCloseOnboarding_Click(object sender, RoutedEventArgs e)
        {
            OnboardingOverlay.Visibility = Visibility.Collapsed;
            _canPersistGridConfig = true;

            if (!_hasCompletedMandatoryNoticeFlow)
            {
                StartMandatoryNoticeFlow();
                Log("Onboarding fermé via la croix, ouverture de la Notice guidée.");
                return;
            }

            RestoreControlPanelAfterOnboarding(saveAfterFit: true);
            Log("Onboarding fermé via la croix.");
        }

        private void BtnOnbNext_Click(object sender, RoutedEventArgs e)
        {
            if (_onboardingStep < 7) {
                ShowOnboardingStep(_onboardingStep + 1);
            } else {
                OnboardingOverlay.Visibility = Visibility.Collapsed;
                _canPersistGridConfig = true;
                RestoreControlPanelAfterOnboarding(saveAfterFit: true);
                StartMandatoryNoticeFlow();
                Log("Onboarding terminé, ouverture de la Notice guidée.");
            }
        }

        private void BtnSuccessContinue_Click(object sender, RoutedEventArgs e)
        {
            SuccessOverlay.Visibility = Visibility.Collapsed;
            RestoreControlPanelAfterOnboarding(saveAfterFit: true);

            if (!_hasCompletedMandatoryNoticeFlow)
            {
                StartMandatoryNoticeFlow();
                return;
            }
        }

        private void RestoreControlPanelAfterOnboarding(bool saveAfterFit)
        {
            if (ControlPanel == null) return;

            ControlPanel.Visibility = Visibility.Visible;
            ApplyViewMode();
            QueueAutoFitControlPanelHeight(saveAfterFit);
        }

        private void StartMandatoryNoticeFlow()
        {
            _isMandatoryNoticeFlowActive = true;
            _mandatoryNoticeStep = 1;
            SuccessOverlay.Visibility = Visibility.Collapsed;
            RestoreWindowForInteractiveFlow();
            RestoreControlPanelAfterOnboarding(saveAfterFit: false);
            ShowMandatoryNoticeStep(_mandatoryNoticeStep);
        }

        private void ShowMandatoryNoticeStep(int step)
        {
            _mandatoryNoticeStep = Math.Clamp(step, 1, NoticeWizardStepCount);
            ShowNoticePage(_mandatoryNoticeStep, mandatoryFlow: true);
            UpdateMandatoryNoticeWizardUi();
        }

        private void UpdateMandatoryNoticeWizardUi()
        {
            if (TxtNoticeWizardProgress != null)
            {
                TxtNoticeWizardProgress.Text = $"{_mandatoryNoticeStep} / {NoticeWizardStepCount}";
            }

            bool isFinalStep = _mandatoryNoticeStep >= NoticeWizardStepCount;
            if (BtnNoticeWizardNext != null)
            {
                BtnNoticeWizardNext.Content = isFinalStep
                    ? (Application.Current?.Resources["TxtFinish"] as string ?? "Terminer")
                    : (Application.Current?.Resources["TxtNext"] as string ?? "Suivant ➔");
                BtnNoticeWizardNext.IsEnabled = true;
                BtnNoticeWizardNext.Opacity = 1.0;
            }
        }

        private void CompleteMandatoryNoticeFlow()
        {
            _isMandatoryNoticeFlowActive = false;
            _hasCompletedMandatoryNoticeFlow = true;
            _mandatoryNoticeStep = 1;

            if (NoticeWizardFooter != null) NoticeWizardFooter.Visibility = Visibility.Collapsed;
            if (BtnNoticeBackToApp != null) BtnNoticeBackToApp.Visibility = Visibility.Visible;
            if (BtnNoticeSummaryBack != null) BtnNoticeSummaryBack.Visibility = Visibility.Visible;
            if (NoticePageContainer != null) NoticePageContainer.Visibility = Visibility.Collapsed;
            if (NoticeSummary != null) NoticeSummary.Visibility = Visibility.Visible;

            NoticeContent.Visibility = Visibility.Collapsed;
            SettingsContent.Visibility = Visibility.Collapsed;
            PanelContent.Visibility = Visibility.Visible;

            QueueAutoFitControlPanelHeight(saveAfterFit: true);
            SaveGridConfig();
            Log("Notice obligatoire terminée.");
        }

        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsLoaded && ControlPanel != null)
            {
                ControlPanel.Opacity = Math.Clamp(e.NewValue, MinPanelOpacity, MaxPanelOpacity);
            }
        }

        private void InitUIStates()
        {
            ApplyLanguage();
            ApplyIconTheme();
            ApplyThemeColor();
            ApplyLargeText();
            UpdateLegendColors();
            ApplyViewMode();

            Button[] themeBtns = { BtnTheme0, BtnTheme1, BtnTheme2, BtnOnbTheme0, BtnOnbTheme1, BtnOnbTheme2 };
            for(int i = 0; i < themeBtns.Length; i++) {
                if (themeBtns[i] != null) themeBtns[i].Opacity = ((i % 3) == _iconIdx) ? 1.0 : 0.4;
            }

            // Initialisation visuelle des raccourcis si c'est le premier lancement
            if (TxtKeyStart != null && string.IsNullOrEmpty(TxtKeyStart.Text)) TxtKeyStart.Text = KeyInterop.KeyFromVirtualKey((int)_vkStart).ToString();
            if (TxtKeyToggle != null && string.IsNullOrEmpty(TxtKeyToggle.Text)) TxtKeyToggle.Text = KeyInterop.KeyFromVirtualKey((int)_vkToggle).ToString();
            if (TxtKeyClear != null && string.IsNullOrEmpty(TxtKeyClear.Text)) TxtKeyClear.Text = KeyInterop.KeyFromVirtualKey((int)_vkClear).ToString();
        }

        private void LoadIcons()
        {
            void TryLoadImage(Image img, params string[] possibleNames)
            {
                if (img == null) return;
                foreach (string name in possibleNames)
                {
                    string[] prefixes = {
                        "pack://application:,,,/Ressources/Icones/",
                        "pack://application:,,,/Ressources/",
                        "pack://application:,,,/Icones/",
                        "pack://application:,,,/"
                    };
                    foreach (string prefix in prefixes)
                    {
                        string uri = prefix + name + (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "" : ".png");
                        Log($"[LoadIcons] Tentative de chargement : {uri}");
                        try
                        {
                            BitmapImage bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.UriSource = new Uri(uri, UriKind.Absolute);
                            bmp.CacheOption = BitmapCacheOption.OnLoad; // Prévient les crashs asynchrones WPF
                            bmp.EndInit();
                            img.Source = bmp;
                            Log($"[LoadIcons] SUCCÈS -> {uri}");
                            return; // L'image a été trouvée et chargée avec succès !
                        }
                        catch (Exception ex)
                        {
                            Log($"[LoadIcons] ÉCHEC ({uri}) : {ex.Message}");
                        }
                    }
                }
                Log($"[LoadIcons] CRITIQUE : Toutes les tentatives ont échoué pour {possibleNames[0]} !");
            }

            // Cherche toutes les combinaisons probables que tu aurais pu écrire !
            TryLoadImage(ImgLang0, "lang_fr", "fr", "france");
            TryLoadImage(ImgOnbLang0, "lang_fr", "fr", "france");
            TryLoadImage(ImgLang1, "gb.png", "gb", "lang_gb", "lang_en", "en", "england", "uk", "anglais");
            TryLoadImage(ImgOnbLang1, "gb.png", "gb", "lang_gb", "lang_en", "en", "england", "uk", "anglais");
            TryLoadImage(ImgLang2, "lang_pt", "pt", "portugal", "br");
            TryLoadImage(ImgOnbLang2, "lang_pt", "pt", "portugal", "br");
            TryLoadImage(ImgLang3, "lang_es", "es", "espana", "spain");
            TryLoadImage(ImgOnbLang3, "lang_es", "es", "espana", "spain");
            TryLoadImage(ImgLang4, "lang_it", "it", "italy", "italie");
            TryLoadImage(ImgOnbLang4, "lang_it", "it", "italy", "italie");
            TryLoadImage(ImgLang5, "lang_de", "de", "germany", "allemagne");
            TryLoadImage(ImgOnbLang5, "lang_de", "de", "germany", "allemagne");
            TryLoadImage(ImgLang6, "arabe", "lang_ar", "ar", "arabia", "arabic", "maroc", "algerie");
            TryLoadImage(ImgOnbLang6, "arabe", "lang_ar", "ar", "arabia", "arabic", "maroc", "algerie");
            TryLoadImage(ImgLang7, "nl", "lang_nl", "pays_bas", "netherlands", "dutch");
            TryLoadImage(ImgOnbLang7, "nl", "lang_nl", "pays_bas", "netherlands", "dutch");
            TryLoadImage(ImgLang8, "pl", "lang_pl", "pologne", "poland", "polish");
            TryLoadImage(ImgOnbLang8, "pl", "lang_pl", "pologne", "poland", "polish");
            TryLoadImage(ImgLang9, "ru", "lang_ru", "russie", "russia", "russian");
            TryLoadImage(ImgOnbLang9, "ru", "lang_ru", "russie", "russia", "russian");
            TryLoadImage(ImgLang10, "su", "sv", "lang_sv", "suede", "sweden", "swedish");
            TryLoadImage(ImgOnbLang10, "su", "sv", "lang_sv", "suede", "sweden", "swedish");
            TryLoadImage(ImgLang11, "turc", "tr", "lang_tr", "turquie", "turkey", "turkish");
            TryLoadImage(ImgOnbLang11, "turc", "tr", "lang_tr", "turquie", "turkey", "turkish");

            TryLoadImage(ImgCb0, "cb_normal", "normal", "aucun");
            TryLoadImage(ImgOnbCb0, "cb_normal", "normal", "aucun");
            TryLoadImage(ImgCb1, "cb_protanopie", "Protanopie", "protanopie", "rouge");
            TryLoadImage(ImgOnbCb1, "cb_protanopie", "Protanopie", "protanopie", "rouge");
            TryLoadImage(ImgCb2, "cb_deuteranopie", "Deutéranopie", "Deuteranopie", "deuteranopie", "vert");
            TryLoadImage(ImgOnbCb2, "cb_deuteranopie", "Deutéranopie", "Deuteranopie", "deuteranopie", "vert");
            TryLoadImage(ImgCb3, "cb_tritanopie", "Tritanopie", "tritanopie", "bleu");
            TryLoadImage(ImgOnbCb3, "cb_tritanopie", "Tritanopie", "tritanopie", "bleu");

            TryLoadImage(ImgTheme0, "Apparences/Coeur_1/Seuil_1", "Seuil_1");
            TryLoadImage(ImgOnbTheme0, "Apparences/Coeur_1/Seuil_1", "Seuil_1");
            TryLoadImage(ImgTheme1, "Apparences/Harebourg_1/Seuil_1", "Harebourg_1/Seuil_1");
            TryLoadImage(ImgOnbTheme1, "Apparences/Harebourg_1/Seuil_1", "Harebourg_1/Seuil_1");
            TryLoadImage(ImgTheme2, "Apparences/Harebourg_2/Seuil_1", "Harebourg_2/Seuil_1");
            TryLoadImage(ImgOnbTheme2, "Apparences/Harebourg_2/Seuil_1", "Harebourg_2/Seuil_1");

            TryLoadImage(ImgCreator, "Icones/Credits.png", "Credits", "Credits.png");
        }

        private void BtnColorblind_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int cbIdx))
            {
                _colorblindMode = cbIdx;
                ApplyThemeColor(); // Mets à jour les bordures et textes visuels des boutons

                UpdateLegendColors();

                RefreshOverlay();
            }
        }

        private void BtnLanguage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int langIdx))
            {
                _langIdx = langIdx;
                ApplyThemeColor(); // Appelle intelligemment la couleur de thème
                ApplyLanguage();
                Log($"Langue changée : {_langIdx}");
            }
        }

        private void RefreshDynamicTexts()
        {
            if (_currentState == AppState.Idle) SetStatus(Application.Current?.Resources["StatusReady"] as string ?? "Prêt. En attente...", Brushes.Gray);
            else if (_currentState == AppState.WaitingForPlayer) SetStatus(Application.Current?.Resources["StatusStep1"] as string ?? "Étape 1 : Cliquez sur VOTRE personnage.", Brushes.DeepSkyBlue);
            else if (_currentState == AppState.WaitingForMonster) SetStatus(Application.Current?.Resources["StatusStep2"] as string ?? "Étape 2 : Cliquez sur la CIBLE.", Brushes.Crimson);

            ApplyLargeText(); // Force la traduction instantanée du bouton "Texte Agrandi"

            if (OnboardingOverlay != null && OnboardingOverlay.Visibility == Visibility.Visible) ShowOnboardingStep(_onboardingStep);

            RefreshOverlay();
        }

        private void ApplyLanguage()
        {
            string lang = "fr";
            switch (_langIdx)
            {
                case 0: lang = "fr"; break;
                case 1: lang = "en"; break;
                case 2: lang = "pt"; break;
                case 3: lang = "es"; break;
                case 4: lang = "it"; break;
                case 5: lang = "de"; break;
                case 6: lang = "ar"; break;
                case 7: lang = "nl"; break;
                case 8: lang = "pl"; break;
                case 9: lang = "ru"; break;
                case 10: lang = "sv"; break;
                case 11: lang = "tr"; break;
            }

            try
            {
                ResourceDictionary dict = new ResourceDictionary();
                dict.Source = new Uri($"pack://application:,,,/Langues/{lang}.xaml", UriKind.Absolute);

                var oldDict = Application.Current?.Resources.MergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains(".xaml"));
                if (oldDict != null)
                    Application.Current?.Resources.MergedDictionaries.Remove(oldDict);

                Application.Current?.Resources.MergedDictionaries.Add(dict);

                RefreshDynamicTexts();
            }
            catch { /* Fichier de langue manquant */ }
        }

        private void BtnIconTheme_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int iconIdx))
            {
                _iconIdx = iconIdx;
                Button[] btns = { BtnTheme0, BtnTheme1, BtnTheme2, BtnOnbTheme0, BtnOnbTheme1, BtnOnbTheme2 };
                foreach (var b in btns) {
                    if (b != null) {
                        b.Opacity = (int.Parse(b.Tag?.ToString() ?? "0") == _iconIdx) ? 1.0 : 0.4;
                    }
                }
                ApplyIconTheme();
                Log($"Thème d'icônes changé : {_iconIdx}");
            }
        }

        private void ApplyIconTheme()
        {
            string themeFolder = "";
            switch (_iconIdx)
            {
                case 0: themeFolder = "Apparences/Coeur_1/"; break; // Thème par défaut
                case 1: themeFolder = "Apparences/Harebourg_1/"; break;
                case 2: themeFolder = "Apparences/Harebourg_2/"; break;
                default: themeFolder = "Apparences/Coeur_1/"; break;
            }

            void LoadTarget(Image img, string path) {
                if (img == null) return;
                string fullPath = $"pack://application:,,,/Ressources/{path}";
                Log($"[ApplyIconTheme] Tentative : {fullPath}");
                try {
                    BitmapImage bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad; // Protège contre le crash layout
                    bmp.EndInit();
                    img.Source = bmp;
                    Log($"[ApplyIconTheme] SUCCÈS -> {fullPath}");
                } catch (Exception ex) {
                    Log($"[ApplyIconTheme] ERREUR -> {fullPath} : {ex.Message}");
                    img.Source = null;
                }
            }

            LoadTarget(ImgSeuil1, $"{themeFolder}Seuil_1.png");
            LoadTarget(ImgSeuil2, $"{themeFolder}Seuil_2.png");
            LoadTarget(ImgSeuil3, $"{themeFolder}Seuil_3.png");
            LoadTarget(ImgSeuil4, $"{themeFolder}Seuil_4.png");
            LoadTarget(ImgSeuil5, $"{themeFolder}Seuil_5.png");

            LoadTarget(ImgSeuil1Compact, $"{themeFolder}Seuil_1.png");
            LoadTarget(ImgSeuil2Compact, $"{themeFolder}Seuil_2.png");
            LoadTarget(ImgSeuil3Compact, $"{themeFolder}Seuil_3.png");
            LoadTarget(ImgSeuil4Compact, $"{themeFolder}Seuil_4.png");
            LoadTarget(ImgSeuil5Compact, $"{themeFolder}Seuil_5.png");

            LoadTarget(ImgTargetMob, "Icones/mob.png");
            LoadTarget(ImgTargetBoss, "Icones/harebourg.png");
        }

        private void ApplyThemeColor()
        {
            // IMPORTANT : Met à jour la ressource locale de la fenêtre pour écraser le jaune par défaut !
            this.Resources["ThemeColorBrush"] = new SolidColorBrush(_themeColor);
            if (Application.Current != null) Application.Current.Resources["ThemeColorBrush"] = new SolidColorBrush(_themeColor);

            SolidColorBrush themeBrush = new SolidColorBrush(_themeColor);

            UpdateThresholdUI(); // Applique le contour thématique au seuil actuellement sélectionné

            if (_isBossTarget && BtnTargetBoss != null) { BtnTargetBoss.Foreground = themeBrush; BtnTargetBoss.BorderBrush = themeBrush; }
            else if (!_isBossTarget && BtnTargetMob != null) { BtnTargetMob.Foreground = themeBrush; BtnTargetMob.BorderBrush = themeBrush; }

            if (_isOddTurn && BtnTurnOdd != null) { BtnTurnOdd.Foreground = themeBrush; BtnTurnOdd.BorderBrush = themeBrush; }
            else if (!_isOddTurn && BtnTurnEven != null) { BtnTurnEven.Foreground = themeBrush; BtnTurnEven.BorderBrush = themeBrush; }

            RefreshViewModeButtons();

            // Mise à jour de l'apparence des boutons Daltonisme
            Button[] cbBtns = { BtnCb0, BtnCb1, BtnCb2, BtnCb3, BtnOnbCb0, BtnOnbCb1, BtnOnbCb2, BtnOnbCb3 };
            for(int i = 0; i < cbBtns.Length; i++) {
                if (cbBtns[i] != null) {
                    cbBtns[i].BorderBrush = (_colorblindMode == (i % 4)) ? themeBrush : new SolidColorBrush(Color.FromRgb(50, 53, 64));
                }
            }

            // Mise à jour de l'apparence des boutons Langue
            Button[] langBtns = { BtnLang0, BtnLang1, BtnLang2, BtnLang3, BtnLang4, BtnLang5, BtnLang6, BtnLang7, BtnLang8, BtnLang9, BtnLang10, BtnLang11, BtnOnbLang0, BtnOnbLang1, BtnOnbLang2, BtnOnbLang3, BtnOnbLang4, BtnOnbLang5, BtnOnbLang6, BtnOnbLang7, BtnOnbLang8, BtnOnbLang9, BtnOnbLang10, BtnOnbLang11 };
            for(int i = 0; i < langBtns.Length; i++) {
                if (langBtns[i] != null) {
                    langBtns[i].BorderBrush = (_langIdx == (i % 12)) ? themeBrush : new SolidColorBrush(Color.FromRgb(50, 53, 64));
                }
            }

            // Filtrage intelligent des couleurs (Eviter les couleurs pièges pour le daltonisme sélectionné)
            void FilterColorGrid(UniformGrid grid)
            {
                if (grid == null) return;
                foreach (Button btn in grid.Children.OfType<Button>())
                {
                    if (btn.Background is SolidColorBrush brush)
                    {
                        Color c = brush.Color;
                        bool isSafe = true;
                        if (_colorblindMode == 1 || _colorblindMode == 2) {
                            // Protanopie/Deutéranopie: On retire le rouge pur, le vert pur, le violet
                            if (c == Color.FromRgb(232, 17, 35) || c == Color.FromRgb(50, 205, 50) || c == Color.FromRgb(0, 163, 108) || c == Color.FromRgb(108, 14, 186)) isSafe = false;
                        }
                        else if (_colorblindMode == 3) {
                            // Tritanopie: On retire les bleus et jaunes purs
                            if (c == Color.FromRgb(77, 168, 218) || c == Color.FromRgb(255, 215, 0) || c == Color.FromRgb(0, 206, 209)) isSafe = false;
                        }

                        btn.Visibility = isSafe ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
            FilterColorGrid(ThemeColorGrid!);
            FilterColorGrid(GridOnbColor!);
            RefreshScreenList();
            RefreshOnbScreenList();
        }

        private List<DisplayScreen> GetScreens()
        {
            List<DisplayScreen> screens = new List<DisplayScreen>();
            MonitorEnumDelegate callback = (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                MONITORINFOEX mi = new MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    string name = Application.Current?.Resources["TxtScreen"] as string ?? "Écran";
                    string primary = Application.Current?.Resources["TxtPrimary"] as string ?? "Principal";
                    bool isPrim = (mi.dwFlags & 1) != 0;
                    screens.Add(new DisplayScreen
                    {
                        DeviceName = mi.szDevice,
                        DisplayName = $"{name} {screens.Count + 1}" + (isPrim ? $" ({primary})" : ""),
                        Bounds = GetMonitorBoundsInDip(mi.rcMonitor),
                        IsPrimary = isPrim
                    });
                }
                return true;
            };
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            return screens;
        }

        private Rect GetMonitorBoundsInDip(RECT monitorRect)
        {
            Matrix transformFromDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            Point topLeft = transformFromDevice.Transform(new Point(monitorRect.left, monitorRect.top));
            Point bottomRight = transformFromDevice.Transform(new Point(monitorRect.right, monitorRect.bottom));
            return new Rect(topLeft, bottomRight);
        }

        private string GetWindowTextSafe(IntPtr hwnd)
        {
            int length = GetWindowTextLength(hwnd);
            if (length <= 0) return string.Empty;

            StringBuilder builder = new StringBuilder(length + 1);
            return GetWindowText(hwnd, builder, builder.Capacity) > 0
                ? builder.ToString()
                : string.Empty;
        }

        private string GetClassNameSafe(IntPtr hwnd)
        {
            StringBuilder builder = new StringBuilder(256);
            return GetClassName(hwnd, builder, builder.Capacity) > 0
                ? builder.ToString()
                : string.Empty;
        }

        private static string GetProcessNameSafe(IntPtr hwnd)
        {
            try
            {
                _ = GetWindowThreadProcessId(hwnd, out uint processId);
                if (processId == 0)
                {
                    return string.Empty;
                }

                using Process process = Process.GetProcessById((int)processId);
                return process.ProcessName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool TryGetWindowClientBoundsInDip(IntPtr hwnd, out Rect clientBounds)
        {
            clientBounds = Rect.Empty;

            if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || IsIconic(hwnd) || !GetClientRect(hwnd, out RECT rect))
            {
                return false;
            }

            int widthPx = rect.right - rect.left;
            int heightPx = rect.bottom - rect.top;
            if (widthPx < MinTrackedGameClientWidth || heightPx < MinTrackedGameClientHeight)
            {
                return false;
            }

            POINT clientOrigin = new POINT { X = 0, Y = 0 };
            if (!ClientToScreen(hwnd, ref clientOrigin))
            {
                return false;
            }

            Matrix transformFromDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            Point topLeft = transformFromDevice.Transform(new Point(clientOrigin.X, clientOrigin.Y));
            Point bottomRight = transformFromDevice.Transform(new Point(clientOrigin.X + widthPx, clientOrigin.Y + heightPx));
            clientBounds = new Rect(topLeft, bottomRight);
            return clientBounds.Width > 0 && clientBounds.Height > 0;
        }

        private DisplayScreen? GetSelectedOrPrimaryScreen()
        {
            List<DisplayScreen> screens = GetScreens();
            return screens.FirstOrDefault(screen => screen.DeviceName == _selectedScreenDeviceName)
                ?? screens.FirstOrDefault(screen => screen.IsPrimary)
                ?? screens.FirstOrDefault();
        }

        private static bool IsPotentialDofusWindow(string title, string className, string processName)
        {
            bool processLooksLikeDofus = processName.IndexOf("dofus", StringComparison.OrdinalIgnoreCase) >= 0;
            bool titleLooksLikeDofus = title.IndexOf("dofus", StringComparison.OrdinalIgnoreCase) >= 0;
            bool unityWindow = className.IndexOf("unity", StringComparison.OrdinalIgnoreCase) >= 0;

            return processLooksLikeDofus || (titleLooksLikeDofus && unityWindow);
        }

        private static double ScoreGameWindowCandidate(GameWindowCandidate candidate, DisplayScreen? preferredScreen)
        {
            double score = 0;

            if (candidate.IsForeground) score += 1000;
            if (candidate.ProcessName.IndexOf("dofus", StringComparison.OrdinalIgnoreCase) >= 0) score += 400;
            if (candidate.Title.IndexOf("dofus", StringComparison.OrdinalIgnoreCase) >= 0) score += 200;
            if (candidate.ClassName.IndexOf("unity", StringComparison.OrdinalIgnoreCase) >= 0) score += 100;

            if (preferredScreen != null)
            {
                Point center = new Point(
                    candidate.ClientBounds.Left + (candidate.ClientBounds.Width / 2),
                    candidate.ClientBounds.Top + (candidate.ClientBounds.Height / 2));
                score += preferredScreen.Bounds.Contains(center) ? 500 : -250;
            }

            score += Math.Min((candidate.ClientBounds.Width * candidate.ClientBounds.Height) / 10000.0, 100);
            return score;
        }

        private GameWindowCandidate? FindBestGameWindowCandidate()
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            DisplayScreen? preferredScreen = GetSelectedOrPrimaryScreen();
            List<GameWindowCandidate> candidates = new List<GameWindowCandidate>();

            EnumWindows((hwnd, _) =>
            {
                if (hwnd == IntPtr.Zero || hwnd == _windowHandle || !IsWindowVisible(hwnd) || IsIconic(hwnd))
                {
                    return true;
                }

                string title = GetWindowTextSafe(hwnd);
                string className = GetClassNameSafe(hwnd);
                string processName = GetProcessNameSafe(hwnd);
                if (!IsPotentialDofusWindow(title, className, processName))
                {
                    return true;
                }

                if (!TryGetWindowClientBoundsInDip(hwnd, out Rect clientBounds))
                {
                    return true;
                }

                candidates.Add(new GameWindowCandidate
                {
                    Handle = hwnd,
                    ClientBounds = clientBounds,
                    IsForeground = hwnd == foregroundWindow,
                    Title = title,
                    ClassName = className,
                    ProcessName = processName
                });

                return true;
            }, IntPtr.Zero);

            return candidates
                .OrderByDescending(candidate => ScoreGameWindowCandidate(candidate, preferredScreen))
                .FirstOrDefault();
        }

        private void InvalidateTrackedGameWindowAnchor(bool keepCurrentBounds = true, bool allowAutoHide = true)
        {
            _trackedGameWindowHandle = IntPtr.Zero;
            _gameWindowTickCounter = GameWindowRescanTickInterval;
            if (!keepCurrentBounds)
            {
                _trackedGameClientBounds = Rect.Empty;
            }

            if (allowAutoHide && _isOverlayEnabled && !_isAutoHiddenBecauseGameWindowUnavailable)
            {
                _isAutoHiddenBecauseGameWindowUnavailable = true;
                _shouldRestoreWindowAfterGameWindowReturns = Visibility == Visibility.Visible;
                _trackedWindowInteractionSuppressionUntilUtc = DateTime.UtcNow.AddMilliseconds(250);

                RunWithoutLayoutPersistence(() =>
                {
                    if (Visibility != Visibility.Visible)
                    {
                        Visibility = Visibility.Visible;
                    }

                    Topmost = false;
                    Opacity = 0.0;
                    SetWindowClickThrough(true);

                    double hiddenWidth = Math.Max(GetLayoutWidth(), GetWindowMinWidthValue());
                    double hiddenHeight = Math.Max(GetLayoutHeight(), GetWindowMinHeightValue());
                    Left = SystemParameters.VirtualScreenLeft - hiddenWidth - 200;
                    Top = SystemParameters.VirtualScreenTop - hiddenHeight - 200;
                });

                Log("Application masquée automatiquement : fenêtre Dofus indisponible ou réduite.");
            }
        }

        private bool ShouldTrackGameWindowAnchor()
        {
            if (_windowHandle == IntPtr.Zero || _isApplyingTrackedWindowBounds || _isApplyingInitialLayout)
            {
                return false;
            }

            if (_isMandatoryNoticeFlowActive ||
                (OnboardingOverlay != null && OnboardingOverlay.Visibility == Visibility.Visible) ||
                (SuccessOverlay != null && SuccessOverlay.Visibility == Visibility.Visible))
            {
                return false;
            }

            return true;
        }

        private void UpdateTrackedGameWindowAnchor()
        {
            if (!ShouldTrackGameWindowAnchor())
            {
                return;
            }

            bool shouldRescan = _trackedGameWindowHandle == IntPtr.Zero ||
                                _gameWindowTickCounter >= GameWindowRescanTickInterval ||
                                !IsWindow(_trackedGameWindowHandle) ||
                                IsIconic(_trackedGameWindowHandle);

            if (shouldRescan)
            {
                GameWindowCandidate? candidate = FindBestGameWindowCandidate();
                _gameWindowTickCounter = 0;

                if (candidate == null)
                {
                    InvalidateTrackedGameWindowAnchor();
                    return;
                }

                _trackedGameWindowHandle = candidate.Handle;
                ApplyTrackedGameWindowBounds(candidate.ClientBounds);
                return;
            }

            _gameWindowTickCounter++;

            if (!TryGetWindowClientBoundsInDip(_trackedGameWindowHandle, out Rect clientBounds))
            {
                InvalidateTrackedGameWindowAnchor();
                return;
            }

            ApplyTrackedGameWindowBounds(clientBounds);
        }

        private void ApplyTrackedGameWindowBounds(Rect clientBounds)
        {
            if (clientBounds.Width <= 0 || clientBounds.Height <= 0)
            {
                return;
            }

            bool shouldRestoreAutoHiddenWindow =
                _isAutoHiddenBecauseGameWindowUnavailable &&
                _isOverlayEnabled &&
                _shouldRestoreWindowAfterGameWindowReturns;
            _isAutoHiddenBecauseGameWindowUnavailable = false;
            if (shouldRestoreAutoHiddenWindow)
            {
                if (Visibility != Visibility.Visible)
                {
                    Visibility = Visibility.Visible;
                }

                Opacity = 1.0;
                Topmost = true;
                _trackedWindowInteractionSuppressionUntilUtc = DateTime.UtcNow.AddMilliseconds(250);
                _shouldRestoreWindowAfterGameWindowReturns = false;
                Log("Application restaurée après retour de la fenêtre Dofus.");
            }

            Rect previousTrackedBounds = _trackedGameClientBounds;
            _trackedGameClientBounds = clientBounds;

            bool anchorChanged = previousTrackedBounds.IsEmpty || !BoundsCloseEnough(previousTrackedBounds, clientBounds);
            Rect currentWindowBounds = new Rect(Left, Top, GetLayoutWidth(), GetLayoutHeight());
            bool windowBoundsChanged = !BoundsCloseEnough(currentWindowBounds, clientBounds);
            bool shouldAttemptWindowedReferenceRepair = _pendingWindowedV9ReferenceRepair;

            if (!anchorChanged && !windowBoundsChanged && !shouldAttemptWindowedReferenceRepair)
            {
                return;
            }

            _isApplyingTrackedWindowBounds = true;
            try
            {
                if (windowBoundsChanged)
                {
                    _trackedWindowInteractionSuppressionUntilUtc = DateTime.UtcNow.AddMilliseconds(180);
                }

                RunWithoutLayoutPersistence(() =>
                {
                    if (windowBoundsChanged)
                    {
                        Left = clientBounds.Left;
                        Top = clientBounds.Top;
                        Width = clientBounds.Width;
                        Height = clientBounds.Height;
                        UpdateLayout();
                    }

                    SyncControlPanelWidthToWindow();
                    ClampControlPanelToWindow();
                });

                bool repairedWindowedReference = RepairWindowedV9ReferenceIfNeeded(new Size(clientBounds.Width, clientBounds.Height));
                if (repairedWindowedReference)
                {
                    SaveGridConfig(rememberBounds: false);
                }

                if (anchorChanged || repairedWindowedReference)
                {
                    ApplySelectedDisplayModeCalibration();
                    RefreshOverlay();
                }

            }
            finally
            {
                _isApplyingTrackedWindowBounds = false;
            }
        }

        private double GetLayoutWidth()
        {
            if (ActualWidth > 0) return ActualWidth;
            if (!double.IsNaN(Width) && Width > 0) return Width;
            return SystemParameters.PrimaryScreenWidth;
        }

        private double GetLayoutHeight()
        {
            if (ActualHeight > 0) return ActualHeight;
            if (!double.IsNaN(Height) && Height > 0) return Height;
            return SystemParameters.PrimaryScreenHeight;
        }

        private static double ClampFinite(double value, double min, double max)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return min;
            if (max < min) max = min;
            return Math.Min(Math.Max(value, min), max);
        }

        private static bool IsFiniteNumber(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsPositiveFinite(double value)
        {
            return IsFiniteNumber(value) && value > 0;
        }

        private static bool IsUsablePanelHeight(double value)
        {
            return IsPositiveFinite(value) && value > CollapsedPanelHeightThreshold;
        }

        private double GetCurrentControlPanelHeight()
        {
            if (ControlPanel == null) return CompactPanelDefaultHeight;
            if (!double.IsNaN(ControlPanel.Height) && ControlPanel.Height > 0) return ControlPanel.Height;
            if (ControlPanel.ActualHeight > 0) return ControlPanel.ActualHeight;
            return CompactPanelDefaultHeight;
        }

        private double GetWindowMinWidthValue()
        {
            return Math.Max(120, ControlPanelMinWidthValue + ControlPanelScreenPadding);
        }

        private double GetWindowMinHeightValue()
        {
            return Math.Max(80, ControlPanelMinHeightValue + ControlPanelScreenPadding);
        }

        private double GetTightWindowWidth(double panelX, double panelWidth)
        {
            double safePanelX = IsFiniteNumber(panelX) ? Math.Max(0, panelX) : PanelDefaultX;
            double safePanelWidth = IsPositiveFinite(panelWidth) ? panelWidth : CompactPanelDefaultWidth;
            return ClampFinite(
                safePanelX + safePanelWidth + ControlPanelScreenPadding,
                GetWindowMinWidthValue(),
                SystemParameters.VirtualScreenWidth);
        }

        private double GetSavedPanelWindowWidth()
        {
            return GetTightWindowWidth(_panelX, _panelWidth);
        }

        private double GetCurrentPanelWidth()
        {
            if (ControlPanel == null) return CompactPanelDefaultWidth;
            if (!double.IsNaN(ControlPanel.Width) && ControlPanel.Width > 0) return ControlPanel.Width;
            if (ControlPanel.ActualWidth > 0) return ControlPanel.ActualWidth;
            return CompactPanelDefaultWidth;
        }

        private void FitWindowWidthToControlPanel()
        {
            // DÉSACTIVÉ : On rend la fenêtre de l'overlay et le menu 100% indépendants
        }

        private void SyncControlPanelWidthToWindow()
        {
            if (ControlPanel == null || _isApplyingInitialLayout || _isClampingControlPanel) return;

            bool suppressHeightReflow = _isApplyingTrackedWindowBounds;
            bool panelIsCollapsed =
                PanelContent != null && SettingsContent != null && NoticeContent != null &&
                PanelContent.Visibility != Visibility.Visible &&
                SettingsContent.Visibility != Visibility.Visible &&
                NoticeContent.Visibility != Visibility.Visible;

            double currentPanelWidth = GetCurrentPanelWidth();
            double currentPanelHeight = GetCurrentControlPanelHeight();
            double targetWidth = IsPositiveFinite(_panelWidth)
                ? ClampFinite(_panelWidth, ControlPanelMinWidthValue, GetControlPanelMaxWidth())
                : currentPanelWidth;

            bool shouldRestoreLeft = IsFiniteNumber(_panelX) && Math.Abs(ControlPanel.Margin.Left - _panelX) > AnchorBoundsToleranceDip;
            bool shouldRestoreTop = IsFiniteNumber(_panelY) && Math.Abs(ControlPanel.Margin.Top - _panelY) > AnchorBoundsToleranceDip;
            bool shouldRestoreWidth = IsPositiveFinite(targetWidth) && Math.Abs(currentPanelWidth - targetWidth) > AnchorBoundsToleranceDip;
            bool shouldReAutoFitHeight = !suppressHeightReflow &&
                !panelIsCollapsed &&
                IsUsablePanelHeight(_panelHeight) &&
                currentPanelHeight + AnchorBoundsToleranceDip < Math.Min(_panelHeight, GetControlPanelMaxHeight());

            if (!shouldRestoreLeft && !shouldRestoreTop && !shouldRestoreWidth && !shouldReAutoFitHeight)
            {
                return;
            }

            RunWithoutLayoutPersistence(() =>
            {
                if (shouldRestoreLeft || shouldRestoreTop)
                {
                    ControlPanel.Margin = new Thickness(
                        Math.Max(0, IsFiniteNumber(_panelX) ? _panelX : ControlPanel.Margin.Left),
                        Math.Max(0, IsFiniteNumber(_panelY) ? _panelY : ControlPanel.Margin.Top),
                        0,
                        0);
                }

                if (shouldRestoreWidth)
                {
                    ControlPanel.Width = targetWidth;
                }

                if (panelIsCollapsed)
                {
                    if (!double.IsNaN(ControlPanel.Height))
                    {
                        ControlPanel.Height = double.NaN;
                    }
                }
                else if (IsEffectiveCompactMode())
                {
                    if (IsUsablePanelHeight(_panelHeight))
                    {
                        double targetHeight = ClampFinite(_panelHeight, ControlPanelMinHeightValue, GetControlPanelMaxHeight());
                        if (double.IsNaN(ControlPanel.Height) || Math.Abs(ControlPanel.Height - targetHeight) > AnchorBoundsToleranceDip)
                        {
                            ControlPanel.Height = targetHeight;
                        }
                    }
                }
                else if (!suppressHeightReflow && !double.IsNaN(ControlPanel.Height))
                {
                    ControlPanel.Height = double.NaN;
                }

                ClampControlPanelToWindow();
            });

            if (!panelIsCollapsed && shouldReAutoFitHeight)
            {
                QueueAutoFitControlPanelHeight();
            }
        }

        private double GetControlPanelMaxWidth()
        {
            double layoutWidth = GetLayoutWidth();
            double left = ClampFinite(ControlPanel.Margin.Left, 0, Math.Max(0, layoutWidth - ControlPanelMinWidthValue - ControlPanelScreenPadding));
            double available = layoutWidth - left - ControlPanelScreenPadding;
            return Math.Max(ControlPanelMinWidthValue, Math.Min(Math.Max(ControlPanelMaxWidthValue, _panelWidth), available));
        }

        private double GetControlPanelMaxHeight()
        {
            double layoutHeight = GetLayoutHeight();
            double top = ClampFinite(ControlPanel.Margin.Top, 0, Math.Max(0, layoutHeight - ControlPanelMinHeightValue - ControlPanelScreenPadding));
            double available = layoutHeight - top - ControlPanelScreenPadding;
            return Math.Max(ControlPanelMinHeightValue, available);
        }

        private void ApplySavedControlPanelBounds()
        {
            if (ControlPanel == null) return;

            ControlPanel.Margin = new Thickness(
                Math.Max(0, _panelX),
                Math.Max(0, _panelY),
                0,
                0);
            ControlPanel.Width = ClampFinite(_panelWidth, ControlPanelMinWidthValue, GetControlPanelMaxWidth());
            ControlPanel.Height = double.IsNaN(_panelHeight)
                ? double.NaN
                : ClampFinite(_panelHeight, ControlPanelMinHeightValue, GetControlPanelMaxHeight());
        }

        private void ApplySavedWindowBounds()
        {
            double minWidth = GetWindowMinWidthValue();
            double minHeight = GetWindowMinHeightValue();

            Width = ClampFinite(_windowWidth, minWidth, SystemParameters.VirtualScreenWidth);
            Height = ClampFinite(_windowHeight, minHeight, SystemParameters.VirtualScreenHeight);
            Left = double.IsNaN(_windowX) || double.IsInfinity(_windowX) ? WindowDefaultX : _windowX;
            Top = double.IsNaN(_windowY) || double.IsInfinity(_windowY) ? WindowDefaultY : _windowY;
        }

        private void RememberWindowBounds()
        {
            if (WindowState != WindowState.Normal) return;

            _windowX = Left;
            _windowY = Top;
            _windowWidth = ActualWidth > 0 ? ActualWidth : Width;
            _windowHeight = ActualHeight > 0 ? ActualHeight : Height;
        }

        private void ClampWindowToScreen(DisplayScreen screen)
        {
            double width = ActualWidth > 0 ? ActualWidth : Width;
            double height = ActualHeight > 0 ? ActualHeight : Height;

            double minLeft = screen.Bounds.Left;
            double minTop = screen.Bounds.Top;
            double maxLeft = Math.Max(minLeft, screen.Bounds.Right - width);
            double maxTop = Math.Max(minTop, screen.Bounds.Bottom - height);

            Left = ClampFinite(Left, minLeft, maxLeft);
            Top = ClampFinite(Top, minTop, maxTop);
            RememberWindowBounds();
        }

        private void ApplyCompactWindowSize()
        {
            RunWithoutLayoutPersistence(() =>
            {
                double compactHeight = IsPositiveFinite(_panelHeight)
                    ? _panelHeight
                    : CompactPanelDefaultHeight;

                if (ControlPanel != null)
                {
                    ControlPanel.Height = compactHeight;
                }

                ClampControlPanelToWindow();
            });
        }

        private void FitWindowHeightToControlPanel()
        {
            // DÉSACTIVÉ : La fenêtre ne doit pas rétrécir verticalement quand on replie le menu
        }

        private void RememberControlPanelBounds()
        {
            if (ControlPanel == null) return;

            _panelX = ControlPanel.Margin.Left;
            _panelY = ControlPanel.Margin.Top;
            double currentPanelWidth = !double.IsNaN(ControlPanel.Width) && ControlPanel.Width > 0
                ? ControlPanel.Width
                : ControlPanel.ActualWidth;
            double currentPanelHeight = !double.IsNaN(ControlPanel.Height) && ControlPanel.Height > 0
                ? ControlPanel.Height
                : ControlPanel.ActualHeight;
            if (IsPositiveFinite(currentPanelWidth)) _panelWidth = currentPanelWidth;
            bool panelIsCollapsed =
                PanelContent != null && SettingsContent != null && NoticeContent != null &&
                PanelContent.Visibility != Visibility.Visible &&
                SettingsContent.Visibility != Visibility.Visible &&
                NoticeContent.Visibility != Visibility.Visible;
            if (panelIsCollapsed && IsUsablePanelHeight(_savedPanelHeight))
            {
                _panelHeight = _savedPanelHeight;
            }
            else if (!panelIsCollapsed && IsUsablePanelHeight(currentPanelHeight))
            {
                _panelHeight = currentPanelHeight;
            }
        }

        private void QueuePanelBoundsSave()
        {
            if (!IsLoaded || _isApplyingInitialLayout || _suspendLayoutPersistence || ControlPanel == null) return;

            CaptureCurrentLayoutBounds();
            if (_panelBoundsSaveTimer == null)
            {
                _panelBoundsSaveTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(250)
                };
                _panelBoundsSaveTimer.Tick += (_, _) => SavePanelBoundsNow();
            }

            _panelBoundsSaveTimer.Stop();
            _panelBoundsSaveTimer.Start();
        }

        private void SavePanelBoundsNow()
        {
            _panelBoundsSaveTimer?.Stop();
            if (_isApplyingInitialLayout || _suspendLayoutPersistence || ControlPanel == null) return;

            CaptureCurrentLayoutBounds();
            SaveGridConfig(rememberBounds: false);
        }

        private void SaveCurrentLayoutAsDefault()
        {
            _panelBoundsSaveTimer?.Stop();
            CaptureCurrentLayoutBounds();
            SaveGridConfig(rememberBounds: false);
            Log($"Layout sauvegardé via Ctrl+S : fenêtre {_windowX:0.##},{_windowY:0.##} {_windowWidth:0.##}x{_windowHeight:0.##} / panneau {_panelX:0.##},{_panelY:0.##} {_panelWidth:0.##}x{_panelHeight:0.##}");
        }

        private void CaptureCurrentLayoutBounds()
        {
            if (WindowState == WindowState.Normal)
            {
                if (IsFiniteNumber(Left)) _windowX = Left;
                if (IsFiniteNumber(Top)) _windowY = Top;
                double currentWindowWidth = ActualWidth > 0 ? ActualWidth : Width;
                double currentWindowHeight = ActualHeight > 0 ? ActualHeight : Height;
                if (IsPositiveFinite(currentWindowWidth)) _windowWidth = currentWindowWidth;
                if (IsPositiveFinite(currentWindowHeight)) _windowHeight = currentWindowHeight;
            }

            if (ControlPanel == null) return;

            if (IsFiniteNumber(ControlPanel.Margin.Left)) _panelX = ControlPanel.Margin.Left;
            if (IsFiniteNumber(ControlPanel.Margin.Top)) _panelY = ControlPanel.Margin.Top;
            double currentPanelWidth = !double.IsNaN(ControlPanel.Width) && ControlPanel.Width > 0
                ? ControlPanel.Width
                : ControlPanel.ActualWidth;
            double currentPanelHeight = !double.IsNaN(ControlPanel.Height) && ControlPanel.Height > 0
                ? ControlPanel.Height
                : ControlPanel.ActualHeight;
            if (IsPositiveFinite(currentPanelWidth)) _panelWidth = currentPanelWidth;

            bool panelIsCollapsed =
                PanelContent != null && SettingsContent != null && NoticeContent != null &&
                PanelContent.Visibility != Visibility.Visible &&
                SettingsContent.Visibility != Visibility.Visible &&
                NoticeContent.Visibility != Visibility.Visible;
            if (panelIsCollapsed && IsUsablePanelHeight(_savedPanelHeight))
            {
                _panelHeight = _savedPanelHeight;
            }
            else if (!panelIsCollapsed && IsUsablePanelHeight(currentPanelHeight))
            {
                _panelHeight = currentPanelHeight;
            }
        }

        private void ClampControlPanelToWindow()
        {
            if (ControlPanel == null || _isClampingControlPanel) return;

            _isClampingControlPanel = true;
            try
            {
                double maxWidth = GetControlPanelMaxWidth();
                double maxHeight = GetControlPanelMaxHeight();

                ControlPanel.MaxWidth = maxWidth;
                ControlPanel.MaxHeight = maxHeight;

                if (!double.IsNaN(ControlPanel.Width))
                {
                    ControlPanel.Width = ClampFinite(ControlPanel.Width, ControlPanelMinWidthValue, maxWidth);
                }

                if (!double.IsNaN(ControlPanel.Height))
                {
                    ControlPanel.Height = ClampFinite(ControlPanel.Height, ControlPanelMinHeightValue, maxHeight);
                }

                double panelWidth = ControlPanel.ActualWidth > 0
                    ? ControlPanel.ActualWidth
                    : (double.IsNaN(ControlPanel.Width) ? ControlPanelMinWidthValue : ControlPanel.Width);
                double panelHeight = ControlPanel.ActualHeight > 0
                    ? ControlPanel.ActualHeight
                    : (double.IsNaN(ControlPanel.Height) ? ControlPanelMinHeightValue : ControlPanel.Height);

                double maxLeft = Math.Max(0, GetLayoutWidth() - panelWidth - ControlPanelScreenPadding);
                double maxTop = Math.Max(0, GetLayoutHeight() - panelHeight - ControlPanelScreenPadding);
                double newLeft = ClampFinite(ControlPanel.Margin.Left, 0, maxLeft);
                double newTop = ClampFinite(ControlPanel.Margin.Top, 0, maxTop);

                if (!newLeft.Equals(ControlPanel.Margin.Left) || !newTop.Equals(ControlPanel.Margin.Top))
                {
                    ControlPanel.Margin = new Thickness(newLeft, newTop, 0, 0);
                }

                MinWidth = GetWindowMinWidthValue();
                MinHeight = GetWindowMinHeightValue();
            }
            finally
            {
                _isClampingControlPanel = false;
            }
        }

        private void MoveToScreen(DisplayScreen screen)
        {
            InvalidateTrackedGameWindowAnchor(keepCurrentBounds: false, allowAutoHide: false);

            RunWithoutLayoutPersistence(() =>
            {
                double width = ClampFinite(_windowWidth, GetWindowMinWidthValue(), screen.Bounds.Width);
                double height = ClampFinite(_windowHeight, GetWindowMinHeightValue(), screen.Bounds.Height);

                _selectedScreenDeviceName = screen.DeviceName;
                Left = screen.Bounds.Left + WindowDefaultX;
                Top = screen.Bounds.Top + WindowDefaultY;
                Width = width;
                Height = height;

                UpdateLayout();
                ClampWindowToScreen(screen);
                ApplySavedControlPanelBounds();
                ClampControlPanelToWindow();
                UpdateLayout();
                ClampControlPanelToWindow();
                RememberWindowBounds();
            });

            Dispatcher.BeginInvoke(new Action(() =>
            {
                ClampControlPanelToWindow();
                if (!_isAutoHiddenBecauseGameWindowUnavailable && Opacity > 0.01)
                {
                    Activate();
                }
            }), DispatcherPriority.Loaded);
        }

        private void RefreshScreenList()
        {
            if (ScreenListPanel == null) return;
            ScreenListPanel.Children.Clear();
            List<DisplayScreen> screens = GetScreens();
            foreach (var screen in screens)
            {
                Button btn = new Button();
                btn.Content = screen.DisplayName;
                btn.Tag = screen.DeviceName;
                btn.Style = (Style)FindResource("ScreenSelectionButtonStyle");
                btn.Width = OnboardingScreenSelectionButtonWidth;
                btn.Height = OnboardingScreenSelectionButtonHeight;
                btn.HorizontalAlignment = HorizontalAlignment.Center;
                btn.HorizontalContentAlignment = HorizontalAlignment.Center;
                btn.Padding = new Thickness(10, 5, 10, 5);
                btn.Margin = new Thickness(4, 0, 4, 6);
                if (screen.DeviceName == _selectedScreenDeviceName)
                {
                    btn.Foreground = new SolidColorBrush(_themeColor);
                    btn.BorderBrush = new SolidColorBrush(_themeColor);
                    btn.Background = new SolidColorBrush(Color.FromRgb(45, 48, 59));
                }
                btn.Click += BtnSelectScreen_Click;
                ScreenListPanel.Children.Add(btn);
            }
        }

        private void BtnSelectScreen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string deviceName)
            {
                var screens = GetScreens();
                var target = screens.FirstOrDefault(s => s.DeviceName == deviceName);
                if (target != null)
                {
                    MoveToScreen(target);
                    RefreshScreenList();
                    SaveGridConfig();
                }
            }
        }

        private void RefreshOnbScreenList()
        {
            if (OnbScreenListPanel == null) return;
            OnbScreenListPanel.Children.Clear();
            List<DisplayScreen> screens = GetScreens();
            foreach (var screen in screens)
            {
                Button btn = new Button();
                btn.Content = screen.DisplayName;
                btn.Tag = screen.DeviceName;
                btn.Style = (Style)FindResource("ScreenSelectionButtonStyle");
                btn.Width = ScreenSelectionButtonWidth;
                btn.Height = ScreenSelectionButtonHeight;
                btn.HorizontalAlignment = HorizontalAlignment.Center;
                btn.HorizontalContentAlignment = HorizontalAlignment.Center;
                btn.Padding = new Thickness(10, 4, 10, 4);
                btn.Margin = new Thickness(0, 0, 0, 6);

                if (screen.DeviceName == _selectedScreenDeviceName ||
                    (string.IsNullOrEmpty(_selectedScreenDeviceName) && screen.IsPrimary))
                {
                    btn.Foreground = new SolidColorBrush(_themeColor);
                    btn.BorderBrush = new SolidColorBrush(_themeColor);
                    btn.Background = new SolidColorBrush(Color.FromRgb(45, 48, 59));
                }
                btn.Click += BtnSelectOnbScreen_Click;
                OnbScreenListPanel.Children.Add(btn);
            }
        }

        private void BtnSelectOnbScreen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string deviceName)
            {
                var screens = GetScreens();
                var target = screens.FirstOrDefault(s => s.DeviceName == deviceName);
                if (target != null)
                {
                    MoveToScreen(target);
                    RefreshOnbScreenList();
                    RefreshScreenList();
                    SaveGridConfig();
                }
            }
        }

        private void BtnToggleLargeText_Click(object sender, RoutedEventArgs e)
        {
            _isLargeText = !_isLargeText;
            ApplyLargeText();
            SaveGridConfig();
        }

        private void ApplyLargeText()
        {
            double scale = _isLargeText ? 1.15 : 1.0;
            ScaleTransform st = new ScaleTransform(scale, scale);
            if (ControlPanel != null) ControlPanel.LayoutTransform = st;
            if (OnboardingOverlay != null) OnboardingOverlay.LayoutTransform = st;
            ClampControlPanelToWindow();

            if (BtnToggleLargeText != null)
            {
                BtnToggleLargeText.Content = Application.Current?.Resources["TxtLargeText"] as string ?? "Texte Agrandi";
                BtnToggleLargeText.Foreground = new SolidColorBrush(_isLargeText ? _themeColor : Color.FromRgb(229, 231, 235));
            }
        }

        private void BtnViewMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                bool compactMode = btn.Tag.ToString() == "Compact";
                _isCompactMode = compactMode;
                _isResponsiveCompactMode = false;

                if (ControlPanel != null)
                {
                    double currentWidth = !double.IsNaN(ControlPanel.Width) && ControlPanel.Width > 0
                        ? ControlPanel.Width
                        : ControlPanel.ActualWidth;
                    double targetWidth = currentWidth > 0
                        ? currentWidth
                        : CompactPanelDefaultWidth;
                    double compactHeight = IsUsablePanelHeight(_panelHeight)
                        ? _panelHeight
                        : CompactPanelDefaultHeight;

                    ControlPanel.Width = ClampFinite(targetWidth, ControlPanelMinWidthValue, GetControlPanelMaxWidth());
                    ControlPanel.Height = compactMode ? compactHeight : double.NaN;
                }

                ApplyViewMode();
                if (compactMode)
                {
                    ApplyCompactWindowSize();
                }
                QueueAutoFitControlPanelHeight(saveAfterFit: true);
                SaveGridConfig();
            }
        }

        private bool IsEffectiveCompactMode()
        {
            return _isCompactMode || _isResponsiveCompactMode;
        }

        private void UpdateResponsiveCompactMode()
        {
            if (ControlPanel == null || _isApplyingViewMode) return;

            bool nextResponsiveCompact = false;

            if (nextResponsiveCompact == _isResponsiveCompactMode) return;

            _isResponsiveCompactMode = nextResponsiveCompact;
            ApplyViewMode();
        }

        private void ApplyViewMode()
        {
            if (_isApplyingViewMode) return;

            _isApplyingViewMode = true;
            try
            {
                bool effectiveCompact = IsEffectiveCompactMode();

                if (PanelCombatLog != null) PanelCombatLog.Visibility = effectiveCompact ? Visibility.Collapsed : Visibility.Visible;
                if (PanelContent != null) PanelContent.Margin = effectiveCompact ? new Thickness(10, 8, 10, 8) : new Thickness(12);
                if (ExpThreshold != null) ExpThreshold.Margin = effectiveCompact ? new Thickness(0, 0, 0, 6) : new Thickness(0, 0, 0, 10);
                if (ExpAdvanced != null) ExpAdvanced.Margin = effectiveCompact ? new Thickness(0, 0, 0, 6) : new Thickness(0, 0, 0, 10);
                if (ExpMelee != null) ExpMelee.Margin = new Thickness(0);
                if (PanelTargetType != null) PanelTargetType.Margin = effectiveCompact ? new Thickness(0, 0, 0, 6) : new Thickness(0, 0, 0, 10);
                if (GridTurnType != null) GridTurnType.Margin = effectiveCompact ? new Thickness(0, 0, 0, 6) : new Thickness(0, 0, 0, 10);

                if (PanelHitCount != null && PanelContent != null)
                {
                    if (effectiveCompact)
                    {
                        PanelHitCount.Margin = new Thickness(0);
                        PanelContent.Margin = new Thickness(15, 15, 15, 5);
                    }
                    else
                    {
                        PanelHitCount.Margin = new Thickness(0, 0, 0, 6);
                        PanelContent.Margin = new Thickness(15, 15, 15, 8);
                    }
                }

                if (ControlPanel != null)
                {
                    ControlPanel.Height = double.NaN;
                }

                RefreshViewModeButtons();
            }
            finally
            {
                _isApplyingViewMode = false;
            }

            ClampControlPanelToWindow();
            UpdateThresholdLayout();
        }

        private void BtnThemeColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Background is SolidColorBrush brush)
            {
                _themeColor = brush.Color;
                ApplyThemeColor();
                Log($"Couleur de thème changée : {_themeColor.ToString()}");
            }
        }

        private void BtnCollapse_Click(object sender, RoutedEventArgs e)
        {
            TogglePanelCollapse();
        }

        private double _savedPanelHeight = double.NaN;

        private void RestorePanelHeight()
        {
            if (double.IsNaN(ControlPanel.Height) && !double.IsNaN(_savedPanelHeight))
            {
                ControlPanel.Height = _savedPanelHeight;
            }
            ClampControlPanelToWindow();
        }

        private void MainPanelSection_Changed(object sender, RoutedEventArgs e)
        {
            QueueAutoFitControlPanelHeight(saveAfterFit: true);
        }

        private void QueueAutoFitControlPanelHeight(bool saveAfterFit = false)
        {
            if (ControlPanel == null) return;

            _saveAfterAutoFitControlPanel |= saveAfterFit;
            if (_isAutoFitControlPanelQueued) return;

            _isAutoFitControlPanelQueued = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _isAutoFitControlPanelQueued = false;
                bool shouldSave = _saveAfterAutoFitControlPanel;
                _saveAfterAutoFitControlPanel = false;
                AutoFitControlPanelHeight(shouldSave);
            }), DispatcherPriority.Loaded);
        }

        private void AutoFitControlPanelHeight(bool saveAfterFit)
        {
            if (ControlPanel == null || _isAutoFittingControlPanel || ControlPanel.Visibility != Visibility.Visible) return;

            if (SettingsContent != null && SettingsContent.Visibility == Visibility.Visible && IsEffectiveCompactMode())
            {
                ClampControlPanelToWindow();
                if (saveAfterFit) SaveGridConfig(rememberBounds: false);
                return;
            }

            _isAutoFittingControlPanel = true;
            try
            {
                RunWithoutLayoutPersistence(() =>
                {
                    ControlPanel.Height = double.NaN;
                    ControlPanel.UpdateLayout();

                    double desiredHeight = ControlPanel.DesiredSize.Height;
                    if (double.IsNaN(desiredHeight) || double.IsInfinity(desiredHeight) || desiredHeight <= 0)
                    {
                        desiredHeight = ControlPanel.ActualHeight;
                    }

                    double maxHeight = GetControlPanelMaxHeight();
                    if (!_isCompactMode && !_isResponsiveCompactMode)
                    {
                        FitWindowHeightToControlPanel();
                    }
                    else
                    {
                        ControlPanel.Height = desiredHeight > maxHeight
                            ? maxHeight
                            : double.NaN;
                    }

                    ClampControlPanelToWindow();
                });
            }
            finally
            {
                _isAutoFittingControlPanel = false;
            }

            if (saveAfterFit) SaveGridConfig(rememberBounds: false);
        }

        private void TogglePanelCollapse()
        {
            if (EnsureMandatoryNoticeFlowVisible()) return;

            if (PanelContent.Visibility == Visibility.Visible || SettingsContent.Visibility == Visibility.Visible || NoticeContent.Visibility == Visibility.Visible)
            {
                _savedPanelHeight = GetCurrentControlPanelHeight();
                if (!IsUsablePanelHeight(_savedPanelHeight))
                {
                    _savedPanelHeight = IsUsablePanelHeight(_panelHeight) ? _panelHeight : CompactPanelDefaultHeight;
                }
                PanelContent.Visibility = Visibility.Collapsed;
                SettingsContent.Visibility = Visibility.Collapsed;
                NoticeContent.Visibility = Visibility.Collapsed;
                ControlPanel.Height = double.NaN; // Permet à la fenêtre de se rétracter sur le titre
            }
            else
            {
                PanelContent.Visibility = Visibility.Visible;
                RestorePanelHeight(); // Restaure la hauteur précédente
                QueueAutoFitControlPanelHeight();
            }
        }

        private void ControlPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsiveCompactMode();
            UpdateThresholdLayout();
            ClampControlPanelToWindow();
        }

        private void UpdateThresholdLayout()
        {
            if (ThresholdGrid == null || ThresholdGridCompact == null) return;

            double width = !double.IsNaN(ControlPanel.Width) && ControlPanel.Width > 0
                ? ControlPanel.Width
                : ControlPanel.ActualWidth;
            bool useCompactThresholds = width < 285.0;

            Visibility fullVisibility = useCompactThresholds ? Visibility.Collapsed : Visibility.Visible;
            Visibility compactVisibility = useCompactThresholds ? Visibility.Visible : Visibility.Collapsed;
            bool changed = ThresholdGrid.Visibility != fullVisibility ||
                           ThresholdGridCompact.Visibility != compactVisibility;

            ThresholdGrid.Visibility = fullVisibility;
            ThresholdGridCompact.Visibility = compactVisibility;

            if (changed)
            {
                QueueAutoFitControlPanelHeight();
            }
        }

        private bool _isDraggingPanel = false;
        private Point _dragClickPosition;

        private void ControlPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                TogglePanelCollapse();
                return;
            }
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDraggingPanel = true;
                _dragClickPosition = e.GetPosition(ControlPanel);
                ((UIElement)sender).CaptureMouse();
            }
        }

        private void ControlPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingPanel)
            {
                Point windowPosition = e.GetPosition(this);
                double newX = windowPosition.X - _dragClickPosition.X;
                double newY = windowPosition.Y - _dragClickPosition.Y;

                double maxX = Math.Max(0, GetLayoutWidth() - ControlPanel.ActualWidth - ControlPanelScreenPadding);
                double maxY = Math.Max(0, GetLayoutHeight() - ControlPanel.ActualHeight - ControlPanelScreenPadding);
                newX = ClampFinite(newX, 0, maxX);
                newY = ClampFinite(newY, 0, maxY);
                if (newX < 0) newX = 0;
                if (newY < 0) newY = 0;
                if (newX + ControlPanel.ActualWidth > this.ActualWidth) newX = Math.Max(0, this.ActualWidth - ControlPanel.ActualWidth);
                if (newY + ControlPanel.ActualHeight > this.ActualHeight) newY = Math.Max(0, this.ActualHeight - ControlPanel.ActualHeight);

                ControlPanel.Margin = new Thickness(newX, newY, 0, 0);
                ClampControlPanelToWindow();
                QueuePanelBoundsSave();
            }
        }

        private void ControlPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingPanel)
            {
                _isDraggingPanel = false;
                ((UIElement)sender).ReleaseMouseCapture();
                SavePanelBoundsNow();
            }
        }

        private void MenuResizeGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double newWidth = ControlPanel.ActualWidth + e.HorizontalChange;
            double newHeight = ControlPanel.ActualHeight + e.VerticalChange;

            ControlPanel.Width = ClampFinite(newWidth, ControlPanelMinWidthValue, ControlPanelMaxWidthValue);
            FitWindowWidthToControlPanel();
            ControlPanel.Height = ClampFinite(newHeight, ControlPanelMinHeightValue, GetControlPanelMaxHeight());
            UpdateResponsiveCompactMode();
            ClampControlPanelToWindow();
            QueuePanelBoundsSave();

            // Limites minimales pour ne pas écraser le contenu
            if (newWidth >= 100) ControlPanel.Width = newWidth;
            if (newHeight >= 100) ControlPanel.Height = newHeight;
        }

        private void MenuResizeRightGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double newWidth = ControlPanel.ActualWidth + e.HorizontalChange;

            ControlPanel.Width = ClampFinite(newWidth, ControlPanelMinWidthValue, ControlPanelMaxWidthValue);
            FitWindowWidthToControlPanel();
            UpdateResponsiveCompactMode();
            ClampControlPanelToWindow();
            QueuePanelBoundsSave();
        }

        private void MenuResizeGrip_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            SavePanelBoundsNow();
        }

        protected override void OnClosed(EventArgs e)
        {
            _transparencyTimer?.Stop();
            StopUpdatePromptTimer();
            SavePanelBoundsNow();
            SaveGridConfig();
            UnregisterHotKey(_windowHandle, HOTKEY_ID_F2);
            UnregisterHotKey(_windowHandle, HOTKEY_ID_F3);
            UnregisterHotKey(_windowHandle, HOTKEY_ID_F4);
            UnregisterHotKey(_windowHandle, HOTKEY_ID_DEBUG);
            Log("=== FROST FERMÉ NORMALEMENT ===");
            base.OnClosed(e);
        }

        // --- SYSTÈME DE SAUVEGARDE DU CALIBRAGE ---
        private void SaveGridConfig(bool rememberBounds = true)
        {
            try
            {
                if (!_canPersistGridConfig) return;

                if (rememberBounds && !_suspendLayoutPersistence && ControlPanel != null)
                {
                    CaptureCurrentLayoutBounds();
                }

                StoreCurrentCalibrationToSelectedMode();
                GridCalibrationProfile fullscreenProfile = _fullscreenCalibration;
                GridCalibrationProfile windowedProfile = _windowedCalibration;
                EnsureCalibrationProfileReferenceSize(fullscreenProfile);
                EnsureCalibrationProfileReferenceSize(windowedProfile);
                GridCalibrationProfile selectedProfile = GetCalibrationProfile(_selectedGameDisplayMode);
                EnsureCalibrationProfileReferenceSize(selectedProfile);

                string colorHex = _themeColor.ToString();
                double panelOpacity = Math.Clamp(ControlPanel?.Opacity ?? 1.0, MinPanelOpacity, MaxPanelOpacity);
                string data = string.Join(";", new[]
                {
                    selectedProfile.OffsetX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    selectedProfile.OffsetY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    selectedProfile.TileWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    selectedProfile.TileHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    panelOpacity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _langIdx.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _colorblindMode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _iconIdx.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    colorHex,
                    _vkStart.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _vkToggle.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _vkClear.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _isLargeText.ToString(),
                    _selectedScreenDeviceName,
                    _isCompactMode.ToString(),
                    _onbX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _onbY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _compactX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _compactY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _compactScale.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _panelX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _panelY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _panelWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _panelHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _windowX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _windowY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _windowWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _windowHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _creatorX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _creatorY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _creatorScale.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _selectedGameDisplayMode.ToString(),
                    fullscreenProfile.OffsetX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    fullscreenProfile.OffsetY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    fullscreenProfile.TileWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    fullscreenProfile.TileHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    windowedProfile.OffsetX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    windowedProfile.OffsetY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    windowedProfile.TileWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    windowedProfile.TileHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _hasCompletedMandatoryNoticeFlow.ToString(),
                    GridConfigFormatVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _lastDownloadedReleaseTag ?? string.Empty,
                    _lastDownloadedInstallerName ?? string.Empty,
                    fullscreenProfile.ReferenceWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    fullscreenProfile.ReferenceHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    windowedProfile.ReferenceWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    windowedProfile.ReferenceHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
                File.WriteAllText(GridConfigPath, data);
                File.WriteAllText(GridConfigBackupPath, data);
                _hasSavedGridConfig = true;
            }
            catch { }
        }

        private void LoadGridConfig(bool allowBackupRestore = true)
        {
            try
            {
                _selectedGameDisplayMode = GameDisplayMode.Windowed;
                _fullscreenCalibration = CreateDefaultFullscreenCalibration();
                _windowedCalibration = CreateDefaultWindowedCalibration();

                if (File.Exists(GridConfigPath))
                {
                    _hasSavedGridConfig = true;
                    _canPersistGridConfig = true;
                    var parts = File.ReadAllText(GridConfigPath).Split(';');
                    int savedConfigVersion = 0;
                    GridCalibrationProfile loadedProfile = CreateDefaultFullscreenCalibration();
                    if (parts.Length >= 3)
                    {
                        _gridOffsetX = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                        _gridOffsetY = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                        _tileWidth = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);

                        if (parts.Length >= 4)
                            _tileHeight = double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
                        else
                            _tileHeight = _tileWidth / 2; // Compatibilité avec l'ancienne sauvegarde

                        loadedProfile = CreateCalibrationProfile(_gridOffsetX, _gridOffsetY, _tileWidth, _tileHeight);
                        _fullscreenCalibration = loadedProfile.Clone();
                        _windowedCalibration = loadedProfile.Clone();
                    }
                    if (parts.Length >= 9)
                    {
                        double op = Math.Clamp(double.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture), MinPanelOpacity, MaxPanelOpacity);
                        if (ControlPanel != null) ControlPanel.Opacity = op;
                        if (SliderOpacity != null) SliderOpacity.Value = op;

                        _langIdx = int.Parse(parts[5]);
                        _colorblindMode = int.Parse(parts[6]);
                        _iconIdx = int.Parse(parts[7]);

                        try
                        {
                            _themeColor = (Color)ColorConverter.ConvertFromString(parts[8]);
                        }
                        catch { }
                    }
                    if (parts.Length >= 12)
                    {
                        _vkStart = uint.Parse(parts[9]);
                        _vkToggle = uint.Parse(parts[10]);
                        _vkClear = uint.Parse(parts[11]);
                    }

                    if (TxtKeyStart != null) TxtKeyStart.Text = KeyInterop.KeyFromVirtualKey((int)_vkStart).ToString();
                    if (TxtKeyToggle != null) TxtKeyToggle.Text = KeyInterop.KeyFromVirtualKey((int)_vkToggle).ToString();
                    if (TxtKeyClear != null) TxtKeyClear.Text = KeyInterop.KeyFromVirtualKey((int)_vkClear).ToString();
                    if (parts.Length >= 13)
                    {
                        if (bool.TryParse(parts[12], out bool isLarge)) _isLargeText = isLarge;
                    }
                    if (parts.Length >= 14)
                    {
                        _selectedScreenDeviceName = parts[13];
                    }
                    if (parts.Length >= 15)
                    {
                        if (bool.TryParse(parts[14], out bool isCompact)) _isCompactMode = isCompact;
                    }
                    if (parts.Length >= 19)
                    {
                        if (double.TryParse(parts[15], System.Globalization.CultureInfo.InvariantCulture, out double ox)) _onbX = ox;
                        if (double.TryParse(parts[16], System.Globalization.CultureInfo.InvariantCulture, out double oy)) _onbY = oy;
                        if (double.TryParse(parts[17], System.Globalization.CultureInfo.InvariantCulture, out double cx)) _compactX = cx;
                        if (double.TryParse(parts[18], System.Globalization.CultureInfo.InvariantCulture, out double cy)) _compactY = cy;

                        if (OnboardingTranslate != null) { OnboardingTranslate.X = _onbX; OnboardingTranslate.Y = _onbY; }
                    }
                    if (parts.Length >= 20)
                    {
                        if (double.TryParse(parts[19], System.Globalization.CultureInfo.InvariantCulture, out double scale)) _compactScale = scale;
                    }
                    if (parts.Length >= 24)
                    {
                        if (double.TryParse(parts[20], System.Globalization.CultureInfo.InvariantCulture, out double px)) _panelX = px;
                        if (double.TryParse(parts[21], System.Globalization.CultureInfo.InvariantCulture, out double py)) _panelY = py;
                        if (double.TryParse(parts[22], System.Globalization.CultureInfo.InvariantCulture, out double pw)) _panelWidth = pw;
                        if (double.TryParse(parts[23], System.Globalization.CultureInfo.InvariantCulture, out double ph)) _panelHeight = ph;

                    }
                    if (parts.Length >= 28)
                    {
                        if (double.TryParse(parts[24], System.Globalization.CultureInfo.InvariantCulture, out double wx)) _windowX = wx;
                        if (double.TryParse(parts[25], System.Globalization.CultureInfo.InvariantCulture, out double wy)) _windowY = wy;
                        if (double.TryParse(parts[26], System.Globalization.CultureInfo.InvariantCulture, out double ww)) _windowWidth = ww;
                        if (double.TryParse(parts[27], System.Globalization.CultureInfo.InvariantCulture, out double wh)) _windowHeight = wh;
                    }
                    if (parts.Length >= 31)
                    {
                        if (double.TryParse(parts[28], System.Globalization.CultureInfo.InvariantCulture, out double cx)) _creatorX = cx;
                        if (double.TryParse(parts[29], System.Globalization.CultureInfo.InvariantCulture, out double cy)) _creatorY = cy;
                        if (double.TryParse(parts[30], System.Globalization.CultureInfo.InvariantCulture, out double cs)) _creatorScale = cs;
                        if (CreatorTranslate != null) { CreatorTranslate.X = _creatorX; CreatorTranslate.Y = _creatorY; }
                        if (CreatorScale != null) { CreatorScale.ScaleX = _creatorScale; CreatorScale.ScaleY = _creatorScale; }
                    }
                    else if (parts.Length >= 24)
                    {
                        _windowX = _panelX + WindowDefaultX;
                        _windowY = _panelY + WindowDefaultY;
                        _windowWidth = _panelX + _panelWidth + ControlPanelScreenPadding;
                        _windowHeight = _panelY + _panelHeight + ControlPanelScreenPadding;
                    }
                    if (parts.Length >= 32 &&
                        Enum.TryParse(parts[31], true, out GameDisplayMode savedMode))
                    {
                        _selectedGameDisplayMode = savedMode;
                    }
                    if (parts.Length >= 36 &&
                        TryParseCalibrationProfile(parts, 32, out GridCalibrationProfile fullscreenProfile))
                    {
                        _fullscreenCalibration = fullscreenProfile;
                    }
                    if (parts.Length >= 40 &&
                        TryParseCalibrationProfile(parts, 36, out GridCalibrationProfile windowedProfile))
                    {
                        _windowedCalibration = windowedProfile;
                    }
                    if (parts.Length >= 41 &&
                        bool.TryParse(parts[40], out bool hasCompletedMandatoryNoticeFlow))
                    {
                        _hasCompletedMandatoryNoticeFlow = hasCompletedMandatoryNoticeFlow;
                    }
                    if (parts.Length >= 42)
                    {
                        int.TryParse(parts[41], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out savedConfigVersion);
                    }
                    if (parts.Length >= 43)
                    {
                        _lastDownloadedReleaseTag = parts[42];
                    }
                    if (parts.Length >= 44)
                    {
                        _lastDownloadedInstallerName = parts[43];
                    }
                    if (parts.Length >= 48)
                    {
                        if (TryParseInvariantDouble(parts[44], out double fullscreenReferenceWidth))
                            _fullscreenCalibration.ReferenceWidth = fullscreenReferenceWidth;
                        if (TryParseInvariantDouble(parts[45], out double fullscreenReferenceHeight))
                            _fullscreenCalibration.ReferenceHeight = fullscreenReferenceHeight;
                        if (TryParseInvariantDouble(parts[46], out double windowedReferenceWidth))
                            _windowedCalibration.ReferenceWidth = windowedReferenceWidth;
                        if (TryParseInvariantDouble(parts[47], out double windowedReferenceHeight))
                            _windowedCalibration.ReferenceHeight = windowedReferenceHeight;
                    }

                    EnsureCalibrationProfileReferenceSize(_fullscreenCalibration);
                    EnsureCalibrationProfileReferenceSize(_windowedCalibration);
                    RepairLegacyAnchoredCalibrationProfilesIfNeeded(savedConfigVersion);
                    MigrateRemovedOverlayCanvasMarginIfNeeded(savedConfigVersion);
                    RepairTransientWindowedReferenceIfNeeded(savedConfigVersion);
                    _pendingWindowedV9ReferenceRepair = NeedsWindowedV9ReferenceRepair(savedConfigVersion);
                    ApplySelectedDisplayModeCalibration();
                }
            }
            catch
            {
                if (!allowBackupRestore)
                    return;

                try
                {
                    if (File.Exists(GridConfigBackupPath))
                    {
                        File.Copy(GridConfigBackupPath, GridConfigPath, true);
                        LoadGridConfig(allowBackupRestore: false);
                    }
                }
                catch { }
            }
        }
    }
}
