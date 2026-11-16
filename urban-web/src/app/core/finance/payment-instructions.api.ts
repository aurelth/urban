import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PaymentInstructions, PaymentInstructionsPayload } from './payment-instructions.models';

const URL = '/api/finance/payment-instructions';

/** Chamadas HTTP dos dados para pagamento (chave Pix e conta bancária), uma por endpoint. */
@Injectable({ providedIn: 'root' })
export class PaymentInstructionsApi {
  private readonly http = inject(HttpClient);

  get(): Observable<PaymentInstructions> {
    return this.http.get<PaymentInstructions>(URL);
  }

  save(payload: PaymentInstructionsPayload): Observable<PaymentInstructions> {
    return this.http.put<PaymentInstructions>(URL, payload);
  }
}
