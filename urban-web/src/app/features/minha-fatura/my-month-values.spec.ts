import { MY_INVOICE_MESSAGES, unitHeading } from './my-month-values';

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
