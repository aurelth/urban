import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CondominiumApi } from './condominium.api';
import {
  MyCondominium,
  RegisterCondominiumPayload,
  RegisteredCondominium,
  UpdateManagerIdentityPayload,
  UpdateMyCondominiumPayload,
} from './condominium.models';

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

  describe('dados do síndico e do condomínio', () => {
    const MINE: MyCondominium = {
      condominiumId: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c4d5e',
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
        address: ADDRESS,
      },
    };

    it('le os dados com GET /api/condominiums/mine', () => {
      const received: MyCondominium[] = [];

      api.mine().subscribe((mine) => received.push(mine));
      const request = http.expectOne('/api/condominiums/mine');
      expect(request.request.method).toBe('GET');
      request.flush(MINE);

      expect(received).toEqual([MINE]);
    });

    it('salva os campos livres com PUT /api/condominiums/mine, em digitos e com a versao lida', () => {
      const payload: UpdateMyCondominiumPayload = {
        version: '7.9',
        displayName: 'Marina',
        phone: '47991234567',
        managerAddress: ADDRESS,
        condominiumName: 'Residencial das Flores',
        condominiumAddress: ADDRESS,
      };
      const received: MyCondominium[] = [];

      api.updateMine(payload).subscribe((mine) => received.push(mine));
      const request = http.expectOne('/api/condominiums/mine');
      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual(payload);
      request.flush({ ...MINE, version: '8.9' });

      expect(received[0].version).toBe('8.9');
    });

    it('salva os dados sensiveis com PUT /api/condominiums/mine/manager-identity, com a senha no corpo', () => {
      const payload: UpdateManagerIdentityPayload = {
        version: '7.9',
        password: 'Senha-segura-123',
        fullName: 'Marina Souza Lima',
        birthDate: '1980-06-15',
        cpf: '11144477735',
      };

      api.updateManagerIdentity(payload).subscribe();
      const request = http.expectOne('/api/condominiums/mine/manager-identity');

      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual(payload);
      request.flush(MINE);
    });

    it('deixa o CPF de fora quando a pessoa nao o troca', () => {
      api
        .updateManagerIdentity({
          version: '7.9',
          password: 'Senha-segura-123',
          fullName: 'Marina Souza',
          birthDate: '1980-06-15',
        })
        .subscribe();
      const request = http.expectOne('/api/condominiums/mine/manager-identity');

      expect(Object.keys(request.request.body as object)).not.toContain('cpf');
      request.flush(MINE);
    });

    it('entrega o 409 de versao velha como erro HTTP, para a tela tratar o conflito', () => {
      let status = 0;

      api.updateMine({} as UpdateMyCondominiumPayload).subscribe({
        error: (error: { status: number }) => (status = error.status),
      });
      http
        .expectOne('/api/condominiums/mine')
        .flush(
          { status: 409, code: 'concurrency.conflict' },
          { status: 409, statusText: 'Conflict' },
        );

      expect(status).toBe(409);
    });
  });
});
