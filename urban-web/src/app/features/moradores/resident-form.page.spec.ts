import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { SessionStore } from '../../core/auth/session.store';
import { Resident } from '../../core/residents/residents.models';
import { provideUrbanIcons } from '../../core/theme/icons';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, signInAs } from '../../testing/auth.fixtures';
import {
  fieldValueOf,
  menuSelectLabel,
  menuSelectOptions,
  setFieldValue,
} from '../../testing/menu-select';
import { occupancy, resident } from '../../testing/residents.fixtures';
import { unit, unitPage } from '../../testing/units.fixtures';
import { ResidentFormPage } from './resident-form.page';

const BLOCK_A_101 = unit({ block: 'A', number: '101' });
const HOUSE_7 = unit({ block: null, number: '7', type: 'house' });

interface Harness {
  fixture: ComponentFixture<ResidentFormPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

async function configure(id?: string): Promise<Harness> {
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
  await signInAs(TestBed.inject(SessionStore), http, REGISTERED_MANAGER);
  const fixture = TestBed.createComponent(ResidentFormPage);
  if (id !== undefined) {
    fixture.componentRef.setInput('id', id);
  }
  fixture.detectChanges();
  return { fixture, http, html: fixture.nativeElement };
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    await flushPromises();
    TestBed.tick();
    fixture.detectChanges();
    await fixture.whenStable();
  }
}

/** Abre o cadastro (sem id) e responde a busca das unidades. */
async function openNew(units = [BLOCK_A_101, HOUSE_7]): Promise<Harness> {
  const harness = await configure();
  unitsRequest(harness.http).flush(unitPage(units, units.length, 1, 200));
  await settle(harness.fixture);
  return harness;
}

/** Abre a edição de um morador e responde a busca dele. */
async function openEdit(found: Resident = resident()): Promise<Harness> {
  const harness = await configure(found.id);
  harness.http.expectOne(`/api/residents/${found.id}`).flush(found);
  await settle(harness.fixture);
  return harness;
}

function unitsRequest(http: HttpTestingController): TestRequest {
  return http.expectOne((request) => request.url === '/api/units' && request.method === 'GET');
}

