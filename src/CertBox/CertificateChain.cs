// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel and CertBox contributors.
// Developed with AI; third-party components retain their original licenses.

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CertBox;

public sealed record CertificateField(string Name, string Value, string? Oid = null);

public sealed record CertificateInfo(string Subject, string Issuer, string Fingerprint, bool IsCa, bool IsRoot, IReadOnlyList<string> DnsNames)
{
    public string RoleKey => IsRoot ? "CA raiz" : IsCa ? "CA intermediária" : "Domínio";
    public string Role => L.T(RoleKey);
    public string Name { get; init; } = "";
    public DateTime NotBefore { get; init; }
    public DateTime NotAfter { get; init; }
    public string SerialNumber { get; init; } = "";
    public string PublicKey { get; init; } = "";
    public string SignatureAlgorithm { get; init; } = "";
    public IReadOnlyList<CertificateField> AdditionalFields { get; init; } = Array.Empty<CertificateField>();
}

public sealed record ChainSummary(IReadOnlyList<CertificateInfo> Certificates, string? Warning);

/// <summary>
/// Orders only supplied certificates. Checks link signatures, CA permissions and
/// path length. Does not assert public trust, hostname suitability or revocation.
/// </summary>
public static class CertificateChain
{
    public static IReadOnlyList<X509Certificate2> OrderForExport(IReadOnlyList<X509Certificate2> certificates)
    {
        if (certificates.Any(c => !IsCa(c))) return Order(certificates);
        // CA-only exports are valid too. Place children before their supplied issuers.
        var remaining = certificates.ToList();
        var ordered = new List<X509Certificate2>();
        while (remaining.Count > 0)
        {
            var next = remaining.Where(parent => !remaining.Any(child => child != parent
                && SameName(child.IssuerName, parent.SubjectName) && VerifySignature(child, parent))).ToArray();
            if (next.Length == 0) throw new ArgumentException(L.T("A cadeia contém um ciclo entre certificados."));
            ordered.AddRange(next); foreach (var certificate in next) remaining.Remove(certificate);
        }
        return ordered;
    }

    public static IReadOnlyList<CertificateInfo> OrderDescriptions(IEnumerable<CertificateInfo> certificates)
    {
        var all = certificates.DistinctBy(c => c.Fingerprint).ToArray();
        int Depth(CertificateInfo child, HashSet<string> visited)
        {
            if (!visited.Add(child.Fingerprint)) return 0;
            var parent = all.FirstOrDefault(c => c.Fingerprint != child.Fingerprint && c.Subject == child.Issuer);
            return parent == null ? 0 : 1 + Depth(parent, visited);
        }
        return all.OrderBy(c => c.IsCa).ThenByDescending(c => Depth(c, new HashSet<string>())).ThenBy(c => c.IsRoot).ToArray();
    }
    public static CertificateInfo Describe(X509Certificate2 certificate)
    {
        try
        {
            return new(certificate.Subject, certificate.Issuer, Fingerprint(certificate), IsCa(certificate),
                IsCa(certificate) && SameName(certificate.SubjectName, certificate.IssuerName) && VerifySignature(certificate, certificate), DnsNames(certificate))
            {
                Name = certificate.GetNameInfo(X509NameType.SimpleName, false),
                NotBefore = certificate.NotBefore.ToUniversalTime(),
                NotAfter = certificate.NotAfter.ToUniversalTime(),
                SerialNumber = certificate.SerialNumber,
                PublicKey = DescribePublicKey(certificate),
                SignatureAlgorithm = certificate.SignatureAlgorithm.FriendlyName ?? certificate.SignatureAlgorithm.Value ?? "—",
                AdditionalFields = DescribeAdditionalFields(certificate)
            };
        }
        catch (AsnContentException ex) { throw new ArgumentException(L.F("Extensão X.509 inválida em {0}", certificate.Subject), ex); }
    }

