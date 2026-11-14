import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ResidentPayload, UpdateResidentPayload } from '../../core/residents/residents.models';
import { flushPromises } from '../../testing/async';
import { resident } from '../../testing/residents.fixtures';
import { unit, unitPage } from '../../testing/units.fixtures';
import { ResidentFormStore } from './resident-form.store';

type Store = InstanceType<typeof ResidentFormStore>;

const CREATE_PAYLOAD: ResidentPayload = {
  fullName: 'Maria Souza',
  cpf: '52998224725',
  birthDate: '1990-05-20',
  phone: '47998765432',
  email: null,
  profession: 'Engenheira',
  emergencyContact: { name: 'Carlos Souza', relationship: 'Irmão', phone: '4733334444' },
  unitId: '0199aaaa-0000-7000-8000-000000000001',
  kind: 'owner',
  startDate: '2024-03-01',
};

const UPDATE_PAYLOAD: UpdateResidentPayload = {
  version: '7',
  fullName: 'Maria Souza Lima',
  birthDate: '1990-05-20',
  phone: '47998765432',
  email: null,
  profession: 'Engenheira',
  emergencyContact: { name: 'Carlos Souza', relationship: 'Irmão', phone: '4733334444' },
};

function setup(): { store: Store; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), ResidentFormStore],
  });
  return { store: TestBed.inject(ResidentFormStore), http: TestBed.inject(HttpTestingController) };
}

function unitsRequest(http: HttpTestingController): TestRequest {
  return http.expectOne((request) => request.url === '/api/units' && request.method === 'GET');
}

function params(request: TestRequest): URLSearchParams {
  return new URLSearchParams(request.request.params.toString());
}

function reject(request: TestRequest, status: number, code?: string): void {
  request.flush(code === undefined ? null : { code }, { status, statusText: 'Erro' });
}

