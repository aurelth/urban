import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  InvoiceDeliveriesQuery,
  MonthInvoiceDeliveries,
  SendMonthInvoicesResult,
} from './invoice-deliveries.models';

const BASE = '/api/finance';

/** Chamadas HTTP do envio das faturas por e-mail, uma por endpoint. */
@Injectable({ providedIn: 'root' })
export class InvoiceDeliveriesApi {
  private readonly http = inject(HttpClient);

  listMonth(month: string, query: InvoiceDeliveriesQuery): Observable<MonthInvoiceDeliveries> {
    const params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    return this.http.get<MonthInvoiceDeliveries>(`${BASE}/months/${month}/invoice-deliveries`, {
      params,
    });
  }

  /** Põe na fila as faturas do mês que ainda não foram enviadas (e as que falharam). */
  sendMonth(month: string): Observable<SendMonthInvoicesResult> {
    return this.http.post<SendMonthInvoicesResult>(
      `${BASE}/months/${month}/invoice-deliveries`,
      null,
    );
  }

  /** Põe na fila a fatura de uma cobrança, mesmo que já tenha sido enviada. */
  resend(chargeId: string): Observable<void> {
    return this.http.post<void>(`${BASE}/charges/${chargeId}/invoice-deliveries`, null);
  }
}
