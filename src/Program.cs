using System;
using System.Threading;
using System.Windows.Forms;

namespace GridPager
{
    static class Program
    {
        public static CancellationTokenSource ApplicationClosingSource = new CancellationTokenSource();
        public static CancellationToken ApplicationClosing => ApplicationClosingSource.Token;
        public static WindowsVersion WindowsVersion = new WindowsVersion();

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GridPagerApplicationContext());
        }
    }

    public class GridPagerApplicationContext : ApplicationContext
    {
        private NotifyIcon _trayIcon;
        private TaskbarToolbar _taskbarToolbar;
        private ToolbarSettings _settings;
        private GlobalHotKeyManager _hotKeyManager;
        private HiddenWindow _hiddenWindow; // For handling hotkey messages

        public GridPagerApplicationContext()
        {
            _settings = ToolbarSettings.Load();

            // Create hidden window for hotkey message handling
            _hiddenWindow = new HiddenWindow();

            SetupGlobalHotKeys();
            InitializeTrayIcon();
            InitializeTaskbarToolbar();
        }

        private void SetupGlobalHotKeys()
        {
            try
            {
                _hotKeyManager = new GlobalHotKeyManager(_hiddenWindow.Handle);
                _hotKeyManager.HotKeyPressed += OnHotKeyPressed;
                _hotKeyManager.MoveWindowHotKeyPressed += OnMoveWindowHotKeyPressed;

                // Connect HiddenWindow to HotKeyManager for message processing
                _hiddenWindow.SetHotKeyManager(_hotKeyManager);

                if (_hotKeyManager.RegisterHotKeys())
                {
                    System.Diagnostics.Debug.WriteLine("Global hotkeys registered successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Failed to register global hotkeys");
                    // Can show message to user
                    MessageBox.Show("Warning: Global hotkeys could not be registered.\n\n" +
                                   "Navigation: Ctrl+Alt+Arrows\n" +
                                   "Move Window: Ctrl+Alt+WASD\n\n" +
                                   "They might be used by another application.",
                                   "GridPager - Hotkey Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up hotkeys: {ex.Message}");
            }
        }

        private async void OnHotKeyPressed(GlobalHotKeyManager.HotKeyDirection direction)
        {
            try
            {
                // Navigate between virtual desktops
                VirtualDesktopNavigator.NavigateToDesktop(direction);

                // Small delay to allow Virtual Desktop API to fully update
                await System.Threading.Tasks.Task.Delay(100);

                // Immediately update toolbar after switching
                _taskbarToolbar?.ForceUpdate();

                // Soft attention state clearing after longer delay (optional)
                await System.Threading.Tasks.Task.Delay(500);
                try
                {
                    await WindowAttentionManager.ClearAllWindowAttentionStates();
                    System.Diagnostics.Debug.WriteLine("Soft attention state clearing after navigation");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Soft attention clearing failed: {ex.Message}");
                }

                // Debug message for verification
                System.Diagnostics.Debug.WriteLine($"Navigation hotkey pressed: {direction} - toolbar updated");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in navigation hotkey: {ex.Message}");
            }
        }

        private async void OnMoveWindowHotKeyPressed(GlobalHotKeyManager.HotKeyDirection direction)
        {
            try
            {
                // Move active window to another virtual desktop
                WindowMover.MoveActiveWindowToDesktop(direction);

                // Small delay for synchronization
                await System.Threading.Tasks.Task.Delay(150);

                // Update toolbar after moving window
                _taskbarToolbar?.ForceUpdate();

                // Debug message for verification
                System.Diagnostics.Debug.WriteLine($"Move window hotkey pressed: {direction} - toolbar updated");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in move window hotkey: {ex.Message}");
            }
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon(new System.IO.MemoryStream(Helpers.GetResourceFromAssembly(typeof(Program), "GridPager.Resources.icon.ico"))),
                Text = "GridPager - Grid Virtual Desktop Navigator",
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();

            var showHideItem = new ToolStripMenuItem(_settings.IsVisible ? "Hide Toolbar" : "Show Toolbar");
            showHideItem.Click += (s, e) => ToggleToolbar();
            contextMenu.Items.Add(showHideItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // Add hotkey information
            var hotkeysItem = new ToolStripMenuItem("Navigation: Ctrl+Alt+Arrows");
            hotkeysItem.Enabled = false;
            contextMenu.Items.Add(hotkeysItem);

            var moveWindowItem = new ToolStripMenuItem("Move Window: Ctrl+Alt+WASD");
            moveWindowItem.Enabled = false;
            contextMenu.Items.Add(moveWindowItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // Clear attention states option
            var clearAttentionItem = new ToolStripMenuItem("Clear Orange Highlighting");
            clearAttentionItem.Click += async (sender, args) => {
                try
                {
                    await WindowAttentionManager.ClearAllWindowAttentionStates();
                    // Don't show MessageBox from system tray for better UX
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error clearing attention states: {ex.Message}");
                }
            };
            contextMenu.Items.Add(clearAttentionItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var aboutItem = new ToolStripMenuItem("About");
            aboutItem.Click += (s, e) => ShowAbout();
            contextMenu.Items.Add(aboutItem);

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitItem);

            _trayIcon.ContextMenuStrip = contextMenu;
            _trayIcon.DoubleClick += (s, e) => ToggleToolbar();
        }

        private void InitializeTaskbarToolbar()
        {
            if (_settings.IsVisible)
            {
                _taskbarToolbar = new TaskbarToolbar(_settings);
                _taskbarToolbar.Show();
            }
        }

        private void ToggleToolbar()
        {
            if (_taskbarToolbar == null)
            {
                _settings.IsVisible = true;
                _taskbarToolbar = new TaskbarToolbar(_settings);
                _taskbarToolbar.Show();
            }
            else
            {
                _settings.IsVisible = false;
                _settings.Save();
                _taskbarToolbar.Hide();
                _taskbarToolbar.Dispose();
                _taskbarToolbar = null;
            }
            UpdateTrayMenu();
        }

        private void UpdateTrayMenu()
        {
            var showHideItem = _trayIcon.ContextMenuStrip.Items[0] as ToolStripMenuItem;
            if (showHideItem != null)
            {
                showHideItem.Text = _settings.IsVisible ? "Hide Toolbar" : "Show Toolbar";
            }
        }

        private void ShowAbout()
        {
            var virtualDesktops = WindowsVirtualDesktop.GetInstance();
            var desktopCount = 0;
            try { desktopCount = virtualDesktops.Count; } catch { }

            MessageBox.Show($"GridPager - Grid Virtual Desktop Navigator v1.3.2{Environment.NewLine}{Environment.NewLine}" +
                           $"Compact Layout: 3×2 grid (max 6 desktops){Environment.NewLine}" +
                           $"Current Desktops: {desktopCount}{Environment.NewLine}" +
                           $"Navigation: Ctrl+Alt+Arrow Keys{Environment.NewLine}" +
                           $"Move Window: Ctrl+Alt+WASD{Environment.NewLine}" +
                           $"Grid navigation with wrapping{Environment.NewLine}" +
                           $"Manual fix: Clear Orange Highlighting{Environment.NewLine}{Environment.NewLine}" +
                           $"Based on Switchie by darkguy2008{Environment.NewLine}" +
                           $"GridPager fork with enhanced grid navigation", "About GridPager");
        }

        private void ExitApplication()
        {
            Program.ApplicationClosingSource.Cancel();

            // Disable hotkeys
            _hotKeyManager?.UnregisterHotKeys();
            _hotKeyManager?.Dispose();

            if (_taskbarToolbar != null)
            {
                _settings.IsVisible = _taskbarToolbar != null;
                _settings.Save();
            }

            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _taskbarToolbar?.Hide();
            _taskbarToolbar?.Dispose();
            _hiddenWindow?.DestroyHandle();

            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hotKeyManager?.Dispose();
                _trayIcon?.Dispose();
                _taskbarToolbar?.Dispose();
                _hiddenWindow?.DestroyHandle();
            }
            base.Dispose(disposing);
        }
    }

    // Hidden window for hotkey message handling
    public class HiddenWindow : NativeWindow
    {
        private GlobalHotKeyManager _hotKeyManager;

        public HiddenWindow()
        {
            // Create invisible window
            CreateParams cp = new CreateParams();
            cp.Caption = "GridPagerHiddenWindow";
            cp.X = cp.Y = cp.Width = cp.Height = 0;
            CreateHandle(cp);
        }

        public void SetHotKeyManager(GlobalHotKeyManager hotKeyManager)
        {
            _hotKeyManager = hotKeyManager;
        }

        protected override void WndProc(ref Message m)
        {
            // Process hotkey messages
            if (_hotKeyManager?.ProcessHotKey(m) == true)
            {
                return; // Message processed
            }

            base.WndProc(ref m);
        }

        protected override void OnHandleChange()
        {
            base.OnHandleChange();
            if (Handle != IntPtr.Zero)
            {
                // Handle created, can register hotkeys
                System.Diagnostics.Debug.WriteLine($"GridPager hidden window handle created: {Handle:X}");
            }
        }
    }
}
