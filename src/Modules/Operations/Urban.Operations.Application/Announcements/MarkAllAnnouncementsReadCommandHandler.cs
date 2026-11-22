using MediatR;
using Urban.Operations.Application.Abstractions;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Operations.Application.Announcements;

/// <summary>Marca como lidos todos os comunicados que chegam à conta (só os da categoria, se informada).</summary>
public sealed class MarkAllAnnouncementsReadCommandHandler(
    IAnnouncements announcements,
    IAnnouncementAudience directory,
    ITenantContext tenant,
    IClock clock) : IRequestHandler<MarkAllAnnouncementsReadCommand, Result<int>>
{
    private readonly IAnnouncements _announcements = announcements;
    private readonly IAnnouncementAudience _directory = directory;
    private readonly ITenantContext _tenant = tenant;
    private readonly IClock _clock = clock;

    public Task<Result<int>> Handle(MarkAllAnnouncementsReadCommand request, CancellationToken cancellationToken) =>
        throw new NotImplementedException($"{_announcements} {_directory} {_tenant} {_clock}");
}
