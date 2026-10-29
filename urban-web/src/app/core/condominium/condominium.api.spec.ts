import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CondominiumApi } from './condominium.api';
import { RegisterCondominiumPayload, RegisteredCondominium } from './condominium.models';

const ADDRESS = {
  postalCode: '89010000',
  street: 'Rua das Flores',
  number: '120',
  complement: null,
  neighborhood: 'Centro',
  city: 'Blumenau',
  state: 'SC',
};

const PAYLOAD: RegisterCondominiumPayload = {
  displayName: 'Marina',
  manager: {
    fullName: 'Marina Souza',
    cpf: '52998224725',
    birthDate: '1980-06-15',
    phone: '47991234567',
    address: ADDRESS,
  },
  condominium: { name: 'Residencial das Flores', cnpj: '11222333000181', address: ADDRESS },
};

describe('CondominiumApi', () => {
  let api: CondominiumApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(CondominiumApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('cadastra o condomínio com POST /api/condominiums e devolve o resumo', () => {
    const received: RegisteredCondominium[] = [];

    api.register(PAYLOAD).subscribe((summary) => received.push(summary));
    const request = http.expectOne('/api/condominiums');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(PAYLOAD);
    request.flush({
      condominiumId: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c4d5e',
      name: 'Residencial das Flores',
      cnpj: '11.222.333/0001-81',
      managerCpf: '***.982.247-**',
    });

    expect(received).toEqual([
      {
        condominiumId: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c4d5e',
        name: 'Residencial das Flores',
        cnpj: '11.222.333/0001-81',
        managerCpf: '***.982.247-**',
      },
    ]);
  });
});
