import { debtor, overdueCharge } from '../../testing/delinquency.fixtures';
import { daysText, debtorSummary, overdueChargesText } from './delinquency-values';

describe('daysText', () => {
  it.each([
    [1, '1 dia'],
    [2, '2 dias'],
    [36, '36 dias'],
    [365, '365 dias'],
  ])('escreve %i como "%s"', (days, text) => {
    expect(daysText(days)).toBe(text);
  });
});

describe('overdueChargesText', () => {
  it.each([
    [1, '1 cobrança em atraso'],
    [2, '2 cobranças em atraso'],
    [12, '12 cobranças em atraso'],
  ])('escreve %i como "%s"', (count, text) => {
    expect(overdueChargesText(count)).toBe(text);
  });
});

describe('debtorSummary', () => {
  it('diz quando venceu a única cobrança em atraso e há quantos dias', () => {
    const summary = debtorSummary(
      debtor({ overdueCharges: 1, oldestDueDate: '2026-11-10', daysOverdue: 5 }),
    );

    expect(summary).toBe('1 cobrança em atraso · venceu em 10/11/2026, há 5 dias');
  });

  it('com várias cobranças, conta a mais antiga', () => {
    const summary = debtorSummary(
      debtor({
        charges: [
          overdueCharge({ dueDate: '2026-10-10', daysOverdue: 36 }),
          overdueCharge({ dueDate: '2026-11-10', daysOverdue: 5 }),
        ],
      }),
    );

    expect(summary).toBe('2 cobranças em atraso · a mais antiga venceu em 10/10/2026, há 36 dias');
  });

  it('usa o singular quando o atraso é de um dia', () => {
    const summary = debtorSummary(
      debtor({ overdueCharges: 1, oldestDueDate: '2026-11-14', daysOverdue: 1 }),
    );

    expect(summary).toBe('1 cobrança em atraso · venceu em 14/11/2026, há 1 dia');
  });
});
