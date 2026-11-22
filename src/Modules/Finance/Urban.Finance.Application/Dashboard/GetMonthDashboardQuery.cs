using MediatR;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Dashboard;

/// <summary>O painel financeiro de um mês: as despesas por grupo e, se o mês já fechou, o que foi cobrado, recebido e está atrasado.</summary>
public sealed record GetMonthDashboardQuery(string Month) : IRequest<Result<MonthDashboardView>>;
