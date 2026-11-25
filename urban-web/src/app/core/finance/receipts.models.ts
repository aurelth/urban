/** A resposta do pedido de envio do recibo por e-mail: quem vai receber (o e-mail em si não sai da API). */
export interface ReceiptEmailResult {
  readonly recipientName: string;
}
