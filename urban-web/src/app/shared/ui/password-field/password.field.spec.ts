import { Component, inject } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { provideUrbanIcons } from '../../../core/theme/icons';
import { passwordPolicy } from '../../forms/password.validator';
import { PasswordField } from './password.field';

@Component({
  imports: [ReactiveFormsModule, PasswordField],
  template: `
    <form [formGroup]="form" novalidate>
      <urban-password-field />
    </form>
  `,
})
class SignInHost {
  readonly form = inject(NonNullableFormBuilder).group({ password: ['', Validators.required] });
}

@Component({
  imports: [ReactiveFormsModule, PasswordField],
  template: `
    <form [formGroup]="form" novalidate>
      <urban-password-field autocomplete="new-password" [policy]="true" />
    </form>
  `,
})
class CreateHost {
  readonly form = inject(NonNullableFormBuilder).group({
    password: ['', [Validators.required, passwordPolicy]],
  });
}

function render<T extends SignInHost | CreateHost>(host: new () => T): ComponentFixture<T> {
  TestBed.configureTestingModule({ providers: [provideUrbanIcons()] });
  const fixture = TestBed.createComponent(host);
  fixture.detectChanges();
  return fixture;
}

function html(fixture: ComponentFixture<unknown>): HTMLElement {
  return fixture.nativeElement;
}

function errorText(fixture: ComponentFixture<unknown>): string {
  return html(fixture).querySelector('mat-error')?.textContent?.trim() ?? '';
}

describe('PasswordField', () => {
  it('liga o campo ao controle password do formulário que o contém', () => {
    const fixture = render(SignInHost);
    const input = html(fixture).querySelector('input')!;

    input.value = 'Senha-segura-123';
    input.dispatchEvent(new Event('input'));

    expect(fixture.componentInstance.form.getRawValue().password).toBe('Senha-segura-123');
  });

  it('usa o autocomplete da senha atual por padrão e o da senha nova quando pedido', () => {
    expect(html(render(SignInHost)).querySelector('input')!.getAttribute('autocomplete')).toBe(
      'current-password',
    );
    TestBed.resetTestingModule();
    expect(html(render(CreateHost)).querySelector('input')!.getAttribute('autocomplete')).toBe(
      'new-password',
    );
  });

  it('mantém o atributo formControlName, que o foco no primeiro campo inválido procura', () => {
    const input = html(render(SignInHost)).querySelector('input')!;

    expect(input.getAttribute('formControlName')).toBe('password');
  });

  it('esconde a senha e alterna para mostrar e ocultar', () => {
    const fixture = render(SignInHost);
    const input = html(fixture).querySelector('input')!;
    const toggle = html(fixture).querySelector<HTMLButtonElement>('button')!;

    expect(input.type).toBe('password');
    expect(toggle.getAttribute('aria-label')).toBe('Mostrar senha');
    expect(toggle.getAttribute('aria-pressed')).toBe('false');

    toggle.click();
    fixture.detectChanges();

    expect(input.type).toBe('text');
    expect(toggle.getAttribute('aria-label')).toBe('Ocultar senha');
    expect(toggle.getAttribute('aria-pressed')).toBe('true');
  });

  it('na entrada só pede a senha, sem dica nem regras de criação', () => {
    const fixture = render(SignInHost);

    fixture.componentInstance.form.controls.password.markAsTouched();
    fixture.detectChanges();

    expect(html(fixture).querySelector('mat-hint')).toBeNull();
    expect(errorText(fixture)).toBe('Informe a senha');
  });

  it('na criação mostra a dica da política e o aviso de cada regra', () => {
    const fixture = render(CreateHost);
    const control = fixture.componentInstance.form.controls.password;

    expect(html(fixture).querySelector('mat-hint')?.textContent).toContain(
      'Mínimo de 10 caracteres, com letra e número',
    );

    control.markAsTouched();
    fixture.detectChanges();
    expect(errorText(fixture)).toBe('Informe a senha');

    const cases: readonly (readonly [string, string])[] = [
      ['curta1', 'A senha precisa ter pelo menos 10 caracteres'],
      ['a1'.repeat(65), 'A senha pode ter no máximo 128 caracteres'],
      ['somente-letras', 'A senha precisa ter pelo menos uma letra e um número'],
    ];
    for (const [value, message] of cases) {
      control.setValue(value);
      fixture.detectChanges();
      expect(errorText(fixture)).toBe(message);
    }
  });

  it('mostra o erro quando o formulário é enviado vazio, sem ninguém ter tocado no campo', () => {
    const fixture = render(SignInHost);

    html(fixture).querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(errorText(fixture)).toBe('Informe a senha');
  });

  it('deixa o espaço de 20px abaixo de si, menos quando mostra a regra da senha (ela ocupa o lugar)', () => {
    const signIn = render(SignInHost);
    expect(html(signIn).querySelector('urban-password-field')!.classList).toContain('mb-5');
  });

  it('não soma o espaço de baixo à regra da senha', () => {
    const create = render(CreateHost);
    expect(html(create).querySelector('urban-password-field')!.classList).not.toContain('mb-5');
  });
});
