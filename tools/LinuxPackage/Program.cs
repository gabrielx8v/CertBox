// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel and CertBox contributors.
// Developed with AI; third-party components retain their original licenses.

using System.Formats.Tar;
using System.IO.Compression;
using System.Text;

// Build-time packaging utility, never included as an end-user application.
var executable = Path.GetFullPath(args[0]);
var icon = Path.GetFullPath(args[1]);
var archive = Path.GetFullPath(args[2]);
var version = args[3];
var repository = args[4];
var header = new byte[20];
using (var file = File.OpenRead(executable)) file.ReadExactly(header);
if (!header.AsSpan(0, 4).SequenceEqual(new byte[] { 0x7f, 0x45, 0x4c, 0x46 }) || header[4] != 2 || header[18] != 0x3e || header[19] != 0)
    throw new InvalidDataException("Expected a Linux x64 ELF executable.");
Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
using (var output = File.Create(archive))
using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
using (var writer = new TarWriter(gzip, TarEntryFormat.Ustar))
{
    void Add(string name, Stream data, UnixFileMode mode)
    {
        var entry = new UstarTarEntry(TarEntryType.RegularFile, "CertBox/" + name)
        {
            DataStream = data, Mode = mode, ModificationTime = DateTimeOffset.UnixEpoch,
            Uid = 0, Gid = 0, UserName = "", GroupName = ""
        };
        writer.WriteEntry(entry);
    }
    var read = UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    using var binary = File.OpenRead(executable);
    Add("certbox", binary, read | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    using var png = File.OpenRead(icon);
    Add("certbox.png", png, read | UnixFileMode.UserWrite);
    var instructions = $"""
        CertBox {version} — Linux x64 — graphical application
        Desenvolvido com IA / Developed with AI / Desarrollado con IA

        Extraia o arquivo e abra certbox pelo gerenciador de arquivos.
        Extract the archive and open certbox in your file manager.
        Extrae el archivo y abre certbox desde el gestor de archivos.

        A versão Linux não foi testada manualmente.
        The Linux version has not been manually tested.
        La versión Linux no ha sido probada manualmente.

        X11 or XWayland, glibc, fontconfig, FreeType, OpenSSL and desktop system
        libraries are required. The .NET runtime is included. This is a GUI,
        without a separate CLI application or a shell launcher.

        Project, source, documentation and support:
        {repository}

        GPL-3.0-or-later. Full license, privacy policy and third-party notices
        are available in Settings. No certificates or keys are sent to the internet.
        """;
    using var text = new MemoryStream(Encoding.UTF8.GetBytes(instructions));
    Add("README.txt", text, read | UnixFileMode.UserWrite);
}
using (var input = File.OpenRead(archive))
using (var gzip = new GZipStream(input, CompressionMode.Decompress))
using (var reader = new TarReader(gzip))
{
    var first = reader.GetNextEntry() ?? throw new InvalidDataException("Empty Linux archive.");
    if (first.Name != "CertBox/certbox" || (first.Mode & UnixFileMode.UserExecute) == 0) throw new InvalidDataException("Linux executable mode missing.");
}
Console.WriteLine("Linux x64 GUI packaged with executable permissions: " + archive);
