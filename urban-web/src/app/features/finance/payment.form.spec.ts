import { FormControl } from '@angular/forms';
import { amountUpTo, paymentDate, registerPayload, todayText } from './payment.form';

const NOW = (): Date => new Date('2026-11-15T15:00:00Z');

function check(validator: ReturnType<typeof paymentDate>, value: string): unknown {
  return validator(new FormControl(value, { nonNullable: true }));
}

describe('todayText', () => {
  it('usa o dia de Brasilia, nao o do UTC', () => {
    expect(todayText(new Date('2026-11-15T15:00:00Z'))).toBe('15/11/2026');
    expect(todayText(new Date('2026-11-16T01:30:00Z'))).toBe('15/11/2026');
    expect(todayText(new Date('2026-11-16T03:00:00Z'))).toBe('16/11/2026');
  });
});

describe('paymentDate', () => {
  const validator = paymentDate('2026-11', NOW);

  it.each(['15/11/2026', '01/11/2026', '01/11/2025'])('aceita %s', (value) => {
    expect(check(validator, value)).toBeNull();
  });

  it('deixa o vazio para o required', () => {
    expect(check(validator, '')).toBeNull();
  });

  it.each(['31/02/2026', '15/11', '2026-11-10'])('recusa %s como data que nao existe', (value) => {
    expect(check(validator, value)).toEqual({ date: true });
  });

  it('recusa data no futuro, pelo dia de Brasilia', () => {
    expect(check(validator, '16/11/2026')).toEqual({ future: true });
    expect(
      check(
        paymentDate('2026-11', () => new Date('2026-11-16T01:00:00Z')),
        '16/11/2026',
      ),
    ).toEqual({ future: true });
  });

  it('recusa data anterior a 12 meses antes da competencia', () => {
    expect(check(validator, '31/10/2025')).toEqual({ tooOld: true });
    expect(check(paymentDate('2026-03', NOW), '28/02/2025')).toEqual({ tooOld: true });
    expect(check(paymentDate('2026-03', NOW), '01/03/2025')).toBeNull();
  });
});

describe('amountUpTo', () => {
  const validator = amountUpTo(300);

  it('aceita ate o saldo, inclusive', () => {
    expect(check(validator, '300,00')).toBeNull();
    expect(check(validator, '12,50')).toBeNull();
  });

  it('recusa o que passa do saldo', () => {
    expect(check(validator, '300,01')).toEqual({ exceeds: true });
  });

  it('deixa valor vazio ou ilegivel para o validador de dinheiro', () => {
    expect(check(validator, '')).toBeNull();
    expect(check(validator, 'abc')).toBeNull();
  });
});

describe('registerPayload', () => {
  it('monta o corpo da API: valor, data em AAAA-MM-DD, forma e observacao aparada', () => {
    expect(
      registerPayload({
        amount: '1.250,50',
        receivedOn: '10/11/2026',
        method: 'boleto',
        note: '  Pagou no caixa  ',
      }),
    ).toEqual({
      amount: 1250.5,
      receivedOn: '2026-11-10',
      method: 'boleto',
      note: 'Pagou no caixa',
    });
  });

  it('manda a observacao em branco como nula', () => {
    expect(
      registerPayload({ amount: '10', receivedOn: '10/11/2026', method: 'pix', note: '   ' }).note,
    ).toBeNull();
  });
});