function testId(html: HTMLElement, id: string): HTMLElement | null {
  return html.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function field(html: HTMLElement, name: string): HTMLElement {
  return html.querySelector<HTMLElement>(`[formControlName="${name}"]`)!;
}

function fill(html: HTMLElement, name: string, value: string): void {
  setFieldValue(field(html, name), value);
}

function valueOf(html: HTMLElement, name: string): string {
  return fieldValueOf(field(html, name));
}

function submitButton(html: HTMLElement): HTMLButtonElement {
  return html.querySelector<HTMLButtonElement>('form button[type="submit"]')!;
}

async function submit(h: Harness): Promise<void> {
  h.html.querySelector('form')!.dispatchEvent(new Event('submit'));
  await settle(h.fixture);
}

function errorTexts(html: HTMLElement): string[] {
  return [...html.querySelectorAll('mat-error')].map((error) => error.textContent!.trim());
}

function alertTexts(html: HTMLElement): string[] {
  return [...html.querySelectorAll('[role="alert"]')].map((alert) => alert.textContent!.trim());
}

function fillRegistration(html: HTMLElement): void {
  fill(html, 'fullName', 'Maria Souza');
  fill(html, 'cpf', '52998224725');
  fill(html, 'birthDate', '20051990');
  fill(html, 'phone', '47998765432');
  fill(html, 'profession', 'Engenheira');
  fill(html, 'contactName', 'Carlos Souza');
  fill(html, 'contactRelationship', 'Irmão');
  fill(html, 'contactPhone', '4733334444');
  fill(html, 'unitId', BLOCK_A_101.id);
  fill(html, 'kind', 'tenant');
  fill(html, 'startDate', '01032024');
}

function reject(request: TestRequest, status: number, code: string): void {
  request.flush({ code }, { status, statusText: 'Erro' });
}

describe('ResidentFormPage, cadastro', () => {
  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    http.match((request) => request.url.startsWith('icons/'));
    http.verify();
  });

  it('mostra o título, as três seções e só os campos do cadastro', async () => {
    const { html } = await openNew();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Novo morador');
    expect(testId(html, 'person-section')).not.toBeNull();
    expect(testId(html, 'contact-section')).not.toBeNull();
    expect(testId(html, 'housing-section')).not.toBeNull();
    expect(field(html, 'cpf')).not.toBeNull();
    expect(testId(html, 'change-cpf')).toBeNull();
    expect(submitButton(html).textContent?.trim()).toBe('Cadastrar morador');
  });

  it('lista as unidades ativas e os tipos de vínculo nos menus', async () => {
    const { html } = await openNew();

    expect(menuSelectOptions(field(html, 'unitId')).map((option) => option.label)).toEqual([
      'Bloco A · 101',
      '7',
    ]);
    expect(menuSelectOptions(field(html, 'kind')).map((option) => option.label)).toEqual([
      'Proprietário',
      'Inquilino',
      'Dependente',
    ]);
  });

  it('sem unidade ativa explica o motivo e leva ao cadastro de unidades, sem formulário', async () => {
    const { html } = await openNew([]);

    expect(testId(html, 'no-units')?.textContent).toContain('Cadastre ao menos uma unidade');
    expect(testId(html, 'units-link')?.getAttribute('href')).toBe('/unidades');
    expect(html.querySelector('form')).toBeNull();
  });

  it('mostra o erro da busca das unidades e tenta de novo', async () => {
    const harness = await configure();
    unitsRequest(harness.http).flush(null, { status: 500, statusText: 'Erro' });
    await settle(harness.fixture);

    expect(testId(harness.html, 'load-error')).not.toBeNull();

    testId(harness.html, 'retry')!.click();
    unitsRequest(harness.http).flush(unitPage([BLOCK_A_101], 1, 1, 200));
    await settle(harness.fixture);

    expect(testId(harness.html, 'load-error')).toBeNull();
    expect(field(harness.html, 'fullName')).not.toBeNull();
  });

  it('enviar vazio não chama a API e mostra o que falta em cada campo obrigatório', async () => {
    const h = await openNew();

    await submit(h);

    expect(errorTexts(h.html)).toEqual(
      expect.arrayContaining([
        'Informe o nome completo',
        'Informe o CPF',
        'Informe a data de nascimento',
        'Informe o telefone',
        'Informe a profissão',
        'Informe o nome do contato',
        'Informe o parentesco',
        'Informe o telefone do contato',
        'Escolha a unidade',
        'Escolha o vínculo: proprietário, inquilino ou dependente',
        'Informe a data de entrada',
      ]),
    );
    expect(errorTexts(h.html)).not.toContain('Informe um e-mail válido, como nome@exemplo.com');
    // Nenhuma requisição de gravação saiu: o verify() do afterEach falharia.
  });

  it('avisa na hora do CPF e do telefone inválidos e da data de nascimento que não existe', async () => {
    const h = await openNew();

    fill(h.html, 'birthDate', '31022000');
    fill(h.html, 'cpf', '11111111111');
    fill(h.html, 'phone', '1234');
    for (const name of ['birthDate', 'cpf', 'phone']) {
      field(h.html, name).dispatchEvent(new Event('blur'));
    }
    await settle(h.fixture);

    expect(errorTexts(h.html)).toEqual(
      expect.arrayContaining([
        'Informe uma data válida, como 31/01/1980',
        'Informe um CPF válido',
        'Informe um telefone com DDD',
      ]),
    );
  });

  it('cadastra: manda só dígitos, a data em AAAA-MM-DD e mostra a confirmação com a unidade', async () => {
    const h = await openNew();
    fillRegistration(h.html);
    fill(h.html, 'email', 'maria@exemplo.com');
    await settle(h.fixture);

    await submit(h);

    const request = h.http.expectOne('/api/residents');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      fullName: 'Maria Souza',
      cpf: '52998224725',
      birthDate: '1990-05-20',
      phone: '47998765432',
      email: 'maria@exemplo.com',
      profession: 'Engenheira',
      emergencyContact: { name: 'Carlos Souza', relationship: 'Irmão', phone: '4733334444' },
      unitId: BLOCK_A_101.id,
      kind: 'tenant',
      startDate: '2024-03-01',
    });
    request.flush(
      resident({
        fullName: 'Maria Souza',
        occupancies: [
          occupancy({ unitId: BLOCK_A_101.id, unitBlock: 'A', unitNumber: '101', kind: 'tenant' }),
        ],
      }),
      { status: 201, statusText: 'Created' },
    );
    await settle(h.fixture);

    const created = testId(h.html, 'created')!;
    expect(created.textContent).toContain('Morador cadastrado');
    expect(created.textContent).toContain('Maria Souza');
    expect(created.textContent).toContain('Bloco A · 101');
    expect(testId(h.html, 'residents-link')?.getAttribute('href')).toBe('/moradores');
    expect(h.html.querySelector('form')).toBeNull();
  });

  it('o e-mail é opcional: em branco vai nulo', async () => {
    const h = await openNew();
    fillRegistration(h.html);
    await settle(h.fixture);

    await submit(h);

    const request = h.http.expectOne('/api/residents');
    expect(request.request.body.email).toBeNull();
    request.flush(resident(), { status: 201, statusText: 'Created' });
    await settle(h.fixture);
  });

  it('mostra o motivo da recusa da API sem apagar o que foi digitado', async () => {
    const h = await openNew();
    fillRegistration(h.html);
    await settle(h.fixture);

    await submit(h);
    reject(h.http.expectOne('/api/residents'), 409, 'condominium.cpf.already_registered');
    await settle(h.fixture);

    expect(alertTexts(h.html)).toContain('Este CPF já está cadastrado neste condomínio');
    expect(valueOf(h.html, 'fullName')).toBe('Maria Souza');
    expect(valueOf(h.html, 'cpf')).toBe('529.982.247-25');
    expect(testId(h.html, 'created')).toBeNull();
    expect(submitButton(h.html).disabled).toBe(false);
  });

  it('explica a unidade que foi desativada enquanto o formulário estava aberto', async () => {
    const h = await openNew();
    fillRegistration(h.html);
    await settle(h.fixture);

    await submit(h);
    reject(h.http.expectOne('/api/residents'), 409, 'unit.deactivated');
    await settle(h.fixture);

    expect(alertTexts(h.html).join(' ')).toContain('desativada');
  });

  it('"Cadastrar outro morador" volta ao formulário vazio, com as unidades já carregadas', async () => {
    const h = await openNew();
    fillRegistration(h.html);
    await settle(h.fixture);
    await submit(h);
    h.http.expectOne('/api/residents').flush(resident(), { status: 201, statusText: 'Created' });
    await settle(h.fixture);

    testId(h.html, 'create-another')!.click();
    await settle(h.fixture);

    expect(testId(h.html, 'created')).toBeNull();
    expect(valueOf(h.html, 'fullName')).toBe('');
    expect(menuSelectLabel(field(h.html, 'unitId'))).not.toContain('Bloco A');
    expect(menuSelectOptions(field(h.html, 'unitId'))).toHaveLength(2);
  });
});

