# Third-party notices

CertBox uses the following third-party components. Their original licenses
and copyright notices apply to those components; the application license does
not replace them.

- .NET runtime 10 (Microsoft and contributors): MIT and additional notices.
  https://github.com/dotnet/runtime/blob/main/LICENSE.TXT
  https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT
- Avalonia 11.3.23, Avalonia.Win32, Avalonia.Skia, Avalonia.Themes.Fluent and related components:
  MIT. https://github.com/AvaloniaUI/Avalonia/blob/11.3.23/licence.md
- Inter font: SIL Open Font License 1.1. https://github.com/rsms/inter/blob/master/LICENSE.txt
- SkiaSharp and HarfBuzzSharp (Microsoft/Xamarin and contributors): MIT, with
  Skia/HarfBuzz upstream notices. https://github.com/mono/SkiaSharp
  https://github.com/harfbuzz/harfbuzz
- MicroCom.Runtime: MIT. https://github.com/kekekeks/MicroCom
- Linux: Avalonia.X11 and Avalonia.FreeDesktop 11.3.23 (MIT), plus
  Tmds.DBus.Protocol 0.21.3 (Tom Deseyn, MIT). https://github.com/tmds/Tmds.DBus
- Windows only: ANGLE and its original notices. Linux builds do not include ANGLE.

The native graphics components include work from the FreeType Project
(https://freetype.org). This software is based in part on the work of the
Independent JPEG Group. The complete upstream notices below remain applicable.

The source `licenses/` folder includes upstream license/notice texts for the
.NET, Avalonia, font and graphics components. Keep those texts when redistributing
the source. Each executable also embeds its platform-relevant
texts and this notice, accessible under Settings > Third-party licenses, so they
remain available with a single-file distribution. The project license does not
replace any upstream license. See `DEPENDENCIES.md` and `DEPENDENCIES.json` for
the checked source revisions, native build configuration, attribution and scope
of verification. Aggregated notices can also mention optional components not
linked into the Windows library targets.
