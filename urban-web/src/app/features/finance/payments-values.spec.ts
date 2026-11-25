import { chargeSummary, monthPayments } from '../../testing/finance.fixtures';
import {
  receiptLabel,
  PAYMENT_METHODS,
  STATUS_FILTERS,
  chargeStatusView,
  methodLabel,
  toReceive,
} from './payments-values';

describe('chargeStatusView', () => {
  it.each([
    ['open', 'Em aberto', 'schedule', 'text-warning'],
    ['partial', 'Parcial', 'trending_up', 'text-warning'],
    ['paid', 'Paga', 'check_circle', 'text-success'],
    ['overdue', 'Atrasada', 'error', 'text-error'],
  ] as const)('mostra %s com texto, icone e cor de estado', (status, label, icon, tone) => {
    expect(chargeStatusView(status)).toEqual({ label, icon, tone });
  });
});

describe('PAYMENT_METHODS', () => {
  it('oferece as cinco formas da API, com Pix primeiro', () => {
    expect(PAYMENT_METHODS).toEqual([
      { value: 'pix', label: 'Pix' },
      { value: 'boleto', label: 'Boleto' },
      { value: 'transfer', label: 'Transferência' },
      { value: 'cash', label: 'Dinheiro' },
      { value: 'other', label: 'Outra' },
    ]);
  });

  it('traduz a forma para o texto da tela', () => {
    expect(methodLabel('transfer')).toBe('Transferência');
    expect(methodLabel('cash')).toBe('Dinheiro');
  });
});

describe('receiptLabel', () => {
  it('diz o recibo pelo codigo', () => {
    expect(receiptLabel('REC-000123', false)).toBe('Recibo REC-000123');
  });

  it('marca o recibo do pagamento estornado como cancelado', () => {
    expect(receiptLabel('REC-000123', true)).toBe('Recibo REC-000123 · cancelado');
  });
});

describe('STATUS_FILTERS', () => {
  it('comeca por "todas" e depois traz cada situacao', () => {
    expect(STATUS_FILTERS).toEqual([
      { value: 'all', label: 'Todas as situações' },
      { value: 'open', label: 'Em aberto' },
      { value: 'partial', label: 'Parciais' },
      { value: 'paid', label: 'Pagas' },
      { value: 'overdue', label: 'Atrasadas' },
    ]);
  });
});

describe('toReceive', () => {
  it('e o cobrado menos o recebido, sem sobra de ponto flutuante', () => {
    expect(toReceive(monthPayments({ charged: 0.3, received: 0.1 }))).toBe(0.2);
    expect(toReceive(monthPayments({ charged: 2400, received: 800 }))).toBe(1600);
  });

  it('chega a zero quando tudo foi recebido', () => {
    expect(
      toReceive(
        monthPayments({ charged: 800, received: 800, items: [chargeSummary({ status: 'paid' })] }),
      ),
    ).toBe(0);
  });
});
