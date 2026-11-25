import { MessagesByCode } from '../../core/http/problem-message';

/** O que a tela do síndico diz quando o recibo é recusado, ao baixar ou ao enviar por e-mail (limite, rede e o resto vêm do texto padrão). */
export const RECEIPT_MESSAGES: MessagesByCode = {
  'finance.payment.not_found':
    'Este pagamento não existe mais. Feche esta janela e atualize a lista',
  'finance.receipt_delivery.no_recipient':
    'A unidade não tem um responsável com e-mail cadastrado. Cadastre o e-mail do morador e tente de novo',
  'finance.receipt_delivery.payment_reversed':
    'Este pagamento foi estornado e o recibo dele não é enviado por e-mail. Feche esta janela e atualize a lista',
};
