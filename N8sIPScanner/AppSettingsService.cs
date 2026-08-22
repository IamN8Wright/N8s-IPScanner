using System;
using System.IO;

namespace N8sIPScanner;

public static class AppSettingsService
{
    public static AppSettings Current { get; private set; } = new();

    public static void Load()
    {
        var settings = new AppSettings();

        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
            {
                Current = settings;
                return;
            }

            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var equalsIndex = line.IndexOf('=');
                if (equalsIndex <= 0)
                {
                    continue;
                }

                var key = line[..equalsIndex].Trim();
                var value = line[(equalsIndex + 1)..].Trim();

                if (string.Equals(key, "ThemeMode", StringComparison.OrdinalIgnoreCase))
                {
                    settings.ThemeMode = string.Equals(value, "Light", StringComparison.OrdinalIgnoreCase)
                        ? "Light"
                        : "Dark";
                }
                else if (string.Equals(key, "ShowLoopbackAdapters", StringComparison.OrdinalIgnoreCase))
                {
                    settings.ShowLoopbackAdapters = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                }
                else if (string.Equals(key, "ShowDisconnectedAdapters", StringComparison.OrdinalIgnoreCase))
                {
                    settings.ShowDisconnectedAdapters = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch
        {
            // Fall back to defaults if settings are unreadable.
        }

        Current = settings;
    }

    public static void Save()
    {
        try
        {
            var dir = GetSettingsDirectory();
            Directory.CreateDirectory(dir);

            File.WriteAllText(
                GetSettingsPath(),
                $"ThemeMode={Current.ThemeMode}\n" +
                $"ShowLoopbackAdapters={Current.ShowLoopbackAdapters.ToString().ToLowerInvariant()}\n" +
                $"ShowDisconnectedAdapters={Current.ShowDisconnectedAdapters.ToString().ToLowerInvariant()}\n");
        }
        catch
        {
            // Settings persistence should never stop the scanner from working.
        }
    }

    private static string GetSettingsDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "N8sIPScanner");
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(GetSettingsDirectory(), "settings.ini");
    }
}
