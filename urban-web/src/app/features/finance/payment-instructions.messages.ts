import { MessagesByCode } from '../../core/http/problem-message';

/** Recusas da API ao gravar os dados para pagamento. */
export const PAYMENT_INSTRUCTIONS_MESSAGES: MessagesByCode = {
  'finance.payment_instructions.pix_kind_invalid': 'Escolha o tipo da chave Pix',
  'finance.payment_instructions.pix_key_invalid': 'A chave Pix não é válida para o tipo escolhido',
  'finance.payment_instructions.bank_invalid': 'Informe o banco, com 2 a 60 caracteres',
  'finance.payment_instructions.agency_invalid':
    'Informe a agência com até 5 dígitos, e o dígito verificador depois de um hífen, se tiver',
  'finance.payment_instructions.account_number_invalid':
    'Informe a conta com até 12 dígitos, e o dígito verificador depois de um hífen, se tiver',
  'finance.payment_instructions.account_kind_invalid': 'Escolha se a conta é corrente ou poupança',
};

/** Texto de cada erro de campo (as chaves vêm de `paymentFormErrors`). */
export const PIX_KEY_ERRORS = {
  required: 'Informe a chave Pix',
  cnpj: 'Informe um CNPJ válido, com 14 caracteres',
  cpf: 'Informe um CPF com 11 dígitos',
  email: 'Informe um e-mail válido, com até 77 caracteres',
  phone: 'Informe um celular com DDD',
  random: 'Informe a chave aleatória no formato 123e4567-e89b-12d3-a456-426614174000',
};

export const BANK_ERRORS = {
  required: 'Informe o banco',
  bank: 'O nome do banco deve ter de 2 a 60 caracteres',
};

export const AGENCY_ERRORS = {
  required: 'Informe a agência',
  agency:
    'Informe a agência com até 5 dígitos, e o dígito verificador depois de um hífen, se tiver',
};

export const ACCOUNT_NUMBER_ERRORS = {
  required: 'Informe o número da conta',
  number: 'Informe a conta com até 12 dígitos, e o dígito verificador depois de um hífen, se tiver',
};

export const ACCOUNT_KIND_ERRORS = { required: 'Escolha o tipo da conta' };
