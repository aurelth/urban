import { SelectOption } from '../../shared/ui/select-field/select.field';
import {
  ChargeStatus,
  MonthPayments,
  PaymentMethod,
  PaymentsStatusFilter,
} from '../../core/finance/payments.models';

export interface ChargeStatusView {
  readonly label: string;
  readonly icon: string;
  /** Classe de cor do estado: laranja em andamento, verde sucesso, vermelho erro. */
  readonly tone: string;
}

const STATUS_VIEWS: Readonly<Record<ChargeStatus, ChargeStatusView>> = {
  open: { label: 'Em aberto', icon: 'schedule', tone: 'text-warning' },
  partial: { label: 'Parcial', icon: 'trending_up', tone: 'text-warning' },
  paid: { label: 'Paga', icon: 'check_circle', tone: 'text-success' },
  overdue: { label: 'Atrasada', icon: 'error', tone: 'text-error' },
};

/** Texto, ícone e cor de cada situação: o estado nunca depende só da cor. */
export function chargeStatusView(status: ChargeStatus): ChargeStatusView {
  return STATUS_VIEWS[status];
}

export const PAYMENT_METHODS: readonly SelectOption[] = [
  { value: 'pix', label: 'Pix' },
  { value: 'boleto', label: 'Boleto' },
  { value: 'transfer', label: 'Transferência' },
  { value: 'cash', label: 'Dinheiro' },
  { value: 'other', label: 'Outra' },
];

export function methodLabel(method: PaymentMethod): string {
  return PAYMENT_METHODS.find((option) => option.value === method)?.label ?? method;
}

/** "Recibo REC-000123", com " · cancelado" quando o pagamento foi estornado (o recibo continua, marcado). */
export function receiptLabel(code: string, reversed: boolean): string {
  return reversed ? `Recibo ${code} · cancelado` : `Recibo ${code}`;
}

export const STATUS_FILTERS: readonly (SelectOption & { readonly value: PaymentsStatusFilter })[] =
  [
    { value: 'all', label: 'Todas as situações' },
    { value: 'open', label: 'Em aberto' },
    { value: 'partial', label: 'Parciais' },
    { value: 'paid', label: 'Pagas' },
    { value: 'overdue', label: 'Atrasadas' },
  ];

/** O que ainda falta receber no mês inteiro: o cobrado menos o recebido, em centavos exatos. */
export function toReceive(data: MonthPayments): number {
  return Math.round((data.charged - data.received) * 100) / 100;
}
