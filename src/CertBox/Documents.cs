// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel and CertBox contributors.
// Developed with AI; third-party components retain their original licenses.

namespace CertBox;

public static class Documents
{
    public static string Read(string name, string language = "en")
    {
        var resource = name == "LICENSE" ? "LICENSE.txt" : name + "." + (L.Languages.Contains(language) ? language : "en") + ".md";
        using var stream = typeof(Documents).Assembly.GetManifestResourceStream("CertBox.Documents." + resource)
            ?? throw new InvalidOperationException("Missing embedded document: " + resource);
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        // License text is authoritative and preserved verbatim. Our own short
        // documents only use plain paragraphs and Markdown heading markers.
        return name == "LICENSE" ? text : string.Join("\n", text.Replace("\r\n", "\n").Split('\n')
            .Select(line => line.StartsWith("# ", StringComparison.Ordinal) ? line[2..] : line));
    }
}
