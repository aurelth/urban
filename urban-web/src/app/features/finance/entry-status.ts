import { EntryStatus, ExpenseEntry, SettableEntryStatus } from '../../core/finance/finance.models';

const LABELS: Readonly<Record<EntryStatus, string>> = {
  pending: 'Pendente',
  paid: 'Pago',
  overdue: 'Vencido',
  canceled: 'Cancelado',
};

const ICONS: Readonly<Record<EntryStatus, string>> = {
  pending: 'schedule',
  paid: 'check_circle',
  overdue: 'error',
  canceled: 'block',
};

const TONES: Readonly<Record<EntryStatus, string>> = {
  pending: 'text-warning',
  paid: 'text-success',
  overdue: 'text-error',
  canceled: 'text-on-surface-variant',
};

const NOTICES: Readonly<Record<SettableEntryStatus, string>> = {
  pending: 'Lançamento marcado como pendente',
  paid: 'Lançamento marcado como pago',
  canceled: 'Lançamento cancelado',
};

/** Só estas três se marcam à mão: "vencido" é a situação de um pendente depois do vencimento, calculada pela API. */
export const STATUS_CHOICES: readonly {
  readonly status: SettableEntryStatus;
  readonly label: string;
}[] = [
  { status: 'pending', label: LABELS.pending },
  { status: 'paid', label: LABELS.paid },
  { status: 'canceled', label: LABELS.canceled },
];

export function statusLabel(status: EntryStatus): string {
  return LABELS[status];
}

/** Estado nunca depende só de cor: cada situação tem um ícone e um texto. */
export function statusIcon(status: EntryStatus): string {
  return ICONS[status];
}

export function statusTone(status: EntryStatus): string {
  return TONES[status];
}

export function statusNotice(status: SettableEntryStatus): string {
  return NOTICES[status];
}

/** Não repete a situação atual (vencido conta como pendente) e só deixa pagar depois de informar o valor. */
export function isChoiceDisabled(item: ExpenseEntry, choice: SettableEntryStatus): boolean {
  switch (choice) {
    case 'pending':
      return item.status === 'pending' || item.status === 'overdue';
    case 'paid':
      return item.status === 'paid' || item.needsAmount;
    case 'canceled':
      return item.status === 'canceled';
  }
}
