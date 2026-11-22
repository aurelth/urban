/** O total de um grupo de despesa no mês, sem as despesas canceladas. */
export interface DashboardGroup {
  readonly groupId: string;
  readonly groupName: string;
  readonly total: number;
  /** Quantas despesas do grupo ainda esperam o valor. */
  readonly needsAmountCount: number;
}

/** As despesas do mês, com o total de cada grupo (em ordem alfabética). */
export interface DashboardExpenses {
  readonly total: number;
  readonly needsAmountCount: number;
  readonly overdueCount: number;
  readonly groups: readonly DashboardGroup[];
}

/**
 * As cobranças de um mês fechado. `open`, `partial`, `paid` e `overdue` contam unidades e somam `units`; `overdueAmount` é o
 * saldo das cobranças atrasadas deste mês (o total de todos os meses é da Inadimplência).
 */
export interface DashboardCharges {
  /** AAAA-MM-DD. */
  readonly dueDate: string;
  readonly charged: number;
  readonly received: number;
  readonly balance: number;
  readonly units: number;
  readonly open: number;
  readonly partial: number;
  readonly paid: number;
  readonly overdue: number;
  readonly overdueAmount: number;
}

/** O painel financeiro de um mês. `charges` só vem com o mês fechado (`closed`). */
export interface MonthDashboard {
  readonly month: string;
  /** AAAA-MM-DD do dia, em Brasília, a que a situação das cobranças se refere. */
  readonly asOf: string;
  readonly closed: boolean;
  readonly expenses: DashboardExpenses;
  readonly charges: DashboardCharges | null;
}
