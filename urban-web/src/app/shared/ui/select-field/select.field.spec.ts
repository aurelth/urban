import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
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

function setup(): { host: Host; html: HTMLElement; select: HTMLSelectElement } {
  TestBed.configureTestingModule({
    providers: [{ provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } }],
  });
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const html: HTMLElement = fixture.nativeElement;
  return { host: fixture.componentInstance, html, select: html.querySelector('select')! };
}

describe('SelectField', () => {
  it('lista as opcoes e liga a escolha ao controle do formulario', () => {
    const { host, html, select } = setup();

    expect(html.querySelector('mat-label')?.textContent?.trim()).toBe('Mês');
    expect([...select.options].map((option) => option.textContent?.trim())).toEqual([
      'Novembro de 2026',
      'Dezembro de 2026',
    ]);

    select.value = '2026-12';
    select.dispatchEvent(new Event('change'));

    expect(host.form.controls.month.value).toBe('2026-12');
  });

  it('acrescenta uma primeira opcao vazia quando pedido', () => {
    const { host, html, select } = setup();
    host.emptyLabel.set('Sem data final');
    TestBed.tick();
    html.ownerDocument.defaultView!.dispatchEvent(new Event('resize'));

    expect(select.options[0].value).toBe('');
    expect(select.options[0].textContent?.trim()).toBe('Sem data final');
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
