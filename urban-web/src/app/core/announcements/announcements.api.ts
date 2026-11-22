import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Announcement,
  AnnouncementCategory,
  AnnouncementPage,
  AnnouncementPayload,
  MarkAllResult,
  MyAnnouncementPage,
  MyAnnouncementsQuery,
  UpdateAnnouncementPayload,
} from './announcements.models';

const BASE = '/api/announcements';

/** Chamadas HTTP dos comunicados do síndico, uma por endpoint. */
@Injectable({ providedIn: 'root' })
export class AnnouncementsApi {
  private readonly http = inject(HttpClient);

  list(page: number, pageSize: number): Observable<AnnouncementPage> {
    return this.http.get<AnnouncementPage>(BASE, {
      params: new HttpParams().set('page', page).set('pageSize', pageSize),
    });
  }

  create(payload: AnnouncementPayload): Observable<Announcement> {
    return this.http.post<Announcement>(BASE, payload);
  }

  update(id: string, payload: UpdateAnnouncementPayload): Observable<Announcement> {
    return this.http.put<Announcement>(`${BASE}/${id}`, payload);
  }

  /** A versão vai na consulta: a API não lê corpo no DELETE. */
  remove(id: string, version: string): Observable<void> {
    return this.http.delete<void>(`${BASE}/${id}`, { params: { version } });
  }

  /** O mural de quem mora: só manda os filtros que a pessoa ligou. */
  listMine(query: MyAnnouncementsQuery): Observable<MyAnnouncementPage> {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    if (query.unreadOnly) {
      params = params.set('unreadOnly', true);
    }
    if (query.category !== null) {
      params = params.set('category', query.category);
    }
    return this.http.get<MyAnnouncementPage>(`${BASE}/mine`, { params });
  }

  markRead(id: string): Observable<void> {
    return this.http.post<void>(`${BASE}/${id}/read`, null);
  }

  /** Sem categoria, marca o mural inteiro; com ela, só os avisos dela. */
  markAllRead(category: AnnouncementCategory | null): Observable<MarkAllResult> {
    const params =
      category === null ? new HttpParams() : new HttpParams().set('category', category);
    return this.http.post<MarkAllResult>(`${BASE}/read-all`, null, { params });
  }
}
