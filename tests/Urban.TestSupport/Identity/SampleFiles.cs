using System.IO.Compression;
using System.Text;

namespace Urban.TestSupport.Identity;

/// <summary>Arquivos pequenos e válidos de cada tipo aceito como anexo, montados em memória.</summary>
public static class SampleFiles
{
    public static byte[] Pdf() => Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\n%%EOF");

    public static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    public static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

    public static byte[] Docx() => Zip("word/document.xml");

    public static byte[] Xlsx() => Zip("xl/workbook.xml");

    /// <summary>Um zip válido que não é de nenhum dos formatos do Office.</summary>
    public static byte[] PlainZip() => Zip("leia-me.txt");

    public static byte[] Text(string text = "linha 1\nlinha 2\n") => Encoding.UTF8.GetBytes(text);

    /// <summary>O mesmo conteúdo, estendido com zeros até ter exatamente <paramref name="size"/> bytes.</summary>
    public static byte[] PaddedTo(byte[] start, int size)
    {
        byte[] bytes = new byte[size];
        start.CopyTo(bytes, 0);

        return bytes;
    }

    private static byte[] Zip(string entryName)
    {
        using MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = zip.CreateEntry(entryName);
            using StreamWriter writer = new(entry.Open());
            writer.Write("<xml/>");
        }

        return stream.ToArray();
    }
}
