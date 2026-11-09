import {
  ChargeLine,
  ConsumptionTariff,
  Expense,
  ExpenseEntry,
  ExpenseGroup,
  GroupTotal,
  MonthClosing,
  MonthEntries,
  TariffBand,
  UnitCharge,
} from '../core/finance/finance.models';

let sequence = 0;

function nextId(prefix: string): string {
  sequence += 1;
  return `0199bbbb-${prefix}-7000-8000-${String(sequence).padStart(12, '0')}`;
}

export function expenseGroup(overrides: Partial<ExpenseGroup> = {}): ExpenseGroup {
  return {
    id: nextId('0001'),
    name: 'Despesas ordinárias',
    reserveFundBase: false,
    version: '5',
    ...overrides,
  };
}

/** Despesa fixa mensal ativa de teste; os outros tipos mudam só o que for preciso. */
export function expense(overrides: Partial<Expense> = {}): Expense {
  return {
    id: nextId('0002'),
    groupId: '0199bbbb-0001-7000-8000-000000000001',
    description: 'Portaria',
    supplier: null,
    kind: 'fixed',
    startMonth: '2026-09',
    endMonth: null,
    lastMonth: null,
    currentAmount: 4200,
    dueDay: 10,
    installmentCount: null,
    currentInstallment: null,
    installments: [],
    amountChanges: [{ fromMonth: '2026-09', amount: 4200 }],
    isEnded: false,
    version: '11',
    ...overrides,
  };
}

export function installmentExpense(overrides: Partial<Expense> = {}): Expense {
  return expense({
    description: 'Elevador',
    kind: 'installment',
    startMonth: '2026-11',
    lastMonth: '2027-04',
    currentAmount: 166.66,
    installmentCount: 6,
    currentInstallment: 1,
    installments: [
      { number: 1, month: '2026-11', amount: 166.66 },
      { number: 2, month: '2026-12', amount: 166.66 },
      { number: 3, month: '2027-01', amount: 166.66 },
      { number: 4, month: '2027-02', amount: 166.66 },
      { number: 5, month: '2027-03', amount: 166.66 },
      { number: 6, month: '2027-04', amount: 166.7 },
    ],
    amountChanges: [],
    ...overrides,
  });
}

export function entry(overrides: Partial<ExpenseEntry> = {}): ExpenseEntry {
  return {
    id: nextId('0003'),
    expenseId: nextId('0002'),
    groupId: '0199bbbb-0001-7000-8000-000000000001',
    month: '2026-11',
    description: 'Portaria',
    amount: 4200,
    label: null,
    dueDate: '2026-11-10',
    status: 'pending',
    needsAmount: false,
    suggestedAmount: null,
    version: '21',
    ...overrides,
  };
}

export function groupTotal(overrides: Partial<GroupTotal> = {}): GroupTotal {
  return {
    groupId: '0199bbbb-0001-7000-8000-000000000001',
    groupName: 'Despesas ordinárias',
    total: 4200,
    needsAmountCount: 0,
    ...overrides,
  };
}

export function monthEntries(overrides: Partial<MonthEntries> = {}): MonthEntries {
  return {
    month: '2026-11',
    entries: [],
    groups: [],
    total: 0,
    needsAmountCount: 0,
    overdueCount: 0,
    closing: null,
    ...overrides,
  };
}

export function chargeLine(overrides: Partial<ChargeLine> = {}): ChargeLine {
  return {
    kind: 'expenses',
    label: 'Despesas',
    amount: 2100,
    quantity: null,
    previousReading: null,
    currentReading: null,
    ...overrides,
  };
}

/** Sem `lines`, a cobrança tem só a linha de despesas, com o valor todo (como os meses fechados antes do consumo). */
export function unitCharge(overrides: Partial<UnitCharge> = {}): UnitCharge {
  const amount = overrides.amount ?? 2100;
  return {
    unitId: nextId('0004'),
    block: null,
    number: '101',
    weight: 0.5,
    amount,
    lines: [chargeLine({ amount })],
    ...overrides,
  };
}

export function monthClosing(overrides: Partial<MonthClosing> = {}): MonthClosing {
  return {
    closedAt: '2026-11-15T15:00:00-03:00',
    dueDate: '2026-12-10',
    total: 4200,
    basis: 'idealFraction',
    charges: [unitCharge({ number: '101' }), unitCharge({ number: '102' })],
    ...overrides,
  };
}

export function tariffBand(overrides: Partial<TariffBand> = {}): TariffBand {
  return { upToCubicMeters: null, fixedAmount: 0, pricePerCubicMeter: 5.1, ...overrides };
}

/** Tarifa de água em 3 faixas (a primeira com valor fixo), sem esgoto nem gás; os testes ligam o resto. */
export function consumptionTariff(overrides: Partial<ConsumptionTariff> = {}): ConsumptionTariff {
  return {
    validFrom: '2026-11',
    water: [
      tariffBand({ upToCubicMeters: 10, fixedAmount: 28.41, pricePerCubicMeter: 0 }),
      tariffBand({ upToCubicMeters: 20, pricePerCubicMeter: 3.5 }),
      tariffBand({ pricePerCubicMeter: 5.1 }),
    ],
    sewage: null,
    sewagePercent: 100,
    gas: null,
    ...overrides,
  };
}
