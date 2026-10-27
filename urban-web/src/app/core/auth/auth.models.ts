export type UserRole = 'SuperAdmin' | 'Manager' | 'Resident';

/** Resposta de GET /api/auth/me. */
export interface CurrentUser {
  readonly id: string;
  readonly email: string;
  readonly role: UserRole;
  /** Nulo para o SuperAdmin e para o síndico que ainda não cadastrou o condomínio. */
  readonly condominiumId: string | null;
}

/** Resposta de GET /api/terms/current: a versão dos termos que a pessoa precisa aceitar. */
export interface CurrentTerms {
  readonly version: string;
}

/** Resposta de login e refresh. O token de renovação vai só no cookie HttpOnly. */
export interface AccessTokenResponse {
  readonly accessToken: string;
  readonly accessTokenExpiresAt: string;
}
