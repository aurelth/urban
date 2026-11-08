import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { ExpenseEntry } from '../../core/finance/finance.models';
import { flushPromises } from '../../testing/async';
import {
  alertOf,
  configureDialogTest,
  dialogOf,
  errorsOf,
  fieldOf,
  removeOverlays,
  submitButtonOf,
  submitDialog,
  typeIn,
} from '../../testing/dialog.harness';
import { entry } from '../../testing/finance.fixtures';
import { openEntryAmountDialog } from './entry-amount.dialog';

const TAG = 'urban-entry-amount-dialog';

type Save = jest.Mock<(amount: number) => Promise<string | null>>;

function setup(target: ExpenseEntry): { save: Save; result: Promise<boolean> } {
  configureDialogTest();
  const save: Save = jest.fn<(amount: number) => Promise<string | null>>().mockResolvedValue(null);
  const result = openEntryAmountDialog(TestBed.inject(Injector), { entry: target, save });
  TestBed.tick();
  return { save, result };
}

describe('EntryAmountDialog', () => {
  afterEach(removeOverlays);

  describe('lancamento pendente', () => {
    const pending = entry({
      description: 'Água',
      amount: null,
      needsAmount: true,
      suggestedAmount: 812.34,
    });

    it('sugere o valor do mes anterior e deixa confirmar de uma vez', async () => {
      const { save, result } = setup(pending);

      expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Valor de Água');
      expect(fieldOf(TAG, 'amount').value).toBe('812,34');
      expect(dialogOf(TAG).textContent).toContain('Sugestão: o valor do mês anterior');
      expect(submitButtonOf(TAG).textContent?.trim()).toBe('Informar valor');

      submitDialog(TAG);
      await flushPromises();

      expect(save).toHaveBeenCalledWith(812.34);
      expect(await result).toBe(true);
    });

    it('comeca vazio quando nao ha sugestao', () => {
      setup(entry({ amount: null, needsAmount: true, suggestedAmount: null }));

      expect(fieldOf(TAG, 'amount').value).toBe('');
      expect(dialogOf(TAG).textContent).not.toContain('Sugestão');
    });
  });

  describe('lancamento com valor', () => {
    it('mostra o valor atual e so habilita o botao depois de mudar', () => {
      setup(entry({ amount: 4200 }));

      expect(fieldOf(TAG, 'amount').value).toBe('4200,00');
      expect(submitButtonOf(TAG).textContent?.trim()).toBe('Salvar valor');
      expect(submitButtonOf(TAG).disabled).toBe(true);

      typeIn(TAG, 'amount', '4.300,00');
      TestBed.tick();

      expect(submitButtonOf(TAG).disabled).toBe(false);
    });
  });

  it('nao grava valor invalido e explica', () => {
    const { save } = setup(entry({ amount: null, needsAmount: true }));

    typeIn(TAG, 'amount', '0');
    submitDialog(TAG);
    TestBed.tick();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual(['Informe um valor maior que zero, com até 2 casas decimais']);
  });

  it('continua aberto com o erro quando a gravacao e recusada', async () => {
    const { save } = setup(entry({ amount: null, needsAmount: true }));
    save.mockResolvedValue('Este lançamento foi alterado em outro lugar');

    typeIn(TAG, 'amount', '10');
    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();

    expect(alertOf(TAG)).toBe('Este lançamento foi alterado em outro lugar');
    expect(fieldOf(TAG, 'amount').value).toBe('10');
  });

  it('cancelar fecha sem gravar', async () => {
    const { save, result } = setup(entry({ amount: null, needsAmount: true }));

    dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

    expect(await result).toBe(false);
    expect(save).not.toHaveBeenCalled();
  });
});
