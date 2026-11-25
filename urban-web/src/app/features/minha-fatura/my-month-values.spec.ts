import { MY_INVOICE_MESSAGES, MY_RECEIPT_MESSAGES, unitHeading } from './my-month-values';

describe('unitHeading', () => {
  it('chama a unidade sem bloco pelo número', () => {
    expect(unitHeading({ block: null, number: '101' })).toBe('Unidade 101');
  });

  it('põe o bloco antes do número', () => {
    expect(unitHeading({ block: 'A', number: '102' })).toBe('Bloco A · Unidade 102');
  });
});

describe('MY_INVOICE_MESSAGES', () => {
  it('explica que a fatura não está disponível, sem falar em lista de cobranças', () => {
    expect(MY_INVOICE_MESSAGES['finance.charge.not_found']).toBe(
      'Esta fatura não está disponível. Atualize a tela e tente de novo',
    );
  });
});

describe('MY_RECEIPT_MESSAGES', () => {
  it('explica que o recibo não está disponível, sem falar em lista de pagamentos', () => {
    expect(MY_RECEIPT_MESSAGES['finance.payment.not_found']).toBe(
      'Este recibo não está disponível. Atualize a tela e tente de novo',
    );
  });
});
