import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PostalCodeApi } from './postal-code.api';
import { PostalCodeLookup } from './postal-code.models';

const ADDRESS = {
  postalCode: '89010000',
  street: 'Rua XV de Novembro',
  neighborhood: 'Centro',
  city: 'Blumenau',
  state: 'SC',
};

describe('PostalCodeApi', () => {
  let api: PostalCodeApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(PostalCodeApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function lookup(): PostalCodeLookup[] {
    const received: PostalCodeLookup[] = [];
    api.lookup('89010000').subscribe((result) => received.push(result));
    return received;
  }

  it('consulta o CEP com GET /api/postal-codes/{cep}, só com os dígitos', () => {
    lookup();

    const request = http.expectOne('/api/postal-codes/89010000');

    expect(request.request.method).toBe('GET');
    request.flush(ADDRESS);
  });

  it('devolve o endereço quando a API encontra o CEP', () => {
    const received = lookup();

    http.expectOne('/api/postal-codes/89010000').flush(ADDRESS);

    expect(received).toEqual([{ status: 'found', address: ADDRESS }]);
  });

  it('devolve "não encontrado" quando a API responde 404', () => {
    const received = lookup();

    http
      .expectOne('/api/postal-codes/89010000')
      .flush(
        { status: 404, code: 'condominium.postal_code.not_found' },
        { status: 404, statusText: 'Not Found' },
      );

    expect(received).toEqual([{ status: 'not-found' }]);
  });

  it.each([
    [400, 'Bad Request'],
    [401, 'Unauthorized'],
    [429, 'Too Many Requests'],
    [500, 'Internal Server Error'],
    [503, 'Service Unavailable'],
  ])('devolve "indisponível" quando a API responde %i', (status, statusText) => {
    const received = lookup();

    http.expectOne('/api/postal-codes/89010000').flush({}, { status, statusText });

    expect(received).toEqual([{ status: 'unavailable' }]);
  });

  it('devolve "indisponível" quando não há rede', () => {
    const received = lookup();

    http.expectOne('/api/postal-codes/89010000').error(new ProgressEvent('error'));

    expect(received).toEqual([{ status: 'unavailable' }]);
  });

  it('devolve "indisponível" quando a resposta é um objeto sem os campos do endereço', () => {
    const received = lookup();

    http.expectOne('/api/postal-codes/89010000').flush({ postalCode: '89010000', city: 42 });

    expect(received).toEqual([{ status: 'unavailable' }]);
  });

  it('devolve "indisponível" quando a resposta não é o endereço esperado', () => {
    const received = lookup();

    http.expectOne('/api/postal-codes/89010000').flush('<html></html>');

    expect(received).toEqual([{ status: 'unavailable' }]);
  });
});
