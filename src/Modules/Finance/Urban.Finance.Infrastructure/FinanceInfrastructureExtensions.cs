using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Infrastructure.Invoices;
using Urban.Finance.Infrastructure.Persistence;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Time;

namespace Urban.Finance.Infrastructure;

public static class FinanceInfrastructureExtensions
{
    private const string PostgresConnectionName = "Postgres";

    /// <remarks>
    /// Quem hospeda o módulo precisa registrar o <see cref="ITenantContext"/> da sua origem de requisições (a API lê do token) e o
    /// <see cref="ITenantScope"/>, com que as tarefas em segundo plano escolhem o condomínio, e o <c>IBackgroundJobClient</c> do Hangfire.
    /// </remarks>
    public static IServiceCollection AddFinanceInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string postgres = configuration.GetConnectionString(PostgresConnectionName) is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Falta a cadeia de conexão '{PostgresConnectionName}' (ConnectionStrings:{PostgresConnectionName}).");

        services.TryAddSingleton<IClock, SystemClock>();
        services.AddDbContext<FinanceDbContext>(options => FinancePersistence.Configure(options, postgres));
        services.AddScoped<IExpenseGroups, FinanceExpenseGroups>();
        services.AddScoped<IExpenses, FinanceExpenses>();
        services.AddScoped<IExpenseEntries, FinanceExpenseEntries>();
        services.AddScoped<IMonthClosings, FinanceMonthClosings>();
        services.AddScoped<IFinanceSettings, FinanceSettingsStore>();
        services.AddScoped<IPaymentInstructionsStore, PaymentInstructionsStore>();
        services.AddScoped<IConsumptionTariffs, FinanceConsumptionTariffs>();
        services.AddScoped<IMeterReadings, FinanceMeterReadings>();
        services.AddScoped<IPayments, FinancePayments>();
        services.AddScoped<IOverdueCharges, FinanceOverdueCharges>();
        services.AddScoped<ICharges, FinanceCharges>();
        services.AddSingleton<IInvoiceRenderer, QuestPdfInvoiceRenderer>();
        services.AddScoped<IInvoiceDeliveries, FinanceInvoiceDeliveries>();
        services.AddScoped<IInvoiceDeliveryDispatcher, HangfireInvoiceDeliveryDispatcher>();

        return services;
    }

    public static async Task MigrateFinanceAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        FinanceDbContext db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
