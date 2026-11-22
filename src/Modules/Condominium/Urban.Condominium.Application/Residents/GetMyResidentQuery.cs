using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Residents;

public sealed record GetMyResidentQuery(Guid UserId) : IRequest<Result<ResidentView>>;
