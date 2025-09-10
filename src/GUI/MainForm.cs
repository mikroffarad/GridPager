using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GridPager
{
    public class MainForm : Form
    {
        private List<VirtualDesktopThumbnail> _desktopThumbnails = new List<VirtualDesktopThumbnail>();
        private Button _addButton;
        private ToolbarSettings _settings;
        private int _activeDesktopIndex = 0;
        private Timer _updateTimer;

        // Optimized grid configuration for taskbar height limit (50px max)
        private const int GRID_COLUMNS = 3;
        private const int GRID_ROWS = 2;
        private const int MAX_DESKTOPS = GRID_COLUMNS * GRID_ROWS; // 6 desktops max
        private const int THUMBNAIL_WIDTH = 48;  // Smaller for compact design
        private const int THUMBNAIL_HEIGHT = 20; // Much smaller to fit in taskbar
        private const int THUMBNAIL_SPACING = 1; // Minimal spacing
        private const int FORM_PADDING = 2;      // Minimal padding

        public MainForm(ToolbarSettings settings = null)
        {
            _settings = settings ?? new ToolbarSettings();

            InitializeComponent();
            CreateDesktopThumbnails();
            CreateAddButton();
            SetupUpdateTimer();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            AutoScaleDimensions = new SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(25, 25, 25); // Dark for taskbar integration
            FormBorderStyle = FormBorderStyle.None;
            Name = "MainForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = false;

            // Calculate form size - optimized for max 50px height
            int formWidth = (GRID_COLUMNS * THUMBNAIL_WIDTH) + ((GRID_COLUMNS - 1) * THUMBNAIL_SPACING) + (FORM_PADDING * 2);
            int formHeight = (GRID_ROWS * THUMBNAIL_HEIGHT) + ((GRID_ROWS - 1) * THUMBNAIL_SPACING) + (FORM_PADDING * 2);

            // Ensure we don't exceed taskbar height
            formHeight = Math.Min(formHeight, 46); // Leave some margin for taskbar

            Size = new Size(formWidth, formHeight);

            ResumeLayout(false);
        }

        private void CreateDesktopThumbnails()
        {
            try
            {
                // Clear existing thumbnails
                foreach (var thumbnail in _desktopThumbnails)
                {
                    Controls.Remove(thumbnail);
                    thumbnail.Dispose();
                }
                _desktopThumbnails.Clear();

                // Get actual desktop count
                var virtualDesktops = WindowsVirtualDesktop.GetInstance();
                int actualDesktopCount = Math.Min(virtualDesktops.Count, MAX_DESKTOPS);

                // Create thumbnails in grid layout
                for (int i = 0; i < MAX_DESKTOPS; i++)
                {
                    var thumbnail = new VirtualDesktopThumbnail(i);
                    thumbnail.Size = new Size(THUMBNAIL_WIDTH, THUMBNAIL_HEIGHT);

                    // Calculate grid position
                    int row = i / GRID_COLUMNS;
                    int col = i % GRID_COLUMNS;

                    int x = FORM_PADDING + (col * (THUMBNAIL_WIDTH + THUMBNAIL_SPACING));
                    int y = FORM_PADDING + (row * (THUMBNAIL_HEIGHT + THUMBNAIL_SPACING));

                    thumbnail.Location = new Point(x, y);
                    thumbnail.Anchor = AnchorStyles.Left | AnchorStyles.Top;

                    // Show/hide thumbnail based on actual desktop count
                    thumbnail.Visible = i < actualDesktopCount;
                    thumbnail.Enabled = i < actualDesktopCount;

                    _desktopThumbnails.Add(thumbnail);
                    Controls.Add(thumbnail);
                }
            }
            catch (Exception ex)
            {
                // If virtual desktop API fails, show at least one thumbnail
                var thumbnail = new VirtualDesktopThumbnail(0);
                thumbnail.Size = new Size(THUMBNAIL_WIDTH, THUMBNAIL_HEIGHT);
                thumbnail.Location = new Point(FORM_PADDING, FORM_PADDING);
                _desktopThumbnails.Add(thumbnail);
                Controls.Add(thumbnail);
            }
        }

        private void CreateAddButton()
        {
            _addButton = new Button();
            _addButton.Size = new Size(THUMBNAIL_WIDTH, THUMBNAIL_HEIGHT);
            _addButton.Text = "+";
            _addButton.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            _addButton.ForeColor = Color.White;
            _addButton.BackColor = Color.FromArgb(70, 70, 70);
            _addButton.FlatStyle = FlatStyle.Flat;
            _addButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            _addButton.FlatAppearance.BorderSize = 1;
            _addButton.UseVisualStyleBackColor = false;
            _addButton.Click += AddButton_Click;

            // Position add button - we'll update this in UpdateAddButton
            UpdateAddButton();

            Controls.Add(_addButton);
        }

        private void UpdateAddButton()
        {
            try
            {
                var virtualDesktops = WindowsVirtualDesktop.GetInstance();
                int actualDesktopCount = virtualDesktops.Count;

                // Show add button only if we can add more desktops
                bool canAddMore = actualDesktopCount < MAX_DESKTOPS;
                _addButton.Visible = canAddMore;
                _addButton.Enabled = canAddMore;

                if (canAddMore)
                {
                    // Position add button in the next available slot
                    int nextIndex = Math.Min(actualDesktopCount, MAX_DESKTOPS - 1);
                    int row = nextIndex / GRID_COLUMNS;
                    int col = nextIndex % GRID_COLUMNS;

                    int x = FORM_PADDING + (col * (THUMBNAIL_WIDTH + THUMBNAIL_SPACING));
                    int y = FORM_PADDING + (row * (THUMBNAIL_HEIGHT + THUMBNAIL_SPACING));

                    _addButton.Location = new Point(x, y);
                }
            }
            catch
            {
                _addButton.Visible = false;
            }
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            try
            {
                MessageBox.Show("Use Windows key + Ctrl + D to create a new desktop\n\nTip: Use Ctrl+Alt+Arrow keys for navigation!",
                               "Create Desktop", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Refresh the thumbnails after a short delay
                Task.Delay(1000).ContinueWith(_ =>
                {
                    if (!IsDisposed)
                    {
                        Invoke(new Action(() =>
                        {
                            CreateDesktopThumbnails();
                            UpdateAddButton();
                        }));
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create new desktop: {ex.Message}", "Error",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetupUpdateTimer()
        {
            _updateTimer = new Timer();
            _updateTimer.Interval = 500; // Reduced from 1000 to 500ms for faster updates
            _updateTimer.Tick += UpdateActiveDesktop;
            _updateTimer.Start();
        }

        private void UpdateActiveDesktop(object sender, EventArgs e)
        {
            try
            {
                var virtualDesktops = WindowsVirtualDesktop.GetInstance();
                var currentDesktop = virtualDesktops.Current;
                int newActiveIndex = WindowsVirtualDesktopManager.GetInstance().FromDesktop(currentDesktop);

                if (newActiveIndex != _activeDesktopIndex)
                {
                    _activeDesktopIndex = newActiveIndex;

                    // Update thumbnail states
                    for (int i = 0; i < _desktopThumbnails.Count; i++)
                    {
                        _desktopThumbnails[i].IsActiveDesktop = (i == _activeDesktopIndex);
                    }
                }

                // Check if desktop count changed
                int actualDesktopCount = Math.Min(virtualDesktops.Count, MAX_DESKTOPS);
                bool needsUpdate = false;

                for (int i = 0; i < _desktopThumbnails.Count; i++)
                {
                    bool shouldBeVisible = i < actualDesktopCount;
                    if (_desktopThumbnails[i].Visible != shouldBeVisible)
                    {
                        _desktopThumbnails[i].Visible = shouldBeVisible;
                        _desktopThumbnails[i].Enabled = shouldBeVisible;
                        needsUpdate = true;
                    }
                }

                if (needsUpdate)
                {
                    UpdateAddButton();
                }

                // Update thumbnails with current window information
                foreach (var thumbnail in _desktopThumbnails.Where(t => t.Visible))
                {
                    thumbnail.RefreshContent();
                }
            }
            catch
            {
                // Ignore errors during update
            }
        }

        public void ForceUpdateActiveDesktop()
        {
            try
            {
                // Immediately update active desktop without waiting for timer
                UpdateActiveDesktop(null, null);
                System.Diagnostics.Debug.WriteLine("Force update of active desktop completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ForceUpdateActiveDesktop: {ex.Message}");
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (e.Button == MouseButtons.Right)
            {
                ShowContextMenu(e.Location);
            }
        }

        private void ShowContextMenu(Point location)
        {
            var menu = new ContextMenuStrip();

            // Add hotkey info
            var hotkeyInfo = new ToolStripMenuItem("Navigation: Ctrl+Alt+Arrows");
            hotkeyInfo.Enabled = false;
            menu.Items.Add(hotkeyInfo);

            var moveWindowInfo = new ToolStripMenuItem("Move Window: Ctrl+Alt+WASD");
            moveWindowInfo.Enabled = false;
            menu.Items.Add(moveWindowInfo);
            menu.Items.Add(new ToolStripSeparator());

            // Test hotkeys
            var testHotkeys = new ToolStripMenuItem("Test Navigation");
            testHotkeys.Click += (s, e) => {
                MessageBox.Show("Navigation Keys:\nCtrl+Alt+Arrows - Switch between desktops\n\nMove Window Keys:\nCtrl+Alt+W/A/S/D - Move active window to adjacent desktop\n\nBoth support wrapping at grid edges!",
                               "Test Hotkeys", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            menu.Items.Add(testHotkeys);

            // Clear attention states (manual fix for orange highlighting)
            var clearAttentionItem = new ToolStripMenuItem("Clear Orange Highlighting");
            clearAttentionItem.Click += async (s, e) => {
                try
                {
                    await WindowAttentionManager.ClearAllWindowAttentionStates();
                    MessageBox.Show("Cleared orange highlighting from all windows.",
                                   "Attention States Cleared", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error clearing attention states: {ex.Message}",
                                   "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            menu.Items.Add(clearAttentionItem);

            // Refresh button
            var refreshItem = new ToolStripMenuItem("Refresh");
            refreshItem.Click += (s, e) => {
                CreateDesktopThumbnails();
                UpdateAddButton();
            };
            menu.Items.Add(refreshItem);

            menu.Items.Add(new ToolStripSeparator());

            // About
            var aboutItem = new ToolStripMenuItem("About");
            aboutItem.Click += (s, e) => {
                MessageBox.Show($"GridPager - Grid Virtual Desktop Navigator v1.3.2{Environment.NewLine}{Environment.NewLine}" +
                               $"Compact Layout: 3×2 grid (max 6 desktops){Environment.NewLine}" +
                               $"Current Desktops: {WindowsVirtualDesktop.GetInstance().Count}{Environment.NewLine}" +
                               $"Navigation: Ctrl+Alt+Arrow Keys{Environment.NewLine}" +
                               $"Move Window: Ctrl+Alt+WASD{Environment.NewLine}" +
                               $"Both with grid wrapping support{Environment.NewLine}" +
                               $"Manual fix: Clear Orange Highlighting{Environment.NewLine}{Environment.NewLine}" +
                               $"Based on Switchie by darkguy2008{Environment.NewLine}" +
                               $"GridPager fork with enhanced grid navigation", "About GridPager");
            };
            menu.Items.Add(aboutItem);

            // Hide toolbar
            var hideItem = new ToolStripMenuItem("Hide");
            hideItem.Click += (s, e) => Hide();
            menu.Items.Add(hideItem);

            menu.Show(this, location);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _updateTimer?.Stop();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _updateTimer?.Dispose();
                _addButton?.Dispose();
                foreach (var thumbnail in _desktopThumbnails)
                {
                    thumbnail.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Minimal border - just a simple line
            using (var pen = new Pen(Color.FromArgb(60, 60, 60), 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return cp;
            }
        }
    }
}
