import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { jest } from '@jest/globals';
import { provideUrbanIcons } from '../../../core/theme/icons';
import { ExpandableText } from './expandable-text';

const LONG = 'Linha 1\nLinha 2\nLinha 3\nLinha 4\nLinha 5\nLinha 6';

/** O jsdom não faz layout: o teste diz quanto o texto mede com e sem o corte de 4 linhas. */
function measures(scrollHeight: number, clientHeight: number): void {
  jest.spyOn(HTMLElement.prototype, 'scrollHeight', 'get').mockReturnValue(scrollHeight);
  jest.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockReturnValue(clientHeight);
}

async function setup(
  text: string,
): Promise<{ fixture: ComponentFixture<ExpandableText>; html: HTMLElement }> {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
    ],
  });
  const fixture = TestBed.createComponent(ExpandableText);
  fixture.componentRef.setInput('text', text);
  await fixture.whenStable();
  fixture.detectChanges();
  return { fixture, html: fixture.nativeElement };
}

function body(html: HTMLElement): HTMLElement {
  return html.querySelector<HTMLElement>('[data-testid="body"]')!;
}

function toggle(html: HTMLElement): HTMLButtonElement | null {
  return html.querySelector<HTMLButtonElement>('[data-testid="toggle-body"]');
}

async function click(fixture: ComponentFixture<ExpandableText>, html: HTMLElement): Promise<void> {
  toggle(html)!.click();
  await fixture.whenStable();
  fixture.detectChanges();
}

describe('ExpandableText', () => {
  afterEach(() => jest.restoreAllMocks());

  it('mostra o texto cortado em 4 linhas e guarda as quebras de linha', async () => {
    measures(200, 96);
    const { html } = await setup(LONG);

    expect(body(html).textContent).toContain('Linha 6');
    expect(body(html).classList.contains('line-clamp-4')).toBe(true);
    expect(body(html).classList.contains('whitespace-pre-line')).toBe(true);
  });

  it('oferece "Ver mais" quando o texto passa das 4 linhas', async () => {
    measures(200, 96);
    const { html } = await setup(LONG);

    expect(toggle(html)?.textContent?.trim()).toBe('Ver mais');
    expect(toggle(html)?.getAttribute('aria-expanded')).toBe('false');
  });

  it('não oferece nada quando o texto cabe nas 4 linhas', async () => {
    measures(96, 96);
    const { html } = await setup('Curto');

    expect(toggle(html)).toBeNull();
    expect(body(html).classList.contains('line-clamp-4')).toBe(true);
  });

  it('"Ver mais" mostra o texto inteiro e vira "Ver menos"', async () => {
    measures(200, 96);
    const { fixture, html } = await setup(LONG);

    await click(fixture, html);

    expect(body(html).classList.contains('line-clamp-4')).toBe(false);
    expect(toggle(html)?.textContent?.trim()).toBe('Ver menos');
    expect(toggle(html)?.getAttribute('aria-expanded')).toBe('true');
  });

  it('"Ver menos" volta às 4 linhas e a "Ver mais"', async () => {
    measures(200, 96);
    const { fixture, html } = await setup(LONG);

    await click(fixture, html);
    await click(fixture, html);

    expect(body(html).classList.contains('line-clamp-4')).toBe(true);
    expect(toggle(html)?.textContent?.trim()).toBe('Ver mais');
    expect(toggle(html)?.getAttribute('aria-expanded')).toBe('false');
  });

  it('o botão leva a seta para baixo fechado e para cima aberto', async () => {
    measures(200, 96);
    const { fixture, html } = await setup(LONG);
    const icon = (): string | null =>
      toggle(html)!.querySelector('mat-icon')!.getAttribute('data-mat-icon-name');

    expect(icon()).toBe('keyboard_arrow_down');
    await click(fixture, html);
    expect(icon()).toBe('keyboard_arrow_up');
  });

  it('texto novo (uma edição) volta a mostrar só o começo', async () => {
    measures(200, 96);
    const { fixture, html } = await setup(LONG);
    await click(fixture, html);

    fixture.componentRef.setInput('text', `${LONG}\nLinha 7`);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(body(html).classList.contains('line-clamp-4')).toBe(true);
    expect(toggle(html)?.textContent?.trim()).toBe('Ver mais');
  });

  it('o texto editado para algo curto deixa de oferecer "Ver mais"', async () => {
    measures(200, 96);
    const { fixture, html } = await setup(LONG);

    measures(96, 96);
    fixture.componentRef.setInput('text', 'Curto');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(toggle(html)).toBeNull();
  });
});

@Component({
  imports: [ExpandableText],
  template: '<urban-expandable-text [text]="text" />',
})
class Host {
  text = 'Oi';
}

describe('ExpandableText dentro de outro componente', () => {
  it('é um bloco, para o botão ficar embaixo do texto', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideUrbanIcons()],
    });
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();

    expect(
      fixture.nativeElement.querySelector('urban-expandable-text').classList.contains('block'),
    ).toBe(true);
  });
});
