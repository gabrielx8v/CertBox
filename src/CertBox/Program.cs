// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel and CertBox contributors.
// Developed with AI; third-party components retain their original licenses.

using Avalonia;

namespace CertBox;

internal static class Program
{
    [STAThread]
    public static void Main() => BuildAvaloniaApp().StartWithClassicDesktopLifetime(Array.Empty<string>());

    public static AppBuilder BuildAvaloniaApp() => App.CreateBuilder();
}
