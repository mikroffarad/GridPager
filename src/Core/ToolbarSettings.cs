using System;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace GridPager
{
    public class ToolbarSettings
    {
        private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GridPager", "settings.json");

        public Point Position { get; set; } = new Point(100, 100);
        public bool IsVisible { get; set; } = true;
        public bool DockToTop { get; set; } = false;
        public int Opacity { get; set; } = 100;

        public static ToolbarSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<ToolbarSettings>(json) ?? new ToolbarSettings();
                }
            }
            catch { }

            return new ToolbarSettings();
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}