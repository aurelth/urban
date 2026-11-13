/** Um pedido de cadastro de síndico que aguarda a decisão do administrador (item de GET /api/admin/registration-requests). */
export interface RegistrationRequest {
  readonly id: string;
  readonly email: string;
  /** Como a pessoa prefere ser chamada. */
  readonly name: string;
  readonly condominiumName: string;
  /** Quando o pedido chegou (ISO 8601). */
  readonly requestedAt: string;
}
