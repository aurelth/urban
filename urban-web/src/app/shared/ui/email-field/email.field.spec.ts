import { Component, inject } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { emailFormat } from '../../forms/email.validator';
import { EmailField } from './email.field';

@Component({
  imports: [ReactiveFormsModule, EmailField],
  template: `
    <form [formGroup]="form" novalidate>
      <urban-email-field />
    </form>
  `,
})
class DefaultHost {
  readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, emailFormat]],
  });
}

@Component({
  imports: [ReactiveFormsModule, EmailField],
  template: `
    <form [formGroup]="form" novalidate>
      <urban-email-field autocomplete="username" />
    </form>
  `,
})
class LoginHost {
  readonly form = inject(NonNullableFormBuilder).group({ email: [''] });
}

function render<T extends DefaultHost | LoginHost>(host: new () => T): ComponentFixture<T> {
  const fixture = TestBed.createComponent(host);
  fixture.detectChanges();
  return fixture;
}

function input(fixture: ComponentFixture<unknown>): HTMLInputElement {
  return fixture.nativeElement.querySelector('input');
}

describe('EmailField', () => {
  it('liga o campo ao controle email do formulário que o contém', () => {
    const fixture = render(DefaultHost);

    input(fixture).value = 'sindica@example.com';
    input(fixture).dispatchEvent(new Event('input'));

    expect(fixture.componentInstance.form.getRawValue().email).toBe('sindica@example.com');
  });

  it('deixa o teclado e o gerenciador de senhas tratarem o campo como e-mail', () => {
    const element = input(render(DefaultHost));

    expect(element.type).toBe('email');
    expect(element.getAttribute('autocomplete')).toBe('email');
    expect(element.getAttribute('inputmode')).toBe('email');
    expect(element.getAttribute('autocapitalize')).toBe('none');
    expect(element.getAttribute('spellcheck')).toBe('false');
  });

  it('usa o autocomplete pedido, como username na entrada', () => {
    expect(input(render(LoginHost)).getAttribute('autocomplete')).toBe('username');
  });

  it('mantém o atributo formControlName, que o foco no primeiro campo inválido procura', () => {
    expect(input(render(DefaultHost)).getAttribute('formControlName')).toBe('email');
  });

  it('pede o e-mail quando está vazio e avisa quando o formato não serve', () => {
    const fixture = render(DefaultHost);
    const control = fixture.componentInstance.form.controls.email;

    control.markAsTouched();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('mat-error').textContent).toContain(
      'Informe o e-mail',
    );

    control.setValue('sem-arroba');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('mat-error').textContent).toContain(
      'Informe um e-mail válido',
    );
  });

  it('mostra o erro quando o formulário é enviado vazio, sem ninguém ter tocado no campo', () => {
    const fixture = render(DefaultHost);

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('mat-error')?.textContent).toContain(
      'Informe o e-mail',
    );
  });
});
