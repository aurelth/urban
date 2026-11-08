import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { flushPromises } from '../../testing/async';
import {
  alertOf,
  chooseIn,
  configureDialogTest,
  dialogOf,
  fieldOf,
  removeOverlays,
  submitDialog,
} from '../../testing/dialog.harness';
import { expense } from '../../testing/finance.fixtures';
import { openEndExpenseDialog } from './end-expense.dialog';

const TAG = 'urban-end-expense-dialog';

type Save = jest.Mock<(lastMonth: string) => Promise<string | null>>;

function setup(startMonth = '2026-09'): { save: Save; result: Promise<boolean> } {
  configureDialogTest();
  const save: Save = jest
    .fn<(lastMonth: string) => Promise<string | null>>()
    .mockResolvedValue(null);
  const result = openEndExpenseDialog(TestBed.inject(Injector), {
    expense: expense({ description: 'Portaria', startMonth }),
    month: '2026-11',
    save,
  });
  TestBed.tick();
  return { save, result };
}

describe('EndExpenseDialog', () => {
  afterEach(removeOverlays);

  it('explica o efeito e sugere o mes atual como ultimo mes', () => {
    setup();

    expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Encerrar Portaria');
    expect(dialogOf(TAG).textContent).toContain(
      'deixa de gerar lançamentos depois do mês escolhido',
    );
    expect(fieldOf(TAG, 'lastMonth').value).toBe('2026-11');
  });

  it('oferece os meses desde o inicio da despesa ate dois anos a frente', () => {
    setup();

    const months = [...fieldOf(TAG, 'lastMonth').options].map((option) => option.value);

    expect(months[0]).toBe('2026-09');
    expect(months.at(-1)).toBe('2028-11');
  });

  it('sugere o primeiro mes quando a despesa ainda nao comecou', () => {
    setup('2027-02');

    expect(fieldOf(TAG, 'lastMonth').value).toBe('2027-02');
  });

  it('grava o ultimo mes escolhido e fecha', async () => {
    const { save, result } = setup();

    chooseIn(TAG, 'lastMonth', '2027-03');
    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledWith('2027-03');
    expect(await result).toBe(true);
  });

  it('continua aberto com o erro quando a gravacao e recusada', async () => {
    const { save } = setup();
    save.mockResolvedValue('O mês final não pode ser anterior ao mês inicial');

    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();

    expect(alertOf(TAG)).toBe('O mês final não pode ser anterior ao mês inicial');
  });

  it('cancelar fecha sem gravar', async () => {
    const { save, result } = setup();

    dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

    expect(await result).toBe(false);
    expect(save).not.toHaveBeenCalled();
  });
});
