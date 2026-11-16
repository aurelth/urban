import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { PaymentInstructions } from '../../core/finance/payment-instructions.models';
import { provideUrbanIcons } from '../../core/theme/icons';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { fieldValueOf, setFieldValue } from '../../testing/menu-select';
import { NO_INSTRUCTIONS, paymentInstructions } from '../../testing/payment-instructions.fixtures';
import { PaymentInstructionsPage } from './payment-instructions.page';

const URL = '/api/finance/payment-instructions';

interface Harness {
  fixture: ComponentFixture<PaymentInstructionsPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    await flushPromises();
    TestBed.tick();
    fixture.detectChanges();
    await fixture.whenStable();
  }
}

async function setup(
  data: PaymentInstructions | 'pending' = paymentInstructions(),
): Promise<Harness> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
    ],
  });
  const http = TestBed.inject(HttpTestingController);
  const fixture = TestBed.createComponent(PaymentInstructionsPage);
  fixture.detectChanges();
  if (data !== 'pending') {
    http.expectOne(URL).flush(data);
    await settle(fixture);
  }
  return { fixture, http, html: fixture.nativeElement };
}

function section(html: HTMLElement, name: 'pix' | 'account'): HTMLElement {
  return html.querySelector<HTMLElement>(`[data-testid="${name}-section"]`)!;
}

function field(html: HTMLElement, name: string): HTMLInputElement {
  return html.querySelector<HTMLInputElement>(`[formControlName="${name}"]`)!;
}

function fill(html: HTMLElement, name: string, value: string): void {
  setFieldValue(field(html, name), value);
}

