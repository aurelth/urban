import { expense, installmentExpense } from '../../testing/finance.fixtures';
import {
  amountText,
  canAdjust,
  canEnd,
  canSetInstallment,
  kindLabel,
  periodText,
} from './expense-values';

function plain(text: string): string {
  return text.replaceAll('\u00a0', ' ');
}

describe('expense-values', () => {
  it('nomeia os tipos', () => {
    expect(
      (['single', 'fixed', 'variable', 'installment'] as const).map((kind) => kindLabel(kind)),
    ).toEqual(['Única', 'Fixa mensal', 'Variável mensal', 'Parcelada']);
  });

  describe('amountText', () => {
    it('mostra o valor em vigor da unica e da fixa', () => {
      expect(plain(amountText(expense({ kind: 'fixed', currentAmount: 4200 })))).toBe(
        'R$ 4.200,00',
      );
      expect(plain(amountText(expense({ kind: 'single', currentAmount: 350.5 })))).toBe(
        'R$ 350,50',
      );
    });

    it('avisa que a variavel e informada a cada mes', () => {
      expect(amountText(expense({ kind: 'variable', currentAmount: null }))).toBe(
        'Informado a cada mês',
      );
    });

    it('soma as parcelas da parcelada, sem erro de arredondamento', () => {
      expect(plain(amountText(installmentExpense()))).toBe('R$ 1.000,00 em 6x');
    });
  });

  describe('periodText', () => {
    it('mostra so o mes da unica', () => {
      expect(periodText(expense({ kind: 'single', startMonth: '2026-11' }))).toBe('11/2026');
    });

    it('diz "em diante" quando nao ha mes final', () => {
      expect(periodText(expense({ startMonth: '2026-09', endMonth: null, lastMonth: null }))).toBe(
        '09/2026 em diante',
      );
    });

    it('usa o ultimo mes quando existe', () => {
      expect(
        periodText(expense({ startMonth: '2026-09', endMonth: '2027-08', lastMonth: '2027-08' })),
      ).toBe('09/2026 a 08/2027');
      expect(
        periodText(expense({ startMonth: '2026-09', endMonth: null, lastMonth: '2026-11' })),
      ).toBe('09/2026 a 11/2026');
    });

    it('vai da primeira a ultima parcela', () => {
      expect(periodText(installmentExpense())).toBe('11/2026 a 04/2027');
    });
  });

  describe('acoes permitidas', () => {
    it('reajuste so para fixa ativa', () => {
      expect(canAdjust(expense({ kind: 'fixed' }))).toBe(true);
      expect(canAdjust(expense({ kind: 'fixed', isEnded: true }))).toBe(false);
      expect(canAdjust(expense({ kind: 'variable' }))).toBe(false);
    });

    it('parcela so para parcelada ativa', () => {
      expect(canSetInstallment(installmentExpense())).toBe(true);
      expect(canSetInstallment(installmentExpense({ isEnded: true }))).toBe(false);
      expect(canSetInstallment(expense())).toBe(false);
    });

    it('encerrar so para fixa ou variavel ativa', () => {
      expect(canEnd(expense({ kind: 'fixed' }))).toBe(true);
      expect(canEnd(expense({ kind: 'variable' }))).toBe(true);
      expect(canEnd(expense({ kind: 'variable', isEnded: true }))).toBe(false);
      expect(canEnd(expense({ kind: 'single' }))).toBe(false);
      expect(canEnd(installmentExpense())).toBe(false);
    });
  });
});
