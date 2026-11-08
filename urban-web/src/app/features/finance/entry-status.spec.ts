import { EntryStatus } from '../../core/finance/finance.models';
import { entry } from '../../testing/finance.fixtures';
import {
  STATUS_CHOICES,
  isChoiceDisabled,
  statusIcon,
  statusLabel,
  statusNotice,
  statusTone,
} from './entry-status';

const ALL: readonly EntryStatus[] = ['pending', 'paid', 'overdue', 'canceled'];

describe('entry-status', () => {
  it('nomeia as quatro situacoes', () => {
    expect(ALL.map(statusLabel)).toEqual(['Pendente', 'Pago', 'Vencido', 'Cancelado']);
  });

  it('nunca depende so da cor: cada situacao tem um icone proprio', () => {
    expect(ALL.map(statusIcon)).toEqual(['schedule', 'check_circle', 'error', 'block']);
  });

  it('usa a cor de aviso para pendente, de sucesso para pago e de erro para vencido', () => {
    expect(ALL.map(statusTone)).toEqual([
      'text-warning',
      'text-success',
      'text-error',
      'text-on-surface-variant',
    ]);
  });

  it('oferece so o que se pode marcar: vencido e calculado, nao escolhido', () => {
    expect(STATUS_CHOICES).toEqual([
      { status: 'pending', label: 'Pendente' },
      { status: 'paid', label: 'Pago' },
      { status: 'canceled', label: 'Cancelado' },
    ]);
  });

  describe('isChoiceDisabled', () => {
    it('nao repete a situacao atual, e vencido conta como pendente', () => {
      expect(isChoiceDisabled(entry({ status: 'pending' }), 'pending')).toBe(true);
      expect(isChoiceDisabled(entry({ status: 'overdue' }), 'pending')).toBe(true);
      expect(isChoiceDisabled(entry({ status: 'paid' }), 'paid')).toBe(true);
      expect(isChoiceDisabled(entry({ status: 'canceled' }), 'canceled')).toBe(true);
    });

    it('deixa mudar para as outras situacoes', () => {
      expect(isChoiceDisabled(entry({ status: 'paid' }), 'pending')).toBe(false);
      expect(isChoiceDisabled(entry({ status: 'canceled' }), 'pending')).toBe(false);
      expect(isChoiceDisabled(entry({ status: 'pending' }), 'paid')).toBe(false);
      expect(isChoiceDisabled(entry({ status: 'overdue' }), 'paid')).toBe(false);
      expect(isChoiceDisabled(entry({ status: 'pending' }), 'canceled')).toBe(false);
    });

    it('so deixa pagar depois de informar o valor', () => {
      expect(isChoiceDisabled(entry({ status: 'pending', needsAmount: true }), 'paid')).toBe(true);
      expect(isChoiceDisabled(entry({ status: 'pending', needsAmount: true }), 'canceled')).toBe(
        false,
      );
    });
  });

  it('avisa o que aconteceu', () => {
    expect(statusNotice('paid')).toBe('Lançamento marcado como pago');
    expect(statusNotice('pending')).toBe('Lançamento marcado como pendente');
    expect(statusNotice('canceled')).toBe('Lançamento cancelado');
  });
});