describe('ResidentFormPage, edição', () => {
  const MARIA = resident({
    fullName: 'Maria Souza',
    maskedCpf: '***.456.789-**',
    birthDate: '1990-05-20',
    phone: '47998765432',
    email: 'maria@exemplo.com',
    profession: 'Engenheira',
    emergencyContact: { name: 'Carlos Souza', relationship: 'Irmão', phone: '4733334444' },
    version: '7',
  });

  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    http.match((request) => request.url.startsWith('icons/'));
    http.verify();
  });

  it('busca o morador e preenche os campos, com a máscara e a data em dd/mm/aaaa', async () => {
    const { html } = await openEdit(MARIA);

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Editar morador');
    expect(valueOf(html, 'fullName')).toBe('Maria Souza');
    expect(valueOf(html, 'birthDate')).toBe('20/05/1990');
    expect(valueOf(html, 'phone')).toBe('(47) 99876-5432');
    expect(valueOf(html, 'email')).toBe('maria@exemplo.com');
    expect(valueOf(html, 'profession')).toBe('Engenheira');
    expect(valueOf(html, 'contactName')).toBe('Carlos Souza');
    expect(valueOf(html, 'contactRelationship')).toBe('Irmão');
    expect(valueOf(html, 'contactPhone')).toBe('(47) 3333-4444');
    expect(submitButton(html).textContent?.trim()).toBe('Salvar alterações');
  });

  it('não mostra a seção de moradia nem busca unidades: o vínculo é outra tela', async () => {
    const { html } = await openEdit(MARIA);

    expect(testId(html, 'housing-section')).toBeNull();
    expect(html.querySelector('[formControlName="unitId"]')).toBeNull();
  });

  it('mostra o CPF só mascarado, e o campo aparece quando a pessoa pede para trocá-lo', async () => {
    const { fixture, html } = await openEdit(MARIA);

    expect(testId(html, 'cpf-masked')?.textContent?.trim()).toBe('***.456.789-**');
    expect(html.querySelector('[formControlName="cpf"]')).toBeNull();

    testId(html, 'change-cpf')!.click();
    await settle(fixture);

    expect(field(html, 'cpf')).not.toBeNull();
    expect(valueOf(html, 'cpf')).toBe('');
    expect(testId(html, 'cpf-masked')).toBeNull();
  });

  it('"Manter o CPF atual" descarta o CPF digitado', async () => {
    const { fixture, html } = await openEdit(MARIA);
    testId(html, 'change-cpf')!.click();
    await settle(fixture);
    fill(html, 'cpf', '52998224725');

    testId(html, 'keep-cpf')!.click();
    await settle(fixture);

    expect(html.querySelector('[formControlName="cpf"]')).toBeNull();
    expect(testId(html, 'cpf-masked')).not.toBeNull();
    expect(submitButton(html).disabled).toBe(true);
  });

  it('só habilita "Salvar alterações" depois de uma mudança', async () => {
    const { fixture, html } = await openEdit(MARIA);
    expect(submitButton(html).disabled).toBe(true);

    fill(html, 'profession', 'Médica');
    await settle(fixture);
    expect(submitButton(html).disabled).toBe(false);

    fill(html, 'profession', 'Engenheira');
    await settle(fixture);
    expect(submitButton(html).disabled).toBe(true);
  });

  it('salva com a versão lida e sem CPF, troca os dados pelos que a API devolve e avisa', async () => {
    const h = await openEdit(MARIA);
    fill(h.html, 'profession', 'Médica');
    await settle(h.fixture);

    await submit(h);

    const request = h.http.expectOne(`/api/residents/${MARIA.id}`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      version: '7',
      fullName: 'Maria Souza',
      birthDate: '1990-05-20',
      phone: '47998765432',
      email: 'maria@exemplo.com',
      profession: 'Médica',
      emergencyContact: { name: 'Carlos Souza', relationship: 'Irmão', phone: '4733334444' },
    });
    request.flush({ ...MARIA, profession: 'Médica', version: '8' });
    await settle(h.fixture);

    expect(testId(h.html, 'saved')?.textContent).toContain('Dados salvos');
    expect(valueOf(h.html, 'profession')).toBe('Médica');
    expect(submitButton(h.html).disabled).toBe(true);
  });

  it('manda o CPF novo quando a pessoa o troca', async () => {
    const h = await openEdit(MARIA);
    testId(h.html, 'change-cpf')!.click();
    await settle(h.fixture);
    fill(h.html, 'cpf', '52998224725');
    await settle(h.fixture);

    await submit(h);

    const request = h.http.expectOne(`/api/residents/${MARIA.id}`);
    expect(request.request.body.cpf).toBe('52998224725');
    request.flush({ ...MARIA, version: '8' });
    await settle(h.fixture);

    expect(testId(h.html, 'cpf-masked')).not.toBeNull();
  });

  it('apagar o e-mail manda nulo, para a API limpá-lo', async () => {
    const h = await openEdit(MARIA);
    fill(h.html, 'email', '');
    await settle(h.fixture);

    await submit(h);

    const request = h.http.expectOne(`/api/residents/${MARIA.id}`);
    expect(request.request.body.email).toBeNull();
    request.flush({ ...MARIA, email: null, version: '8' });
    await settle(h.fixture);
  });

  it('CPF repetido volta como erro do formulário e a tela continua editável', async () => {
    const h = await openEdit(MARIA);
    testId(h.html, 'change-cpf')!.click();
    await settle(h.fixture);
    fill(h.html, 'cpf', '52998224725');
    await settle(h.fixture);

    await submit(h);
    reject(
      h.http.expectOne(`/api/residents/${MARIA.id}`),
      409,
      'condominium.cpf.already_registered',
    );
    await settle(h.fixture);

    expect(alertTexts(h.html)).toContain('Este CPF já está cadastrado neste condomínio');
    expect(testId(h.html, 'conflict')).toBeNull();
    expect(valueOf(h.html, 'cpf')).toBe('529.982.247-25');
  });

  it('versão velha (409) mostra o aviso e "Recarregar dados" busca o morador de novo, sem apagar o digitado antes', async () => {
    const h = await openEdit(MARIA);
    fill(h.html, 'profession', 'Médica');
    await settle(h.fixture);
    await submit(h);
    reject(h.http.expectOne(`/api/residents/${MARIA.id}`), 409, 'concurrency.conflict');
    await settle(h.fixture);

    expect(testId(h.html, 'conflict')?.textContent).toContain('alterados em outro lugar');
    expect(valueOf(h.html, 'profession')).toBe('Médica');
    expect(submitButton(h.html).disabled).toBe(true);

    testId(h.html, 'reload')!.click();
    h.http
      .expectOne(`/api/residents/${MARIA.id}`)
      .flush({ ...MARIA, profession: 'Arquiteta', version: '9' });
    await settle(h.fixture);

    expect(testId(h.html, 'conflict')).toBeNull();
    expect(valueOf(h.html, 'profession')).toBe('Arquiteta');
  });

  it('morador que não existe (404) mostra o aviso e nenhum formulário', async () => {
    const harness = await configure('sumiu');
    reject(harness.http.expectOne('/api/residents/sumiu'), 404, 'condominium.resident.not_found');
    await settle(harness.fixture);

    expect(testId(harness.html, 'not-found')?.textContent).toContain('Morador não encontrado');
    expect(testId(harness.html, 'back')?.getAttribute('href')).toBe('/moradores');
    expect(harness.html.querySelector('form')).toBeNull();
  });

  it('falha ao carregar mostra o erro e tenta de novo', async () => {
    const harness = await configure(MARIA.id);
    harness.http
      .expectOne(`/api/residents/${MARIA.id}`)
      .flush(null, { status: 500, statusText: 'Erro' });
    await settle(harness.fixture);
    expect(testId(harness.html, 'load-error')).not.toBeNull();

    testId(harness.html, 'retry')!.click();
    harness.http.expectOne(`/api/residents/${MARIA.id}`).flush(MARIA);
    await settle(harness.fixture);

    expect(valueOf(harness.html, 'fullName')).toBe('Maria Souza');
  });
});

