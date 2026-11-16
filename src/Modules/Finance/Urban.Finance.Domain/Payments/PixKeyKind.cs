namespace Urban.Finance.Domain.Payments;

/// <summary>Os tipos de chave Pix que o Banco Central define. O 0 nunca vale.</summary>
public enum PixKeyKind
{
    Cnpj = 1,
    Cpf = 2,
    Email = 3,
    Phone = 4,
    Random = 5,
}
