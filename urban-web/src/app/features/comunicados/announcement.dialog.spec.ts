import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { Announcement, AnnouncementPayload } from '../../core/announcements/announcements.models';
import { Unit } from '../../core/units/units.models';
import { announcement } from '../../testing/announcements.fixtures';
import { flushPromises } from '../../testing/async';
import {
  alertOf,
  choiceOf,
  choicesOf,
  checkboxOf,
  chooseIn,
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
import { unit } from '../../testing/units.fixtures';
import { openAnnouncementDialog } from './announcement.dialog';

const TAG = 'urban-announcement-dialog';

type Save = jest.Mock<(payload: AnnouncementPayload) => Promise<string | null>>;

const FIRST = unit({ block: 'A', number: '101' });
const SECOND = unit({ block: 'B', number: '202' });
const THIRD = unit({ block: 'A', number: '102' });
const HOUSE = unit({ block: null, number: '12' });
const UNITS: readonly Unit[] = [FIRST, SECOND, THIRD, HOUSE];

function setup(
  existing: Announcement | null = null,
  units: readonly Unit[] = UNITS,
): { save: Save; result: Promise<boolean> } {
  configureDialogTest();
  const save: Save = jest
    .fn<(payload: AnnouncementPayload) => Promise<string | null>>()
    .mockResolvedValue(null);
  const result = openAnnouncementDialog(TestBed.inject(Injector), {
    announcement: existing,
    units,
    save,
  });
  TestBed.tick();
  return { save, result };
}

function radio(scope: 'condominium' | 'block' | 'unit'): HTMLInputElement {
  return dialogOf(TAG).querySelector<HTMLInputElement>(`input[type="radio"][value="${scope}"]`)!;
}

function chooseScope(scope: 'condominium' | 'block' | 'unit'): void {
  radio(scope).click();
  TestBed.tick();
}

function counter(): string {
  return dialogOf(TAG).querySelector('[data-testid="body-counter"]')?.textContent?.trim() ?? '';
}

async function settle(): Promise<void> {
  await flushPromises();
  TestBed.tick();
  await flushPromises();
  TestBed.tick();
}

async function submit(): Promise<void> {
  submitDialog(TAG);
  await settle();
}

describe('openAnnouncementDialog', () => {
  afterEach(removeOverlays);

  describe('novo comunicado', () => {
    it('abre em branco, para o condomínio inteiro, como geral e com o botão de publicar', () => {
      setup();

      expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Novo comunicado');
      expect(fieldOf(TAG, 'title').value).toBe('');
      expect(fieldOf(TAG, 'body').value).toBe('');
      expect(choiceOf(TAG, 'category')).toBe('general');
      expect(radio('condominium').checked).toBe(true);
      expect(submitButtonOf(TAG).textContent?.trim()).toBe('Publicar comunicado');
      expect(submitButtonOf(TAG).disabled).toBe(false);
      expect(dialogOf(TAG).querySelector('[data-testid="cancel"]')?.textContent?.trim()).toBe(
        'Cancelar',
      );
    });

    it('oferece as três categorias', () => {
      setup();

      expect(choicesOf(TAG, 'category').map((option) => option.label)).toEqual([
        'Geral',
        'Manutenção',
        'Importante',
      ]);
    });

    it('oferece as três formas de alcance e mostra só o campo da escolhida', () => {
      setup();

      const text = dialogOf(TAG).textContent!;
      expect(text).toContain('Condomínio inteiro');
      expect(text).toContain('Um bloco');
      expect(text).toContain('Uma unidade');
      expect(fieldOf(TAG, 'block')).toBeNull();
      expect(fieldOf(TAG, 'unitId')).toBeNull();

      chooseScope('block');
      expect(fieldOf(TAG, 'block')).not.toBeNull();
      expect(fieldOf(TAG, 'unitId')).toBeNull();

      chooseScope('unit');
      expect(fieldOf(TAG, 'block')).toBeNull();
      expect(fieldOf(TAG, 'unitId')).not.toBeNull();

      chooseScope('condominium');
      expect(fieldOf(TAG, 'block')).toBeNull();
      expect(fieldOf(TAG, 'unitId')).toBeNull();
    });

    it('lista os blocos sem repetir e em ordem, e as unidades com o bloco', () => {
      setup();

      chooseScope('block');
      expect(choicesOf(TAG, 'block').map((option) => [option.value, option.label])).toEqual([
        ['', 'Escolha o bloco'],
        ['A', 'Bloco A'],
        ['B', 'Bloco B'],
      ]);

      chooseScope('unit');
      expect(choicesOf(TAG, 'unitId').map((option) => [option.value, option.label])).toEqual([
        ['', 'Escolha a unidade'],
        [FIRST.id, 'Bloco A · 101'],
        [SECOND.id, 'Bloco B · 202'],
        [THIRD.id, 'Bloco A · 102'],
        [HOUSE.id, '12'],
      ]);
    });

    it('não deixa escolher bloco quando nenhuma unidade tem bloco', () => {
      setup(null, [HOUSE]);

      expect(radio('block').disabled).toBe(true);
      expect(radio('unit').disabled).toBe(false);
      expect(radio('condominium').disabled).toBe(false);
    });

    it('não deixa escolher bloco nem unidade quando não há unidade ativa', () => {
      setup(null, []);

      expect(radio('block').disabled).toBe(true);
      expect(radio('unit').disabled).toBe(true);
    });

    it('exige o título e o texto e não chama a API sem eles', async () => {
      const { save } = setup();

      await submit();

      expect(errorsOf(TAG)).toEqual(
        expect.arrayContaining(['Informe o título', 'Informe o texto']),
      );
      expect(save).not.toHaveBeenCalled();
    });

    it('trata título só de espaços como em branco', async () => {
      const { save } = setup();

      typeIn(TAG, 'title', '   ');
      typeIn(TAG, 'body', 'Texto');
      await submit();

      expect(errorsOf(TAG)).toEqual(['Informe o título']);
      expect(save).not.toHaveBeenCalled();
    });

    it('limita o título a 120 e o texto a 5000 caracteres', async () => {
      const { save } = setup();

      typeIn(TAG, 'title', 'T'.repeat(121));
      typeIn(TAG, 'body', 'x'.repeat(5001));
      await submit();

      expect(errorsOf(TAG)).toEqual(
        expect.arrayContaining([
          'O título pode ter até 120 caracteres',
          'O texto pode ter até 5000 caracteres',
        ]),
      );
      expect(save).not.toHaveBeenCalled();
    });

    it('aceita título de 120 e texto de 5000 caracteres', async () => {
      const { save } = setup();

      typeIn(TAG, 'title', 'T'.repeat(120));
      typeIn(TAG, 'body', 'x'.repeat(5000));
      await submit();

      expect(save).toHaveBeenCalledTimes(1);
    });

    it('conta os caracteres do texto, sem os espaços das pontas', () => {
      setup();
      expect(counter()).toBe('0 de 5000 caracteres');

      typeIn(TAG, 'body', '  Olá, vizinhos  ');
      TestBed.tick();

      expect(counter()).toBe('13 de 5000 caracteres');
    });

    it('publica para o condomínio inteiro, com o texto aparado e bloco e unidade nulos', async () => {
      const { save } = setup();

      typeIn(TAG, 'title', ' Reunião do condomínio ');
      typeIn(TAG, 'body', ' Sábado às 10h no salão.\nTraga a sua opinião. ');
      await submit();

      expect(save).toHaveBeenCalledWith({
        title: 'Reunião do condomínio',
        body: 'Sábado às 10h no salão.\nTraga a sua opinião.',
        category: 'general',
        scope: 'condominium',
        block: null,
        unitId: null,
        notifyByEmail: false,
      });
    });

    it('publica para um bloco, na categoria escolhida', async () => {
      const { save } = setup();

      typeIn(TAG, 'title', 'Limpeza da caixa d’água');
      typeIn(TAG, 'body', 'Sem água das 8h às 12h.');
      chooseIn(TAG, 'category', 'maintenance');
      chooseScope('block');
      chooseIn(TAG, 'block', 'B');
      await submit();

      expect(save).toHaveBeenCalledWith({
        title: 'Limpeza da caixa d’água',
        body: 'Sem água das 8h às 12h.',
        category: 'maintenance',
        scope: 'block',
        block: 'B',
        unitId: null,
        notifyByEmail: false,
      });
    });

    it('publica para uma unidade', async () => {
      const { save } = setup();

      typeIn(TAG, 'title', 'Encomenda na portaria');
      typeIn(TAG, 'body', 'Retire até sexta.');
      chooseScope('unit');
      chooseIn(TAG, 'unitId', SECOND.id);
      await submit();

      expect(save).toHaveBeenCalledWith({
        title: 'Encomenda na portaria',
        body: 'Retire até sexta.',
        category: 'general',
        scope: 'unit',
        block: null,
        unitId: SECOND.id,
        notifyByEmail: false,
      });
    });

    describe('aviso por e-mail', () => {
      function emailBox(): HTMLInputElement {
        return checkboxOf(TAG, 'notify-email');
      }

      async function publish(): Promise<void> {
        typeIn(TAG, 'title', 'Aviso');
        typeIn(TAG, 'body', 'Texto');
        await submit();
      }

      it('oferece "Avisar também por e-mail", desmarcado na categoria geral', () => {
        setup();

        expect(dialogOf(TAG).querySelector('[data-testid="notify-email"]')?.textContent).toContain(
          'Avisar também por e-mail',
        );
        expect(emailBox().checked).toBe(false);
      });

      it('já vem marcado quando a categoria é importante e desmarcado nas outras', () => {
        setup();

        chooseIn(TAG, 'category', 'important');
        TestBed.tick();
        expect(emailBox().checked).toBe(true);

        chooseIn(TAG, 'category', 'maintenance');
        TestBed.tick();
        expect(emailBox().checked).toBe(false);
      });

      it('publica pedindo o e-mail quando o síndico marca a caixa', async () => {
        const { save } = setup();

        toggleIn(TAG, 'notify-email');
        TestBed.tick();
        await publish();

        expect(save).toHaveBeenCalledWith(expect.objectContaining({ notifyByEmail: true }));
      });

      it('publica pedindo o e-mail quando a categoria é importante e ele não mexeu na caixa', async () => {
        const { save } = setup();

        chooseIn(TAG, 'category', 'important');
        TestBed.tick();
        await publish();

        expect(save).toHaveBeenCalledWith(
          expect.objectContaining({ category: 'important', notifyByEmail: true }),
        );
      });

      it('respeita a escolha do síndico: desmarcou num importante, mudar a categoria não marca de novo', async () => {
        const { save } = setup();

        chooseIn(TAG, 'category', 'important');
        TestBed.tick();
        toggleIn(TAG, 'notify-email');
        TestBed.tick();
        chooseIn(TAG, 'category', 'general');
        TestBed.tick();
        chooseIn(TAG, 'category', 'important');
        TestBed.tick();
        expect(emailBox().checked).toBe(false);
        await publish();

        expect(save).toHaveBeenCalledWith(expect.objectContaining({ notifyByEmail: false }));
      });

      it('na edição não há o que marcar, porque o e-mail sai só ao publicar, e o corpo não leva o pedido', async () => {
        const { save } = setup(announcement({ title: 'Antigo', notifiedByEmail: true }));

        expect(dialogOf(TAG).querySelector('[data-testid="notify-email"]')).toBeNull();
        typeIn(TAG, 'title', 'Novo');
        TestBed.tick();
        await submit();

        expect(save).toHaveBeenCalledTimes(1);
        expect(save.mock.calls[0][0]).not.toHaveProperty('notifyByEmail');
      });
    });

    it('pede o bloco quando o alcance é um bloco e nenhum foi escolhido', async () => {
      const { save } = setup();

      typeIn(TAG, 'title', 'Aviso');
      typeIn(TAG, 'body', 'Texto');
      chooseScope('block');
      await submit();

      expect(errorsOf(TAG)).toEqual(['Escolha o bloco']);
      expect(save).not.toHaveBeenCalled();
    });

    it('pede a unidade quando o alcance é uma unidade e nenhuma foi escolhida', async () => {
      const { save } = setup();

      typeIn(TAG, 'title', 'Aviso');
      typeIn(TAG, 'body', 'Texto');
      chooseScope('unit');
      await submit();

      expect(errorsOf(TAG)).toEqual(['Escolha a unidade']);
      expect(save).not.toHaveBeenCalled();
    });

    it('esquece o bloco escolhido quando o alcance volta para o condomínio inteiro', async () => {
      const { save } = setup();

      typeIn(TAG, 'title', 'Aviso');
      typeIn(TAG, 'body', 'Texto');
      chooseScope('block');
      chooseIn(TAG, 'block', 'A');
      chooseScope('condominium');
      await submit();

      expect(save).toHaveBeenCalledWith(
        expect.objectContaining({ scope: 'condominium', block: null, unitId: null }),
      );
    });

    it('fecha com sucesso quando a gravação dá certo', async () => {
      const { result } = setup();

      typeIn(TAG, 'title', 'Aviso');
      typeIn(TAG, 'body', 'Texto');
      await submit();

      expect(dialogOf(TAG)).toBeNull();
      await expect(result).resolves.toBe(true);
    });

    it('trava o botão e troca o texto enquanto grava', async () => {
      const { save } = setup();
      let finish: (message: string | null) => void = () => undefined;
      save.mockReturnValue(new Promise<string | null>((resolve) => (finish = resolve)));

      typeIn(TAG, 'title', 'Aviso');
      typeIn(TAG, 'body', 'Texto');
      submitDialog(TAG);
      TestBed.tick();

      expect(submitButtonOf(TAG).disabled).toBe(true);
      expect(submitButtonOf(TAG).textContent?.trim()).toBe('Salvando');
      expect(
        dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')?.disabled,
      ).toBe(true);
      finish(null);
      await settle();
    });

    it('mostra o erro da API, continua aberto e mantém o que foi digitado', async () => {
      const { save, result } = setup();
      save.mockResolvedValue('Este bloco não tem unidades cadastradas');
      const closed = jest.fn();
      void result.then(closed);

      typeIn(TAG, 'title', 'Aviso');
      typeIn(TAG, 'body', 'Texto do aviso');
      chooseScope('block');
      chooseIn(TAG, 'block', 'A');
      await submit();

      expect(alertOf(TAG)).toBe('Este bloco não tem unidades cadastradas');
      expect(dialogOf(TAG)).not.toBeNull();
      expect(fieldOf(TAG, 'title').value).toBe('Aviso');
      expect(fieldOf(TAG, 'body').value).toBe('Texto do aviso');
      expect(choiceOf(TAG, 'block')).toBe('A');
      expect(submitButtonOf(TAG).disabled).toBe(false);
      expect(closed).not.toHaveBeenCalled();
    });

    it('apaga o erro antigo ao tentar de novo', async () => {
      const { save } = setup();
      save.mockResolvedValueOnce('Falhou').mockResolvedValueOnce(null);
      typeIn(TAG, 'title', 'Aviso');
      typeIn(TAG, 'body', 'Texto');
      await submit();
      expect(alertOf(TAG)).toBe('Falhou');

      submitDialog(TAG);
      TestBed.tick();

      expect(alertOf(TAG)).toBe('');
      await settle();
    });

    it('cancelar fecha sem gravar', async () => {
      const { save, result } = setup();

      dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

      await expect(result).resolves.toBe(false);
      expect(save).not.toHaveBeenCalled();
    });
  });

  describe('editar comunicado', () => {
    const existing = announcement({
      title: 'Água cortada',
      body: 'Na terça, das 8h às 12h.',
      category: 'maintenance',
      scope: 'block',
      block: 'B',
      version: '41',
    });

    it('abre preenchido, com o alcance e o título de edição', () => {
      setup(existing);

      expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Editar comunicado');
      expect(fieldOf(TAG, 'title').value).toBe('Água cortada');
      expect(fieldOf(TAG, 'body').value).toBe('Na terça, das 8h às 12h.');
      expect(choiceOf(TAG, 'category')).toBe('maintenance');
      expect(radio('block').checked).toBe(true);
      expect(choiceOf(TAG, 'block')).toBe('B');
      expect(submitButtonOf(TAG).textContent?.trim()).toBe('Salvar alterações');
    });

    it('abre com a unidade escolhida quando o comunicado é de uma unidade', () => {
      setup(announcement({ scope: 'unit', unitId: SECOND.id }));

      expect(radio('unit').checked).toBe(true);
      expect(choiceOf(TAG, 'unitId')).toBe(SECOND.id);
    });

    it('só habilita o botão depois de alguma mudança', () => {
      setup(existing);
      expect(submitButtonOf(TAG).disabled).toBe(true);

      typeIn(TAG, 'title', 'Água cortada na terça');
      TestBed.tick();
      expect(submitButtonOf(TAG).disabled).toBe(false);

      typeIn(TAG, 'title', 'Água cortada');
      TestBed.tick();
      expect(submitButtonOf(TAG).disabled).toBe(true);
    });

    it('espaços nas pontas não contam como mudança', () => {
      setup(existing);

      typeIn(TAG, 'title', '  Água cortada ');
      TestBed.tick();

      expect(submitButtonOf(TAG).disabled).toBe(true);
    });

    it('mudar o alcance conta como mudança', () => {
      setup(existing);

      chooseScope('condominium');

      expect(submitButtonOf(TAG).disabled).toBe(false);
    });

    it('grava o que mudou e fecha', async () => {
      const { save, result } = setup(existing);

      typeIn(TAG, 'body', 'Na quarta, das 8h às 12h.');
      TestBed.tick();
      await submit();

      expect(save).toHaveBeenCalledWith({
        title: 'Água cortada',
        body: 'Na quarta, das 8h às 12h.',
        category: 'maintenance',
        scope: 'block',
        block: 'B',
        unitId: null,
      });
      await expect(result).resolves.toBe(true);
    });

    it('não chama a API sem mudança, mesmo se o formulário for enviado', async () => {
      const { save } = setup(existing);

      await submit();

      expect(save).not.toHaveBeenCalled();
    });
  });
});
