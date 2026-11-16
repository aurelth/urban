import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ChargePayments,
  MonthPayments,
  Payment,
  PaymentsQuery,
  RegisterPaymentPayload,
} from './payments.models';

const BASE = '/api/finance';

/** Chamadas HTTP dos pagamentos, uma por endpoint. */
@Injectable({ providedIn: 'root' })
export class PaymentsApi {
  private readonly http = inject(HttpClient);

  listMonth(month: string, query: PaymentsQuery): Observable<MonthPayments> {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    if (query.status !== 'all') {
      params = params.set('status', query.status);
    }
    return this.http.get<MonthPayments>(`${BASE}/months/${month}/payments`, { params });
  }

  listCharge(chargeId: string): Observable<ChargePayments> {
    return this.http.get<ChargePayments>(`${BASE}/charges/${chargeId}/payments`);
  }

  register(chargeId: string, payload: RegisterPaymentPayload): Observable<Payment> {
    return this.http.post<Payment>(`${BASE}/charges/${chargeId}/payments`, payload);
  }

  reverse(paymentId: string, reason: string): Observable<void> {
    return this.http.post<void>(`${BASE}/payments/${paymentId}/reverse`, { reason });
  }
}
