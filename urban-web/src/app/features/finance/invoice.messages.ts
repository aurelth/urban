import { MessagesByCode } from '../../core/http/problem-message';

/** O que a tela diz quando o download da fatura é recusado (404, limite, rede e o resto vêm do texto padrão). */
export const INVOICE_MESSAGES: MessagesByCode = {
  'finance.charge.not_found': 'Esta cobrança não existe mais. Atualize a lista e tente de novo',
};
