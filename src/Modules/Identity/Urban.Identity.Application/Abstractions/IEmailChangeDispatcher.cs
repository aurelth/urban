using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta que pede o envio do link de troca de e-mail em segundo plano. Só o id da conta e o endereço novo viajam: o segredo do link nasce depois, dentro do envio.</summary>
public interface IEmailChangeDispatcher
{
    Task EnqueueAsync(Guid userId, Email newEmail, CancellationToken cancellationToken);
}
