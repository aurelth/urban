import { Expense, ExpenseKind } from '../../core/finance/finance.models';
import { formatMoney } from '../../core/finance/money';
import { formatMonth } from '../../core/finance/month';

const KIND_LABELS: Readonly<Record<ExpenseKind, string>> = {
  single: 'Única',
  fixed: 'Fixa mensal',
  variable: 'Variável mensal',
  installment: 'Parcelada',
};

export function kindLabel(kind: ExpenseKind): string {
  return KIND_LABELS[kind];
}

/** O valor da lista: o em vigor, o total das parcelas ou o aviso de que a variável é informada a cada mês. */
export function amountText(item: Expense): string {
  switch (item.kind) {
    case 'variable':
      return 'Informado a cada mês';
    case 'installment': {
      const cents = item.installments.reduce(
        (sum, parcel) => sum + Math.round(parcel.amount * 100),
        0,
      );
      return `${formatMoney(cents / 100)} em ${item.installmentCount}x`;
    }
    default:
      return formatMoney(item.currentAmount);
  }
}

export function periodText(item: Expense): string {
  if (item.kind === 'single') {
    return formatMonth(item.startMonth);
  }
  if (item.kind === 'installment') {
    const first = item.installments[0]?.month ?? item.startMonth;
    return `${formatMonth(first)} a ${formatMonth(item.lastMonth ?? first)}`;
  }
  const last = item.lastMonth ?? item.endMonth;
  return last === null
    ? `${formatMonth(item.startMonth)} em diante`
    : `${formatMonth(item.startMonth)} a ${formatMonth(last)}`;
}

/** O vencimento da lista: o dia de cada mês, ou a data completa na despesa única (que só vence uma vez). */
export function dueText(item: Expense): string {
  if (item.kind !== 'single') {
    return `Todo dia ${item.dueDay}`;
  }
  const [year, month] = item.startMonth.split('-').map(Number);
  const day = Math.min(item.dueDay, new Date(Date.UTC(year, month, 0)).getUTCDate());
  return `${String(day).padStart(2, '0')}/${String(month).padStart(2, '0')}/${year}`;
}

export function canAdjust(item: Expense): boolean {
  return item.kind === 'fixed' && !item.isEnded;
}

export function canSetInstallment(item: Expense): boolean {
  return item.kind === 'installment' && !item.isEnded;
}

export function canEnd(item: Expense): boolean {
  return (item.kind === 'fixed' || item.kind === 'variable') && !item.isEnded;
}
