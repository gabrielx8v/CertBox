# What CertBox checks

CertBox inspects X.509 certificates and exports certificates and keys as CRT, PEM and KEY files. Processing is local, and export operations protect input files from being overwritten.

The validity-period indicator compares certificate dates with the computer's clock. It does not confirm public trust, website matching, revocation or complete validity for TLS use. Chain ordering uses only supplied certificates; the app does not query or download issuers over the internet.

Checkboxes control exported content. The details panel's selector changes only the certificate being viewed. All checkboxes start unchecked.

Export CRT saves only checked certificates, without a private key. Create PEM also includes the loaded key when the domain certificate is checked, after verifying the pair. Save key writes only the key. Encryption of protected keys is preserved; creating a PEM with a protected key requires its password to verify the pair.

PEM/DER certificates and RSA, EC and DSA keys are accepted in the formats documented in the README. PFX/P12, PKCS#7, OpenSSH, Ed25519 and PEM with legacy Proc-Type encryption are unsupported. Limits are 32 certificate files, one key, 128 certificates and 16 MB per file.

CRT and KEY files can also contain PEM text; an extension indicates the usual purpose and does not guarantee encoding. The app identifies file contents.
