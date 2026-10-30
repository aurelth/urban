import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  MyCondominium,
  UpdateManagerIdentityPayload,
  UpdateMyCondominiumPayload,
} from '../../../core/condominium/condominium.models';
import { ApiProblem } from '../../../core/http/api-problem';
import { problem } from '../../../testing/auth.fixtures';
import { ProfileStore } from './profile.store';

const ADDRESS = {
  postalCode: '89010000',
  street: 'Rua das Flores',
  number: '120',
  complement: null,
  neighborhood: 'Centro',
  city: 'Blumenau',
  state: 'SC',
};

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
  condominium: { name: 'Residencial das Flores', cnpj: '11.222.333/0001-81', address: ADDRESS },
};

const FREE: UpdateMyCondominiumPayload = {
  version: '7.9',
  displayName: 'Mari',
  phone: '47991234567',
  managerAddress: ADDRESS,
  condominiumName: 'Residencial das Flores',
  condominiumAddress: ADDRESS,
};

const IDENTITY: UpdateManagerIdentityPayload = {
  version: '7.9',
  password: 'Senha-segura-123',
  fullName: 'Marina Souza Lima',
  birthDate: '1980-06-15',
};

function setup(): { store: InstanceType<typeof ProfileStore>; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), ProfileStore],
  });
  return { store: TestBed.inject(ProfileStore), http: TestBed.inject(HttpTestingController) };
}

