import { PaymentInstructions } from '../../core/finance/payment-instructions.models';
import {
  ACCOUNT_KIND_OPTIONS,
  EMPTY_FORM_VALUE,
  PIX_KIND_OPTIONS,
  PaymentFormValue,
  formChanged,
  paymentFormErrors,
  pixKeyHint,
  pixKeyMask,
  toFormValue,
  toPayload,
} from './payment-instructions.form';

const FILLED: PaymentFormValue = {
  pixKind: 'cnpj',
  pixKey: '11.222.333/0001-81',
  bank: 'Banco do Brasil',
  agency: '1234-5',
  number: '98765-4',
  accountKind: 'checking',
};

function withValue(overrides: Partial<PaymentFormValue>): PaymentFormValue {
  return { ...FILLED, ...overrides };
}

describe('opcoes de tipo', () => {
  it('oferece os cinco tipos de chave Pix, na ordem da tela', () => {
    expect(PIX_KIND_OPTIONS.map((option) => [option.value, option.label])).toEqual([
      ['cnpj', 'CNPJ'],
      ['cpf', 'CPF'],
      ['email', 'E-mail'],
      ['phone', 'Celular'],
      ['random', 'Chave aleatória'],
    ]);
  });

  it('oferece conta corrente e poupanca', () => {
    expect(ACCOUNT_KIND_OPTIONS.map((option) => [option.value, option.label])).toEqual([
      ['checking', 'Conta corrente'],
      ['savings', 'Poupança'],
    ]);
  });
});

describe('pixKeyMask e pixKeyHint', () => {
  it('usa a mascara do documento ou do telefone e nenhuma para e-mail e chave aleatoria', () => {
    expect(pixKeyMask('cnpj')).toBe('cnpj');
    expect(pixKeyMask('cpf')).toBe('cpf');
    expect(pixKeyMask('phone')).toBe('phone');
    expect(pixKeyMask('email')).toBeNull();
    expect(pixKeyMask('random')).toBeNull();
    expect(pixKeyMask('')).toBeNull();
  });

  it('so a chave aleatoria tem dica, com o formato que o banco mostra', () => {
    expect(pixKeyHint('random')).toContain('123e4567-e89b-12d3-a456-426614174000');
    expect(pixKeyHint('cnpj')).toBeNull();
    expect(pixKeyHint('')).toBeNull();
  });
});

describe('toFormValue', () => {
  it('sem dados cadastrados, o formulario fica vazio', () => {
    expect(toFormValue(null)).toEqual(EMPTY_FORM_VALUE);
    expect(toFormValue({ pix: null, account: null, updatedAt: null })).toEqual(EMPTY_FORM_VALUE);
    expect(EMPTY_FORM_VALUE).toEqual({
      pixKind: '',
      pixKey: '',
      bank: '',
      agency: '',
      number: '',
      accountKind: '',
    });
  });

  it('mostra a chave com a mascara do tipo e a conta como foi gravada', () => {
    const saved: PaymentInstructions = {
      pix: { kind: 'cnpj', key: '11222333000181' },
      account: { bank: 'Banco do Brasil', agency: '1234-5', number: '98765-4', kind: 'savings' },
      updatedAt: '2026-11-16T13:30:00+00:00',
    };

    expect(toFormValue(saved)).toEqual({
      pixKind: 'cnpj',
      pixKey: '11.222.333/0001-81',
      bank: 'Banco do Brasil',
      agency: '1234-5',
      number: '98765-4',
      accountKind: 'savings',
    });
  });

  it('mostra o CPF e o celular com mascara, sem o +55 que a API guarda', () => {
    const cpf = toFormValue({
      pix: { kind: 'cpf', key: '52998224725' },
      account: null,
      updatedAt: null,
    });
    const phone = toFormValue({
      pix: { kind: 'phone', key: '+5547991234567' },
      account: null,
      updatedAt: null,
    });

    expect(cpf.pixKey).toBe('529.982.247-25');
    expect(phone.pixKey).toBe('(47) 99123-4567');
  });

  it('mostra e-mail e chave aleatoria do jeito que foram gravados', () => {
    const email = toFormValue({
      pix: { kind: 'email', key: 'caixa@predio.com.br' },
      account: null,
      updatedAt: null,
    });
    const random = toFormValue({
      pix: { kind: 'random', key: '123e4567-e89b-12d3-a456-426614174000' },
      account: null,
      updatedAt: null,
    });

    expect(email.pixKey).toBe('caixa@predio.com.br');
    expect(random.pixKey).toBe('123e4567-e89b-12d3-a456-426614174000');
  });

  it('so a chave Pix cadastrada preenche a chave, e so a conta preenche a conta', () => {
    const onlyAccount = toFormValue({
      pix: null,
      account: { bank: 'Caixa', agency: '0001', number: '123', kind: 'checking' },
      updatedAt: null,
    });

    expect(onlyAccount.pixKind).toBe('');
    expect(onlyAccount.pixKey).toBe('');
    expect(onlyAccount.bank).toBe('Caixa');
  });
});

