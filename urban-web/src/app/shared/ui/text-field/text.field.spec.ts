import { Component, inject } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MaskKind } from '../../forms/masks';
import { TextField } from './text.field';

@Component({
  imports: [ReactiveFormsModule, TextField],
  template: `
    <form [formGroup]="form" novalidate>
      <urban-text-field
        name="fullName"
        label="Nome completo"
        autocomplete="name"
        hint="Como no documento"
        [errors]="errors"
      />
      <urban-text-field name="nickname" label="Apelido" [optional]="true" inputmode="search" />
      <urban-text-field name="document" label="CPF" [mask]="mask" />
      <urban-text-field name="landline" label="Telefone" [mask]="'phone'" [optional]="true" />
      <div formGroupName="address">
        <urban-text-field name="street" label="Rua" [errors]="{ required: 'Informe a rua' }" />
      </div>
    </form>
  `,
})
class Host {
  mask: MaskKind = 'cpf';
  readonly errors = { required: 'Informe o nome', fullName: 'Informe o nome e o sobrenome' };
  readonly form = inject(NonNullableFormBuilder).group({
    fullName: ['', [Validators.required, Validators.minLength(5)]],
    nickname: [''],
    document: [''],
    landline: [''],
    address: inject(NonNullableFormBuilder).group({ street: ['', Validators.required] }),
  });
}

function render(): { fixture: ComponentFixture<Host>; html: HTMLElement } {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  return { fixture, html: fixture.nativeElement };
}

function input(html: HTMLElement, name: string): HTMLInputElement {
  return html.querySelector<HTMLInputElement>(`input[formControlName="${name}"]`)!;
}

function errorText(html: HTMLElement): string {
  return html.querySelector('mat-error')?.textContent?.trim() ?? '';
}

describe('TextField', () => {
  it('liga o campo ao controle de mesmo nome no formulário que o contém', () => {
    const { fixture, html } = render();

    input(html, 'fullName').value = 'Maria Silva';
    input(html, 'fullName').dispatchEvent(new Event('input'));

    expect(fixture.componentInstance.form.getRawValue().fullName).toBe('Maria Silva');
  });

  it('mostra o rótulo e a dica', () => {
    const { html } = render();
    const field = html.querySelector('urban-text-field')!;

    expect(field.querySelector('mat-label')?.textContent).toContain('Nome completo');
    expect(field.querySelector('mat-hint')?.textContent).toContain('Como no documento');
  });

  it('marca como obrigatório, a não ser que o campo seja opcional', () => {
    const { html } = render();

    expect(input(html, 'fullName').required).toBe(true);
    expect(input(html, 'nickname').required).toBe(false);
    expect(input(html, 'document').required).toBe(true);
    expect(input(html, 'landline').required).toBe(false);
  });

  it('repassa o autocomplete e o teclado pedidos', () => {
    const { html } = render();

    expect(input(html, 'fullName').getAttribute('autocomplete')).toBe('name');
    expect(input(html, 'nickname').getAttribute('inputmode')).toBe('search');
    expect(input(html, 'nickname').getAttribute('autocomplete')).toBe('off');
  });

  it('mantém o atributo formControlName, que a busca do campo inválido procura', () => {
    const { html } = render();

    expect(input(html, 'fullName').getAttribute('formControlName')).toBe('fullName');
  });

  it('aplica a máscara pedida', () => {
    const { fixture, html } = render();

    input(html, 'document').value = '12345678909';
    input(html, 'document').dispatchEvent(new Event('input'));

    expect(input(html, 'document').value).toBe('123.456.789-09');
    expect(fixture.componentInstance.form.getRawValue().document).toBe('123.456.789-09');
  });

  it('usa o teclado da máscara quando há máscara', () => {
    const { html } = render();

    expect(input(html, 'document').getAttribute('inputmode')).toBe('numeric');
  });

  it('mostra a mensagem do primeiro erro que o controle tem, na ordem do mapa', () => {
    const { fixture, html } = render();
    const control = fixture.componentInstance.form.controls.fullName;

    control.markAsTouched();
    fixture.detectChanges();
    expect(errorText(html)).toBe('Informe o nome');

    control.setValue('Ana');
    fixture.detectChanges();
    expect(errorText(html)).toBe('Confira este campo');
  });

  it('usa o erro do validador próprio quando o mapa o conhece', () => {
    const { fixture, html } = render();
    const control = fixture.componentInstance.form.controls.fullName;

    control.setValidators([Validators.required, () => ({ fullName: true })]);
    control.setValue('Maria');
    control.markAsTouched();
    fixture.detectChanges();

    expect(errorText(html)).toBe('Informe o nome e o sobrenome');
  });

  it('mostra o erro quando o formulário é enviado, sem ninguém ter tocado no campo', () => {
    const { fixture, html } = render();

    html.querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(errorText(html)).toBe('Informe o nome');
  });

  it('acha o controle dentro de um grupo do formulário', () => {
    const { fixture, html } = render();
    const street = fixture.componentInstance.form.controls.address.controls.street;

    input(html, 'street').value = 'Rua das Flores';
    input(html, 'street').dispatchEvent(new Event('input'));
    expect(street.value).toBe('Rua das Flores');

    street.setValue('');
    street.markAsTouched();
    fixture.detectChanges();

    const messages = [...html.querySelectorAll('mat-error')].map((e) => e.textContent?.trim());
    expect(messages).toContain('Informe a rua');
  });

  it('deixa o espaço de 20px abaixo de si, que o Material não reserva mais, menos quando há dica (a dica ocupa o lugar)', () => {
    const { html } = render();
    const fields = [...html.querySelectorAll('urban-text-field')];

    expect(fields[0].classList).not.toContain('mb-5');
    expect(fields[1].classList).toContain('mb-5');
    expect(fields[4].classList).toContain('mb-5');
  });
});
