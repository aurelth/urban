using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Abstractions;

/// <summary>Porta de persistência dos dados para pagamento (uma linha por condomínio), sempre dentro do tenant da requisição.</summary>
public interface IPaymentInstructionsStore
{
    /// <returns>Os dados já gravados, ou nulo se o condomínio ainda não cadastrou nada.</returns>
    Task<PaymentInstructions?> FindAsync(CancellationToken cancellationToken);

    /// <summary>Grava os dados (cria a linha na primeira vez). Falha com conflito se outra requisição criou a linha primeiro.</summary>
    Task<Result> SaveAsync(PaymentInstructions instructions, CancellationToken cancellationToken);
}
