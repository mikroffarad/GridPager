using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GridPager
{
    /// <summary>
    /// Utility for managing window attention states (orange highlighting on taskbar)
    /// Called only when needed, not automatically
    /// </summary>
    public static class WindowAttentionManager
    {
        [DllImport("user32.dll")]
        private static extern bool FlashWindow(IntPtr hWnd, bool bInvert);

        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

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

        /// <summary>
        /// Clears attention state for all windows
        /// WARNING: Use only in extreme cases!
        /// </summary>
        public static async Task ClearAllWindowAttentionStates()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Manual clearing of window attention states...");

                int clearedCount = 0;

                // Enumerate all windows and clear their attention state
                EnumWindows(new EnumWindowsProc((hWnd, lParam) =>
                {
                    try
                    {
                        // Check if window is visible and has a title
                        if (IsWindowVisible(hWnd) && GetWindowTextLength(hWnd) > 0)
                        {
                            // Get title for debug purposes
                            var titleBuilder = new System.Text.StringBuilder(256);
                            GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                            string title = titleBuilder.ToString();

                            // Clear attention state only if window is not active
                            IntPtr foregroundWindow = GetForegroundWindow();
                            if (hWnd != foregroundWindow)
                            {
                                // Clear attention state (orange highlighting)
                                FLASHWINFO fInfo = new FLASHWINFO();
                                fInfo.cbSize = (uint)Marshal.SizeOf(fInfo);
                                fInfo.hwnd = hWnd;
                                fInfo.dwFlags = FLASHW_STOP; // Stop flashing
                                fInfo.uCount = 0;
                                fInfo.dwTimeout = 0;

                                if (FlashWindowEx(ref fInfo))
                                {
                                    clearedCount++;
                                    System.Diagnostics.Debug.WriteLine($"Cleared attention state for: {title} ({hWnd:X})");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error clearing attention state for window {hWnd:X}: {ex.Message}");
                    }

                    return true; // Continue enumeration
                }), IntPtr.Zero);

                System.Diagnostics.Debug.WriteLine($"Manual attention state clearing completed: {clearedCount} windows processed");

                // Small delay for stabilization
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ClearAllWindowAttentionStates: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears attention state for a specific window
        /// </summary>
        public static bool ClearWindowAttentionState(IntPtr windowHandle)
        {
            try
            {
                if (windowHandle == IntPtr.Zero || !IsWindowVisible(windowHandle))
                    return false;

                // Check if this is not the active window
                IntPtr foregroundWindow = GetForegroundWindow();
                if (windowHandle == foregroundWindow)
                    return false; // Don't clear attention state for active window

                FLASHWINFO fInfo = new FLASHWINFO();
                fInfo.cbSize = (uint)Marshal.SizeOf(fInfo);
                fInfo.hwnd = windowHandle;
                fInfo.dwFlags = FLASHW_STOP;
                fInfo.uCount = 0;
                fInfo.dwTimeout = 0;

                bool result = FlashWindowEx(ref fInfo);
                if (result)
                {
                    System.Diagnostics.Debug.WriteLine($"Cleared attention state for window {windowHandle:X}");
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing attention state for window {windowHandle:X}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if window has attention state
        /// </summary>
        public static bool HasAttentionState(IntPtr windowHandle)
        {
            try
            {
                // This is difficult to check directly through Windows API
                // For now, return false
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}