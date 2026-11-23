using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel.Abstractions;

namespace Urban.Operations.Application.Announcements;

/// <summary>
/// O e-mail de um comunicado: o assunto diz o grau quando importa e o texto traz o comunicado inteiro e o caminho do mural. A resposta de
/// quem recebe vai para o síndico que publicou (<c>replyTo</c>), e não para o endereço do sistema, que ninguém lê.
/// </summary>
internal static class AnnouncementEmail
{
    public static EmailMessage Build(Announcement announcement, string to, Uri mural, string? replyTo)
    {
        string subject = SubjectOf(announcement);
        List<string> lines =
        [
            $"O síndico publicou um comunicado {AudienceOf(announcement)} no Urban.",
            string.Empty,
            subject,
            string.Empty,
            announcement.Body,
            string.Empty,
            "Veja também no mural:",
            mural.AbsoluteUri,
        ];

        if (replyTo is not null)
        {
            lines.AddRange([string.Empty, "Para responder, é só responder a este e-mail: a resposta chega ao síndico."]);
        }

        return new EmailMessage(to, subject, string.Join('\n', lines), replyTo);
    }

    /// <summary>"Importante: título", "Manutenção: título" ou só o título, no comunicado geral.</summary>
    private static string SubjectOf(Announcement announcement) => announcement.Category switch
    {
        AnnouncementCategory.Important => $"Importante: {announcement.Title}",
        AnnouncementCategory.Maintenance => $"Manutenção: {announcement.Title}",
        _ => announcement.Title,
    };

    private static string AudienceOf(Announcement announcement) => announcement.Scope switch
    {
        AnnouncementScope.Block => $"para o bloco {announcement.Block}",
        AnnouncementScope.Unit => "para a sua unidade",
        _ => "para todo o condomínio",
    };
}
