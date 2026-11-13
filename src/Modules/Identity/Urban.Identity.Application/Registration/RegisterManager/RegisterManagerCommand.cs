using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Registration.RegisterManager;

public sealed record RegisterManagerCommand(string Email, string Name, string CondominiumName) : IRequest<Result>;
