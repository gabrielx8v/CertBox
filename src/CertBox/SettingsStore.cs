// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel and CertBox contributors.
// Developed with AI; third-party components retain their original licenses.

using System.Runtime.InteropServices;
using System.Text.Json;

namespace CertBox;

public sealed class SettingsStore
{
    public string FilePath { get; }
    public string Language { get; private set; }
    public bool HasSavedLanguage { get; private set; }

    public SettingsStore(string? directory = null, string? systemLanguage = null)
    {
        FilePath = Path.Combine(directory ?? DefaultDirectory(), "settings.json");
        Language = Suggest(systemLanguage ?? SystemLanguage());
        try
        {
            var file = new FileInfo(FilePath);
            if (!file.Exists || file.Length > 4096) return;
            using var json = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("language", out var value)
                && value.ValueKind == JsonValueKind.String
                && value.GetString() is string saved && L.Languages.Contains(saved))
            {
                Language = saved;
                HasSavedLanguage = true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }

    public bool Save(string language)
    {
        if (!L.Languages.Contains(language)) throw new ArgumentException(nameof(language));
        string? temp = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteString("language", language);
                writer.WriteEndObject();
            }
            File.Move(temp, FilePath, overwrite: true);
            Language = language;
            HasSavedLanguage = true;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        finally
        {
            if (temp != null)
                try { File.Delete(temp); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    public static string Suggest(string language) => language.Split('-', '_', '.', ':')[0].ToLowerInvariant() switch
    {
        "pt" => "pt", "es" => "es", _ => "en"
    };

    private static string SystemLanguage()
    {
        // InvariantGlobalization keeps the portable executable small. Read the
        // real Windows UI language directly instead of loading globalization data.
        if (!OperatingSystem.IsWindows())
        {
            foreach (var variable in new[] { "LC_ALL", "LC_MESSAGES", "LANGUAGE", "LANG" })
                if (Environment.GetEnvironmentVariable(variable) is string value && !string.IsNullOrWhiteSpace(value)) return value;
            return "en";
        }
        return (GetUserDefaultUILanguage() & 0x03ff) switch { 0x16 => "pt", 0x0a => "es", _ => "en" };
    }

    private static string DefaultDirectory()
    {
        if (!OperatingSystem.IsLinux()) return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CertBox");
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(config) || !Path.IsPathRooted(config)) config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(config, "CertBox");
    }

    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();
}
