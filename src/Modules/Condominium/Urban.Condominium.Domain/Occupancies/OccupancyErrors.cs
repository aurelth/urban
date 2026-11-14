using Urban.SharedKernel;

namespace Urban.Condominium.Domain.Occupancies;

public static class OccupancyErrors
{
    public static readonly Error KindInvalid = new("condominium.occupancy.kind_invalid", "Escolha se a pessoa é proprietária ou inquilina");

    public static readonly Error StartDateInvalid = new("condominium.occupancy.start_date_invalid", "Informe uma data de entrada válida, até 1 ano à frente");

    public static readonly Error CondominiumRequired = new("condominium.occupancy.condominium_required", "O vínculo precisa pertencer a um condomínio");

    public static readonly Error PersonRequired = new("condominium.occupancy.person_required", "O vínculo precisa apontar para uma pessoa");

    public static readonly Error UnitRequired = new("condominium.occupancy.unit_required", "O vínculo precisa apontar para uma unidade");

    public static readonly Error EndDateInvalid = new("condominium.occupancy.end_date_invalid", "Informe uma data de saída entre a de entrada e hoje");

    public static readonly Error NotFound = new("condominium.occupancy.not_found", "Vínculo não encontrado");

    public static readonly Error AlreadyActive = new("condominium.occupancy.already_active", "A pessoa já mora nesta unidade");

    public static readonly Error AlreadyEnded = new("condominium.occupancy.already_ended", "O vínculo já foi encerrado");
}
