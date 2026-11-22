import { inject } from '@angular/core';
import { MonthDashboardApi } from '../../core/finance/month-dashboard.api';
import { createMonthStore } from './month-store';

/** Estado do painel financeiro do mês. Fornecido no componente, então cada visita começa do zero. */
export const MonthDashboardStore = createMonthStore(() => {
  const api = inject(MonthDashboardApi);
  return (month) => api.get(month);
});
