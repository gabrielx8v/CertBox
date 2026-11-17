// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel and CertBox contributors.
// Developed with AI; third-party components retain their original licenses.

using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using Avalonia.Interactivity;
using Avalonia.Input;
using CertBox;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Exercise control templates and layout without opening a window.
        App.CreateBuilder().SetupWithoutStarting();
        var fixtures = Path.GetFullPath(args.Length > 0 ? args[0] : "work/test-fixtures");
        var settingsFolder = Path.Combine(fixtures, "ui-settings-" + Guid.NewGuid().ToString("N"));
        var preferences = new SettingsStore(settingsFolder, "pt-BR");
        var window = new MainWindow(preferences);
        var files = new[] { "dominio.crt", "intermediaria-baixa.crt", "intermediaria-alta.crt", "raiz.crt" }
            .Select(file => Path.Combine(fixtures, file)).ToArray();
        Invoke(window, "Accept", new object[] { files });
        var root = (Control)window.Content!;
        window.Content = null;
        Layout(root);
        var list = (ScrollViewer)Field(window, "fileList");
        list.ApplyTemplate();
        Layout(root);
        Require(list.Template != null, "lista recebe o template de ScrollViewer");
        Require(list.GetVisualDescendants().OfType<ScrollContentPresenter>().Any(), "lista possui apresentador de rolagem");
        Require(list.GetVisualDescendants().OfType<ScrollBar>().Count() == 2, "template inclui as barras de rolagem");
        Require(window.Title == "CertBox", "novo nome aplicado à janela");
        var workspace = (Grid)Field(window, "workspace");
        Require(workspace.ColumnDefinitions.Count == 3 && workspace.Children.OfType<GridSplitter>().Any(), "duas colunas com divisória vertical");
        var choices = (StackPanel)Field(window, "exportChoices");
        var boxes = choices.Children.OfType<CheckBox>().ToArray();
        Require(boxes.Length == 4 && boxes.All(c => c.IsChecked == false), "todos os certificados começam desmarcados");
        Require(!((Button)Field(window, "exportCrt")).IsEnabled && !((Button)Field(window, "generate")).IsEnabled, "exportação começa desabilitada até marcar certificados");
        var picker = (ComboBox)Field(window, "certificatePicker");
        var inspected = Field(window, "selectedFingerprint");
        boxes[1].IsChecked = true;
        Require(Equals(inspected, Field(window, "selectedFingerprint")), "marcar exportação não altera certificado em visualização");
        var checkedIds = (HashSet<string>)Field(window, "checkedCertificates");
        var before = checkedIds.Order().ToArray();
        picker.SelectedIndex = 3;
        Require(before.SequenceEqual(checkedIds.Order()), "visualizar raiz não altera caixas de exportação");
        Require(((Button)Field(window, "exportCrt")).IsEnabled && ((Button)Field(window, "generate")).IsEnabled, "CAs selecionadas podem gerar CRT ou PEM");
        Require(!((Button)Field(window, "saveKey")).IsEnabled, "salvar chave desabilitado quando não há chave");
        var crt = (Button)Field(window, "exportCrt"); var key = (Button)Field(window, "saveKey"); var pem = (Button)Field(window, "generate");
        Require(ReferenceEquals(crt.Parent, key.Parent) && ReferenceEquals(key.Parent, pem.Parent) && crt.Parent is Grid,
            "três botões no mesmo grid para compartilhar alinhamento");
        var actions = (Grid)crt.Parent!;
        actions.Measure(new Size(400, double.PositiveInfinity));
        actions.Arrange(new Rect(0, 0, 400, actions.DesiredSize.Height));
        Require(crt.Bounds.Height == 38 && key.Bounds.Height == 38 && pem.Bounds.Height == 38
            && crt.Bounds.Left == key.Bounds.Left && crt.Bounds.Right == pem.Bounds.Right
            && key.Bounds.Top == pem.Bounds.Top && key.Bounds.Bottom == pem.Bounds.Bottom,
            "botões com alturas e bordas alinhadas após o layout");
        foreach (var width in new[] { 320d, 400d, 501d })
        {
            actions.Measure(new Size(width, double.PositiveInfinity));
            actions.Arrange(new Rect(0, 0, width, actions.DesiredSize.Height));
            Require(Math.Abs(key.Bounds.Width - pem.Bounds.Width) <= 1
                && Math.Abs((key.Bounds.Right + pem.Bounds.Left) / 2 - (crt.Bounds.Left + crt.Bounds.Right) / 2) <= .5,
                "botões de mesma largura e divisão centralizada em " + width + " px");
        }
        Require(boxes.All(c => c.Content is TextBlock text && text.Margin.Left >= 10 && text.LineHeight == 18 && c.MinHeight >= 36),
            "texto separado do checkbox e linhas com espaço para leitura");
        foreach (var box in boxes) box.IsChecked = false;
        Require(!((Button)Field(window, "exportCrt")).IsEnabled && !((Button)Field(window, "generate")).IsEnabled, "exportação desabilitada sem certificados marcados");
        Invoke(window, "Accept", new object[] { new[] { Path.Combine(fixtures, "haproxy-com-raiz.pem") } });
        Require(((Button)Field(window, "saveKey")).IsEnabled, "chave de um pacote PEM reconhecida para exportação separada");
        Require(choices.Children.OfType<CheckBox>().All(c => c.IsChecked == false), "carregar pacote PEM não marca nenhuma caixa");
        var selection = (ConversionSelection)Field(window, "selection"); selection.Clear();
        Invoke(window, "Refresh", Array.Empty<object>());
        Invoke(window, "Accept", new object[] { new[] { Path.Combine(fixtures, "raiz.crt") } });
        Require(choices.Children.OfType<CheckBox>().Single().IsChecked == false, "raiz carregada sozinha também começa desmarcada");
        Require(window.Icon != null, "ícone aprovado aplicado à janela");
        var gear = (Button)Field(window, "settingsButton");
        Require(gear.Content is TextBlock { Text: "⚙" } or PathIcon { Data: not null }, "engrenagem ocupa o cabeçalho no lugar do nome");
        // Restore the root so settings navigation is exercised in its real host.
        window.Content = root;
        var mainLayout = (Grid)Field(window, "mainLayout");
        Invoke(window, "OpenSettings", new object[] { true });
        var onboarding = (SettingsPage)Field(window, "settingsPage");
        Require(!mainLayout.IsVisible && !onboarding.BackButton.IsVisible, "primeira abertura oferece escolha de idioma na própria janela");
        onboarding.LanguagePicker.SelectedIndex = 1;
        Require(Equals(onboarding.SaveButton.Content, "Continue"), "seletor traduz a tela inicial antes de salvar");
        onboarding.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(mainLayout.IsVisible && preferences.HasSavedLanguage && new SettingsStore(settingsFolder, "pt").Language == "en", "continuar salva idioma e retorna ao programa");
        Require(Equals(crt.Content, "Export CRT") && Equals(key.Content, "Save key (.key)")
            && Equals(((Button)Field(window, "addFiles")).Content, "Add files"), "botões estáticos mudam imediatamente para inglês");
        Require(picker.SelectedItem!.ToString()!.StartsWith("Root CA"), "papéis dinâmicos acompanham o idioma");
        Invoke(window, "Accept", new object[] { new[] { Path.Combine(fixtures, "haproxy-protegido.pem") } });
        choices.Children.OfType<CheckBox>().First().IsChecked = true;
        picker.SelectedIndex = 2;
        var selectedIds = checkedIds.Order().ToArray();
        var inspectedId = Field(window, "selectedFingerprint");
        var secret = (TextBox)Field(window, "password"); secret.Text = "temporary-test-secret";
        gear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var settingsPage = (SettingsPage)Field(window, "settingsPage");
        settingsPage.LanguagePicker.SelectedIndex = 2;
        Require(Equals(settingsPage.SaveButton.Content, "Guardar"), "configurações oferecem espanhol");
        settingsPage.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(Equals(key.Content, "Guardar clave (.key)") && L.Language == "es", "espanhol aplicado ao salvar");
        Require(checkedIds.Order().SequenceEqual(selectedIds) && Equals(inspectedId, Field(window, "selectedFingerprint"))
            && secret.Text == "temporary-test-secret" && selection.Certificates.Single().Kind == InputKind.Bundle,
            "trocar idioma preserva seleção, certificado visualizado, senha e arquivos");
        var detailLabels = ((StackPanel)Field(window, "certificateData")).GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
        Require(detailLabels.Contains("Emisor") && detailLabels.Contains("Uso de la clave"), "campos fixos e dinâmicos traduzidos sem reler o certificado");
        var badges = ((StackPanel)Field(window, "fileRows")).GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
        Require(badges.Contains("Dominio") && badges.Contains("CA intermedia") && badges.Contains("Clave privada"), "etiquetas de arquivos traduzidas");
        gear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        settingsPage = (SettingsPage)Field(window, "settingsPage"); settingsPage.LanguagePicker.SelectedIndex = 0;
        settingsPage.BackButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(L.Language == "es" && mainLayout.IsVisible && new SettingsStore(settingsFolder).Language == "es", "voltar cancela mudança de idioma");
        var notices = SettingsPage.ReadThirdPartyNotices();
        Require(notices.Contains("SIL OPEN FONT LICENSE") && notices.Contains("FreeType") && notices.Contains("The MIT License"), "licenças externas acessíveis sem arquivos adicionais");
        var blocked = Path.Combine(settingsFolder, "blocked"); File.WriteAllText(blocked, "blocked");
        var failedSettings = new SettingsPage(new SettingsStore(blocked, "pt"), true);
        bool continued = false; failedSettings.Completed += language => continued = language == "en";
        failedSettings.LanguagePicker.SelectedIndex = 1;
        failedSettings.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(failedSettings.ContinueWithoutSaving.IsVisible, "falha ao salvar oferece continuar sem persistência");
        failedSettings.ContinueWithoutSaving.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(continued, "programa continua utilizável quando a pasta de dados não permite gravação");
        foreach (var language in L.Languages)
        {
            Invoke(window, "ApplyLanguage", new object[] { language });
            var mainTexts = root.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
            Require(mainTexts.Contains(L.T("Desenvolvido com IA")), "declaração de IA no rodapé em " + language);
            gear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var about = (SettingsPage)Field(window, "settingsPage");
            var settingsTexts = about.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
            Require(settingsTexts.Contains("CertBox " + ProjectInfo.Version + " · " + ProjectInfo.PlatformName)
                && settingsTexts.Contains(ProjectInfo.LicenseId)
                && settingsTexts.Contains(L.T("Desenvolvido com IA")), "Sobre, versão, licença e declaração de IA em " + language);
            var expanders = about.GetLogicalDescendants().OfType<Expander>().ToArray();
            var privacy = expanders.Single(e => Equals(e.Header, L.T("Privacidade"))); privacy.IsExpanded = true;
            Require(privacy.Content is TextBox { Text: { } policy } && policy.Contains("%LOCALAPPDATA%")
                && policy.Contains(language == "en" ? "AI" : "IA"), "política local incorporada em " + language);
            var license = expanders.Single(e => Equals(e.Header, L.T("Texto integral da GPLv3"))); license.IsExpanded = true;
            Require(license.Content is TextBox { Text: { } fullLicense } && fullLicense.Length > 30000
                && fullLicense.Contains("GNU GENERAL PUBLIC LICENSE") && fullLicense.Contains("WITHOUT ANY WARRANTY"), "GPL integral acessível em " + language);
            about.BackButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        L.Use("pt");
        Console.WriteLine("Verificações da interface passaram. Nenhuma janela foi exibida.");
    }

    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Invoke(object target, string name, object[] arguments) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, arguments);
    private static void Require(bool condition, string description)
    {
        if (!condition) throw new Exception("Falhou: " + description);
        Console.WriteLine("OK " + description);
    }
    private static void Layout(Control root)
    {
        root.InvalidateMeasure();
        root.Measure(new Size(960, 600));
        root.Arrange(new Rect(0, 0, 960, 600));
        Dispatcher.UIThread.RunJobs();
    }
}
