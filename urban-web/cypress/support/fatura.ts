/** O download da fatura em PDF (GET): devolve um PDF mínimo com o nome sugerido pela API, como o `Results.File` do .NET faz. */
export function stubInvoice(chargeId: string, fileName: string): void {
  cy.intercept('GET', `/api/finance/charges/${chargeId}/invoice`, {
    statusCode: 200,
    headers: {
      'content-type': 'application/pdf',
      'content-disposition': `attachment; filename=${fileName}; filename*=UTF-8''${fileName}`,
    },
    body: '%PDF-1.7\n% fatura de teste do Urban\n',
  }).as('invoice');
}

/** A API recusa o download com um problema (404, 429...): o corpo chega como arquivo, como na API de verdade. */
export function stubInvoiceRejected(chargeId: string, status: number, code: string): void {
  cy.intercept('GET', `/api/finance/charges/${chargeId}/invoice`, {
    statusCode: status,
    headers: { 'content-type': 'application/problem+json' },
    body: JSON.stringify({ status, code, detail: 'Detalhe da API' }),
  }).as('invoice');
}