    private static IReadOnlyList<CertificateField> DescribeAdditionalFields(X509Certificate2 certificate)
    {
        var fields = new List<CertificateField> { new("Versão", "X.509 v" + certificate.Version) };
        foreach (var extension in certificate.Extensions)
        {
            var oid = extension.Oid?.Value ?? "Desconhecido";
            var name = oid switch
            {
                "2.5.29.14" => "Identificador da chave",
                "2.5.29.15" => "Uso da chave",
                "2.5.29.17" => "Outros nomes (SAN)",
                "2.5.29.18" => "Nomes do emissor",
                "2.5.29.19" => "Restrições de CA",
                "2.5.29.30" => "Restrições de nomes",
                "2.5.29.31" => "Distribuição de CRL",
                "2.5.29.32" => "Políticas do certificado",
                "2.5.29.33" => "Mapeamento de políticas",
                "2.5.29.35" => "Identificador do emissor",
                "2.5.29.36" => "Restrições de políticas",
                "2.5.29.37" => "Uso estendido da chave",
                "2.5.29.46" => "CRL incremental",
                "2.5.29.54" => "Inibição de políticas",
                "1.3.6.1.5.5.7.1.1" => "Acesso à autoridade (AIA)",
                "1.3.6.1.5.5.7.1.11" => "Acesso ao titular (SIA)",
                "1.3.6.1.5.5.7.1.24" => "Recursos TLS",
                "1.3.6.1.4.1.11129.2.4.2" => "Transparência (SCT)",
                _ => extension.Oid?.FriendlyName ?? "Extensão " + oid
            };
            string value;
            try
            {
                value = oid == "2.5.29.17" ? AdditionalSanNames(extension) : extension.Format(true).Trim();
                // DNS names already appear in the fixed fields above.
                if (oid == "2.5.29.17" && value.Length == 0) continue;
            }
            catch (Exception ex) when (ex is CryptographicException or AsnContentException or ArgumentException)
            {
                value = HexBytes(extension.RawData);
            }
            if (string.IsNullOrWhiteSpace(value)) value = HexBytes(extension.RawData);
            fields.Add(new(name, value + "\nOID: " + oid + (extension.Critical ? " · Crítica" : ""), oid));
        }
        return fields;
    }

    private static string HexBytes(byte[] data) => string.Join(" ", Convert.ToHexString(data).Chunk(2).Select(bytes => new string(bytes)));

    private static string AdditionalSanNames(X509Extension extension)
    {
        var sequence = new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadSequence();
        var names = new List<string>();
        while (sequence.HasData)
        {
            var tag = sequence.PeekTag();
            if (tag.TagClass != TagClass.ContextSpecific) throw new AsnContentException();
            switch (tag.TagValue)
            {
                case 2: sequence.ReadEncodedValue(); break;
                case 1: names.Add("E-mail: " + sequence.ReadCharacterString(UniversalTagNumber.IA5String, tag)); break;
                case 6: names.Add("URI: " + sequence.ReadCharacterString(UniversalTagNumber.IA5String, tag)); break;
                case 7: names.Add("IP: " + new System.Net.IPAddress(sequence.ReadOctetString(tag))); break;
                case 8: names.Add("Identificador: " + sequence.ReadObjectIdentifier(tag)); break;
                default: names.Add("Nome alternativo (tipo " + tag.TagValue + "): " + HexBytes(sequence.ReadEncodedValue().ToArray())); break;
            }
        }
        return string.Join("\n", names);
    }

    private static string DescribePublicKey(X509Certificate2 certificate)
    {
        using AsymmetricAlgorithm? key = (AsymmetricAlgorithm?)certificate.GetRSAPublicKey()
            ?? (AsymmetricAlgorithm?)certificate.GetECDsaPublicKey() ?? certificate.GetDSAPublicKey();
        var algorithm = certificate.PublicKey.Oid.Value switch
        {
            "1.2.840.113549.1.1.1" or "1.2.840.113549.1.1.10" => "RSA",
            "1.2.840.10045.2.1" => "EC",
            "1.2.840.10040.4.1" => "DSA",
            _ => certificate.PublicKey.Oid.FriendlyName ?? certificate.PublicKey.Oid.Value ?? "—"
        };
        return key == null ? algorithm : $"{algorithm} · {key.KeySize} bits";
    }

