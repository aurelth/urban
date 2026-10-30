import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { jest } from '@jest/globals';
import { SessionStore } from '../../../core/auth/session.store';
import { MyCondominium } from '../../../core/condominium/condominium.models';
import { provideUrbanIcons } from '../../../core/theme/icons';
import { flushPromises } from '../../../testing/async';
import {
  REGISTERED_MANAGER,
  problem,
  signInAs,
  tokenResponse,
} from '../../../testing/auth.fixtures';
import { ProfilePage } from './profile.page';

type Field = HTMLInputElement | HTMLSelectElement;

const ADDRESS = {
  postalCode: '89010000',
  street: 'Rua das Flores',
  number: '120',
  complement: 'Apto 42',
  neighborhood: 'Centro',
  city: 'Blumenau',
  state: 'SC',
};

const MINE: MyCondominium = {
  condominiumId: REGISTERED_MANAGER.condominiumId!,
  version: '7.9',
  displayName: 'Marina',
  manager: {
    fullName: 'Marina Souza',
    maskedCpf: '***.982.247-**',
    birthDate: '1980-06-15',
    phone: '47991234567',
    address: ADDRESS,
  },
  condominium: {
    name: 'Residencial das Flores',
    cnpj: '11.222.333/0001-81',
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
};

interface Harness {
  fixture: ComponentFixture<ProfilePage>;
  http: HttpTestingController;
  html: HTMLElement;
  store: InstanceType<typeof SessionStore>;
}

async function setup(data: MyCondominium | 'pending' = MINE): Promise<Harness> {
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
  const store = TestBed.inject(SessionStore);
  await signInAs(store, http, REGISTERED_MANAGER);
  const fixture = TestBed.createComponent(ProfilePage);
  fixture.detectChanges();
  if (data !== 'pending') {
    http.expectOne('/api/condominiums/mine').flush(data);
    await settle(fixture);
  }
  return { fixture, http, html: fixture.nativeElement, store };
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    await flushPromises();
    TestBed.tick();
    fixture.detectChanges();
    await fixture.whenStable();
  }
}

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

