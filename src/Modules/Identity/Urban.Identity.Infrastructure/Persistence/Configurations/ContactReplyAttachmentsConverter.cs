using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Urban.Identity.Domain.Contact;

namespace Urban.Identity.Infrastructure.Persistence.Configurations;

/// <summary>Nome, tipo e tamanho dos arquivos de uma resposta vão para uma coluna jsonb: são sempre lidos e gravados inteiros, nunca consultados por dentro.</summary>
internal sealed class ContactReplyAttachmentsConverter()
    : ValueConverter<IReadOnlyList<ContactReplyAttachment>, string>(attachments => ToJson(attachments), json => FromJson(json))
{
    public static readonly ValueComparer<IReadOnlyList<ContactReplyAttachment>> Comparer =
        new((left, right) => AreEqual(left, right), attachments => HashOf(attachments), attachments => Copy(attachments));

    private static string ToJson(IReadOnlyList<ContactReplyAttachment> attachments) => JsonSerializer.Serialize(attachments);

    private static IReadOnlyList<ContactReplyAttachment> FromJson(string json) => JsonSerializer.Deserialize<ContactReplyAttachment[]>(json) ?? [];

    private static bool AreEqual(IReadOnlyList<ContactReplyAttachment>? left, IReadOnlyList<ContactReplyAttachment>? right) =>
        ReferenceEquals(left, right) || (left is not null && right is not null && left.SequenceEqual(right));

    private static int HashOf(IReadOnlyList<ContactReplyAttachment> attachments) =>
        attachments.Aggregate(0, (hash, attachment) => HashCode.Combine(hash, attachment));

    private static IReadOnlyList<ContactReplyAttachment> Copy(IReadOnlyList<ContactReplyAttachment> attachments) => [.. attachments];
}
