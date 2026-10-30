using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Profile;

public sealed record GetMyCondominiumQuery(Guid ManagerUserId) : IRequest<Result<MyCondominium>>;
