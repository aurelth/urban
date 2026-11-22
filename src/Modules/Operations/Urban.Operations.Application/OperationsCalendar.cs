namespace Urban.Operations.Application;

/// <summary>"Hoje" do condomínio. O Brasil não tem horário de verão desde 2019, então o fuso (UTC-3) é fixo e não depende do sistema.</summary>
internal static class OperationsCalendar
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(-3);

    public static DateOnly Today(DateTimeOffset utcNow) => DateOnly.FromDateTime(utcNow.ToOffset(Offset).DateTime);
}
