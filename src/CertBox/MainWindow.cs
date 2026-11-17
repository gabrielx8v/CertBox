// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel and CertBox contributors.
// Developed with AI; third-party components retain their original licenses.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Automation;
using Avalonia.LogicalTree;
using Avalonia.Platform;
using System.Globalization;
using System.Runtime.InteropServices;

namespace CertBox;

public sealed class MainWindow : Window
{
    private readonly SettingsStore settings;
    private readonly List<Action> languageBindings = new();
    private readonly Grid mainLayout;
    private readonly Grid root;
    private SettingsPage? settingsPage;
    private readonly Button settingsButton = new()
    {
        Content = CreateSettingsIcon(),
        Width = 38, Height = 38, Padding = new Thickness(0), CornerRadius = new CornerRadius(6),
        HorizontalAlignment = HorizontalAlignment.Left, HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center, Background = Brushes.Transparent,
        Foreground = Brush.Parse("#475467"), BorderThickness = new Thickness(0)
    };
    private readonly ConversionSelection selection = new();
    private readonly HashSet<string> checkedCertificates = new();
    private bool busy;
    private bool dragActive;
    private long dragEpoch;
    private readonly Border dropZone = new() { CornerRadius = new CornerRadius(10), Background = Brush.Parse("#FAFBFC"), BorderBrush = Brush.Parse("#D7DDE5"), BorderThickness = new Thickness(1) };
    private readonly TextBlock mode = new() { Text = "CRT ⇄ PEM", FontSize = 12, Foreground = Brush.Parse("#586273") };
    private readonly TextBlock prompt = new() { Text = "Arraste seus arquivos", FontSize = 18, FontWeight = FontWeight.Medium, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock hint = new() { Text = "CRT, PEM ou KEY", FontSize = 12, Foreground = Brush.Parse("#667085"), HorizontalAlignment = HorizontalAlignment.Center };
    private readonly StackPanel fileRows = new() { Spacing = 7 };
    private readonly Grid details = new() { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 10, Margin = new Thickness(12) };
    private readonly Grid zone = new() { RowDefinitions = new RowDefinitions("Auto,12,*") };
    private readonly FileListViewport fileList = new();
    private readonly GridSplitter divider = CreateDivider(false);
    private readonly Grid workspace = new() { ColumnDefinitions = new ColumnDefinitions("43*,12,57*") };
    private readonly TextBlock status = new() { IsVisible = false, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxHeight = 48, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBox password = new() { Watermark = "Senha da chave para gerar PEM", PasswordChar = '●', IsVisible = false };
    private readonly Button select = new() { Content = "Selecionar", HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(20, 8) };
    private readonly Button clear = SmallButton("Limpar");
    private readonly Button addFiles = SmallButton("Adicionar arquivos");
    private readonly Control emptyState;
    private readonly StackPanel inspector = new() { Spacing = 10, Margin = new Thickness(16) };
    private readonly ScrollViewer inspectorView;
    private readonly ComboBox certificatePicker = new() { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
    private readonly StackPanel certificateData = new() { Spacing = 8 };
    private readonly Border dragOverlay = new()
    {
        IsVisible = false, IsHitTestVisible = false, Margin = new Thickness(8), CornerRadius = new CornerRadius(12),
        Background = Brush.Parse("#F0EAF3FF"), BorderBrush = Brush.Parse("#3B82F6"), BorderThickness = new Thickness(2)
    };
    private string? selectedFingerprint;
    private readonly StackPanel exportChoices = new() { Spacing = 4 };
    private readonly TextBlock exportSummary = new() { FontSize = 11, Foreground = Brush.Parse("#344054"), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock exportOrder = new() { FontSize = 10, Foreground = Brush.Parse("#667085"), TextWrapping = TextWrapping.Wrap, MaxHeight = 32, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Button exportCrt = ExportButton("Exportar CRT", "#2863AF", "#FFFFFF");
    private readonly Button saveKey = ExportButton("Salvar chave (.key)", "#D1FAE5", "#047857");
    private readonly Button generate = ExportButton("Gerar PEM", "#DBEAFE", "#1E40AF");
    private IReadOnlyList<CertificateInfo> displayCertificates = Array.Empty<CertificateInfo>();
    private IReadOnlyList<CertificateInfo> DisplayCertificates => displayCertificates;
    private InputInfo? KeySource => selection.Key ?? selection.Certificates.FirstOrDefault(f => f.Kind == InputKind.Bundle);

    public MainWindow(SettingsStore? preferences = null)
    {
        settings = preferences ?? new SettingsStore();
        Title = "CertBox";
        using (var icon = AssetLoader.Open(new Uri("avares://certbox/Assets/" + ProjectInfo.IconResource))) Icon = new WindowIcon(icon);
        Width = 1000; Height = 680; MinWidth = 820; MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.White;
        UseLayoutRounding = true;
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 12, Margin = new Thickness(20, 14, 20, 10) };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        ToolTip.SetTip(settingsButton, "Configurações");
        AutomationProperties.SetName(settingsButton, "Configurações");
        settingsButton.Click += (_, _) => OpenSettings(false);
        header.Children.Add(settingsButton);
        mode.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(mode, 1); header.Children.Add(mode); layout.Children.Add(header);

        details.Children.Add(SectionTitle("Arquivos"));
        var center = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12) };
        center.Children.Add(prompt); center.Children.Add(hint);
        select.Click += async (_, _) => await SelectFiles(); center.Children.Add(select);
        emptyState = new Border { MinHeight = 150, Child = center };
        var listArea = new Grid(); listArea.Children.Add(emptyState);
        fileList.Content = fileRows; listArea.Children.Add(fileList);
        Grid.SetRow(listArea, 1); details.Children.Add(listArea);
        var helpers = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
        addFiles.IsVisible = clear.IsVisible = true;
        addFiles.Click += async (_, _) => await SelectFiles();
        clear.Background = Brush.Parse("#DC2626"); clear.Foreground = Brushes.White;
        clear.FontWeight = FontWeight.SemiBold;
        clear.Click += (_, _) => { if (busy) return; selection.Clear(); password.Text = ""; status.IsVisible = false; Refresh(); };
        helpers.Children.Add(addFiles); helpers.Children.Add(clear);
        Grid.SetRow(helpers, 2); details.Children.Add(helpers);
        dropZone.Child = details; zone.Children.Add(dropZone);
        zone.RowDefinitions[0].MinHeight = 116;
        zone.RowDefinitions[2].MinHeight = 245;
        AutomationProperties.SetName(divider, "Ajustar divisão entre arquivos e exportação");
        ToolTip.SetTip(divider, "Arraste para expandir a lista de arquivos");
        divider.AddHandler(PointerPressedEvent, (_, e) => { if (e.GetCurrentPoint(divider).Properties.IsLeftButtonPressed) PrepareListResize(); }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        divider.AddHandler(KeyDownEvent, (_, e) => { if (e.Key is Avalonia.Input.Key.Up or Avalonia.Input.Key.Down) PrepareListResize(); }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Grid.SetRow(divider, 1); zone.Children.Add(divider);

        var exports = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 8, Margin = new Thickness(0, 4, 10, 0) };
        exports.Children.Add(SectionTitle("Gerar arquivo"));
        var choicesView = new ScrollViewer { Content = exportChoices, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(choicesView, 1); exports.Children.Add(choicesView);
        var actions = new Grid { RowDefinitions = new RowDefinitions("Auto,38,38"), ColumnDefinitions = new ColumnDefinitions("*,*"), RowSpacing = 8, ColumnSpacing = 8, UseLayoutRounding = true };
        var exportInfo = new StackPanel { Spacing = 6 };
        exportInfo.Children.Add(exportSummary); exportInfo.Children.Add(exportOrder); exportInfo.Children.Add(password);
        Grid.SetColumnSpan(exportInfo, 2); actions.Children.Add(exportInfo);
        exportCrt.Click += async (_, _) => await SaveCertificates(false);
        generate.Click += async (_, _) => await SaveCertificates(true);
        saveKey.Click += async (_, _) => await SavePrivateKey();
        ToolTip.SetTip(exportCrt, "Agrupa somente os certificados marcados, sem chave privada.");
        ToolTip.SetTip(generate, "Agrupa os certificados marcados. Inclui a chave carregada quando o domínio está marcado.");
        ToolTip.SetTip(saveKey, "Salva somente a chave privada, preservando sua senha.");
        Grid.SetRow(exportCrt, 1); Grid.SetColumnSpan(exportCrt, 2); actions.Children.Add(exportCrt);
        Grid.SetRow(saveKey, 2); actions.Children.Add(saveKey);
        Grid.SetRow(generate, 2); Grid.SetColumn(generate, 1); actions.Children.Add(generate);
        Grid.SetRow(actions, 2); exports.Children.Add(actions);
        Grid.SetRow(exports, 2); zone.Children.Add(exports);
        workspace.ColumnDefinitions[0].MinWidth = 310; workspace.ColumnDefinitions[2].MinWidth = 340;
        workspace.Children.Add(zone);
        var columnDivider = CreateDivider(true);
        AutomationProperties.SetName(columnDivider, "Ajustar largura das colunas");
        Grid.SetColumn(columnDivider, 1); workspace.Children.Add(columnDivider);

        inspector.Children.Add(SectionTitle("Dados do certificado"));
        inspector.Children.Add(new TextBlock { Text = "Certificado em visualização", FontSize = 11, Foreground = Brush.Parse("#667085") });
        AutomationProperties.SetName(certificatePicker, "Certificado em visualização");
        certificatePicker.SelectionChanged += (_, _) =>
        {
            if (certificatePicker.SelectedItem is CertificateChoice choice) { selectedFingerprint = choice.Certificate.Fingerprint; ShowCertificate(choice.Certificate); }
        };
        inspector.Children.Add(certificatePicker); inspector.Children.Add(certificateData);
        inspectorView = new ScrollViewer { Content = inspector, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var infoBorder = new Border { Background = Brush.Parse("#FAFBFC"), BorderBrush = Brush.Parse("#D7DDE5"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Child = inspectorView };
        Grid.SetColumn(infoBorder, 2); workspace.Children.Add(infoBorder);
        Grid.SetRow(workspace, 1); layout.Children.Add(workspace);

        var footer = new StackPanel { Spacing = 6 }; footer.Children.Add(status);
        footer.Children.Add(new TextBlock { Text = "Processamento local · Nenhum arquivo é enviado à internet", FontSize = 10, Foreground = Brush.Parse("#697386"), HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        footer.Children.Add(new TextBlock { Text = "Desenvolvido com IA", FontSize = 10, Foreground = Brush.Parse("#697386"), HorizontalAlignment = HorizontalAlignment.Center });
        Grid.SetRow(footer, 2); layout.Children.Add(footer);
        dragOverlay.Child = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 10, Children =
        {
            new TextBlock { Text = "↓", FontSize = 42, Foreground = Brush.Parse("#2563EB"), HorizontalAlignment = HorizontalAlignment.Center },
            new TextBlock { Text = "Solte os arquivos aqui", FontSize = 22, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#1D4ED8"), HorizontalAlignment = HorizontalAlignment.Center },
            new TextBlock { Text = "Certificados e chaves serão identificados", FontSize = 12, Foreground = Brush.Parse("#2863AF"), HorizontalAlignment = HorizontalAlignment.Center }
        } };
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnDragOver, handledEventsToo: true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver, handledEventsToo: true);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave, handledEventsToo: true);
        AddHandler(DragDrop.DropEvent, OnDrop, handledEventsToo: true);
        mainLayout = layout;
        root = new Grid(); root.Children.Add(layout); root.Children.Add(dragOverlay);
        RegisterStaticTranslations(layout);
        RegisterStaticTranslations(dragOverlay);
        Content = root;
        ApplyLanguage(settings.Language);
        Opened += (_, _) => { if (!settings.HasSavedLanguage) OpenSettings(true); };
    }

    private void RegisterStaticTranslations(Control content)
    {
        void Bind(string? key, Action<string> write)
        {
            if (key != null && L.Keys.Contains(key)) languageBindings.Add(() => write(L.T(key)));
        }
        foreach (var control in content.GetLogicalDescendants().OfType<Control>().Prepend(content))
        {
            if (control is TextBlock text) Bind(text.Text, value => text.Text = value);
            if (control is Button button && button.Content is string label) Bind(label, value => button.Content = value);
            if (control is TextBox input) Bind(input.Watermark, value => input.Watermark = value);
            if (ToolTip.GetTip(control) is string tip) Bind(tip, value => ToolTip.SetTip(control, value));
            Bind(AutomationProperties.GetName(control), value => AutomationProperties.SetName(control, value));
        }
    }

    private static Control CreateSettingsIcon()
    {
        if (OperatingSystem.IsWindows()) return new TextBlock { Text = "⚙", FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 25 };
        var points = Enumerable.Range(0, 32).Select(i =>
        {
            double radius = i % 4 is 1 or 2 ? 11 : 8;
            double angle = i * Math.PI / 16;
            return (12 + radius * Math.Cos(angle)).ToString("F3", CultureInfo.InvariantCulture) + "," + (12 + radius * Math.Sin(angle)).ToString("F3", CultureInfo.InvariantCulture);
        });
        var path = "F0 M" + string.Join(" L", points) + " Z M12,8 A4,4 0 1 0 12,16 A4,4 0 1 0 12,8 Z";
        return new PathIcon { Data = Geometry.Parse(path), Width = 25, Height = 25 };
    }

    private void ApplyLanguage(string language)
    {
        L.Use(language);
        foreach (var update in languageBindings) update();
        status.IsVisible = false;
        UpdatePrompt();
        Refresh();
    }

    private void OpenSettings(bool firstRun)
    {
        if (busy || settingsPage != null) return;
        SetDragHighlight(false);
        settingsPage = new SettingsPage(settings, firstRun, L.Language);
        void CloseSettings()
        {
            root.Children.Remove(settingsPage!);
            settingsPage = null;
            mainLayout.IsVisible = true;
            settingsButton.Focus();
        }
        settingsPage.Completed += language => { CloseSettings(); ApplyLanguage(language); };
        settingsPage.Canceled += CloseSettings;
        mainLayout.IsVisible = false;
        root.Children.Add(settingsPage);
        settingsPage.LanguagePicker.Focus();
    }

    private static TextBlock SectionTitle(string text) => new() { Text = text, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#344054") };
    private static Button ExportButton(string text, string background, string foreground) => new()
    {
        Content = text, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
        Height = 38, MinHeight = 38, Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
        FontSize = 11, Padding = new Thickness(10, 0), Background = Brush.Parse(background), Foreground = Brush.Parse(foreground)
    };
    private static GridSplitter CreateDivider(bool vertical) => new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
        ResizeDirection = vertical ? GridResizeDirection.Columns : GridResizeDirection.Rows,
        ResizeBehavior = GridResizeBehavior.PreviousAndNext,
        Cursor = new Cursor(vertical ? StandardCursorType.SizeWestEast : StandardCursorType.SizeNorthSouth),
        Template = new FuncControlTemplate<GridSplitter>((_, _) => new Grid { Background = Brushes.Transparent, Children =
        {
            new Border { Width = vertical ? 1 : double.NaN, Height = vertical ? double.NaN : 1, Background = Brush.Parse("#CDD5DF"), HorizontalAlignment = vertical ? HorizontalAlignment.Center : HorizontalAlignment.Stretch, VerticalAlignment = vertical ? VerticalAlignment.Stretch : VerticalAlignment.Center },
            new Border { Width = vertical ? 3 : 32, Height = vertical ? 32 : 3, CornerRadius = new CornerRadius(2), Background = Brush.Parse("#98A2B3"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        } })
    };

    private static Button SmallButton(string text) => new()
    {
        Content = text, FontSize = 11, Height = 32, Margin = new Thickness(0), Padding = new Thickness(12, 0),
        VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Background = Brushes.Transparent,
        BorderThickness = new Thickness(0), Foreground = Brush.Parse("#475467"), IsVisible = false
    };

    private static (string Foreground, string Background) RoleColors(string role) => role switch
    {
        "Domínio" or "domain" or "complete" => ("#1E40AF", "#DBEAFE"),
        "CA intermediária" or "intermediate" or "ca" => ("#92400E", "#FEF3C7"),
        "CA raiz" or "root" => ("#6D28D9", "#EDE9FE"),
        _ => ("#047857", "#D1FAE5")
    };

    private static Control Badge(string role)
    {
        var colors = RoleColors(role);
        return new Border
        {
            Background = Brush.Parse(colors.Background), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 3), Margin = new Thickness(0, 0, 6, 2),
            Child = new TextBlock { Text = L.T(role), FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse(colors.Foreground) }
        };
    }

    private sealed class FileListViewport : ScrollViewer
    {
        // Keep the ScrollViewer theme/template when specializing its measurement.
        protected override Type StyleKeyOverride => typeof(ScrollViewer);

        private bool useDefaultHeight = true;
        public bool UseDefaultHeight
        {
            get => useDefaultHeight;
            set { if (useDefaultHeight != value) { useDefaultHeight = value; InvalidateMeasure(); } }
        }

        public FileListViewport()
        {
            MinHeight = 36;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (UseDefaultHeight && Content is StackPanel rows)
            {
                var firstRows = rows.Children.Take(2).ToArray();
                double height = 0;
                foreach (var row in firstRows)
                {
                    row.Measure(new Size(Math.Max(0, availableSize.Width - 16), double.PositiveInfinity));
                    height += row.DesiredSize.Height;
                }
                if (firstRows.Length > 1) height += rows.Spacing;
                availableSize = new Size(availableSize.Width, Math.Min(availableSize.Height, height));
            }
            return base.MeasureOverride(availableSize);
        }
    }

    private void PrepareListResize()
    {
        if (!details.IsVisible || !fileList.UseDefaultHeight) return;
        // Freeze the current proportions before GridSplitter starts, so both panes
        // participate in resizing and respect their minimum heights.
        zone.RowDefinitions[0].Height = new GridLength(Math.Max(1, zone.RowDefinitions[0].ActualHeight), GridUnitType.Star);
        zone.RowDefinitions[2].Height = new GridLength(Math.Max(1, zone.RowDefinitions[2].ActualHeight), GridUnitType.Star);
        fileList.UseDefaultHeight = false;
    }

    private Control FileRow(InputInfo file)
    {
        var row = new StackPanel { Spacing = 5 };
        row.Children.Add(new TextBlock { Text = Path.GetFileName(file.Path), FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#111827"), TextTrimming = TextTrimming.CharacterEllipsis });
        var badges = new WrapPanel();
        foreach (var role in file.Certificates.Select(c => c.RoleKey).Distinct()) badges.Children.Add(Badge(role));
        if (file.Kind is InputKind.PrivateKey or InputKind.Bundle) badges.Children.Add(Badge(L.T("Chave privada")));
        row.Children.Add(badges);
        var remove = new Button
        {
            Content = "×", FontSize = 19, Padding = new Thickness(7, 0), MinWidth = 30, Height = 30,
            VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent, Foreground = Brush.Parse("#667085"),
            BorderThickness = new Thickness(0), IsEnabled = !busy
        };
        AutomationProperties.SetName(remove, L.F("Remover {0}", Path.GetFileName(file.Path)));
        ToolTip.SetTip(remove, L.T("Remover este arquivo"));
        remove.Click += (_, _) =>
        {
            if (busy) return;
            selection.Remove(file.Path); password.Text = ""; status.IsVisible = false; Refresh();
        };
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        content.Children.Add(row); Grid.SetColumn(remove, 1); content.Children.Add(remove);
        var box = new Border { Child = content, Padding = new Thickness(10, 8), Background = Brushes.White, BorderBrush = Brush.Parse("#E0E5EC"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6) };
        ToolTip.SetTip(box, file.Path);
        return box;
    }

    private sealed record CertificateChoice(CertificateInfo Certificate)
    {
        public override string ToString() => Certificate.Role + " · " + (string.IsNullOrEmpty(Certificate.Name) ? Certificate.Subject : Certificate.Name);
    }

    private void RefreshInspector()
    {
        var choices = DisplayCertificates.Select(c => new CertificateChoice(c)).ToArray();
        var current = choices.FirstOrDefault(c => c.Certificate.Fingerprint == selectedFingerprint)
            ?? choices.FirstOrDefault(c => !c.Certificate.IsCa) ?? choices.FirstOrDefault();
        certificatePicker.ItemsSource = choices;
        certificatePicker.IsVisible = choices.Length > 0;
        certificatePicker.SelectedItem = current;
        if (current != null)
        {
            selectedFingerprint = current.Certificate.Fingerprint;
            ShowCertificate(current.Certificate);
        }
        else
        {
            selectedFingerprint = null; certificateData.Children.Clear();
            if (selection.Key == null)
            {
                certificateData.Children.Add(new TextBlock { Text = L.T("Adicione um arquivo para visualizar os dados do certificado."), FontSize = 12, Foreground = Brush.Parse("#667085"), TextWrapping = TextWrapping.Wrap });
                return;
            }
            certificateData.Children.Add(Badge(L.T("Chave privada")));
            certificateData.Children.Add(new TextBlock { Text = selection.Key?.IsEncrypted == true ? L.T("Chave protegida por senha") : L.T("Chave privada identificada"), FontSize = 16, FontWeight = FontWeight.SemiBold });
            certificateData.Children.Add(new TextBlock { Text = L.T("Adicione o certificado para visualizar seus dados e gerar o PEM."), FontSize = 12, Foreground = Brush.Parse("#667085"), TextWrapping = TextWrapping.Wrap });
        }
    }

    private void ShowCertificate(CertificateInfo certificate)
    {
        certificateData.Children.Clear();
        var now = DateTime.UtcNow;
        var validity = now < certificate.NotBefore ? L.T("Ainda não vigente") : now > certificate.NotAfter ? L.T("Expirado") : L.T("Dentro do prazo");
        certificateData.Children.Add(new TextBlock
        {
            Text = validity + " · " + certificate.Role, FontSize = 11, FontWeight = FontWeight.Medium,
            Foreground = Brush.Parse(now < certificate.NotBefore || now > certificate.NotAfter ? "#B45309" : "#047857")
        });
        var table = new Grid { ColumnDefinitions = new ColumnDefinitions("100,*"), RowSpacing = 5, ColumnSpacing = 8 };
        void Field(string label, string value, bool mono = false)
        {
            int row = table.RowDefinitions.Count; table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var name = new TextBlock { Text = L.T(label), FontSize = 11, Foreground = Brush.Parse("#667085"), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            Grid.SetRow(name, row); table.Children.Add(name);
            var text = new SelectableTextBlock { Text = string.IsNullOrEmpty(value) ? "—" : value, FontSize = 11, Foreground = Brush.Parse("#1F2937"), TextWrapping = TextWrapping.Wrap };
            ApplySystemSelectionColors(text);
            text.GotFocus += (_, _) => ApplySystemSelectionColors(text);
            if (mono) text.FontFamily = new FontFamily("Consolas, monospace");
            var cell = new Border { Background = Brushes.White, BorderBrush = Brush.Parse("#E0E5EC"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 5), Child = text };
            Grid.SetRow(cell, row); Grid.SetColumn(cell, 1); table.Children.Add(cell);
        }
        Field(L.T("Titular"), certificate.Subject);
        Field(L.T("Emissor"), certificate.Issuer);
        Field(L.T("Válido de"), L.Date(certificate.NotBefore));
        Field(L.T("Válido até"), L.Date(certificate.NotAfter));
        Field(L.T("Chave pública"), certificate.PublicKey);
        Field(L.T("Assinatura"), certificate.SignatureAlgorithm);
        Field("DNS (SAN)", string.Join("\n", certificate.DnsNames));
        Field(L.T("Número de série"), certificate.SerialNumber, true);
        Field("SHA-256", string.Join(" ", Enumerable.Range(0, certificate.Fingerprint.Length / 2).Select(i => certificate.Fingerprint.Substring(i * 2, 2))), true);
        if (certificate.AdditionalFields.Count > 0)
        {
            int row = table.RowDefinitions.Count; table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var heading = new TextBlock { Text = L.T("Informações adicionais"), FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#344054"), Margin = new Thickness(0, 12, 0, 5) };
            Grid.SetRow(heading, row); Grid.SetColumnSpan(heading, 2); table.Children.Add(heading);
            foreach (var field in certificate.AdditionalFields)
                Field(field.Name.StartsWith("Extensão ", StringComparison.Ordinal) ? L.F("Extensão {0}", field.Oid ?? "") : field.Name, L.Detail(field.Value));
        }
        certificateData.Children.Add(table);
    }

    private static void ApplySystemSelectionColors(SelectableTextBlock text)
    {
        if (!OperatingSystem.IsWindows()) return;
        // COLOR_HIGHLIGHT and COLOR_HIGHLIGHTTEXT are the system's selection pair.
        text.SelectionBrush = SystemColorBrush(13);
        text.SelectionForegroundBrush = SystemColorBrush(14);
    }

    private static IBrush SystemColorBrush(int index)
    {
        uint color = GetSysColor(index);
        return new SolidColorBrush(Color.FromRgb((byte)color, (byte)(color >> 8), (byte)(color >> 16)));
    }

    [DllImport("user32.dll")]
    private static extern uint GetSysColor(int index);

    private async Task SelectFiles(bool keyOnly = false)
    {
        if (busy) return;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = keyOnly ? L.T("Adicionar chave privada") : L.T("Selecionar certificados do domínio, CAs e chave opcional"),
                AllowMultiple = !keyOnly,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(keyOnly ? L.T("Chave privada") : L.T("Certificados e chaves"))
                    { Patterns = keyOnly ? new[] { "*.key", "*.pem", "*.der" } : new[] { "*.crt", "*.cer", "*.pem", "*.der", "*.key" } }, new FilePickerFileType(L.T("Todos os arquivos")) { Patterns = new[] { "*" } }
                }
            });
            if (files.Count == 0) return;
            var paths = files.Select(f => f.TryGetLocalPath() ?? throw new ArgumentException(L.T("Selecione arquivos locais."))).ToArray();
            if (keyOnly && PemService.Inspect(paths[0]).Kind != InputKind.PrivateKey) throw new ArgumentException(L.T("Selecione um arquivo de chave privada."));
            Accept(paths);
        }
        catch (Exception ex) when (IsConversionError(ex)) { SetError(ex); }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        dragEpoch++;
        bool accepted = !busy && settingsPage == null && e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
        SetDragHighlight(accepted);
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        var epoch = ++dragEpoch;
        // Child-to-child transitions are followed by an enter/over; avoid flicker.
        Dispatcher.UIThread.Post(() => { if (epoch == dragEpoch) SetDragHighlight(false); }, DispatcherPriority.Background);
    }

    private void SetDragHighlight(bool active)
    {
        if (dragActive == active) return;
        dragActive = active;
        dragOverlay.IsVisible = active;
        dropZone.Background = Brush.Parse(active ? "#EAF3FF" : "#FAFBFC");
        dropZone.BorderBrush = Brush.Parse(active ? "#3B82F6" : "#D7DDE5");
        dropZone.BoxShadow = active ? new BoxShadows(new BoxShadow { Color = Color.Parse("#663B82F6"), Blur = 24, Spread = 3 }) : default;
        prompt.Foreground = Brush.Parse(active ? "#1D4ED8" : "#1F2937");
        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        prompt.Text = dragActive ? L.T("Solte os arquivos aqui") : L.T("Arraste seus arquivos");
        hint.Text = dragActive ? L.T("Certificados e chaves serão identificados") : L.T("CRT, PEM ou KEY");
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        dragEpoch++;
        SetDragHighlight(false);
        if (busy || settingsPage != null) return;
        try { Accept(e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath() ?? throw new ArgumentException(L.T("Arraste arquivos locais."))).ToArray() ?? Array.Empty<string>()); }
        catch (Exception ex) when (IsConversionError(ex)) { SetError(ex); }
    }

    private void Accept(string[] paths) { selection.Accept(paths); password.Text = ""; status.IsVisible = false; Refresh(); }

    private void Refresh()
    {
        displayCertificates = CertificateChain.OrderDescriptions(selection.CertificateDetails);
        bool hasFiles = selection.InputPaths.Any();
        mode.Text = "CRT ⇄ PEM";
        if (busy) SetDragHighlight(false);
        emptyState.IsVisible = !hasFiles; fileList.IsVisible = hasFiles;
        fileRows.Children.Clear();
        foreach (var file in selection.Certificates) fileRows.Children.Add(FileRow(file));
        if (selection.Key != null) fileRows.Children.Add(FileRow(selection.Key));
        if (!hasFiles)
        {
            fileList.UseDefaultHeight = true;
            zone.RowDefinitions[0].Height = GridLength.Auto;
            zone.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
        }
        var certificates = DisplayCertificates;
        var current = certificates.Select(c => c.Fingerprint).ToHashSet();
        checkedCertificates.IntersectWith(current);
        exportChoices.Children.Clear();
        foreach (var certificate in certificates)
        {
            var label = new CertificateChoice(certificate).ToString();
            var check = new CheckBox
            {
                Content = new TextBlock { Text = label, FontSize = 12, LineHeight = 18, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10, 0, 0, 0), Foreground = Brush.Parse("#344054") },
                IsChecked = checkedCertificates.Contains(certificate.Fingerprint), IsEnabled = !busy,
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center, MinHeight = 36, Padding = new Thickness(4, 4)
            };
            AutomationProperties.SetName(check, label);
            check.IsCheckedChanged += (_, _) =>
            {
                if (check.IsChecked == true) checkedCertificates.Add(certificate.Fingerprint);
                else checkedCertificates.Remove(certificate.Fingerprint);
                UpdateExportControls();
            };
            exportChoices.Children.Add(check);
        }
        if (certificates.Count == 0) exportChoices.Children.Add(new TextBlock { Text = L.T("Carregue certificados para escolher o que exportar."), FontSize = 12, Foreground = Brush.Parse("#667085"), TextWrapping = TextWrapping.Wrap });
        RefreshInspector();
        select.IsEnabled = addFiles.IsEnabled = password.IsEnabled = certificatePicker.IsEnabled = !busy;
        clear.IsEnabled = hasFiles && !busy;
        UpdateExportControls();
    }

    private void UpdateExportControls()
    {
        var selected = DisplayCertificates.Where(c => checkedCertificates.Contains(c.Fingerprint)).ToArray();
        bool includesKey = KeySource != null && selected.Any(c => !c.IsCa);
        exportSummary.Text = selected.Length == 0 ? L.T("Nenhum certificado marcado") : L.F("{0} certificado(s) em um arquivo", selected.Length) + (includesKey ? L.T(" · PEM inclui chave privada") : "");
        exportOrder.Text = string.Join(" → ", selected.Select(c => new CertificateChoice(c).ToString()));
        exportOrder.IsVisible = selected.Length > 0;
        ToolTip.SetTip(exportOrder, exportOrder.Text);
        exportCrt.IsEnabled = generate.IsEnabled = selected.Length > 0 && !busy;
        saveKey.IsEnabled = KeySource != null && !busy;
        password.IsVisible = includesKey && KeySource?.IsEncrypted == true;
        ToolTip.SetTip(generate, includesKey ? L.T("Gerar PEM com os certificados marcados e a chave privada carregada.") : L.T("Gerar PEM somente com os certificados marcados."));
    }

    private async Task<string?> ChooseOutput(string extension, string suggestedName, string? label = null)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = label == null ? L.F("Salvar arquivo {0}", extension.ToUpperInvariant()) : L.F("Salvar {0}", label),
            DefaultExtension = extension, SuggestedFileName = suggestedName, ShowOverwritePrompt = true,
            FileTypeChoices = new[] { new FilePickerFileType(L.F("Arquivo {0}", extension.ToUpperInvariant())) { Patterns = new[] { "*." + extension } } }
        });
        return file == null ? null : file.TryGetLocalPath() ?? throw new ArgumentException(L.T("Escolha um destino local."));
    }

    private async Task SaveCertificates(bool pem)
    {
        var selected = checkedCertificates.ToArray();
        if (selected.Length == 0 || busy) return;
        var paths = selection.CertificatePaths.ToArray();
        var inputs = selection.InputPaths.ToArray();
        var key = pem && DisplayCertificates.Any(c => !c.IsCa && checkedCertificates.Contains(c.Fingerprint)) ? KeySource?.Path : null;
        var secret = string.IsNullOrEmpty(password.Text) ? null : password.Text;
        var source = selection.Document?.Path ?? paths[0];
        busy = true; Refresh();
        try
        {
            var content = await Task.Run(() => PemService.ExportSelected(paths, selected, key, secret));
            var extension = pem ? "pem" : "crt";
            var name = Path.GetFileNameWithoutExtension(source) + "-" + L.T("selecionados") + "." + extension;
            var output = await ChooseOutput(extension, name);
            if (output == null) { SetStatus(L.T("Salvamento cancelado.")); return; }
            PemService.SaveFiles(new[] { new ConversionOutput(output, content) }, inputs, true);
            password.Text = "";
            SetStatus(L.F("{0} salvo em {1}", extension.ToUpperInvariant(), output) + (key == null ? "" : L.T(" · Inclui a chave privada.")));
        }
        catch (Exception ex) when (IsConversionError(ex)) { SetError(ex); if (ex is PasswordRequiredException) password.Focus(); }
        finally { busy = false; Refresh(); }
    }

    private async Task SavePrivateKey()
    {
        var source = KeySource;
        if (source == null || busy) return;
        var inputs = selection.InputPaths.ToArray();
        busy = true; Refresh();
        try
        {
            var content = await Task.Run(() => PemService.ExtractPrivateKey(source.Path));
            var output = await ChooseOutput("key", Path.GetFileNameWithoutExtension(source.Path) + "-" + L.T("chave") + ".key", L.T("chave privada"));
            if (output == null) { SetStatus(L.T("Salvamento cancelado.")); return; }
            PemService.SaveFiles(new[] { new ConversionOutput(output, content) }, inputs, true);
            SetStatus(L.F("Chave privada salva em {0}", output));
        }
        catch (Exception ex) when (IsConversionError(ex)) { SetError(ex); }
        finally { busy = false; Refresh(); }
    }

    private static bool IsConversionError(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException or PasswordRequiredException or System.Security.Cryptography.CryptographicException;

    private void SetError(Exception exception)
    {
        var message = exception switch
        {
            FileNotFoundException or DirectoryNotFoundException => L.T("Arquivo ou pasta não encontrado. Carregue os arquivos novamente ou escolha outro destino."),
            UnauthorizedAccessException or IOException => L.T("Não foi possível acessar o arquivo. Verifique as permissões e se ele está em uso."),
            System.Security.Cryptography.CryptographicException => L.T("Não foi possível ler os dados criptográficos. Verifique se o arquivo é um certificado ou uma chave em formato suportado."),
            _ => exception.Message
        };
        SetStatus(message, true);
    }

    private void SetStatus(string text, bool error = false)
    {
        status.Text = text; status.IsVisible = true;
        status.Foreground = Brush.Parse(error ? "#B42318" : "#344054"); ToolTip.SetTip(status, text);
    }
}
