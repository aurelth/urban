import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { flushPromises } from '../../testing/async';
import {
  alertOf,
  configureDialogTest,
  dialogOf,
  removeOverlays,
  submitButtonOf,
  submitDialog,
} from '../../testing/dialog.harness';
import { expense } from '../../testing/finance.fixtures';
import { openDeleteExpenseDialog } from './delete-expense.dialog';

const TAG = 'urban-delete-expense-dialog';

type Save = jest.Mock<() => Promise<string | null>>;

function setup(): { save: Save; result: Promise<boolean> } {
  configureDialogTest();
  const save: Save = jest.fn<() => Promise<string | null>>().mockResolvedValue(null);
  const result = openDeleteExpenseDialog(TestBed.inject(Injector), {
    expense: expense({ description: 'Portaria' }),
    save,
  });
  TestBed.tick();
  return { save, result };
}

describe('DeleteExpenseDialog', () => {
  afterEach(removeOverlays);

  it('diz qual despesa vai ser excluida e que os lancamentos somem junto', () => {
    setup();

    expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Excluir Portaria');
    expect(dialogOf(TAG).textContent).toContain('todos os lançamentos dela');
    expect(dialogOf(TAG).textContent).toContain('Encerrar');
    expect(submitButtonOf(TAG).textContent?.trim()).toBe('Excluir despesa');
  });

  it('exclui e fecha', async () => {
    const { save, result } = setup();

    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledTimes(1);
    expect(await result).toBe(true);
  });

  it('continua aberto com o erro quando a API recusa', async () => {
    const { save } = setup();
    save.mockResolvedValue('Há lançamentos pagos');

    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();

    expect(alertOf(TAG)).toBe('Há lançamentos pagos');
  });

  it('cancelar fecha sem excluir', async () => {
    const { save, result } = setup();

    dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

    expect(await result).toBe(false);
    expect(save).not.toHaveBeenCalled();
  });

  it('o botao de excluir e vermelho e sem fundo, como o de apagar tarifa', () => {
    setup();

    expect(submitButtonOf(TAG).classList.contains('urban-danger')).toBe(true);
  });
});
