using MediatR;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Payments;

/// <summary>Registra um recebimento contra a cobrança de uma unidade. Pode haver vários por cobrança, até o valor dela.</summary>
public sealed record RegisterPaymentCommand(Guid ChargeId, decimal Amount, DateOnly ReceivedOn, PaymentMethod Method, string? Note, Guid ActorUserId)
    : IRequest<Result<PaymentView>>;

/// <summary>Estorna um pagamento: ele continua no histórico, marcado, e deixa de contar. O motivo é obrigatório.</summary>
public sealed record ReversePaymentCommand(Guid PaymentId, string? Reason, Guid ActorUserId) : IRequest<Result>;

/// <summary>As cobranças de um mês fechado com o que foi pago de cada uma. O mês vai como texto "AAAA-MM".</summary>
public sealed record ListMonthPaymentsQuery(string? Month, ChargeStatus? Status = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<MonthPaymentsView>>;

/// <summary>Os pagamentos de uma cobrança, os estornados também.</summary>
public sealed record ListChargePaymentsQuery(Guid ChargeId) : IRequest<Result<ChargePaymentsView>>;
