import { Component, inject, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { addressGroup } from '../../forms/address.group';
import { AddressFields } from './address.fields';

@Component({
  imports: [ReactiveFormsModule, AddressFields],
  template: `
    <form [formGroup]="form" novalidate>
      <div formGroupName="manager">
        <urban-address-fields groupName="address" [personal]="personal()" />
      </div>
    </form>
  `,
})
class Host {
  readonly personal = signal(true);
  private readonly builder = inject(NonNullableFormBuilder);
  readonly form = this.builder.group({
    manager: this.builder.group({ address: addressGroup(this.builder) }),
  });
}

function render(): { fixture: ComponentFixture<Host>; html: HTMLElement } {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  return { fixture, html: fixture.nativeElement };
}

function control(html: HTMLElement, name: string): HTMLInputElement | HTMLSelectElement {
  return html.querySelector<HTMLInputElement>(`[formControlName="${name}"]`)!;
}

function type(html: HTMLElement, name: string, value: string): void {
  const element = control(html, name);
  element.value = value;
  element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? 'change' : 'input'));
}

function messages(html: HTMLElement): string[] {
  return [...html.querySelectorAll('mat-error')].map((error) => error.textContent!.trim());
}

describe('AddressFields', () => {
  it('mostra os sete campos do endereço, na ordem em que a pessoa preenche', () => {
    const { html } = render();
    const labels = [...html.querySelectorAll('mat-label')].map((label) =>
      label.textContent!.trim(),
    );

    expect(labels).toEqual(['CEP', 'Rua', 'Número', 'Complemento', 'Bairro', 'Cidade', 'UF']);
  });

  it('liga cada campo ao controle do grupo indicado', () => {
    const { fixture, html } = render();

    type(html, 'postalCode', '89010000');
    type(html, 'street', 'Rua das Flores');
    type(html, 'number', '120');
    type(html, 'complement', 'Sala 2');
    type(html, 'neighborhood', 'Centro');
    type(html, 'city', 'Blumenau');
    type(html, 'state', 'SC');

    expect(fixture.componentInstance.form.getRawValue().manager.address).toEqual({
      postalCode: '89010-000',
      street: 'Rua das Flores',
      number: '120',
      complement: 'Sala 2',
      neighborhood: 'Centro',
      city: 'Blumenau',
      state: 'SC',
    });
  });

  it('formata o CEP enquanto a pessoa digita', () => {
    const { html } = render();

    type(html, 'postalCode', '89010000');

    expect(control(html, 'postalCode').value).toBe('89010-000');
    expect(control(html, 'postalCode').getAttribute('inputmode')).toBe('numeric');
  });

  it('pede os campos obrigatórios quando o formulário é enviado vazio', () => {
    const { fixture, html } = render();

    html.querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(messages(html)).toEqual([
      'Informe o CEP',
      'Informe a rua',
      'Informe o número (use S/N quando não houver)',
      'Informe o bairro',
      'Informe a cidade',
      'Informe a UF',
    ]);
  });

  it('avisa quando o CEP não tem forma de CEP', () => {
    const { fixture, html } = render();

    type(html, 'postalCode', '1234');
    fixture.componentInstance.form.controls.manager.controls.address.controls.postalCode.markAsTouched();
    fixture.detectChanges();

    expect(messages(html)).toEqual(['Informe um CEP válido']);
  });

  it('trata texto só de espaços como não preenchido', () => {
    const { fixture, html } = render();

    type(html, 'street', '    ');
    fixture.componentInstance.form.controls.manager.controls.address.controls.street.markAsTouched();
    fixture.detectChanges();

    expect(messages(html)).toEqual(['Informe a rua']);
  });

  it('deixa o complemento opcional e avisa quando passa de 60 caracteres', () => {
    const { fixture, html } = render();
    const complement =
      fixture.componentInstance.form.controls.manager.controls.address.controls.complement;

    expect((control(html, 'complement') as HTMLInputElement).required).toBe(false);

    type(html, 'complement', 'a'.repeat(61));
    complement.markAsTouched();
    fixture.detectChanges();

    expect(messages(html)).toEqual(['O complemento deve ter até 60 caracteres']);
  });

  it.each([
    ['street', 121, 'A rua deve ter até 120 caracteres'],
    ['number', 11, 'O número deve ter até 10 caracteres'],
    ['neighborhood', 81, 'O bairro deve ter até 80 caracteres'],
    ['city', 81, 'A cidade deve ter até 80 caracteres'],
  ] as const)('avisa quando %s passa do tamanho', (name, length, message) => {
    const { fixture, html } = render();

    type(html, name, 'a'.repeat(length));
    fixture.componentInstance.form.controls.manager.controls.address.controls[name].markAsTouched();
    fixture.detectChanges();

    expect(messages(html)).toEqual([message]);
  });

  it('sugere o endereço salvo do navegador quando é o endereço da própria pessoa', () => {
    const { html } = render();

    expect(control(html, 'postalCode').getAttribute('autocomplete')).toBe('postal-code');
    expect(control(html, 'street').getAttribute('autocomplete')).toBe('address-line1');
    expect(control(html, 'complement').getAttribute('autocomplete')).toBe('address-line2');
    expect(control(html, 'neighborhood').getAttribute('autocomplete')).toBe('address-level3');
    expect(control(html, 'city').getAttribute('autocomplete')).toBe('address-level2');
    expect(control(html, 'state').getAttribute('autocomplete')).toBe('address-level1');
  });

  it('não sugere nada quando o endereço é de outro lugar', () => {
    const { fixture, html } = render();

    fixture.componentInstance.personal.set(false);
    fixture.detectChanges();

    for (const name of [
      'postalCode',
      'street',
      'number',
      'complement',
      'neighborhood',
      'city',
      'state',
    ]) {
      expect(control(html, name).getAttribute('autocomplete')).toBe('off');
    }
  });
});
