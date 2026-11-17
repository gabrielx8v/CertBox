// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel and CertBox contributors.
// Developed with AI; third-party components retain their original licenses.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using Avalonia.Interactivity;
using CertBox;

internal static class Program
{
    private static readonly List<string> passed = new();

    // This runner is placed beside the *published* assemblies and runtime.
    // It never opens a window and is never included in the distributed app.
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var fixtures = args[0]; var output = args[1]; var report = args[2];
            Require(ProjectInfo.Repository?.AbsoluteUri == "https://github.com/gabrielx8v/CertBox"
                && ProjectInfo.Support?.AbsoluteUri == "https://github.com/gabrielx8v/CertBox/issues",
                "Links reais do projeto incorporados");
            string Fixture(string file) => Path.Combine(fixtures, file);
            var bundle = Fixture("haproxy-com-raiz.pem");
            var info = PemService.Inspect(bundle);
            Require(info.Certificates.Count == 4 && info.Kind == InputKind.Bundle, "Inspeção de pacote com domínio, duas intermediárias, raiz e chave");
            var ordered = CertificateChain.OrderDescriptions(info.Certificates);
            var domain = ordered.First(c => !c.IsCa);
            Require(domain.AdditionalFields.Count > 0 && domain.DnsNames.Count > 0, "Campos dinâmicos e SAN");
            foreach (var certificate in ordered)
            {
                var content = PemService.ExportSelected(new[] { bundle }, new[] { certificate.Fingerprint });
                var path = Path.Combine(output, certificate.Fingerprint + ".crt");
                PemService.SaveFiles(new[] { new ConversionOutput(path, content) }, new[] { bundle }, true);
                var saved = PemService.Inspect(path);
                Require(saved.Certificates.Count == 1 && saved.Certificates[0].Fingerprint == certificate.Fingerprint, certificate.Role + " exportada individualmente");
            }
            var intermediateIds = ordered.Where(c => c.IsCa && !c.IsRoot).Select(c => c.Fingerprint).ToArray();
            var caPath = Path.Combine(output, "intermediarias.crt");
            PemService.SaveFiles(new[] { new ConversionOutput(caPath, PemService.ExportSelected(new[] { bundle }, intermediateIds)) }, new[] { bundle }, true);
            Require(PemService.Inspect(caPath).Certificates.Count == 2, "Agrupamento de duas intermediárias");
            var selected = new[] { domain.Fingerprint };
            foreach (var source in new[] { bundle, Fixture("haproxy-protegido.pem") })
            {
                var pemPath = Path.Combine(output, Path.GetFileName(source)!);
                var pem = PemService.ExportSelected(new[] { source }, selected, source, "teste123");
                PemService.SaveFiles(new[] { new ConversionOutput(pemPath, pem) }, new[] { source }, true);
                var saved = PemService.Inspect(pemPath);
                Require(saved.Kind == InputKind.Bundle && saved.Certificates.Count == 1, "PEM com chave: " + Path.GetFileName(source));
            }
            foreach (var key in new[] { "protegida.der", "protegida.key", "ec.key", "chave com espaço.key", "dsa.key", "dsa.der" })
            {
                var path = Path.Combine(output, key + ".key");
                PemService.SaveFiles(new[] { new ConversionOutput(path, PemService.ExtractPrivateKey(Fixture(key))) }, Array.Empty<string>(), true);
                Require(PemService.Inspect(path).Kind == InputKind.PrivateKey, "Chave preservada: " + key);
            }
            Require(PemService.Inspect(Fixture("certificado.der")).Certificates.Count == 1, "Certificado DER");
            Require(PemService.Inspect(Fixture("ec.crt")).Certificates[0].PublicKey == "EC · 256 bits", "Certificado EC");

