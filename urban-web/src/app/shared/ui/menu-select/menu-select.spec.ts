import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { flushPromises } from '../../../testing/async';
import { provideUrbanIcons } from '../../../core/theme/icons';
import { chooseMenuOption, menuSelectOptions, menuSelectValue } from '../../../testing/menu-select';
import { MenuOption, MenuSelect } from './menu-select';

const OPTIONS: readonly MenuOption[] = [
  { value: 'active', label: 'Ativas', icon: 'check_circle', tone: 'text-success' },
  { value: 'ended', label: 'Encerradas', icon: 'event_busy', menuTone: 'tone-ended' },
  { value: 'all', label: 'Todas' },
];

@Component({
  imports: [ReactiveFormsModule, MatFormField, MatLabel, MatError, MatHint, MenuSelect],
  template: `
    <form [formGroup]="form">
      <mat-form-field appearance="outline">
        <mat-label>Situação</mat-label>
        <urban-menu-select formControlName="status" [options]="options()" />
        <mat-hint>Escolha uma situação</mat-hint>
        <mat-error>Informe a situação</mat-error>
      </mat-form-field>
    </form>
  `,
})
class FormHost {
  readonly form = new FormGroup({
    status: new FormControl('active', { nonNullable: true, validators: Validators.required }),
  });
  readonly options = signal<readonly MenuOption[]>(OPTIONS);
}

@Component({
  imports: [MenuSelect],
  template: `
    <urban-menu-select
      data-testid="alone"
      ariaLabel="Tipo da linha 1"
      [options]="options"
      [value]="value()"
      (valueChange)="value.set($event)"
    />
  `,
})
class AloneHost {
  readonly options = OPTIONS;
  readonly value = signal('ended');
}

function setupForm(): { host: FormHost; html: HTMLElement; field: HTMLElement } {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
    ],
  });
  const fixture = TestBed.createComponent(FormHost);
  fixture.detectChanges();
  const html: HTMLElement = fixture.nativeElement;
  return {
    host: fixture.componentInstance,
    html,
    field: html.querySelector<HTMLElement>('urban-menu-select')!,
  };
}

function setupAlone(): { host: AloneHost; html: HTMLElement; field: HTMLElement } {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
    ],
  });
  const fixture = TestBed.createComponent(AloneHost);
  fixture.detectChanges();
  const html: HTMLElement = fixture.nativeElement;
  return {
    host: fixture.componentInstance,
    html,
    field: html.querySelector<HTMLElement>('urban-menu-select')!,
  };
}

function trigger(field: HTMLElement): HTMLButtonElement {
  return field.querySelector('button')!;
}

function items(): HTMLElement[] {
  return [...document.querySelectorAll<HTMLElement>('[role="menuitemradio"]')];
}

