// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel and CertBox contributors.
// Developed with AI; third-party components retain their original licenses.

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace CertBox;

public sealed class App : Application
{
    public static AppBuilder CreateBuilder()
    {
        var builder = AppBuilder.Configure<App>();
#if LINUX_DESKTOP
        return builder.UseX11().UseSkia().UseHarfBuzz().WithInterFont();
#else
        return builder.UseWin32().UseSkia().UseHarfBuzz().WithInterFont();
#endif
    }

    public override void Initialize()
    {
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
