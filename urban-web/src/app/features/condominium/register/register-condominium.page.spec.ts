import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { jest } from '@jest/globals';
import { SessionStore } from '../../../core/auth/session.store';
import { flushPromises } from '../../../testing/async';
import {
  MANAGER,
  REGISTERED_MANAGER,
  problem,
  signInAs,
  tokenResponse,
} from '../../../testing/auth.fixtures';
import { Harness, settle, setupComponent, submitForm } from '../../../testing/component.harness';
import { RegisterCondominiumPage } from './register-condominium.page';

type Field = HTMLInputElement | HTMLSelectElement;

function section(html: HTMLElement, name: 'manager' | 'condominium'): HTMLElement {
  return html.querySelector<HTMLElement>(`[data-testid="${name}-section"]`)!;
}

function field(root: HTMLElement, name: string): Field {
  return root.querySelector<Field>(`[formControlName="${name}"]`)!;
}

function fill(root: HTMLElement, name: string, value: string): void {
  const element = field(root, name);
  element.value = value;
  element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? 'change' : 'input'));
}

function fillAll(root: HTMLElement, values: Record<string, string>): void {
  for (const [name, value] of Object.entries(values)) {
    fill(root, name, value);
  }
}

const MANAGER_ADDRESS = {
  postalCode: '89010000',
  street: 'Rua das Flores',
  number: '120',
  complement: 'Apto 42',
  neighborhood: 'Centro',
  city: 'Blumenau',
  state: 'SC',
};

const CONDOMINIUM_ADDRESS = {
  postalCode: '89012100',
  street: 'Rua XV de Novembro',
  number: '500',
  complement: '',
  neighborhood: 'Centro',
  city: 'Blumenau',
  state: 'SC',
};

function fillValidForm(html: HTMLElement, sameAddress = false): void {
  fillAll(section(html, 'manager'), {
    fullName: 'Marina Souza',
    displayName: 'Marina',
    cpf: '52998224725',
    birthDate: '15061980',
    phone: '47991234567',
    ...MANAGER_ADDRESS,
  });
  fill(section(html, 'condominium'), 'name', 'Residencial das Flores');
  fill(section(html, 'condominium'), 'cnpj', '11222333000181');
  if (sameAddress) {
    html.querySelector<HTMLInputElement>('[data-testid="same-address"] input')!.click();
  } else {
    fillAll(section(html, 'condominium'), CONDOMINIUM_ADDRESS);
  }
}

const EXPECTED_ADDRESS = {
  postalCode: '89010000',
  street: 'Rua das Flores',
  number: '120',
  complement: 'Apto 42',
  neighborhood: 'Centro',
  city: 'Blumenau',
  state: 'SC',
};

const SUMMARY = {
  condominiumId: REGISTERED_MANAGER.condominiumId,
  name: 'Residencial das Flores',
  cnpj: '11.222.333/0001-81',
  managerCpf: '***.982.247-**',
};

function spyNavigate() {
  return jest.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
}

async function setup(): Promise<
  Harness<RegisterCondominiumPage> & {
    navigate: ReturnType<typeof spyNavigate>;
    store: InstanceType<typeof SessionStore>;
  }
> {
  const harness = setupComponent(RegisterCondominiumPage);
  const store = TestBed.inject(SessionStore);
  await signInAs(store, harness.http, MANAGER);
  const navigate = spyNavigate();
  harness.fixture.detectChanges();
  await harness.fixture.whenStable();
  return { ...harness, navigate, store };
}

function submitButton(html: HTMLElement): HTMLButtonElement {
  return html.querySelector<HTMLButtonElement>('button[type="submit"]')!;
}

function alertText(html: HTMLElement): string {
  return html.querySelector('[role="alert"]')?.textContent?.trim() ?? '';
}

async function answerSession(
  http: HttpTestingController,
  fixture: ComponentFixture<unknown>,
): Promise<void> {
  await flushPromises();
  http.expectOne('/api/auth/refresh').flush(tokenResponse('access-9'));
  await flushPromises();
  http.expectOne('/api/auth/me').flush(REGISTERED_MANAGER);
  await settle(fixture);
}

