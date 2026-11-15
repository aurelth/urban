import { OverlayContainer } from '@angular/cdk/overlay';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, inject } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { provideUrbanIcons } from '../../../core/theme/icons';
import { flushPromises } from '../../../testing/async';
import { TextField } from './text.field';

@Component({
  imports: [ReactiveFormsModule, TextField],
  template: `
    <form [formGroup]="form" novalidate>
      <urban-text-field name="entry" label="Data de entrada" mask="date" />
      <urban-text-field name="birth" label="Nascimento" mask="date" [birthDate]="true" />
      <urban-text-field name="document" label="CPF" mask="cpf" />
      <urban-text-field name="plain" label="Observação" />
    </form>
  `,
})
class Host {
  readonly form = inject(NonNullableFormBuilder).group({
    entry: [''],
    birth: [''],
    document: [''],
    plain: [''],
  });
}

function render(): { fixture: ComponentFixture<Host>; html: HTMLElement; overlay: HTMLElement } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), provideUrbanIcons()],
  });
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  return {
    fixture,
    html: fixture.nativeElement,
    overlay: TestBed.inject(OverlayContainer).getContainerElement(),
  };
}

function toggle(html: HTMLElement, field: string): HTMLButtonElement | null {
  return html.querySelector<HTMLButtonElement>(
    `urban-text-field:has(input[formControlName="${field}"]) button[data-testid="open-calendar"]`,
  );
}

async function settle(fixture: ComponentFixture<Host>): Promise<void> {
  fixture.detectChanges();
  await fixture.whenStable();
  await flushPromises();
  fixture.detectChanges();
}

describe('TextField com calendário', () => {
  it('só os campos de data têm o botão "Abrir calendário"', () => {
    const { html } = render();

    expect(toggle(html, 'entry')).not.toBeNull();
    expect(toggle(html, 'entry')!.getAttribute('aria-label')).toBe('Abrir calendário');
    expect(toggle(html, 'birth')).not.toBeNull();
    expect(toggle(html, 'document')).toBeNull();
    expect(toggle(html, 'plain')).toBeNull();
  });

  it('o botão é de ícone, fica fora da ordem de digitação sem perder o teclado e não envia o formulário', () => {
    const { html } = render();

    const button = toggle(html, 'entry')!;
    expect(button.type).toBe('button');
    expect(button.querySelector('mat-icon')).not.toBeNull();
  });

  it('o calendário só é carregado quando a pessoa pede', async () => {
    const { fixture, html, overlay } = render();
    await settle(fixture);

    expect(overlay.querySelector('mat-calendar')).toBeNull();
    expect(html.querySelector('urban-calendar-popup')).toBeNull();
  });

  it('clicar no botão abre o calendário no mês da data digitada', async () => {
    const { fixture, html, overlay } = render();
    fixture.componentInstance.form.controls.entry.setValue('05/03/2024');

    toggle(html, 'entry')!.click();
    await settle(fixture);

    expect(overlay.querySelector('mat-calendar')).not.toBeNull();
    expect(overlay.querySelector('.mat-calendar-body-selected')?.textContent?.trim()).toBe('5');
    expect(overlay.querySelector('.mat-calendar-period-button')?.textContent).toContain('2024');
  });

  it('escolher um dia escreve dd/mm/aaaa no campo, mascarado, e marca o controle como mexido', async () => {
    const { fixture, html, overlay } = render();
    const control = fixture.componentInstance.form.controls.entry;
    control.setValue('05/03/2024');

    toggle(html, 'entry')!.click();
    await settle(fixture);
    Array.from(overlay.querySelectorAll<HTMLElement>('.mat-calendar-body-cell'))
      .find((cell) => cell.textContent?.trim() === '20')!
      .click();
    await settle(fixture);

    expect(control.value).toBe('20/03/2024');
    expect(html.querySelector<HTMLInputElement>('input[formControlName="entry"]')!.value).toBe(
      '20/03/2024',
    );
    expect(control.dirty).toBe(true);
    expect(control.touched).toBe(true);
    expect(overlay.querySelector('mat-calendar')).toBeNull();
  });

  it('com o campo vazio ou incompleto, o calendário abre no mês de hoje e nada fica marcado', async () => {
    const { fixture, html, overlay } = render();
    fixture.componentInstance.form.controls.entry.setValue('05/03');

    toggle(html, 'entry')!.click();
    await settle(fixture);

    expect(overlay.querySelector('.mat-calendar-body-selected')).toBeNull();
    expect(overlay.querySelector('.mat-calendar-period-button')?.textContent).toContain(
      String(new Date().getFullYear()),
    );
  });

  it('data de nascimento abre na lista de anos', async () => {
    const { fixture, html, overlay } = render();

    toggle(html, 'birth')!.click();
    await settle(fixture);

    expect(overlay.querySelector('mat-multi-year-view')).not.toBeNull();
  });

  it('ao fechar o calendário sem escolher, o foco volta para o botão e o texto fica como estava', async () => {
    const { fixture, html } = render();
    const control = fixture.componentInstance.form.controls.entry;
    control.setValue('05/03/2024');

    toggle(html, 'entry')!.click();
    await settle(fixture);
    document.body.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Escape', keyCode: 27, bubbles: true }),
    );
    await settle(fixture);

    expect(control.value).toBe('05/03/2024');
    expect(document.activeElement).toBe(toggle(html, 'entry'));
  });

  it('dá para abrir de novo depois de fechar', async () => {
    const { fixture, html, overlay } = render();

    toggle(html, 'entry')!.click();
    await settle(fixture);
    document.body.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Escape', keyCode: 27, bubbles: true }),
    );
    await settle(fixture);
    toggle(html, 'entry')!.click();
    await settle(fixture);

    expect(overlay.querySelector('mat-calendar')).not.toBeNull();
  });

  it('com o controle desabilitado o botão também fica desabilitado', async () => {
    const { fixture, html } = render();

    fixture.componentInstance.form.controls.entry.disable();
    await settle(fixture);

    expect(toggle(html, 'entry')!.disabled).toBe(true);
  });
});
