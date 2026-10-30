import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, inject, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';
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

@Component({
  imports: [ReactiveFormsModule, AddressFields],
  template: `
    <form [formGroup]="form" novalidate>
      <div formGroupName="manager">
        <urban-address-fields groupName="address" />
      </div>
      <div formGroupName="condominium">
        <urban-address-fields groupName="address" [personal]="false" />
      </div>
    </form>
  `,
})
class TwoAddressesHost {
  private readonly builder = inject(NonNullableFormBuilder);
  readonly form = this.builder.group({
    manager: this.builder.group({ address: addressGroup(this.builder) }),
    condominium: this.builder.group({ address: addressGroup(this.builder) }),
  });
}

beforeEach(() => {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  // O aviso de falha leva o ícone de erro. Registrado de antemão, o ícone não vai buscar o SVG pela rede.
  TestBed.inject(MatIconRegistry).addSvgIconLiteral(
    'error',
    TestBed.inject(DomSanitizer).bypassSecurityTrustHtml('<svg></svg>'),
  );
});

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

const BLUMENAU = {
  postalCode: '89010000',
  street: 'Rua XV de Novembro',
  neighborhood: 'Centro',
  city: 'Blumenau',
  state: 'SC',
};

const JOINVILLE = {
  postalCode: '89201000',
  street: 'Rua do Príncipe',
  neighborhood: 'Centro',
  city: 'Joinville',
  state: 'SC',
};

const LOOKUP = '/api/postal-codes/';
const NOT_FOUND_NOTICE = 'Não encontramos esse CEP. Preencha o endereço manualmente';
const UNAVAILABLE_NOTICE = 'Não foi possível buscar o endereço agora. Preencha manualmente';

function notice(root: HTMLElement): string {
  return [...root.querySelectorAll('[role="status"], [role="alert"]')]
    .map((element) => element.textContent?.trim() ?? '')
    .join('');
}

function alert(root: HTMLElement): HTMLElement | null {
  return root.querySelector<HTMLElement>('[role="alert"]');
}

function address(fixture: ComponentFixture<Host>) {
  return fixture.componentInstance.form.getRawValue().manager.address;
}

function renderTwo(): {
  fixture: ComponentFixture<TwoAddressesHost>;
  html: HTMLElement;
  sections: HTMLElement[];
} {
  const fixture = TestBed.createComponent(TwoAddressesHost);
  fixture.detectChanges();
  const html: HTMLElement = fixture.nativeElement;
  return {
    fixture,
    html,
    sections: [...html.querySelectorAll<HTMLElement>('urban-address-fields')],
  };
}