function fail(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

describe('ProfileStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('comeca carregando, sem dados', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.data()).toBeNull();
  });

  describe('load', () => {
    it('busca os dados e passa a pronto', async () => {
      const { store, http } = setup();

      const loading = store.load();
      expect(store.status()).toBe('loading');
      http.expectOne('/api/condominiums/mine').flush(MINE);
      await loading;

      expect(store.status()).toBe('ready');
      expect(store.data()).toEqual(MINE);
    });

    it('vira "nao encontrado" quando a API diz 404', async () => {
      const { store, http } = setup();

      const loading = store.load();
      const [body, options] = fail(404, 'condominium.not_found');
      http.expectOne('/api/condominiums/mine').flush(body, options);
      await loading;

      expect(store.status()).toBe('not-found');
      expect(store.data()).toBeNull();
    });

    it.each([500, 503, 0])('vira "erro" quando a busca falha com %s', async (status) => {
      const { store, http } = setup();

      const loading = store.load();
      http
        .expectOne('/api/condominiums/mine')
        .flush(null, { status: status || 500, statusText: 'Erro' });
      await loading;

      expect(store.status()).toBe('error');
    });

    it('depois de um erro, buscar de novo volta a carregar e pode dar certo', async () => {
      const { store, http } = setup();
      const first = store.load();
      http.expectOne('/api/condominiums/mine').flush(null, { status: 503, statusText: 'Erro' });
      await first;

      const second = store.load();
      expect(store.status()).toBe('loading');
      http.expectOne('/api/condominiums/mine').flush(MINE);
      await second;

      expect(store.status()).toBe('ready');
    });

    it('recarregar depois de um conflito troca os dados velhos pelos novos', async () => {
      const { store, http } = setup();
      const first = store.load();
      http.expectOne('/api/condominiums/mine').flush(MINE);
      await first;
      const saving = store.saveFree(FREE);
      const [body, options] = fail(409, 'concurrency.conflict');
      http.expectOne('/api/condominiums/mine').flush(body, options);
      await saving;

      const reloading = store.load();
      http
        .expectOne('/api/condominiums/mine')
        .flush({ ...MINE, version: '9.9', displayName: 'Outra' });
      await reloading;

      expect(store.status()).toBe('ready');
      expect(store.data()?.version).toBe('9.9');
    });
  });

  describe('saveFree', () => {
    async function loaded(): Promise<ReturnType<typeof setup>> {
      const harness = setup();
      const loading = harness.store.load();
      harness.http.expectOne('/api/condominiums/mine').flush(MINE);
      await loading;
      return harness;
    }

    it('fica "salvando" durante a chamada e guarda a resposta quando da certo', async () => {
      const { store, http } = await loaded();

      const saving = store.saveFree(FREE);
      expect(store.status()).toBe('saving');
      const request = http.expectOne('/api/condominiums/mine');
      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual(FREE);
      request.flush({ ...MINE, displayName: 'Mari', version: '8.9' });

      await expect(saving).resolves.toBeNull();
      expect(store.status()).toBe('ready');
      expect(store.data()?.displayName).toBe('Mari');
      expect(store.data()?.version).toBe('8.9');
    });

    it('vira "conflito" no 409, guarda o que a tela mostrava e devolve o problema', async () => {
      const { store, http } = await loaded();

      const saving = store.saveFree(FREE);
      const [body, options] = fail(409, 'concurrency.conflict');
      http.expectOne('/api/condominiums/mine').flush(body, options);
      const result: ApiProblem | null = await saving;

      expect(store.status()).toBe('conflict');
      expect(store.data()).toEqual(MINE);
      expect(result).toMatchObject({ status: 409, code: 'concurrency.conflict' });
    });

    it('volta a "pronto" em outro erro, mantem os dados e devolve o problema', async () => {
      const { store, http } = await loaded();

      const saving = store.saveFree(FREE);
      const [body, options] = fail(400, 'condominium.phone.invalid');
      http.expectOne('/api/condominiums/mine').flush(body, options);
      const result = await saving;

      expect(store.status()).toBe('ready');
      expect(store.data()).toEqual(MINE);
      expect(result).toMatchObject({ status: 400, code: 'condominium.phone.invalid' });
    });

    it('vira "nao encontrado" quando o cadastro some no meio do caminho', async () => {
      const { store, http } = await loaded();

      const saving = store.saveFree(FREE);
      const [body, options] = fail(404, 'condominium.not_found');
      http.expectOne('/api/condominiums/mine').flush(body, options);
      await saving;

      expect(store.status()).toBe('not-found');
    });
  });

  describe('saveIdentity', () => {
    async function loaded(): Promise<ReturnType<typeof setup>> {
      const harness = setup();
      const loading = harness.store.load();
      harness.http.expectOne('/api/condominiums/mine').flush(MINE);
      await loading;
      return harness;
    }

    it('salva pelo endereco dos dados sensiveis e guarda a resposta', async () => {
      const { store, http } = await loaded();

      const saving = store.saveIdentity(IDENTITY);
      expect(store.status()).toBe('saving');
      const request = http.expectOne('/api/condominiums/mine/manager-identity');
      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual(IDENTITY);
      request.flush({
        ...MINE,
        version: '8.9',
        manager: { ...MINE.manager, fullName: 'Marina Souza Lima' },
      });

      await expect(saving).resolves.toBeNull();
      expect(store.status()).toBe('ready');
      expect(store.data()?.manager.fullName).toBe('Marina Souza Lima');
    });

    it('senha incorreta devolve o problema, volta a "pronto" e nao troca os dados', async () => {
      const { store, http } = await loaded();

      const saving = store.saveIdentity(IDENTITY);
      const [body, options] = fail(403, 'identity.password.incorrect');
      http.expectOne('/api/condominiums/mine/manager-identity').flush(body, options);
      const result = await saving;

      expect(store.status()).toBe('ready');
      expect(store.data()).toEqual(MINE);
      expect(result).toMatchObject({ status: 403, code: 'identity.password.incorrect' });
    });

    it('vira "conflito" no 409', async () => {
      const { store, http } = await loaded();

      const saving = store.saveIdentity(IDENTITY);
      const [body, options] = fail(409, 'concurrency.conflict');
      http.expectOne('/api/condominiums/mine/manager-identity').flush(body, options);
      await saving;

      expect(store.status()).toBe('conflict');
      expect(store.data()).toEqual(MINE);
    });

    it('o 409 de CPF repetido no condominio nao e conflito de versao', async () => {
      const { store, http } = await loaded();

      const saving = store.saveIdentity(IDENTITY);
      const [body, options] = fail(409, 'condominium.cpf.already_registered');
      http.expectOne('/api/condominiums/mine/manager-identity').flush(body, options);
      const result = await saving;

      expect(store.status()).toBe('ready');
      expect(result).toMatchObject({ code: 'condominium.cpf.already_registered' });
    });
  });
});
