using MediatR;
using Urban.Identity.Application.Bootstrap;
using Urban.SharedKernel;

namespace Urban.Api.Extensions;

public static partial class SuperAdminBootstrapExtensions
{
    public const string EmailSetting = "SuperAdmin:Email";

    /// <summary>
    /// Garante a conta do SuperAdmin a partir de <c>SuperAdmin:Email</c>; sem essa configuração não faz nada. Um e-mail inválido
    /// impede a partida (erro de configuração). E-mail que já é de outro papel não derruba a API: registra o erro e não altera a conta.
    /// </summary>
    public static async Task BootstrapSuperAdminAsync(this IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        string? address = configuration[EmailSetting];
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        Result result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new BootstrapSuperAdminCommand(address), cancellationToken);

        if (result.IsSuccess)
        {
            return;
        }

        if (result.Error != BootstrapErrors.EmailTakenByAnotherRole)
        {
            throw new InvalidOperationException($"A configuração {EmailSetting} não é um e-mail válido.");
        }

        LogEmailTaken(scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SuperAdminBootstrapExtensions)));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "O e-mail configurado para o SuperAdmin já pertence a uma conta com outro papel. Nenhuma conta foi criada nem alterada.")]
    private static partial void LogEmailTaken(ILogger logger);
}