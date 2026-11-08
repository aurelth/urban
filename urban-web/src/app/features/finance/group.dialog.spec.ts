import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { ExpenseGroup, ExpenseGroupValues } from '../../core/finance/finance.models';
import { flushPromises } from '../../testing/async';
import {
  alertOf,
  checkboxOf,
  configureDialogTest,
  dialogOf,
  errorsOf,
  fieldOf,
  removeOverlays,
  submitButtonOf,
  submitDialog,
  toggleIn,
  typeIn,
} from '../../testing/dialog.harness';
import { expenseGroup } from '../../testing/finance.fixtures';
import { GroupDialogConfig, openGroupDialog } from './group.dialog';

const TAG = 'urban-group-dialog';

type Save = jest.Mock<(values: ExpenseGroupValues) => Promise<string | null>>;

const CHECKBOX = 'reserve-fund-base';

function setup(group: ExpenseGroup | null = null): { save: Save; result: Promise<boolean> } {
  configureDialogTest();
  const save: Save = jest
    .fn<(values: ExpenseGroupValues) => Promise<string | null>>()
    .mockResolvedValue(null);
  const config: GroupDialogConfig = { group, save };
  const result = openGroupDialog(TestBed.inject(Injector), config);
  TestBed.tick();
  return { save, result };
}

describe('GroupDialog', () => {
  afterEach(removeOverlays);

  describe('novo grupo', () => {
    it('mostra o titulo e o botao de criar', () => {
      setup();

      expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Novo grupo');
      expect(submitButtonOf(TAG).textContent?.trim()).toBe('Criar grupo');
      expect(fieldOf(TAG, 'name').value).toBe('');
    });

    it('nao grava com o nome vazio e explica o que falta', () => {
      const { save } = setup();

      submitDialog(TAG);
      TestBed.tick();

      expect(save).not.toHaveBeenCalled();
      expect(errorsOf(TAG)).toEqual(['Informe o nome do grupo']);
    });

    it('recusa nome com mais de 60 caracteres', () => {
      const { save } = setup();

      typeIn(TAG, 'name', 'x'.repeat(61));
      submitDialog(TAG);
      TestBed.tick();

      expect(save).not.toHaveBeenCalled();
      expect(errorsOf(TAG)).toEqual(['O nome pode ter até 60 caracteres']);
    });

    it('grava o nome sem espacos nas pontas e fecha', async () => {
      const { save, result } = setup();

      typeIn(TAG, 'name', '  Obras  ');
      submitDialog(TAG);
      await flushPromises();

      expect(save).toHaveBeenCalledWith({ name: 'Obras', reserveFundBase: false });
      expect(await result).toBe(true);
      expect(document.querySelector(TAG)).toBeNull();
    });

    it('oferece a marca de base do fundo de reserva, desmarcada, com a explicacao', () => {
      setup();

      const box = dialogOf(TAG).querySelector(`[data-testid="${CHECKBOX}"]`)!;
      expect(box.textContent).toContain('Entra na base do fundo de reserva');
      expect(checkboxOf(TAG, CHECKBOX).checked).toBe(false);
      expect(dialogOf(TAG).textContent).toContain(
        'O percentual do fundo de reserva é calculado sobre os grupos marcados',
      );
    });

    it('cria o grupo ja marcado como base do fundo de reserva', async () => {
      const { save } = setup();

      typeIn(TAG, 'name', 'Contratos');
      toggleIn(TAG, CHECKBOX);
      submitDialog(TAG);
      await flushPromises();

      expect(save).toHaveBeenCalledWith({ name: 'Contratos', reserveFundBase: true });
    });

    it('continua aberto com o erro quando a gravacao e recusada', async () => {
      const { save } = setup();
      save.mockResolvedValue('Já existe um grupo com este nome');

      typeIn(TAG, 'name', 'Obras');
      submitDialog(TAG);
      await flushPromises();
      TestBed.tick();

      expect(alertOf(TAG)).toBe('Já existe um grupo com este nome');
      expect(fieldOf(TAG, 'name').value).toBe('Obras');
    });

    it('cancelar fecha sem gravar', async () => {
      const { save, result } = setup();

      dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

      expect(await result).toBe(false);
      expect(save).not.toHaveBeenCalled();
    });
  });

  describe('editar', () => {
    it('mostra o nome e a marca atuais e so habilita o botao depois de mudar', () => {
      setup(expenseGroup({ name: 'Obras', reserveFundBase: true }));

      expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Editar grupo');
      expect(fieldOf(TAG, 'name').value).toBe('Obras');
      expect(checkboxOf(TAG, CHECKBOX).checked).toBe(true);
      expect(submitButtonOf(TAG).textContent?.trim()).toBe('Salvar alterações');
      expect(submitButtonOf(TAG).disabled).toBe(true);

      typeIn(TAG, 'name', 'Reformas');
      TestBed.tick();

      expect(submitButtonOf(TAG).disabled).toBe(false);
    });

    it('habilita o botao so por mudar a marca e desabilita de novo ao voltar', () => {
      setup(expenseGroup({ name: 'Obras', reserveFundBase: false }));

      toggleIn(TAG, CHECKBOX);
      TestBed.tick();
      expect(submitButtonOf(TAG).disabled).toBe(false);

      toggleIn(TAG, CHECKBOX);
      TestBed.tick();
      expect(submitButtonOf(TAG).disabled).toBe(true);
    });

    it('grava o nome e a marca juntos', async () => {
      const { save, result } = setup(expenseGroup({ name: 'Obras', reserveFundBase: false }));

      typeIn(TAG, 'name', 'Reformas');
      toggleIn(TAG, CHECKBOX);
      submitDialog(TAG);
      await flushPromises();

      expect(save).toHaveBeenCalledWith({ name: 'Reformas', reserveFundBase: true });
      expect(await result).toBe(true);
    });

    it('grava so a marca quando o nome continua o mesmo', async () => {
      const { save } = setup(expenseGroup({ name: 'Obras', reserveFundBase: true }));

      toggleIn(TAG, CHECKBOX);
      submitDialog(TAG);
      await flushPromises();

      expect(save).toHaveBeenCalledWith({ name: 'Obras', reserveFundBase: false });
    });

    it('nao grava quando nada mudou', async () => {
      const { save } = setup(expenseGroup({ name: 'Obras' }));

      submitDialog(TAG);
      await flushPromises();

      expect(save).not.toHaveBeenCalled();
    });
  });
});
