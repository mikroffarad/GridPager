using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GridPager
{
    public static class WindowMover
    {
        private const int GRID_COLUMNS = 3;
        private const int GRID_ROWS = 2;
        private const int MAX_DESKTOPS = GRID_COLUMNS * GRID_ROWS; // 6 desktops max

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool FlashWindow(IntPtr hWnd, bool bInvert);

        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        // Flash flags
        private const uint FLASHW_STOP = 0;
        private const uint FLASHW_CAPTION = 1;
        private const uint FLASHW_TRAY = 2;
        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMER = 4;
        private const uint FLASHW_TIMERNOFG = 12;

        public static async void MoveActiveWindowToDesktop(GlobalHotKeyManager.HotKeyDirection direction)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"MoveActiveWindowToDesktop called with direction: {direction}");

                // Get active window
                IntPtr activeWindowHandle = GetForegroundWindow();
                if (activeWindowHandle == IntPtr.Zero || !IsWindow(activeWindowHandle))
                {
                    System.Diagnostics.Debug.WriteLine("No active window found");
                    return;
                }

                var virtualDesktops = WindowsVirtualDesktop.GetInstance();
                var manager = WindowsVirtualDesktopManager.GetInstance();

                if (virtualDesktops.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("No virtual desktops found");
                    return;
                }

                // Get current desktop index
                var currentDesktop = virtualDesktops.Current;
                int currentIndex = manager.FromDesktop(currentDesktop);

                System.Diagnostics.Debug.WriteLine($"Current desktop index: {currentIndex}, Total desktops: {virtualDesktops.Count}");
                System.Diagnostics.Debug.WriteLine($"Active window handle: {activeWindowHandle:X}");

                if (currentIndex < 0) currentIndex = 0;

                // Calculate target desktop with wrapping
                int targetIndex = CalculateTargetIndex(currentIndex, direction, virtualDesktops.Count);

                System.Diagnostics.Debug.WriteLine($"Target desktop index: {targetIndex}");

                if (targetIndex != currentIndex && targetIndex < virtualDesktops.Count)
                {
                    var targetDesktop = virtualDesktops.FromIndex(targetIndex);
                    if (targetDesktop != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Moving window from desktop {currentIndex + 1} to desktop {targetIndex + 1}");

                        // Move window to target desktop
                        bool moveResult = await MoveWindowToDesktop(activeWindowHandle, targetDesktop);

                        if (moveResult)
                        {
                            // Switch to target desktop after moving window
                            targetDesktop.MakeVisible();

                            // Small delay for synchronization
                            await Task.Delay(100);

                            // Focus window on the new desktop
                            SetForegroundWindow(activeWindowHandle);

                            // Optional attention state clearing - if needed
                            // await ClearWindowAttentionStates();

                            ShowMoveWindowFeedback(currentIndex, targetIndex, direction);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Failed to move window to target desktop");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to get desktop at index {targetIndex}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No move needed - same desktop or invalid index");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MoveActiveWindowToDesktop: {ex.Message}");
            }
        }

        private static async Task ClearWindowAttentionStates()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Clearing window attention states after window move...");

                // Enumerate all windows and clear their attention state
                EnumWindows(new EnumWindowsProc((hWnd, lParam) =>
                {
                    try
                    {
                        // Check if window is visible and has title
                        if (IsWindowVisible(hWnd) && GetWindowTextLength(hWnd) > 0)
                        {
                            // Clear attention state (orange highlighting)
                            FLASHWINFO fInfo = new FLASHWINFO();
                            fInfo.cbSize = (uint)Marshal.SizeOf(fInfo);
                            fInfo.hwnd = hWnd;
                            fInfo.dwFlags = FLASHW_STOP; // Stop flashing
                            fInfo.uCount = 0;
                            fInfo.dwTimeout = 0;

                            FlashWindowEx(ref fInfo);

                            // Additional fallback using simpler API
                            FlashWindow(hWnd, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error clearing attention state for window {hWnd:X}: {ex.Message}");
                    }

                    return true; // Continue enumeration
                }), IntPtr.Zero);

                System.Diagnostics.Debug.WriteLine("Window attention states cleared after window move");

                // Small stabilization delay
                await Task.Delay(50);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ClearWindowAttentionStates: {ex.Message}");
            }
        }

        private static async Task<bool> MoveWindowToDesktop(IntPtr windowHandle, IWindowsVirtualDesktop targetDesktop)
        {
            try
            {
                // Use Windows Virtual Desktop API to move window
                var manager = WindowsVirtualDesktopManager.GetInstance();

                // Get ApplicationView for window and move to target desktop
                // This relies on Internal API, which may have compatibility issues

                // Primary method: use WindowManager
                var windows = WindowManager.GetOpenWindows();
                var targetWindow = windows.Find(w => w.Handle == windowHandle);

                if (targetWindow != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Found window: {targetWindow.Title}");

                    // Use direct API for moving window
                    try
                    {
                        // Use MoveWindow from API
                        targetDesktop.MoveWindow(windowHandle);
                        System.Diagnostics.Debug.WriteLine("Window moved successfully using MoveWindow");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"MoveWindow failed: {ex.Message}");

                        // Fallback: try alternative method
                        return await TryAlternativeWindowMove(windowHandle, targetDesktop);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Window not found in WindowManager list");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MoveWindowToDesktop: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> TryAlternativeWindowMove(IntPtr windowHandle, IWindowsVirtualDesktop targetDesktop)
        {
            try
            {
                // Alternative window move method using COM interfaces
                // This might require additional implementation based on Windows Internal API

                System.Diagnostics.Debug.WriteLine("Trying alternative window move method");

                // For now this method returns false
                // In practice, this would implement alternative window move approach
                await Task.Delay(10);
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Alternative window move failed: {ex.Message}");
                return false;
            }
        }

        private static int CalculateTargetIndex(int currentIndex, GlobalHotKeyManager.HotKeyDirection direction, int totalDesktops)
        {
            // Use same wrapping logic as in navigation
            int currentRow = currentIndex / GRID_COLUMNS;
            int currentCol = currentIndex % GRID_COLUMNS;

            System.Diagnostics.Debug.WriteLine($"Current position: Row {currentRow}, Col {currentCol}");

            int newRow = currentRow;
            int newCol = currentCol;

            switch (direction)
            {
                case GlobalHotKeyManager.HotKeyDirection.Left:  // A key
                    newCol--;
                    if (newCol < 0)
                    {
                        newCol = GRID_COLUMNS - 1;
                        System.Diagnostics.Debug.WriteLine("Wrapping left to end of row");
                    }
                    break;

                case GlobalHotKeyManager.HotKeyDirection.Right: // D key
                    newCol++;
                    if (newCol >= GRID_COLUMNS)
                    {
                        newCol = 0;
                        System.Diagnostics.Debug.WriteLine("Wrapping right to start of row");
                    }
                    break;

                case GlobalHotKeyManager.HotKeyDirection.Up:    // W key
                    newRow--;
                    if (newRow < 0)
                    {
                        newRow = GRID_ROWS - 1;
                        System.Diagnostics.Debug.WriteLine("Wrapping up to last row");
                    }
                    break;

                case GlobalHotKeyManager.HotKeyDirection.Down:  // S key
                    newRow++;
                    if (newRow >= GRID_ROWS)
                    {
                        newRow = 0;
                        System.Diagnostics.Debug.WriteLine("Wrapping down to first row");
                    }
                    break;
            }

            int newIndex = newRow * GRID_COLUMNS + newCol;
            System.Diagnostics.Debug.WriteLine($"New position: Row {newRow}, Col {newCol}, Index {newIndex}");

            // Wrapping for cases when calculated index exceeds actual desktop count
            if (newIndex >= totalDesktops)
            {
                switch (direction)
                {
                    case GlobalHotKeyManager.HotKeyDirection.Right:
                    case GlobalHotKeyManager.HotKeyDirection.Down:
                        newIndex = 0;
                        System.Diagnostics.Debug.WriteLine("Index out of bounds, wrapping to 0");
                        break;
                    case GlobalHotKeyManager.HotKeyDirection.Left:
                    case GlobalHotKeyManager.HotKeyDirection.Up:
                        newIndex = totalDesktops - 1;
                        System.Diagnostics.Debug.WriteLine($"Index out of bounds, wrapping to {totalDesktops - 1}");
                        break;
                }
            }

            return newIndex;
        }

        private static void ShowMoveWindowFeedback(int fromIndex, int toIndex, GlobalHotKeyManager.HotKeyDirection direction)
        {
            try
            {
                string directionName;
                switch (direction)
                {
                    case GlobalHotKeyManager.HotKeyDirection.Left:
                        directionName = "Left (A)";
                        break;
                    case GlobalHotKeyManager.HotKeyDirection.Right:
                        directionName = "Right (D)";
                        break;
                    case GlobalHotKeyManager.HotKeyDirection.Up:
                        directionName = "Up (W)";
                        break;
                    case GlobalHotKeyManager.HotKeyDirection.Down:
                        directionName = "Down (S)";
                        break;
                    default:
                        directionName = direction.ToString();
                        break;
                }

                string message = $"Window moved: Desktop {fromIndex + 1} → {toIndex + 1} ({directionName})";
                System.Diagnostics.Debug.WriteLine($"Move window feedback: {message}");
            }
            catch { }
        }
    }
}