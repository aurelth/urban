import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { provideUrbanIcons } from '../../../core/theme/icons';
import {
  chooseMenuOption,
  menuSelectLabel,
  menuSelectOptions,
  menuSelectValue,
} from '../../../testing/menu-select';
import { SelectField, SelectOption } from './select.field';

@Component({
  imports: [ReactiveFormsModule, SelectField],
  template: `
    <form [formGroup]="form">
      <urban-select-field
        name="month"
        label="Mês"
        [options]="options()"
        [emptyLabel]="emptyLabel()"
        [hint]="hint()"
        [errors]="{ required: 'Escolha o mês' }"
      />
    </form>
  `,
})
class Host {
  readonly form = new FormGroup({
    month: new FormControl('', { nonNullable: true, validators: Validators.required }),
  });
  readonly options = signal<SelectOption[]>([
    { value: '2026-11', label: 'Novembro de 2026' },
    { value: '2026-12', label: 'Dezembro de 2026' },
  ]);
  readonly emptyLabel = signal<string | null>(null);
  readonly hint = signal<string | null>(null);
}

function setup(): { host: Host; html: HTMLElement; select: HTMLElement } {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
    ],
  });
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const html: HTMLElement = fixture.nativeElement;
  return {
    host: fixture.componentInstance,
    html,
    select: html.querySelector<HTMLElement>('urban-menu-select')!,
  };
}

describe('SelectField', () => {
  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    http.match((request) => request.url.startsWith('icons/'));
    http.verify();
  });

  it('lista as opcoes no menu e liga a escolha ao controle do formulario', () => {
    const { host, html, select } = setup();

    expect(html.querySelector('mat-label')?.textContent?.trim()).toBe('Mês');
    expect(menuSelectOptions(select)).toEqual([
      { value: '2026-11', label: 'Novembro de 2026', checked: false },
      { value: '2026-12', label: 'Dezembro de 2026', checked: false },
    ]);

    chooseMenuOption(select, '2026-12');
    TestBed.tick();

    expect(host.form.controls.month.value).toBe('2026-12');
    expect(menuSelectLabel(select)).toBe('Dezembro de 2026');
  });

  it('mostra no botao o valor que ja esta no controle', () => {
    const { host, select } = setup();

    host.form.controls.month.setValue('2026-11');
    TestBed.tick();

    expect(menuSelectValue(select)).toBe('2026-11');
    expect(menuSelectLabel(select)).toBe('Novembro de 2026');
  });

  it('acrescenta uma primeira opcao vazia quando pedido', () => {
    const { host, select } = setup();
    host.emptyLabel.set('Sem data final');
    TestBed.tick();

    expect(menuSelectOptions(select)[0]).toEqual({
      value: '',
      label: 'Sem data final',
      checked: true,
    });
  });

  it('mostra a dica quando ha uma', () => {
    const { host, html } = setup();
    host.hint.set('Escolha o mês');
    TestBed.tick();

    expect(html.querySelector('mat-hint')?.textContent?.trim()).toBe('Escolha o mês');
  });

  it('mostra o erro do controle com o texto de quem usa', () => {
    const { host, html } = setup();
    host.form.controls.month.markAsTouched();
    host.form.updateValueAndValidity();
    TestBed.tick();

    expect(html.querySelector('mat-error')?.textContent?.trim()).toBe('Escolha o mês');
  });
});
