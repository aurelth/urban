using Urban.SharedKernel;

namespace Urban.Operations.Domain.Announcements;

/// <summary>
/// A quem o comunicado se dirige: o condomínio inteiro, um bloco ou uma unidade. Os três jeitos nascem pelas fábricas, então nunca
/// existe um bloco junto de uma unidade nem um alcance sem o que o define.
/// </summary>
public sealed record AnnouncementAudience
{
    public const int BlockMaxLength = 30;

    private AnnouncementAudience(AnnouncementScope scope, string? block, Guid? unitId)
    {
        Scope = scope;
        Block = block;
        UnitId = unitId;
    }

    public AnnouncementScope Scope { get; }

    /// <summary>O bloco, só no alcance <see cref="AnnouncementScope.Block"/>.</summary>
    public string? Block { get; }

    /// <summary>A unidade, só no alcance <see cref="AnnouncementScope.Unit"/>.</summary>
    public Guid? UnitId { get; }

    public static AnnouncementAudience WholeCondominium { get; } = new(AnnouncementScope.Condominium, null, null);

    public static Result<AnnouncementAudience> OfBlock(string? block) => throw new NotImplementedException();

    public static Result<AnnouncementAudience> OfUnit(Guid? unitId) => throw new NotImplementedException();

    /// <summary>Monta o alcance pelo que a requisição trouxe. Bloco e unidade que sobram para o alcance escolhido são ignorados.</summary>
    public static Result<AnnouncementAudience> Parse(string? scope, string? block, Guid? unitId) => throw new NotImplementedException();

    /// <summary>Monta o alcance a partir dos campos já gravados no comunicado, sem repetir a conferência.</summary>
    internal static AnnouncementAudience From(AnnouncementScope scope, string? block, Guid? unitId) => new(scope, block, unitId);

    /// <summary>O comunicado vale para quem mora em algum destes blocos ou unidades.</summary>
    public bool Reaches(IReadOnlyCollection<string> blocks, IReadOnlyCollection<Guid> unitIds) => throw new NotImplementedException();
}
