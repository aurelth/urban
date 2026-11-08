using Urban.SharedKernel;

namespace Urban.Finance.Domain.Consumption;

public static class ConsumptionErrors
{
    public static readonly Error BandsInvalid = new("consumption.bands_invalid", "As faixas precisam ser de 1 a 6, com limites positivos e crescentes (em m³), a última sem limite, valor fixo com até 2 casas e preço por m³ com até 4 casas");

    public static readonly Error GasInvalid = new("consumption.gas_invalid", "Informe os kg por m³ (até 100, com 3 casas) e o preço do kg (até R$ 999,99, com 2 casas), ambos maiores que zero");

    public static readonly Error SewagePercentInvalid = new("consumption.sewage_percent_invalid", "O percentual do esgoto vai de 0 a 100, com até 2 casas decimais");

    public static readonly Error CondominiumRequired = new("consumption.condominium_required", "O condomínio é obrigatório");

    public static readonly Error ReadingInvalid = new("consumption.reading_invalid", "A leitura atual não pode ser menor que a anterior, e as duas vão de 0 a 9.999.999,999 m³ com até 3 casas decimais");

    public static readonly Error UnitRequired = new("consumption.unit_required", "A unidade da leitura é obrigatória");

    public static readonly Error ReserveFundPercentInvalid = new("consumption.reserve_percent_invalid", "O percentual do fundo de reserva vai de 0 a 100, com até 2 casas decimais");
}
