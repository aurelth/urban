using Urban.SharedKernel;

namespace Urban.Condominium.Domain.Vehicles;

public static class VehicleErrors
{
    public static readonly Error PlateInvalid = new("vehicle.plate.invalid", "Informe a placa no formato ABC-1234 ou ABC1D23");

    public static readonly Error ModelInvalid = new("vehicle.model.invalid", "Informe o modelo do veículo, com 2 a 60 caracteres");

    public static readonly Error ColorInvalid = new("vehicle.color.invalid", "Informe a cor do veículo, com 2 a 30 caracteres");

    public static readonly Error UnitRequired = new("vehicle.unit_required", "Escolha a unidade do veículo");

    public static readonly Error CondominiumRequired = new("vehicle.condominium_required", "O veículo precisa pertencer a um condomínio");

    public static readonly Error VersionRequired = new("vehicle.version.required", "Informe a versão dos dados que você está editando");

    public static readonly Error PageInvalid = new("vehicle.page.invalid", "A página deve ser 1 ou maior e o tamanho, de 1 a 200");

    public static readonly Error NotFound = new("vehicle.not_found", "Veículo não encontrado");

    public static readonly Error PlateAlreadyRegistered = new("vehicle.plate.already_registered", "Já existe um veículo com esta placa neste condomínio");
}
