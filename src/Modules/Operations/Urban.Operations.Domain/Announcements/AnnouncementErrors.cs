using Urban.SharedKernel;

namespace Urban.Operations.Domain.Announcements;

public static class AnnouncementErrors
{
    public static readonly Error CondominiumRequired = new("announcement.condominium_required", "O comunicado precisa pertencer a um condomínio");

    public static readonly Error AuthorRequired = new("announcement.author_required", "O comunicado precisa ter autor");

    public static readonly Error TitleInvalid = new("announcement.title.invalid", "Informe o título, com até 120 caracteres e em uma linha só");

    public static readonly Error BodyInvalid = new("announcement.body.invalid", "Informe o texto, com até 5000 caracteres");

    public static readonly Error CategoryInvalid = new("announcement.category.invalid", "Escolha a categoria: general, maintenance ou important");

    public static readonly Error ScopeInvalid = new("announcement.scope.invalid", "Escolha a quem se dirige: condominium, block ou unit");

    public static readonly Error BlockInvalid = new("announcement.block.invalid", "Informe o bloco, com até 30 caracteres");

    public static readonly Error UnitRequired = new("announcement.unit.required", "Informe a unidade do comunicado");

    public static readonly Error BlockNotFound = new("announcement.block.not_found", "Este condomínio não tem unidades nesse bloco");

    public static readonly Error UnitNotFound = new("announcement.unit.not_found", "Unidade não encontrada");

    public static readonly Error NotFound = new("announcement.not_found", "Comunicado não encontrado");

    public static readonly Error PageInvalid = new("announcement.page.invalid", "Informe a página (a partir de 1) e o tamanho dela (de 1 a 100)");
}
