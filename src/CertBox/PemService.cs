// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel and CertBox contributors.
// Developed with AI; third-party components retain their original licenses.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CertBox;

public enum InputKind { Certificate, PrivateKey, Bundle }
public sealed record InputInfo(string Path, InputKind Kind, bool IsEncrypted, IReadOnlyList<CertificateInfo> Certificates);
public sealed record PemParts(string Certificates, string? PrivateKey, string DomainCertificate, string? CaCertificates, string? Warning);
public sealed record ConversionOutput(string Path, string Content);

public sealed class PasswordRequiredException : Exception
{
    public PasswordRequiredException() : base(L.T("A chave está protegida. Informe a senha da chave.")) { }
}

public static class PemService
{
    private const int MaxFileBytes = 16 * 1024 * 1024;

    public static byte[] ReadInput(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length > MaxFileBytes) throw new ArgumentException(L.T("O arquivo excede o limite de 16 MB."));
        using var memory = new MemoryStream();
        file.CopyTo(memory);
        return memory.ToArray();
    }

    public static bool IsCertificate(string path)
    {
        try
        {
            var certificates = ReadCertificates(ReadInput(path));
            foreach (var certificate in certificates) certificate.Dispose();
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException) { return false; }
    }

    public static InputInfo Inspect(string path)
    {
        path = Path.GetFullPath(path);
        var data = ReadInput(path);
        var blocks = ReadPem(data);
        if (blocks.Count > 0)
        {
            var certificates = blocks.Where(b => b.Label == "CERTIFICATE").ToList();
            if (certificates.Count > 128) throw new ArgumentException(L.T("O limite é de 128 certificados por conversão."));
            var keys = blocks.Where(b => IsKeyLabel(b.Label)).ToList();
            if (keys.Count > 1 || certificates.Count + keys.Count != blocks.Count)
                throw new ArgumentException(L.T("Use certificados X.509 e uma única chave privada em formato suportado."));
            var descriptions = new List<CertificateInfo>();
            foreach (var certificate in certificates)
                using (var loaded = X509CertificateLoader.LoadCertificate(certificate.Bytes)) descriptions.Add(CertificateChain.Describe(loaded));
            var encrypted = keys.Count == 1 && keys[0].Label == "ENCRYPTED PRIVATE KEY";
            if (keys.Count == 1)
            {
                if (encrypted)
                {
                    if (!LooksEncryptedDer(keys[0].Bytes)) throw new ArgumentException(L.T("Chave criptografada inválida."));
                }
                else
                {
                    var (key, _) = ReadKey(Encoding.UTF8.GetBytes(PemEncoding.WriteString(keys[0].Label, keys[0].Bytes)), null);
                    key.Dispose();
                }
            }
            return new InputInfo(path, certificates.Count > 0
                ? (keys.Count > 0 ? InputKind.Bundle : InputKind.Certificate)
                : InputKind.PrivateKey, encrypted, descriptions);
        }
        X509Certificate2? derCertificate = null;
        try { derCertificate = X509CertificateLoader.LoadCertificate(data); }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException) { }
        if (derCertificate != null)
        {
            using (derCertificate)
                return new InputInfo(path, InputKind.Certificate, false, new[] { CertificateChain.Describe(derCertificate) });
        }
        if (LooksEncryptedDer(data)) return new InputInfo(path, InputKind.PrivateKey, true, Array.Empty<CertificateInfo>());
        var (privateKey, _) = ReadKey(data, null);
        privateKey.Dispose();
        return new InputInfo(path, InputKind.PrivateKey, false, Array.Empty<CertificateInfo>());
    }

    private static bool IsKeyLabel(string label) => label is "PRIVATE KEY" or "ENCRYPTED PRIVATE KEY" or "RSA PRIVATE KEY" or "EC PRIVATE KEY";

    public static string ToPem(string certificatePath, string? keyPath = null, string? password = null) => ToPem(new[] { certificatePath }, keyPath, password);

    public static string ToPem(IEnumerable<string> certificatePaths, string? keyPath = null, string? password = null)
    {
        var certificates = LoadCertificates(certificatePaths);
        try
        {
            if (keyPath == null) return JoinCertificates(CertificateChain.Order(certificates));
            var (key, pem) = ReadKey(ReadInput(keyPath), password);
            using (key) return pem.Replace("\r\n", "\n") + "\n" + JoinCertificates(CertificateChain.Order(certificates, key));
        }
        finally { foreach (var certificate in certificates) certificate.Dispose(); }
    }

    public static ChainSummary Analyze(IEnumerable<string> certificatePaths)
    {
        var certificates = LoadCertificates(certificatePaths);
        try { return CertificateChain.Summarize(CertificateChain.Order(certificates)); }
        finally { foreach (var certificate in certificates) certificate.Dispose(); }
    }

    private static List<X509Certificate2> LoadCertificates(IEnumerable<string> paths, bool allowBundles = false)
    {
        var result = new List<X509Certificate2>();
        try
        {
            var files = paths.ToArray();
            if (files.Length is < 1 or > 32) throw new ArgumentException(L.T("Selecione entre 1 e 32 arquivos de certificados."));
            foreach (var path in files)
            {
                result.AddRange(ReadCertificates(ReadInput(path), allowBundles));
                if (result.Count > 128) throw new ArgumentException(L.T("O limite é de 128 certificados por conversão."));
            }
            return result;
        }
        catch { foreach (var certificate in result) certificate.Dispose(); throw; }
    }

    public static PemParts FromPem(string path)
    {
        var info = Inspect(path);
        if (info.Kind == InputKind.PrivateKey) throw new ArgumentException(L.T("O PEM precisa conter pelo menos um certificado."));
        var blocks = ReadPem(ReadInput(path));
        if (blocks.Count == 0) throw new ArgumentException(L.T("A conversão para CRT requer um certificado ou pacote em formato PEM."));
        var certificateBlocks = blocks.Where(b => b.Label == "CERTIFICATE").ToList();
        var certificates = new List<X509Certificate2>();
        try
        {
            foreach (var block in certificateBlocks) certificates.Add(X509CertificateLoader.LoadCertificate(block.Bytes));
            var keyBlock = blocks.Where(b => IsKeyLabel(b.Label)).ToList();
            var keyPem = keyBlock.Count == 0 ? null : PemEncoding.WriteString(keyBlock[0].Label, keyBlock[0].Bytes).Replace("\r\n", "\n") + "\n";
            // Extract encrypted material unchanged: no decryption or password is needed.
            List<X509Certificate2> ordered;
            if (keyPem != null && !info.IsEncrypted)
            {
                var (key, _) = ReadKey(Encoding.UTF8.GetBytes(keyPem), null);
                using (key) ordered = CertificateChain.Order(certificates, key);
            }
            else ordered = CertificateChain.Order(certificates);
            return new PemParts(JoinCertificates(ordered), keyPem, JoinCertificates(ordered.Take(1)),
                ordered.Count > 1 ? JoinCertificates(ordered.Skip(1)) : null, CertificateChain.Summarize(ordered).Warning);
        }
        finally { foreach (var certificate in certificates) certificate.Dispose(); }
    }

    private static string JoinCertificates(IEnumerable<X509Certificate2> certificates) =>
        string.Concat(certificates.Select(c => c.ExportCertificatePem().Replace("\r\n", "\n") + "\n"));

    /// <summary>Exports the checked certificates, ordered using the full supplied chain.</summary>
    public static string ExportSelected(IEnumerable<string> paths, IEnumerable<string> fingerprints, string? keyPath = null, string? password = null)
    {
        var selected = fingerprints.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selected.Count == 0) throw new ArgumentException(L.T("Marque pelo menos um certificado para exportar."));
        var certificates = LoadCertificates(paths, allowBundles: true);
        try
        {
            var all = certificates.DistinctBy(CertificateChain.Fingerprint).ToArray();
            if (selected.Except(all.Select(CertificateChain.Fingerprint), StringComparer.OrdinalIgnoreCase).Any())
                throw new ArgumentException(L.T("Um certificado selecionado mudou ou não está mais disponível. Carregue os arquivos novamente."));
            var ordered = CertificateChain.OrderForExport(all).Where(c => selected.Contains(CertificateChain.Fingerprint(c))).ToArray();
            var pem = JoinCertificates(ordered);
            if (keyPath == null) return pem;
            var domain = ordered.Where(c => !CertificateChain.IsCa(c)).ToArray();
            if (domain.Length != 1) throw new ArgumentException(L.T("Marque o certificado do domínio para incluir a chave privada no PEM."));
            var (key, keyPem) = ReadKey(Encoding.UTF8.GetBytes(ExtractPrivateKey(keyPath)), password);
            using (key) CertificateChain.Order(domain, key); // Checks the key/domain pair even for a partial export.
            return keyPem.Replace("\r\n", "\n") + "\n" + pem;
        }
        finally { foreach (var certificate in certificates) certificate.Dispose(); }
    }

    /// <summary>Copies a standalone or bundled key, preserving its original encryption.</summary>
    public static string ExtractPrivateKey(string path)
    {
        var info = Inspect(path);
        if (info.Kind is not (InputKind.PrivateKey or InputKind.Bundle)) throw new ArgumentException(L.T("Este arquivo não contém uma chave privada."));
        var data = ReadInput(path);
        var blocks = ReadPem(data);
        var keys = blocks.Where(b => IsKeyLabel(b.Label)).ToArray();
        if (keys.Length == 1) return PemEncoding.WriteString(keys[0].Label, keys[0].Bytes).Replace("\r\n", "\n") + "\n";
        if (LooksEncryptedDer(data)) return PemEncoding.WriteString("ENCRYPTED PRIVATE KEY", data).Replace("\r\n", "\n") + "\n";
        var (key, pem) = ReadKey(data, null);
        using (key) return pem.Replace("\r\n", "\n") + "\n";
    }

    public static (string Key, string Certificate) Identify(string first, string second)
    {
        bool firstIsCert = IsCertificate(first), secondIsCert = IsCertificate(second);
        if (firstIsCert == secondIsCert)
            throw new ArgumentException(L.T("Selecione uma chave privada e um certificado X.509 válidos."));
        return firstIsCert ? (second, first) : (first, second);
    }

    private static List<(string Label, byte[] Bytes)> ReadPem(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data).TrimStart('\uFEFF');
        var result = new List<(string, byte[])>();
        ReadOnlySpan<char> remaining = text.AsSpan();
        while (PemEncoding.TryFind(remaining, out var fields))
        {
            var start = fields.Location.Start.GetOffset(remaining.Length);
            if (!remaining[..start].Trim().IsEmpty) throw new ArgumentException(L.T("Conteúdo inesperado no arquivo PEM."));
            result.Add((remaining[fields.Label].ToString(), Convert.FromBase64String(remaining[fields.Base64Data].ToString())));
            remaining = remaining[fields.Location.End.GetOffset(remaining.Length)..];
        }
        if (!remaining.Trim().IsEmpty && result.Count > 0) throw new ArgumentException(L.T("Conteúdo inesperado no arquivo PEM."));
        return result;
    }

    private static List<X509Certificate2> ReadCertificates(byte[] data, bool allowBundles = false)
    {
        var result = new List<X509Certificate2>();
        try
        {
            var blocks = ReadPem(data);
            if ((allowBundles ? blocks.Count(b => b.Label == "CERTIFICATE") : blocks.Count) > 128)
                throw new ArgumentException(L.T("O limite é de 128 certificados por conversão."));
            if (blocks.Count == 0) result.Add(X509CertificateLoader.LoadCertificate(data));
            else foreach (var block in blocks)
            {
                if (allowBundles && IsKeyLabel(block.Label)) continue;
                if (block.Label != "CERTIFICATE") throw new ArgumentException(L.T("O arquivo CRT deve conter apenas certificados X.509."));
                result.Add(X509CertificateLoader.LoadCertificate(block.Bytes));
            }
            return result;
        }
        catch
        {
            foreach (var certificate in result) certificate.Dispose();
            throw;
        }
    }

    private static (AsymmetricAlgorithm Key, string Pem) ReadKey(byte[] data, string? password)
    {
        var blocks = ReadPem(data);
        if (blocks.Count > 1) throw new ArgumentException(L.T("O arquivo KEY deve conter uma única chave privada."));
        var label = blocks.Count == 1 ? blocks[0].Label : "";
        var bytes = blocks.Count == 1 ? blocks[0].Bytes : data;
        if (label != "" && label is not ("PRIVATE KEY" or "ENCRYPTED PRIVATE KEY" or "RSA PRIVATE KEY" or "EC PRIVATE KEY"))
            throw new ArgumentException(L.T("Formato de chave não suportado. Use PKCS#8, RSA PKCS#1 ou EC SEC1."));
        if (label == "ENCRYPTED PRIVATE KEY" && password == null) throw new PasswordRequiredException();

        foreach (var factory in new Func<AsymmetricAlgorithm>[] { RSA.Create, ECDsa.Create, DSA.Create })
        {
            var key = factory();
            try
            {
                int consumed;
                string outputLabel = label;
                if (label == "ENCRYPTED PRIVATE KEY") key.ImportEncryptedPkcs8PrivateKey(password!, bytes, out consumed);
                else if (label == "RSA PRIVATE KEY" && key is RSA rsa) rsa.ImportRSAPrivateKey(bytes, out consumed);
                else if (label == "EC PRIVATE KEY" && key is ECDsa ec) ec.ImportECPrivateKey(bytes, out consumed);
                else if (label is "PRIVATE KEY" or "")
                {
                    try { key.ImportPkcs8PrivateKey(bytes, out consumed); outputLabel = "PRIVATE KEY"; }
                    catch (CryptographicException) when (label == "")
                    {
                        if (key is RSA r)
                        {
                            try { r.ImportRSAPrivateKey(bytes, out consumed); outputLabel = "RSA PRIVATE KEY"; }
                            catch (CryptographicException) { key.ImportEncryptedPkcs8PrivateKey(password ?? "", bytes, out consumed); outputLabel = "ENCRYPTED PRIVATE KEY"; }
                        }
                        else if (key is ECDsa e)
                        {
                            try { e.ImportECPrivateKey(bytes, out consumed); outputLabel = "EC PRIVATE KEY"; }
                            catch (CryptographicException) { key.ImportEncryptedPkcs8PrivateKey(password ?? "", bytes, out consumed); outputLabel = "ENCRYPTED PRIVATE KEY"; }
                        }
                        else { key.ImportEncryptedPkcs8PrivateKey(password ?? "", bytes, out consumed); outputLabel = "ENCRYPTED PRIVATE KEY"; }
                    }
                }
                else { key.Dispose(); continue; }
                if (consumed != bytes.Length) throw new CryptographicException();
                // Preserve the original encryption and key encoding; never silently remove a password.
                return (key, PemEncoding.WriteString(outputLabel, bytes));
            }
            catch (CryptographicException) { key.Dispose(); }
        }
        if (password == null && (label == "ENCRYPTED PRIVATE KEY" || LooksEncryptedDer(bytes))) throw new PasswordRequiredException();
        throw new ArgumentException(password != null
            ? L.T("Não foi possível abrir a chave. Verifique a senha e o formato do arquivo.")
            : L.T("Chave inválida ou formato não suportado. Chaves PEM com criptografia antiga (Proc-Type) precisam ser convertidas para PKCS#8."));
    }

    private static bool LooksEncryptedDer(byte[] bytes)
    {
        try
        {
            var reader = new System.Formats.Asn1.AsnReader(bytes, System.Formats.Asn1.AsnEncodingRules.DER);
            var sequence = reader.ReadSequence();
            var algorithm = sequence.ReadSequence();
            algorithm.ReadObjectIdentifier();
            if (algorithm.HasData) algorithm.ReadEncodedValue();
            algorithm.ThrowIfNotEmpty();
            sequence.ReadOctetString();
            sequence.ThrowIfNotEmpty();
            reader.ThrowIfNotEmpty();
            return true;
        }
        catch (System.Formats.Asn1.AsnContentException) { return false; }
    }

    public static string Build(string keyPath, string certificatePath, string? password = null) => ToPem(certificatePath, keyPath, password);

    public static void Save(string content, string output, string keyPath, string certificatePath, bool overwrite)
    {
        SaveFiles(new[] { new ConversionOutput(output, content) }, new[] { keyPath, certificatePath }, overwrite);
    }

    public static void SaveFiles(IReadOnlyList<ConversionOutput> outputs, IEnumerable<string> inputs, bool overwrite)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var protectedPaths = inputs.Select(Path.GetFullPath).ToHashSet(comparer);
        var paths = outputs.Select(o => Path.GetFullPath(o.Path)).ToArray();
        if (paths.Distinct(comparer).Count() != paths.Length) throw new ArgumentException(L.T("Domínio, CA e KEY devem ter destinos diferentes."));
        foreach (var path in paths)
        {
            if (protectedPaths.Contains(path)) throw new ArgumentException(L.T("Escolha outro nome: a saída não pode substituir os arquivos de entrada."));
            if (Directory.Exists(path)) throw new IOException(L.F("O destino é uma pasta: {0}", path));
            if (!overwrite && File.Exists(path)) throw new IOException(L.F("A saída já existe: {0}. Escolha outro nome ou confirme a substituição.", path));
            if (!Directory.Exists(Path.GetDirectoryName(path))) throw new DirectoryNotFoundException(L.T("A pasta de destino não existe."));
        }
        var staged = new List<(string Temp, string Final)>();
        var backups = new List<(string Backup, string Final)>();
        var committed = new List<string>();
        try
        {
            for (int i = 0; i < outputs.Count; i++)
            {
                var temp = Path.Combine(Path.GetDirectoryName(paths[i])!, ".pem-" + Guid.NewGuid().ToString("N") + ".tmp");
                staged.Add((temp, paths[i]));
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
                if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                using var stream = new FileStream(temp, options);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(outputs[i].Content);
            }
            foreach (var (temp, final) in staged)
            {
                if (overwrite && File.Exists(final))
                {
                    var backup = final + "." + Guid.NewGuid().ToString("N") + ".backup";
                    File.Move(final, backup);
                    backups.Add((backup, final));
                }
                File.Move(temp, final);
                committed.Add(final);
            }
        }
        catch
        {
            foreach (var final in committed) File.Delete(final);
            foreach (var (backup, final) in backups) File.Move(backup, final);
            throw;
        }
        finally
        {
            foreach (var (temp, _) in staged) if (File.Exists(temp)) File.Delete(temp);
        }
        foreach (var (backup, _) in backups) File.Delete(backup);
    }
}
