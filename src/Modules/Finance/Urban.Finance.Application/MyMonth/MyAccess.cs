using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.MyMonth;

/// <summary>A regra de como a conta mora, num lugar só: o mês do morador e o menu perguntam a mesma coisa e não podem discordar.</summary>
internal static class MyAccess
{
    public static MyMonthAccess Of(ResidentDwelling dwelling) =>
        dwelling.Units.Count > 0 ? MyMonthAccess.Unit : dwelling.LivesAsDependent ? MyMonthAccess.Dependent : MyMonthAccess.None;
}
