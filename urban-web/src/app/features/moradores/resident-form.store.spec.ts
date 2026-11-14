import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ResidentPayload, UpdateResidentPayload } from '../../core/residents/residents.models';
import { flushPromises } from '../../testing/async';
import { occupancy, resident } from '../../testing/residents.fixtures';
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

  describe('vínculos', () => {
    const ADD_PAYLOAD = {
      unitId: '0199aaaa-0000-7000-8000-000000000002',
      kind: 'tenant' as const,
      startDate: '2026-01-05',
    };

    async function editing(store: Store, http: HttpTestingController): Promise<void> {
      const loading = store.loadForEdit('abc');
      http.expectOne('/api/residents/abc').flush(resident({ id: 'abc', version: '7' }));
      await loading;
    }

    describe('loadUnits', () => {
      it('busca as unidades ativas sem mexer na situação da tela', async () => {
        const { store, http } = setup();
        await editing(store, http);
        const units = [unit(), unit()];

        const loading = store.loadUnits();
        expect(store.unitsStatus()).toBe('loading');
        expect(store.status()).toBe('ready');
        const request = unitsRequest(http);
        request.flush(unitPage(units, 2, 1, 200));
        await loading;

        expect(params(request).get('status')).toBe('active');
        expect(params(request).get('pageSize')).toBe('200');
        expect(store.unitsStatus()).toBe('ready');
        expect(store.units()).toEqual(units);
        expect(store.status()).toBe('ready');
      });

      it('não busca de novo quando as unidades já estão carregadas', async () => {
        const { store, http } = setup();
        const first = store.loadUnits();
        unitsRequest(http).flush(unitPage([unit()], 1, 1, 200));
        await first;

        await store.loadUnits();

        http.expectNone((request) => request.url === '/api/units');
      });

      it('vira "error" quando a busca falha e dá para tentar de novo', async () => {
        const { store, http } = setup();

        const first = store.loadUnits();
        reject(unitsRequest(http), 500);
        await first;
        expect(store.unitsStatus()).toBe('error');

        const second = store.loadUnits();
        unitsRequest(http).flush(unitPage([unit()], 1, 1, 200));
        await second;
        expect(store.unitsStatus()).toBe('ready');
      });
    });

    describe('addOccupancy', () => {
      it('grava, troca só os vínculos do morador, mantém a versão lida e devolve nulo', async () => {
        const { store, http } = setup();
        await editing(store, http);
        const added = occupancy({ unitId: ADD_PAYLOAD.unitId, kind: 'tenant' });
        const returned = resident({
          id: 'abc',
          version: '9',
          occupancies: [occupancy(), added],
        });

        const saving = store.addOccupancy('abc', ADD_PAYLOAD);
        expect(store.occupancyBusy()).toBe(true);
        const request = http.expectOne('/api/residents/abc/occupancies');
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual(ADD_PAYLOAD);
        request.flush(returned, { status: 201, statusText: 'Created' });

        await expect(saving).resolves.toBeNull();
        expect(store.occupancyBusy()).toBe(false);
        expect(store.resident()?.occupancies).toEqual(returned.occupancies);
        expect(store.resident()?.version).toBe('7');
        expect(store.status()).toBe('ready');
      });

      it.each([
        [409, 'condominium.occupancy.already_active'],
        [409, 'unit.deactivated'],
        [404, 'unit.not_found'],
        [400, 'condominium.occupancy.start_date_invalid'],
      ])('devolve o problema %s %s e deixa a tela como estava', async (status, code) => {
        const { store, http } = setup();
        await editing(store, http);
        const before = store.resident();

        const saving = store.addOccupancy('abc', ADD_PAYLOAD);
        reject(http.expectOne('/api/residents/abc/occupancies'), status, code);
        const problem = await saving;

        expect(problem).toMatchObject({ status, code });
        expect(store.occupancyBusy()).toBe(false);
        expect(store.status()).toBe('ready');
        expect(store.resident()).toEqual(before);
      });

      it('vira "not-found" quando o morador sumiu', async () => {
        const { store, http } = setup();
        await editing(store, http);

        const saving = store.addOccupancy('abc', ADD_PAYLOAD);
        reject(
          http.expectOne('/api/residents/abc/occupancies'),
          404,
          'condominium.resident.not_found',
        );
        await saving;

        expect(store.status()).toBe('not-found');
      });

      it('não tira a tela do conflito de versão: os dados pessoais continuam velhos', async () => {
        const { store, http } = setup();
        await editing(store, http);
        const updating = store.update('abc', UPDATE_PAYLOAD);
        reject(http.expectOne('/api/residents/abc'), 409, 'concurrency.conflict');
        await updating;

        const saving = store.addOccupancy('abc', ADD_PAYLOAD);
        http
          .expectOne('/api/residents/abc/occupancies')
          .flush(resident({ id: 'abc', version: '9' }), { status: 201, statusText: 'Created' });
        await saving;

        expect(store.status()).toBe('conflict');
        expect(store.resident()?.version).toBe('7');
      });
    });

    describe('endOccupancy', () => {
      it('grava a saída e troca só os vínculos do morador', async () => {
        const { store, http } = setup();
        await editing(store, http);
        const ended = occupancy({ id: 'occ-1', endDate: '2026-11-10' });
        const returned = resident({ id: 'abc', version: '9', occupancies: [ended] });

        const saving = store.endOccupancy('abc', 'occ-1', { endDate: '2026-11-10' });
        expect(store.occupancyBusy()).toBe(true);
        const request = http.expectOne('/api/residents/abc/occupancies/occ-1/end');
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({ endDate: '2026-11-10' });
        request.flush(returned);

        await expect(saving).resolves.toBeNull();
        expect(store.occupancyBusy()).toBe(false);
        expect(store.resident()?.occupancies).toEqual([ended]);
        expect(store.resident()?.version).toBe('7');
      });

      it.each([
        [400, 'condominium.occupancy.end_date_invalid'],
        [404, 'condominium.occupancy.not_found'],
        [409, 'condominium.occupancy.already_ended'],
      ])('devolve o problema %s %s e deixa a tela como estava', async (status, code) => {
        const { store, http } = setup();
        await editing(store, http);
        const before = store.resident();

        const saving = store.endOccupancy('abc', 'occ-1', { endDate: '2026-11-10' });
        reject(http.expectOne('/api/residents/abc/occupancies/occ-1/end'), status, code);
        const problem = await saving;

        expect(problem).toMatchObject({ status, code });
        expect(store.occupancyBusy()).toBe(false);
        expect(store.status()).toBe('ready');
        expect(store.resident()).toEqual(before);
      });

      it('uma falha de rede devolve o problema e libera a tela', async () => {
        const { store, http } = setup();
        await editing(store, http);

        const saving = store.endOccupancy('abc', 'occ-1', { endDate: '2026-11-10' });
        http
          .expectOne('/api/residents/abc/occupancies/occ-1/end')
          .error(new ProgressEvent('error'));
        const problem = await saving;

        expect(problem?.status).toBe(0);
        expect(store.occupancyBusy()).toBe(false);
      });
    });

    describe('refreshOccupancies', () => {
      it('busca o morador de novo e troca só os vínculos, sem mexer na versão nem na situação', async () => {
        const { store, http } = setup();
        await editing(store, http);
        const fresh = resident({
          id: 'abc',
          version: '12',
          occupancies: [occupancy(), occupancy()],
        });

        const refreshing = store.refreshOccupancies('abc');
        http.expectOne('/api/residents/abc').flush(fresh);
        await refreshing;

        expect(store.resident()?.occupancies).toEqual(fresh.occupancies);
        expect(store.resident()?.version).toBe('7');
        expect(store.status()).toBe('ready');
      });

      it('vira "not-found" quando o morador sumiu e ignora as outras falhas', async () => {
        const { store, http } = setup();
        await editing(store, http);

        const first = store.refreshOccupancies('abc');
        reject(http.expectOne('/api/residents/abc'), 500);
        await first;
        expect(store.status()).toBe('ready');

        const second = store.refreshOccupancies('abc');
        reject(http.expectOne('/api/residents/abc'), 404, 'condominium.resident.not_found');
        await second;
        expect(store.status()).toBe('not-found');
      });
    });
  });

  describe('refreshResident', () => {
    async function editing(store: Store, http: HttpTestingController): Promise<void> {
      const loading = store.loadForEdit('abc');
      http.expectOne('/api/residents/abc').flush(resident({ id: 'abc', version: '7' }));
      await loading;
    }

    it('lê o morador de novo sem passar por "loading" e troca o acesso e a versão', async () => {
      const { store, http } = setup();
      await editing(store, http);
      const fresh = resident({ id: 'abc', accessStatus: 'invited', version: '12' });

      const refreshing = store.refreshResident('abc');
      expect(store.status()).toBe('ready');
      http.expectOne('/api/residents/abc').flush(fresh);
      await refreshing;

      expect(store.resident()).toEqual(fresh);
      expect(store.status()).toBe('ready');
    });

    it('vira "not-found" quando o morador sumiu e ignora as outras falhas', async () => {
      const { store, http } = setup();
      await editing(store, http);
      const before = store.resident();

      const first = store.refreshResident('abc');
      reject(http.expectOne('/api/residents/abc'), 500);
      await first;
      expect(store.status()).toBe('ready');
      expect(store.resident()).toBe(before);

      const second = store.refreshResident('abc');
      reject(http.expectOne('/api/residents/abc'), 404, 'condominium.resident.not_found');
      await second;
      expect(store.status()).toBe('not-found');
    });
  });
});