describe('RegisterCondominiumPage', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('pede os dados do síndico e os do condomínio, em duas seções', async () => {
    const { html } = await setup();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Cadastre seu condomínio');
    expect(section(html, 'manager').querySelector('h2')?.textContent?.trim()).toBe('Seus dados');
    expect(section(html, 'condominium').querySelector('h2')?.textContent?.trim()).toBe(
      'Dados do condomínio',
    );
    const labels = (root: HTMLElement): string[] =>
      [...root.querySelectorAll('mat-label')].map((label) => label.textContent!.trim());
    expect(labels(section(html, 'manager'))).toEqual([
      'Nome completo',
      'Como prefere ser chamado',
      'CPF',
      'Data de nascimento',
      'Celular',
      'CEP',
      'Rua',
      'Número',
      'Complemento',
      'Bairro',
      'Cidade',
      'UF',
    ]);
    expect(labels(section(html, 'condominium'))).toEqual([
      'Nome do condomínio',
      'CNPJ',
      'CEP',
      'Rua',
      'Número',
      'Complemento',
      'Bairro',
      'Cidade',
      'UF',
    ]);
  });

  it('formata CPF, data, celular, CEP e CNPJ enquanto a pessoa digita', async () => {
    const { html } = await setup();
    const manager = section(html, 'manager');
    const condominium = section(html, 'condominium');

    fillAll(manager, { cpf: '52998224725', birthDate: '15061980', phone: '47991234567' });
    fill(manager, 'postalCode', '89010000');
    fill(condominium, 'cnpj', '12abc34501de35');

    expect(field(manager, 'cpf').value).toBe('529.982.247-25');
    expect(field(manager, 'birthDate').value).toBe('15/06/1980');
    expect(field(manager, 'phone').value).toBe('(47) 99123-4567');
    expect(field(manager, 'postalCode').value).toBe('89010-000');
    expect(field(condominium, 'cnpj').value).toBe('12.ABC.345/01DE-35');
  });

  it('preenche pelo CEP só o endereço em que a pessoa digitou', async () => {
    const { fixture, html, http } = await setup();
    const manager = section(html, 'manager');
    const condominium = section(html, 'condominium');

    fill(manager, 'postalCode', '89010000');
    http.expectOne('/api/postal-codes/89010000').flush({
      postalCode: '89010000',
      street: 'Rua XV de Novembro',
      neighborhood: 'Centro',
      city: 'Blumenau',
      state: 'SC',
    });
    await settle(fixture);

    expect(field(manager, 'street').value).toBe('Rua XV de Novembro');
    expect(field(manager, 'city').value).toBe('Blumenau');
    expect(field(manager, 'number').value).toBe('');
    expect(field(condominium, 'street').value).toBe('');
    expect(field(condominium, 'city').value).toBe('');
  });

  it('não envia nada e mostra o que falta quando o formulário está incompleto', async () => {
    const { fixture, html, http } = await setup();

    await submitForm(fixture);
    fixture.detectChanges();

    http.expectNone('/api/condominiums');
    const messages = [...html.querySelectorAll('mat-error')].map((e) => e.textContent!.trim());
    expect(messages).toContain('Informe o nome completo');
    expect(messages).toContain('Informe como prefere ser chamado');
    expect(messages).toContain('Informe o CPF');
    expect(messages).toContain('Informe a data de nascimento');
    expect(messages).toContain('Informe o celular');
    expect(messages).toContain('Informe o nome do condomínio');
    expect(messages).toContain('Informe o CNPJ');
    expect(messages.filter((m) => m === 'Informe o CEP')).toHaveLength(2);
    expect(document.activeElement).toBe(field(section(html, 'manager'), 'fullName'));
  });

  it('avisa quando o CPF, o celular ou a data não servem', async () => {
    const { fixture, html } = await setup();
    const manager = section(html, 'manager');

    fillAll(manager, { cpf: '11111111111', phone: '4731234567', birthDate: '15062020' });
    fill(section(html, 'condominium'), 'cnpj', '11222333000180');
    await submitForm(fixture);
    fixture.detectChanges();

    const messages = [...html.querySelectorAll('mat-error')].map((e) => e.textContent!.trim());
    expect(messages).toContain('Informe um CPF válido');
    expect(messages).toContain('Informe um celular com DDD');
    expect(messages).toContain('O síndico precisa ter 18 anos ou mais');
    expect(messages).toContain('Informe um CNPJ válido');
  });

  it('pede nome e sobrenome e recusa números no como prefere ser chamado', async () => {
    const { fixture, html, http } = await setup();
    fillValidForm(html);
    fill(section(html, 'manager'), 'fullName', 'Marina');
    fill(section(html, 'manager'), 'displayName', 'Marina 3');

    await submitForm(fixture);
    fixture.detectChanges();

    http.expectNone('/api/condominiums');
    const messages = [...html.querySelectorAll('mat-error')].map((e) => e.textContent!.trim());
    expect(messages).toContain('Informe o nome e o sobrenome');
    expect(messages).toContain('Use de 2 a 40 letras, sem números');
  });

  it.each([
    ['A', false],
    ['AB', true],
    ['A'.repeat(120), true],
    ['A'.repeat(121), false],
  ])('confere o tamanho do nome do condomínio (%#)', async (name, accepted) => {
    const { fixture, html, http } = await setup();
    fillValidForm(html);
    fill(section(html, 'condominium'), 'name', name);

    await submitForm(fixture);
    fixture.detectChanges();

    if (accepted) {
      http.expectOne('/api/condominiums');
      expect(section(html, 'condominium').querySelector('mat-error')).toBeNull();
    } else {
      http.expectNone('/api/condominiums');
      expect(section(html, 'condominium').querySelector('mat-error')?.textContent?.trim()).toBe(
        'O nome deve ter de 2 a 120 caracteres',
      );
    }
  });

  it('deixa o navegador sugerir só o endereço do síndico', async () => {
    const { html } = await setup();

    expect(field(section(html, 'manager'), 'street').getAttribute('autocomplete')).toBe(
      'address-line1',
    );
    expect(field(section(html, 'condominium'), 'street').getAttribute('autocomplete')).toBe('off');
  });

  it('cadastra o condomínio, renova a sessão e leva ao painel', async () => {
    const { fixture, html, http, navigate, store } = await setup();
    fillValidForm(html);

    await submitForm(fixture);
    const request = http.expectOne('/api/condominiums');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      displayName: 'Marina',
      manager: {
        fullName: 'Marina Souza',
        cpf: '52998224725',
        birthDate: '1980-06-15',
        phone: '47991234567',
        address: EXPECTED_ADDRESS,
      },
      condominium: {
        name: 'Residencial das Flores',
        cnpj: '11222333000181',
        address: {
          postalCode: '89012100',
          street: 'Rua XV de Novembro',
          number: '500',
          complement: null,
          neighborhood: 'Centro',
          city: 'Blumenau',
          state: 'SC',
        },
      },
    });
    request.flush(SUMMARY, { status: 201, statusText: 'Created' });
    await answerSession(http, fixture);

    expect(store.user()).toEqual(REGISTERED_MANAGER);
    expect(store.needsCondominium()).toBe(false);
    expect(navigate).toHaveBeenCalledWith('/dashboard');
  });

  it('desabilita o botão e troca o texto enquanto o cadastro está em andamento', async () => {
    const { fixture, html, http } = await setup();
    fillValidForm(html);

    await submitForm(fixture);
    fixture.detectChanges();

    expect(submitButton(html).disabled).toBe(true);
    expect(submitButton(html).textContent?.trim()).toBe('Cadastrando');
    http.expectOne('/api/condominiums');
  });

  describe('endereço do condomínio igual ao do síndico', () => {
    it('esconde os campos do endereço do condomínio e envia o endereço do síndico nos dois', async () => {
      const { fixture, html, http } = await setup();
      fillValidForm(html, true);
      fixture.detectChanges();

      expect(section(html, 'condominium').querySelector('urban-address-fields')).toBeNull();
      await submitForm(fixture);
      const body = http.expectOne('/api/condominiums').request.body;

      expect(body.condominium.address).toEqual(EXPECTED_ADDRESS);
      expect(body.manager.address).toEqual(EXPECTED_ADDRESS);
    });

    it('volta a pedir o endereço do condomínio quando a pessoa desmarca', async () => {
      const { fixture, html, http } = await setup();
      fillValidForm(html, true);
      fixture.detectChanges();

      html.querySelector<HTMLInputElement>('[data-testid="same-address"] input')!.click();
      fixture.detectChanges();
      expect(section(html, 'condominium').querySelector('urban-address-fields')).not.toBeNull();

      await submitForm(fixture);
      fixture.detectChanges();

      http.expectNone('/api/condominiums');
      expect(document.activeElement).toBe(field(section(html, 'condominium'), 'postalCode'));
    });
  });

  describe('quando a API recusa', () => {
    async function submitWith(
      status: number,
      code: string,
    ): Promise<Awaited<ReturnType<typeof setup>>> {
      const harness = await setup();
      fillValidForm(harness.html);
      await submitForm(harness.fixture);
      harness.http
        .expectOne('/api/condominiums')
        .flush(problem(status, code), { status, statusText: 'Erro' });
      await settle(harness.fixture);
      return harness;
    }

    it('explica que o CNPJ já está cadastrado e deixa corrigir', async () => {
      const { html, http, navigate } = await submitWith(409, 'condominium.cnpj.already_registered');

      expect(alertText(html)).toBe(
        'Este CNPJ já está cadastrado. Peça um convite ao síndico atual',
      );
      expect(submitButton(html).disabled).toBe(false);
      expect(submitButton(html).textContent?.trim()).toBe('Cadastrar condomínio');
      http.expectNone('/api/auth/refresh');
      expect(navigate).not.toHaveBeenCalled();
    });

    it('tira o aviso anterior quando a pessoa envia de novo', async () => {
      const { fixture, html, http } = await submitWith(409, 'condominium.cnpj.already_registered');
      expect(alertText(html)).not.toBe('');

      await submitForm(fixture);
      fixture.detectChanges();

      expect(alertText(html)).toBe('');
      http.expectOne('/api/condominiums');
    });

    it('leva ao painel quem já cadastrou um condomínio, depois de renovar a sessão', async () => {
      const { fixture, http, navigate } = await submitWith(
        409,
        'condominium.manager.already_registered',
      );

      await answerSession(http, fixture);

      expect(navigate).toHaveBeenCalledWith('/dashboard');
    });

    it.each([
      ['condominium.cpf.invalid', 'Informe um CPF válido'],
      ['condominium.cnpj.invalid', 'Informe um CNPJ válido'],
      ['condominium.postal_code.invalid', 'Informe um CEP válido'],
      ['condominium.phone.mobile_required', 'Informe um celular com DDD'],
      ['condominium.person.full_name_invalid', 'Informe o nome e o sobrenome'],
      ['condominium.birth_date.invalid', 'Informe uma data de nascimento válida'],
      ['condominium.birth_date.under_age', 'O síndico precisa ter 18 anos ou mais'],
      ['condominium.name.invalid', 'Informe o nome do condomínio, com 2 a 120 caracteres'],
      ['condominium.address.street_invalid', 'Informe a rua'],
      ['condominium.address.number_invalid', 'Informe o número (use S/N quando não houver)'],
      ['condominium.address.complement_invalid', 'O complemento deve ter até 60 caracteres'],
      ['condominium.address.neighborhood_invalid', 'Informe o bairro'],
      ['condominium.address.city_invalid', 'Informe a cidade'],
      ['condominium.address.state_invalid', 'Informe a UF'],
      ['identity.display_name.invalid', 'Informe como prefere ser chamado, com 2 a 40 letras'],
      [
        'request.invalid',
        'Não foi possível ler os dados enviados. Confira o formato e tente de novo',
      ],
    ])('mostra o texto da interface para %s', async (code, message) => {
      const { html } = await submitWith(400, code);

      expect(alertText(html)).toBe(message);
    });

    it('explica que só o síndico cadastra o condomínio', async () => {
      const { html } = await submitWith(403, 'identity.user.not_manager');

      expect(alertText(html)).toBe('Só o síndico pode cadastrar o condomínio');
    });

    it('pede para esperar quando há tentativas demais', async () => {
      const { fixture, html, http } = await setup();
      fillValidForm(html);

      await submitForm(fixture);
      http.expectOne('/api/condominiums').flush(problem(429, 'rate_limited'), {
        status: 429,
        statusText: 'Too Many Requests',
        headers: { 'Retry-After': '30' },
      });
      await settle(fixture);

      expect(alertText(html)).toBe(
        'Muitas tentativas em pouco tempo. Aguarde 30 segundos e tente de novo',
      );
    });

    it('avisa quando não há conexão', async () => {
      const { fixture, html, http } = await setup();
      fillValidForm(html);

      await submitForm(fixture);
      http.expectOne('/api/condominiums').error(new ProgressEvent('error'));
      await settle(fixture);

      expect(alertText(html)).toBe(
        'Não foi possível conectar ao Urban. Verifique sua conexão e tente de novo',
      );
    });
  });

  it('deixa a sessão cair, para a casca levar à entrada, quando ela não pode ser renovada depois do cadastro', async () => {
    const { fixture, html, http, navigate, store } = await setup();
    fillValidForm(html);

    await submitForm(fixture);
    http.expectOne('/api/condominiums').flush(SUMMARY, { status: 201, statusText: 'Created' });
    await flushPromises();
    http
      .expectOne('/api/auth/refresh')
      .flush(problem(401, 'x'), { status: 401, statusText: 'Unauthorized' });
    await settle(fixture);

    expect(store.status()).toBe('anonymous');
    expect(store.expired()).toBe(true);
    expect(navigate).not.toHaveBeenCalled();
  });
});
