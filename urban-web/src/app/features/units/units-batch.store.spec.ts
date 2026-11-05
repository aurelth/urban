import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { UnitPayload } from '../../core/units/units.models';
import { UnitsBatchStore } from './units-batch.store';

const UNITS: UnitPayload[] = [
  { block: 'A', number: '101', type: 'apartment', idealFraction: null, area: null },
  { block: 'A', number: '102', type: 'apartment', idealFraction: null, area: null },
];

function setup(): { store: InstanceType<typeof UnitsBatchStore>; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), UnitsBatchStore],
  });
  return { store: TestBed.inject(UnitsBatchStore), http: TestBed.inject(HttpTestingController) };
}

function reject(http: HttpTestingController, status: number, body: unknown): void {
  http.expectOne('/api/units/batch').flush(body, { status, statusText: 'Erro' });
}

describe('UnitsBatchStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('comeca parado, sem problemas e sem lote cadastrado', () => {
    const { store } = setup();

    expect(store.saving()).toBe(false);
    expect(store.issues()).toEqual([]);
    expect(store.created()).toBeNull();
  });

  it('envia o lote e guarda quantas unidades foram cadastradas', async () => {
    const { store, http } = setup();

    const submitting = store.submit(UNITS);
    expect(store.saving()).toBe(true);
    const request = http.expectOne('/api/units/batch');
    expect(request.request.body).toEqual({ units: UNITS });
    request.flush({ created: 2 });

    expect(await submitting).toBeNull();
    expect(store.created()).toBe(2);
    expect(store.saving()).toBe(false);
    expect(store.issues()).toEqual([]);
  });

  it('guarda o problema de cada linha quando a API recusa o lote inteiro', async () => {
    const { store, http } = setup();

    const submitting = store.submit(UNITS);
    reject(http, 409, {
      code: 'unit.identifier.already_exists',
      errors: [{ position: 1, code: 'unit.identifier.already_exists' }],
    });
    const result = await submitting;

    expect(result).toMatchObject({ status: 409, code: 'unit.identifier.already_exists' });
    expect(store.issues()).toEqual([{ position: 1, code: 'unit.identifier.already_exists' }]);
    expect(store.created()).toBeNull();
    expect(store.saving()).toBe(false);
  });

  it('devolve o problema sem linhas marcadas quando a falha nao e de uma linha', async () => {
    const { store, http } = setup();

    const submitting = store.submit(UNITS);
    reject(http, 503, null);

    expect(await submitting).toMatchObject({ status: 503 });
    expect(store.issues()).toEqual([]);
  });

  it('limpa os problemas antigos ao enviar de novo', async () => {
    const { store, http } = setup();
    const first = store.submit(UNITS);
    reject(http, 422, {
      code: 'unit.ideal_fraction.sum_exceeded',
      errors: [{ position: 0, code: 'unit.ideal_fraction.sum_exceeded' }],
    });
    await first;

    const second = store.submit(UNITS);
    expect(store.issues()).toEqual([]);
    http.expectOne('/api/units/batch').flush({ created: 2 });
    await second;

    expect(store.created()).toBe(2);
  });

  it('tira o problema de uma linha quando ela e editada, sem mexer nas outras', async () => {
    const { store, http } = setup();
    const submitting = store.submit(UNITS);
    reject(http, 400, {
      code: 'unit.number.invalid',
      errors: [
        { position: 0, code: 'unit.number.invalid' },
        { position: 1, code: 'unit.area.invalid' },
      ],
    });
    await submitting;

    store.clearIssue(0);

    expect(store.issues()).toEqual([{ position: 1, code: 'unit.area.invalid' }]);
  });

  it('recomeçar zera o lote cadastrado e os problemas', async () => {
    const { store, http } = setup();
    const submitting = store.submit(UNITS);
    http.expectOne('/api/units/batch').flush({ created: 2 });
    await submitting;

    store.startOver();

    expect(store.created()).toBeNull();
    expect(store.issues()).toEqual([]);
  });
});
