import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { toApiProblem } from '../http/api-problem';
import { InvoicesApi } from './invoices.api';

const URL = '/api/finance/charges/c1/invoice';

describe('InvoicesApi', () => {
  let api: InvoicesApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(InvoicesApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('baixa o PDF da cobranca e devolve o arquivo com o nome que a API sugeriu', async () => {
    const result = firstValueFrom(api.download('c1'));

    const request = http.expectOne(URL);
    expect(request.request.method).toBe('GET');
    expect(request.request.responseType).toBe('blob');
    const pdf = new Blob(['%PDF-1.7'], { type: 'application/pdf' });
    request.flush(pdf, {
      headers: { 'Content-Disposition': "attachment; filename*=UTF-8''fatura-2026-11-101.pdf" },
    });

    const download = await result;
    expect(download.fileName).toBe('fatura-2026-11-101.pdf');
    expect(download.blob).toBe(pdf);
  });

  it('usa o nome padrao quando a resposta nao traz o nome', async () => {
    const result = firstValueFrom(api.download('c1'));

    http.expectOne(URL).flush(new Blob(['%PDF-1.7']));

    expect((await result).fileName).toBe('fatura.pdf');
  });

  it('devolve o codigo da API mesmo com a falha chegando como arquivo', async () => {
    const result = firstValueFrom(api.download('c1'));

    http.expectOne(URL).flush(
      new Blob([JSON.stringify({ status: 404, code: 'finance.charge.not_found' })], {
        type: 'application/problem+json',
      }),
      { status: 404, statusText: 'Not Found' },
    );

    const error: unknown = await result.catch((failure: unknown) => failure);
    expect(error).toBeInstanceOf(HttpErrorResponse);
    expect(toApiProblem(error)).toMatchObject({ status: 404, code: 'finance.charge.not_found' });
  });

  it('mantem o status e o Retry-After quando o corpo da falha nao e um problema', async () => {
    const result = firstValueFrom(api.download('c1'));

    http.expectOne(URL).flush(new Blob(['nao e json']), {
      status: 429,
      statusText: 'Too Many Requests',
      headers: { 'Retry-After': '30' },
    });

    const error: unknown = await result.catch((failure: unknown) => failure);
    expect(toApiProblem(error)).toMatchObject({ status: 429, code: null, retryAfterSeconds: 30 });
  });

  describe('downloadMine', () => {
    const MINE = '/api/finance/me/charges/c1/invoice';

    it('baixa o PDF da propria fatura pela rota do morador, com o nome que a API sugeriu', async () => {
      const result = firstValueFrom(api.downloadMine('c1'));

      const request = http.expectOne(MINE);
      expect(request.request.url).not.toBe(URL);
      expect(request.request.method).toBe('GET');
      expect(request.request.responseType).toBe('blob');
      const pdf = new Blob(['%PDF-1.7'], { type: 'application/pdf' });
      request.flush(pdf, {
        headers: { 'Content-Disposition': "attachment; filename*=UTF-8''fatura-2026-11-101.pdf" },
      });

      const download = await result;
      expect(download.fileName).toBe('fatura-2026-11-101.pdf');
      expect(download.blob).toBe(pdf);
    });

    it('usa o nome padrao quando a resposta nao traz o nome', async () => {
      const result = firstValueFrom(api.downloadMine('c1'));

      http.expectOne(MINE).flush(new Blob(['%PDF-1.7']));

      expect((await result).fileName).toBe('fatura.pdf');
    });

    it('devolve o codigo da API mesmo com a falha chegando como arquivo', async () => {
      const result = firstValueFrom(api.downloadMine('c1'));

      http.expectOne(MINE).flush(
        new Blob([JSON.stringify({ status: 404, code: 'finance.charge.not_found' })], {
          type: 'application/problem+json',
        }),
        { status: 404, statusText: 'Not Found' },
      );

      const error: unknown = await result.catch((failure: unknown) => failure);
      expect(error).toBeInstanceOf(HttpErrorResponse);
      expect(toApiProblem(error)).toMatchObject({ status: 404, code: 'finance.charge.not_found' });
    });
  });
});
