import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MyAccessApi } from './my-access.api';

describe('MyAccessApi', () => {
  let api: MyAccessApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(MyAccessApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lê como a conta mora, sem mandar parâmetros: quem pergunta vem do token', () => {
    let received: unknown;
    api.get().subscribe((data) => (received = data));

    const request = http.expectOne('/api/finance/me/access');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.keys()).toEqual([]);
    request.flush({ access: 'unit' });

    expect(received).toEqual({ access: 'unit' });
  });
});
