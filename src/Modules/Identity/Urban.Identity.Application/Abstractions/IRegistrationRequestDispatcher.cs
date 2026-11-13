namespace Urban.Identity.Application.Abstractions;

public interface IRegistrationRequestDispatcher
{
    Task EnqueueAsync(Guid userId, CancellationToken cancellationToken);
}