function testId(html: HTMLElement, id: string): HTMLElement | null {
  return html.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function saveButton(html: HTMLElement): HTMLButtonElement {
  return html.querySelector<HTMLButtonElement>('form button[type="submit"]')!;
}

function alertTexts(html: HTMLElement): string[] {
  return [...html.querySelectorAll('[role="alert"]')].map((e) => e.textContent!.trim());
}

async function submit(h: Harness): Promise<void> {
  h.fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
  await settle(h.fixture);
}

function dialog(): HTMLElement | null {
  return document.querySelector('urban-password-confirm-dialog');
}

function typeInDialog(password: string): void {
  const input = dialog()!.querySelector<HTMLInputElement>('input')!;
  input.value = password;
  input.dispatchEvent(new Event('input'));
}

async function confirmInDialog(h: Harness, password = 'Senha-segura-123'): Promise<void> {
  typeInDialog(password);
  dialog()!.querySelector('form')!.dispatchEvent(new Event('submit'));
  await settle(h.fixture);
}

async function answerSessionReload(h: Harness, displayName = 'Marina'): Promise<void> {
  await flushPromises();
  h.http.expectOne('/api/auth/refresh').flush(tokenResponse('access-9'));
  await flushPromises();
  h.http.expectOne('/api/auth/me').flush({ ...REGISTERED_MANAGER, displayName });
  await settle(h.fixture);
}

function failure(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

function saved(changes: Partial<MyCondominium> = {}): MyCondominium {
  return { ...MINE, version: '8.9', ...changes };
}

describe('ProfilePage', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    document.querySelectorAll('.cdk-overlay-container').forEach((overlay) => overlay.remove());
    TestBed.inject(HttpTestingController).match(() => true);
  });

  describe('ao abrir', () => {
    it('mostra que esta carregando enquanto os dados nao chegam', async () => {
      const { html } = await setup('pending');

      expect(html.querySelector('h1')?.textContent?.trim()).toBe('Meus dados');
      expect(testId(html, 'loading')?.getAttribute('role')).toBe('status');
      expect(testId(html, 'loading')?.textContent).toContain('Carregando seus dados');
      expect(html.querySelector('form')).toBeNull();
    });

    it('busca os dados com GET e preenche o formulario como aparecem nos campos', async () => {
      const { html } = await setup();

      const manager = section(html, 'manager');
      const condominium = section(html, 'condominium');
      expect(field(manager, 'fullName').value).toBe('Marina Souza');
      expect(field(manager, 'displayName').value).toBe('Marina');
      expect(field(manager, 'birthDate').value).toBe('15/06/1980');
      expect(field(manager, 'phone').value).toBe('(47) 99123-4567');
      expect(field(manager, 'postalCode').value).toBe('89010-000');
      expect(field(manager, 'street').value).toBe('Rua das Flores');
      expect(field(manager, 'number').value).toBe('120');
      expect(field(manager, 'complement').value).toBe('Apto 42');
      expect(field(manager, 'state').value).toBe('SC');
      expect(field(condominium, 'name').value).toBe('Residencial das Flores');
      expect(field(condominium, 'postalCode').value).toBe('89012-100');
      expect(field(condominium, 'street').value).toBe('Rua XV de Novembro');
      expect(field(condominium, 'complement').value).toBe('');
      expect(html.querySelector('[role="alert"]')).toBeNull();
    });

    it('mostra o CPF mascarado, sem campo para digitar', async () => {
      const { html } = await setup();

      expect(testId(html, 'cpf-masked')?.textContent?.trim()).toBe('***.982.247-**');
      expect(field(section(html, 'manager'), 'cpf')).toBeNull();
      expect(html.textContent).not.toContain('52998224725');
      expect(testId(html, 'change-cpf')?.textContent?.trim()).toBe('Alterar CPF');
    });

    it('mostra o CNPJ como texto, porque ele nao pode ser alterado', async () => {
      const { html } = await setup();

      expect(testId(html, 'cnpj')?.textContent).toContain('11.222.333/0001-81');
      expect(field(section(html, 'condominium'), 'cnpj')).toBeNull();
      expect(section(html, 'condominium').textContent).toContain('não pode ser alterado');
    });

    it('carregar os dados nao dispara a busca de CEP nem mexe no numero', async () => {
      const { fixture, html, http } = await setup();
      await settle(fixture);

      http.expectNone('/api/postal-codes/89010000');
      http.expectNone('/api/postal-codes/89012100');
      expect(field(section(html, 'manager'), 'number').value).toBe('120');
      expect(field(section(html, 'condominium'), 'number').value).toBe('500');
    });

    it('trocar o CEP depois disso preenche rua, bairro, cidade e UF e preserva o numero', async () => {
      const { fixture, html, http } = await setup();
      const manager = section(html, 'manager');

      fill(manager, 'postalCode', '89012100');
      http.expectOne('/api/postal-codes/89012100').flush({
        postalCode: '89012100',
        street: 'Rua XV de Novembro',
        neighborhood: 'Centro',
        city: 'Joinville',
        state: 'SC',
      });
      await settle(fixture);

      expect(field(manager, 'street').value).toBe('Rua XV de Novembro');
      expect(field(manager, 'city').value).toBe('Joinville');
      expect(field(manager, 'number').value).toBe('120');
    });

    it('avisa quando o cadastro do condominio nao existe, sem mostrar o formulario', async () => {
      const { fixture, html, http } = await setup('pending');
      const [body, options] = failure(404, 'condominium.not_found');

      http.expectOne('/api/condominiums/mine').flush(body, options);
      await settle(fixture);

      expect(testId(html, 'not-found')?.getAttribute('role')).toBe('alert');
      expect(testId(html, 'not-found')?.textContent).toContain(
        'Não encontramos o cadastro do seu condomínio',
      );
      expect(html.querySelector('form')).toBeNull();
    });

    it('avisa quando a busca falha e deixa tentar de novo', async () => {
      const { fixture, html, http } = await setup('pending');
      http.expectOne('/api/condominiums/mine').flush(null, { status: 503, statusText: 'Erro' });
      await settle(fixture);
      expect(testId(html, 'load-error')?.textContent).toContain(
        'Não foi possível carregar seus dados',
      );

      testId(html, 'retry')!.click();
      http.expectOne('/api/condominiums/mine').flush(MINE);
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(field(section(html, 'manager'), 'fullName').value).toBe('Marina Souza');
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
      const manager = section(html, 'manager');

      fill(manager, 'phone', '47988887766');
      fixture.detectChanges();
      expect(saveButton(html).disabled).toBe(false);

      fill(manager, 'phone', '47991234567');
      fixture.detectChanges();
      expect(saveButton(html).disabled).toBe(true);
    });

    it('mudar so espacos nao conta como mudanca', async () => {
      const { fixture, html } = await setup();

      fill(section(html, 'manager'), 'displayName', '  Marina ');
      fixture.detectChanges();

      expect(saveButton(html).disabled).toBe(true);
    });

    it('fica desabilitado e troca o texto enquanto salva', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'phone', '47988887766');

      await submit(h);

      expect(saveButton(h.html).disabled).toBe(true);
      expect(saveButton(h.html).textContent?.trim()).toBe('Salvando');
      h.http.expectOne('/api/condominiums/mine');
    });
  });

  describe('alterar o CPF', () => {
    it('so vira campo quando a pessoa pede, vazio e com a mascara de CPF', async () => {
      const { fixture, html } = await setup();

      testId(html, 'change-cpf')!.click();
      fixture.detectChanges();

      const input = field(section(html, 'manager'), 'cpf');
      expect(input).not.toBeNull();
      expect(input.value).toBe('');
      expect(testId(html, 'cpf-masked')).toBeNull();
      fill(section(html, 'manager'), 'cpf', '11144477735');
      expect(input.value).toBe('111.444.777-35');
    });

    it('manter o CPF atual volta ao texto mascarado e esquece o que foi digitado', async () => {
      const { fixture, html } = await setup();
      testId(html, 'change-cpf')!.click();
      fixture.detectChanges();
      fill(section(html, 'manager'), 'cpf', '11144477735');
      fixture.detectChanges();
      expect(saveButton(html).disabled).toBe(false);

      testId(html, 'keep-cpf')!.click();
      fixture.detectChanges();

      expect(testId(html, 'cpf-masked')?.textContent?.trim()).toBe('***.982.247-**');
      expect(field(section(html, 'manager'), 'cpf')).toBeNull();
      expect(saveButton(html).disabled).toBe(true);
    });

    it('recusa um CPF invalido sem abrir o dialogo nem chamar a API', async () => {
      const h = await setup();
      testId(h.html, 'change-cpf')!.click();
      h.fixture.detectChanges();
      fill(section(h.html, 'manager'), 'cpf', '11111111111');

      await submit(h);

      expect(dialog()).toBeNull();
      h.http.expectNone('/api/condominiums/mine/manager-identity');
      expect([...h.html.querySelectorAll('mat-error')].map((e) => e.textContent!.trim())).toContain(
        'Informe um CPF válido',
      );
    });
  });

  describe('salvar os campos livres', () => {
    it('manda o PUT com a versao lida, sem pedir a senha', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'phone', '47988887766');
      fill(section(h.html, 'manager'), 'displayName', '  Mari  ');
      fill(section(h.html, 'condominium'), 'name', 'Residencial Novo');

      await submit(h);
      const request = h.http.expectOne('/api/condominiums/mine');

      expect(dialog()).toBeNull();
      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual({
        version: '7.9',
        displayName: 'Mari',
        phone: '47988887766',
        managerAddress: ADDRESS,
        condominiumName: 'Residencial Novo',
        condominiumAddress: { ...MINE.condominium.address, complement: null },
      });
    });

    it('nao leva CPF, nome completo, data de nascimento nem CNPJ', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'phone', '47988887766');

      await submit(h);
      const body = h.http.expectOne('/api/condominiums/mine').request.body as Record<
        string,
        unknown
      >;

      for (const forbidden of ['cpf', 'fullName', 'birthDate', 'cnpj', 'password']) {
        expect(Object.keys(body)).not.toContain(forbidden);
      }
    });

    it('ao salvar, renova a sessao e o cabecalho passa a mostrar o nome novo', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'displayName', 'Mari');
      await submit(h);

      h.http.expectOne('/api/condominiums/mine').flush(saved({ displayName: 'Mari' }));
      await answerSessionReload(h, 'Mari');

      expect(h.store.user()?.displayName).toBe('Mari');
    });

    it('avisa em verde que os dados foram salvos, com icone e texto', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'phone', '47988887766');
      await submit(h);

      h.http.expectOne('/api/condominiums/mine').flush(saved());
      await answerSessionReload(h);

      const notice = testId(h.html, 'saved');
      expect(notice?.getAttribute('role')).toBe('status');
      expect(notice?.textContent).toContain('Dados salvos');
      expect(notice?.querySelector('.text-success mat-icon')).not.toBeNull();
    });

    it('depois de salvar, o botao volta a ficar desabilitado e o proximo envio usa a versao nova', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'phone', '47988887766');
      await submit(h);
      h.http
        .expectOne('/api/condominiums/mine')
        .flush(saved({ manager: { ...MINE.manager, phone: '47988887766' } }));
      await answerSessionReload(h);
      expect(saveButton(h.html).disabled).toBe(true);

      fill(section(h.html, 'manager'), 'phone', '47977776666');
      await submit(h);

      expect(h.http.expectOne('/api/condominiums/mine').request.body.version).toBe('8.9');
    });

    it('o aviso de salvo some quando a pessoa volta a editar', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'phone', '47988887766');
      await submit(h);
      h.http.expectOne('/api/condominiums/mine').flush(saved());
      await answerSessionReload(h);
      expect(testId(h.html, 'saved')).not.toBeNull();

      fill(section(h.html, 'manager'), 'phone', '47977776666');
      h.fixture.detectChanges();

      expect(testId(h.html, 'saved')).toBeNull();
    });

    it('nao envia nada e mostra o que falta quando um campo esta invalido', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'displayName', '');
      fill(section(h.html, 'manager'), 'phone', '4731234567');

      await submit(h);

      h.http.expectNone('/api/condominiums/mine');
      const messages = [...h.html.querySelectorAll('mat-error')].map((e) => e.textContent!.trim());
      expect(messages).toContain('Informe como prefere ser chamado');
      expect(messages).toContain('Informe um celular com DDD');
      expect(document.activeElement).toBe(field(section(h.html, 'manager'), 'displayName'));
    });

    it('mostra o texto da interface quando a API recusa um campo e deixa corrigir', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'phone', '47988887766');
      await submit(h);
      const [body, options] = failure(400, 'condominium.phone.mobile_required');

      h.http.expectOne('/api/condominiums/mine').flush(body, options);
      await settle(h.fixture);

      expect(alertTexts(h.html)).toEqual(['Informe um celular com DDD']);
      expect(saveButton(h.html).disabled).toBe(false);
      expect(testId(h.html, 'saved')).toBeNull();
      h.http.expectNone('/api/auth/refresh');
    });

    it('pede para esperar quando ha tentativas demais', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'phone', '47988887766');
      await submit(h);

      h.http.expectOne('/api/condominiums/mine').flush(problem(429, 'rate_limited'), {
        status: 429,
        statusText: 'Too Many Requests',
        headers: { 'Retry-After': '30' },
      });
      await settle(h.fixture);

      expect(alertTexts(h.html)[0]).toContain('30');
    });
  });

  describe('salvar os dados sensiveis', () => {
    it.each([
      ['o nome completo', 'fullName', 'Marina Souza Lima'],
      ['a data de nascimento', 'birthDate', '16061980'],
    ])('trocar %s pede a senha antes de salvar', async (_name, control, value) => {
      const h = await setup();
      fill(section(h.html, 'manager'), control, value);

      await submit(h);

      expect(dialog()).not.toBeNull();
      expect(dialog()?.textContent).toContain('Confirme com a sua senha');
      h.http.expectNone('/api/condominiums/mine/manager-identity');
    });

    it('manda a senha e o CPF novo so em digitos, e nao chama o PUT dos campos livres', async () => {
      const h = await setup();
      testId(h.html, 'change-cpf')!.click();
      h.fixture.detectChanges();
      fill(section(h.html, 'manager'), 'cpf', '11144477735');
      await submit(h);

      await confirmInDialog(h);
      const request = h.http.expectOne('/api/condominiums/mine/manager-identity');

      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual({
        version: '7.9',
        password: 'Senha-segura-123',
        fullName: 'Marina Souza',
        birthDate: '1980-06-15',
        cpf: '11144477735',
      });
      h.http.expectNone('/api/condominiums/mine');
    });

    it('sem CPF novo, o corpo nao leva CPF', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'fullName', 'Marina Souza Lima');
      await submit(h);

      await confirmInDialog(h);
      const body = h.http.expectOne('/api/condominiums/mine/manager-identity').request
        .body as Record<string, unknown>;

      expect(Object.keys(body)).not.toContain('cpf');
    });

    it('com sucesso, fecha o dialogo, renova a sessao, avisa e volta o CPF para o texto mascarado', async () => {
      const h = await setup();
      testId(h.html, 'change-cpf')!.click();
      h.fixture.detectChanges();
      fill(section(h.html, 'manager'), 'cpf', '11144477735');
      await submit(h);
      await confirmInDialog(h);

      h.http
        .expectOne('/api/condominiums/mine/manager-identity')
        .flush(saved({ manager: { ...MINE.manager, maskedCpf: '***.444.777-**' } }));
      await answerSessionReload(h);

      expect(dialog()).toBeNull();
      expect(testId(h.html, 'saved')?.textContent).toContain('Dados salvos');
      expect(testId(h.html, 'cpf-masked')?.textContent?.trim()).toBe('***.444.777-**');
      expect(field(section(h.html, 'manager'), 'cpf')).toBeNull();
      expect(saveButton(h.html).disabled).toBe(true);
    });

    it('com mudancas livres e sensiveis, grava primeiro os sensiveis e depois os livres, com a versao nova', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'fullName', 'Marina Souza Lima');
      fill(section(h.html, 'manager'), 'phone', '47988887766');
      await submit(h);
      await confirmInDialog(h);

      h.http
        .expectOne('/api/condominiums/mine/manager-identity')
        .flush(saved({ manager: { ...MINE.manager, fullName: 'Marina Souza Lima' } }));
      await settle(h.fixture);
      const free = h.http.expectOne('/api/condominiums/mine');
      expect(free.request.body.version).toBe('8.9');
      expect(free.request.body.phone).toBe('47988887766');
      free.flush({ ...saved(), version: '9.9' });
      await answerSessionReload(h);

      expect(dialog()).toBeNull();
      expect(testId(h.html, 'saved')).not.toBeNull();
    });

    it('senha incorreta aparece no dialogo, sem sair da conta nem chamar nada alem do PUT', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'fullName', 'Marina Souza Lima');
      await submit(h);
      await confirmInDialog(h, 'Senha-errada-999');
      const [body, options] = failure(403, 'identity.password.incorrect');

      h.http.expectOne('/api/condominiums/mine/manager-identity').flush(body, options);
      await settle(h.fixture);

      expect(dialog()?.querySelector('[role="alert"]')?.textContent?.trim()).toBe(
        'Senha incorreta',
      );
      expect(dialog()?.querySelector('input')?.value).toBe('');
      expect(h.store.status()).toBe('authenticated');
      h.http.expectNone('/api/auth/logout');
      h.http.expectNone('/api/auth/refresh');
      expect(saveButton(h.html).disabled).toBe(false);
    });

    it('depois da senha incorreta, tentar com a certa salva', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'fullName', 'Marina Souza Lima');
      await submit(h);
      await confirmInDialog(h, 'Senha-errada-999');
      const [body, options] = failure(403, 'identity.password.incorrect');
      h.http.expectOne('/api/condominiums/mine/manager-identity').flush(body, options);
      await settle(h.fixture);

      await confirmInDialog(h, 'Senha-segura-123');
      const retry = h.http.expectOne('/api/condominiums/mine/manager-identity');
      expect(retry.request.body.password).toBe('Senha-segura-123');
      retry.flush(saved({ manager: { ...MINE.manager, fullName: 'Marina Souza Lima' } }));
      await answerSessionReload(h);

      expect(dialog()).toBeNull();
    });

    it('conta bloqueada por tentativas aparece no dialogo', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'fullName', 'Marina Souza Lima');
      await submit(h);
      await confirmInDialog(h);
      const [body, options] = failure(423, 'identity.login.locked_out');

      h.http.expectOne('/api/condominiums/mine/manager-identity').flush(body, options);
      await settle(h.fixture);

      expect(dialog()?.querySelector('[role="alert"]')?.textContent).toContain(
        'Conta bloqueada por muitas tentativas',
      );
    });

    it('CPF repetido no condominio aparece no dialogo para a pessoa corrigir', async () => {
      const h = await setup();
      testId(h.html, 'change-cpf')!.click();
      h.fixture.detectChanges();
      fill(section(h.html, 'manager'), 'cpf', '11144477735');
      await submit(h);
      await confirmInDialog(h);
      const [body, options] = failure(409, 'condominium.cpf.already_registered');

      h.http.expectOne('/api/condominiums/mine/manager-identity').flush(body, options);
      await settle(h.fixture);

      expect(dialog()?.querySelector('[role="alert"]')?.textContent).toContain(
        'CPF já está cadastrado',
      );
      expect(testId(h.html, 'conflict')).toBeNull();
    });

    it('cancelar o dialogo nao salva nada e mantem o que a pessoa digitou', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'fullName', 'Marina Souza Lima');
      await submit(h);

      dialog()!.querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();
      await settle(h.fixture);

      h.http.expectNone('/api/condominiums/mine/manager-identity');
      expect(dialog()).toBeNull();
      expect(field(section(h.html, 'manager'), 'fullName').value).toBe('Marina Souza Lima');
      expect(saveButton(h.html).disabled).toBe(false);
    });

    it('a senha digitada nao fica no formulario da pagina nem no HTML', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'fullName', 'Marina Souza Lima');
      await submit(h);

      await confirmInDialog(h, 'Senha-secreta-777');
      h.http
        .expectOne('/api/condominiums/mine/manager-identity')
        .flush(saved({ manager: { ...MINE.manager, fullName: 'Marina Souza Lima' } }));
      await answerSessionReload(h);

      expect(document.body.innerHTML).not.toContain('Senha-secreta-777');
    });
  });

  describe('conflito de versao', () => {
    async function conflictOnFreeSave(): Promise<Harness> {
      const h = await setup();
      fill(section(h.html, 'manager'), 'phone', '47988887766');
      await submit(h);
      const [body, options] = failure(409, 'concurrency.conflict');
      h.http.expectOne('/api/condominiums/mine').flush(body, options);
      await settle(h.fixture);
      return h;
    }

    it('avisa que os dados mudaram em outro lugar e oferece recarregar, sem perder o que foi digitado', async () => {
      const h = await conflictOnFreeSave();

      const warning = testId(h.html, 'conflict');
      expect(warning?.getAttribute('role')).toBe('alert');
      expect(warning?.textContent).toContain('Os dados foram alterados em outro lugar');
      expect(testId(h.html, 'reload')?.textContent?.trim()).toBe('Recarregar dados');
      expect(field(section(h.html, 'manager'), 'phone').value).toBe('(47) 98888-7766');
    });

    it('recarregar busca os dados de novo, troca o formulario e tira o aviso', async () => {
      const h = await conflictOnFreeSave();

      testId(h.html, 'reload')!.click();
      h.http.expectOne('/api/condominiums/mine').flush({
        ...MINE,
        version: '9.9',
        manager: { ...MINE.manager, phone: '47955554444' },
      });
      await settle(h.fixture);

      expect(testId(h.html, 'conflict')).toBeNull();
      expect(field(section(h.html, 'manager'), 'phone').value).toBe('(47) 95555-4444');
      expect(saveButton(h.html).disabled).toBe(true);
    });

    it('depois de recarregar, o proximo envio usa a versao nova', async () => {
      const h = await conflictOnFreeSave();
      testId(h.html, 'reload')!.click();
      h.http.expectOne('/api/condominiums/mine').flush({ ...MINE, version: '9.9' });
      await settle(h.fixture);
      fill(section(h.html, 'manager'), 'phone', '47977776666');

      await submit(h);

      expect(h.http.expectOne('/api/condominiums/mine').request.body.version).toBe('9.9');
    });

    it('o conflito ao salvar dados sensiveis fecha o dialogo e mostra o mesmo aviso', async () => {
      const h = await setup();
      fill(section(h.html, 'manager'), 'fullName', 'Marina Souza Lima');
      await submit(h);
      await confirmInDialog(h);
      const [body, options] = failure(409, 'concurrency.conflict');

      h.http.expectOne('/api/condominiums/mine/manager-identity').flush(body, options);
      await settle(h.fixture);

      expect(dialog()).toBeNull();
      expect(testId(h.html, 'conflict')).not.toBeNull();
      h.http.expectNone('/api/condominiums/mine');
    });
  });
});
