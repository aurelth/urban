using Urban.SharedKernel;

namespace Urban.Condominium.Domain.Units;

public static class UnitErrors
{
    public static readonly Error NumberInvalid = new("unit.number.invalid", "Informe o número da unidade, com até 20 caracteres");

    public static readonly Error BlockInvalid = new("unit.block.invalid", "O bloco pode ter até 30 caracteres");

    public static readonly Error TypeInvalid = new("unit.type.invalid", "Escolha se a unidade é apartamento ou casa");

    public static readonly Error IdealFractionInvalid = new("unit.ideal_fraction.invalid", "A fração ideal deve ser maior que 0% e no máximo 100%, com até 8 casas decimais");

    public static readonly Error AreaInvalid = new("unit.area.invalid", "A área deve ser maior que 0 e no máximo 99.999,99 m², com até 2 casas decimais");

    public static readonly Error CondominiumRequired = new("unit.condominium_required", "A unidade precisa pertencer a um condomínio");

    public static readonly Error VersionRequired = new("unit.version.required", "Informe a versão dos dados que você está editando");

    public static readonly Error PageInvalid = new("unit.page.invalid", "A página deve ser 1 ou maior e o tamanho, de 1 a 200");

    public static readonly Error BatchSizeInvalid = new("unit.batch.size_invalid", "O cadastro em lote aceita de 1 a 500 unidades");

    public static readonly Error NotFound = new("unit.not_found", "Unidade não encontrada");

    public static readonly Error IdentifierAlreadyExists = new("unit.identifier.already_exists", "Já existe uma unidade com este bloco e número, ativa ou desativada");

    public static readonly Error Deactivated = new("unit.deactivated", "A unidade está desativada. Reative-a para editar");

    public static readonly Error AlreadyDeactivated = new("unit.already_deactivated", "A unidade já está desativada");

    public static readonly Error AlreadyActive = new("unit.already_active", "A unidade já está ativa");

    public static readonly Error FractionSumExceeded = new("unit.ideal_fraction.sum_exceeded", "A soma das frações ideais das unidades ativas não pode passar de 100%");
}
