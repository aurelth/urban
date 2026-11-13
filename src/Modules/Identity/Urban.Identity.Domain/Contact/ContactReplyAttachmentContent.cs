using System.IO.Compression;

namespace Urban.Identity.Domain.Contact;

/// <summary>
/// Confere se o conteúdo é do tipo que a extensão diz. Docx e xlsx são zip, então o cabeçalho não basta: o arquivo precisa ter a
/// parte que só o Word (<c>word/document.xml</c>) ou o Excel (<c>xl/workbook.xml</c>) criam. Só a lista de entradas do zip é
/// lida, nada é descompactado. Texto e CSV não têm assinatura: valem se não houver byte de controle (compatível com UTF-8 e com ANSI).
/// </summary>
internal static class ContactReplyAttachmentContent
{
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];

    public static bool Matches(string extension, byte[] content) => extension.ToLowerInvariant() switch
    {
        ".pdf" => content.AsSpan().StartsWith("%PDF-"u8),
        ".png" => content.AsSpan().StartsWith(PngSignature),
        ".jpg" or ".jpeg" => content.AsSpan().StartsWith(JpegSignature),
        ".docx" => HasZipEntry(content, "word/document.xml"),
        ".xlsx" => HasZipEntry(content, "xl/workbook.xml"),
        ".csv" or ".txt" => IsPlainText(content),
        _ => false,
    };

    private static bool HasZipEntry(byte[] content, string entryName)
    {
        try
        {
            using MemoryStream stream = new(content, writable: false);
            using ZipArchive zip = new(stream, ZipArchiveMode.Read);

            return zip.GetEntry(entryName) is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static bool IsPlainText(byte[] content) => content.All(b => b >= 0x20 || b is 0x09 or 0x0A or 0x0D);
}
