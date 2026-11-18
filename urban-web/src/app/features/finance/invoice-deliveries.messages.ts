import { MessagesByCode } from '../../core/http/problem-message';

/**
 * O que a tela diz quando o envio de uma fatura é recusado. O limite de requisições e a falta de rede vêm do texto padrão;
 * "mês não fechado" não precisa de frase porque a tela passa a explicar que o mês não foi fechado.
 */
export const INVOICE_DELIVERY_MESSAGES: MessagesByCode = {
  'finance.charge.not_found': 'Esta cobrança não existe mais. Atualizamos a lista',
  'finance.invoice_delivery.no_recipient':
    'Esta unidade não tem responsável com e-mail cadastrado. Cadastre o e-mail e tente de novo',
  'finance.invoice_delivery.already_queued':
    'A fatura desta unidade já está na fila de envio. Atualizamos a lista',
};
