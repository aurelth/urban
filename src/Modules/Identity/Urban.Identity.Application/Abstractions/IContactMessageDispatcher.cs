namespace Urban.Identity.Application.Abstractions;

public interface IContactMessageDispatcher
{
    Task EnqueueAsync(Guid messageId, CancellationToken cancellationToken);
}
