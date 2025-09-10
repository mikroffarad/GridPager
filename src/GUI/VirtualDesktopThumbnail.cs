using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GridPager
{
    public class VirtualDesktopThumbnail : UserControl
    {
        private int _desktopIndex;
        private bool _isActive;
        private ToolTip _tooltip;
        private bool _isHovered = false;
        private List<Window> _windows = new List<Window>();
        private Timer _refreshTimer;

        public int DesktopIndex
        {
            get { return _desktopIndex; }
            set
            {
                _desktopIndex = value;
                UpdateTooltip();
                RefreshContent();
                Invalidate();
            }
        }

        public bool IsActiveDesktop
        {
            get { return _isActive; }
            set
            {
                _isActive = value;
                Invalidate();
            }
        }

        public VirtualDesktopThumbnail(int desktopIndex)
        {
            _desktopIndex = desktopIndex;

            _tooltip = new ToolTip();
            UpdateTooltip();

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            // Refresh timer for window updates
            _refreshTimer = new Timer();
            _refreshTimer.Interval = 2000; // Refresh every 2 seconds
            _refreshTimer.Tick += (s, e) => RefreshContent();
            _refreshTimer.Start();
        }

        private void UpdateTooltip()
        {
            var windowCount = _windows.Count;
            var tooltip = $"Desktop {_desktopIndex + 1}";
            if (windowCount > 0)
            {
                tooltip += $" ({windowCount} window{(windowCount != 1 ? "s" : "")})";
                var topWindows = _windows.Take(3).Select(w => w.Title).ToArray();
                tooltip += $"\n{string.Join(", ", topWindows)}";
                if (windowCount > 3) tooltip += "...";
            }
            else
            {
                tooltip += " (empty)";
            }
            _tooltip.SetToolTip(this, tooltip);
        }

        public void RefreshContent()
        {
            Task.Run(() =>
            {
                try
                {
                    var newWindows = GetWindowsForDesktop(_desktopIndex);

                    if (!IsDisposed)
                    {
                        Invoke(new Action(() =>
                        {
                            _windows = newWindows;
                            UpdateTooltip();
                            Invalidate();
                        }));
                    }
                }
                catch
                {
                    // Ignore errors during refresh
                }
            });
        }

        private List<Window> GetWindowsForDesktop(int desktopIndex)
        {
            try
            {
                var virtualDesktops = WindowsVirtualDesktop.GetInstance();
                if (desktopIndex >= virtualDesktops.Count) return new List<Window>();

                // Get all windows from WindowManager and filter by desktop
                var allWindows = WindowManager.GetOpenWindows();
                var desktopWindows = allWindows
                    .Where(w => w.VirtualDesktopIndex == desktopIndex)
                    .OrderByDescending(w => w.IsActive)  // Active window first
                    .ThenBy(w => w.ZOrder)               // Then by Z-order
                    .Take(6)                             // Limit to 6 windows max for compact view
                    .ToList();

                return desktopWindows;
            }
            catch
            {
                return new List<Window>();
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);

            try
            {
                // Switch to this virtual desktop
                var virtualDesktops = WindowsVirtualDesktop.GetInstance();
                if (_desktopIndex < virtualDesktops.Count)
                {
                    var desktop = virtualDesktops.FromIndex(_desktopIndex);
                    if (desktop != null)
                    {
                        desktop.MakeVisible();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to switch to desktop {_desktopIndex + 1}: {ex.Message}", "GridPager",
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Background colors
            Color backgroundColor;
            Color borderColor;

            if (_isActive)
            {
                backgroundColor = Color.FromArgb(0, 120, 215); // Windows accent blue
                borderColor = Color.FromArgb(60, 160, 255);
            }
            else if (_isHovered)
            {
                backgroundColor = Color.FromArgb(60, 60, 60); // Hover gray
                borderColor = Color.FromArgb(100, 100, 100);
            }
            else
            {
                backgroundColor = Color.FromArgb(35, 35, 35); // Normal dark
                borderColor = Color.FromArgb(55, 55, 55);
            }

            // Fill background
            using (var brush = new SolidBrush(backgroundColor))
            {
                g.FillRectangle(brush, 0, 0, Width, Height);
            }

            // Draw border
            using (var pen = new Pen(borderColor, 1))
            {
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }

            // Draw window icons only (no text, no numbers)
            if (_windows.Count > 0)
            {
                DrawWindowIcons(g);
            }

            // Draw subtle gradient effect for active state
            if (_isActive)
            {
                using (var gradientBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 0, Width, Height),
                    Color.FromArgb(20, Color.White),
                    Color.FromArgb(5, Color.White),
                    System.Drawing.Drawing2D.LinearGradientMode.Vertical))
                {
                    g.FillRectangle(gradientBrush, 1, 1, Width - 2, Height - 2);
                }
            }
        }

        private void DrawWindowIcons(Graphics g)
        {
            const int iconSize = 10;  // Smaller icons for compact view
            const int iconSpacing = 1; // Minimal spacing
            const int iconsPerRow = 3; // 3 icons per row for compact 48px width
            const int startX = 2;
            const int startY = 2;

            for (int i = 0; i < Math.Min(_windows.Count, 6); i++) // Max 6 icons (3x2)
            {
                var window = _windows[i];
                if (window.Icon == null) continue;

                int row = i / iconsPerRow;
                int col = i % iconsPerRow;

                int x = startX + (col * (iconSize + iconSpacing));
                int y = startY + (row * (iconSize + iconSpacing));

                // Ensure we don't draw outside bounds
                if (x + iconSize > Width - 2 || y + iconSize > Height - 2) break;

                try
                {
                    // Draw minimal background for icon
                    using (var iconBg = new SolidBrush(Color.FromArgb(15, Color.White)))
                    {
                        g.FillRectangle(iconBg, x - 1, y - 1, iconSize + 2, iconSize + 2);
                    }

                    // Draw thin glow effect for active window
                    if (window.IsActive)
                    {
                        using (var glowPen = new Pen(Color.FromArgb(120, Color.Yellow), 1))
                        {
                            g.DrawRectangle(glowPen, x - 1, y - 1, iconSize + 1, iconSize + 1);
                        }
                    }

                    // Draw the window icon
                    g.DrawImage(window.Icon, new Rectangle(x, y, iconSize, iconSize));
                }
                catch
                {
                    // Draw minimal placeholder if icon fails
                    using (var brush = new SolidBrush(Color.FromArgb(80, Color.White)))
                    {
                        g.FillRectangle(brush, x, y, iconSize, iconSize);
                    }
                }
            }

            // Draw minimal "..." indicator if there are more windows
            if (_windows.Count > 6)
            {
                using (var font = new Font("Segoe UI", 5, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.White))
                {
                    g.DrawString("...", font, brush, Width - 10, Height - 8);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshTimer?.Stop();
                _refreshTimer?.Dispose();
                _tooltip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}