describe('MenuSelect', () => {
  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    http.match((request) => request.url.startsWith('icons/'));
    http.verify();
  });

  it('mostra no botão a opção escolhida, com o ícone e a cor dela', () => {
    const { field } = setupForm();

    expect(trigger(field).textContent).toContain('Ativas');
    const value = field.querySelector<HTMLElement>('[data-testid="menu-select-value"]')!;
    expect(value.className).toContain('text-success');
    expect(value.querySelector('mat-icon')).not.toBeNull();
  });

  it('fica em branco quando o valor não está na lista', () => {
    const { host, field } = setupForm();

    host.form.controls.status.setValue('outra');
    TestBed.tick();

    expect(trigger(field).textContent?.trim()).toBe('');
    expect(menuSelectValue(field)).toBe('outra');
  });

  it('abre o menu com uma opção por item, marcando a escolhida', () => {
    const { field } = setupForm();

    expect(menuSelectOptions(field)).toEqual([
      { value: 'active', label: 'Ativas', checked: true },
      { value: 'ended', label: 'Encerradas', checked: false },
      { value: 'all', label: 'Todas', checked: false },
    ]);
  });

  it('liga a escolha ao controle, marcando-o como tocado e alterado', () => {
    const { host, field } = setupForm();
    const control = host.form.controls.status;

    chooseMenuOption(field, 'ended');
    TestBed.tick();

    expect(control.value).toBe('ended');
    expect(control.dirty).toBe(true);
    expect(control.touched).toBe(true);
    expect(trigger(field).textContent).toContain('Encerradas');
    expect(trigger(field).getAttribute('aria-expanded')).toBe('false');
  });

  it('marca o controle como tocado ao fechar o menu sem escolher', () => {
    const { host, field } = setupForm();

    trigger(field).click();
    document.querySelector<HTMLElement>('.cdk-overlay-backdrop')!.click();
    TestBed.tick();

    expect(host.form.controls.status.touched).toBe(true);
    expect(host.form.controls.status.value).toBe('active');
  });

  it('pinta cada item com a cor definida para ele', () => {
    const { field } = setupForm();

    trigger(field).click();

    expect(items()[1].className).toContain('tone-ended');
    expect(items()[0].className).not.toContain('tone-ended');
  });

  it('desenha a seta cinza e a pinta de índigo só com o menu aberto', () => {
    const { field } = setupForm();
    const arrow = field.querySelector('mat-icon[data-testid="menu-select-arrow"]')!;

    expect(arrow.className).toContain('[--mat-icon-color:var(--mat-sys-on-surface-variant)]');
    expect(arrow.className).toContain(
      'group-aria-expanded:[--mat-icon-color:var(--mat-sys-primary)]',
    );
    expect(trigger(field).getAttribute('aria-expanded')).toBe('false');
    trigger(field).click();
    TestBed.tick();
    expect(trigger(field).getAttribute('aria-expanded')).toBe('true');
  });

  it('leva o foco à opção escolhida quando o menu abre', async () => {
    const { field } = setupForm();

    trigger(field).click();
    await flushPromises();

    expect(document.activeElement).toBe(items()[0]);
    expect(items()[0].getAttribute('aria-checked')).toBe('true');
  });

  it('acompanha o controle desabilitado', () => {
    const { host, field } = setupForm();

    host.form.controls.status.disable();
    TestBed.tick();

    expect(trigger(field).disabled).toBe(true);
  });

  it('flutua o rótulo do campo e aponta o rótulo para o botão', () => {
    const { html, field } = setupForm();
    const label = html.querySelector('label')!;

    expect(label.getAttribute('for')).toBe(trigger(field).id);
    expect(trigger(field).id).not.toBe('');
    expect(html.querySelector('.mdc-floating-label--float-above')).not.toBeNull();
  });

  it('mostra o erro do controle quando fica inválido e tocado', () => {
    const { host, html, field } = setupForm();

    host.form.controls.status.setValue('');
    host.form.controls.status.markAsTouched();
    TestBed.tick();

    expect(html.querySelector('mat-error')?.textContent?.trim()).toBe('Informe a situação');
    expect(trigger(field).getAttribute('aria-invalid')).toBe('true');
    expect(trigger(field).getAttribute('aria-describedby')).toBe(
      html.querySelector('mat-error')!.id,
    );
  });

  it('descreve o botão pela dica quando não há erro', () => {
    const { html, field } = setupForm();

    expect(trigger(field).getAttribute('aria-describedby')).toBe(
      html.querySelector('mat-hint')!.id,
    );
  });

  it('troca a lista de opções quando ela muda', () => {
    const { host, field } = setupForm();

    host.options.set([{ value: 'active', label: 'Em dia' }]);
    TestBed.tick();

    expect(trigger(field).textContent).toContain('Em dia');
  });

  it('serve fora de um campo do Material, com borda própria, valor e evento', () => {
    const { host, field } = setupAlone();

    expect(trigger(field).className).toContain('border-outline-variant');
    expect(trigger(field).getAttribute('aria-label')).toBe('Tipo da linha 1');
    expect(trigger(field).textContent).toContain('Encerradas');

    chooseMenuOption(field, 'all');
    TestBed.tick();

    expect(host.value()).toBe('all');
    expect(trigger(field).textContent).toContain('Todas');
  });

  it('dentro de um campo do Material, deixa a borda para o campo', () => {
    const { field } = setupForm();

    expect(trigger(field).className).not.toContain('border-outline-variant');
  });
});