    public static string Fingerprint(X509Certificate2 certificate) => certificate.GetCertHashString(HashAlgorithmName.SHA256);
    public static bool IsCa(X509Certificate2 certificate) => certificate.Extensions.OfType<X509BasicConstraintsExtension>().Any(e => e.CertificateAuthority);
    private static bool SameName(X500DistinguishedName a, X500DistinguishedName b) => a.RawData.AsSpan().SequenceEqual(b.RawData) || string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

    public static List<X509Certificate2> Order(IEnumerable<X509Certificate2> certificates, AsymmetricAlgorithm? key = null)
    {
        var all = certificates.DistinctBy(Fingerprint).ToList();
        if (all.Count > 128) throw new ArgumentException(L.T("O limite é de 128 certificados por conversão."));
        var leaves = all.Where(c => !IsCa(c)).ToList();
        if (leaves.Count == 0) throw new ArgumentException(L.T("Foram recebidas apenas CAs. Adicione o certificado do domínio."));
        if (leaves.Count > 1) throw new ArgumentException(L.T("Há mais de um certificado de domínio. Selecione um domínio e somente a cadeia correspondente."));
        var leaf = leaves[0];
        if (key != null && !MatchesKey(leaf, key)) throw new ArgumentException(L.T("A chave privada não corresponde ao certificado do domínio."));
        var ordered = new List<X509Certificate2> { leaf };
        var visited = new HashSet<string> { Fingerprint(leaf) };
        while (true)
        {
            var child = ordered[^1];
            var selfIssued = SameName(child.SubjectName, child.IssuerName);
            if (selfIssued && VerifySignature(child, child)) break;
            var namedParents = all.Where(c => Fingerprint(c) != Fingerprint(child) && IsCa(c) && SameName(child.IssuerName, c.SubjectName)).ToList();
            var parents = namedParents.Where(c => KeyIdentifiersMatch(child, c) && VerifySignature(child, c)).ToList();
            if (parents.Count == 0)
            {
                if (namedParents.Count > 0 || selfIssued) throw new ArgumentException(L.F("A assinatura do certificado não confere com a CA fornecida: {0}", child.Subject));
                break; // A missing terminal issuer can be an intentionally omitted root.
            }
            if (parents.Count > 1) throw new ArgumentException(L.F("Cadeia ambígua: mais de uma CA válida pode emitir {0}. Selecione apenas uma cadeia.", child.Subject));
            var parent = parents[0];
            if (!visited.Add(Fingerprint(parent))) throw new ArgumentException(L.T("A cadeia contém um ciclo entre certificados."));
            var usage = parent.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
            if (usage != null && (usage.KeyUsages & X509KeyUsageFlags.KeyCertSign) == 0)
                throw new ArgumentException(L.F("A CA não tem permissão para assinar certificados: {0}", parent.Subject));
            var basic = parent.Extensions.OfType<X509BasicConstraintsExtension>().First();
            int subordinateCas = ordered.Skip(1).Count(c => IsCa(c) && !SameName(c.SubjectName, c.IssuerName));
            if (basic.HasPathLengthConstraint && subordinateCas > basic.PathLengthConstraint)
                throw new ArgumentException(L.F("A cadeia ultrapassa o limite de intermediários permitido por {0}", parent.Subject));
            ordered.Add(parent);
        }
        if (ordered.Count != all.Count)
            throw new ArgumentException(L.T("Há certificados sem ligação com o domínio. A cadeia pode estar incompleta ou conter uma CA de outro certificado."));
        return ordered;
    }

    public static ChainSummary Summarize(IReadOnlyList<X509Certificate2> ordered)
    {
        var infos = ordered.Select(Describe).ToArray();
        var top = ordered[^1];
        string? warning = null;
        if (!SameName(top.SubjectName, top.IssuerName))
            warning = ordered.Count == 1
                ? L.T("Nenhuma CA foi fornecida. O servidor pode precisar dos certificados intermediários.")
                : L.T("O emissor final não foi fornecido. A raiz pode ser omitida, mas a completude da cadeia não foi confirmada.");
        return new ChainSummary(infos, warning);
    }

    private static bool MatchesKey(X509Certificate2 certificate, AsymmetricAlgorithm key)
    {
        using AsymmetricAlgorithm? publicKey = (AsymmetricAlgorithm?)certificate.GetRSAPublicKey() ?? (AsymmetricAlgorithm?)certificate.GetECDsaPublicKey() ?? certificate.GetDSAPublicKey();
        return publicKey != null && publicKey.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(key.ExportSubjectPublicKeyInfo());
    }

