import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { jest } from '@jest/globals';
import { ApportionmentMethod, UnitsSummary } from '../../core/units/units.models';
import { provideUrbanIcons } from '../../core/theme/icons';
import { unitsSummary } from '../../testing/units.fixtures';
import { ApportionmentCard } from './apportionment.card';

function setup(
  summary: UnitsSummary = unitsSummary(),
  busy = false,
): {
  fixture: ComponentFixture<ApportionmentCard>;
  html: HTMLElement;
  changes: jest.Mock<(method: ApportionmentMethod) => void>;
} {
  TestBed.configureTestingModule({
    providers: [
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
    ],
  });
  const fixture = TestBed.createComponent(ApportionmentCard);
  fixture.componentRef.setInput('summary', summary);
  fixture.componentRef.setInput('busy', busy);
  const changes = jest.fn<(method: ApportionmentMethod) => void>();
  fixture.componentInstance.methodChange.subscribe(changes);
  fixture.detectChanges();
  return { fixture, html: fixture.nativeElement, changes };
}

function radio(html: HTMLElement, method: ApportionmentMethod): HTMLInputElement {
  return html.querySelector<HTMLInputElement>(`input[type="radio"][value="${method}"]`)!;
}

function readiness(html: HTMLElement, method: ApportionmentMethod): string {
  return (
    html
      .querySelector(`[data-testid="readiness-${method}"]`)
      ?.textContent?.replace(/\s+/g, ' ')
      .trim() ?? ''
  );
}

describe('ApportionmentCard', () => {
  it('oferece os tres metodos e marca o escolhido', () => {
    const { html } = setup(unitsSummary({ apportionmentMethod: 'equalShare' }));

    expect(html.querySelector('h2')?.textContent?.trim()).toBe('Método de rateio');
    expect(html.textContent).toContain('Fração ideal');
    expect(html.textContent).toContain('Valor igual');
    expect(html.textContent).toContain('Área');
    expect(html.textContent).toContain('Cada unidade paga conforme a sua fração ideal');
    expect(html.textContent).toContain('Todas as unidades ativas pagam o mesmo valor');
    expect(html.textContent).toContain('Cada unidade paga conforme a sua área');
    expect(radio(html, 'idealFraction').checked).toBe(false);
    expect(radio(html, 'equalShare').checked).toBe(true);
    expect(radio(html, 'area').checked).toBe(false);
  });

  it('diz se cada metodo esta pronto, com texto e icone', () => {
    const { html } = setup(
      unitsSummary({
        readiness: [
          { method: 'idealFraction', ready: false },
          { method: 'equalShare', ready: true },
          { method: 'area', ready: false },
        ],
      }),
    );

    expect(readiness(html, 'equalShare')).toContain('Pronto para usar');
    expect(html.querySelector('[data-testid="readiness-equalShare"] mat-icon')).not.toBeNull();
    expect(readiness(html, 'idealFraction')).not.toContain('Pronto para usar');
    expect(html.querySelector('[data-testid="readiness-area"] mat-icon')).not.toBeNull();
  });

  it.each([
    [{ withoutIdealFraction: 1 }, '1 unidade sem fração ideal'],
    [{ withoutIdealFraction: 20 }, '20 unidades sem fração ideal'],
    [
      { withoutIdealFraction: 0, idealFractionTotal: 0.875 },
      'A soma das frações é 87,5%: ela precisa chegar a 100%',
    ],
    [
      { active: 0, apartments: 0, withoutIdealFraction: 0, idealFractionTotal: 0 },
      'Cadastre unidades ativas',
    ],
  ])('explica por que a fracao ideal nao esta pronta (%j)', (overrides, text) => {
    const { html } = setup(
      unitsSummary({
        ...overrides,
        readiness: [
          { method: 'idealFraction', ready: false },
          { method: 'equalShare', ready: false },
          { method: 'area', ready: false },
        ],
      }),
    );

    expect(readiness(html, 'idealFraction')).toContain(text);
  });

  it.each([
    [1, '1 unidade sem área'],
    [8, '8 unidades sem área'],
  ])('explica por que a area nao esta pronta (%s)', (without, text) => {
    const { html } = setup(
      unitsSummary({
        withoutArea: without,
        readiness: [
          { method: 'idealFraction', ready: false },
          { method: 'equalShare', ready: true },
          { method: 'area', ready: false },
        ],
      }),
    );

    expect(readiness(html, 'area')).toContain(text);
  });

  it('sem unidades ativas, valor igual pede o cadastro de unidades', () => {
    const { html } = setup(
      unitsSummary({
        active: 0,
        readiness: [
          { method: 'idealFraction', ready: false },
          { method: 'equalShare', ready: false },
          { method: 'area', ready: false },
        ],
      }),
    );

    expect(readiness(html, 'equalShare')).toContain('Cadastre unidades ativas');
  });

  it('avisa o metodo escolhido quando a pessoa troca', () => {
    const { html, changes } = setup();

    radio(html, 'area').click();

    expect(changes).toHaveBeenCalledTimes(1);
    expect(changes).toHaveBeenCalledWith('area');
  });

  it('nao avisa nada ao clicar no metodo que ja esta escolhido', () => {
    const { html, changes } = setup();

    radio(html, 'idealFraction').click();

    expect(changes).not.toHaveBeenCalled();
  });

  it('trava as opcoes enquanto uma troca esta em andamento', () => {
    const { html } = setup(unitsSummary(), true);

    for (const method of ['idealFraction', 'equalShare', 'area'] as const) {
      expect(radio(html, method).disabled).toBe(true);
    }
  });

  it('avisa que a escolha vale para as cobrancas futuras', () => {
    const { html } = setup();

    expect(html.textContent).toContain(
      'O método escolhido será usado quando as cobranças forem calculadas',
    );
  });
});
