using System;
using System.IO;
using System.Text;

namespace NoteMode.Services;

/// <summary>
/// Reads and writes text files the way an editor should: the encoding a file was opened with
/// (BOM or not, UTF-16, legacy ANSI) is the one it is saved with, files another program has open
/// (logs) can still be read, and a save never leaves a half-written file behind.
/// </summary>
public static class TextFileIO
{
    /// <summary>UTF-8 without a BOM: what new files are saved as.</summary>
    public static readonly Encoding DefaultEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
    private static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private static readonly Lazy<Encoding> Ansi = new(() =>
    {
        // Files that are not valid UTF-8 are almost always Windows-1252 (or its Latin-1 subset).
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    });

    public static (string Text, Encoding Encoding) Read(string path)
    {
        byte[] bytes;
        // ReadWrite | Delete: a log file another process is still writing to can be opened.
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            using var memory = new MemoryStream(stream.CanSeek ? (int)Math.Min(stream.Length, int.MaxValue) : 0);
            stream.CopyTo(memory);
            bytes = memory.ToArray();
        }

        var encoding = Detect(bytes, out var bomLength);
        return (encoding.GetString(bytes, bomLength, bytes.Length - bomLength), encoding);
    }

    public static Encoding Detect(ReadOnlySpan<byte> bytes, out int bomLength)
    {
        if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) { bomLength = 3; return Utf8Bom; }
        if (bytes.StartsWith(new byte[] { 0xFF, 0xFE, 0x00, 0x00 })) { bomLength = 4; return Encoding.UTF32; }
        if (bytes.StartsWith(new byte[] { 0xFF, 0xFE })) { bomLength = 2; return Encoding.Unicode; }
        if (bytes.StartsWith(new byte[] { 0xFE, 0xFF })) { bomLength = 2; return Encoding.BigEndianUnicode; }

        bomLength = 0;
        try
        {
            StrictUtf8.GetCharCount(bytes);
            return DefaultEncoding;
        }
        catch (DecoderFallbackException)
        {
            return Ansi.Value;
        }
    }

    /// <summary>Short name for the status bar, e.g. "UTF-8", "UTF-8 BOM", "UTF-16 LE", "Windows-1252".</summary>
    public static string DisplayName(Encoding encoding) => encoding switch
    {
        UTF8Encoding utf8 => utf8.GetPreamble().Length > 0 ? "UTF-8 BOM" : "UTF-8",
        UnicodeEncoding when encoding.CodePage == 1201 => "UTF-16 BE",
        UnicodeEncoding => "UTF-16 LE",
        UTF32Encoding => "UTF-32",
        _ => encoding.WebName switch
        {
            "windows-1252" => "Windows-1252",
            var name => name.ToUpperInvariant()
        }
    };

    /// <summary>
    /// Writes <paramref name="text"/> in <paramref name="encoding"/>. An existing file is replaced
    /// through a temporary sibling, so running out of disk space or a crash mid-save leaves the
    /// original intact. Throws on failure (read-only file, no permission, file locked).
    /// </summary>
    public static void Write(string path, string text, Encoding encoding)
    {
        var preamble = encoding.GetPreamble();
        var body = encoding.GetBytes(text);

        if (!File.Exists(path))
        {
            WriteBytes(path, preamble, body);
            return;
        }

        // Replacing via rename would succeed on Linux/macOS even for a read-only file.
        if (File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly))
            throw new UnauthorizedAccessException($"{Path.GetFileName(path)} is read-only.");

        var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        var temp = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            WriteBytes(temp, preamble, body);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // The folder does not allow new files (but the file itself may be writable).
            TryDelete(temp);
            WriteBytes(path, preamble, body);
            return;
        }

        try
        {
            // Keeps the original's attributes and permissions.
            File.Replace(temp, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        catch (UnauthorizedAccessException)
        {
            // Read-only or protected file: report it rather than work around it.
            TryDelete(temp);
            throw;
        }
        catch (IOException)
        {
            // File systems without an atomic replace (some network shares). Fall back to
            // writing in place; that still fails loudly if the file is locked.
            TryDelete(temp);
            WriteBytes(path, preamble, body);
        }
    }

    private static void WriteBytes(string path, byte[] preamble, byte[] body)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(preamble);
        stream.Write(body);
        stream.Flush(flushToDisk: true);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best effort */ }
    }
}
