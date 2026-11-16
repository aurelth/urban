import { formatMoney } from '../../core/finance/money';
import { MessagesByCode } from '../../core/http/problem-message';
import { FINANCE_FORM_MESSAGES } from './finance.messages';

/** O que a tela diz quando o registro de um pagamento é recusado (dentro do diálogo, que já pede para atualizar a lista). */
export const REGISTER_PAYMENT_MESSAGES: MessagesByCode = {
  ...FINANCE_FORM_MESSAGES,
  'finance.payment.amount_invalid': 'Informe um valor maior que zero, com até 2 casas decimais',
  'finance.payment.method_invalid': 'Escolha a forma de pagamento',
  'finance.payment.date_invalid':
    'A data do recebimento não pode ser no futuro nem anterior a 12 meses antes da competência',
  'finance.payment.note_invalid': 'A observação pode ter até 200 caracteres',
  'finance.payment.exceeds_balance': (problem) => {
    const balance = problem.data?.['balance'];
    return typeof balance === 'number'
      ? `O valor passa do saldo atual de ${formatMoney(balance)}. Atualizamos a lista`
      : 'O valor passa do saldo da cobrança. Atualizamos a lista';
  },
  'finance.charge.not_found': 'Esta cobrança não existe mais. Atualizamos a lista',
};

/** O que o diálogo do histórico diz quando o estorno é recusado. */
export const REVERSE_PAYMENT_MESSAGES: MessagesByCode = {
  'finance.payment.reason_invalid': 'Informe o motivo do estorno',
  'finance.payment.already_reversed': 'Este pagamento já foi estornado. A lista já foi atualizada',
  'finance.payment.not_found': 'Este pagamento não existe mais. A lista já foi atualizada',
};