    private static IReadOnlyList<string> DnsNames(X509Certificate2 certificate)
    {
        var extension = certificate.Extensions["2.5.29.17"];
        if (extension == null) return Array.Empty<string>();
        var names = new List<string>();
        var sequence = new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadSequence();
        while (sequence.HasData)
        {
            var tag = sequence.PeekTag();
            if (tag.TagClass == TagClass.ContextSpecific && tag.TagValue == 2)
                names.Add(sequence.ReadCharacterString(UniversalTagNumber.IA5String, new Asn1Tag(TagClass.ContextSpecific, 2)));
            else sequence.ReadEncodedValue();
        }
        return names;
    }

    private static bool KeyIdentifiersMatch(X509Certificate2 child, X509Certificate2 parent)
    {
        try
        {
            var extension = child.Extensions["2.5.29.35"];
            if (extension == null) return true;
            var aki = new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadSequence();
            while (aki.HasData)
            {
                var tag = aki.PeekTag();
                if (tag.TagClass != TagClass.ContextSpecific) throw new AsnContentException();
                switch (tag.TagValue)
                {
                    case 0:
                        var id = aki.ReadOctetString(tag);
                        var ski = parent.Extensions["2.5.29.14"];
                        if (ski != null && !id.AsSpan().SequenceEqual(new AsnReader(ski.RawData, AsnEncodingRules.DER).ReadOctetString())) return false;
                        break;
                    case 1:
                        var names = aki.ReadSequence(tag);
                        var directoryNames = new List<X500DistinguishedName>();
                        while (names.HasData)
                        {
                            var nameTag = names.PeekTag();
                            if (nameTag.TagClass == TagClass.ContextSpecific && nameTag.TagValue == 4)
                            {
                                var directory = names.ReadSequence(nameTag);
                                directoryNames.Add(new X500DistinguishedName(directory.ReadEncodedValue().ToArray()));
                                directory.ThrowIfNotEmpty();
                            }
                            else names.ReadEncodedValue();
                        }
                        if (directoryNames.Count > 0 && !directoryNames.Any(n => SameName(n, parent.IssuerName))) return false;
                        break;
                    case 2:
                        var serial = new System.Numerics.BigInteger(aki.ReadIntegerBytes(tag).Span, isUnsigned: false, isBigEndian: true);
                        if (serial != new System.Numerics.BigInteger(parent.GetSerialNumber(), isUnsigned: true, isBigEndian: false)) return false;
                        break;
                    default: throw new AsnContentException();
                }
            }
            return true;
        }
        catch (AsnContentException ex) { throw new ArgumentException(L.F("Identificador da CA inválido em {0}", child.Subject), ex); }
    }

