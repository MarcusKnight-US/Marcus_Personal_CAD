using System;
using System.IO;
using System.Text.Json;

namespace DwgToPngConverter
{
    public class AppConfig
    {
        public int DefaultDpi { get; set; } = 200;
        public float TextSizeMultiplier { get; set; } = 1.0f;
        public int MinLayoutWidth { get; set; } = 1600;
        public int MaxLayoutWidth { get; set; } = 8000;
        public double DefaultPaperWidthMm { get; set; } = 914.4;
        public double DefaultPaperHeightMm { get; set; } = 609.6;
        public float OverallLineWeight { get; set; } = 1.0f;
        public string BackgroundColor { get; set; } = "#FFFFFF";
        public float MinLineWeight { get; set; } = 0.5f;
        public float ModelSpaceMarginMultiplier { get; set; } = 0.9f;
        public float PaperSpaceMarginMultiplier { get; set; } = 0.95f;
        public int ModelSpaceWidth { get; set; } = 1000;
        public int ModelSpaceHeight { get; set; } = 1000;


        private static AppConfig? _instance;
        public static AppConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Load();
                }
                return _instance;
            }
        }

        public static void Reload()
        {
            _instance = Load();
        }

        private static AppConfig Load()
        {
            var config = new AppConfig();
            try
            {
                string[] pathsToTry = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "config.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "DwgToPngConverter", "config.json"),
                    "config.json"
                };

                string? foundPath = null;
                foreach (var path in pathsToTry)
                {
                    if (File.Exists(path))
                    {
                        foundPath = path;
                        break;
                    }
                }

                if (foundPath != null)
                {
                    string json = File.ReadAllText(foundPath);
                    var parsed = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (parsed != null)
                    {
                        config = parsed;
                        Console.WriteLine($"Loaded config from: {foundPath}");
                    }
                }
                else
                {
                    Console.WriteLine("No config.json found, using default settings.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to load config.json ({ex.Message}), using defaults.");
            }
            return config;
        }
    }
}
