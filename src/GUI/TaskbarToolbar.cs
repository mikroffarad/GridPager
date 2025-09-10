using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;

namespace GridPager
{
    public class TaskbarToolbar : IDisposable
    {
        // Windows API constants for working with taskbar
        private const string TASKBAR_CLASS = "Shell_TrayWnd";
        private const string TRAY_NOTIFY_CLASS = "TrayNotifyWnd";
        private const string REBAR_CLASS = "ReBarWindow32";
        private const string TOOLBAR_CLASS = "ToolbarWindow32";

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int WS_CHILD = 0x40000000;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private MainForm _mainForm;
        private ToolbarSettings _settings;
        private IntPtr _taskbarHandle;
        private IntPtr _trayNotifyHandle;
        private Timer _positionTimer;
        private bool _isEmbedded = false;

        public TaskbarToolbar(ToolbarSettings settings)
        {
            _settings = settings;
            InitializeToolbar();
            TryEmbedInTaskbar();
        }

        public void ForceUpdate()
        {
            try
            {
                // Force update MainForm to immediately refresh active desktop state
                if (_mainForm != null && !_mainForm.IsDisposed)
                {
                    if (_mainForm.InvokeRequired)
                    {
                        _mainForm.BeginInvoke(new Action(() => _mainForm.ForceUpdateActiveDesktop()));
                    }
                    else
                    {
                        _mainForm.ForceUpdateActiveDesktop();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ForceUpdate: {ex.Message}");
            }
        }

        private void InitializeToolbar()
        {
            _mainForm = new MainForm(_settings);
            _mainForm.FormBorderStyle = FormBorderStyle.None;
            _mainForm.ShowInTaskbar = false;
            _mainForm.TopMost = false;
            _mainForm.BackColor = Color.FromArgb(40, 40, 40); // Darker for better integration

            // Set window styles for embedding
            int style = GetWindowLong(_mainForm.Handle, GWL_STYLE);
            style |= WS_CHILD;
            SetWindowLong(_mainForm.Handle, GWL_STYLE, style);

            int exStyle = GetWindowLong(_mainForm.Handle, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            SetWindowLong(_mainForm.Handle, GWL_EXSTYLE, exStyle);
        }

        private void TryEmbedInTaskbar()
        {
            try
            {
                // Find taskbar components
                _taskbarHandle = FindWindow(TASKBAR_CLASS, null);
                _trayNotifyHandle = FindWindowEx(_taskbarHandle, IntPtr.Zero, TRAY_NOTIFY_CLASS, null);

                if (_taskbarHandle != IntPtr.Zero && _trayNotifyHandle != IntPtr.Zero)
                {
                    // Try to embed in taskbar
                    SetParent(_mainForm.Handle, _taskbarHandle);
                    _isEmbedded = true;

                    // Start position monitoring
                    _positionTimer = new Timer();
                    _positionTimer.Interval = 250; // Check every 250ms
                    _positionTimer.Tick += UpdatePosition;
                    _positionTimer.Start();

                    UpdatePosition(null, null);
                }
                else
                {
                    // Fallback to floating toolbar near taskbar
                    FallbackToFloatingToolbar();
                }
            }
            catch
            {
                // If embedding fails, use floating approach
                FallbackToFloatingToolbar();
            }
        }

        private void FallbackToFloatingToolbar()
        {
            _isEmbedded = false;

            // Remove child style
            int style = GetWindowLong(_mainForm.Handle, GWL_STYLE);
            style &= ~WS_CHILD;
            SetWindowLong(_mainForm.Handle, GWL_STYLE, style);

            // Set as normal topmost window
            _mainForm.TopMost = true;
            _mainForm.ShowInTaskbar = false;

            // Position near system tray
            PositionNearSystemTray();

            // Monitor position changes
            _positionTimer = new Timer();
            _positionTimer.Interval = 500;
            _positionTimer.Tick += (s, e) => PositionNearSystemTray();
            _positionTimer.Start();
        }

        private void PositionNearSystemTray()
        {
            try
            {
                var taskbarHandle = FindWindow(TASKBAR_CLASS, null);
                var trayHandle = FindWindowEx(taskbarHandle, IntPtr.Zero, TRAY_NOTIFY_CLASS, null);

                if (taskbarHandle != IntPtr.Zero && trayHandle != IntPtr.Zero)
                {
                    GetWindowRect(taskbarHandle, out RECT taskbarRect);
                    GetWindowRect(trayHandle, out RECT trayRect);

                    // Position to the left of system tray
                    int x = trayRect.Left - _mainForm.Width - 10;
                    int y = taskbarRect.Top + (taskbarRect.Bottom - taskbarRect.Top - _mainForm.Height) / 2;

                    // Ensure it's within screen bounds
                    var screen = Screen.PrimaryScreen;
                    if (x < screen.WorkingArea.Left) x = screen.WorkingArea.Left + 10;
                    if (y < screen.WorkingArea.Top) y = screen.WorkingArea.Top;

                    _mainForm.Location = new Point(x, y);
                }
            }
            catch
            {
                // Fallback to bottom-right corner
                var screen = Screen.PrimaryScreen;
                _mainForm.Location = new Point(
                    screen.WorkingArea.Right - _mainForm.Width - 10,
                    screen.WorkingArea.Bottom - _mainForm.Height - 10
                );
            }
        }

        private void UpdatePosition(object sender, EventArgs e)
        {
            if (!_isEmbedded || _taskbarHandle == IntPtr.Zero || _trayNotifyHandle == IntPtr.Zero)
                return;

            try
            {
                // Get current taskbar and tray positions
                GetWindowRect(_taskbarHandle, out RECT taskbarRect);
                GetWindowRect(_trayNotifyHandle, out RECT trayRect);

                // Calculate position - place to the left of system tray
                int toolbarWidth = _mainForm.Width;
                int toolbarHeight = _mainForm.Height;

                // Position relative to taskbar
                int x = (trayRect.Left - taskbarRect.Left) - toolbarWidth - 5; // 5px margin
                int y = (taskbarRect.Bottom - taskbarRect.Top - toolbarHeight) / 2; // Center vertically

                // Ensure we don't go off the left edge
                if (x < 10) x = 10;

                SetWindowPos(_mainForm.Handle, IntPtr.Zero, x, y, toolbarWidth, toolbarHeight,
                           SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

                // Make sure it's visible
                ShowWindow(_mainForm.Handle, SW_SHOW);
            }
            catch
            {
                // If positioning fails, fall back to floating mode
                FallbackToFloatingToolbar();
            }
        }

        public void Show()
        {
            _mainForm?.Show();
            _positionTimer?.Start();
        }

        public void Hide()
        {
            _positionTimer?.Stop();
            _mainForm?.Hide();
        }

        public void Dispose()
        {
            _positionTimer?.Stop();
            _positionTimer?.Dispose();

            if (_mainForm != null)
            {
                // Restore normal window before disposing
                if (_mainForm.Handle != IntPtr.Zero && _isEmbedded)
                {
                    try
                    {
                        SetParent(_mainForm.Handle, IntPtr.Zero);
                        int style = GetWindowLong(_mainForm.Handle, GWL_STYLE);
                        style &= ~WS_CHILD;
                        SetWindowLong(_mainForm.Handle, GWL_STYLE, style);
                    }
                    catch { }
                }
                _mainForm.Dispose();
            }
        }
    }
}