    public static bool VerifySignature(X509Certificate2 child, X509Certificate2 issuer)
    {
        try
        {
            var reader = new AsnReader(child.RawData, AsnEncodingRules.DER);
            var certificate = reader.ReadSequence();
            var tbs = certificate.ReadEncodedValue();
            var algorithmBytes = certificate.ReadEncodedValue();
            var algorithm = new AsnReader(algorithmBytes, AsnEncodingRules.DER).ReadSequence();
            string oid = algorithm.ReadObjectIdentifier();
            var signature = certificate.ReadBitString(out int unused);
            if (unused != 0) return false;
            certificate.ThrowIfNotEmpty(); reader.ThrowIfNotEmpty();
            // X.509 requires the inner and outer signature algorithm identifiers to agree.
            var signed = new AsnReader(tbs, AsnEncodingRules.DER).ReadSequence();
            if (signed.PeekTag().TagClass == TagClass.ContextSpecific) signed.ReadEncodedValue();
            signed.ReadIntegerBytes();
            if (!signed.ReadEncodedValue().Span.SequenceEqual(algorithmBytes.Span)) return false;

            HashAlgorithmName hash = oid switch
            {
                "1.2.840.113549.1.1.5" or "1.2.840.10045.4.1" or "1.2.840.10040.4.3" => HashAlgorithmName.SHA1,
                "1.2.840.113549.1.1.11" or "1.2.840.10045.4.3.2" or "2.16.840.1.101.3.4.3.2" => HashAlgorithmName.SHA256,
                "1.2.840.113549.1.1.12" or "1.2.840.10045.4.3.3" => HashAlgorithmName.SHA384,
                "1.2.840.113549.1.1.13" or "1.2.840.10045.4.3.4" => HashAlgorithmName.SHA512,
                "1.2.840.113549.1.1.10" => ReadPssHash(algorithm),
                _ => throw new ArgumentException(L.F("Algoritmo de assinatura de certificado não suportado: {0}", oid))
            };
            if (oid.StartsWith("1.2.840.113549.1.1.", StringComparison.Ordinal))
            {
                using var rsa = issuer.GetRSAPublicKey();
                return rsa != null && rsa.VerifyData(tbs.Span, signature, hash, oid == "1.2.840.113549.1.1.10" ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1);
            }
            if (oid.StartsWith("1.2.840.10045.4.", StringComparison.Ordinal))
            {
                using var ec = issuer.GetECDsaPublicKey();
                return ec != null && ec.VerifyData(tbs.Span, signature, hash, DSASignatureFormat.Rfc3279DerSequence);
            }
            using var dsa = issuer.GetDSAPublicKey();
            return dsa != null && dsa.VerifyData(tbs.Span, signature, hash, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (Exception ex) when (ex is CryptographicException or AsnContentException or OverflowException) { return false; }
    }

    private static HashAlgorithmName ReadPssHash(AsnReader algorithm)
    {
        var hash = HashAlgorithmName.SHA1;
        var mgf = HashAlgorithmName.SHA1;
        int saltLength = 20, trailer = 1, previous = -1;
        if (algorithm.HasData)
        {
            var parameters = algorithm.ReadSequence();
            while (parameters.HasData)
            {
                var tag = parameters.PeekTag();
                if (tag.TagClass != TagClass.ContextSpecific || tag.TagValue <= previous || tag.TagValue > 3)
                    throw new ArgumentException(L.T("Parâmetros RSA-PSS inválidos."));
                previous = tag.TagValue;
                var field = parameters.ReadSequence(tag);
                switch (tag.TagValue)
                {
                    case 0: hash = ReadHashAlgorithm(field); break;
                    case 1:
                        var mask = field.ReadSequence();
                        if (mask.ReadObjectIdentifier() != "1.2.840.113549.1.1.8") throw new ArgumentException(L.T("RSA-PSS requer MGF1 nesta versão."));
                        mgf = ReadHashAlgorithm(mask); mask.ThrowIfNotEmpty(); break;
                    case 2: saltLength = checked((int)field.ReadInteger()); break;
                    case 3: trailer = checked((int)field.ReadInteger()); break;
                }
                field.ThrowIfNotEmpty();
            }
        }
        algorithm.ThrowIfNotEmpty();
        int hashSize = hash.Name switch { "SHA1" => 20, "SHA256" => 32, "SHA384" => 48, "SHA512" => 64, _ => 0 };
        if (mgf != hash || saltLength != hashSize || trailer != 1)
            throw new ArgumentException(L.T("Parâmetros RSA-PSS não suportados: use MGF1 com o mesmo hash e salt do tamanho do hash."));
        return hash;
    }

    private static HashAlgorithmName ReadHashAlgorithm(AsnReader reader)
    {
        var algorithm = reader.ReadSequence();
        string oid = algorithm.ReadObjectIdentifier();
        if (algorithm.HasData) algorithm.ReadNull();
        algorithm.ThrowIfNotEmpty();
        return oid switch
        {
            "1.3.14.3.2.26" => HashAlgorithmName.SHA1,
            "2.16.840.1.101.3.4.2.1" => HashAlgorithmName.SHA256,
            "2.16.840.1.101.3.4.2.2" => HashAlgorithmName.SHA384,
            "2.16.840.1.101.3.4.2.3" => HashAlgorithmName.SHA512,
            _ => throw new ArgumentException(L.F("Hash RSA-PSS não suportado: {0}", oid))
        };
    }
}
