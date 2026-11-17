// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel and CertBox contributors.
// Developed with AI; third-party components retain their original licenses.

using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace CertBox;

public sealed class SettingsPage : Border
{
    private readonly SettingsStore settings;
    private readonly bool firstRun;
    private readonly List<Action> translations = new();
    public ComboBox LanguagePicker { get; } = new() { ItemsSource = L.LanguageNames, MinWidth = 220, HorizontalAlignment = HorizontalAlignment.Stretch };
    public Button SaveButton { get; } = ActionButton(true);
    public Button BackButton { get; } = ActionButton(false);
    public Button ContinueWithoutSaving { get; } = ActionButton(false);
    private readonly TextBlock saveError = new() { Foreground = Brush.Parse("#B42318"), TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly TextBlock linkError = new() { Foreground = Brush.Parse("#B42318"), TextWrapping = TextWrapping.Wrap, IsVisible = false };
    public event Action<string>? Completed;
    public event Action? Canceled;
    private string SelectedLanguage => L.Languages[Math.Max(0, LanguagePicker.SelectedIndex)];

    public SettingsPage(SettingsStore settings, bool firstRun, string? sessionLanguage = null)
    {
        this.settings = settings;
        this.firstRun = firstRun;
        Background = Brushes.White;
        Padding = new Thickness(24, 20);
        LanguagePicker.SelectedIndex = Array.IndexOf(L.Languages, sessionLanguage ?? settings.Language);
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 20 };
        var header = new StackPanel { Spacing = 8 };
        header.Children.Add(Text(firstRun ? "Escolha seu idioma" : "Configurações", 23, true));
        header.Children.Add(Text("Você pode alterar o idioma nas configurações a qualquer momento.", 12));
        layout.Children.Add(header);

        var body = new StackPanel { Spacing = 12, MaxWidth = 650, HorizontalAlignment = HorizontalAlignment.Stretch };
        body.Children.Add(Text("Idioma", 14, true));
        body.Children.Add(LanguagePicker);
        body.Children.Add(Text("Preferência salva para este usuário neste computador.", 11));
        LanguagePicker.SelectionChanged += (_, _) => UpdateLanguage();
        var license = new StackPanel { Spacing = 10, Margin = new Thickness(0, 20, 0, 0), IsVisible = !firstRun };
        license.Children.Add(Text("Sobre o CertBox", 16, true));
        var version = Text("Versão", 12);
        translations.Add(() => version.Text = "CertBox " + ProjectInfo.Version + " · " + ProjectInfo.PlatformName);
        license.Children.Add(version);
        license.Children.Add(Text("Inspeção e exportação local de certificados X.509 e chaves privadas.", 12));
        license.Children.Add(Text("Código, interface e ícone produzidos com uso de IA. As bibliotecas de terceiros mantêm sua autoria e suas licenças.", 12));
        license.Children.Add(new TextBlock { Text = ProjectInfo.Copyright, FontSize = 11, Foreground = Brush.Parse("#667085"), TextWrapping = TextWrapping.Wrap });
        var links = new WrapPanel { IsVisible = ProjectInfo.Repository != null };
        links.Children.Add(LinkButton("Projeto", ProjectInfo.Repository));
        links.Children.Add(LinkButton("Código-fonte desta versão", ProjectInfo.Source));
        links.Children.Add(LinkButton("Suporte", ProjectInfo.Support));
        links.Children.Add(LinkButton("Versões", ProjectInfo.Releases));
        license.Children.Add(links); license.Children.Add(linkError);
        license.Children.Add(Document("O que o CertBox verifica", () => Documents.Read("HELP", SelectedLanguage)));
        license.Children.Add(Document("Privacidade", () => Documents.Read("PRIVACY", SelectedLanguage)));
        license.Children.Add(Text("Licença", 16, true));
        license.Children.Add(new TextBlock { Text = ProjectInfo.LicenseId, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#344054") });
        license.Children.Add(Text("Software livre: você pode usar, modificar e redistribuir o CertBox sob a GPL versão 3 ou posterior. Distribuído sem garantia, nos termos da licença.", 12));
        license.Children.Add(Document("Texto integral da GPLv3", () => Documents.Read("LICENSE")));
        var thirdParty = new Expander { HorizontalAlignment = HorizontalAlignment.Stretch };
        translations.Add(() => thirdParty.Header = Tr("Licenças de terceiros"));
        var noticePanel = new StackPanel { Spacing = 8 };
        noticePanel.Children.Add(Text("Os textos originais das licenças são preservados no idioma de seus autores.", 11));
        var notice = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            MinHeight = 170, MaxHeight = 270, FontSize = 11, FontFamily = new FontFamily("Consolas, monospace"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        thirdParty.PropertyChanged += (_, e) =>
        {
            if (e.Property == Expander.IsExpandedProperty && thirdParty.IsExpanded && string.IsNullOrEmpty(notice.Text))
                notice.Text = ReadThirdPartyNotices();
        };
        translations.Add(() => AutomationProperties.SetName(notice, Tr("Licenças de terceiros")));
        noticePanel.Children.Add(notice);
        thirdParty.Content = noticePanel;
        license.Children.Add(thirdParty);
        body.Children.Add(license);
        var scroll = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); layout.Children.Add(scroll);

        var footer = new StackPanel { Spacing = 10 };
        footer.Children.Add(saveError);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        translations.Add(() => SaveButton.Content = Tr(firstRun ? "Continuar" : "Salvar"));
        translations.Add(() => BackButton.Content = Tr("Voltar"));
        translations.Add(() => ContinueWithoutSaving.Content = Tr("Continuar sem salvar"));
        BackButton.IsVisible = !firstRun;
        ContinueWithoutSaving.IsVisible = false;
        SaveButton.Click += (_, _) =>
        {
            if (settings.Save(SelectedLanguage)) Completed?.Invoke(SelectedLanguage);
            else { saveError.IsVisible = ContinueWithoutSaving.IsVisible = true; UpdateLanguage(); }
        };
        BackButton.Click += (_, _) => Canceled?.Invoke();
        ContinueWithoutSaving.Click += (_, _) => Completed?.Invoke(SelectedLanguage);
        buttons.Children.Add(BackButton); buttons.Children.Add(ContinueWithoutSaving); buttons.Children.Add(SaveButton);
        footer.Children.Add(buttons);
        var ai = Text("Desenvolvido com IA", 10); ai.HorizontalAlignment = HorizontalAlignment.Center;
        footer.Children.Add(ai);
        Grid.SetRow(footer, 2); layout.Children.Add(footer);
        Child = layout;
        UpdateLanguage();
    }

    private string Tr(string text) => L.In(SelectedLanguage, text);

    private Button LinkButton(string key, Uri? destination)
    {
        var button = ActionButton(false); button.Margin = new Thickness(0, 0, 8, 8);
        translations.Add(() => { button.Content = Tr(key); AutomationProperties.SetName(button, Tr(key)); });
        button.Click += async (_, _) =>
        {
            if (destination == null) return;
            try
            {
                var top = TopLevel.GetTopLevel(this);
                if (top == null || !await top.Launcher.LaunchUriAsync(destination)) ShowLinkError();
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception) { ShowLinkError(); }
        };
        return button;
    }

    private void ShowLinkError() { linkError.IsVisible = true; linkError.Text = Tr("Não foi possível abrir o navegador. O endereço do projeto está no README."); }

    private Expander Document(string key, Func<string> read)
    {
        var expander = new Expander { HorizontalAlignment = HorizontalAlignment.Stretch };
        var text = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            MinHeight = 160, MaxHeight = 260, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        translations.Add(() =>
        {
            expander.Header = Tr(key);
            AutomationProperties.SetName(text, Tr(key));
            if (expander.IsExpanded) text.Text = read();
        });
        expander.PropertyChanged += (_, e) => { if (e.Property == Expander.IsExpandedProperty && expander.IsExpanded) text.Text = read(); };
        expander.Content = text;
        return expander;
    }
    private TextBlock Text(string key, double size, bool bold = false)
    {
        var text = new TextBlock { FontSize = size, FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
            TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse(bold ? "#1F2937" : "#667085") };
        translations.Add(() => text.Text = Tr(key));
        return text;
    }

    private void UpdateLanguage()
    {
        foreach (var update in translations) update();
        AutomationProperties.SetName(LanguagePicker, Tr("Idioma"));
        saveError.Text = Tr("Não foi possível salvar o idioma. A escolha valerá apenas nesta sessão se você continuar sem salvar.");
    }

    private static Button ActionButton(bool primary) => new()
    {
        Height = 36, MinWidth = 95, Padding = new Thickness(16, 0), CornerRadius = new CornerRadius(4),
        HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        Background = Brush.Parse(primary ? "#2863AF" : "#F2F4F7"), Foreground = primary ? Brushes.White : Brush.Parse("#344054")
    };

    public static string ReadThirdPartyNotices()
    {
        var assembly = typeof(SettingsPage).Assembly;
        var result = new StringBuilder();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("CertBox.Licenses.", StringComparison.Ordinal)).Order())
        {
            result.AppendLine(name["CertBox.Licenses.".Length..]).AppendLine(new string('=', 60));
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            result.AppendLine(reader.ReadToEnd()).AppendLine();
        }
        return result.ToString();
    }
}
