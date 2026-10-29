import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MaskKind } from '../forms/masks';
import { MaskDirective } from './mask.directive';

@Component({
  imports: [ReactiveFormsModule, MaskDirective],
  template: `<input [urbanMask]="kind()" [formControl]="control" />`,
})
class Host {
  readonly kind = signal<MaskKind>('cpf');
  readonly control = new FormControl<string | null>('', { nonNullable: false });
}

function render(kind: MaskKind = 'cpf'): {
  fixture: ComponentFixture<Host>;
  input: HTMLInputElement;
} {
  const fixture = TestBed.createComponent(Host);
  fixture.componentInstance.kind.set(kind);
  fixture.detectChanges();
  return { fixture, input: fixture.nativeElement.querySelector('input') };
}

function type(input: HTMLInputElement, text: string, caret: number = text.length): void {
  input.value = text;
  input.setSelectionRange(caret, caret);
  input.dispatchEvent(new Event('input'));
}

describe('MaskDirective', () => {
  it('formata o que a pessoa digita e guarda o texto formatado no controle', () => {
    const { fixture, input } = render('cpf');

    type(input, '12345678909');

    expect(input.value).toBe('123.456.789-09');
    expect(fixture.componentInstance.control.value).toBe('123.456.789-09');
  });

  it('formata aos poucos, sem separador sobrando no fim', () => {
    const { input } = render('cpf');

    type(input, '123');
    expect(input.value).toBe('123');

    type(input, '1234');
    expect(input.value).toBe('123.4');
  });

  it('descarta letras e o que passa do tamanho', () => {
    const { input } = render('cpf');

    type(input, 'abc123456789012');

    expect(input.value).toBe('123.456.789-01');
  });

  it('aceita letras em maiúsculas no CNPJ', () => {
    const { fixture, input } = render('cnpj');

    type(input, '12abc34501de35');

    expect(input.value).toBe('12.ABC.345/01DE-35');
    expect(fixture.componentInstance.control.value).toBe('12.ABC.345/01DE-35');
  });

  it.each([
    ['cep', '89010000', '89010-000'],
    ['phone', '47991234567', '(47) 99123-4567'],
    ['phone', '+55 47 3123-4567', '(47) 3123-4567'],
    ['date', '15061980', '15/06/1980'],
  ] as const)('formata %s: "%s" vira "%s"', (kind, typed, shown) => {
    const { input } = render(kind);

    type(input, typed);

    expect(input.value).toBe(shown);
  });

  it('mostra formatado o valor que o código coloca no controle, sem mudar o valor do controle', () => {
    const { fixture, input } = render('cpf');

    fixture.componentInstance.control.setValue('12345678909');

    expect(input.value).toBe('123.456.789-09');
    expect(fixture.componentInstance.control.value).toBe('12345678909');
  });

  it('limpa o campo quando o controle é esvaziado ou volta a ser nulo', () => {
    const { fixture, input } = render('cpf');
    type(input, '12345678909');

    fixture.componentInstance.control.reset();

    expect(input.value).toBe('');
    expect(fixture.componentInstance.control.value).toBeNull();
  });

  it('muda o teclado conforme o tipo de máscara', () => {
    const { fixture, input } = render('cpf');
    expect(input.getAttribute('inputmode')).toBe('numeric');

    fixture.componentInstance.kind.set('cnpj');
    fixture.detectChanges();
    expect(input.getAttribute('inputmode')).toBe('text');

    fixture.componentInstance.kind.set('phone');
    fixture.detectChanges();
    expect(input.getAttribute('inputmode')).toBe('tel');
  });

  it('passa a usar a máscara nova quando o tipo muda', () => {
    const { fixture, input } = render('cpf');

    fixture.componentInstance.kind.set('cep');
    fixture.detectChanges();
    type(input, '89010000');

    expect(input.value).toBe('89010-000');
  });

  it('mantém o cursor depois do dígito inserido no meio do texto', () => {
    const { input } = render('cpf');

    type(input, '123.9456', 5);

    expect(input.value).toBe('123.945.6');
    expect(input.selectionStart).toBe(5);
    expect(input.selectionEnd).toBe(5);
  });

  it('deixa o cursor no fim quando a pessoa digita no fim', () => {
    const { input } = render('cpf');

    type(input, '1234');

    expect(input.value).toBe('123.4');
    expect(input.selectionStart).toBe(5);
  });

  it('mantém o cursor no lugar quando um dígito é apagado no meio', () => {
    const { input } = render('cpf');

    type(input, '123.56.789-09', 4);

    expect(input.value).toBe('123.567.890-9');
    expect(input.selectionStart).toBe(3);
  });

  it('põe o cursor depois do parêntese ao digitar o primeiro dígito do telefone', () => {
    const { input } = render('phone');

    type(input, '4');

    expect(input.value).toBe('(4');
    expect(input.selectionStart).toBe(2);
  });

  it('marca o campo como tocado ao sair dele', () => {
    const { fixture, input } = render('cpf');
    expect(fixture.componentInstance.control.touched).toBe(false);

    input.dispatchEvent(new Event('blur'));

    expect(fixture.componentInstance.control.touched).toBe(true);
  });

  it('desabilita e habilita o campo junto com o controle', () => {
    const { fixture, input } = render('cpf');

    fixture.componentInstance.control.disable();
    expect(input.disabled).toBe(true);

    fixture.componentInstance.control.enable();
    expect(input.disabled).toBe(false);
  });

  it('formata também um campo que não está ligado a um formulário', () => {
    @Component({
      imports: [MaskDirective],
      template: `<input urbanMask="cep" />`,
    })
    class Alone {}

    const fixture = TestBed.createComponent(Alone);
    fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');

    type(input, '89010000');
    input.dispatchEvent(new Event('blur'));

    expect(input.value).toBe('89010-000');
  });

  it('formata sem mexer no cursor quando o campo não permite ler a posição dele', () => {
    @Component({
      imports: [MaskDirective],
      template: `<input urbanMask="cpf" type="email" />`,
    })
    class NoCaret {}

    const fixture = TestBed.createComponent(NoCaret);
    fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');

    input.value = '12345678909';
    input.dispatchEvent(new Event('input'));

    expect(input.value).toBe('123.456.789-09');
  });
});
