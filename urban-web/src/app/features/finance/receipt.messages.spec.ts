import { RECEIPT_MESSAGES } from './receipt.messages';

describe('RECEIPT_MESSAGES', () => {
  it('explica que o pagamento nao existe mais e manda atualizar a lista do sindico', () => {
    expect(RECEIPT_MESSAGES['finance.payment.not_found']).toBe(
      'Este pagamento não existe mais. Feche esta janela e atualize a lista',
    );
  });
});