describe('toPayload', () => {
  it('manda a chave sem mascara e a conta aparada', () => {
    expect(toPayload(withValue({ bank: '  Banco do Brasil ', agency: ' 1234-5 ' }))).toEqual({
      pix: { kind: 'cnpj', key: '11222333000181' },
      account: { bank: 'Banco do Brasil', agency: '1234-5', number: '98765-4', kind: 'checking' },
    });
  });

  it('tira o formato de CPF e celular e deixa o +55 para a API', () => {
    expect(toPayload(withValue({ pixKind: 'cpf', pixKey: '529.982.247-25' })).pix).toEqual({
      kind: 'cpf',
      key: '52998224725',
    });
    expect(toPayload(withValue({ pixKind: 'phone', pixKey: '(47) 99123-4567' })).pix).toEqual({
      kind: 'phone',
      key: '47991234567',
    });
  });

  it('apara o e-mail e a chave aleatoria sem mexer no resto', () => {
    expect(
      toPayload(withValue({ pixKind: 'email', pixKey: '  caixa@predio.com.br ' })).pix,
    ).toEqual({
      kind: 'email',
      key: 'caixa@predio.com.br',
    });
    expect(
      toPayload(withValue({ pixKind: 'random', pixKey: ' 123e4567-e89b-12d3-a456-426614174000 ' }))
        .pix,
    ).toEqual({ kind: 'random', key: '123e4567-e89b-12d3-a456-426614174000' });
  });

  it('"Nao informar" no tipo da chave tira a chave, mesmo com texto no campo', () => {
    expect(toPayload(withValue({ pixKind: '' })).pix).toBeNull();
  });

  it('com a conta toda em branco, tira a conta', () => {
    const payload = toPayload(withValue({ bank: '', agency: '  ', number: '', accountKind: '' }));

    expect(payload.account).toBeNull();
    expect(payload.pix).not.toBeNull();
  });

  it('com a conta so comecada, manda o que tem e a API recusa o que falta', () => {
    const payload = toPayload(withValue({ number: '', accountKind: '' }));

    expect(payload.account).toEqual({
      bank: 'Banco do Brasil',
      agency: '1234-5',
      number: '',
      kind: '',
    });
  });

  it('formulario vazio manda tudo nulo', () => {
    expect(toPayload(EMPTY_FORM_VALUE)).toEqual({ pix: null, account: null });
  });
});

describe('formChanged', () => {
  it('espaco e mascara nao contam como mudanca', () => {
    const original = withValue({ pixKey: '11.222.333/0001-81' });

    expect(formChanged(original, withValue({ pixKey: '11222333000181' }))).toBe(false);
    expect(formChanged(original, withValue({ bank: ' Banco do Brasil  ' }))).toBe(false);
  });

  it('muda quando algo que vai para a API muda', () => {
    expect(formChanged(FILLED, withValue({ agency: '9999' }))).toBe(true);
    expect(formChanged(FILLED, withValue({ pixKind: 'cpf', pixKey: '529.982.247-25' }))).toBe(true);
    expect(formChanged(FILLED, withValue({ accountKind: 'savings' }))).toBe(true);
  });

  it('esvaziar a chave ou a conta e mudanca; digitar numa chave sem tipo nao e', () => {
    expect(formChanged(FILLED, withValue({ pixKind: '' }))).toBe(true);
    expect(
      formChanged(FILLED, withValue({ bank: '', agency: '', number: '', accountKind: '' })),
    ).toBe(true);
    expect(formChanged(EMPTY_FORM_VALUE, { ...EMPTY_FORM_VALUE, pixKey: 'sobrou' })).toBe(false);
  });
});

