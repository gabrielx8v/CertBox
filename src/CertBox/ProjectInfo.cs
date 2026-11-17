// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel and CertBox contributors.
// Developed with AI; third-party components retain their original licenses.

using System.Text.Json;

namespace CertBox;

public static class ProjectInfo
{
    public const string LicenseId = "GPL-3.0-or-later";
    public const string Copyright = "Copyright (C) 2026 Gabriel and CertBox contributors";
    public static string Version => typeof(ProjectInfo).Assembly.GetName().Version?.ToString(3) ?? "";
    public static string PlatformName => OperatingSystem.IsLinux() ? "Linux x64" : "Windows x64";
    public static string IconResource => OperatingSystem.IsLinux() ? "certbox-256.png" : "certbox.ico";
    public static Uri? Repository { get; } = ReadRepository();
    public static Uri? Source => Link("tree/v" + Version);
    public static Uri? Support => Link("issues");
    public static Uri? Releases => Link("releases");

    private static Uri? Link(string path) => Repository == null ? null : new Uri(Repository.AbsoluteUri.TrimEnd('/') + "/" + path);

    public static Uri? ParseRepository(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || uri.Host != "github.com" || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return null;
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length != 2 || parts.Any(p => p.Length == 0 || p.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))) return null;
        var repository = parts[1].EndsWith(".git", StringComparison.Ordinal) ? parts[1][..^4] : parts[1];
        if (repository is "" or "." or ".." || parts[0] is "." or "..") return null;
        return new Uri("https://github.com/" + parts[0] + "/" + repository);
    }

    private static Uri? ReadRepository()
    {
        using var stream = typeof(ProjectInfo).Assembly.GetManifestResourceStream("CertBox.Documents.project.json");
        if (stream == null) return null;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.TryGetProperty("repositoryUrl", out var value) && value.ValueKind == JsonValueKind.String
            ? ParseRepository(value.GetString()) : null;
    }
}
