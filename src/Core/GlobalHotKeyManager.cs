using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GridPager
{
    public class GlobalHotKeyManager : IDisposable
    {
        // Windows API for registering global hotkeys
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        // Modifier keys
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_WIN = 0x0008;

        // Virtual key codes
        private const int VK_LEFT = 0x25;
        private const int VK_UP = 0x26;
        private const int VK_RIGHT = 0x27;
        private const int VK_DOWN = 0x28;
        private const int VK_A = 0x41;  // A key
        private const int VK_W = 0x57;  // W key  
        private const int VK_S = 0x53;  // S key
        private const int VK_D = 0x44;  // D key

        // IDs for navigation hotkeys
        private const int HOTKEY_LEFT = 1001;
        private const int HOTKEY_RIGHT = 1002;
        private const int HOTKEY_UP = 1003;
        private const int HOTKEY_DOWN = 1004;

        // IDs for window move hotkeys
        private const int HOTKEY_MOVE_LEFT = 1005;   // Ctrl+Alt+A
        private const int HOTKEY_MOVE_RIGHT = 1006;  // Ctrl+Alt+D
        private const int HOTKEY_MOVE_UP = 1007;     // Ctrl+Alt+W
        private const int HOTKEY_MOVE_DOWN = 1008;   // Ctrl+Alt+S

        // Windows Message for hotkeys
        private const int WM_HOTKEY = 0x0312;

        private IntPtr _windowHandle;
        private bool _hotKeysRegistered = false;

        public delegate void HotKeyPressedEventHandler(HotKeyDirection direction);
        public delegate void MoveWindowHotKeyPressedEventHandler(HotKeyDirection direction);

        public event HotKeyPressedEventHandler HotKeyPressed;
        public event MoveWindowHotKeyPressedEventHandler MoveWindowHotKeyPressed;

        public enum HotKeyDirection
        {
            Left,
            Right,
            Up,
            Down
        }

        public GlobalHotKeyManager(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
        }

        public bool RegisterHotKeys()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Attempting to register hotkeys for window handle: {_windowHandle:X}");

                // Register Ctrl+Alt+Arrows (navigation between desktops)
                bool leftSuccess = RegisterHotKey(_windowHandle, HOTKEY_LEFT, MOD_CONTROL | MOD_ALT, VK_LEFT);
                System.Diagnostics.Debug.WriteLine($"Register Left: {leftSuccess}, Error: {GetLastError()}");

                bool rightSuccess = RegisterHotKey(_windowHandle, HOTKEY_RIGHT, MOD_CONTROL | MOD_ALT, VK_RIGHT);
                System.Diagnostics.Debug.WriteLine($"Register Right: {rightSuccess}, Error: {GetLastError()}");

                bool upSuccess = RegisterHotKey(_windowHandle, HOTKEY_UP, MOD_CONTROL | MOD_ALT, VK_UP);
                System.Diagnostics.Debug.WriteLine($"Register Up: {upSuccess}, Error: {GetLastError()}");

                bool downSuccess = RegisterHotKey(_windowHandle, HOTKEY_DOWN, MOD_CONTROL | MOD_ALT, VK_DOWN);
                System.Diagnostics.Debug.WriteLine($"Register Down: {downSuccess}, Error: {GetLastError()}");

                // Register Ctrl+Alt+WASD (moving active window)
                bool moveLeftSuccess = RegisterHotKey(_windowHandle, HOTKEY_MOVE_LEFT, MOD_CONTROL | MOD_ALT, VK_A);
                System.Diagnostics.Debug.WriteLine($"Register Move Left (A): {moveLeftSuccess}, Error: {GetLastError()}");

                bool moveRightSuccess = RegisterHotKey(_windowHandle, HOTKEY_MOVE_RIGHT, MOD_CONTROL | MOD_ALT, VK_D);
                System.Diagnostics.Debug.WriteLine($"Register Move Right (D): {moveRightSuccess}, Error: {GetLastError()}");

                bool moveUpSuccess = RegisterHotKey(_windowHandle, HOTKEY_MOVE_UP, MOD_CONTROL | MOD_ALT, VK_W);
                System.Diagnostics.Debug.WriteLine($"Register Move Up (W): {moveUpSuccess}, Error: {GetLastError()}");

                bool moveDownSuccess = RegisterHotKey(_windowHandle, HOTKEY_MOVE_DOWN, MOD_CONTROL | MOD_ALT, VK_S);
                System.Diagnostics.Debug.WriteLine($"Register Move Down (S): {moveDownSuccess}, Error: {GetLastError()}");

                bool success = leftSuccess && rightSuccess && upSuccess && downSuccess &&
                              moveLeftSuccess && moveRightSuccess && moveUpSuccess && moveDownSuccess;
                _hotKeysRegistered = success;

                System.Diagnostics.Debug.WriteLine($"Overall hotkey registration: {success}");
                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception during hotkey registration: {ex.Message}");
                return false;
            }
        }

        public void UnregisterHotKeys()
        {
            if (_hotKeysRegistered)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Unregistering hotkeys...");

                    // Unregister navigation hotkeys
                    UnregisterHotKey(_windowHandle, HOTKEY_LEFT);
                    UnregisterHotKey(_windowHandle, HOTKEY_RIGHT);
                    UnregisterHotKey(_windowHandle, HOTKEY_UP);
                    UnregisterHotKey(_windowHandle, HOTKEY_DOWN);

                    // Unregister window move hotkeys
                    UnregisterHotKey(_windowHandle, HOTKEY_MOVE_LEFT);
                    UnregisterHotKey(_windowHandle, HOTKEY_MOVE_RIGHT);
                    UnregisterHotKey(_windowHandle, HOTKEY_MOVE_UP);
                    UnregisterHotKey(_windowHandle, HOTKEY_MOVE_DOWN);

                    _hotKeysRegistered = false;
                    System.Diagnostics.Debug.WriteLine("Hotkeys unregistered successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error unregistering hotkeys: {ex.Message}");
                }
            }
        }

        public bool ProcessHotKey(Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                System.Diagnostics.Debug.WriteLine($"Received hotkey message: ID={id}");

                switch (id)
                {
                    // Navigation hotkeys (Ctrl+Alt+Arrows)
                    case HOTKEY_LEFT:
                        System.Diagnostics.Debug.WriteLine("Processing LEFT hotkey");
                        HotKeyPressed?.Invoke(HotKeyDirection.Left);
                        return true;
                    case HOTKEY_RIGHT:
                        System.Diagnostics.Debug.WriteLine("Processing RIGHT hotkey");
                        HotKeyPressed?.Invoke(HotKeyDirection.Right);
                        return true;
                    case HOTKEY_UP:
                        System.Diagnostics.Debug.WriteLine("Processing UP hotkey");
                        HotKeyPressed?.Invoke(HotKeyDirection.Up);
                        return true;
                    case HOTKEY_DOWN:
                        System.Diagnostics.Debug.WriteLine("Processing DOWN hotkey");
                        HotKeyPressed?.Invoke(HotKeyDirection.Down);
                        return true;

                    // Window move hotkeys (Ctrl+Alt+WASD)
                    case HOTKEY_MOVE_LEFT:
                        System.Diagnostics.Debug.WriteLine("Processing MOVE LEFT (A) hotkey");
                        MoveWindowHotKeyPressed?.Invoke(HotKeyDirection.Left);
                        return true;
                    case HOTKEY_MOVE_RIGHT:
                        System.Diagnostics.Debug.WriteLine("Processing MOVE RIGHT (D) hotkey");
                        MoveWindowHotKeyPressed?.Invoke(HotKeyDirection.Right);
                        return true;
                    case HOTKEY_MOVE_UP:
                        System.Diagnostics.Debug.WriteLine("Processing MOVE UP (W) hotkey");
                        MoveWindowHotKeyPressed?.Invoke(HotKeyDirection.Up);
                        return true;
                    case HOTKEY_MOVE_DOWN:
                        System.Diagnostics.Debug.WriteLine("Processing MOVE DOWN (S) hotkey");
                        MoveWindowHotKeyPressed?.Invoke(HotKeyDirection.Down);
                        return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            UnregisterHotKeys();
        }
    }
}