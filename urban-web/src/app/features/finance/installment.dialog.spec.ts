import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { flushPromises } from '../../testing/async';
import {
  alertOf,
  choiceOf,
  chooseIn,
  choicesOf,
  configureDialogTest,
  dialogOf,
  errorsOf,
  fieldOf,
  removeOverlays,
  submitButtonOf,
  submitDialog,
  typeIn,
} from '../../testing/dialog.harness';
import { installmentExpense } from '../../testing/finance.fixtures';
import { openInstallmentDialog } from './installment.dialog';

const TAG = 'urban-installment-dialog';

type Save = jest.Mock<(number: number, amount: number) => Promise<string | null>>;

function setup(month = '2026-12'): { save: Save; result: Promise<boolean> } {
  configureDialogTest();
  const save: Save = jest
    .fn<(number: number, amount: number) => Promise<string | null>>()
    .mockResolvedValue(null);
  const result = openInstallmentDialog(TestBed.inject(Injector), {
    expense: installmentExpense(),
    month,
    save,
  });
  TestBed.tick();
  return { save, result };
}

describe('InstallmentDialog', () => {
  afterEach(removeOverlays);

  it('lista as parcelas com mes e valor e escolhe a primeira que ainda vem', () => {
    setup('2026-12');

    const labels = choicesOf(TAG, 'installment').map((option) => option.label);

    expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe(
      'Alterar parcela de Elevador',
    );
    expect(labels).toHaveLength(6);
    expect(labels[0]).toBe('Parcela 1 de 6 · 11/2026 · R$ 166,66');
    expect(labels[5]).toBe('Parcela 6 de 6 · 04/2027 · R$ 166,70');
    expect(choiceOf(TAG, 'installment')).toBe('2');
    expect(fieldOf(TAG, 'amount').value).toBe('166,66');
  });

  it('escolhe a ultima parcela quando todas ja passaram', () => {
    setup('2027-09');

    expect(choiceOf(TAG, 'installment')).toBe('6');
    expect(fieldOf(TAG, 'amount').value).toBe('166,70');
  });

  it('troca o valor mostrado quando a parcela muda e so habilita o botao com valor diferente', () => {
    setup();

    expect(submitButtonOf(TAG).disabled).toBe(true);

    chooseIn(TAG, 'installment', '6');
    TestBed.tick();

    expect(fieldOf(TAG, 'amount').value).toBe('166,70');
    expect(submitButtonOf(TAG).disabled).toBe(true);

    typeIn(TAG, 'amount', '170');
    TestBed.tick();

    expect(submitButtonOf(TAG).disabled).toBe(false);
  });

  it('grava a parcela e o valor escolhidos e fecha', async () => {
    const { save, result } = setup();

    chooseIn(TAG, 'installment', '3');
    typeIn(TAG, 'amount', '120,50');
    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledWith(3, 120.5);
    expect(await result).toBe(true);
  });

  it('nao grava valor invalido', () => {
    const { save } = setup();

    typeIn(TAG, 'amount', '0');
    submitDialog(TAG);
    TestBed.tick();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual(['Informe um valor maior que zero, com até 2 casas decimais']);
  });

  it('continua aberto com o erro quando a gravacao e recusada', async () => {
    const { save } = setup();
    save.mockResolvedValue('Esta parcela não existe mais');

    typeIn(TAG, 'amount', '99');
    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();

    expect(alertOf(TAG)).toBe('Esta parcela não existe mais');
  });

  it('cancelar fecha sem gravar', async () => {
    const { save, result } = setup();

    dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

    expect(await result).toBe(false);
    expect(save).not.toHaveBeenCalled();
  });
});
