import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { PaymentInstructionsApi } from './payment-instructions.api';
import { PaymentInstructions } from './payment-instructions.models';

const URL = '/api/finance/payment-instructions';

const INSTRUCTIONS: PaymentInstructions = {
  pix: { kind: 'cnpj', key: '11222333000181' },
  account: { bank: 'Banco do Brasil', agency: '1234-5', number: '98765-4', kind: 'checking' },
  updatedAt: '2026-11-16T13:30:00+00:00',
};

describe('PaymentInstructionsApi', () => {
  let api: PaymentInstructionsApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(PaymentInstructionsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('busca os dados para pagamento do condominio', async () => {
    const result = firstValueFrom(api.get());

    const request = http.expectOne(URL);
    expect(request.request.method).toBe('GET');
    request.flush(INSTRUCTIONS);

    expect(await result).toEqual(INSTRUCTIONS);
  });

  it('grava a chave Pix e a conta com um PUT e devolve o que ficou gravado', async () => {
    const payload = { pix: INSTRUCTIONS.pix, account: INSTRUCTIONS.account };
    const result = firstValueFrom(api.save(payload));

    const request = http.expectOne(URL);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(payload);
    request.flush(INSTRUCTIONS);

    expect(await result).toEqual(INSTRUCTIONS);
  });

  it('manda nulo para tirar uma das partes', async () => {
    const result = firstValueFrom(api.save({ pix: null, account: INSTRUCTIONS.account }));

    const request = http.expectOne(URL);
    expect(request.request.body).toEqual({ pix: null, account: INSTRUCTIONS.account });
    request.flush({ ...INSTRUCTIONS, pix: null });

    expect((await result).pix).toBeNull();
  });
});
