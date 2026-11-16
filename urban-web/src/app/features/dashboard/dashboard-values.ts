import { MonthEntries } from '../../core/finance/finance.models';
import { PaymentInstructions } from '../../core/finance/payment-instructions.models';
import { monthTitle } from '../../core/finance/month';
import { UnitsSummary } from '../../core/units/units.models';

export type MonthState = 'not-open' | 'open' | 'closed';

export type PendingId =
  | 'no-units'
  | 'apportionment'
  | 'payment-instructions'
  | 'month-not-open'
  | 'amounts-pending'
  | 'expenses-overdue'
  | 'charges-overdue';

/** Algo que o síndico precisa resolver, com o texto, para onde ir e o rótulo do link. */
export interface PendingItem {
  readonly id: PendingId;
  readonly text: string;
  readonly link: string;
  readonly linkLabel: string;
}

/** O que cada pendência precisa saber; nulo é o que não foi lido, e disso não se aponta nada. */
export interface PendingInput {
  readonly month: MonthEntries | null;
  /** Quantas cobranças do mês estão em atraso; nulo enquanto o mês está aberto ou a leitura falhou. */
  readonly overdueCharges: number | null;
  readonly units: UnitsSummary | null;
  readonly instructions: PaymentInstructions | null;
}

const ENTRIES_LINK = '/financeiro/lancamentos';
const UNITS_LINK = '/unidades';

export function monthState(month: MonthEntries): MonthState {
  if (month.closing !== null) {
    return 'closed';
  }
  return month.entries.length === 0 ? 'not-open' : 'open';
}

/** Arredonda para baixo: 100% só aparece quando não falta nem um centavo. */
export function receivedPercent(charged: number, received: number): number {
  if (charged <= 0) {
    return 0;
  }
  return Math.min(100, Math.floor((received / charged) * 100));
}

function plural(count: number, one: string, many: string): string {
  return `${String(count)} ${count === 1 ? one : many}`;
}

function unitPendings(units: UnitsSummary | null): PendingItem[] {
  if (units === null) {
    return [];
  }
  if (units.active === 0) {
    return [
      {
        id: 'no-units',
        text: 'Nenhuma unidade cadastrada',
        link: UNITS_LINK,
        linkLabel: 'Cadastrar unidades',
      },
    ];
  }
  const chosen = units.readiness.find((method) => method.method === units.apportionmentMethod);
  return chosen?.ready === false
    ? [
        {
          id: 'apportionment',
          text: 'O rateio ainda não está pronto: faltam dados nas unidades',
          link: UNITS_LINK,
          linkLabel: 'Completar unidades',
        },
      ]
    : [];
}

function instructionPendings(instructions: PaymentInstructions | null): PendingItem[] {
  return instructions !== null && instructions.pix === null && instructions.account === null
    ? [
        {
          id: 'payment-instructions',
          text: 'Os dados de pagamento ainda não foram informados',
          link: '/financeiro/dados-de-pagamento',
          linkLabel: 'Informar dados',
        },
      ]
    : [];
}

function monthPendings(month: MonthEntries | null): PendingItem[] {
  if (month === null) {
    return [];
  }
  if (monthState(month) === 'not-open') {
    return [
      {
        id: 'month-not-open',
        text: `O mês de ${monthTitle(month.month)} ainda não foi aberto`,
        link: ENTRIES_LINK,
        linkLabel: 'Abrir o mês',
      },
    ];
  }
  const items: PendingItem[] = [];
  if (month.needsAmountCount > 0) {
    const count = month.needsAmountCount;
    items.push({
      id: 'amounts-pending',
      text: `${plural(count, 'lançamento aguarda', 'lançamentos aguardam')} o valor`,
      link: ENTRIES_LINK,
      linkLabel: 'Informar valores',
    });
  }
  if (month.overdueCount > 0) {
    items.push({
      id: 'expenses-overdue',
      text: `${plural(month.overdueCount, 'despesa vencida', 'despesas vencidas')} sem pagamento`,
      link: ENTRIES_LINK,
      linkLabel: 'Ver lançamentos',
    });
  }
  return items;
}

function chargePendings(overdueCharges: number | null): PendingItem[] {
  return overdueCharges !== null && overdueCharges > 0
    ? [
        {
          id: 'charges-overdue',
          text: plural(overdueCharges, 'cobrança em atraso', 'cobranças em atraso'),
          link: '/financeiro/pagamentos',
          linkLabel: 'Ver pagamentos',
        },
      ]
    : [];
}

/** As pendências, de quem precisa agir primeiro (cadastro) a quem acompanha o dia a dia (cobranças). */
export function buildPendings(input: PendingInput): PendingItem[] {
  return [
    ...unitPendings(input.units),
    ...instructionPendings(input.instructions),
    ...monthPendings(input.month),
    ...chargePendings(input.overdueCharges),
  ];
}
