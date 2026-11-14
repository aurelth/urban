namespace Urban.Condominium.Application;

/// <summary>A data de hoje no Brasil, que é a que importa para idade e datas de entrada: o dia da pessoa, não o do servidor em UTC.</summary>
internal static class BrazilCalendar
{
    // O Brasil não tem horário de verão desde 2019, então a data local é o horário UTC menos três horas.
    private static readonly TimeSpan Offset = TimeSpan.FromHours(-3);

    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.ToOffset(Offset).DateTime);
}
