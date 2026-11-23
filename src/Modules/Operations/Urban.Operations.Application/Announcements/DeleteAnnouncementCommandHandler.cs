using MediatR;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Operations.Application.Announcements;

/// <summary>Apaga o comunicado e as leituras dele. A versão lida na listagem precisa vir junto.</summary>
public sealed class DeleteAnnouncementCommandHandler(IAnnouncements announcements, ITenantContext tenant, AnnouncementLiveNotifier live) : IRequestHandler<DeleteAnnouncementCommand, Result>
{
    public async Task<Result> Handle(DeleteAnnouncementCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(live);

        if (tenant.CondominiumId is null)
        {
            return Result.Failure(OperationsErrors.CondominiumNotFound);
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return Result.Failure(OperationsErrors.VersionRequired);
        }

        AnnouncementRecord? record = await announcements.FindAsync(request.AnnouncementId, cancellationToken);

        if (record is null)
        {
            return Result.Failure(AnnouncementErrors.NotFound);
        }

        return request.Version == record.Version
            ? await announcements.DeleteAsync(record, cancellationToken)
            : Result.Failure(OperationsErrors.ConcurrencyConflict);
    }
}
