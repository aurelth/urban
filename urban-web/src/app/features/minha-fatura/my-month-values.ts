import { MessagesByCode } from '../../core/http/problem-message';

/** O título de cada cartão de unidade: "Unidade 101" ou "Bloco A · Unidade 102". */
export function unitHeading(unit: {
  readonly block: string | null;
  readonly number: string;
}): string {
  return unit.block ? `Bloco ${unit.block} · Unidade ${unit.number}` : `Unidade ${unit.number}`;
}

/** O que a tela do morador diz quando o download do recibo é recusado (limite, rede e o resto vêm do texto padrão). */
export const MY_RECEIPT_MESSAGES: MessagesByCode = {
  'finance.payment.not_found': 'Este recibo não está disponível. Atualize a tela e tente de novo',
};

/** O que a tela diz quando o download da própria fatura é recusado (limite, rede e o resto vêm do texto padrão). */
export const MY_INVOICE_MESSAGES: MessagesByCode = {
  'finance.charge.not_found': 'Esta fatura não está disponível. Atualize a tela e tente de novo',
};
