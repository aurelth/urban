import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { flushPromises } from '../../testing/async';
import {
  alertOf,
  choiceOf,
  choicesOf,
  chooseIn,
  configureDialogTest,
  dialogOf,
  errorsOf,
  fieldOf,
  removeOverlays,
  submitDialog,
  typeIn,
} from '../../testing/dialog.harness';
import { expense } from '../../testing/finance.fixtures';
import { openExpenseAmountDialog } from './expense-amount.dialog';

const TAG = 'urban-expense-amount-dialog';

type Save = jest.Mock<(fromMonth: string, amount: number) => Promise<string | null>>;

function setup(): { save: Save; result: Promise<boolean> } {
  configureDialogTest();
  const save: Save = jest
    .fn<(fromMonth: string, amount: number) => Promise<string | null>>()
    .mockResolvedValue(null);
  const result = openExpenseAmountDialog(TestBed.inject(Injector), {
    expense: expense({ description: 'Portaria', startMonth: '2026-09', currentAmount: 4200 }),
    month: '2026-11',
    save,
  });
  TestBed.tick();
  return { save, result };
}

describe('ExpenseAmountDialog', () => {
  afterEach(removeOverlays);

  it('mostra o valor em vigor e sugere valer a partir do mes atual', () => {
    setup();

    expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Reajustar Portaria');
    expect(fieldOf(TAG, 'amount').value).toBe('4200,00');
    expect(choiceOf(TAG, 'fromMonth')).toBe('2026-11');
  });

  it('oferece os meses desde o inicio da despesa ate dois anos a frente', () => {
    setup();

    const months = choicesOf(TAG, 'fromMonth').map((option) => option.value);

    expect(months[0]).toBe('2026-09');
    expect(months.at(-1)).toBe('2028-11');
    expect(months).toHaveLength(27);
  });

  it('grava o mes e o valor escolhidos e fecha', async () => {
    const { save, result } = setup();

    chooseIn(TAG, 'fromMonth', '2027-01');
    typeIn(TAG, 'amount', '4.400,00');
    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledWith('2027-01', 4400);
    expect(await result).toBe(true);
  });

  it('nao grava valor invalido', () => {
    const { save } = setup();

    typeIn(TAG, 'amount', 'abc');
    submitDialog(TAG);
    TestBed.tick();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual(['Informe um valor maior que zero, com até 2 casas decimais']);
  });

  it('continua aberto com o erro quando a gravacao e recusada', async () => {
    const { save } = setup();
    save.mockResolvedValue('Só despesas fixas mensais têm reajuste de valor');

    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();

    expect(alertOf(TAG)).toBe('Só despesas fixas mensais têm reajuste de valor');
  });

  it('cancelar fecha sem gravar', async () => {
    const { save, result } = setup();

    dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

    expect(await result).toBe(false);
    expect(save).not.toHaveBeenCalled();
  });
});
