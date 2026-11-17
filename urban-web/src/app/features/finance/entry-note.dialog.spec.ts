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
import { openEntryNoteDialog } from './entry-note.dialog';

const TAG = 'urban-entry-note-dialog';

type Save = jest.Mock<(note: string | null) => Promise<string | null>>;

function setup(target: ExpenseEntry): { save: Save; result: Promise<boolean> } {
  configureDialogTest();
  const save: Save = jest
    .fn<(note: string | null) => Promise<string | null>>()
    .mockResolvedValue(null);
  const result = openEntryNoteDialog(TestBed.inject(Injector), { entry: target, save });
  TestBed.tick();
  return { save, result };
}

describe('EntryNoteDialog', () => {
  afterEach(removeOverlays);

  describe('lancamento sem observacao', () => {
    it('comeca vazio, explica onde a observacao aparece e so habilita o botao depois de escrever', () => {
      setup(entry({ description: 'Portaria', note: null }));

      expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Observação de Portaria');
      expect(fieldOf(TAG, 'note').value).toBe('');
      expect(dialogOf(TAG).textContent).toContain('aparece na fatura, sob a despesa');
      expect(dialogOf(TAG).textContent).toContain('0 de 200 caracteres');
      expect(submitButtonOf(TAG).textContent?.trim()).toBe('Salvar observação');
      expect(submitButtonOf(TAG).disabled).toBe(true);

      typeIn(TAG, 'note', 'OS 4521');
      TestBed.tick();

      expect(submitButtonOf(TAG).disabled).toBe(false);
    });

    it('deixa 24 px entre a explicacao e o campo, como o dialogo de fechar o mes', () => {
      setup(entry());

      const explanation = Array.from(dialogOf(TAG).querySelectorAll('p')).find((p) =>
        p.textContent?.includes('aparece na fatura'),
      );
      expect(explanation!.classList.contains('mb-6')).toBe(true);
    });

    it('grava o texto sem os espacos das pontas e fecha', async () => {
      const { save, result } = setup(entry());

      typeIn(TAG, 'note', '  OS 4521  ');
      submitDialog(TAG);
      await flushPromises();

      expect(save).toHaveBeenCalledWith('OS 4521');
      expect(await result).toBe(true);
    });

    it('conta os caracteres sem os espacos das pontas', () => {
      setup(entry());

      typeIn(TAG, 'note', '  abc  ');
      TestBed.tick();

      expect(dialogOf(TAG).textContent).toContain('3 de 200 caracteres');
    });
  });

  describe('lancamento com observacao', () => {
    it('mostra a observacao atual e so habilita o botao depois de mudar', () => {
      setup(entry({ note: 'OS 4521' }));

      expect(fieldOf(TAG, 'note').value).toBe('OS 4521');
      expect(dialogOf(TAG).textContent).toContain('7 de 200 caracteres');
      expect(submitButtonOf(TAG).disabled).toBe(true);

      typeIn(TAG, 'note', 'OS 4522');
      TestBed.tick();

      expect(submitButtonOf(TAG).disabled).toBe(false);
    });

    it('nao grava quando o texto continua o mesmo, mesmo com espacos a mais', async () => {
      const { save } = setup(entry({ note: 'OS 4521' }));

      typeIn(TAG, 'note', ' OS 4521 ');
      TestBed.tick();
      submitDialog(TAG);
      await flushPromises();

      expect(submitButtonOf(TAG).disabled).toBe(true);
      expect(save).not.toHaveBeenCalled();
    });

    it('apagar o texto remove a observacao, enviando nulo', async () => {
      const { save, result } = setup(entry({ note: 'OS 4521' }));

      typeIn(TAG, 'note', '   ');
      submitDialog(TAG);
      await flushPromises();

      expect(save).toHaveBeenCalledWith(null);
      expect(await result).toBe(true);
    });
  });

  describe('limite de tamanho', () => {
    it('aceita 200 caracteres', async () => {
      const { save } = setup(entry());

      typeIn(TAG, 'note', 'a'.repeat(200));
      submitDialog(TAG);
      await flushPromises();

      expect(save).toHaveBeenCalledWith('a'.repeat(200));
    });

    it('nao grava com mais de 200 caracteres e explica', () => {
      const { save } = setup(entry());

      typeIn(TAG, 'note', 'a'.repeat(201));
      submitDialog(TAG);
      TestBed.tick();

      expect(save).not.toHaveBeenCalled();
      expect(errorsOf(TAG)).toEqual(['A observação pode ter até 200 caracteres, em uma linha só']);
    });
  });

  it('continua aberto com o erro quando a gravacao e recusada', async () => {
    const { save } = setup(entry());
    save.mockResolvedValue('Este lançamento foi alterado em outro lugar');

    typeIn(TAG, 'note', 'OS 4521');
    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();

    expect(alertOf(TAG)).toBe('Este lançamento foi alterado em outro lugar');
    expect(fieldOf(TAG, 'note').value).toBe('OS 4521');
  });

  it('cancelar fecha sem gravar', async () => {
    const { save, result } = setup(entry());

    dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

    expect(await result).toBe(false);
    expect(save).not.toHaveBeenCalled();
  });
});
