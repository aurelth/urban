namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta que pede o envio do link de redefinição em segundo plano. Só o id da conta viaja: o segredo do link nasce depois, dentro do envio.</summary>
public interface IPasswordResetDispatcher
{
    Task EnqueueAsync(Guid userId, CancellationToken cancellationToken);
}