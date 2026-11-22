import {
  DashboardCharges,
  DashboardExpenses,
  DashboardGroup,
  MonthDashboard,
} from '../core/finance/month-dashboard.models';

let sequence = 0;

function nextId(prefix: string): string {
  sequence += 1;
  return `0199cccc-${prefix}-7000-8000-${String(sequence).padStart(12, '0')}`;
}

export function dashboardGroup(overrides: Partial<DashboardGroup> = {}): DashboardGroup {
  return {
    groupId: nextId('0060'),
    groupName: 'Despesas ordinárias',
    total: 1000,
    needsAmountCount: 0,
    ...overrides,
  };
}

/** Sem `total`, soma os grupos dados. */
export function dashboardExpenses(overrides: Partial<DashboardExpenses> = {}): DashboardExpenses {
  const groups = overrides.groups ?? [dashboardGroup()];
  return {
    total: groups.reduce((sum, group) => sum + group.total, 0),
    needsAmountCount: 0,
    overdueCount: 0,
    groups,
    ...overrides,
  };
}

/** Sem `balance`, é o cobrado menos o recebido; sem `units`, a soma das situações. */
export function dashboardCharges(overrides: Partial<DashboardCharges> = {}): DashboardCharges {
  const base = {
    dueDate: '2026-11-10',
    charged: 2400,
    received: 1300,
    open: 0,
    partial: 1,
    paid: 1,
    overdue: 1,
    overdueAmount: 800,
    ...overrides,
  };
  return {
    ...base,
    balance: overrides.balance ?? Math.round((base.charged - base.received) * 100) / 100,
    units: overrides.units ?? base.open + base.partial + base.paid + base.overdue,
  };
}

/** Um mês fechado, com cobranças; `charges: null` e `closed: false` dão o mês ainda aberto. */
export function monthDashboard(overrides: Partial<MonthDashboard> = {}): MonthDashboard {
  const closed = overrides.closed ?? overrides.charges !== null;
  return {
    month: '2026-11',
    asOf: '2026-11-15',
    closed,
    expenses: dashboardExpenses(),
    charges: closed ? dashboardCharges() : null,
    ...overrides,
  };
}
