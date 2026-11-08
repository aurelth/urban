using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Api.Mapping;
using Urban.Finance.Application.Consumption;
using Urban.Finance.Application.Expenses;
using Urban.Finance.Application.Groups;
using Urban.Finance.Application.Months;
using Urban.Finance.Application.Settings;
using Urban.Finance.Domain.Closing;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

public static class FinanceEndpoints
{
    private const string RateLimitText = "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";

    private const string VersionText = "O corpo leva a versão lida na listagem (400 sem ela, 409 se outra pessoa mudou antes).";

    public static IEndpointRouteBuilder MapFinanceEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder finance = app.MapGroup(FinanceRoutes.Prefix).WithTags("Financeiro").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Finance);

        MapGroups(finance);
        MapExpenseGroupReserveFund(finance);
        MapExpenses(finance);
        MapMonths(finance);
        MapSettings(finance);
        MapTariffs(finance);

        return app;
    }

    private static void MapGroups(RouteGroupBuilder finance)
    {
        finance.MapGet("/expense-groups", (ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SendAsync(user, response, sender, new ListExpenseGroupsQuery(), views => FinanceMapper.ToResponse(views), ct))
            .WithName("ListExpenseGroups")
            .WithSummary("Lista os grupos de despesas do condomínio")
            .WithDescription("Exige o token de acesso de um síndico com condomínio cadastrado. Devolve os grupos em ordem alfabética, cada um com a versão que a troca de nome precisa devolver. A resposta nunca é guardada em cache. " + RateLimitText)
            .Produces<IReadOnlyList<ExpenseGroupResponse>>()
            .WithErrors(StatusCodes.Status404NotFound);

        finance.MapPost("/expense-groups", (ExpenseGroupRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                CreateAsync(user, response, sender, new CreateExpenseGroupCommand(request.Name), view => $"{FinanceRoutes.Groups}/{view.Id}", view => FinanceMapper.ToResponse(view), ct))
            .WithName("CreateExpenseGroup")
            .WithSummary("Cria um grupo de despesas")
            .WithDescription("O nome tem até 60 caracteres e não se repete no condomínio, sem diferenciar maiúsculas (409). Nome vazio ou comprido demais responde 400. Devolve 201 com o grupo. " + RateLimitText)
            .Produces<ExpenseGroupResponse>(StatusCodes.Status201Created)
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        finance.MapPost("/expense-groups/default", (ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SendAsync(user, response, sender, new EnsureDefaultExpenseGroupCommand(), view => FinanceMapper.ToResponse(view), ct))
            .WithName("EnsureDefaultExpenseGroup")
            .WithSummary("Garante o grupo \"Despesas ordinárias\"")
            .WithDescription("Cria o grupo padrão se o condomínio ainda não tiver um e devolve o grupo. Pode ser chamado quantas vezes quiser: sempre responde 200 com o mesmo grupo. " + RateLimitText)
            .Produces<ExpenseGroupResponse>()
            .WithErrors(StatusCodes.Status404NotFound);

        finance.MapPut("/expense-groups/{id:guid}", (Guid id, ExpenseGroupRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SendAsync(user, response, sender, new RenameExpenseGroupCommand(id, request.Version, request.Name), view => FinanceMapper.ToResponse(view), ct))
            .WithName("RenameExpenseGroup")
            .WithSummary("Troca o nome de um grupo de despesas")
            .WithDescription(VersionText + " As regras do nome são as da criação (400 e 409). " + RateLimitText)
            .Produces<ExpenseGroupResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);
    }

    private static void MapExpenseGroupReserveFund(RouteGroupBuilder finance)
    {
        finance.MapPut("/expense-groups/{id:guid}/reserve-fund-base", (Guid id, ReserveFundBaseRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SetReserveFundBaseAsync(id, request, user, response, sender, ct))
            .WithName("SetExpenseGroupReserveFundBase")
            .WithSummary("Marca ou desmarca o grupo como base do fundo de reserva")
            .WithDescription(VersionText + " O corpo leva enabled (true ou false; sem ele, 400). O fundo de reserva é o percentual das configurações aplicado à soma das despesas dos grupos marcados, no fechamento do mês. " + RateLimitText)
            .Produces<ExpenseGroupResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);
    }

    private static void MapSettings(RouteGroupBuilder finance)
    {
        finance.MapGet("/settings", (ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SendAsync(user, response, sender, new GetFinanceSettingsQuery(), view => FinanceMapper.ToResponse(view), ct))
            .WithName("GetFinanceSettings")
            .WithSummary("Lê as configurações financeiras do condomínio")
            .WithDescription("Devolve o percentual do fundo de reserva (0 enquanto o condomínio não configurou nada: sem fundo de reserva no recibo). A resposta nunca é guardada em cache. " + RateLimitText)
            .Produces<FinanceSettingsResponse>()
            .WithErrors(StatusCodes.Status404NotFound);

        finance.MapPut("/settings", (FinanceSettingsRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SetReserveFundPercentAsync(request, user, response, sender, ct))
            .WithName("SetFinanceSettings")
            .WithSummary("Muda o percentual do fundo de reserva")
            .WithDescription("O percentual vai de 0 a 100, com até 2 casas (400 fora disso ou sem ele). O fechamento de um mês usa o percentual do momento em que fecha; meses já fechados não mudam. " + RateLimitText)
            .Produces<FinanceSettingsResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);
    }

    private static void MapTariffs(RouteGroupBuilder finance)
    {
        finance.MapGet("/consumption-tariffs", (ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SendAsync(user, response, sender, new ListConsumptionTariffsQuery(), views => FinanceMapper.ToResponse(views), ct))
            .WithName("ListConsumptionTariffs")
            .WithSummary("Lista as tarifas de água, esgoto e gás do condomínio")
            .WithDescription("Da tarifa que começou mais recentemente para a mais antiga. Cada uma vale a partir do mês de início, até a próxima começar. A resposta nunca é guardada em cache. " + RateLimitText)
            .Produces<IReadOnlyList<ConsumptionTariffResponse>>()
            .WithErrors(StatusCodes.Status404NotFound);

        finance.MapPut("/consumption-tariffs/{month}", (string month, ConsumptionTariffRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SendAsync(user, response, sender, FinanceMapper.ToCommand(request, month), view => FinanceMapper.ToResponse(view), ct))
            .WithName("SaveConsumptionTariff")
            .WithSummary("Guarda a tarifa que vale a partir de um mês")
            .WithDescription(
                "O mês vai como AAAA-MM (400 se não for). A água leva de 1 a 6 faixas (water): cada faixa tem o limite de cima em m³ (upToCubicMeters, nulo na última), o valor fixo (fixedAmount) e o preço por m³ (pricePerCubicMeter); só os m³ que caem na faixa pagam o preço dela. "
                + "O esgoto (sewage) segue o mesmo formato, é opcional, e sewagePercent diz quanto das faixas se cobra (100 quando omitido; 50 na tarifa parcial). O gás (gas) leva os kg por m³ e o preço do kg, e o preço do m³ é o produto dos dois em centavos. "
                + "Repetir o mês substitui a tarifa dele; um reajuste é uma tarifa nova com outro mês. Faixas, gás ou percentual inválidos respondem 400. " + RateLimitText)
            .Produces<ConsumptionTariffResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        finance.MapDelete("/consumption-tariffs/{month}", (string month, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
                DeleteTariffAsync(month, user, sender, ct))
            .WithName("DeleteConsumptionTariff")
            .WithSummary("Apaga a tarifa que começa em um mês")
            .WithDescription("Responde 204. Mês sem tarifa responde 404. Os meses já fechados não mudam: o fechamento guarda o que cobrou. " + RateLimitText)
            .Produces(StatusCodes.Status204NoContent)
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);
    }

    private static void MapExpenses(RouteGroupBuilder finance)
    {
        finance.MapGet("/expenses", (string? status, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                ListExpensesAsync(status, user, response, sender, ct))
            .WithName("ListExpenses")
            .WithSummary("Lista as despesas do condomínio")
            .WithDescription("Filtro status: active (o padrão, as que ainda são cobradas), ended (as que já acabaram) ou all; outro valor responde 400. Cada despesa traz o valor em vigor no mês atual, as parcelas e o histórico de reajustes. A resposta nunca é guardada em cache. " + RateLimitText)
            .Produces<IReadOnlyList<ExpenseResponse>>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        finance.MapPost("/expenses", (ExpenseRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                CreateAsync(user, response, sender, FinanceMapper.ToCommand(request), view => $"{FinanceRoutes.Expenses}/{view.Id}", view => FinanceMapper.ToResponse(view), ct))
            .WithName("CreateExpense")
            .WithSummary("Cadastra uma despesa")
            .WithDescription(
                "O tipo (kind) é single (única), fixed (fixa mensal), variable (variável mensal) ou installment (parcelada). Única e fixa levam o valor (amount); a fixa pode ter mês final (endMonth); "
                + "a parcelada leva o total, o número de parcelas (2 a 120) e a parcela que cai no mês inicial (currentInstallment); a variável não tem valor. Os centavos que sobram da divisão vão para a última parcela. "
                + "O dia do vencimento (dueDay, de 1 a 31) é obrigatório em todos os tipos. Se o mês inicial já pode ser aberto, o lançamento dele é criado junto. Dado inválido responde 400 com o código do campo; grupo que não existe, 404. Devolve 201 com a despesa. " + RateLimitText)
            .Produces<ExpenseResponse>(StatusCodes.Status201Created)
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);

        finance.MapPut("/expenses/{id:guid}", (Guid id, ExpenseUpdateRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SendAsync(user, response, sender, FinanceMapper.ToCommand(request, id), view => FinanceMapper.ToResponse(view), ct))
            .WithName("UpdateExpense")
            .WithSummary("Edita a descrição, o fornecedor, o grupo e o dia do vencimento de uma despesa")
            .WithDescription(VersionText + " Valores e meses não mudam aqui: use as rotas de reajuste, parcela e encerramento. Os lançamentos já gerados mantêm o que valia quando nasceram (inclusive o vencimento); a mudança vale para os próximos. " + RateLimitText)
            .Produces<ExpenseResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        finance.MapPost("/expenses/{id:guid}/amount", (Guid id, ExpenseAmountRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SendAsync(user, response, sender, FinanceMapper.ToCommand(request, id), view => FinanceMapper.ToResponse(view), ct))
            .WithName("ChangeExpenseAmount")
            .WithSummary("Reajusta o valor de uma despesa fixa a partir de um mês")
            .WithDescription(VersionText + " Os meses anteriores continuam como estavam; o lançamento que nunca foi editado passa a seguir o novo valor. Só despesas fixas (422); mês antes do início da despesa, 400. " + RateLimitText)
            .Produces<ExpenseResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);

        finance.MapPut("/expenses/{id:guid}/installments/{number:int}", (Guid id, int number, InstallmentAmountRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SendAsync(user, response, sender, FinanceMapper.ToCommand(request, id, number), view => FinanceMapper.ToResponse(view), ct))
            .WithName("SetInstallmentAmount")
            .WithSummary("Muda o valor de uma parcela")
            .WithDescription(VersionText + " Parcela que não existe responde 404; despesa que não é parcelada, 422. " + RateLimitText)
            .Produces<ExpenseResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);

        finance.MapPost("/expenses/{id:guid}/end", (Guid id, EndExpenseRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SendAsync(user, response, sender, FinanceMapper.ToCommand(request, id), view => FinanceMapper.ToResponse(view), ct))
            .WithName("EndExpense")
            .WithSummary("Encerra uma despesa fixa ou variável")
            .WithDescription(VersionText + " O último mês não pode ser anterior ao início (400). Despesas únicas e parceladas terminam sozinhas (422). " + RateLimitText)
            .Produces<ExpenseResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);
    }

    private static void MapMonths(RouteGroupBuilder finance)
    {
        finance.MapGet("/months/{month}", (string month, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SendAsync(user, response, sender, new GetMonthQuery(month), view => FinanceMapper.ToResponse(view), ct))
            .WithName("GetMonth")
            .WithSummary("Lê os lançamentos de um mês")
            .WithDescription("O mês vai como AAAA-MM (400 se não for). Devolve os lançamentos como estão, com o total, os totais por grupo (sem os cancelados), a quantidade que espera valor e a de vencidos, sem gerar nada: um mês nunca aberto volta vazio. A resposta nunca é guardada em cache. " + RateLimitText)
            .Produces<MonthResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        finance.MapPost("/months/{month}/open", (string month, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SendAsync(user, response, sender, new OpenMonthCommand(month), view => FinanceMapper.ToResponse(view), ct))
            .WithName("OpenMonth")
            .WithSummary("Abre um mês, gerando os lançamentos que faltam")
            .WithDescription("Gera o lançamento de toda despesa que vence no mês e ainda não tem um; o que já existe, inclusive o que foi editado à mão, não é tocado. Pode ser repetido à vontade. Abre qualquer mês até 5 anos à frente (422 além disso). Cada lançamento nasce pendente, com o vencimento do mês; a despesa variável espera o valor, sugerindo o do mês anterior. " + RateLimitText)
            .Produces<MonthResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);

        finance.MapPost("/months/{month}/close", (string month, MonthCloseRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                CloseMonthAsync(month, request, user, response, sender, ct))
            .WithName("CloseMonth")
            .WithSummary("Fecha o mês e reparte o total entre as unidades")
            .WithDescription(
                "Soma os lançamentos não cancelados e reparte o total entre as unidades ativas pelo método de rateio do condomínio (fração ideal, valor igual ou área), em centavos que fecham exatamente o total. "
                + "O corpo leva o vencimento (dueDate, AAAA-MM-DD, não antes do primeiro dia do mês) de todas as cobranças. Com o mês fechado, valores e cancelamentos não mudam (409); marcar como pago continua valendo. "
                + "Mês nunca aberto, valor variável ainda por informar, nada a cobrar e dados das unidades que não bastam para o método escolhido respondem 422; mês já fechado, 409. " + RateLimitText)
            .Produces<MonthResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);

        finance.MapPost("/months/{month}/reopen", (string month, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SendAsync(user, response, sender, new ReopenMonthCommand(month), view => FinanceMapper.ToResponse(view), ct))
            .WithName("ReopenMonth")
            .WithSummary("Reabre um mês fechado")
            .WithDescription("Apaga o fechamento e as cobranças do mês; os lançamentos voltam a poder ser editados e o mês pode ser fechado de novo, refazendo as cobranças. Mês que não está fechado responde 409. " + RateLimitText)
            .Produces<MonthResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        finance.MapPut("/entries/{id:guid}/amount", (Guid id, EntryAmountRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SendAsync(user, response, sender, FinanceMapper.ToCommand(request, id), view => FinanceMapper.ToResponse(view), ct))
            .WithName("SetEntryAmount")
            .WithSummary("Informa ou corrige o valor de um lançamento")
            .WithDescription(VersionText + " Vale só para aquele mês: a despesa e os outros meses não mudam. " + RateLimitText)
            .Produces<ExpenseEntryResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        finance.MapPut("/entries/{id:guid}/status", (Guid id, EntryStatusRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken ct) =>
                SendAsync(user, response, sender, FinanceMapper.ToCommand(request, id), view => FinanceMapper.ToResponse(view), ct))
            .WithName("SetEntryStatus")
            .WithSummary("Marca um lançamento como pendente, pago ou cancelado")
            .WithDescription(VersionText + " O status é pending, paid ou canceled; outro texto, inclusive overdue, responde 400, porque vencido é calculado pela data (pendente depois do vencimento). Pago exige o valor informado (422). Cancelado sai dos totais do mês. " + RateLimitText)
            .Produces<ExpenseEntryResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> SetReserveFundBaseAsync(Guid id, ReserveFundBaseRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        if (request.Enabled is null)
        {
            return ErrorHttp.ToProblem(RequestErrors.Invalid);
        }

        return ApiResults.Respond(await sender.Send(FinanceMapper.ToCommand(request, id), cancellationToken), response, view => FinanceMapper.ToResponse(view));
    }

    private static async Task<IResult> SetReserveFundPercentAsync(FinanceSettingsRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        if (request.ReserveFundPercent is null)
        {
            return ErrorHttp.ToProblem(RequestErrors.Invalid);
        }

        return ApiResults.Respond(await sender.Send(FinanceMapper.ToCommand(request), cancellationToken), response, view => FinanceMapper.ToResponse(view));
    }

    private static async Task<IResult> DeleteTariffAsync(string month, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        Result result = await sender.Send(new DeleteConsumptionTariffCommand(month), cancellationToken);

        return result.IsSuccess ? Results.NoContent() : ErrorHttp.ToProblem(result.Error);
    }

    private static async Task<IResult> ListExpensesAsync(string? status, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        if (!FinanceMapper.TryParseStatus(status, out ExpenseStatusFilter filter))
        {
            return ErrorHttp.ToProblem(RequestErrors.Invalid);
        }

        Result<IReadOnlyList<ExpenseView>> result = await sender.Send(new ListExpensesQuery(filter), cancellationToken);

        return ApiResults.Respond(result, response, views => FinanceMapper.ToResponse(views));
    }

    private static async Task<IResult> CloseMonthAsync(
        string month,
        MonthCloseRequest request,
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        if (!FinanceMapper.TryParseDueDate(request.DueDate, out DateOnly dueDate))
        {
            return ErrorHttp.ToProblem(ClosingErrors.DueDateInvalid);
        }

        Result<MonthView> result = await sender.Send(new CloseMonthCommand(month, dueDate, userId), cancellationToken);

        return ApiResults.Respond(result, response, view => FinanceMapper.ToResponse(view));
    }

    private static async Task<IResult> SendAsync<T, TResponse>(
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        IRequest<Result<T>> request,
        Func<T, TResponse> toResponse,
        CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        return ApiResults.Respond(await sender.Send(request, cancellationToken), response, toResponse);
    }

    private static async Task<IResult> CreateAsync<T, TResponse>(
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        IRequest<Result<T>> request,
        Func<T, string> location,
        Func<T, TResponse> toResponse,
        CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        return ApiResults.Created(await sender.Send(request, cancellationToken), response, location, toResponse);
    }
}
