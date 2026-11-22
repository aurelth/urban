import {
  dashboardCharges,
  dashboardExpenses,
  dashboardGroup,
  monthDashboard,
} from '../../testing/month-dashboard.fixtures';
import {
  dashboardMonthState,
  expenseAttentions,
  groupShare,
  overdueChargesText,
  unitSituations,
} from './month-dashboard-values';

describe('dashboardMonthState', () => {
  it('é fechado quando o mês foi fechado', () => {
    expect(dashboardMonthState(monthDashboard())).toBe('closed');
  });

  it('é aberto quando há despesas e o mês ainda não foi fechado', () => {
    expect(dashboardMonthState(monthDashboard({ closed: false, charges: null }))).toBe('open');
  });

  it('é "ainda não aberto" quando não há nenhum grupo de despesa', () => {
    const data = monthDashboard({
      closed: false,
      charges: null,
      expenses: dashboardExpenses({ groups: [], total: 0 }),
    });

    expect(dashboardMonthState(data)).toBe('not-open');
  });
});

describe('groupShare', () => {
  it.each([
    [1000, 250, 25],
    [1000, 1000, 100],
    [1000, 333, 33],
    [1000, 336, 34],
    [3, 1, 33],
  ])('o grupo de %d com %d é %d%% do total', (total, group, expected) => {
    expect(groupShare(total, group)).toBe(expected);
  });

  it('é zero sem total ou sem valor no grupo', () => {
    expect(groupShare(0, 0)).toBe(0);
    expect(groupShare(1000, 0)).toBe(0);
  });

  it('nunca passa de 100', () => {
    expect(groupShare(100, 150)).toBe(100);
  });
});

describe('unitSituations', () => {
  const charges = dashboardCharges({ open: 4, partial: 3, paid: 2, overdue: 1 });

  it('lista pagas, em aberto, parciais e atrasadas, nessa ordem, com a contagem de cada uma', () => {
    const situations = unitSituations(charges);

    expect(situations.map((item) => [item.status, item.label, item.count])).toEqual([
      ['paid', 'Pagas', 2],
      ['open', 'Em aberto', 4],
      ['partial', 'Parciais', 3],
      ['overdue', 'Atrasadas', 1],
    ]);
  });

  it('usa o ícone e a cor de cada situação, o mesmo da tela de pagamentos', () => {
    const situations = unitSituations(charges);

    expect(situations.map((item) => [item.icon, item.tone])).toEqual([
      ['check_circle', 'text-success'],
      ['schedule', 'text-warning'],
      ['trending_up', 'text-warning'],
      ['error', 'text-error'],
    ]);
  });
});

describe('expenseAttentions', () => {
  it('não avisa de nada quando está tudo preenchido e em dia', () => {
    expect(expenseAttentions(dashboardExpenses())).toEqual([]);
  });

  it('avisa dos lançamentos sem valor, no singular e no plural', () => {
    expect(expenseAttentions(dashboardExpenses({ needsAmountCount: 1 }))).toEqual([
      { id: 'needs-amount', text: '1 lançamento aguarda o valor' },
    ]);
    expect(expenseAttentions(dashboardExpenses({ needsAmountCount: 3 }))).toEqual([
      { id: 'needs-amount', text: '3 lançamentos aguardam o valor' },
    ]);
  });

  it('avisa das despesas vencidas, no singular e no plural', () => {
    expect(expenseAttentions(dashboardExpenses({ overdueCount: 1 }))).toEqual([
      { id: 'overdue', text: '1 despesa vencida' },
    ]);
    expect(expenseAttentions(dashboardExpenses({ overdueCount: 2 }))).toEqual([
      { id: 'overdue', text: '2 despesas vencidas' },
    ]);
  });

  it('põe primeiro o que falta preencher', () => {
    const items = expenseAttentions(dashboardExpenses({ needsAmountCount: 2, overdueCount: 1 }));

    expect(items.map((item) => item.id)).toEqual(['needs-amount', 'overdue']);
  });

  it('mantém o grupo que serve de exemplo nos dados de teste', () => {
    expect(dashboardGroup().groupName).toBe('Despesas ordinárias');
  });
});

describe('overdueChargesText', () => {
  it.each([
    [0, 'Nenhuma cobrança em atraso'],
    [1, '1 cobrança em atraso'],
    [4, '4 cobranças em atraso'],
  ])('com %d atrasadas diz "%s"', (count, expected) => {
    expect(overdueChargesText(count)).toBe(expected);
  });
});
