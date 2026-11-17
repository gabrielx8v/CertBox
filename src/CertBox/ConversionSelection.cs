// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel and CertBox contributors.
// Developed with AI; third-party components retain their original licenses.

namespace CertBox;

public enum ConversionDirection { ToPem, FromPem }

/// <summary>Selection logic independent of the desktop UI.</summary>
public sealed class ConversionSelection
{
    private List<InputInfo> certificates = new();
    public IReadOnlyList<InputInfo> Certificates => certificates;
    public InputInfo? Document => certificates.FirstOrDefault(f => f.Certificates.Any(c => !c.IsCa)) ?? certificates.FirstOrDefault();
    public InputInfo? Key { get; private set; }
    public ConversionDirection Direction { get; private set; } = ConversionDirection.ToPem;
    public IEnumerable<string> CertificatePaths => certificates.Select(c => c.Path);
    public IEnumerable<string> InputPaths => Key == null ? CertificatePaths : CertificatePaths.Append(Key.Path);
    public IReadOnlyList<CertificateInfo> CertificateDetails => certificates.SelectMany(f => f.Certificates).DistinctBy(c => c.Fingerprint).ToArray();
    public bool CanConvert => CertificateDetails.Count(c => !c.IsCa) == 1;
    public bool NeedsPassword => Direction == ConversionDirection.ToPem && Key?.IsEncrypted == true;

    public void Clear() { certificates.Clear(); Key = null; Direction = ConversionDirection.ToPem; }

    public void Remove(string path)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        certificates.RemoveAll(file => string.Equals(file.Path, path, comparison));
        if (string.Equals(Key?.Path, path, comparison)) Key = null;
        if (certificates.Count == 0 && Key == null) Direction = ConversionDirection.ToPem;
    }

    public void Accept(IEnumerable<string> paths)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var incomingPaths = paths.Distinct(comparer).ToArray();
        if (incomingPaths.Length is < 1 or > 33) throw new ArgumentException(L.T("Selecione até 32 arquivos de certificados e uma chave privada."));
        var incoming = incomingPaths.Select(PemService.Inspect).ToArray();
        var bundles = incoming.Where(f => f.Kind == InputKind.Bundle).ToArray();
        if (bundles.Length > 0)
        {
            if (incoming.Length != 1) throw new ArgumentException(L.T("Selecione um PEM com chave sozinho para extrair seus componentes."));
            Validate(new[] { bundles[0] });
            certificates = new() { bundles[0] }; Key = null; Direction = ConversionDirection.FromPem;
            return;
        }
        var keys = incoming.Where(f => f.Kind == InputKind.PrivateKey).ToArray();
        if (keys.Length > 1) throw new ArgumentException(L.T("Selecione uma única chave privada."));
        if (Document?.Kind == InputKind.Bundle && incoming.All(f => f.Kind == InputKind.PrivateKey))
            throw new ArgumentException(L.T("O PEM já contém uma chave. Limpe a seleção para iniciar outra combinação."));

        var next = Document?.Kind == InputKind.Bundle ? new List<InputInfo>() : new List<InputInfo>(certificates);
        var nextKey = Document?.Kind == InputKind.Bundle ? null : Key;
        bool wasEmpty = next.Count == 0 && nextKey == null;
        foreach (var file in incoming.Where(f => f.Kind == InputKind.Certificate))
        {
            next.RemoveAll(existing => comparer.Equals(existing.Path, file.Path));
            next.Add(file);
        }
        if (keys.Length == 1) nextKey = keys[0];
        Validate(next);
        var direction = (wasEmpty || Direction == ConversionDirection.FromPem) && incoming.Length == 1 && nextKey == null && next.Count == 1
            && next[0].Certificates.Any(c => !c.IsCa) && Path.GetExtension(next[0].Path).Equals(".pem", StringComparison.OrdinalIgnoreCase)
            ? ConversionDirection.FromPem : ConversionDirection.ToPem;
        certificates = next; Key = nextKey; Direction = direction;
    }

    private static void Validate(IEnumerable<InputInfo> files)
    {
        var inputs = files.ToArray();
        if (inputs.Length > 32) throw new ArgumentException(L.T("O limite é de 32 arquivos de certificados."));
        var all = inputs.SelectMany(f => f.Certificates).DistinctBy(c => c.Fingerprint).ToArray();
        if (all.Length > 128) throw new ArgumentException(L.T("O limite é de 128 certificados por conversão."));
        if (all.Count(c => !c.IsCa) > 1) throw new ArgumentException(L.T("Há mais de um certificado de domínio. Use apenas um domínio com sua cadeia de CAs."));
    }
}
