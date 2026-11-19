import { Debtor } from '../../core/finance/delinquency.models';
import { formatDate } from '../../core/finance/month';

export function daysText(days: number): string {
  return `${String(days)} ${days === 1 ? 'dia' : 'dias'}`;
}

export function overdueChargesText(count: number): string {
  return `${String(count)} ${count === 1 ? 'cobrança' : 'cobranças'} em atraso`;
}

/** Quantas cobranças a unidade tem em atraso e desde quando: com mais de uma, conta a mais antiga. */
export function debtorSummary(debtor: Debtor): string {
  const oldest = debtor.overdueCharges === 1 ? 'venceu' : 'a mais antiga venceu';
  return `${overdueChargesText(debtor.overdueCharges)} · ${oldest} em ${formatDate(debtor.oldestDueDate)}, há ${daysText(debtor.daysOverdue)}`;
}