describe('AddressFields, preenchimento pelo CEP', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('consulta o CEP completo, só com os dígitos, e preenche rua, bairro, cidade e UF', () => {
    const { fixture, html } = render();

    type(html, 'postalCode', '89010000');
    http.expectOne(`${LOOKUP}89010000`).flush(BLUMENAU);
    fixture.detectChanges();

    expect(address(fixture)).toEqual({
      postalCode: '89010-000',
      street: 'Rua XV de Novembro',
      number: '',
      complement: '',
      neighborhood: 'Centro',
      city: 'Blumenau',
      state: 'SC',
    });
    expect((control(html, 'street') as HTMLInputElement).value).toBe('Rua XV de Novembro');
    expect((control(html, 'state') as HTMLSelectElement).value).toBe('SC');
  });

  it.each(['', '8901', '8901000', '00000000'])(
    'não consulta enquanto o CEP não está completo e válido (%j)',
    (value) => {
      const { html } = render();

      type(html, 'postalCode', value);

      http.expectNone((request) => request.url.startsWith(LOOKUP));
    },
  );

  it('não consulta de novo o mesmo CEP, mesmo que a pessoa mexa no campo sem mudá-lo', () => {
    const { html } = render();
    type(html, 'postalCode', '89010000');
    http.expectOne(`${LOOKUP}89010000`).flush(BLUMENAU);

    type(html, 'postalCode', '89010-000');
    type(html, 'postalCode', '89010000');

    http.expectNone((request) => request.url.startsWith(LOOKUP));
  });

  it('mostra que está buscando, em texto, sem travar o formulário', () => {
    const { fixture, html } = render();

    type(html, 'postalCode', '89010000');
    fixture.detectChanges();

    expect(notice(html)).toBe('Buscando o endereço');
    expect((control(html, 'street') as HTMLInputElement).disabled).toBe(false);
    expect((control(html, 'number') as HTMLInputElement).disabled).toBe(false);
    http.expectOne(`${LOOKUP}89010000`).flush(BLUMENAU);
    fixture.detectChanges();
    expect(notice(html)).toBe('Endereço preenchido. Confira os dados e informe o número');
  });

  it.each([
    [404, 'Not Found', NOT_FOUND_NOTICE],
    [503, 'Service Unavailable', UNAVAILABLE_NOTICE],
    [429, 'Too Many Requests', UNAVAILABLE_NOTICE],
  ])('com resposta %i avisa em texto e deixa preencher à mão', (status, statusText, message) => {
    const { fixture, html } = render();
    type(html, 'street', 'Rua das Flores');

    type(html, 'postalCode', '89010000');
    http.expectOne(`${LOOKUP}89010000`).flush({}, { status, statusText });
    fixture.detectChanges();

    expect(notice(html)).toBe(message);
    expect(address(fixture).street).toBe('Rua das Flores');
    type(html, 'neighborhood', 'Itoupava');
    expect(address(fixture).neighborhood).toBe('Itoupava');
    expect((control(html, 'neighborhood') as HTMLInputElement).disabled).toBe(false);
  });

  it.each([
    [404, 'Not Found', NOT_FOUND_NOTICE],
    [503, 'Service Unavailable', UNAVAILABLE_NOTICE],
    [429, 'Too Many Requests', UNAVAILABLE_NOTICE],
  ])(
    'mostra a falha %i como alerta em vermelho, com ícone, como os outros erros do formulário',
    (status, statusText, message) => {
      const { fixture, html } = render();

      type(html, 'postalCode', '89010000');
      http.expectOne(`${LOOKUP}89010000`).flush({}, { status, statusText });
      fixture.detectChanges();

      const warning = alert(html)!;
      expect(warning.textContent).toContain(message);
      expect(warning.classList).toContain('text-error');
      expect(warning.querySelector('mat-icon')).not.toBeNull();
    },
  );

  it('mostra a falta de rede como alerta em vermelho', () => {
    const { fixture, html } = render();

    type(html, 'postalCode', '89010000');
    http.expectOne(`${LOOKUP}89010000`).error(new ProgressEvent('error'));
    fixture.detectChanges();

    expect(alert(html)?.textContent).toContain(UNAVAILABLE_NOTICE);
    expect(alert(html)?.classList).toContain('text-error');
  });

  it('mostra "buscando" e "preenchido" como texto comum, sem alerta e sem vermelho', () => {
    const { fixture, html } = render();

    type(html, 'postalCode', '89010000');
    fixture.detectChanges();
    expect(alert(html)).toBeNull();
    expect(html.querySelector('[role="status"]')?.textContent).toContain('Buscando o endereço');
    expect(html.querySelector('[role="status"] .text-error')).toBeNull();

    http.expectOne(`${LOOKUP}89010000`).flush(BLUMENAU);
    fixture.detectChanges();
    expect(alert(html)).toBeNull();
    expect(html.querySelector('[role="status"]')?.textContent).toContain('Endereço preenchido');
    expect(html.querySelector('[role="status"] .text-error')).toBeNull();
  });

  it('tira o alerta quando o CEP seguinte é encontrado', () => {
    const { fixture, html } = render();
    type(html, 'postalCode', '89010000');
    http.expectOne(`${LOOKUP}89010000`).flush({}, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();
    expect(alert(html)).not.toBeNull();

    type(html, 'postalCode', '89020000');
    http.expectOne(`${LOOKUP}89020000`).flush(BLUMENAU);
    fixture.detectChanges();

    expect(alert(html)).toBeNull();
    expect(notice(html)).toContain('Endereço preenchido');
  });

  it('tira o alerta quando o CEP deixa de estar completo', () => {
    const { fixture, html } = render();
    type(html, 'postalCode', '89010000');
    http
      .expectOne(`${LOOKUP}89010000`)
      .flush({}, { status: 503, statusText: 'Service Unavailable' });
    fixture.detectChanges();
    expect(alert(html)).not.toBeNull();

    type(html, 'postalCode', '8901');
    fixture.detectChanges();

    expect(alert(html)).toBeNull();
  });

  it('avisa que a consulta está indisponível quando não há rede', () => {
    const { fixture, html } = render();

    type(html, 'postalCode', '89010000');
    http.expectOne(`${LOOKUP}89010000`).error(new ProgressEvent('error'));
    fixture.detectChanges();

    expect(notice(html)).toBe(UNAVAILABLE_NOTICE);
  });

  it('troca o endereço quando o CEP muda, e mantém o número e o complemento da pessoa', () => {
    const { fixture, html } = render();
    type(html, 'postalCode', '89010000');
    http.expectOne(`${LOOKUP}89010000`).flush(BLUMENAU);
    type(html, 'number', '120');
    type(html, 'complement', 'Sala 2');

    type(html, 'postalCode', '89201000');
    http.expectOne(`${LOOKUP}89201000`).flush(JOINVILLE);

    expect(address(fixture)).toEqual({
      postalCode: '89201-000',
      street: 'Rua do Príncipe',
      number: '120',
      complement: 'Sala 2',
      neighborhood: 'Centro',
      city: 'Joinville',
      state: 'SC',
    });
  });

  it('não apaga a rua e o bairro que a pessoa digitou quando o CEP não os informa', () => {
    const { fixture, html } = render();
    type(html, 'street', 'Rua das Flores');
    type(html, 'neighborhood', 'Itoupava');

    type(html, 'postalCode', '69900000');
    http.expectOne(`${LOOKUP}69900000`).flush({
      postalCode: '69900000',
      street: '',
      neighborhood: '',
      city: 'Rio Branco',
      state: 'AC',
    });

    expect(address(fixture)).toMatchObject({
      street: 'Rua das Flores',
      neighborhood: 'Itoupava',
      city: 'Rio Branco',
      state: 'AC',
    });
  });

  it('descarta a consulta antiga quando a pessoa troca o CEP antes da resposta', () => {
    const { fixture, html } = render();
    type(html, 'postalCode', '89010000');
    const stale = http.expectOne(`${LOOKUP}89010000`);

    type(html, 'postalCode', '89201000');
    http.expectOne(`${LOOKUP}89201000`).flush(JOINVILLE);

    expect(stale.cancelled).toBe(true);
    expect(address(fixture).city).toBe('Joinville');
  });

  it('tira o aviso quando o CEP deixa de estar completo', () => {
    const { fixture, html } = render();
    type(html, 'postalCode', '89010000');
    http.expectOne(`${LOOKUP}89010000`).flush(BLUMENAU);
    fixture.detectChanges();
    expect(notice(html)).not.toBe('');

    type(html, 'postalCode', '8901');
    fixture.detectChanges();

    expect(notice(html)).toBe('');
  });

  it('preenche cada endereço por conta própria quando há dois no formulário', () => {
    const { fixture, html, sections } = renderTwo();

    type(sections[1], 'postalCode', '89010000');
    http.expectOne(`${LOOKUP}89010000`).flush(BLUMENAU);
    fixture.detectChanges();

    const value = fixture.componentInstance.form.getRawValue();
    expect(value.condominium.address.street).toBe('Rua XV de Novembro');
    expect(value.manager.address.street).toBe('');
    expect(notice(sections[1])).toContain('Endereço preenchido');
    expect(notice(html.querySelector<HTMLElement>('[formGroupName="manager"]')!)).toBe('');
  });

  it('não consulta quando o grupo do endereço é desligado ("mesmo endereço")', () => {
    const { fixture } = renderTwo();
    const group = fixture.componentInstance.form.controls.condominium.controls.address;
    group.controls.postalCode.setValue('89010-000', { emitEvent: false });

    group.disable();

    http.expectNone((request) => request.url.startsWith(LOOKUP));
  });
});
