using MediatR;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Receipts;

/// <summary>
/// O síndico pede o envio por e-mail do recibo de um pagamento ao responsável da unidade. Só confere e põe na fila: o envio roda em
/// segundo plano e pode ser pedido de novo quantas vezes ele quiser (reenvio).
/// </summary>
public sealed record QueueReceiptEmailCommand(Guid PaymentId) : IRequest<Result<ReceiptRecipient>>;

/// <summary>Roda no segundo plano: monta o PDF do recibo e o envia ao responsável da unidade. O tenant já foi escolhido por quem chama.</summary>
public sealed record SendReceiptEmailCommand(Guid PaymentId) : IRequest<Result>;

/// <param name="Name">O nome do responsável da unidade, para a tela dizer a quem o recibo vai.</param>
public sealed record ReceiptRecipient(string Name);
