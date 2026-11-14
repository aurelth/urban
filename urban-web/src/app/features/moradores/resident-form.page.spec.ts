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
        'Escolha se é proprietário ou inquilino',
        'Informe a data de entrada',
      ]),
    );
    expect(errorTexts(h.html)).not.toContain('Informe um e-mail válido, como nome@exemplo.com');
    // Nenhuma requisição de gravação saiu: o verify() do afterEach falharia.
  });

  it('avisa na hora do menor de 18 anos, do CPF e do telefone inválidos', async () => {
    const h = await openNew();
    const year = new Date().getFullYear() - 10;

    fill(h.html, 'birthDate', `0101${year}`);
    fill(h.html, 'cpf', '11111111111');
    fill(h.html, 'phone', '1234');
    for (const name of ['birthDate', 'cpf', 'phone']) {
      field(h.html, name).dispatchEvent(new Event('blur'));
    }
    await settle(h.fixture);

    expect(errorTexts(h.html)).toEqual(
      expect.arrayContaining([
        'O morador precisa ter 18 anos ou mais',
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
});
