import { Component, inject } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { StateField } from './state.field';

@Component({
  imports: [ReactiveFormsModule, StateField],
  template: `
    <form [formGroup]="form" novalidate>
      <urban-state-field name="state" autocomplete="address-level1" />
    </form>
  `,
})
class Host {
  readonly form = inject(NonNullableFormBuilder).group({ state: ['', Validators.required] });
}

function render(): { fixture: ComponentFixture<Host>; select: HTMLSelectElement } {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  return { fixture, select: fixture.nativeElement.querySelector('select') };
}

describe('StateField', () => {
  it('oferece uma opção vazia e as 27 UFs', () => {
    const { select } = render();
    const options = [...select.options].map((option) => option.value);

    expect(options).toHaveLength(28);
    expect(options[0]).toBe('');
    expect(options).toContain('SC');
    expect(options).toContain('DF');
  });

  it('liga a escolha ao controle do formulário', () => {
    const { fixture, select } = render();

    select.value = 'SC';
    select.dispatchEvent(new Event('change'));

    expect(fixture.componentInstance.form.getRawValue().state).toBe('SC');
  });

  it('mostra a UF que já está no controle', () => {
    const { fixture, select } = render();

    fixture.componentInstance.form.controls.state.setValue('RS');
    fixture.detectChanges();

    expect(select.value).toBe('RS');
  });

  it('é obrigatória e avisa quando fica vazia', () => {
    const { fixture, select } = render();

    expect(select.required).toBe(true);
    fixture.componentInstance.form.controls.state.markAsTouched();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('mat-error').textContent).toContain('Informe a UF');
  });

  it('mostra o erro quando o formulário é enviado, sem ninguém ter tocado no campo', () => {
    const { fixture } = render();

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('mat-error')?.textContent).toContain('Informe a UF');
  });

  it('mantém o rótulo, o autocomplete e o atributo formControlName', () => {
    const { fixture, select } = render();

    expect(fixture.nativeElement.querySelector('mat-label').textContent).toContain('UF');
    expect(select.getAttribute('autocomplete')).toBe('address-level1');
    expect(select.getAttribute('formControlName')).toBe('state');
  });

  it('deixa o espaço de 20px abaixo de si, que o Material não reserva mais', () => {
    const { fixture } = render();

    expect(fixture.nativeElement.querySelector('urban-state-field').classList).toContain('mb-5');
  });

  it('deixa a área do erro crescer com o texto sem depender de configuração global', () => {
    const subscript = render().fixture.nativeElement.querySelector(
      '.mat-mdc-form-field-subscript-wrapper',
    );

    expect(subscript?.classList).toContain('mat-mdc-form-field-subscript-dynamic-size');
  });
});
