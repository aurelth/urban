using MediatR;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Delinquency;

/// <summary>As unidades com cobrança em atraso, de todos os meses fechados, da que mais deve para a que menos deve.</summary>
public sealed record ListDelinquencyQuery(int Page = 1, int PageSize = 50) : IRequest<Result<DelinquencyView>>;
