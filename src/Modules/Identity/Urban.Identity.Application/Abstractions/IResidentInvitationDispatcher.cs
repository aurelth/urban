namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta que pede o envio do convite do morador em segundo plano. Só o id da conta viaja: o segredo do link nasce depois, dentro do envio.</summary>
public interface IResidentInvitationDispatcher
{
    Task EnqueueAsync(Guid userId, CancellationToken cancellationToken);
}
