using System;
using System.Drawing;
using System.Windows.Forms;

namespace GridPager
{
    public class VirtualDesktopButton : UserControl
    {
        private int _desktopIndex;
        private bool _isActive;
        private ToolTip _tooltip;
        private bool _isHovered = false;

        public int DesktopIndex
        {
            get { return _desktopIndex; }
            set
            {
                _desktopIndex = value;
                UpdateTooltip();
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

        public VirtualDesktopButton(int desktopIndex)
        {
            _desktopIndex = desktopIndex;

            _tooltip = new ToolTip();
            UpdateTooltip();

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        private void UpdateTooltip()
        {
            _tooltip.SetToolTip(this, $"Virtual Desktop {_desktopIndex + 1}");
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);

            try
            {
                // Switch to this virtual desktop using the FromIndex method
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
            Color textColor = Color.White;

            if (_isActive)
            {
                backgroundColor = Color.FromArgb(0, 120, 215); // Windows 10/11 accent blue
                borderColor = Color.FromArgb(60, 160, 255);
                textColor = Color.White;
            }
            else if (_isHovered)
            {
                backgroundColor = Color.FromArgb(70, 70, 70); // Hover gray
                borderColor = Color.FromArgb(100, 100, 100);
            }
            else
            {
                backgroundColor = Color.FromArgb(45, 45, 45); // Normal dark
                borderColor = Color.FromArgb(65, 65, 65);
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

            // Draw desktop number
            string text = (_desktopIndex + 1).ToString();
            using (var font = new Font("Segoe UI", 9, FontStyle.Bold))
            using (var brush = new SolidBrush(textColor))
            {
                var textSize = g.MeasureString(text, font);
                var x = (Width - textSize.Width) / 2;
                var y = (Height - textSize.Height) / 2;
                g.DrawString(text, font, brush, x, y);
            }

            // Draw subtle gradient effect for active state
            if (_isActive)
            {
                using (var gradientBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 0, Width, Height),
                    Color.FromArgb(40, Color.White),
                    Color.FromArgb(10, Color.White),
                    System.Drawing.Drawing2D.LinearGradientMode.Vertical))
                {
                    g.FillRectangle(gradientBrush, 1, 1, Width - 2, Height - 2);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tooltip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}