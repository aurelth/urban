import { Debtor } from '../../core/finance/delinquency.models';

export function daysText(days: number): string {
  throw new Error(`Não implementado: ${String(days)}`);
}

export function overdueChargesText(count: number): string {
  throw new Error(`Não implementado: ${String(count)}`);
}

export function debtorSummary(debtor: Debtor): string {
  throw new Error(`Não implementado: ${debtor.unitId}`);
}
