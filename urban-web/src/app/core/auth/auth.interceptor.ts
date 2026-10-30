import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { SessionStore } from './session.store';

/** Rotas da API que funcionam sem login: não levam o token e um 401 delas não pede renovação. */
const PUBLIC_PATHS: ReadonlySet<string> = new Set([
  '/api/auth/login',
  '/api/auth/refresh',
  '/api/auth/register',
  '/api/auth/confirm-email',
  '/api/auth/forgot-password',
  '/api/auth/reset-password',
  '/api/auth/confirm-email-change',
  '/api/terms/current',
]);

function requiresSession(url: string): boolean {
  const path = url.split('?')[0];
  return path.startsWith('/api/') && !PUBLIC_PATHS.has(path);
}

function withBearer(request: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token === null
    ? request
    : request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

/**
 * Coloca o token nas chamadas à API. Se a API responder 401 (token vencido), renova a sessão e repete
 * a chamada uma única vez; se a renovação falhar, a sessão cai e o 401 original segue para quem chamou.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!requiresSession(request.url)) {
    return next(request);
  }
  const session = inject(SessionStore);

  return next(withBearer(request, session.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }
      return from(session.renew()).pipe(
        switchMap((renewed) => {
          if (!renewed) {
            session.expire();
            return throwError(() => error);
          }
          return next(withBearer(request, session.accessToken()));
        }),
      );
    }),
  );
};
