namespace Urban.Finance.Domain.Payments;

/// <summary>Como o dinheiro chegou. Gravado como texto (o nome do valor); o 0 nunca vale.</summary>
public enum PaymentMethod
{
    Pix = 1,
    Boleto = 2,
    Transfer = 3,
    Cash = 4,
    Other = 5,
}
