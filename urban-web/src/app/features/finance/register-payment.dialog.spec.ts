import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { NOW } from '../../core/finance/clock';
import { formatMoney } from '../../core/finance/money';
import { RegisterPaymentPayload } from '../../core/finance/payments.models';
import { flushPromises } from '../../testing/async';
import {
  alertOf,
  chooseIn,
  choiceOf,
  configureDialogTest,
  dialogOf,
  errorsOf,
  fieldOf,
  removeOverlays,
  submitButtonOf,
  submitDialog,
  typeIn,
} from '../../testing/dialog.harness';
import { openRegisterPaymentDialog } from './register-payment.dialog';

const TAG = 'urban-register-payment-dialog';

type Save = jest.Mock<(payload: RegisterPaymentPayload) => Promise<string | null>>;

function setup(balance = 300): { save: Save; result: Promise<boolean> } {
  configureDialogTest();
  TestBed.configureTestingModule({
    providers: [{ provide: NOW, useValue: () => new Date('2026-11-15T15:00:00Z') }],
  });
  const save: Save = jest
    .fn<(payload: RegisterPaymentPayload) => Promise<string | null>>()
    .mockResolvedValue(null);
  const result = openRegisterPaymentDialog(TestBed.inject(Injector), {
    label: 'Bloco A · 101',
    month: '2026-11',
    balance,
    save,
  });
  TestBed.tick();
  return { save, result };
}

describe('RegisterPaymentDialog', () => {
  afterEach(removeOverlays);

  it('abre com o saldo no valor, o dia de hoje na data e Pix como forma', () => {
    setup(300);

    expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe(
      'Registrar pagamento de Bloco A · 101',
    );
    expect(fieldOf(TAG, 'amount').value).toBe('300,00');
    expect(dialogOf(TAG).textContent).toContain(`Saldo: ${formatMoney(300)}`);
    expect(fieldOf(TAG, 'receivedOn').value).toBe('15/11/2026');
    expect(choiceOf(TAG, 'method')).toBe('pix');
    expect(fieldOf(TAG, 'note').value).toBe('');
    expect(submitButtonOf(TAG).textContent?.trim()).toBe('Registrar pagamento');
  });

  it('grava o pagamento com os campos convertidos para a API e fecha com verdadeiro', async () => {
    const { save, result } = setup(300);

    typeIn(TAG, 'amount', '120,50');
    typeIn(TAG, 'receivedOn', '10/11/2026');
    chooseIn(TAG, 'method', 'cash');
    typeIn(TAG, 'note', '  Pagou no caixa ');
    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledWith({
      amount: 120.5,
      receivedOn: '2026-11-10',
      method: 'cash',
      note: 'Pagou no caixa',
    });
    expect(await result).toBe(true);
  });

  it('manda a observacao em branco como nula', async () => {
    const { save } = setup(300);

    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledWith(expect.objectContaining({ amount: 300, note: null }));
  });

  it('nao grava valor invalido e explica', () => {
    const { save } = setup(300);

    typeIn(TAG, 'amount', '0');
    submitDialog(TAG);
    TestBed.tick();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual(['Informe um valor maior que zero, com até 2 casas decimais']);
  });

  it('nao grava valor acima do saldo e diz quanto falta pagar', () => {
    const { save } = setup(300);

    typeIn(TAG, 'amount', '300,01');
    submitDialog(TAG);
    TestBed.tick();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual([`O valor passa do saldo de ${formatMoney(300)}`]);
  });

  it.each([
    ['31/02/2026', 'Informe uma data que exista, como 10/11/2026'],
    ['16/11/2026', 'A data do recebimento não pode ser no futuro'],
    ['31/10/2025', 'A data do recebimento é anterior a 12 meses antes da competência (11/2026)'],
  ])('nao grava a data %s e explica', (value, message) => {
    const { save } = setup(300);

    typeIn(TAG, 'receivedOn', value);
    submitDialog(TAG);
    TestBed.tick();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual([message]);
  });

  it('exige a data', () => {
    const { save } = setup(300);

    typeIn(TAG, 'receivedOn', '');
    submitDialog(TAG);
    TestBed.tick();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual(['Informe a data em que o pagamento foi recebido']);
  });

  it('recusa observacao com mais de 200 caracteres', () => {
    const { save } = setup(300);

    typeIn(TAG, 'note', 'a'.repeat(201));
    submitDialog(TAG);
    TestBed.tick();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual(['A observação pode ter até 200 caracteres']);
  });

  it('continua aberto, com o erro e o que foi digitado, quando a gravacao e recusada', async () => {
    const { save } = setup(300);
    save.mockResolvedValue('O valor passa do saldo da cobrança');

    typeIn(TAG, 'amount', '250');
    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();

    expect(alertOf(TAG)).toBe('O valor passa do saldo da cobrança');
    expect(fieldOf(TAG, 'amount').value).toBe('250');
    expect(submitButtonOf(TAG).disabled).toBe(false);
  });

  it('cancelar fecha sem gravar', async () => {
    const { save, result } = setup(300);

    dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

    expect(await result).toBe(false);
    expect(save).not.toHaveBeenCalled();
  });
});
