namespace Urban.Finance.Domain.Consumption;

/// <summary>O que o hidrômetro ou o medidor de cada unidade mede. O esgoto não tem medidor: é calculado sobre a água.</summary>
public enum ConsumptionService
{
    Water,
    Gas,
}