describe('paymentFormErrors', () => {
  it('formulario completo e valido', () => {
    expect(paymentFormErrors(FILLED)).toEqual({});
  });

  it('formulario vazio e valido: nada precisa ser informado', () => {
    expect(paymentFormErrors(EMPTY_FORM_VALUE)).toEqual({});
  });

  it('com um tipo de chave escolhido, a chave e obrigatoria', () => {
    expect(paymentFormErrors(withValue({ pixKey: '   ' }))).toEqual({ pixKey: 'required' });
  });

  it('sem tipo de chave, o texto da chave nao e conferido', () => {
    expect(paymentFormErrors(withValue({ pixKind: '', pixKey: 'qualquer coisa' }))).toEqual({});
  });

  it.each([
    ['cnpj', '11.222.333/0001', 'cnpj'],
    ['cnpj', '11.222.333/0001-8X', 'cnpj'],
    ['cpf', '529.982.247', 'cpf'],
    ['email', 'sem-arroba', 'email'],
    ['email', 'a@b', 'email'],
    ['phone', '(47) 3333-4444', 'phone'],
    ['phone', '(47) 9912', 'phone'],
    ['random', '123', 'random'],
  ])('recusa a chave %s "%s" com o erro %s', (kind, key, error) => {
    expect(paymentFormErrors(withValue({ pixKind: kind as 'cnpj', pixKey: key }))).toEqual({
      pixKey: error,
    });
  });

  it.each([
    ['cnpj', '12.ABC.345/01DE-35'],
    ['cnpj', '11222333000181'],
    ['cpf', '529.982.247-25'],
    ['email', 'caixa@predio.com.br'],
    ['phone', '(47) 99123-4567'],
    ['phone', '+55 47 99123-4567'],
    ['random', '123e4567-e89b-12d3-a456-426614174000'],
    ['random', '123E4567-E89B-12D3-A456-426614174000'],
  ])('aceita a chave %s "%s"', (kind, key) => {
    expect(paymentFormErrors(withValue({ pixKind: kind as 'cnpj', pixKey: key }))).toEqual({});
  });

  it('recusa e-mail com mais de 77 caracteres, como a API', () => {
    const long = `${'a'.repeat(72)}@x.com`;
    const fits = `${'a'.repeat(71)}@x.com`;

    expect(paymentFormErrors(withValue({ pixKind: 'email', pixKey: long }))).toEqual({
      pixKey: 'email',
    });
    expect(paymentFormErrors(withValue({ pixKind: 'email', pixKey: fits }))).toEqual({});
  });

  it('comecada a conta, banco, agencia, conta e tipo passam a ser obrigatorios', () => {
    expect(
      paymentFormErrors(withValue({ bank: 'Caixa', agency: '', number: '', accountKind: '' })),
    ).toEqual({
      agency: 'required',
      number: 'required',
      accountKind: 'required',
    });
    expect(
      paymentFormErrors(withValue({ bank: '', agency: '', number: '123', accountKind: '' })),
    ).toEqual({
      bank: 'required',
      agency: 'required',
      accountKind: 'required',
    });
    expect(
      paymentFormErrors(withValue({ bank: '', agency: '', number: '', accountKind: 'savings' })),
    ).toEqual({
      bank: 'required',
      agency: 'required',
      number: 'required',
    });
  });

  it('banco so de espacos conta como nao preenchido', () => {
    expect(paymentFormErrors(withValue({ bank: '   ' }))).toEqual({ bank: 'required' });
  });

  it('o banco tem de 2 a 60 caracteres, sem espacos nas pontas', () => {
    expect(paymentFormErrors(withValue({ bank: 'B' }))).toEqual({ bank: 'bank' });
    expect(paymentFormErrors(withValue({ bank: ' B ' }))).toEqual({ bank: 'bank' });
    expect(paymentFormErrors(withValue({ bank: 'B'.repeat(61) }))).toEqual({ bank: 'bank' });
    expect(paymentFormErrors(withValue({ bank: 'BB' }))).toEqual({});
    expect(paymentFormErrors(withValue({ bank: 'B'.repeat(60) }))).toEqual({});
  });

  it.each(['abc', '123456', '1234-', '1234-55', '-1'])('recusa a agencia "%s"', (agency) => {
    expect(paymentFormErrors(withValue({ agency }))).toEqual({ agency: 'agency' });
  });

  it.each(['1', '12345', '1234-5', '1234-x', '1234-X'])('aceita a agencia "%s"', (agency) => {
    expect(paymentFormErrors(withValue({ agency }))).toEqual({});
  });

  it.each(['abc', '1234567890123', '123-', '123-45'])('recusa a conta "%s"', (number) => {
    expect(paymentFormErrors(withValue({ number }))).toEqual({ number: 'number' });
  });

  it.each(['1', '123456789012', '98765-4', '98765-x'])('aceita a conta "%s"', (number) => {
    expect(paymentFormErrors(withValue({ number }))).toEqual({});
  });

  it('junta os erros da chave e da conta', () => {
    expect(paymentFormErrors({ ...FILLED, pixKey: '', agency: 'abc', accountKind: '' })).toEqual({
      pixKey: 'required',
      agency: 'agency',
      accountKind: 'required',
    });
  });
});
