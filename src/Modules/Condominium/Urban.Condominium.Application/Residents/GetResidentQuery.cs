using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Residents;

public sealed record GetResidentQuery(Guid ResidentId) : IRequest<Result<ResidentView>>;
