using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
        private uint _vkStart = 0x71; // Touche F2 dynamique
        private uint _vkToggle = 0x72; // Touche F3 dynamique
        private uint _vkClear = 0x73; // Touche F4 dynamique

        // État de l'application
        private enum AppState { Idle, WaitingForPlayer, WaitingForMonster }
        private AppState _currentState = AppState.Idle;
        private bool _isDebugMode = false;

        // Le système de Grille Absolue (Méthode Luframe)
        private double _gridOffsetX = 676.9333333333333; // Ton calibrage par défaut
        private double _gridOffsetY = 295.0666666666667;
        
        // Coordonnées matricielles (Colonnes, Lignes)
        private bool _isPlayerSet = false;
        private bool _isMonsterSet = false;
        private Point _playerCell;
        private Point _monsterCell;

        // Taille dynamique de la grille Dofus (ajustable via Ctrl+Molette)
        private double _tileWidth = 61.906011457519114; // Ton calibrage par défaut
        private double _tileHeight = 31.086352010843605; 

        // Angle actuel basé sur le seuil de vie sélectionné (en degrés)
        private int _currentSeuil = 1;
        private double _currentAngle = 90; 

        // Nombre de coups (CàC)
        private int _hitCount = 0;

        // Calibration
        private bool _isCalibrating = false;
        private bool _isCalibrationMode = false;
        private Point _dragStartPoint;
        private double _dragStartOffsetX;
        private double _dragStartOffsetY;

        // Mécaniques Gousset
        private bool _isBossTarget = false;
        private bool _isOddTurn = true;
        private bool _isClockwise = true;

        private int _colorblindMode = 0;
        private int _langIdx = 0;
        private int _iconIdx = 0;
        private Color _themeColor = Color.FromRgb(77, 168, 218); // Bleu glace FROST (#4DA8DA)
        private bool _isLargeText = false;
        private const double PanelDefaultX = 0.0;
        private const double PanelDefaultY = 0.0;
        private const double CompactPanelDefaultWidth = 216.0;
        private const double CompactPanelDefaultHeight = 494.0;
        private const double WindowDefaultX = 0.0;
        private const double WindowDefaultY = 0.0;
        private const double WindowDefaultWidth = 1280.0;
        private const double WindowDefaultHeight = 671.3333333333333;
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
        private bool _isAutoFittingControlPanel = false;
        private bool _isAutoFitControlPanelQueued = false;
        private bool _saveAfterAutoFitControlPanel = false;
        private bool _isApplyingInitialLayout = true;
        private bool _hasSavedGridConfig = false;
        private bool _suspendLayoutPersistence = false;
        private DispatcherTimer? _panelBoundsSaveTimer;


        private Point _lastHoveredCell = new Point(-999, -999);
        private bool _isOverlayEnabled = true;
        private string _lastStatusText = "";
        private Brush _lastStatusForeground = Brushes.Gray;
        private IntPtr _windowHandle;
        private DispatcherTimer? _transparencyTimer;
        private bool _isClickThrough = false;
        private int _onboardingStep = 1;
        private static readonly string AppDataDirectory = InitializeAppDataDirectory();
        private static readonly string GridConfigPath = System.IO.Path.Combine(AppDataDirectory, "grid_config.txt");
        private static readonly string DebugLogPath = System.IO.Path.Combine(AppDataDirectory, "frost_debug.log");
        private static readonly string LegacyGridConfigPath = System.IO.Path.Combine(AppContext.BaseDirectory, "grid_config.txt");

        private static string InitializeAppDataDirectory()
        {
            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FROST"
            );
            Directory.CreateDirectory(path);
            return path;
        }

        public static void Log(string message)
        {
            try { File.AppendAllText(DebugLogPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n"); } catch { }
        }

        private void SetStatus(string text, Brush foreground)
        {
            _lastStatusText = text;
            _lastStatusForeground = foreground;
        }

        public MainWindow()
        {
            TryMigrateLegacyConfig();
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
                try
                {
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
                    if (!File.Exists(GridConfigPath))
                    {
                        StartOnboarding();
                    }
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
            _panelHeight = IsPositiveFinite(_panelHeight) ? Math.Max(_panelHeight, ControlPanelMinHeightValue) : CompactPanelDefaultHeight;

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
            }
            catch { }
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
            // Si on est en train de cibler, de calibrer, ou sur le Wizard (y compris l'écran de succès final), la fenêtre doit intercepter les clics
            if (_currentState != AppState.Idle || _isCalibrationMode || _isCalibrating || 
               (OnboardingOverlay != null && OnboardingOverlay.Visibility == Visibility.Visible) ||
               (SuccessOverlay != null && SuccessOverlay.Visibility == Visibility.Visible) ||
               _isDebugMode)
            {
                SetWindowClickThrough(false);
                return;
            }

            if (GetCursorPos(out POINT lpPoint))
            {
                try
                {
                    Point mouseScreen = new Point(lpPoint.X, lpPoint.Y);
                    Point mouseRelative = this.PointFromScreen(mouseScreen);
                    
                    if (ControlPanel.IsLoaded && ControlPanel.IsVisible)
                    {
                        GeneralTransform transform = ControlPanel.TransformToAncestor(this);
                        Rect panelBounds = transform.TransformBounds(new Rect(0, 0, ControlPanel.ActualWidth, ControlPanel.ActualHeight));
                        
                        // Marge de sécurité autour du panneau pour que ce soit naturel
                        panelBounds.Inflate(30, 30); 

                        if (panelBounds.Contains(mouseRelative))
                            SetWindowClickThrough(false); // Le menu est survolé = cliquable
                        else
                            SetWindowClickThrough(true); // Hors du menu = 100% fantôme (clics au travers)
                    }
                    else SetWindowClickThrough(true);
                }
                catch { SetWindowClickThrough(false); }
            }
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
        // --- CONVERSIONS MATHÉMATIQUES ABSOLUES ---
        private Point PointToCell(Point p)
        {
            double dx = p.X - _gridOffsetX;
            double dy = p.Y - _gridOffsetY;
            int col = (int)Math.Round((dx / (_tileWidth / 2) + dy / (_tileHeight / 2)) / 2, MidpointRounding.AwayFromZero);
            int row = (int)Math.Round((dy / (_tileHeight / 2) - dx / (_tileWidth / 2)) / 2, MidpointRounding.AwayFromZero);
            return new Point(col, row);
        }

        private Point CellToPoint(Point cell)
        {
            double cx = _gridOffsetX + (cell.X - cell.Y) * (_tileWidth / 2);
            double cy = _gridOffsetY + (cell.X + cell.Y) * (_tileHeight / 2);
            return new Point(cx, cy);
        }

        private void StartSequence()
        {
            if (_isCalibrationMode)
            {
                // Désactive proprement le mode calibration avant de lancer le ciblage
                BtnToggleCalibration_Click(null!, null!);
            }

            _currentState = AppState.WaitingForPlayer;
            _isPlayerSet = false;
            _isMonsterSet = false;
            OverlayCanvas.Visibility = Visibility.Visible;
            OverlayCanvas.IsHitTestVisible = true;
            OverlayCanvas.Children.Clear();
            SetStatus(Application.Current?.Resources["StatusStep1"] as string ?? "Étape 1 : Cliquez sur VOTRE personnage.", Brushes.DeepSkyBlue);
            
            // Active le fond pour intercepter les clics, tout en restant presque invisible (1 d'opacité)
            OverlayCanvas.Background = new SolidColorBrush(Color.FromArgb((byte)1, (byte)0, (byte)0, (byte)0)); 
            OverlayCanvas.Cursor = Cursors.Hand; 
            RefreshOverlay();
            Log("Séquence de ciblage démarrée.");
        }

        private void ToggleVisibility()
        {
            _isOverlayEnabled = !_isOverlayEnabled;

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
        }

        private void ClearSequence()
        {
            if (_isCalibrationMode)
            {
                BtnToggleCalibration_Click(null!, null!); // Quitte le mode proprement
                return;
            }

            _currentState = AppState.Idle;
            OverlayCanvas.Children.Clear();
            _isPlayerSet = false;
            _isMonsterSet = false;
            OverlayCanvas.Background = null;
            OverlayCanvas.IsHitTestVisible = false;

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
            // Clic Gauche en mode Calibration Dédié
            if (_isCalibrationMode && e.LeftButton == MouseButtonState.Pressed)
            {
                _isCalibrating = true;
                _dragStartPoint = e.GetPosition(this);
                _dragStartOffsetX = _gridOffsetX;
                _dragStartOffsetY = _gridOffsetY;
                OverlayCanvas.CaptureMouse();
                return;
            }

            // Clic Droit = Début de la calibration (Déplacement de la grille)
            if (e.RightButton == MouseButtonState.Pressed)
            {
                if (_isCalibrationMode)
                {
                    _isCalibrating = true;
                    _dragStartPoint = e.GetPosition(this);
                    _dragStartOffsetX = _gridOffsetX;
                    _dragStartOffsetY = _gridOffsetY;
                    OverlayCanvas.CaptureMouse();
                    RefreshOverlay(); // Pour afficher la grille de calibration temporaire
                }
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed) return;

            if (_currentState == AppState.WaitingForPlayer)
            {
                _playerCell = PointToCell(e.GetPosition(OverlayCanvas));
                _isPlayerSet = true;
                _lastHoveredCell = new Point(-999, -999); // Réinitialise le survol
                
                _currentState = AppState.WaitingForMonster;
                SetStatus(Application.Current?.Resources["StatusStep2"] as string ?? "Étape 2 : Cliquez sur le MONSTRE.", Brushes.Crimson);
                RefreshOverlay();
                Log($"Joueur placé en ({_playerCell.X}, {_playerCell.Y})");
            }
            else if (_currentState == AppState.WaitingForMonster)
            {
                if (_lastHoveredCell != new Point(-999, -999))
                    _monsterCell = _lastHoveredCell;
                else
                    _monsterCell = PointToCell(e.GetPosition(OverlayCanvas));
                
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
            if (_isCalibrating)
            {
                Point currentPos = e.GetPosition(this);
                _gridOffsetX = _dragStartOffsetX + (currentPos.X - _dragStartPoint.X);
                _gridOffsetY = _dragStartOffsetY + (currentPos.Y - _dragStartPoint.Y);
                RefreshOverlay();
                return;
            }

            // Logique de survol
            if (_currentState == AppState.WaitingForMonster)
            {
                Point cell = PointToCell(e.GetPosition(OverlayCanvas));
                if (_lastHoveredCell != cell)
                {
                    _lastHoveredCell = cell;
                    RefreshOverlay();
                }
            }
        }

        private void OverlayCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isCalibrating)
            {
                if (e.RightButton == MouseButtonState.Released || (_isCalibrationMode && e.LeftButton == MouseButtonState.Released))
                {
                    _isCalibrating = false;
                    OverlayCanvas.ReleaseMouseCapture();
                    SaveGridConfig();
                    RefreshOverlay(); // Pour masquer la grille temporaire si hors mode
                }
            }
        }

        private void OverlayCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFast = e.Delta > 0 ? 1.02 : 0.98;
            double zoomSlow = e.Delta > 0 ? 1.005 : 0.995; // Ajustement très fin

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                _tileWidth *= zoomFast;
                _tileHeight *= zoomFast;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                _tileWidth *= zoomSlow;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                _tileHeight *= zoomSlow;
            }
            else return;

            RefreshOverlay();
            SaveGridConfig();
        }

        private Color GetPlayerColor()
        {
            if (_colorblindMode == 1 || _colorblindMode == 2) return Color.FromRgb(86, 180, 233); // Protanopie/Deutéranopie (Bleu ciel)
            if (_colorblindMode == 3) return Color.FromRgb(255, 255, 255); // Tritanopie (Blanc)
            return Colors.DeepSkyBlue; // Normal
        }

        private Color GetBossColor()
        {
            if (_colorblindMode == 1 || _colorblindMode == 2) return Color.FromRgb(213, 94, 0); // Protanopie/Deutéranopie (Orange vif)
            if (_colorblindMode == 3) return Color.FromRgb(255, 0, 0); // Tritanopie (Rouge)
            return Colors.Crimson; // Normal
        }

        private Color GetTargetColor()
        {
            if (_colorblindMode == 1 || _colorblindMode == 2) return Color.FromRgb(0, 100, 255); // Protanopie/Deuteranopie
            if (_colorblindMode == 3) return Color.FromRgb(255, 0, 0); // Tritanopie
            return Color.FromRgb(108, 14, 186); // Normal (Violet)
        }

        private Color GetTPColor()
        {
            if (_colorblindMode == 1 || _colorblindMode == 2) return Color.FromRgb(255, 200, 0); // Protanopie/Deuteranopie
            if (_colorblindMode == 3) return Color.FromRgb(0, 200, 255); // Tritanopie
            return Color.FromRgb(255, 0, 127); // Normal (Rose)
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

            double angleStep = _isClockwise ? 90 : -90;
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

        private void DrawMarker(Point p, Color color, bool isTarget = false, string label = "", double opacity = 1.0)
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
            if (!string.IsNullOrEmpty(label))
            {
                Border labelBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(220, 20, 22, 26)),
                    BorderBrush = new SolidColorBrush(color),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Opacity = opacity,
                    IsHitTestVisible = false
                };

                TextBlock txt = new TextBlock { Text = label, Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center };
                labelBorder.Child = txt;

                labelBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(labelBorder, p.X - (labelBorder.DesiredSize.Width / 2));
                Canvas.SetTop(labelBorder, p.Y - (height / 2) - 22);
                OverlayCanvas.Children.Add(labelBorder);
            }
        }

        private void DrawCalibrationGrid()
        {
            // On dessine une véritable "Grille Dofus" (rectangulaire en quinconce)
            // Cela permet un alignement parfait avec le mode tactique du jeu ou le simulateur
            int cols = 25; // Couvre largement la largeur de l'écran
            int rows = 35; // Couvre largement la hauteur de l'écran

            for (int r = -rows; r <= rows; r++)
            {
                for (int c = -cols; c <= cols; c++)
                {
                    // Disposition en quinconce classique de Dofus
                    double cx = _gridOffsetX + c * _tileWidth + ((Math.Abs(r) % 2 == 1) ? (_tileWidth / 2) : 0);
                    double cy = _gridOffsetY + r * (_tileHeight / 2);

                    Polygon tile = new Polygon
                    {
                        Points = new PointCollection
                        {
                            new Point(_tileWidth / 2, 0),
                            new Point(_tileWidth, _tileHeight / 2),
                            new Point(_tileWidth / 2, _tileHeight),
                            new Point(0, _tileHeight / 2)
                        },
                        // Style épuré type "Simulateur" ou "Mode Tactique"
                        Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 0, 0)), // Lignes rouges
                        StrokeThickness = 1.0,
                        Fill = new SolidColorBrush(Colors.Transparent),
                        IsHitTestVisible = false
                    };

                    Canvas.SetLeft(tile, cx - (_tileWidth / 2));
                    Canvas.SetTop(tile, cy - (_tileHeight / 2));
                    OverlayCanvas.Children.Add(tile);

                    // Mise en valeur de la case centrale pour la repérer facilement
                    Point pCell = PointToCell(new Point(cx, cy));
                    if (pCell.X == 0 && pCell.Y == 0)
                    {
                        tile.Stroke = new SolidColorBrush(Colors.Red);
                        tile.StrokeThickness = 2.0;
                        tile.Fill = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0));
                        
                        string centerText = Application.Current?.Resources["TxtCenter"] as string ?? "Centre";
                        TextBlock txt = new TextBlock { Text = centerText, Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 };
                        Canvas.SetLeft(txt, cx - 18);
                        Canvas.SetTop(txt, cy - 8);
                        OverlayCanvas.Children.Add(txt);
                    }
                }
            }
        }

        private void RefreshOverlay()
        {
            OverlayCanvas.Children.Clear();

            string playerLabel = Application.Current?.Resources["TxtLabelPlayer"] as string ?? "Joueur";
            string bossLabel = Application.Current?.Resources["TxtLabelBoss"] as string ?? "Adversaire";

            if (_isCalibrating || _isCalibrationMode)
            {
                DrawCalibrationGrid();
            }

            if (_isPlayerSet)
                DrawMarker(CellToPoint(_playerCell), GetPlayerColor(), false, playerLabel);

            if (_currentState == AppState.Idle && _isMonsterSet)
            {
                DrawMarker(CellToPoint(_monsterCell), GetBossColor(), false, bossLabel);
                DrawMechanics(_monsterCell, 1.0);
            }
            else if (_currentState == AppState.WaitingForMonster && _lastHoveredCell != new Point(-999, -999))
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

        private void BtnRotation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                _isClockwise = btn.Tag.ToString() == "CW";
                BtnRotationCW.Background = BtnRotationCCW.Background = new SolidColorBrush(Color.FromRgb(26, 28, 35));
                BtnRotationCW.Foreground = BtnRotationCCW.Foreground = new SolidColorBrush(Color.FromRgb(122, 128, 144));
                BtnRotationCW.BorderBrush = BtnRotationCCW.BorderBrush = new SolidColorBrush(Color.FromRgb(50, 53, 64));

                SolidColorBrush themeBrush = new SolidColorBrush(_themeColor);
                btn.Background = new SolidColorBrush(Color.FromRgb(45, 48, 59));
                btn.Foreground = themeBrush;
                btn.BorderBrush = themeBrush;

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

        private void EnsureCalibrationClosed()
        {
            if (_isCalibrationMode)
            {
                BtnToggleCalibration_Click(null!, null!);
            }
        }

        // Bascule vers la Notice d'utilisation
        private void BtnInfo_Click(object sender, RoutedEventArgs e)
        {
            NoticeBubble.IsOpen = false; // Ferme la bulle d'incitation puisqu'on a cliqué !
            EnsureCalibrationClosed();
            PanelContent.Visibility = Visibility.Collapsed;
            SettingsContent.Visibility = Visibility.Collapsed;
            NoticeContent.Visibility = Visibility.Visible;
            
            if (NoticePageContainer != null) NoticePageContainer.Visibility = Visibility.Collapsed;
            if (NoticeSummary != null) NoticeSummary.Visibility = Visibility.Visible;

            RestorePanelHeight();
            QueueAutoFitControlPanelHeight();
            Log("Notice ouverte.");
        }

        private void BtnNoticePage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                NoticeSummary.Visibility = Visibility.Collapsed;
                NoticePageContainer.Visibility = Visibility.Visible;

                Page1.Visibility = tag == "1" ? Visibility.Visible : Visibility.Collapsed;
                Page2.Visibility = tag == "2" ? Visibility.Visible : Visibility.Collapsed;
                Page3.Visibility = tag == "3" ? Visibility.Visible : Visibility.Collapsed;
                Page4.Visibility = tag == "4" ? Visibility.Visible : Visibility.Collapsed;
                Page5.Visibility = tag == "5" ? Visibility.Visible : Visibility.Collapsed;
                
                RestorePanelHeight();
                QueueAutoFitControlPanelHeight();
            }
        }

        private void BtnNoticeSummary_Click(object sender, RoutedEventArgs e)
        {
            NoticePageContainer.Visibility = Visibility.Collapsed;
            NoticeSummary.Visibility = Visibility.Visible;
            RestorePanelHeight();
            QueueAutoFitControlPanelHeight();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            NoticeContent.Visibility = Visibility.Collapsed;
            SettingsContent.Visibility = Visibility.Collapsed;
            PanelContent.Visibility = Visibility.Visible;
            QueueAutoFitControlPanelHeight();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            PanelContent.Visibility = Visibility.Collapsed;
            NoticeContent.Visibility = Visibility.Collapsed;
            SettingsContent.Visibility = Visibility.Visible;
            RestorePanelHeight();
            Log("Paramètres ouverts.");
        }

        private void BtnBackSettings_Click(object sender, RoutedEventArgs e)
        {
            EnsureCalibrationClosed();
            SettingsContent.Visibility = Visibility.Collapsed;
            PanelContent.Visibility = Visibility.Visible;
            QueueAutoFitControlPanelHeight();
        }

        private void BtnToggleCalibration_Click(object sender, RoutedEventArgs e)
        {
            _isCalibrationMode = !_isCalibrationMode;

            if (_isCalibrationMode)
            {
                _currentState = AppState.Idle; 
                _isPlayerSet = false;
                _isMonsterSet = false;

                OverlayCanvas.Visibility = Visibility.Visible;
                OverlayCanvas.IsHitTestVisible = true;
                OverlayCanvas.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
                OverlayCanvas.Cursor = Cursors.SizeAll;

                SetStatus(Application.Current?.Resources["TxtCalibStatus"] as string ?? "Glissez | Molette + Ctrl (Taille) / Shift (Largeur) / Alt (Hauteur)", Brushes.Orange);

                if (BtnToggleCalibration != null)
                {
                    BtnToggleCalibration.Content = Application.Current?.Resources["TxtBtnCalibOff"] as string ?? "Quitter le mode Calibration";
                    BtnToggleCalibration.Foreground = Brushes.White;
                    BtnToggleCalibration.Background = new SolidColorBrush(Color.FromRgb(220, 20, 60)); // Bouton en Rouge
                }
                Log("Mode Calibration activé.");
            }
            else
            {
                OverlayCanvas.IsHitTestVisible = false;
                OverlayCanvas.Background = null;
                OverlayCanvas.Cursor = Cursors.Arrow;

                SetStatus(Application.Current.Resources["StatusReady"] as string ?? "Prêt. En attente...", Brushes.Gray);

                if (BtnToggleCalibration != null)
                {
                    BtnToggleCalibration.Content = Application.Current?.Resources["TxtBtnCalibOn"] as string ?? "Activer le mode Calibration";
                    BtnToggleCalibration.Foreground = new SolidColorBrush(Color.FromRgb(229, 231, 235));
                    BtnToggleCalibration.Background = new SolidColorBrush(Color.FromRgb(35, 37, 46));
                }
                SaveGridConfig();
                Log("Mode Calibration désactivé.");
            }

            RefreshOverlay();
        }
        
        private void BtnResetCalibration_Click(object sender, RoutedEventArgs e)
        {
            _gridOffsetX = 676.9333333333333;
            _gridOffsetY = 295.0666666666667;
            _tileWidth = 61.906011457519114;
            _tileHeight = 31.086352010843605;
            SaveGridConfig();
            RefreshOverlay();
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
            ControlPanel.Visibility = Visibility.Visible;
            SaveGridConfig();
            Log("Onboarding fermé via la croix.");
        }

        private void BtnOnbNext_Click(object sender, RoutedEventArgs e)
        {
            if (_onboardingStep < 7) {
                ShowOnboardingStep(_onboardingStep + 1);
            } else {
                // Reconstruire l'arbre XAML d'origine est complexe, la solution la plus robuste 
                // est de simplement masquer l'onboarding et relancer l'application visuellement
                OnboardingOverlay.Visibility = Visibility.Collapsed;
                ControlPanel.Visibility = Visibility.Visible;
                SaveGridConfig();
                
                if (SuccessTranslate != null)
                {
                    SuccessTranslate.X = _onbX;
                    SuccessTranslate.Y = _onbY;
                }

                // Affiche le nouvel écran de succès intégré
                SuccessOverlay.Visibility = Visibility.Visible;
                Log("Onboarding terminé, affichage de l'écran de succès.");
            }
        }

        private void BtnSuccessContinue_Click(object sender, RoutedEventArgs e)
        {
            SuccessOverlay.Visibility = Visibility.Collapsed;
            
            // Affiche la bulle de notification pointant vers la notice en Forcé (StaysOpen=True)
            NoticeBubble.IsOpen = true;
        }

        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsLoaded && ControlPanel != null)
            {
                ControlPanel.Opacity = e.NewValue;
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
            if (_isCalibrationMode)
            {
                SetStatus(Application.Current?.Resources["TxtCalibStatus"] as string ?? "Glissez | Molette + Ctrl (Taille) / Shift (Largeur) / Alt (Hauteur)", Brushes.Orange);
                if (BtnToggleCalibration != null) BtnToggleCalibration.Content = Application.Current?.Resources["TxtBtnCalibOff"] as string ?? "Quitter le mode Calibration";
            }
            else
            {
                if (_currentState == AppState.Idle) SetStatus(Application.Current?.Resources["StatusReady"] as string ?? "Prêt. En attente...", Brushes.Gray);
                else if (_currentState == AppState.WaitingForPlayer) SetStatus(Application.Current?.Resources["StatusStep1"] as string ?? "Étape 1 : Cliquez sur VOTRE personnage.", Brushes.DeepSkyBlue);
                else if (_currentState == AppState.WaitingForMonster) SetStatus(Application.Current?.Resources["StatusStep2"] as string ?? "Étape 2 : Cliquez sur le MONSTRE.", Brushes.Crimson);
                
                if (BtnToggleCalibration != null) BtnToggleCalibration.Content = Application.Current?.Resources["TxtBtnCalibOn"] as string ?? "Activer le mode Calibration";
            }
            
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

            if (_isClockwise && BtnRotationCW != null) { BtnRotationCW.Foreground = themeBrush; BtnRotationCW.BorderBrush = themeBrush; }
            else if (!_isClockwise && BtnRotationCCW != null) { BtnRotationCCW.Foreground = themeBrush; BtnRotationCCW.BorderBrush = themeBrush; }

            if (BtnViewFull != null) {
                BtnViewFull.BorderBrush = !_isCompactMode ? themeBrush : new SolidColorBrush(Color.FromRgb(50, 53, 64));
                BtnViewFull.Foreground = !_isCompactMode ? themeBrush : Brushes.White;
            }
            if (BtnViewCompact != null) {
                BtnViewCompact.BorderBrush = _isCompactMode ? themeBrush : new SolidColorBrush(Color.FromRgb(50, 53, 64));
                BtnViewCompact.Foreground = _isCompactMode ? themeBrush : Brushes.White;
            }

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
                        Bounds = new Rect(mi.rcMonitor.left, mi.rcMonitor.top, mi.rcMonitor.right - mi.rcMonitor.left, mi.rcMonitor.bottom - mi.rcMonitor.top),
                        IsPrimary = isPrim
                    });
                }
                return true;
            };
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            return screens;
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
            // DÉSACTIVÉ : Le menu ne doit plus s'écraser ou s'étirer quand on redimensionne la fenêtre
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
            if (panelIsCollapsed && IsPositiveFinite(_savedPanelHeight))
            {
                _panelHeight = _savedPanelHeight;
            }
            else if (IsPositiveFinite(currentPanelHeight))
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
            if (panelIsCollapsed && IsPositiveFinite(_savedPanelHeight))
            {
                _panelHeight = _savedPanelHeight;
            }
            else if (IsPositiveFinite(currentPanelHeight))
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
            double width = ClampFinite(_windowWidth, GetWindowMinWidthValue(), screen.Bounds.Width);
            double height = ClampFinite(_windowHeight, GetWindowMinHeightValue(), screen.Bounds.Height);

            this.Left = screen.Bounds.Left + WindowDefaultX;
            this.Top = screen.Bounds.Top + WindowDefaultY;
            this.Width = width;
            this.Height = height;
            _selectedScreenDeviceName = screen.DeviceName;
            RememberWindowBounds();
            ClampControlPanelToWindow();
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
                btn.Style = (Style)FindResource("DofusButtonStyle");
                btn.HorizontalContentAlignment = HorizontalAlignment.Center;
                btn.Padding = new Thickness(12, 8, 12, 8);
                btn.Margin = new Thickness(0, 0, 0, 5);
                if (screen.DeviceName == _selectedScreenDeviceName)
                {
                    btn.Foreground = new SolidColorBrush(_themeColor);
                    btn.BorderBrush = new SolidColorBrush(_themeColor);
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
                btn.Style = (Style)FindResource("DofusButtonStyle");
                btn.HorizontalContentAlignment = HorizontalAlignment.Center;
                btn.Padding = new Thickness(15, 10, 15, 10);
                btn.Margin = new Thickness(0, 0, 0, 10);
                btn.Width = 250;
                
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
                    double compactHeight = !double.IsNaN(_panelHeight) && !double.IsInfinity(_panelHeight) && _panelHeight > 0
                        ? _panelHeight
                        : CompactPanelDefaultHeight;

                    ControlPanel.Width = ClampFinite(targetWidth, ControlPanelMinWidthValue, GetControlPanelMaxWidth());
                    ControlPanel.Height = compactMode ? compactHeight : double.NaN;
                }

                _isCompactMode = btn.Tag.ToString() == "Compact";
                ApplyViewMode();
                if (compactMode)
                {
                    ApplyCompactWindowSize();
                }
                else
                {
                    FitWindowHeightToControlPanel();
                }
                QueueAutoFitControlPanelHeight();
                SaveGridConfig(rememberBounds: false);
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
                if (PanelRotation != null) PanelRotation.Margin = effectiveCompact ? new Thickness(0, 0, 0, 6) : new Thickness(0, 0, 0, 10);

                if (PanelHitCount != null && PanelContent != null)
                {
                    if (_isCompactMode)
                    {
                        PanelHitCount.Margin = new Thickness(0);
                        PanelContent.Margin = new Thickness(15, 15, 15, 5);
                    }
                    else
                    {
                        PanelHitCount.Margin = new Thickness(0, 0, 0, 15);
                        PanelContent.Margin = new Thickness(15);
                    }
                }

                if (_isCompactMode && ControlPanel != null)
                {
                    ControlPanel.Height = double.NaN;
                }
                else if (!_isCompactMode && ControlPanel != null)
                {
                    ControlPanel.Height = !double.IsNaN(_panelHeight) && !double.IsInfinity(_panelHeight) && _panelHeight > 0
                        ? _panelHeight
                        : CompactPanelDefaultHeight;
                }

                SolidColorBrush themeBrush = new SolidColorBrush(_themeColor);
                SolidColorBrush defaultBorder = new SolidColorBrush(Color.FromRgb(50, 53, 64));

                if (BtnViewFull != null) {
                    BtnViewFull.BorderBrush = !effectiveCompact ? themeBrush : defaultBorder;
                    BtnViewFull.Foreground = !effectiveCompact ? themeBrush : Brushes.White;
                }
                if (BtnViewCompact != null) {
                    BtnViewCompact.BorderBrush = effectiveCompact ? themeBrush : defaultBorder;
                    BtnViewCompact.Foreground = effectiveCompact ? themeBrush : Brushes.White;
                }
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
            if (ControlPanel == null || _isAutoFittingControlPanel) return;

            if (SettingsContent != null && SettingsContent.Visibility == Visibility.Visible)
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
            EnsureCalibrationClosed();
            if (PanelContent.Visibility == Visibility.Visible || SettingsContent.Visibility == Visibility.Visible || NoticeContent.Visibility == Visibility.Visible)
            {
                _savedPanelHeight = ControlPanel.Height; // Sauvegarde la hauteur actuelle
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
                if (rememberBounds && !_suspendLayoutPersistence && ControlPanel != null)
                {
                    CaptureCurrentLayoutBounds();
                }

                string colorHex = _themeColor.ToString();
                double panelOpacity = ControlPanel?.Opacity ?? 1.0;
                string data = string.Format(System.Globalization.CultureInfo.InvariantCulture, 
                    "{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10};{11};{12};{13};{14};{15};{16};{17};{18};{19};{20};{21};{22};{23};{24};{25};{26};{27};{28};{29};{30}", 
                    _gridOffsetX, _gridOffsetY, _tileWidth, _tileHeight, 
                    panelOpacity, _langIdx, _colorblindMode, _iconIdx, colorHex, _vkStart, _vkToggle, _vkClear, _isLargeText, _selectedScreenDeviceName, _isCompactMode, _onbX, _onbY, _compactX, _compactY, _compactScale,
                    _panelX, _panelY, _panelWidth, _panelHeight, _windowX, _windowY, _windowWidth, _windowHeight, _creatorX, _creatorY, _creatorScale);
                File.WriteAllText(GridConfigPath, data);
            }
            catch { }
        }

        private void LoadGridConfig()
        {
            try
            {
                if (File.Exists(GridConfigPath))
                {
                    _hasSavedGridConfig = true;
                    var parts = File.ReadAllText(GridConfigPath).Split(';');
                    if (parts.Length >= 3)
                    {
                        _gridOffsetX = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                        _gridOffsetY = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                        _tileWidth = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                        
                        if (parts.Length >= 4)
                            _tileHeight = double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
                        else
                            _tileHeight = _tileWidth / 2; // Compatibilité avec l'ancienne sauvegarde
                    }
                    if (parts.Length >= 9)
                    {
                        double op = double.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture);
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
                }
            }
            catch { }
        }
    }
}
