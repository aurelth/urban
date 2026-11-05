import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { jest } from '@jest/globals';
import { provideUrbanIcons } from '../../core/theme/icons';
import { Unit, UnitPayload } from '../../core/units/units.models';
import { flushPromises } from '../../testing/async';
import { unit } from '../../testing/units.fixtures';
import { UnitDialogConfig, openUnitDialog } from './unit.dialog';

type Save = jest.Mock<(payload: UnitPayload) => Promise<string | null>>;

function setup(existing: Unit | null = null): { save: Save; result: Promise<boolean> } {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
    ],
  });
  const save: Save = jest
    .fn<(payload: UnitPayload) => Promise<string | null>>()
    .mockResolvedValue(null);
  const config: UnitDialogConfig = { unit: existing, save };
  const result = openUnitDialog(TestBed.inject(Injector), config);
  TestBed.tick();
  return { save, result };
}

function dialog(): HTMLElement | null {
  return document.querySelector('urban-unit-dialog');
}

function field(name: string): HTMLInputElement {
  return dialog()!.querySelector<HTMLInputElement>(`[formControlName="${name}"]`)!;
}

function type(name: string, value: string): void {
  field(name).value = value;
  field(name).dispatchEvent(new Event('input'));
}

function chooseType(kind: 'apartment' | 'house'): void {
  dialog()!.querySelector<HTMLInputElement>(`input[type="radio"][value="${kind}"]`)!.click();
}

function submit(): void {
  dialog()!.querySelector('form')!.dispatchEvent(new Event('submit'));
}

function submitButton(): HTMLButtonElement {
  return dialog()!.querySelector<HTMLButtonElement>('button[type="submit"]')!;
}

function errors(): string[] {
  return [...dialog()!.querySelectorAll('mat-error')].map((error) => error.textContent!.trim());
}

function alertText(): string {
  return dialog()?.querySelector('[role="alert"]')?.textContent?.trim() ?? '';
}

async function settle(): Promise<void> {
  await flushPromises();
  TestBed.tick();
  await flushPromises();
  TestBed.tick();
}