describe('ResidentFormPage, vínculos', () => {
  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    http.match((request) => request.url.startsWith('icons/'));
    http.verify();
  });

  it('a edição mostra os vínculos do morador abaixo dos dados', async () => {
    const found = resident({ occupancies: [occupancy()] });
    const { html } = await openEdit(found);

    expect(testId(html, 'occupancies')).not.toBeNull();
    expect(testId(html, 'add-occupancy')).not.toBeNull();
  });

  it('o cadastro não mostra os vínculos: a moradia é o cartão do próprio formulário', async () => {
    const { html } = await openNew();

    expect(testId(html, 'occupancies')).toBeNull();
    expect(testId(html, 'housing-section')).not.toBeNull();
  });

  it('encerrar um vínculo não apaga o que foi digitado nos dados pessoais nem libera um salvamento velho', async () => {
    const found = resident({ version: '7', occupancies: [occupancy({ id: 'occ-1' })] });
    const h = await openEdit(found);
    fill(h.html, 'profession', 'Médica');
    await settle(h.fixture);
    expect(submitButton(h.html).disabled).toBe(false);

    testId(h.html, 'end-occupancy')!.click();
    await settle(h.fixture);
    fill(h.html, 'endDate', '01042024');
    testId(h.html, 'end-form')!.dispatchEvent(new Event('submit'));
    await settle(h.fixture);
    h.http.expectOne(`/api/residents/${found.id}/occupancies/occ-1/end`).flush({
      ...found,
      version: '9',
      occupancies: [occupancy({ id: 'occ-1', endDate: '2024-04-01' })],
    });
    await settle(h.fixture);

    expect(valueOf(h.html, 'profession')).toBe('Médica');
    expect(submitButton(h.html).disabled).toBe(false);

    await submit(h);
    const update = h.http.expectOne(`/api/residents/${found.id}`);
    expect(update.request.body.version).toBe('7');
    update.flush(found);
  });

  describe('acesso ao sistema', () => {
    const MESSAGE_LOCKED = 'O e-mail não pode ser alterado depois que o morador cria a senha';

    function emailInput(html: HTMLElement): HTMLInputElement {
      return field(html, 'email') as HTMLInputElement;
    }

    function emailHint(html: HTMLElement): string {
      return (
        emailInput(html)
          .closest('mat-form-field')
          ?.querySelector('mat-hint')
          ?.textContent?.trim() ?? ''
      );
    }

    function accessText(html: HTMLElement, id: string): string {
      return testId(html, id)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
    }

    it('mostra o cartão "Acesso ao sistema" na edição', async () => {
      const { html } = await openEdit(resident({ accessStatus: 'none' }));

      expect(accessText(html, 'access-card')).toContain('Acesso ao sistema');
      expect(accessText(html, 'access-status')).toBe('Sem acesso');
    });

    it('o cadastro não mostra o cartão de acesso: o convite vem depois de cadastrar', async () => {
      const { html } = await openNew();

      expect(testId(html, 'access-card')).toBeNull();
    });

    it('sem acesso, o e-mail segue livre e o cartão oferece "Convidar"', async () => {
      const { html } = await openEdit(resident({ accessStatus: 'none' }));

      expect(emailInput(html).disabled).toBe(false);
      expect(testId(html, 'invite')?.textContent?.trim()).toBe('Convidar');
    });

    it.each(['active', 'inactive'] as const)(
      'com acesso "%s", o e-mail fica travado e a tela explica por quê',
      async (status) => {
        const { html } = await openEdit(resident({ accessStatus: status }));

        expect(emailInput(html).disabled).toBe(true);
        expect(emailHint(html)).toBe(MESSAGE_LOCKED);
        expect(testId(html, 'invite')).toBeNull();
        expect(testId(html, 'resend-invite')).toBeNull();
      },
    );

    it('com convite enviado, o e-mail ainda se corrige e a dica explica o reenvio', async () => {
      const { html } = await openEdit(resident({ accessStatus: 'invited' }));

      expect(emailInput(html).disabled).toBe(false);
      expect(emailHint(html)).toBe(
        'O convite já foi enviado a este endereço. Para corrigi-lo, salve e use Reenviar convite',
      );
      expect(testId(html, 'resend-invite')).not.toBeNull();
    });

    it('sem convite, a dica do e-mail é a do cadastro', async () => {
      const { html } = await openEdit(resident({ accessStatus: 'none' }));

      expect(emailHint(html)).toBe(
        'Opcional. Sem e-mail o morador fica cadastrado, mas não pode ser convidado a acessar o sistema',
      );
    });

    it('com alteração por salvar, o convite fica desabilitado e a tela pede para salvar antes', async () => {
      const h = await openEdit(resident({ accessStatus: 'none' }));
      expect(testId(h.html, 'access-blocked')).toBeNull();

      fill(h.html, 'profession', 'Médica');
      await settle(h.fixture);

      expect(accessText(h.html, 'access-blocked')).toBe(
        'Salve ou descarte as alterações antes de convidar',
      );
      expect(testId(h.html, 'invite')?.hasAttribute('disabled')).toBe(true);
    });

    it('depois de convidar, lê o morador de novo: o acesso vira "convite enviado" e a versão nova vale no próximo salvamento', async () => {
      const found = resident({ accessStatus: 'none', version: '7' });
      const h = await openEdit(found);

      testId(h.html, 'invite')!.click();
      h.http
        .expectOne(`/api/residents/${found.id}/invitation`)
        .flush(null, { status: 202, statusText: 'Accepted' });
      await settle(h.fixture);
      h.http
        .expectOne(`/api/residents/${found.id}`)
        .flush({ ...found, accessStatus: 'invited', version: '8' });
      await settle(h.fixture);

      expect(accessText(h.html, 'access-status')).toBe('Convite enviado');
      expect(testId(h.html, 'resend-invite')).not.toBeNull();
      expect(accessText(h.html, 'access-notice')).toContain('Convite enviado para');

      fill(h.html, 'profession', 'Médica');
      await settle(h.fixture);
      await submit(h);
      const update = h.http.expectOne(`/api/residents/${found.id}`);
      expect(update.request.body.version).toBe('8');
      update.flush({ ...found, accessStatus: 'invited', profession: 'Médica', version: '9' });
    });

    it('e-mail travado na API (409) vira aviso do formulário e a tela continua editável', async () => {
      const found = resident({ accessStatus: 'invited', email: 'maria@exemplo.com' });
      const h = await openEdit(found);
      fill(h.html, 'email', '');
      await settle(h.fixture);

      await submit(h);
      reject(
        h.http.expectOne(`/api/residents/${found.id}`),
        409,
        'condominium.resident.email_locked',
      );
      await settle(h.fixture);

      expect(alertTexts(h.html)).toContain(MESSAGE_LOCKED);
      expect(testId(h.html, 'conflict')).toBeNull();
      expect(submitButton(h.html).disabled).toBe(false);
    });

    it('e-mail que já tem conta em outro cadastro (409) vira aviso do formulário', async () => {
      const found = resident({ accessStatus: 'invited', email: 'maria@exemplo.com' });
      const h = await openEdit(found);
      fill(h.html, 'email', 'outra@exemplo.com');
      await settle(h.fixture);

      await submit(h);
      reject(
        h.http.expectOne(`/api/residents/${found.id}`),
        409,
        'identity.resident_access.email_unavailable',
      );
      await settle(h.fixture);

      expect(alertTexts(h.html)).toContain(
        'Este e-mail já tem uma conta no Urban. Informe outro endereço',
      );
      expect(valueOf(h.html, 'email')).toBe('outra@exemplo.com');
    });

    it('corrigir o e-mail de quem foi convidado salva pelo mesmo PUT e não reenvia o convite', async () => {
      const found = resident({ accessStatus: 'invited', email: 'maria@exemplo.com' });
      const h = await openEdit(found);
      fill(h.html, 'email', 'maria.certa@exemplo.com');
      await settle(h.fixture);

      await submit(h);
      const update = h.http.expectOne(`/api/residents/${found.id}`);
      expect(update.request.body.email).toBe('maria.certa@exemplo.com');
      update.flush({ ...found, email: 'maria.certa@exemplo.com', version: '9' });
      await settle(h.fixture);

      h.http.expectNone(`/api/residents/${found.id}/invitation`);
      expect(testId(h.html, 'saved')).not.toBeNull();
      expect(testId(h.html, 'resend-invite')).not.toBeNull();
    });
  });
});

