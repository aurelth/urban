import {
  BankAccountInput,
  BankAccountKind,
  PaymentInstructions,
  PaymentInstructionsPayload,
  PixKey,
  PixKeyKind,
} from '../../core/finance/payment-instructions.models';
import { isEmailFormat } from '../../shared/forms/email.validator';
import { MaskKind, applyMask, unmask } from '../../shared/forms/masks';
import { SelectOption } from '../../shared/ui/select-field/select.field';

/** Valor do tipo da chave no formulário: vazio é "Não informar". */
export type PixKindValue = PixKeyKind | '';

export interface PaymentFormValue {
  readonly pixKind: PixKindValue;
  readonly pixKey: string;
  readonly bank: string;
  readonly agency: string;
  readonly number: string;
  readonly accountKind: BankAccountKind | '';
}

export type PaymentFormField = keyof PaymentFormValue;

/** A chave do erro de cada campo com problema; campo sem entrada está em ordem. */
export type PaymentFormErrors = Partial<Record<PaymentFormField, string>>;

export const EMPTY_FORM_VALUE: PaymentFormValue = {
  pixKind: '',
  pixKey: '',
  bank: '',
  agency: '',
  number: '',
  accountKind: '',
};

export const PIX_KIND_OPTIONS: readonly SelectOption[] = [
  { value: 'cnpj', label: 'CNPJ' },
  { value: 'cpf', label: 'CPF' },
  { value: 'email', label: 'E-mail' },
  { value: 'phone', label: 'Celular' },
  { value: 'random', label: 'Chave aleatória' },
];

export const ACCOUNT_KIND_OPTIONS: readonly SelectOption[] = [
  { value: 'checking', label: 'Conta corrente' },
  { value: 'savings', label: 'Poupança' },
];

// Os limites e formatos abaixo espelham a API só para avisar na hora; quem decide é ela.
const BANK_MIN_LENGTH = 2;
const BANK_MAX_LENGTH = 60;
const EMAIL_MAX_LENGTH = 77;
const CNPJ_KEY = /^[0-9A-Z]{12}[0-9]{2}$/;
const CPF_KEY = /^[0-9]{11}$/;
const MOBILE_KEY = /^[1-9][1-9]9[0-9]{8}$/;
const RANDOM_KEY = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const AGENCY = /^[0-9]{1,5}(-[0-9X])?$/i;
const ACCOUNT_NUMBER = /^[0-9]{1,12}(-[0-9X])?$/i;

const PIX_KEY_CHECKS: Readonly<Record<PixKeyKind, (text: string) => boolean>> = {
  cnpj: (text) => CNPJ_KEY.test(unmask('cnpj', text)),
  cpf: (text) => CPF_KEY.test(unmask('cpf', text)),
  email: (text) => text.length <= EMAIL_MAX_LENGTH && isEmailFormat(text),
  phone: (text) => MOBILE_KEY.test(unmask('phone', text)),
  random: (text) => RANDOM_KEY.test(text.toLowerCase()),
};

/** A máscara de digitação de cada tipo de chave: e-mail e chave aleatória não têm. */
export function pixKeyMask(kind: PixKindValue): MaskKind | null {
  return kind === 'cnpj' || kind === 'cpf' || kind === 'phone' ? kind : null;
}

export function pixKeyHint(kind: PixKindValue): string | null {
  return kind === 'random'
    ? 'Cole a chave como o banco a mostra, por exemplo 123e4567-e89b-12d3-a456-426614174000'
    : null;
}

/** O formulário com o que está cadastrado: a chave aparece com a máscara do tipo, sem o +55 que a API guarda. */
export function toFormValue(instructions: PaymentInstructions | null): PaymentFormValue {
  const pix = instructions?.pix ?? null;
  const account = instructions?.account ?? null;
  return {
    pixKind: pix?.kind ?? '',
    pixKey: pix === null ? '' : shownKey(pix),
    bank: account?.bank ?? '',
    agency: account?.agency ?? '',
    number: account?.number ?? '',
    accountKind: account?.kind ?? '',
  };
}

/** O que vai para a API: chave sem máscara; "Não informar" e conta em branco viram nulo, que os tira. */
export function toPayload(value: PaymentFormValue): PaymentInstructionsPayload {
  return {
    pix: value.pixKind === '' ? null : { kind: value.pixKind, key: keyToSend(value) },
    account: accountStarted(value) ? accountToSend(value) : null,
  };
}

/** Compara o que iria para a API, então espaço e máscara não contam como mudança. */
export function formChanged(original: PaymentFormValue, current: PaymentFormValue): boolean {
  return JSON.stringify(toPayload(original)) !== JSON.stringify(toPayload(current));
}

/** Os problemas de cada campo. Nada é obrigatório, mas a chave e a conta, uma vez começadas, precisam ficar completas. */
export function paymentFormErrors(value: PaymentFormValue): PaymentFormErrors {
  const errors: PaymentFormErrors = {};
  const pixKey = pixKeyError(value);
  if (pixKey !== null) {
    errors.pixKey = pixKey;
  }
  if (accountStarted(value)) {
    Object.assign(errors, accountErrors(value));
  }
  return errors;
}

function shownKey(pix: PixKey): string {
  const mask = pixKeyMask(pix.kind);
  return mask === null ? pix.key : applyMask(mask, pix.key);
}

function keyToSend(value: PaymentFormValue): string {
  const mask = pixKeyMask(value.pixKind);
  return mask === null ? value.pixKey.trim() : unmask(mask, value.pixKey);
}

function accountStarted(value: PaymentFormValue): boolean {
  return (
    [value.bank, value.agency, value.number].some((text) => text.trim() !== '') ||
    value.accountKind !== ''
  );
}

function accountToSend(value: PaymentFormValue): BankAccountInput {
  return {
    bank: value.bank.trim(),
    agency: value.agency.trim(),
    number: value.number.trim(),
    kind: value.accountKind,
  };
}

function pixKeyError(value: PaymentFormValue): string | null {
  if (value.pixKind === '') {
    return null;
  }
  const text = value.pixKey.trim();
  if (text === '') {
    return 'required';
  }
  return PIX_KEY_CHECKS[value.pixKind](text) ? null : value.pixKind;
}

function accountErrors(value: PaymentFormValue): PaymentFormErrors {
  const bank = value.bank.trim();
  const errors: PaymentFormErrors = {};
  if (bank === '') {
    errors.bank = 'required';
  } else if (bank.length < BANK_MIN_LENGTH || bank.length > BANK_MAX_LENGTH) {
    errors.bank = 'bank';
  }
  const agency = textError(value.agency, AGENCY, 'agency');
  if (agency !== null) {
    errors.agency = agency;
  }
  const number = textError(value.number, ACCOUNT_NUMBER, 'number');
  if (number !== null) {
    errors.number = number;
  }
  if (value.accountKind === '') {
    errors.accountKind = 'required';
  }
  return errors;
}

function textError(text: string, pattern: RegExp, key: string): string | null {
  const trimmed = text.trim();
  if (trimmed === '') {
    return 'required';
  }
  return pattern.test(trimmed) ? null : key;
}
