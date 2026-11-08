import { EntryStatus } from '../../core/finance/finance.models';
import { entry } from '../../testing/finance.fixtures';
import {
  STATUS_CHOICES,
  isChoiceDisabled,
  statusIcon,
  statusLabel,
  statusMenuTone,
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

  it('usa laranja para pendente, verde para pago e vermelho para vencido e para cancelado', () => {
    expect(ALL.map(statusTone)).toEqual([
      'text-warning',
      'text-success',
      'text-error',
      'text-error',
    ]);
  });

  it('pinta o icone e o texto do item do menu com a cor da situacao', () => {
    for (const [status, color] of [
      ['pending', 'var(--urban-warning)'],
      ['paid', 'var(--urban-success)'],
      ['overdue', 'var(--mat-sys-error)'],
      ['canceled', 'var(--mat-sys-error)'],
    ] as const) {
      expect(statusMenuTone(status)).toBe(
        `[--mat-menu-item-icon-color:${color}] [--mat-menu-item-label-text-color:${color}]`,
      );
    }
  });

  it('oferece so o que se pode marcar: vencido e calculado, nao escolhido', () => {
    expect(STATUS_CHOICES).toEqual([
      { status: 'pending', label: 'Pendente' },
      { status: 'paid', label: 'Pago' },
      { status: 'canceled', label: 'Cancelado' },
    ]);
  });

  describe('isChoiceDisabled', () => {
    describe('com o mes fechado', () => {
      it('nao deixa cancelar, porque isso mudaria o total ja rateado', () => {
        expect(isChoiceDisabled(entry({ status: 'pending' }), 'canceled', true)).toBe(true);
        expect(isChoiceDisabled(entry({ status: 'overdue' }), 'canceled', true)).toBe(true);
        expect(isChoiceDisabled(entry({ status: 'paid' }), 'canceled', true)).toBe(true);
      });

      it('nao deixa desfazer um cancelamento', () => {
        const canceled = entry({ status: 'canceled' });

        expect(isChoiceDisabled(canceled, 'pending', true)).toBe(true);
        expect(isChoiceDisabled(canceled, 'paid', true)).toBe(true);
      });

      it('continua deixando pagar e voltar para pendente, que nao mexem nas cobrancas', () => {
        expect(isChoiceDisabled(entry({ status: 'pending' }), 'paid', true)).toBe(false);
        expect(isChoiceDisabled(entry({ status: 'paid' }), 'pending', true)).toBe(false);
      });

      it('sem o terceiro argumento o mes e considerado aberto', () => {
        expect(isChoiceDisabled(entry({ status: 'pending' }), 'canceled')).toBe(false);
      });
    });

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
});