describe('openUnitDialog', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    document.querySelectorAll('.cdk-overlay-container').forEach((overlay) => overlay.remove());
  });

  describe('nova unidade', () => {
    it('abre em branco, como apartamento, com o titulo e o botao de cadastro', () => {
      setup();

      expect(dialog()?.querySelector('h2')?.textContent?.trim()).toBe('Nova unidade');
      expect(field('block').value).toBe('');
      expect(field('number').value).toBe('');
      expect(field('idealFraction').value).toBe('');
      expect(field('area').value).toBe('');
      expect(dialog()!.querySelector<HTMLInputElement>('input[value="apartment"]')!.checked).toBe(
        true,
      );
      expect(submitButton().textContent?.trim()).toBe('Cadastrar unidade');
      expect(dialog()?.querySelector('[data-testid="cancel"]')?.textContent?.trim()).toBe(
        'Cancelar',
      );
    });

    it('explica que bloco, fracao e area sao opcionais e em que unidade se digitam', () => {
      setup();

      const text = dialog()!.textContent!;
      expect(text).toContain('Bloco');
      expect(text).toContain('Número');
      expect(text).toContain('Fração ideal (%)');
      expect(text).toContain('Área (m²)');
      expect(dialog()!.querySelectorAll('mat-hint').length).toBeGreaterThan(0);
    });

    it('exige o numero e nao chama a API sem ele', async () => {
      const { save } = setup();

      submit();
      await settle();

      expect(errors()).toContain('Informe o número da unidade');
      expect(save).not.toHaveBeenCalled();
    });

    it('envia so o que foi preenchido: bloco vazio vira nulo e a fracao vira proporcao', async () => {
      const { save } = setup();

      type('number', ' 101 ');
      submit();
      await settle();

      expect(save).toHaveBeenCalledWith({
        block: null,
        number: '101',
        type: 'apartment',
        idealFraction: null,
        area: null,
      });
    });

    it('converte a porcentagem em fracao e le a area com virgula', async () => {
      const { save } = setup();

      type('block', ' Torre 1 ');
      type('number', '1203');
      chooseType('house');
      type('idealFraction', '1,25');
      type('area', '52,5');
      submit();
      await settle();

      expect(save).toHaveBeenCalledWith({
        block: 'Torre 1',
        number: '1203',
        type: 'house',
        idealFraction: 0.0125,
        area: 52.5,
      });
    });

    it.each([
      [
        'idealFraction',
        '0',
        'Informe uma fração maior que 0% e até 100%, com até 6 casas decimais',
      ],
      [
        'idealFraction',
        '100,5',
        'Informe uma fração maior que 0% e até 100%, com até 6 casas decimais',
      ],
      [
        'idealFraction',
        'abc',
        'Informe uma fração maior que 0% e até 100%, com até 6 casas decimais',
      ],
      ['area', '0', 'Informe uma área maior que 0 e até 99.999,99 m², com até 2 casas decimais'],
      [
        'area',
        '100000',
        'Informe uma área maior que 0 e até 99.999,99 m², com até 2 casas decimais',
      ],
      [
        'area',
        '5,555',
        'Informe uma área maior que 0 e até 99.999,99 m², com até 2 casas decimais',
      ],
    ])('recusa %s "%s" mostrando o motivo', async (name, value, message) => {
      const { save } = setup();

      type('number', '101');
      type(name, value);
      submit();
      await settle();

      expect(errors()).toContain(message);
      expect(save).not.toHaveBeenCalled();
    });

    it('limita o numero a 20 e o bloco a 30 caracteres', async () => {
      const { save } = setup();

      type('number', '1'.repeat(21));
      type('block', 'B'.repeat(31));
      submit();
      await settle();

      expect(errors()).toEqual(
        expect.arrayContaining([
          'O número pode ter até 20 caracteres',
          'O bloco pode ter até 30 caracteres',
        ]),
      );
      expect(save).not.toHaveBeenCalled();
    });

    it('fecha com sucesso quando a gravacao da certo', async () => {
      const { result } = setup();

      type('number', '101');
      submit();
      await settle();

      expect(dialog()).toBeNull();
      await expect(result).resolves.toBe(true);
    });

    it('trava o botao e troca o texto enquanto grava', async () => {
      const { save } = setup();
      let finish: (message: string | null) => void = () => undefined;
      save.mockReturnValue(new Promise<string | null>((resolve) => (finish = resolve)));

      type('number', '101');
      submit();
      TestBed.tick();

      expect(submitButton().disabled).toBe(true);
      expect(submitButton().textContent?.trim()).toBe('Salvando');
      expect(dialog()?.querySelector<HTMLButtonElement>('[data-testid="cancel"]')?.disabled).toBe(
        true,
      );
      finish(null);
      await settle();
    });

    it('mostra o erro da API, continua aberto e mantem o que foi digitado', async () => {
      const { save, result } = setup();
      save.mockResolvedValue('Já existe uma unidade com este bloco e número');
      const closed = jest.fn();
      void result.then(closed);

      type('block', 'A');
      type('number', '101');
      submit();
      await settle();

      expect(alertText()).toBe('Já existe uma unidade com este bloco e número');
      expect(dialog()).not.toBeNull();
      expect(field('block').value).toBe('A');
      expect(field('number').value).toBe('101');
      expect(submitButton().disabled).toBe(false);
      expect(closed).not.toHaveBeenCalled();
    });

    it('apaga o erro antigo ao tentar de novo', async () => {
      const { save } = setup();
      save.mockResolvedValueOnce('Falhou').mockResolvedValueOnce(null);
      type('number', '101');
      submit();
      await settle();
      expect(alertText()).toBe('Falhou');

      submit();
      TestBed.tick();

      expect(alertText()).toBe('');
      await settle();
    });
  });

  describe('editar unidade', () => {
    const existing = unit({
      block: 'Torre 1',
      number: '1203',
      type: 'house',
      idealFraction: 0.0125,
      area: 1250.5,
      version: '41',
    });

    it('abre preenchida, com a fracao em porcentagem e a area com virgula', () => {
      setup(existing);

      expect(dialog()?.querySelector('h2')?.textContent?.trim()).toBe('Editar unidade');
      expect(field('block').value).toBe('Torre 1');
      expect(field('number').value).toBe('1203');
      expect(field('idealFraction').value).toBe('1,25');
      expect(field('area').value).toBe('1250,5');
      expect(dialog()!.querySelector<HTMLInputElement>('input[value="house"]')!.checked).toBe(true);
      expect(submitButton().textContent?.trim()).toBe('Salvar alterações');
    });

    it('so habilita o botao depois de alguma mudanca', () => {
      setup(existing);
      expect(submitButton().disabled).toBe(true);

      type('area', '60');
      TestBed.tick();

      expect(submitButton().disabled).toBe(false);
    });

    it('desabilita de novo se a pessoa desfaz a mudanca', () => {
      setup(existing);

      type('area', '60');
      TestBed.tick();
      type('area', '1250,5');
      TestBed.tick();

      expect(submitButton().disabled).toBe(true);
    });

    it('envia os valores corrigidos, inclusive limpar a fracao e a area', async () => {
      const { save } = setup(existing);

      type('number', '1204');
      type('idealFraction', '');
      type('area', '');
      submit();
      await settle();

      expect(save).toHaveBeenCalledWith({
        block: 'Torre 1',
        number: '1204',
        type: 'house',
        idealFraction: null,
        area: null,
      });
    });

    it('abre uma unidade sem bloco, fracao nem area com os campos vazios', () => {
      setup(unit({ block: null, idealFraction: null, area: null }));

      expect(field('block').value).toBe('');
      expect(field('idealFraction').value).toBe('');
      expect(field('area').value).toBe('');
    });
  });

  describe('cancelar', () => {
    it('fecha sem gravar e responde falso', async () => {
      const { save, result } = setup();

      type('number', '101');
      dialog()!.querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();
      await settle();

      expect(dialog()).toBeNull();
      await expect(result).resolves.toBe(false);
      expect(save).not.toHaveBeenCalled();
    });

    it('e modal e nao fecha ao clicar fora, para ninguem perder o que digitou', () => {
      setup();

      document.querySelector<HTMLElement>('.cdk-overlay-backdrop')?.click();
      TestBed.tick();

      expect(dialog()).not.toBeNull();
    });
  });
});
