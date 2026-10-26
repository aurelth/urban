using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Registration.ConfirmEmail;

public sealed record ConfirmEmailCommand(string Token, string Password, string TermsVersion) : IRequest<Result>;