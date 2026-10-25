using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Bootstrap;

public sealed record BootstrapSuperAdminCommand(string Email) : IRequest<Result>;