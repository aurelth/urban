import { Debtor, Delinquency, OverdueCharge } from '../core/finance/delinquency.models';

let sequence = 0;

function nextId(prefix: string): string {
  sequence += 1;
  return `0199bbbb-${prefix}-7000-8000-${String(sequence).padStart(12, '0')}`;
}

export function overdueCharge(overrides: Partial<OverdueCharge> = {}): OverdueCharge {
  return {
    chargeId: nextId('0050'),
    month: '2026-10',
    dueDate: '2026-11-10',
    amount: 800,
    paid: 0,
    balance: 800,
    daysOverdue: 5,
    ...overrides,
  };
}

/** Sem `charges`, a unidade deve uma cobrança só; `balance` e `overdueCharges` seguem as cobranças dadas. */
export function debtor(overrides: Partial<Debtor> = {}): Debtor {
  const charges = overrides.charges ?? [overdueCharge()];
  return {
    unitId: nextId('0051'),
    block: 'A',
    number: '101',
    balance: charges.reduce((sum, charge) => sum + charge.balance, 0),
    overdueCharges: charges.length,
    oldestDueDate: charges[0]?.dueDate ?? '2026-11-10',
    daysOverdue: charges[0]?.daysOverdue ?? 5,
    charges,
    ...overrides,
  };
}

/** Sem `totalOverdue` e `units`, soma as unidades dadas (que nos testes cabem numa página só). */
export function delinquency(overrides: Partial<Delinquency> = {}): Delinquency {
  const items = overrides.items ?? [debtor()];
  return {
    asOf: '2026-11-15',
    totalOverdue: items.reduce((sum, item) => sum + item.balance, 0),
    units: items.length,
    page: 1,
    pageSize: 50,
    items,
    ...overrides,
  };
}
