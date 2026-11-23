using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel.Abstractions;

namespace Urban.Operations.Application.Announcements;

/// <summary>O e-mail de um comunicado: o assunto diz o grau quando importa e o texto traz o comunicado inteiro e o caminho do mural.</summary>
internal static class AnnouncementEmail
{
    public static EmailMessage Build(Announcement announcement, string to, Uri mural) =>
        throw new NotImplementedException($"Não implementado: {announcement.Id} {to} {mural}");
}