describe('ResidentFormStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  describe('loadForCreate', () => {
    it('busca as unidades ativas, de 200 em 200, e vira "ready"', async () => {
      const { store, http } = setup();
      const units = [unit(), unit()];

      const loading = store.loadForCreate();
      expect(store.status()).toBe('loading');
      const request = unitsRequest(http);
      request.flush(unitPage(units, 2, 1, 200));
      await loading;

      expect(params(request).get('status')).toBe('active');
      expect(params(request).get('pageSize')).toBe('200');
      expect(params(request).get('page')).toBe('1');
      expect(store.status()).toBe('ready');
      expect(store.units()).toEqual(units);
    });

    it('junta as páginas quando há mais de 200 unidades', async () => {
      const { store, http } = setup();
      const first = Array.from({ length: 200 }, () => unit());
      const second = Array.from({ length: 50 }, () => unit());

      const loading = store.loadForCreate();
      unitsRequest(http).flush(unitPage(first, 250, 1, 200));
      await flushPromises();
      const next = unitsRequest(http);
      next.flush(unitPage(second, 250, 2, 200));
      await loading;

      expect(params(next).get('page')).toBe('2');
      expect(store.units()).toHaveLength(250);
      expect(store.status()).toBe('ready');
    });

    it('fica "ready" com a lista vazia quando não há unidade ativa', async () => {
      const { store, http } = setup();

      const loading = store.loadForCreate();
      unitsRequest(http).flush(unitPage([], 0, 1, 200));
      await loading;

      expect(store.status()).toBe('ready');
      expect(store.units()).toEqual([]);
    });

    it('vira "error" quando a busca falha e dá para tentar de novo', async () => {
      const { store, http } = setup();

      const first = store.loadForCreate();
      reject(unitsRequest(http), 500);
      await first;
      expect(store.status()).toBe('error');

      const second = store.loadForCreate();
      unitsRequest(http).flush(unitPage([unit()], 1, 1, 200));
      await second;
      expect(store.status()).toBe('ready');
    });
  });

  describe('loadForEdit', () => {
    it('busca o morador e vira "ready"', async () => {
      const { store, http } = setup();
      const found = resident();

      const loading = store.loadForEdit(found.id);
      expect(store.status()).toBe('loading');
      http.expectOne(`/api/residents/${found.id}`).flush(found);
      await loading;

      expect(store.status()).toBe('ready');
      expect(store.resident()).toEqual(found);
    });

    it('vira "not-found" quando o morador não existe (404)', async () => {
      const { store, http } = setup();

      const loading = store.loadForEdit('x');
      reject(http.expectOne('/api/residents/x'), 404, 'condominium.resident.not_found');
      await loading;

      expect(store.status()).toBe('not-found');
      expect(store.resident()).toBeNull();
    });

    it('vira "error" nas outras falhas', async () => {
      const { store, http } = setup();

      const loading = store.loadForEdit('x');
      reject(http.expectOne('/api/residents/x'), 500);
      await loading;

      expect(store.status()).toBe('error');
    });
  });

  describe('create', () => {
    it('grava, guarda o morador criado e devolve nulo', async () => {
      const { store, http } = setup();
      const created = resident();

      const saving = store.create(CREATE_PAYLOAD);
      expect(store.status()).toBe('saving');
      const request = http.expectOne('/api/residents');
      expect(request.request.method).toBe('POST');
      request.flush(created, { status: 201, statusText: 'Created' });

      await expect(saving).resolves.toBeNull();
      expect(store.status()).toBe('ready');
      expect(store.created()).toEqual(created);
    });

    it.each([
      [400, 'condominium.cpf.invalid'],
      [409, 'condominium.cpf.already_registered'],
      [409, 'unit.deactivated'],
      [404, 'unit.not_found'],
    ])('devolve o problema %s %s e continua editável, sem morador criado', async (status, code) => {
      const { store, http } = setup();

      const saving = store.create(CREATE_PAYLOAD);
      reject(http.expectOne('/api/residents'), status, code);
      const problem = await saving;

      expect(problem).toMatchObject({ status, code });
      expect(store.status()).toBe('ready');
      expect(store.created()).toBeNull();
    });

    it('startAnother limpa o morador criado para um novo cadastro', async () => {
      const { store, http } = setup();
      const saving = store.create(CREATE_PAYLOAD);
      http.expectOne('/api/residents').flush(resident(), { status: 201, statusText: 'Created' });
      await saving;

      store.startAnother();

      expect(store.created()).toBeNull();
    });
  });

  describe('update', () => {
    async function editing(store: Store, http: HttpTestingController, id: string): Promise<void> {
      const loading = store.loadForEdit(id);
      http.expectOne(`/api/residents/${id}`).flush(resident({ id }));
      await loading;
    }

    it('grava com PUT, troca o morador pelo que a API devolve (com a versão nova) e devolve nulo', async () => {
      const { store, http } = setup();
      await editing(store, http, 'abc');
      const updated = resident({ id: 'abc', fullName: 'Maria Souza Lima', version: '8' });

      const saving = store.update('abc', UPDATE_PAYLOAD);
      expect(store.status()).toBe('saving');
      const request = http.expectOne('/api/residents/abc');
      expect(request.request.method).toBe('PUT');
      request.flush(updated);

      await expect(saving).resolves.toBeNull();
      expect(store.status()).toBe('ready');
      expect(store.resident()).toEqual(updated);
    });

    it('vira "conflict" quando a versão está velha (409 de versão) e mantém o morador na tela', async () => {
      const { store, http } = setup();
      await editing(store, http, 'abc');
      const before = store.resident();

      const saving = store.update('abc', UPDATE_PAYLOAD);
      reject(http.expectOne('/api/residents/abc'), 409, 'concurrency.conflict');
      const problem = await saving;

      expect(problem).toMatchObject({ status: 409, code: 'concurrency.conflict' });
      expect(store.status()).toBe('conflict');
      expect(store.resident()).toEqual(before);
    });

    it('o 409 de CPF repetido é erro de campo: a tela continua editável', async () => {
      const { store, http } = setup();
      await editing(store, http, 'abc');

      const saving = store.update('abc', UPDATE_PAYLOAD);
      reject(http.expectOne('/api/residents/abc'), 409, 'condominium.cpf.already_registered');
      await saving;

      expect(store.status()).toBe('ready');
    });

    it('vira "not-found" quando o morador sumiu (404)', async () => {
      const { store, http } = setup();
      await editing(store, http, 'abc');

      const saving = store.update('abc', UPDATE_PAYLOAD);
      reject(http.expectOne('/api/residents/abc'), 404, 'condominium.resident.not_found');
      await saving;

      expect(store.status()).toBe('not-found');
    });

    it('uma falha de rede devolve o problema e deixa editar de novo', async () => {
      const { store, http } = setup();
      await editing(store, http, 'abc');

      const saving = store.update('abc', UPDATE_PAYLOAD);
      http.expectOne('/api/residents/abc').error(new ProgressEvent('error'));
      const problem = await saving;

      expect(problem?.status).toBe(0);
      expect(store.status()).toBe('ready');
    });
  });
});
