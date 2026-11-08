using Urban.Finance.Domain.Settings;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Abstractions;

/// <summary>Porta de persistência das escolhas do condomínio (uma linha por condomínio), sempre dentro do tenant da requisição.</summary>
public interface IFinanceSettings
{
    /// <returns>As escolhas já gravadas, ou nulo se o condomínio ainda não configurou nada.</returns>
    Task<FinanceSettings?> FindAsync(CancellationToken cancellationToken);

    /// <summary>Grava as escolhas (cria a linha na primeira vez). Falha com conflito se outra requisição criou a linha primeiro.</summary>
    Task<Result> SaveAsync(FinanceSettings settings, CancellationToken cancellationToken);
}
