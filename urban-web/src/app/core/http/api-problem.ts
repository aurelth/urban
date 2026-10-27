import { HttpErrorResponse } from '@angular/common/http';

/** O que a API explicou sobre uma falha (application/problem+json) e o que o front precisa dela. */
export interface ApiProblem {
  /** Código HTTP. 0 quando não houve resposta (rede fora). */
  readonly status: number;
  /** Código estável da API (ex.: identity.login.invalid_credentials). */
  readonly code: string | null;
  readonly detail: string | null;
  /** Segundos pedidos pelo cabeçalho Retry-After (limite de requisições). */
  readonly retryAfterSeconds: number | null;
}

const NO_RESPONSE: ApiProblem = { status: 0, code: null, detail: null, retryAfterSeconds: null };

export function toApiProblem(error: unknown): ApiProblem {
  if (!(error instanceof HttpErrorResponse)) {
    return NO_RESPONSE;
  }
  const body: Record<string, unknown> = isRecord(error.error) ? error.error : {};
  return {
    status: error.status,
    code: textOrNull(body['code']),
    detail: textOrNull(body['detail']),
    retryAfterSeconds: parseSeconds(error.headers.get('Retry-After')),
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function textOrNull(value: unknown): string | null {
  return typeof value === 'string' ? value : null;
}

function parseSeconds(header: string | null): number | null {
  const seconds = header !== null && /^\d+$/.test(header) ? Number(header) : 0;
  return seconds > 0 ? seconds : null;
}
