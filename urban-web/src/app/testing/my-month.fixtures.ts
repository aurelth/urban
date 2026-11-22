import {
  MyCharge,
  MyExpenseGroup,
  MyExpenses,
  MyMonth,
  MyUnit,
} from '../core/finance/my-month.models';

let sequence = 0;

function nextId(prefix: string): string {
  sequence += 1;
  return `0199dddd-${prefix}-7000-8000-${String(sequence).padStart(12, '0')}`;
}

export function myCharge(overrides: Partial<MyCharge> = {}): MyCharge {
  return {
    chargeId: nextId('0070'),
    dueDate: '2026-11-10',
    amount: 800,
    paid: 0,
    balance: 800,
    status: 'open',
    lastReceivedOn: null,
    ...overrides,
  };
}

export function myUnit(overrides: Partial<MyUnit> = {}): MyUnit {
  return {
    unitId: nextId('0071'),
    block: null,
    number: '101',
    charge: myCharge(),
    ...overrides,
  };
}

export function myExpenseGroup(overrides: Partial<MyExpenseGroup> = {}): MyExpenseGroup {
  return {
    groupId: nextId('0072'),
    groupName: 'Despesas ordinárias',
    total: 1000,
    ...overrides,
  };
}

/** Sem `total`, soma os grupos dados. */
export function myExpenses(overrides: Partial<MyExpenses> = {}): MyExpenses {
  const groups = overrides.groups ?? [myExpenseGroup()];
  return {
    total: groups.reduce((sum, group) => sum + group.total, 0),
    groups,
    ...overrides,
  };
}

/** Um mês fechado com uma unidade e as despesas; `closed: false` dá o mês ainda aberto. */
export function myMonth(overrides: Partial<MyMonth> = {}): MyMonth {
  const closed = overrides.closed ?? true;
  return {
    month: '2026-11',
    asOf: '2026-11-15',
    access: 'unit',
    closed,
    units: closed ? [myUnit()] : [myUnit({ charge: null })],
    expenses: closed ? myExpenses() : null,
    ...overrides,
  };
}
