// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel and CertBox contributors.
// Developed with AI; third-party components retain their original licenses.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Formats.Asn1;
using CertBox;

string directory = Path.GetFullPath(args.Length > 0 ? args[0] : "work/test-fixtures");
Directory.CreateDirectory(directory);
int count = 0;
string Write(string name, string text) { var path = Path.Combine(directory, name); File.WriteAllText(path, text); return path; }
string Binary(string name, byte[] bytes) { var path = Path.Combine(directory, name); File.WriteAllBytes(path, bytes); return path; }
void Check(string name, Action assertion) { assertion(); Console.WriteLine("OK " + name); count++; }
void Equal<T>(T a, T b) { if (!EqualityComparer<T>.Default.Equals(a, b)) throw new Exception($"Expected {b}, got {a}"); }
void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }

using var rsa = RSA.Create(2048);
var request = new CertificateRequest("CN=Teste RSA", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
using var otherRsa = RSA.Create(2048);
using var otherCertificate = new CertificateRequest("CN=Outro", otherRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
    .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
var key = Write("chave com espaço.key", rsa.ExportPkcs8PrivateKeyPem());
var crt = Write("certificado.crt", certificate.ExportCertificatePem());
var otherKey = Write("outra.key", otherRsa.ExportPkcs8PrivateKeyPem());
var encryptedPem = rsa.ExportEncryptedPkcs8PrivateKeyPem("teste123", new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 10000));
var encrypted = Write("protegida.key", encryptedPem);
var encDer = Binary("protegida.der", rsa.ExportEncryptedPkcs8PrivateKey("teste123", new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 10000)));
// Real PKI: RSA root -> RSA upper CA -> EC lower CA -> RSA domain certificate.
var since = DateTimeOffset.UtcNow.AddDays(-2);
var until = DateTimeOffset.UtcNow.AddDays(7);
void Extensions(CertificateRequest req, bool ca, int length = 0, X509Certificate2? issuer = null)
{
    req.CertificateExtensions.Add(new X509BasicConstraintsExtension(ca, ca, length, true));
    req.CertificateExtensions.Add(new X509KeyUsageExtension(ca ? X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign : X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
    req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));
    if (issuer != null)
    {
        var identifier = issuer.Extensions.OfType<X509SubjectKeyIdentifierExtension>().Single().SubjectKeyIdentifier!;
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.PushSequence(); writer.WriteOctetString(Convert.FromHexString(identifier), new Asn1Tag(TagClass.ContextSpecific, 0)); writer.PopSequence();
        req.CertificateExtensions.Add(new X509Extension("2.5.29.35", writer.Encode(), false));
    }
    if (!ca)
    {
        var san = new SubjectAlternativeNameBuilder(); san.AddDnsName("dominio.exemplo.test"); req.CertificateExtensions.Add(san.Build());
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
    }
}
using var rootKey = RSA.Create(2048);
var rootRequest = new CertificateRequest("CN=CA Raiz Teste", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
Extensions(rootRequest, true, 2);
using var rootCert = rootRequest.CreateSelfSigned(since, until.AddDays(10));
using var upperKey = RSA.Create(2048);
var upperRequest = new CertificateRequest("CN=CA Alta Teste", upperKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
Extensions(upperRequest, true, 1, rootCert);
using var upperCert = upperRequest.Create(rootCert, since, until.AddDays(5), RandomNumberGenerator.GetBytes(16));
using var lowerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var lowerRequest = new CertificateRequest("CN=CA Baixa Teste", lowerKey, HashAlgorithmName.SHA256);
Extensions(lowerRequest, true, 0, upperCert);
using var lowerCert = lowerRequest.Create(upperCert.SubjectName, X509SignatureGenerator.CreateForRSA(upperKey, RSASignaturePadding.Pkcs1), since, until.AddDays(2), RandomNumberGenerator.GetBytes(16));
var domainRequest = new CertificateRequest("CN=dominio.exemplo.test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
Extensions(domainRequest, false, issuer: lowerCert);
using var domainCert = domainRequest.Create(lowerCert.SubjectName, X509SignatureGenerator.CreateForECDsa(lowerKey), since, until, RandomNumberGenerator.GetBytes(16));
var domainPath = Write("dominio.crt", domainCert.ExportCertificatePem());
var lowerPath = Write("intermediaria-baixa.crt", lowerCert.ExportCertificatePem());
var upperPath = Write("intermediaria-alta.crt", upperCert.ExportCertificatePem());
var rootPath = Write("raiz.crt", rootCert.ExportCertificatePem());
var caPath = Write("ca.crt", upperCert.ExportCertificatePem() + "\n" + lowerCert.ExportCertificatePem());
string Certs(params X509Certificate2[] values) => string.Concat(values.Select(c => c.ExportCertificatePem().Replace("\r\n", "\n") + "\n"));
var sortedChain = Certs(domainCert, lowerCert, upperCert, rootCert);
Check("dados públicos para visualização do certificado", () =>
{
    var info = PemService.Inspect(domainPath).Certificates.Single();
    Equal(info.Name, "dominio.exemplo.test");
    Equal(info.NotBefore, domainCert.NotBefore.ToUniversalTime());
    Equal(info.NotAfter, domainCert.NotAfter.ToUniversalTime());
    Equal(info.SerialNumber, domainCert.SerialNumber);
    Equal(info.PublicKey, "RSA · 2048 bits");
    Equal(info.SignatureAlgorithm, domainCert.SignatureAlgorithm.FriendlyName);
    Equal(info.DnsNames.Single(), "dominio.exemplo.test");
    Equal(PemService.Inspect(lowerPath).Certificates.Single().PublicKey, "EC · 256 bits");
});
Check("remoção individual preserva os outros arquivos e reavalia conversão", () =>
{
    var selected = new ConversionSelection(); selected.Accept(new[] { domainPath, caPath, key });
    selected.Remove(caPath);
    Equal(selected.Certificates.Count, 1); Equal(selected.Key!.Path, key); Equal(selected.CanConvert, true);
    selected.Remove(domainPath);
    Equal(selected.Document, null); Equal(selected.Key!.Path, key); Equal(selected.CanConvert, false);
    selected.Remove(key);
    Equal(selected.InputPaths.Count(), 0); Equal(selected.Direction, ConversionDirection.ToPem);
    selected.Accept(new[] { domainPath, caPath, encrypted }); selected.Remove(encrypted);
    Equal(selected.Key, null); Equal(selected.NeedsPassword, false); Equal(selected.CertificateDetails.Count, 3);
});
Check("campos adicionais incluem somente extensões existentes", () =>
{
    var fields = PemService.Inspect(domainPath).Certificates.Single().AdditionalFields;
    Equal(fields.Any(f => f.Oid == "2.5.29.15"), true);
    Equal(fields.Any(f => f.Oid == "2.5.29.37"), true);
    Equal(fields.Any(f => f.Oid == "2.5.29.31"), false);
    Equal(fields.Any(f => f.Oid == "2.5.29.17"), false);
    Equal(fields.Single(f => f.Oid == "2.5.29.19").Value.Contains("Crítica"), true);
});
Check("SAN adicional preserva IP, e-mail e URI sem repetir DNS", () =>
{
    var req = new CertificateRequest("CN=SAN adicional", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var san = new SubjectAlternativeNameBuilder();
    san.AddDnsName("exemplo.test"); san.AddIpAddress(System.Net.IPAddress.Parse("192.0.2.1"));
    san.AddEmailAddress("teste@exemplo.test"); san.AddUri(new Uri("https://exemplo.test/"));
    req.CertificateExtensions.Add(san.Build());
    using var cert = req.CreateSelfSigned(since, until);
    var info = CertificateChain.Describe(cert);
    Equal(info.DnsNames.Single(), "exemplo.test");
    var extra = info.AdditionalFields.Single(f => f.Oid == "2.5.29.17").Value;
    Equal(extra.Contains("IP: 192.0.2.1"), true);
    Equal(extra.Contains("E-mail: teste@exemplo.test"), true);
    Equal(extra.Contains("URI: https://exemplo.test/"), true);
    Equal(extra.Contains("DNS:"), false);
});
Check("extensão desconhecida permanece visível pelo OID", () =>
{
    var req = new CertificateRequest("CN=Extensão personalizada", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    req.CertificateExtensions.Add(new X509Extension("1.3.6.1.4.1.55555.1", new byte[] { 0x04, 0x03, 0x41, 0x42, 0x43 }, false));
    using var cert = req.CreateSelfSigned(since, until);
    var extra = CertificateChain.Describe(cert).AdditionalFields.Single(f => f.Oid == "1.3.6.1.4.1.55555.1");
    Equal(extra.Value.Contains("OID: 1.3.6.1.4.1.55555.1"), true);
    Equal(extra.Value.Split('\n')[0].Length > 0, true);
});
Check("PEM RSA e certificado correspondem", () => { var pem = PemService.Build(key, crt); using var result = X509Certificate2.CreateFromPem(pem, pem); Equal(result.HasPrivateKey, true); });
Check("identificação com ordem invertida", () => Equal(PemService.Identify(crt, key), (key, crt)));
Check("RSA PKCS1 DER + CRT DER", () => { var pem = PemService.Build(Binary("rsa.der", rsa.ExportRSAPrivateKey()), Binary("certificado.der", certificate.RawData)); using var result = X509Certificate2.CreateFromPem(pem, pem); Equal(result.HasPrivateKey, true); });
Check("PKCS8 DER", () => { var pem = PemService.Build(Binary("pkcs8.der", rsa.ExportPkcs8PrivateKey()), crt); using var result = X509Certificate2.CreateFromPem(pem, pem); Equal(result.HasPrivateKey, true); });
Check("senha exigida", () => Throws<PasswordRequiredException>(() => PemService.Build(encrypted, crt)));
Check("senha incorreta rejeitada", () => Throws<ArgumentException>(() => PemService.Build(encrypted, crt, "incorreta")));
Check("criptografia PEM preservada", () => { var pem = PemService.Build(encrypted, crt, "teste123"); Equal(pem.StartsWith(encryptedPem.Replace("\r\n", "\n")), true); using var result = X509Certificate2.CreateFromEncryptedPem(pem, pem, "teste123"); Equal(result.HasPrivateKey, true); });
Check("senha DER exigida", () => Throws<PasswordRequiredException>(() => PemService.Build(encDer, crt)));
Check("criptografia DER preservada", () => { var pem = PemService.Build(encDer, crt, "teste123"); using var result = X509Certificate2.CreateFromEncryptedPem(pem, pem, "teste123"); Equal(result.HasPrivateKey, true); });
Check("chave diferente rejeitada", () => Throws<ArgumentException>(() => PemService.Build(otherKey, crt)));
Check("somente chave pública rejeitada", () => Throws<ArgumentException>(() => PemService.Build(Write("publica.key", rsa.ExportSubjectPublicKeyInfoPem()), crt)));
Check("cadeia real ordenada, certificado do domínio primeiro", () => { var pem = PemService.Build(key, Write("cadeia.crt", Certs(upperCert, rootCert, domainCert, lowerCert))); Equal(pem, rsa.ExportPkcs8PrivateKeyPem().Replace("\r\n", "\n") + "\n" + sortedChain); });
using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
using var ecCertificate = new CertificateRequest("CN=Teste EC", ec, HashAlgorithmName.SHA256).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
var ecCrt = Write("ec.crt", ecCertificate.ExportCertificatePem());
Check("EC SEC1 PEM", () => { var pem = PemService.Build(Write("ec.key", ec.ExportECPrivateKeyPem()), ecCrt); using var result = X509Certificate2.CreateFromPem(pem, pem); Equal(result.HasPrivateKey, true); });
Check("EC SEC1 DER", () => { var pem = PemService.Build(Binary("ec.der", ec.ExportECPrivateKey()), ecCrt); using var result = X509Certificate2.CreateFromPem(pem, pem); Equal(result.HasPrivateKey, true); });
Check("múltiplas chaves rejeitadas", () => Throws<ArgumentException>(() => PemService.Build(Write("duas.key", rsa.ExportPkcs8PrivateKeyPem() + "\n" + rsa.ExportPkcs8PrivateKeyPem()), crt)));
Check("entrada não é sobrescrita", () => Throws<ArgumentException>(() => PemService.Save("teste", key, key, crt, true)));
Check("saída existente exige autorização", () => { var path = Write("existente.pem", "original"); Throws<IOException>(() => PemService.Save("nova", path, key, crt, false)); Equal(File.ReadAllText(path), "original"); PemService.Save("nova", path, key, crt, true); Equal(File.ReadAllText(path), "nova"); });
Check("saída PEM sem BOM e modo privado no Linux", () => { var path = Path.Combine(directory, "resultado.pem"); PemService.Save(PemService.Build(key, crt), path, key, crt, true); Equal(File.ReadAllBytes(path)[0], (byte)'-'); if (!OperatingSystem.IsWindows()) Equal(File.GetUnixFileMode(path), UnixFileMode.UserRead | UnixFileMode.UserWrite); });
var combined = Write("pacote.pem", PemService.Build(key, crt));
var encryptedCombined = Write("pacote-protegido.pem", PemService.Build(encrypted, crt, "teste123"));
var certOnlyPem = Write("somente-certificado.pem", certificate.ExportCertificatePem());
Check("remover último PEM restaura seleção inicial e aceita novo arquivo", () =>
{
    var selected = new ConversionSelection(); selected.Accept(new[] { combined });
    Equal(selected.Direction, ConversionDirection.FromPem); selected.Remove(combined);
    Equal(selected.CanConvert, false); Equal(selected.InputPaths.Count(), 0); Equal(selected.Direction, ConversionDirection.ToPem);
    selected.Accept(new[] { crt }); Equal(selected.CanConvert, true); Equal(selected.Document!.Path, crt);
});
Check("CRT para PEM sem chave", () => { var pem = PemService.ToPem(crt); Equal(pem.Contains("PRIVATE KEY"), false); using var result = X509CertificateLoader.LoadCertificate(System.Text.Encoding.UTF8.GetBytes(pem)); Equal(result.Thumbprint, certificate.Thumbprint); });
Check("DER para PEM sem chave", () => Equal(PemService.ToPem(Path.Combine(directory, "certificado.der")), PemService.ToPem(crt)));
Check("identificação de pacote combinado", () => Equal(PemService.Inspect(combined).Kind, InputKind.Bundle));
Check("identificação de chave DER criptografada sem senha", () => Equal(PemService.Inspect(encDer).IsEncrypted, true));
Check("PEM para CRT sem inventar chave", () => { var result = PemService.FromPem(certOnlyPem); Equal(result.PrivateKey, null); Equal(result.Certificates, PemService.ToPem(crt)); });
Check("pacote para CRT e KEY com par válido", () => { var result = PemService.FromPem(combined); using var restored = X509Certificate2.CreateFromPem(result.Certificates, result.PrivateKey); Equal(restored.HasPrivateKey, true); });
Check("extração criptografada não pede senha e preserva chave", () => { var result = PemService.FromPem(encryptedCombined); Equal(result.PrivateKey, encryptedPem.Replace("\r\n", "\n") + "\n"); using var restored = X509Certificate2.CreateFromEncryptedPem(result.Certificates, result.PrivateKey!, "teste123"); Equal(restored.HasPrivateKey, true); });
Check("extração preserva e ordena cadeia", () => { var chain = Certs(rootCert, lowerCert, domainCert, upperCert); var result = PemService.FromPem(Write("cadeia.pem", chain)); Equal(result.Certificates, sortedChain); Equal(result.DomainCertificate, Certs(domainCert)); Equal(result.CaCertificates, Certs(lowerCert, upperCert, rootCert)); });
Check("pacote com chave errada rejeitado", () => Throws<ArgumentException>(() => PemService.FromPem(Write("pacote-invalido.pem", otherRsa.ExportPkcs8PrivateKeyPem() + "\n" + certificate.ExportCertificatePem()))));
Check("sem certificado não há conversão inversa", () => Throws<ArgumentException>(() => PemService.FromPem(key)));
Check("primeiro CRT define geração PEM sem chave", () => { var selected = new ConversionSelection(); selected.Accept(new[] { crt }); Equal(selected.Direction, ConversionDirection.ToPem); Equal(selected.CanConvert, true); Equal(selected.NeedsPassword, false); });
Check("primeiro PEM define extração CRT", () => { var selected = new ConversionSelection(); selected.Accept(new[] { certOnlyPem }); Equal(selected.Direction, ConversionDirection.FromPem); Equal(selected.CanConvert, true); });
Check("chave primeiro espera certificado e mantém geração PEM", () => { var selected = new ConversionSelection(); selected.Accept(new[] { key }); Equal(selected.CanConvert, false); selected.Accept(new[] { certOnlyPem }); Equal(selected.Direction, ConversionDirection.ToPem); Equal(selected.CanConvert, true); Equal(selected.Key!.Path, Path.GetFullPath(key)); });
Check("chave opcional identifica necessidade de senha", () => { var selected = new ConversionSelection(); selected.Accept(new[] { crt }); selected.Accept(new[] { encrypted }); Equal(selected.NeedsPassword, true); });
Check("extração criptografada não exibe campo senha", () => { var selected = new ConversionSelection(); selected.Accept(new[] { encryptedCombined }); Equal(selected.Direction, ConversionDirection.FromPem); Equal(selected.NeedsPassword, false); });
Check("par selecionado junto combina em qualquer ordem", () => { var selected = new ConversionSelection(); selected.Accept(new[] { certOnlyPem, key }); Equal(selected.Direction, ConversionDirection.ToPem); selected.Accept(new[] { key, crt }); Equal(selected.Direction, ConversionDirection.ToPem); });
Check("seleção inválida não descarta seleção anterior", () => { var selected = new ConversionSelection(); selected.Accept(new[] { crt }); Throws<ArgumentException>(() => selected.Accept(new[] { crt, ecCrt })); Equal(selected.Document!.Path, Path.GetFullPath(crt)); Equal(selected.Key, null); });
Check("nova conversão limpa chave anterior", () => { var selected = new ConversionSelection(); selected.Accept(new[] { crt, key }); selected.Accept(new[] { combined }); Equal(selected.Key, null); Equal(selected.Direction, ConversionDirection.FromPem); selected.Clear(); Equal(selected.CanConvert, false); });
Check("duas saídas não podem ter o mesmo destino", () => { var path = Path.Combine(directory, "colisao.crt"); Throws<ArgumentException>(() => PemService.SaveFiles(new[] { new ConversionOutput(path, "CRT"), new ConversionOutput(path, "KEY") }, new[] { combined }, true)); Equal(File.Exists(path), false); });
Check("recusa sobrescrita da chave antes de salvar CRT", () => { var output = Path.Combine(directory, "novo.crt"); var existingKey = Write("existente.key", "original"); Throws<IOException>(() => PemService.SaveFiles(new[] { new ConversionOutput(output, "CRT"), new ConversionOutput(existingKey, "KEY") }, new[] { combined }, false)); Equal(File.Exists(output), false); Equal(File.ReadAllText(existingKey), "original"); });
Check("extração não sobrescreve entrada", () => Throws<ArgumentException>(() => PemService.SaveFiles(new[] { new ConversionOutput(combined, "CRT") }, new[] { combined }, true)));
Check("salva CRT e KEY separados", () => { var parts = PemService.FromPem(combined); var outputCrt = Path.Combine(directory, "extraido.crt"); var outputKey = Path.Combine(directory, "extraido.key"); PemService.SaveFiles(new[] { new ConversionOutput(outputCrt, parts.Certificates), new ConversionOutput(outputKey, parts.PrivateKey!) }, new[] { combined }, true); using var restored = X509Certificate2.CreateFromPemFile(outputCrt, outputKey); Equal(restored.HasPrivateKey, true); });
if (OperatingSystem.IsWindows()) Check("erro na segunda saída restaura ambas as saídas", () => { var first = Write("rollback.crt", "crt-original"); var second = Write("rollback.key", "key-original"); using var locked = File.Open(second, FileMode.Open, FileAccess.Read, FileShare.None); Throws<IOException>(() => PemService.SaveFiles(new[] { new ConversionOutput(first, "crt-novo"), new ConversionOutput(second, "key-novo") }, new[] { combined }, true)); Equal(File.ReadAllText(first), "crt-original"); locked.Dispose(); Equal(File.ReadAllText(second), "key-original"); });
Check("papéis identificados pelo conteúdo", () => { Equal(PemService.Inspect(domainPath).Certificates[0].Role, "Domínio"); Equal(PemService.Inspect(caPath).Certificates.All(c => c.IsCa && !c.IsRoot), true); Equal(PemService.Inspect(rootPath).Certificates[0].IsRoot, true); Equal(PemService.Inspect(domainPath).Certificates[0].DnsNames[0], "dominio.exemplo.test"); });
Check("CA primeiro aguarda domínio", () => { var selected = new ConversionSelection(); selected.Accept(new[] { caPath }); Equal(selected.CanConvert, false); selected.Accept(new[] { key }); selected.Accept(new[] { domainPath }); Equal(selected.CanConvert, true); Equal(selected.Certificates.Count, 2); Equal(selected.Document!.Path, Path.GetFullPath(domainPath)); });
Check("CRTs separados adicionados um por vez", () => { var selected = new ConversionSelection(); selected.Accept(new[] { domainPath }); selected.Accept(new[] { upperPath }); selected.Accept(new[] { lowerPath }); selected.Accept(new[] { rootPath }); Equal(selected.CertificateDetails.Count, 4); Equal(PemService.ToPem(selected.CertificatePaths), sortedChain); });
foreach (var inputs in new[] {
    new[] { domainPath, caPath, key }, new[] { domainPath, key, caPath }, new[] { caPath, domainPath, key },
    new[] { caPath, key, domainPath }, new[] { key, caPath, domainPath }, new[] { key, domainPath, caPath } })
    Check("domínio + CA + KEY em ordem " + string.Join(",", inputs.Select(Path.GetFileName)), () => { var selected = new ConversionSelection(); selected.Accept(inputs); Equal(selected.Direction, ConversionDirection.ToPem); Equal(PemService.ToPem(selected.CertificatePaths, selected.Key!.Path), rsa.ExportPkcs8PrivateKeyPem().Replace("\r\n", "\n") + "\n" + Certs(domainCert, lowerCert, upperCert)); });
Check("CRTs PEM e DER misturados", () => Equal(PemService.ToPem(new[] { Binary("ca-alta.der", upperCert.RawData), domainPath, lowerPath, rootPath }), sortedChain));
Check("duplicatas não duplicam certificados na saída", () => Equal(PemService.ToPem(new[] { caPath, upperPath, rootPath, domainPath, lowerPath }), sortedChain));
var allPemPath = Write("haproxy-com-raiz.pem", PemService.ToPem(new[] { caPath, domainPath, rootPath }, key));
var noRootPemPath = Write("haproxy.pem", PemService.ToPem(new[] { caPath, domainPath }, key));
var encryptedChain = Write("haproxy-protegido.pem", PemService.ToPem(new[] { caPath, domainPath }, encrypted, "teste123"));
Check("inverso separa domínio, CA e KEY", () => { var parts = PemService.FromPem(allPemPath); Equal(parts.DomainCertificate, Certs(domainCert)); Equal(parts.CaCertificates, Certs(lowerCert, upperCert, rootCert)); Equal(parts.PrivateKey, rsa.ExportPkcs8PrivateKeyPem().Replace("\r\n", "\n") + "\n"); Equal(parts.Warning, null); });
Check("inverso criptografado mantém senha sem solicitá-la", () => { var parts = PemService.FromPem(encryptedChain); Equal(parts.CaCertificates, Certs(lowerCert, upperCert)); Equal(parts.PrivateKey, encryptedPem.Replace("\r\n", "\n") + "\n"); });
Check("ida e volta preserva domínio e CAs", () => { var parts = PemService.FromPem(allPemPath); var d = Write("roundtrip-dominio.crt", parts.DomainCertificate); var c = Write("roundtrip-ca.crt", parts.CaCertificates!); var k = Write("roundtrip.key", parts.PrivateKey!); Equal(PemService.ToPem(new[] { c, d }, k), File.ReadAllText(allPemPath)); });
Check("emissor final ausente gera aviso sem baixar certificados", () => { Equal(PemService.Analyze(new[] { domainPath, caPath }).Warning != null, true); Equal(PemService.Analyze(new[] { domainPath, caPath, rootPath }).Warning, null); });
Check("intermediário faltando entre arquivos rejeitado", () => Throws<ArgumentException>(() => PemService.ToPem(new[] { domainPath, upperPath })));
Check("domínios sem relação rejeitados", () => Throws<ArgumentException>(() => PemService.ToPem(new[] { domainPath, crt, caPath })));
Check("CA sem domínio não gera pacote de servidor", () => Throws<ArgumentException>(() => PemService.ToPem(new[] { caPath, rootPath })));
Check("chave de outra pessoa rejeitada com CRTs separados", () => Throws<ArgumentException>(() => PemService.ToPem(new[] { domainPath, caPath }, otherKey)));
Check("certificado com assinatura adulterada rejeitado", () => { var bytes = domainCert.RawData; bytes[^1] ^= 1; var path = Binary("adulterado.crt", bytes); Throws<ArgumentException>(() => PemService.ToPem(new[] { path, caPath })); });
Check("nome da CA sem chave correta não basta", () => { using var fakeKey = RSA.Create(2048); var req = new CertificateRequest(lowerCert.SubjectName, fakeKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); Extensions(req, true, 0, upperCert); using var fake = req.Create(upperCert.SubjectName, X509SignatureGenerator.CreateForRSA(upperKey, RSASignaturePadding.Pkcs1), since, until, RandomNumberGenerator.GetBytes(16)); Throws<ArgumentException>(() => PemService.ToPem(new[] { domainPath, Write("ca-nome-igual.crt", fake.ExportCertificatePem()), upperPath })); });
Check("mais de um emissor válido gera ambiguidade", () => { using var alternate = lowerRequest.Create(upperCert.SubjectName, X509SignatureGenerator.CreateForRSA(upperKey, RSASignaturePadding.Pkcs1), since, until, RandomNumberGenerator.GetBytes(16)); Throws<ArgumentException>(() => PemService.ToPem(new[] { domainPath, lowerPath, Write("ca-alternativa.crt", alternate.ExportCertificatePem()), upperPath })); });
Check("CA sem keyCertSign rejeitada", () => { var req = new CertificateRequest(lowerCert.SubjectName, lowerKey, HashAlgorithmName.SHA256); Extensions(req, true, 0, upperCert); req.CertificateExtensions.Remove(req.CertificateExtensions.OfType<X509KeyUsageExtension>().Single()); req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true)); using var wrongUsage = req.Create(upperCert.SubjectName, X509SignatureGenerator.CreateForRSA(upperKey, RSASignaturePadding.Pkcs1), since, until, RandomNumberGenerator.GetBytes(16)); Throws<ArgumentException>(() => PemService.ToPem(new[] { domainPath, Write("ca-sem-permissao.crt", wrongUsage.ExportCertificatePem()), upperPath })); });
Check("restrição de comprimento da cadeia respeitada", () => { var req = new CertificateRequest(upperCert.SubjectName, upperKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); Extensions(req, true, 0, rootCert); using var limited = req.Create(rootCert, since, until, RandomNumberGenerator.GetBytes(16)); Throws<ArgumentException>(() => PemService.ToPem(new[] { domainPath, lowerPath, Write("ca-limitada.crt", limited.ExportCertificatePem()), rootPath })); });
Check("assinaturas RSA-PSS na raiz e no domínio", () => { using var signingKey = RSA.Create(2048); var req = new CertificateRequest("CN=Raiz PSS", signingKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pss); Extensions(req, true, 0); using var pssRoot = req.CreateSelfSigned(since, until.AddDays(1)); var leafReq = new CertificateRequest("CN=dominio-pss.test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); Extensions(leafReq, false, issuer: pssRoot); using var pssLeaf = leafReq.Create(pssRoot.SubjectName, X509SignatureGenerator.CreateForRSA(signingKey, RSASignaturePadding.Pss), since, until, RandomNumberGenerator.GetBytes(16)); Equal(PemService.ToPem(new[] { Write("pss-dominio.crt", pssLeaf.ExportCertificatePem()), Write("pss-raiz.crt", pssRoot.ExportCertificatePem()) }), Certs(pssLeaf, pssRoot)); });
Check("AKI com número de série escolhe a CA correta", () => { using var alternate = lowerRequest.Create(upperCert.SubjectName, X509SignatureGenerator.CreateForRSA(upperKey, RSASignaturePadding.Pkcs1), since, until, RandomNumberGenerator.GetBytes(16)); var req = new CertificateRequest("CN=aki-completo.test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); Extensions(req, false); req.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(lowerCert, true, true)); using var issued = req.Create(lowerCert.SubjectName, X509SignatureGenerator.CreateForECDsa(lowerKey), since, until, RandomNumberGenerator.GetBytes(16)); Equal(CertificateChain.Order(new[] { issued, lowerCert, upperCert, rootCert }).Count, 4); Throws<ArgumentException>(() => CertificateChain.Order(new[] { issued, alternate, upperCert, rootCert })); });
Check("ciclo entre CAs é recusado", () => { using var a = RSA.Create(2048); using var b = RSA.Create(2048); var ar = new CertificateRequest("CN=Ciclo A", a, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); var br = new CertificateRequest("CN=Ciclo B", b, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); Extensions(ar, true, 10); Extensions(br, true, 10); using var ac = ar.Create(br.SubjectName, X509SignatureGenerator.CreateForRSA(b, RSASignaturePadding.Pkcs1), since, until, RandomNumberGenerator.GetBytes(16)); using var bc = br.Create(ar.SubjectName, X509SignatureGenerator.CreateForRSA(a, RSASignaturePadding.Pkcs1), since, until, RandomNumberGenerator.GetBytes(16)); var lr = new CertificateRequest("CN=Ciclo dominio", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); Extensions(lr, false); using var lc = lr.Create(ar.SubjectName, X509SignatureGenerator.CreateForRSA(a, RSASignaturePadding.Pkcs1), since, until, RandomNumberGenerator.GetBytes(16)); Throws<ArgumentException>(() => CertificateChain.Order(new[] { lc, ac, bc })); });
Check("SAN malformado gera erro legível", () => { var req = new CertificateRequest("CN=SAN invalido", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); Extensions(req, false, issuer: lowerCert); req.CertificateExtensions.Remove(req.CertificateExtensions.Single(e => e.Oid!.Value == "2.5.29.17")); req.CertificateExtensions.Add(new X509Extension("2.5.29.17", new byte[] { 0x30, 0x03, 0x82, 0x02, 0x41 }, false)); using var issued = req.Create(lowerCert.SubjectName, X509SignatureGenerator.CreateForECDsa(lowerKey), since, until, RandomNumberGenerator.GetBytes(16)); Throws<ArgumentException>(() => PemService.Inspect(Write("san-invalido.crt", issued.ExportCertificatePem()))); });
Check("AKI malformado gera erro legível", () => { var req = new CertificateRequest("CN=AKI invalido", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); Extensions(req, false); req.CertificateExtensions.Add(new X509Extension("2.5.29.35", new byte[] { 0x30, 0x03, 0x80, 0x02, 0x41 }, false)); using var issued = req.Create(lowerCert.SubjectName, X509SignatureGenerator.CreateForECDsa(lowerKey), since, until, RandomNumberGenerator.GetBytes(16)); Throws<ArgumentException>(() => CertificateChain.Order(new[] { issued, lowerCert, upperCert })); });
if (OperatingSystem.IsWindows()) Check("erro no terceiro arquivo restaura domínio e CA", () => { var d = Write("rollback3-dominio.crt", "dominio-original"); var c = Write("rollback3-ca.crt", "ca-original"); var k = Write("rollback3.key", "key-original"); using var locked = File.Open(k, FileMode.Open, FileAccess.Read, FileShare.None); Throws<IOException>(() => PemService.SaveFiles(new[] { new ConversionOutput(d, "novo dominio"), new ConversionOutput(c, "nova ca"), new ConversionOutput(k, "nova key") }, new[] { allPemPath }, true)); Equal(File.ReadAllText(d), "dominio-original"); Equal(File.ReadAllText(c), "ca-original"); locked.Dispose(); Equal(File.ReadAllText(k), "key-original"); });
Check("compatibilidade com dois arquivos CRT completo e KEY", () => { var parts = PemService.FromPem(allPemPath); var c = Path.Combine(directory, "completo.crt"); var k = Path.Combine(directory, "completo.key"); PemService.SaveFiles(new[] { new ConversionOutput(c, parts.Certificates), new ConversionOutput(k, parts.PrivateKey!) }, new[] { allPemPath }, true); Equal(File.ReadAllText(c), sortedChain); Equal(PemService.ToPem(c, k), File.ReadAllText(allPemPath)); });
Check("exportação selecionada mantém somente cada intermediária marcada", () =>
{
    var lowerId = CertificateChain.Fingerprint(lowerCert); var upperId = CertificateChain.Fingerprint(upperCert);
    Equal(PemService.ExportSelected(new[] { allPemPath }, new[] { upperId, lowerId }), Certs(lowerCert, upperCert));
    Equal(PemService.ExportSelected(new[] { allPemPath }, new[] { upperId }), Certs(upperCert));
    Equal(PemService.ExportSelected(new[] { caPath, rootPath }, new[] { upperId, lowerId }), Certs(lowerCert, upperCert));
});
Check("CRT selecionado não inclui chave e aceita CA raiz isolada", () =>
{
    Equal(PemService.ExportSelected(new[] { allPemPath }, new[] { CertificateChain.Fingerprint(rootCert) }), Certs(rootCert));
    var result = PemService.ExportSelected(new[] { encryptedChain }, new[] { CertificateChain.Fingerprint(domainCert) });
    Equal(result, Certs(domainCert)); Equal(result.Contains("PRIVATE KEY"), false);
});
Check("seleção vazia ou certificado indisponível não gera saída", () =>
{
    Throws<ArgumentException>(() => PemService.ExportSelected(new[] { allPemPath }, Array.Empty<string>()));
    Throws<ArgumentException>(() => PemService.ExportSelected(new[] { allPemPath }, new[] { new string('0', 64) }));
});
Check("PEM selecionado combina somente certificados marcados e chave correspondente", () =>
{
    var selected = new[] { CertificateChain.Fingerprint(domainCert), CertificateChain.Fingerprint(lowerCert) };
    var result = PemService.ExportSelected(new[] { allPemPath }, selected, allPemPath);
    using var loaded = X509Certificate2.CreateFromPem(result, result);
    Equal(loaded.HasPrivateKey, true);
    Equal(result, rsa.ExportPkcs8PrivateKeyPem().Replace("\r\n", "\n") + "\n" + Certs(domainCert, lowerCert));
    Throws<ArgumentException>(() => PemService.ExportSelected(new[] { allPemPath }, selected, otherKey));
    Throws<ArgumentException>(() => PemService.ExportSelected(new[] { allPemPath }, new[] { CertificateChain.Fingerprint(rootCert) }, key));
});
Check("exportar chave preserva criptografia sem pedir senha", () =>
{
    Equal(PemService.ExtractPrivateKey(encryptedChain), encryptedPem.Replace("\r\n", "\n") + "\n");
    Equal(PemService.ExtractPrivateKey(encDer), PemEncoding.WriteString("ENCRYPTED PRIVATE KEY", File.ReadAllBytes(encDer)).Replace("\r\n", "\n") + "\n");
    var ids = new[] { CertificateChain.Fingerprint(domainCert) };
    Throws<PasswordRequiredException>(() => PemService.ExportSelected(new[] { encryptedChain }, ids, encryptedChain));
    var result = PemService.ExportSelected(new[] { encryptedChain }, ids, encryptedChain, "teste123");
    using var loaded = X509Certificate2.CreateFromEncryptedPem(result, result, "teste123");
    Equal(loaded.HasPrivateKey, true);
});
Check("ordem exibida independe da ordem dos arquivos", () =>
{
    var infos = new[] { rootCert, upperCert, domainCert, lowerCert }.Select(CertificateChain.Describe);
    Equal(string.Join(",", CertificateChain.OrderDescriptions(infos).Select(c => c.Fingerprint)),
        string.Join(",", new[] { domainCert, lowerCert, upperCert, rootCert }.Select(CertificateChain.Fingerprint)));
});
Check("chaves DSA em PEM e DER preservadas", () =>
{
    using var dsa = DSA.Create(2048);
    var pem = dsa.ExportPkcs8PrivateKeyPem();
    var pemPath = Write("dsa.key", pem);
    var derPath = Binary("dsa.der", dsa.ExportPkcs8PrivateKey());
    Equal(PemService.Inspect(pemPath).Kind, InputKind.PrivateKey);
    Equal(PemService.Inspect(derPath).Kind, InputKind.PrivateKey);
    Equal(PemService.ExtractPrivateKey(pemPath), pem.Replace("\r\n", "\n") + "\n");
    Equal(PemService.ExtractPrivateKey(derPath), pem.Replace("\r\n", "\n") + "\n");
});
var preferencesRoot = Path.Combine(directory, "settings-" + Guid.NewGuid().ToString("N"));
Check("primeira abertura sugere idioma do sistema sem gravar nada", () =>
{
    var preferences = new SettingsStore(preferencesRoot, "es-MX");
    Equal(preferences.HasSavedLanguage, false); Equal(preferences.Language, "es");
    Equal(Directory.Exists(preferencesRoot), false);
    Equal(SettingsStore.Suggest("pt-BR"), "pt"); Equal(SettingsStore.Suggest("en-GB"), "en");
    Equal(SettingsStore.Suggest("fr-FR"), "en");
    Equal(SettingsStore.Suggest("pt_BR.UTF-8"), "pt"); Equal(SettingsStore.Suggest("es_ES.UTF-8:en"), "es");
});
Check("idioma persiste entre aberturas e prevalece sobre idioma do sistema", () =>
{
    var preferences = new SettingsStore(preferencesRoot, "pt-BR");
    Equal(preferences.Save("es"), true);
    var reopened = new SettingsStore(preferencesRoot, "en-US");
    Equal(reopened.HasSavedLanguage, true); Equal(reopened.Language, "es");
    Equal(reopened.Save("en"), true);
    Equal(new SettingsStore(preferencesRoot, "pt-BR").Language, "en");
    Equal(Directory.GetFiles(preferencesRoot, "*.tmp").Length, 0);
    using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(reopened.FilePath));
    Equal(json.RootElement.EnumerateObject().Count(), 1);
    Equal(json.RootElement.GetProperty("language").GetString(), "en");
});
Check("configuração inválida volta à escolha inicial e pode ser substituída", () =>
{
    foreach (var invalid in new[] { "{", "null", "[]", "{\"language\":42}", "{\"language\":\"fr\"}", new string('x', 4097) })
    {
        File.WriteAllText(Path.Combine(preferencesRoot, "settings.json"), invalid);
        var preferences = new SettingsStore(preferencesRoot, "pt-BR");
        Equal(preferences.HasSavedLanguage, false); Equal(preferences.Language, "pt");
        Equal(preferences.Save("pt"), true);
    }
});
Check("falha na gravação das preferências é recuperável sem criar arquivos ao lado do executável", () =>
{
    var blocked = Write("settings-blocked", "not a directory");
    var preferences = new SettingsStore(blocked, "es");
    Equal(preferences.Save("en"), false); Equal(preferences.HasSavedLanguage, false); Equal(preferences.Language, "es");
    Throws<ArgumentException>(() => preferences.Save("xx"));
});
Check("traduções preservam parâmetros e valores externos", () =>
{
    foreach (var language in L.Languages)
    {
        L.Use(language);
        foreach (var phrase in L.Keys)
        {
            var formatted = L.F(phrase, "VALUE-0", "VALUE-1");
            if (phrase.Contains("{0}")) Equal(formatted.Contains("VALUE-0"), true);
            if (phrase.Contains("{1}")) Equal(formatted.Contains("VALUE-1"), true);
            Equal(string.IsNullOrWhiteSpace(formatted), false);
        }
        Equal(L.T("CN=Certificado do Gabriel, O=Minha empresa"), "CN=Certificado do Gabriel, O=Minha empresa");
    }
    L.Use("pt");
});
Check("exportação e validação mantêm conteúdo e erros nos três idiomas", () =>
{
    var expected = PemService.ExportSelected(new[] { allPemPath }, new[] { CertificateChain.Fingerprint(domainCert) });
    foreach (var (language, role, passwordError) in new[] { ("pt", "Domínio", "Informe a senha"), ("en", "Domain", "Enter the key password"), ("es", "Dominio", "Introduce su contraseña") })
    {
        L.Use(language);
        Equal(CertificateChain.Describe(domainCert).Role, role);
        Equal(new PasswordRequiredException().Message.Contains(passwordError), true);
        Equal(PemService.ExportSelected(new[] { allPemPath }, new[] { CertificateChain.Fingerprint(domainCert) }), expected);
    }
    L.Use("pt");
});
Check("links do projeto aceitam somente repositório GitHub HTTPS e normalizam .git", () =>
{
    Equal(ProjectInfo.ParseRepository(" https://github.com/gabriel/CertBox.git/ ")?.AbsoluteUri, "https://github.com/gabriel/CertBox");
    foreach (var invalid in new[] { "", "javascript:alert(1)", "file:///C:/private", "http://github.com/owner/repo", "https://example.com/owner/repo", "https://github.com/owner/repo/issues", "https://github.com/user/repo?token=secret", "https://github.com/user/repo#fragment", "https://user:secret@github.com/owner/repo", "https://github.com:444/owner/repo", "https://github.com/user/.git" })
        Equal(ProjectInfo.ParseRepository(invalid), null);
    Equal(ProjectInfo.LicenseId, "GPL-3.0-or-later");
});
Console.WriteLine($"{count} testes passaram. Fixtures: {directory}");
