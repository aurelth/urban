namespace Urban.SharedKernel.Contracts;

/// <param name="Name">Nome do síndico.</param>
/// <param name="Phone">Celular já formatado, "(47) 99123-4567".</param>
public sealed record InvoiceManager(string Name, string? Phone)
{
    // Nome e telefone são dados pessoais: um log ou uma mensagem de erro que imprima o objeto não os vaza.
    public override string ToString() => nameof(InvoiceManager);
}

/// <param name="Name">Nome do condomínio.</param>
/// <param name="Cnpj">CNPJ já formatado (00.000.000/0000-00).</param>
/// <param name="Address">Endereço em uma linha, pronto para o cabeçalho da fatura.</param>
/// <param name="Manager">Quem o morador procura para tirar dúvidas; nulo se o cadastro do síndico não foi encontrado.</param>
public sealed record InvoiceCondominium(string Name, string Cnpj, string Address, InvoiceManager? Manager = null);

/// <param name="Name">Nome de quem recebe a fatura da unidade.</param>
/// <param name="Email">E-mail do cadastro; o do síndico, que não tem e-mail de cadastro, é o do login. Nulo quando a pessoa não tem nenhum (a fatura existe, mas não há para onde enviá-la).</param>
public sealed record InvoiceResponsible(string Name, string? Email)
{
    // Nome e e-mail são dados pessoais: um log ou uma mensagem de erro que imprima o objeto não os vaza.
    public override string ToString() => nameof(InvoiceResponsible);
}

/// <summary>
/// Contrato do módulo Condominium para o Financeiro: quem emite a fatura (o condomínio) e quem a recebe (o responsável de cada
/// unidade). O Financeiro monta o PDF e o envio sem conhecer moradores, vínculos nem endereços.
/// </summary>
public interface IInvoiceParties
{
    /// <returns>Os dados do condomínio da requisição, ou nulo se ele não existe.</returns>
    Task<InvoiceCondominium?> GetCondominiumAsync(CancellationToken cancellationToken);

    /// <summary>
    /// O responsável de cada unidade na data: o inquilino que mora nela; sem inquilino, o proprietário. Dependentes nunca recebem.
    /// O síndico que mora na unidade é o responsável como qualquer proprietário, e recebe no e-mail do login.
    /// Uma consulta para todas as unidades pedidas, nunca uma por unidade.
    /// </summary>
    /// <param name="unitIds">Unidades do condomínio da requisição.</param>
    /// <param name="today">A data de referência (Brasília): só vale vínculo que já começou e ainda não terminou.</param>
    /// <returns>Só as unidades que têm responsável; as demais ficam de fora.</returns>
    Task<IReadOnlyDictionary<Guid, InvoiceResponsible>> GetResponsiblesAsync(IReadOnlyCollection<Guid> unitIds, DateOnly today, CancellationToken cancellationToken);
}
