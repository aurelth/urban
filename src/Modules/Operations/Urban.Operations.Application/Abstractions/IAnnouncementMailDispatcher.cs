namespace Urban.Operations.Application.Abstractions;

/// <summary>
/// Porta que pede, em segundo plano, o e-mail de um comunicado. O pedido leva só ids: sem requisição não há token, então o condomínio
/// vai junto para a tarefa escolhê-lo antes de qualquer consulta.
/// </summary>
public interface IAnnouncementMailDispatcher
{
    /// <summary>Pede que a tarefa descubra quem deve receber o comunicado e abra um envio para cada conta.</summary>
    Task EnqueueFanOutAsync(Guid condominiumId, Guid announcementId, CancellationToken cancellationToken);

    /// <summary>Pede o envio do comunicado a uma conta só; cada conta tem a sua tentativa, para uma falha não repetir o e-mail dos outros.</summary>
    Task EnqueueDeliveryAsync(Guid condominiumId, Guid announcementId, Guid userId, CancellationToken cancellationToken);
}
