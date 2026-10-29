using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.Condominium.Application.Registration.RegisterCondominium;

/// <summary>
/// Cadastro do condomínio pelo síndico. Primeiro valida tudo em memória (nada vai ao banco com dado inválido); depois, numa
/// transação única entre os módulos, confere os conflitos, grava condomínio e síndico e vincula a conta do síndico (Identity).
/// Se qualquer passo falhar, nada fica gravado.
/// </summary>
public sealed class RegisterCondominiumCommandHandler(
    ICondominiumRegistrations registrations,
    IManagerEnrollment enrollment,
    IAtomicWork atomicWork,
    IClock clock) : IRequestHandler<RegisterCondominiumCommand, Result<RegisteredCondominium>>
{
    // O Brasil não tem horário de verão desde 2019, então a data local é o horário UTC menos três horas.
    private static readonly TimeSpan BrazilOffset = TimeSpan.FromHours(-3);

    public async Task<Result<RegisteredCondominium>> Handle(RegisterCondominiumCommand request, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.UtcNow;
        Result<Registration> built = Build(request, now);
        if (built.IsFailure)
        {
            return Result.Failure<RegisteredCondominium>(built.Error);
        }

        Registration registration = built.Value;
        Result outcome = await atomicWork.ExecuteAsync(
            ct => RegisterAsync(registration, request, ct),
            cancellationToken);

        return outcome.IsSuccess
            ? Result.Success(registration.ToResponse())
            : Result.Failure<RegisteredCondominium>(outcome.Error);
    }

    private static Result<Registration> Build(RegisterCondominiumCommand request, DateTimeOffset now)
    {
        DateOnly today = DateOnly.FromDateTime(now.ToOffset(BrazilOffset).DateTime);

        Result<PersonName> fullName = PersonName.Create(request.Manager.FullName);
        Result<Cpf> cpf = Cpf.Create(request.Manager.Cpf);
        Result<BirthDate> birthDate = BirthDate.Create(request.Manager.BirthDate, today);
        Result<PhoneNumber> phone = PhoneNumber.Create(request.Manager.Phone);
        Result<Address> managerAddress = CreateAddress(request.Manager.Address);
        Result<Cnpj> cnpj = Cnpj.Create(request.Condominium.Cnpj);
        Result<Address> condominiumAddress = CreateAddress(request.Condominium.Address);

        foreach (Result part in new Result[] { fullName, cpf, birthDate, phone, managerAddress, cnpj, condominiumAddress })
        {
            if (part.IsFailure)
            {
                return Result.Failure<Registration>(part.Error);
            }
        }

        Result<CondominiumEntity> condominium = CondominiumEntity.Register(
            request.Condominium.Name, cnpj.Value, condominiumAddress.Value, request.ManagerUserId, now);
        if (condominium.IsFailure)
        {
            return Result.Failure<Registration>(condominium.Error);
        }

        Result<Person> manager = Person.RegisterManager(
            condominium.Value.Id, request.ManagerUserId, fullName.Value, cpf.Value, birthDate.Value, phone.Value, managerAddress.Value, now);

        return manager.IsFailure
            ? Result.Failure<Registration>(manager.Error)
            : Result.Success(new Registration(condominium.Value, manager.Value));
    }

    private static Result<Address> CreateAddress(AddressInput input) =>
        Address.Create(input.Street, input.Number, input.Complement, input.Neighborhood, input.City, input.State, input.PostalCode);

    private async Task<Result> RegisterAsync(Registration registration, RegisterCondominiumCommand request, CancellationToken cancellationToken)
    {
        if (await registrations.CnpjExistsAsync(registration.Condominium.Cnpj, cancellationToken))
        {
            return Result.Failure(CondominiumErrors.CnpjAlreadyRegistered);
        }

        if (await registrations.ManagerHasCondominiumAsync(request.ManagerUserId, cancellationToken))
        {
            return Result.Failure(CondominiumErrors.ManagerAlreadyRegistered);
        }

        Result saved = await registrations.SaveAsync(registration.Condominium, registration.Manager, cancellationToken);
        if (saved.IsFailure)
        {
            return saved;
        }

        return await enrollment.EnrollAsync(request.ManagerUserId, registration.Condominium.Id, request.DisplayName, cancellationToken);
    }

    private sealed record Registration(CondominiumEntity Condominium, Person Manager)
    {
        public RegisteredCondominium ToResponse() =>
            new(Condominium.Id, Condominium.Name, Condominium.Cnpj.Formatted, Manager.Cpf.Masked);
    }
}
