import { MessagesByCode } from '../../core/http/problem-message';

/** O que a tela do síndico diz quando o recibo é recusado (limite, rede e o resto vêm do texto padrão). */
export const RECEIPT_MESSAGES: MessagesByCode = {
  'finance.payment.not_found':
    'Este pagamento não existe mais. Feche esta janela e atualize a lista',
};
