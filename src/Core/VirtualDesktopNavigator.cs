using System;
using System.Threading.Tasks;

namespace GridPager
{
    public class VirtualDesktopNavigator
    {
        private const int GRID_COLUMNS = 3;
        private const int GRID_ROWS = 2;
        private const int MAX_DESKTOPS = GRID_COLUMNS * GRID_ROWS; // 6 desktops max

        public static async void NavigateToDesktop(GlobalHotKeyManager.HotKeyDirection direction)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"NavigateToDesktop called with direction: {direction}");

                var virtualDesktops = WindowsVirtualDesktop.GetInstance();
                var manager = WindowsVirtualDesktopManager.GetInstance();

                if (virtualDesktops.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("No virtual desktops found");
                    return;
                }

                // Get current desktop and its index
                var currentDesktop = virtualDesktops.Current;
                int currentIndex = manager.FromDesktop(currentDesktop);

                System.Diagnostics.Debug.WriteLine($"Current desktop index: {currentIndex}, Total desktops: {virtualDesktops.Count}");

                if (currentIndex < 0) currentIndex = 0;

                // Calculate new index based on direction with wrapping
                int newIndex = CalculateNewIndex(currentIndex, direction, virtualDesktops.Count);

                System.Diagnostics.Debug.WriteLine($"Calculated new index: {newIndex}");

                // Switch to the new desktop
                if (newIndex != currentIndex && newIndex < virtualDesktops.Count)
                {
                    var targetDesktop = virtualDesktops.FromIndex(newIndex);
                    if (targetDesktop != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Switching from desktop {currentIndex + 1} to desktop {newIndex + 1}");
                        targetDesktop.MakeVisible();

                        // Optional: show visual navigation feedback
                        ShowNavigationFeedback(currentIndex, newIndex, direction);

                        // Small delay to ensure Virtual Desktop API completes
                        await Task.Delay(50);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to get desktop at index {newIndex}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No navigation needed - same desktop or invalid index");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in NavigateToDesktop: {ex.Message}");
            }
        }

        private static void ShowNavigationFeedback(int fromIndex, int toIndex, GlobalHotKeyManager.HotKeyDirection direction)
        {
            try
            {
                // Simple feedback showing desktop transition
                string message = $"Desktop {fromIndex + 1} → {toIndex + 1} ({direction})";
                System.Diagnostics.Debug.WriteLine($"Navigation feedback: {message}");

                // Here you could add visual feedback like a tooltip
                // or notification in the taskbar
            }
            catch { }
        }

        private static int CalculateNewIndex(int currentIndex, GlobalHotKeyManager.HotKeyDirection direction, int totalDesktops)
        {
            // Convert linear index to grid coordinates
            int currentRow = currentIndex / GRID_COLUMNS;
            int currentCol = currentIndex % GRID_COLUMNS;

            System.Diagnostics.Debug.WriteLine($"Current position: Row {currentRow}, Col {currentCol}");

            int newRow = currentRow;
            int newCol = currentCol;

            switch (direction)
            {
                case GlobalHotKeyManager.HotKeyDirection.Left:
                    newCol--;
                    if (newCol < 0)
                    {
                        // Wrapping: move to end of row
                        newCol = GRID_COLUMNS - 1;
                        System.Diagnostics.Debug.WriteLine("Wrapping left to end of row");
                    }
                    break;

                case GlobalHotKeyManager.HotKeyDirection.Right:
                    newCol++;
                    if (newCol >= GRID_COLUMNS)
                    {
                        // Wrapping: move to beginning of row
                        newCol = 0;
                        System.Diagnostics.Debug.WriteLine("Wrapping right to start of row");
                    }
                    break;

                case GlobalHotKeyManager.HotKeyDirection.Up:
                    newRow--;
                    if (newRow < 0)
                    {
                        // Wrapping: move to last row
                        newRow = GRID_ROWS - 1;
                        System.Diagnostics.Debug.WriteLine("Wrapping up to last row");
                    }
                    break;

                case GlobalHotKeyManager.HotKeyDirection.Down:
                    newRow++;
                    if (newRow >= GRID_ROWS)
                    {
                        // Wrapping: move to first row
                        newRow = 0;
                        System.Diagnostics.Debug.WriteLine("Wrapping down to first row");
                    }
                    break;
            }

            // Convert grid coordinates back to linear index
            int newIndex = newRow * GRID_COLUMNS + newCol;

            System.Diagnostics.Debug.WriteLine($"New position: Row {newRow}, Col {newCol}, Index {newIndex}");

            // Ensure index is within bounds of actual desktops
            if (newIndex >= totalDesktops)
            {
                // If index exceeds actual desktop count,
                // perform wrapping based on direction
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

        public static string GetNavigationInfo(int currentIndex, int totalDesktops)
        {
            if (totalDesktops == 0) return "No desktops available";

            int currentRow = currentIndex / GRID_COLUMNS;
            int currentCol = currentIndex % GRID_COLUMNS;

            return $"Desktop {currentIndex + 1} (Row {currentRow + 1}, Col {currentCol + 1}) of {totalDesktops}";
        }
    }
}