describe('ResidentFormPage, dependente menor de idade', () => {
  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    http.match((request) => request.url.startsWith('icons/'));
    http.verify();
  });

  const YEAR = new Date().getFullYear() - 10;

  function fillMinor(html: HTMLElement): void {
    fill(html, 'fullName', 'Pedro Souza');
    fill(html, 'cpf', '52998224725');
    fill(html, 'birthDate', `1004${YEAR}`);
    fill(html, 'phone', '47998765432');
    fill(html, 'unitId', BLOCK_A_101.id);
    fill(html, 'kind', 'dependent');
    fill(html, 'startDate', '01032024');
  }

  it('a data de menor dispensa profissão e contato de emergência: nenhum erro de obrigatório', async () => {
    const h = await openNew();
    fillMinor(h.html);
    await settle(h.fixture);

    await submit(h);

    const request = h.http.expectOne('/api/residents');
    expect(request.request.body).toEqual({
      fullName: 'Pedro Souza',
      cpf: '52998224725',
      birthDate: `${YEAR}-04-10`,
      phone: '47998765432',
      email: null,
      profession: null,
      emergencyContact: null,
      unitId: BLOCK_A_101.id,
      kind: 'dependent',
      startDate: '2024-03-01',
    });
    request.flush(
      resident({
        fullName: 'Pedro Souza',
        birthDate: `${YEAR}-04-10`,
        profession: null,
        emergencyContact: null,
        occupancies: [occupancy({ kind: 'dependent' })],
      }),
      { status: 201, statusText: 'Created' },
    );
    await settle(h.fixture);
    expect(testId(h.html, 'created')?.textContent).toContain('Pedro Souza');
  });

  it('avisa que profissão e contato são opcionais para menores de 18 anos', async () => {
    const h = await openNew();

    expect(testId(h.html, 'minor-note')).toBeNull();
    fill(h.html, 'birthDate', `1004${YEAR}`);
    await settle(h.fixture);

    expect(testId(h.html, 'minor-note')?.textContent).toContain(
      'Para menores de 18 anos, profissão e contato de emergência são opcionais',
    );
    fill(h.html, 'birthDate', '20051990');
    await settle(h.fixture);
    expect(testId(h.html, 'minor-note')).toBeNull();
  });

  it('o menor só pode ser dependente: outro vínculo mostra o erro e não envia', async () => {
    const h = await openNew();
    fillMinor(h.html);
    fill(h.html, 'kind', 'tenant');
    await settle(h.fixture);

    await submit(h);

    expect(errorTexts(h.html)).toContain('Quem tem menos de 18 anos só pode ser dependente');
    // Nenhuma gravação saiu: o verify() do afterEach falharia.
  });

  it('o erro do vínculo some quando a pessoa passa a ser adulta ou o vínculo vira dependente', async () => {
    const h = await openNew();
    fillMinor(h.html);
    fill(h.html, 'kind', 'tenant');
    field(h.html, 'kind').dispatchEvent(new Event('blur'));
    await settle(h.fixture);
    expect(errorTexts(h.html)).toContain('Quem tem menos de 18 anos só pode ser dependente');

    fill(h.html, 'birthDate', '20051990');
    await settle(h.fixture);

    expect(errorTexts(h.html)).not.toContain('Quem tem menos de 18 anos só pode ser dependente');
  });

  it('com o contato preenchido pela metade, o menor precisa completar o que começou', async () => {
    const h = await openNew();
    fillMinor(h.html);
    fill(h.html, 'contactName', 'Carlos Souza');
    await settle(h.fixture);

    await submit(h);

    expect(errorTexts(h.html)).toEqual(
      expect.arrayContaining(['Informe o parentesco', 'Informe o telefone do contato']),
    );
    expect(errorTexts(h.html)).not.toContain('Informe a profissão');
  });

  it('o adulto continua precisando de profissão e contato, mesmo como dependente', async () => {
    const h = await openNew();
    fillRegistration(h.html);
    fill(h.html, 'kind', 'dependent');
    fill(h.html, 'profession', '');
    await settle(h.fixture);

    await submit(h);

    expect(errorTexts(h.html)).toContain('Informe a profissão');
  });

  it('a profissão preenchida de um menor vai para a API e é conferida como a do adulto', async () => {
    const h = await openNew();
    fillMinor(h.html);
    fill(h.html, 'profession', 'x');
    await settle(h.fixture);

    await submit(h);

    expect(errorTexts(h.html)).toContain('A profissão deve ter de 2 a 80 caracteres');
  });

  it('quando a API recusa o menor com outro vínculo, mostra o texto novo', async () => {
    const h = await openNew();
    fillMinor(h.html);
    await settle(h.fixture);

    await submit(h);
    reject(h.http.expectOne('/api/residents'), 400, 'condominium.resident.under_age');
    await settle(h.fixture);

    expect(alertTexts(h.html)).toContain(
      'Quem tem menos de 18 anos só pode ser cadastrado como dependente',
    );
  });

  it('na edição, o menor aparece sem profissão e sem contato e salva sem exigi-los', async () => {
    const minor = resident({
      fullName: 'Pedro Souza',
      birthDate: `${YEAR}-04-10`,
      email: null,
      profession: null,
      emergencyContact: null,
      occupancies: [occupancy({ kind: 'dependent' })],
    });
    const h = await openEdit(minor);

    expect(valueOf(h.html, 'profession')).toBe('');
    expect(valueOf(h.html, 'contactName')).toBe('');
    expect(testId(h.html, 'minor-note')).not.toBeNull();
    fill(h.html, 'phone', '47991112222');
    await settle(h.fixture);

    await submit(h);

    const request = h.http.expectOne(`/api/residents/${minor.id}`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.profession).toBeNull();
    expect(request.request.body.emergencyContact).toBeNull();
    request.flush({ ...minor, phone: '47991112222', version: 'novo' });
    await settle(h.fixture);
    expect(testId(h.html, 'saved')).not.toBeNull();
  });

  it('na edição, quem deixa de ser menor passa a precisar de profissão e contato', async () => {
    const minor = resident({
      birthDate: `${YEAR}-04-10`,
      profession: null,
      emergencyContact: null,
      occupancies: [occupancy({ kind: 'dependent' })],
    });
    const h = await openEdit(minor);
    fill(h.html, 'birthDate', '20051990');
    await settle(h.fixture);

    await submit(h);

    expect(errorTexts(h.html)).toEqual(
      expect.arrayContaining(['Informe a profissão', 'Informe o nome do contato']),
    );
  });
});
