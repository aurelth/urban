import { RECEIPT_MESSAGES } from './receipt.messages';

describe('RECEIPT_MESSAGES', () => {
  it('explica que o pagamento nao existe mais e manda atualizar a lista do sindico', () => {
    expect(RECEIPT_MESSAGES['finance.payment.not_found']).toBe(
      'Este pagamento não existe mais. Feche esta janela e atualize a lista',
    );
  });

  it('explica que a unidade nao tem responsavel com e-mail e o que fazer', () => {
    expect(RECEIPT_MESSAGES['finance.receipt_delivery.no_recipient']).toBe(
      'A unidade não tem um responsável com e-mail cadastrado. Cadastre o e-mail do morador e tente de novo',
    );
  });

  it('explica que o recibo de um pagamento estornado nao e enviado', () => {
    expect(RECEIPT_MESSAGES['finance.receipt_delivery.payment_reversed']).toBe(
      'Este pagamento foi estornado e o recibo dele não é enviado por e-mail. Feche esta janela e atualize a lista',
    );
  });
});
