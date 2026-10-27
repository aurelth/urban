const LOGIN_PATH = '/entrar';
const BASE = 'http://urban.invalid';

/**
 * Destino depois do login. Só aceita caminho interno do próprio site: qualquer outra coisa
 * (endereço completo, "//host", barras invertidas, "javascript:") vira o destino padrão,
 * senão o link /entrar?returnUrl=... serviria para redirecionar a pessoa a um site de terceiros.
 */
export function safeReturnUrl(raw: string | null | undefined, fallback = '/dashboard'): string {
  if (!raw?.startsWith('/')) {
    return fallback;
  }
  const url = new URL(raw, BASE);
  if (url.origin !== BASE || url.pathname === LOGIN_PATH) {
    return fallback;
  }
  return `${url.pathname}${url.search}${url.hash}`;
}