function testId(html: HTMLElement, id: string): HTMLElement | null {
  return html.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function saveButton(html: HTMLElement): HTMLButtonElement {
  return html.querySelector<HTMLButtonElement>('form button[type="submit"]')!;
}

function errorTexts(html: HTMLElement): string[] {
  return [...html.querySelectorAll('mat-error')].map((element) => element.textContent!.trim());
}

function alertTexts(html: HTMLElement): string[] {
  return [...html.querySelectorAll('[role="alert"]')].map((e) => e.textContent!.trim());
}

async function submit(h: Harness): Promise<void> {
  h.html.querySelector('form')!.dispatchEvent(new Event('submit'));
  await settle(h.fixture);
}

function failure(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

describe('PaymentInstructionsPage', () => {
  afterEach(() => TestBed.inject(HttpTestingController).match(() => true));

  describe('ao abrir', () => {
    it('mostra que esta carregando enquanto os dados nao chegam', async () => {
      const { html } = await setup('pending');

      expect(html.querySelector('h1')?.textContent?.trim()).toBe('Dados para pagamento');
      expect(testId(html, 'loading')?.getAttribute('role')).toBe('status');
      expect(testId(html, 'loading')?.textContent).toContain('Carregando os dados para pagamento');
      expect(html.querySelector('form')).toBeNull();
    });

    it('preenche a chave Pix e a conta como estao cadastradas', async () => {
      const { html } = await setup();

      expect(fieldValueOf(field(html, 'pixKind'))).toBe('cnpj');
      expect(field(html, 'pixKey').value).toBe('11.222.333/0001-81');
      expect(field(html, 'bank').value).toBe('Banco do Brasil');
      expect(field(html, 'agency').value).toBe('1234-5');
      expect(field(html, 'number').value).toBe('98765-4');
      expect(fieldValueOf(field(html, 'accountKind'))).toBe('checking');
      expect(alertTexts(html)).toEqual([]);
    });

    it('mostra quando os dados foram atualizados, na hora de Brasilia', async () => {
      const { html } = await setup();

      expect(testId(html, 'updated-at')?.textContent).toContain('16/11/2026 às 10:30');
    });

    it('sem nada cadastrado, mostra tudo vazio, sem campo de chave e sem data de atualizacao', async () => {
      const { html } = await setup(NO_INSTRUCTIONS);

      expect(fieldValueOf(field(html, 'pixKind'))).toBe('');
      expect(field(html, 'pixKey')).toBeNull();
      expect(field(html, 'bank').value).toBe('');
      expect(testId(html, 'updated-at')).toBeNull();
      expect(saveButton(html).disabled).toBe(true);
    });

    it('so a chave Pix cadastrada deixa a conta em branco', async () => {
      const { html } = await setup(paymentInstructions({ account: null }));

      expect(field(html, 'pixKey').value).toBe('11.222.333/0001-81');
      expect(field(html, 'bank').value).toBe('');
      expect(field(html, 'agency').value).toBe('');
      expect(field(html, 'number').value).toBe('');
    });

    it('avisa quando a busca falha e deixa tentar de novo', async () => {
      const { fixture, html, http } = await setup('pending');
      http.expectOne(URL).flush(null, { status: 503, statusText: 'Erro' });
      await settle(fixture);
      expect(testId(html, 'load-error')?.textContent).toContain(
        'Não foi possível carregar os dados para pagamento',
      );
      expect(html.querySelector('form')).toBeNull();

      testId(html, 'retry')!.click();
      http.expectOne(URL).flush(paymentInstructions());
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(field(html, 'bank').value).toBe('Banco do Brasil');
    });
  });

  describe('campo da chave Pix', () => {
    it('aparece ao escolher o tipo e some ao escolher "Nao informar"', async () => {
      const { fixture, html } = await setup(NO_INSTRUCTIONS);

      fill(html, 'pixKind', 'cpf');
      fixture.detectChanges();
      expect(field(html, 'pixKey')).not.toBeNull();

      fill(html, 'pixKind', '');
      fixture.detectChanges();
      expect(field(html, 'pixKey')).toBeNull();
    });

    it.each([
      ['cnpj', '11222333000181', '11.222.333/0001-81'],
      ['cpf', '52998224725', '529.982.247-25'],
      ['phone', '47991234567', '(47) 99123-4567'],
      ['email', 'caixa@predio.com.br', 'caixa@predio.com.br'],
    ])('com o tipo %s, digitar "%s" aparece como "%s"', async (kind, typed, shown) => {
      const { fixture, html } = await setup(NO_INSTRUCTIONS);

      fill(html, 'pixKind', kind);
      fixture.detectChanges();
      fill(html, 'pixKey', typed);

      expect(field(html, 'pixKey').value).toBe(shown);
    });

    it('trocar o tipo apaga o que estava digitado, porque a chave deixa de fazer sentido', async () => {
      const { fixture, html } = await setup(NO_INSTRUCTIONS);
      fill(html, 'pixKind', 'cpf');
      fixture.detectChanges();
      fill(html, 'pixKey', '52998224725');

      fill(html, 'pixKind', 'email');
      fixture.detectChanges();

      expect(field(html, 'pixKey').value).toBe('');
    });

    it('a chave aleatoria traz uma dica com o formato', async () => {
      const { fixture, html } = await setup(NO_INSTRUCTIONS);

      fill(html, 'pixKind', 'random');
      fixture.detectChanges();

      expect(section(html, 'pix').textContent).toContain('123e4567-e89b-12d3-a456-426614174000');
    });
  });

  describe('botao de salvar', () => {
    it('comeca desabilitado, porque nada mudou', async () => {
      const { html } = await setup();

      expect(saveButton(html).textContent?.trim()).toBe('Salvar alterações');
      expect(saveButton(html).disabled).toBe(true);
    });

    it('habilita quando algo muda e desabilita de novo quando a pessoa desfaz', async () => {
      const { fixture, html } = await setup();

      fill(html, 'agency', '9999');
      fixture.detectChanges();
      expect(saveButton(html).disabled).toBe(false);

      fill(html, 'agency', '1234-5');
      fixture.detectChanges();
      expect(saveButton(html).disabled).toBe(true);
    });

    it('mudar so espacos nao conta como mudanca', async () => {
      const { fixture, html } = await setup();

      fill(html, 'bank', '  Banco do Brasil ');
      fixture.detectChanges();

      expect(saveButton(html).disabled).toBe(true);
    });

    it('fica desabilitado e troca o texto enquanto salva', async () => {
      const h = await setup();
      fill(h.html, 'agency', '9999');

      await submit(h);

      expect(saveButton(h.html).disabled).toBe(true);
      expect(saveButton(h.html).textContent?.trim()).toBe('Salvando');
      h.http.expectOne(URL);
    });
  });

  describe('salvar', () => {
    it('envia a chave sem mascara e a conta, mostra "Dados salvos" e refaz o formulario', async () => {
      const h = await setup(NO_INSTRUCTIONS);
      fill(h.html, 'pixKind', 'cpf');
      h.fixture.detectChanges();
      fill(h.html, 'pixKey', '52998224725');
      fill(h.html, 'bank', ' Caixa ');
      fill(h.html, 'agency', '0001');
      fill(h.html, 'number', '12345-6');
      fill(h.html, 'accountKind', 'savings');

      await submit(h);
      const request = h.http.expectOne(URL);
      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual({
        pix: { kind: 'cpf', key: '52998224725' },
        account: { bank: 'Caixa', agency: '0001', number: '12345-6', kind: 'savings' },
      });
      request.flush({
        pix: { kind: 'cpf', key: '52998224725' },
        account: { bank: 'Caixa', agency: '0001', number: '12345-6', kind: 'savings' },
        updatedAt: '2026-11-16T14:00:00+00:00',
      });
      await settle(h.fixture);

      const saved = testId(h.html, 'saved')!;
      expect(saved.getAttribute('role')).toBe('status');
      expect(saved.textContent).toContain('Dados salvos');
      expect(field(h.html, 'pixKey').value).toBe('529.982.247-25');
      expect(testId(h.html, 'updated-at')?.textContent).toContain('16/11/2026 às 11:00');
      expect(saveButton(h.html).disabled).toBe(true);
    });

    it('"Nao informar" no tipo tira a chave Pix e deixa a conta como esta', async () => {
      const h = await setup();
      fill(h.html, 'pixKind', '');

      await submit(h);
      const request = h.http.expectOne(URL);
      expect(request.request.body).toEqual({
        pix: null,
        account: { bank: 'Banco do Brasil', agency: '1234-5', number: '98765-4', kind: 'checking' },
      });
      request.flush(paymentInstructions({ pix: null }));
      await settle(h.fixture);

      expect(fieldValueOf(field(h.html, 'pixKind'))).toBe('');
      expect(field(h.html, 'pixKey')).toBeNull();
    });

    it('esvaziar a conta tira a conta', async () => {
      const h = await setup();
      fill(h.html, 'bank', '');
      fill(h.html, 'agency', '');
      fill(h.html, 'number', '');
      fill(h.html, 'accountKind', '');

      await submit(h);
      const request = h.http.expectOne(URL);
      expect(request.request.body).toEqual({
        pix: { kind: 'cnpj', key: '11222333000181' },
        account: null,
      });
      request.flush(paymentInstructions({ account: null }));
      await settle(h.fixture);

      expect(field(h.html, 'bank').value).toBe('');
      expect(saveButton(h.html).disabled).toBe(true);
    });

    it('o aviso "Dados salvos" some quando a pessoa volta a editar', async () => {
      const h = await setup();
      fill(h.html, 'agency', '9999');
      await submit(h);
      h.http
        .expectOne(URL)
        .flush(
          paymentInstructions({ account: { ...paymentInstructions().account!, agency: '9999' } }),
        );
      await settle(h.fixture);
      expect(testId(h.html, 'saved')).not.toBeNull();

      fill(h.html, 'agency', '8888');
      h.fixture.detectChanges();

      expect(testId(h.html, 'saved')).toBeNull();
    });
  });

  describe('formulario invalido', () => {
    it('nao chama a API, mostra o erro de cada campo e leva o foco ao primeiro', async () => {
      const h = await setup();
      fill(h.html, 'pixKey', '11.222.333/0001');
      fill(h.html, 'agency', 'abc');
      fill(h.html, 'number', '');

      await submit(h);

      h.http.expectNone(URL);
      expect(errorTexts(h.html)).toEqual(
        expect.arrayContaining([
          'Informe um CNPJ válido, com 14 caracteres',
          'Informe a agência com até 5 dígitos, e o dígito verificador depois de um hífen, se tiver',
          'Informe o número da conta',
        ]),
      );
      expect(document.activeElement).toBe(field(h.html, 'pixKey'));
    });

    it('a chave Pix em branco pede a chave', async () => {
      const h = await setup(NO_INSTRUCTIONS);
      fill(h.html, 'pixKind', 'email');
      h.fixture.detectChanges();
      fill(h.html, 'bank', 'Caixa');

      await submit(h);

      h.http.expectNone(URL);
      expect(errorTexts(h.html)).toContain('Informe a chave Pix');
    });

    it('conta comecada pede o que falta: agencia, conta e tipo', async () => {
      const h = await setup(NO_INSTRUCTIONS);
      fill(h.html, 'bank', 'Caixa');

      await submit(h);

      h.http.expectNone(URL);
      expect(errorTexts(h.html)).toEqual(
        expect.arrayContaining([
          'Informe a agência',
          'Informe o número da conta',
          'Escolha o tipo da conta',
        ]),
      );
    });

    it('corrigir o campo tira o erro e deixa salvar', async () => {
      const h = await setup();
      fill(h.html, 'agency', 'abc');
      await submit(h);
      expect(errorTexts(h.html).length).toBeGreaterThan(0);

      fill(h.html, 'agency', '1234');
      await submit(h);

      expect(errorTexts(h.html)).toEqual([]);
      h.http.expectOne(URL);
    });
  });

  describe('recusa da API', () => {
    it('mostra o texto do codigo no fim do formulario e mantem o que foi digitado', async () => {
      const h = await setup();
      fill(h.html, 'agency', '9999');
      await submit(h);
      const [body, options] = failure(400, 'finance.payment_instructions.pix_key_invalid');

      h.http.expectOne(URL).flush(body, options);
      await settle(h.fixture);

      expect(alertTexts(h.html)).toEqual(['A chave Pix não é válida para o tipo escolhido']);
      expect(field(h.html, 'agency').value).toBe('9999');
      expect(saveButton(h.html).disabled).toBe(false);
      expect(testId(h.html, 'saved')).toBeNull();
    });

    it('explica a falha de rede', async () => {
      const h = await setup();
      fill(h.html, 'agency', '9999');
      await submit(h);

      h.http.expectOne(URL).flush(null, { status: 503, statusText: 'Erro' });
      await settle(h.fixture);

      expect(alertTexts(h.html)).toHaveLength(1);
      expect(testId(h.html, 'saved')).toBeNull();
    });
  });
});