            App.CreateBuilder().SetupWithLifetime(new ClassicDesktopStyleApplicationLifetime());
            var preferencesFolder = Path.Combine(output, "preferences");
            var preferences = new SettingsStore(preferencesFolder, "pt");
            var window = new MainWindow(preferences);
            Invoke(window, "Accept", new object[] { new[] { bundle } });
            var root = (Control)window.Content!; window.Content = null;
            root.Measure(new Size(960, 600)); root.Arrange(new Rect(0, 0, 960, 600));
            foreach (var presenter in root.GetVisualDescendants().OfType<ContentPresenter>().ToArray()) presenter.UpdateChild();
            var choices = Field<StackPanel>(window, "exportChoices").Children.OfType<CheckBox>().ToArray();
            Require(choices.Length == 4 && choices.All(c => c.IsChecked == false), "Checkboxes e ausência de seleção automática");
            var picker = Field<ComboBox>(window, "certificatePicker"); picker.SelectedIndex = 3;
            Require(choices.All(c => c.IsChecked == false), "Seletor de dados independente");
            choices[1].IsChecked = true;
            Require(Field<Button>(window, "exportCrt").IsEnabled, "Checkbox habilita exportação");
            var data = Field<StackPanel>(window, "certificateData");
            Require(data.Children.Count > 0, "Painel de detalhes construído com tema publicado");
            Require(window.StorageProvider != null, "Serviço nativo de arquivos disponível");
            foreach (var control in new Avalonia.Controls.Primitives.TemplatedControl[] { choices[0], picker, Field<Button>(window, "exportCrt"), Field<TextBox>(window, "password") })
            {
                control.ApplyTemplate();
                Require(control.Template != null && control.GetVisualDescendants().Any(), "Template publicado: " + control.GetType().Name);
            }
            Require(window.Icon != null, "Ícone incorporado na publicação compactada");
            window.Content = root;
            var selectedBefore = Field<HashSet<string>>(window, "checkedCertificates").ToArray();
            foreach (var language in new[] { "en", "es", "pt" })
            {
                Invoke(window, "OpenSettings", new object[] { !preferences.HasSavedLanguage });
                var page = Field<SettingsPage>(window, "settingsPage");
                page.LanguagePicker.SelectedIndex = Array.IndexOf(L.Languages, language);
                var labels = page.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
                Require(labels.Contains("GPL-3.0-or-later") && labels.Contains(L.In(language, "Desenvolvido com IA")), "Licença e declaração de IA na interface publicada: " + language);
                page.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(L.Language == language && new SettingsStore(preferencesFolder, "xx").Language == language,
                    "Preferência gravada e recarregada com runtime reduzido: " + language);
                Require(Field<HashSet<string>>(window, "checkedCertificates").Count == selectedBefore.Length,
                    "Seleção preservada na publicação: " + language);
                var content = PemService.ExportSelected(new[] { bundle }, new[] { domain.Fingerprint });
                Require(!content.Contains("PRIVATE KEY") && content.Contains("CERTIFICATE"), "CRT sem chave em " + language);
                Require(Documents.Read("PRIVACY", language).Contains("%LOCALAPPDATA%")
                    && Documents.Read("HELP", language).Contains("TLS"), "Privacidade e ajuda incorporadas: " + language);
            }
            Require(Documents.Read("LICENSE").Length > 30000 && Documents.Read("LICENSE").Contains("GNU GENERAL PUBLIC LICENSE"), "Texto integral da GPL preservado na publicação");
            Require(SettingsPage.ReadThirdPartyNotices().Contains("SIL OPEN FONT LICENSE"), "Avisos legais preservados pelo trimmer");
            if (args[3] == "linux-x64")
            {
                Require(SettingsPage.ReadThirdPartyNotices().Contains("Tom Deseyn")
                    && new SettingsStore().FilePath.EndsWith("/CertBox/settings.json", StringComparison.Ordinal),
                    "Avisos D-Bus e caminho de preferências Linux");
            }
            else
            {
                Require(new SettingsStore().FilePath == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CertBox", "settings.json"),
                    "Preferências Windows mantidas em LocalAppData");
            }
            SaveReport(report, "PASS\n" + string.Join("\n", passed));
            return 0;
        }
        catch (Exception ex)
        {
            SaveReport(args[2], "FAIL\n" + string.Join("\n", passed) + "\n" + ex.ToString());
            return 1;
        }
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        passed.Add(description);
    }
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    private static void Invoke(object instance, string name, object[] args) => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, args);
    private static void SaveReport(string path, string text) => PemService.SaveFiles(new[] { new ConversionOutput(path, text) }, Array.Empty<string>(), true);
}
