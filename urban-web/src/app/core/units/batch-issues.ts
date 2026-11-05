import { HttpErrorResponse } from '@angular/common/http';

/** Um problema do lote: a posição (a partir de 0, na ordem enviada) e o código do erro da API. */
export interface BatchIssue {
  readonly position: number;
  readonly code: string;
}

/** Lê `errors` do problem+json do cadastro em lote. O que vier mal formado é descartado. */
export function toBatchIssues(error: unknown): BatchIssue[] {
  if (!(error instanceof HttpErrorResponse) || !isRecord(error.error)) {
    return [];
  }
  const errors: unknown = error.error['errors'];
  if (!Array.isArray(errors)) {
    return [];
  }
  return errors.filter(isIssue);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function isIssue(value: unknown): value is BatchIssue {
  return (
    isRecord(value) &&
    typeof value['position'] === 'number' &&
    Number.isInteger(value['position']) &&
    value['position'] >= 0 &&
    typeof value['code'] === 'string'
  );
}
