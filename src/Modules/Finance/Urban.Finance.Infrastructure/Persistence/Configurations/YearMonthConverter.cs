using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Urban.Finance.Domain.Months;

namespace Urban.Finance.Infrastructure.Persistence.Configurations;

/// <summary>A competência vai para o banco como texto "AAAA-MM" (7 caracteres), que ordena e compara como as datas.</summary>
internal sealed class YearMonthConverter()
    : ValueConverter<YearMonth, string>(month => month.ToString(), text => YearMonth.Parse(text).Value)
{
    public const int Length = 7;
}
