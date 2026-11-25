import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { toApiProblem } from '../http/api-problem';
import { ReceiptsApi } from './receipts.api';

const MANAGER = '/api/finance/payments/p1/receipt';
const MINE = '/api/finance/me/payments/p1/receipt';

describe('ReceiptsApi', () => {
  let api: ReceiptsApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(ReceiptsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  describe.each([
    ['download', MANAGER],
    ['downloadMine', MINE],
  ] as const)('%s', (method, url) => {
    it('baixa o PDF do recibo e devolve o arquivo com o nome que a API sugeriu', async () => {
      const result = firstValueFrom(api[method]('p1'));

      const request = http.expectOne(url);
      expect(request.request.method).toBe('GET');
      expect(request.request.responseType).toBe('blob');
      const pdf = new Blob(['%PDF-1.7'], { type: 'application/pdf' });
      request.flush(pdf, {
        headers: { 'Content-Disposition': "attachment; filename*=UTF-8''recibo-000123.pdf" },
      });

      const download = await result;
      expect(download.fileName).toBe('recibo-000123.pdf');
      expect(download.blob).toBe(pdf);
    });

    it('usa recibo.pdf quando a resposta nao traz o nome, nunca o nome da fatura', async () => {
      const result = firstValueFrom(api[method]('p1'));

      http.expectOne(url).flush(new Blob(['%PDF-1.7']));

      expect((await result).fileName).toBe('recibo.pdf');
    });

    it('devolve o codigo da API mesmo com a falha chegando como arquivo', async () => {
      const result = firstValueFrom(api[method]('p1'));

      http.expectOne(url).flush(
        new Blob([JSON.stringify({ status: 404, code: 'finance.payment.not_found' })], {
          type: 'application/problem+json',
        }),
        { status: 404, statusText: 'Not Found' },
      );

      const error: unknown = await result.catch((failure: unknown) => failure);
      expect(error).toBeInstanceOf(HttpErrorResponse);
      expect(toApiProblem(error)).toMatchObject({ status: 404, code: 'finance.payment.not_found' });
    });

    it('mantem o status e o Retry-After quando o corpo da falha nao e um problema', async () => {
      const result = firstValueFrom(api[method]('p1'));

      http.expectOne(url).flush(new Blob(['nao e json']), {
        status: 429,
        statusText: 'Too Many Requests',
        headers: { 'Retry-After': '30' },
      });

      const error: unknown = await result.catch((failure: unknown) => failure);
      expect(toApiProblem(error)).toMatchObject({ status: 429, code: null, retryAfterSeconds: 30 });
    });
  });

  it('a rota do sindico e a do morador sao diferentes', async () => {
    const mine = firstValueFrom(api.downloadMine('p1'));
    http.expectOne(MINE).flush(new Blob(['%PDF-1.7']));
    await mine;

    const manager = firstValueFrom(api.download('p1'));
    http.expectOne(MANAGER).flush(new Blob(['%PDF-1.7']));
    await manager;
  });

  describe('sendByEmail', () => {
    const DELIVERIES = '/api/finance/payments/p1/receipt-deliveries';

    it('pede o envio sem corpo e devolve quem vai receber', async () => {
      const result = firstValueFrom(api.sendByEmail('p1'));

      const request = http.expectOne(DELIVERIES);
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toBeNull();
      request.flush({ recipientName: 'Maria Souza' }, { status: 202, statusText: 'Accepted' });

      await expect(result).resolves.toEqual({ recipientName: 'Maria Souza' });
    });

    it.each([
      [422, 'finance.receipt_delivery.no_recipient'],
      [409, 'finance.receipt_delivery.payment_reversed'],
      [404, 'finance.payment.not_found'],
    ])('devolve o codigo da API na recusa %i', async (status, code) => {
      const result = firstValueFrom(api.sendByEmail('p1'));

      http.expectOne(DELIVERIES).flush({ status, code }, { status, statusText: 'Erro' });

      const error: unknown = await result.catch((failure: unknown) => failure);
      expect(error).toBeInstanceOf(HttpErrorResponse);
      expect(toApiProblem(error)).toMatchObject({ status, code });
    });

    it('mantem o Retry-After quando o limite de requisicoes e atingido', async () => {
      const result = firstValueFrom(api.sendByEmail('p1'));

      http
        .expectOne(DELIVERIES)
        .flush(
          { status: 429, code: 'rate_limit.exceeded' },
          { status: 429, statusText: 'Too Many Requests', headers: { 'Retry-After': '30' } },
        );

      const error: unknown = await result.catch((failure: unknown) => failure);
      expect(toApiProblem(error)).toMatchObject({ status: 429, retryAfterSeconds: 30 });
    });
  });
});
