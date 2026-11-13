using FluentAssertions;
using Urban.Identity.Domain.Contact;
using Urban.SharedKernel;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Domain;

public class ContactReplyAttachmentTests
{
    private const string WordType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string ExcelType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static TheoryData<string, byte[], string> AllowedFiles => new()
    {
        { "ata.pdf", SampleFiles.Pdf(), "application/pdf" },
        { "FOTO.PNG", SampleFiles.Png(), "image/png" },
        { "foto.jpg", SampleFiles.Jpeg(), "image/jpeg" },
        { "foto.jpeg", SampleFiles.Jpeg(), "image/jpeg" },
        { "carta.docx", SampleFiles.Docx(), WordType },
        { "planilha.xlsx", SampleFiles.Xlsx(), ExcelType },
        { "dados.csv", SampleFiles.Text("nome;valor\nAna;10\n"), "text/csv" },
        { "nota.txt", SampleFiles.Text(), "text/plain" },
    };

    public static TheoryData<string, byte[]> FilesWhoseContentIsNotTheTypeTheyClaim => new()
    {
        { "ata.pdf", SampleFiles.Png() },
        { "foto.png", SampleFiles.Pdf() },
        { "foto.jpg", SampleFiles.Png() },
        { "carta.docx", SampleFiles.Xlsx() },
        { "carta.docx", SampleFiles.PlainZip() },
        { "carta.docx", SampleFiles.Text() },
        { "planilha.xlsx", SampleFiles.Docx() },
        { "carta.docx", [0x50, 0x4B, 0x03, 0x04, 0x01, 0x02, 0x03] },
        { "dados.txt", [0x41, 0x00, 0x42] },
        { "dados.csv", [0x01, 0x41, 0x42] },
    };

    [Theory]
    [MemberData(nameof(AllowedFiles))]
    public void From_AcceptsEachAllowedTypeWhenTheContentMatches(string fileName, byte[] content, string contentType)
    {
        Result<ContactReplyAttachment> result = ContactReplyAttachment.From(fileName, content);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new ContactReplyAttachment(fileName, contentType, content.Length));
    }

    [Theory]
    [InlineData("C:\\Users\\ana\\ata.pdf", "ata.pdf")]
    [InlineData("../../ata.pdf", "ata.pdf")]
    [InlineData("pasta/sub/ata.pdf", "ata.pdf")]
    [InlineData("  ata.pdf  ", "ata.pdf")]
    public void From_KeepsOnlyTheNameOfTheFile(string raw, string expected)
    {
        ContactReplyAttachment.From(raw, SampleFiles.Pdf()).Value.FileName.Should().Be(expected);
    }

    [Fact]
    public void From_AcceptsANameOfExactlyTheMaximumLength()
    {
        string name = new string('a', ContactReplyAttachment.MaxFileNameLength - ".pdf".Length) + ".pdf";

        ContactReplyAttachment.From(name, SampleFiles.Pdf()).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".pdf")]
    [InlineData("ata<1>.pdf")]
    [InlineData("ata\"x\".pdf")]
    [InlineData("ata|1.pdf")]
    [InlineData("ata?.pdf")]
    [InlineData("ata*.pdf")]
    [InlineData("ata:1.pdf")]
    [InlineData("ata\0.pdf")]
    [InlineData("ata\n.pdf")]
    [InlineData("pasta/")]
    public void From_RefusesANameThatIsEmptyOrHasForbiddenCharacters(string? name)
    {
        ContactReplyAttachment.From(name, SampleFiles.Pdf()).Error.Should().Be(ContactReplyErrors.AttachmentNameInvalid);
    }

    [Fact]
    public void From_RefusesANameLongerThanTheMaximum()
    {
        string name = new string('a', ContactReplyAttachment.MaxFileNameLength - ".pdf".Length + 1) + ".pdf";

        ContactReplyAttachment.From(name, SampleFiles.Pdf()).Error.Should().Be(ContactReplyErrors.AttachmentNameInvalid);
    }

    [Theory]
    [InlineData("programa.exe")]
    [InlineData("script.js")]
    [InlineData("ata.pdf.exe")]
    [InlineData("pagina.html")]
    [InlineData("imagem.svg")]
    [InlineData("pacote.zip")]
    [InlineData("macro.docm")]
    [InlineData("antigo.doc")]
    [InlineData("antiga.xls")]
    [InlineData("sem-extensao")]
    [InlineData("termina-com-ponto.")]
    public void From_RefusesATypeThatIsNotOnTheList(string name)
    {
        ContactReplyAttachment.From(name, SampleFiles.Pdf()).Error.Should().Be(ContactReplyErrors.AttachmentTypeNotAllowed);
    }

    [Fact]
    public void From_RefusesAnEmptyFile()
    {
        ContactReplyAttachment.From("ata.pdf", []).Error.Should().Be(ContactReplyErrors.AttachmentContentInvalid);
    }

    [Fact]
    public void From_RefusesAMissingFile()
    {
        ContactReplyAttachment.From("ata.pdf", null).Error.Should().Be(ContactReplyErrors.AttachmentContentInvalid);
    }

    [Theory]
    [MemberData(nameof(FilesWhoseContentIsNotTheTypeTheyClaim))]
    public void From_ChecksTheRealContentAndNotJustTheExtension(string fileName, byte[] content)
    {
        ContactReplyAttachment.From(fileName, content).Error.Should().Be(ContactReplyErrors.AttachmentContentInvalid);
    }

    [Fact]
    public void From_AcceptsTextInAnyEncodingThatKeepsTheControlBytesOut()
    {
        byte[] windows1252 = [0x6E, 0x6F, 0x6D, 0x65, 0x3B, 0xE7, 0xE3, 0x6F, 0x0D, 0x0A];

        ContactReplyAttachment.From("dados.csv", windows1252).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void From_AcceptsAFileOfExactlyFiveMegabytesAndRefusesOneByteMore()
    {
        byte[] limit = SampleFiles.PaddedTo(SampleFiles.Pdf(), ContactReplyAttachment.MaxFileBytes);
        byte[] over = SampleFiles.PaddedTo(SampleFiles.Pdf(), ContactReplyAttachment.MaxFileBytes + 1);

        ContactReplyAttachment.From("ata.pdf", limit).IsSuccess.Should().BeTrue();
        ContactReplyAttachment.From("ata.pdf", over).Error.Should().Be(ContactReplyErrors.AttachmentTooLarge);
    }

    [Fact]
    public void MaxFileBytes_IsFiveMebibytes()
    {
        ContactReplyAttachment.MaxFileBytes.Should().Be(5 * 1024 * 1024);
    }
}
