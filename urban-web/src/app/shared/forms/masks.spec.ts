import { MASK_INPUTMODE, MaskKind, applyMask, caretAfter, countSignificant, unmask } from './masks';

describe('applyMask', () => {
  describe('cpf', () => {
    it.each([
      ['', ''],
      ['1', '1'],
      ['123', '123'],
      ['1234', '123.4'],
      ['123456', '123.456'],
      ['1234567', '123.456.7'],
      ['123456789', '123.456.789'],
      ['1234567890', '123.456.789-0'],
      ['12345678909', '123.456.789-09'],
    ])('formata "%s" como "%s"', (typed, shown) => {
      expect(applyMask('cpf', typed)).toBe(shown);
    });

    it('descarta o que passa de 11 dígitos e o que não é dígito', () => {
      expect(applyMask('cpf', '123456789012')).toBe('123.456.789-01');
      expect(applyMask('cpf', 'abc1x2')).toBe('12');
    });

    it('não muda um valor que já está formatado', () => {
      expect(applyMask('cpf', '123.456.789-09')).toBe('123.456.789-09');
    });
  });

  describe('cnpj', () => {
    it.each([
      ['', ''],
      ['11', '11'],
      ['112', '11.2'],
      ['11222', '11.222'],
      ['112223', '11.222.3'],
      ['11222333', '11.222.333'],
      ['112223330', '11.222.333/0'],
      ['112223330001', '11.222.333/0001'],
      ['1122233300018', '11.222.333/0001-8'],
      ['11222333000181', '11.222.333/0001-81'],
    ])('formata "%s" como "%s"', (typed, shown) => {
      expect(applyMask('cnpj', typed)).toBe(shown);
    });

    it('aceita letras nos 12 primeiros caracteres e as deixa em maiúsculas', () => {
      expect(applyMask('cnpj', '12abc34501de35')).toBe('12.ABC.345/01DE-35');
    });

    it('só aceita dígitos nos dois últimos caracteres, que são os verificadores', () => {
      expect(applyMask('cnpj', '12ABC34501DEXY')).toBe('12.ABC.345/01DE');
      expect(applyMask('cnpj', '12ABC34501DE3X')).toBe('12.ABC.345/01DE-3');
    });

    it('descarta acentos, símbolos e o que passa de 14 caracteres', () => {
      expect(applyMask('cnpj', 'ÁÇß-12*')).toBe('12');
      expect(applyMask('cnpj', '112223330001811')).toBe('11.222.333/0001-81');
    });

    it('não muda um valor que já está formatado', () => {
      expect(applyMask('cnpj', '12.ABC.345/01DE-35')).toBe('12.ABC.345/01DE-35');
    });
  });

  describe('cep', () => {
    it.each([
      ['', ''],
      ['1', '1'],
      ['12345', '12345'],
      ['123456', '12345-6'],
      ['12345678', '12345-678'],
      ['123456789', '12345-678'],
      ['89010-000', '89010-000'],
    ])('formata "%s" como "%s"', (typed, shown) => {
      expect(applyMask('cep', typed)).toBe(shown);
    });
  });

  describe('phone', () => {
    it.each([
      ['', ''],
      ['1', '(1'],
      ['11', '(11'],
      ['119', '(11) 9'],
      ['1191234', '(11) 9123-4'],
      ['1131234567', '(11) 3123-4567'],
      ['11912345678', '(11) 91234-5678'],
    ])('formata "%s" como "%s"', (typed, shown) => {
      expect(applyMask('phone', typed)).toBe(shown);
    });

    it('passa para o formato de celular quando chega ao 11º dígito', () => {
      expect(applyMask('phone', '1191234567')).toBe('(11) 9123-4567');
      expect(applyMask('phone', '11912345678')).toBe('(11) 91234-5678');
    });

    it('descarta o que passa de 11 dígitos', () => {
      expect(applyMask('phone', '119123456789')).toBe('(11) 91234-5678');
    });

    it('descarta o +55 colado com o número, mas só quando há o sinal de mais', () => {
      expect(applyMask('phone', '+55 11 91234-5678')).toBe('(11) 91234-5678');
      expect(applyMask('phone', ' +55 (47) 3123-4567')).toBe('(47) 3123-4567');
      expect(applyMask('phone', '55 47 91234-5678')).toBe('(55) 47912-3456');
    });

    it('não confunde o DDD 55 com o código do país', () => {
      expect(applyMask('phone', '(55) 99123-4567')).toBe('(55) 99123-4567');
    });
  });

  describe('date', () => {
    it.each([
      ['', ''],
      ['3', '3'],
      ['31', '31'],
      ['311', '31/1'],
      ['3101', '31/01'],
      ['31012', '31/01/2'],
      ['31012000', '31/01/2000'],
      ['310120001', '31/01/2000'],
      ['31/01/2000', '31/01/2000'],
    ])('formata "%s" como "%s"', (typed, shown) => {
      expect(applyMask('date', typed)).toBe(shown);
    });
  });
});

describe('unmask', () => {
  it('deixa só os dígitos do CPF, do CEP e do telefone', () => {
    expect(unmask('cpf', '123.456.789-09')).toBe('12345678909');
    expect(unmask('cep', '89010-000')).toBe('89010000');
    expect(unmask('phone', '(47) 99123-4567')).toBe('47991234567');
  });

  it('tira o +55 do telefone, como a API', () => {
    expect(unmask('phone', '+55 47 3123-4567')).toBe('4731234567');
  });

  it('deixa o CNPJ em maiúsculas, sem pontuação', () => {
    expect(unmask('cnpj', '12.abc.345/01de-35')).toBe('12ABC34501DE35');
    expect(unmask('cnpj', '11.222.333/0001-81')).toBe('11222333000181');
  });

  it('converte a data dd/mm/aaaa para aaaa-mm-dd', () => {
    expect(unmask('date', '31/01/2000')).toBe('2000-01-31');
    expect(unmask('date', '29/02/2008')).toBe('2008-02-29');
  });

  it.each(['', '31/01/20', '31/02/2000', '29/02/2007', '00/01/2000', '15/13/2000', '15/00/2000'])(
    'devolve vazio para a data incompleta ou inexistente "%s"',
    (text) => {
      expect(unmask('date', text)).toBe('');
    },
  );

  it('devolve vazio quando não há nada', () => {
    const kinds: MaskKind[] = ['cpf', 'cnpj', 'cep', 'phone', 'date'];
    for (const kind of kinds) {
      expect(unmask(kind, '')).toBe('');
    }
  });
});

describe('countSignificant', () => {
  it('conta só os dígitos nas máscaras numéricas', () => {
    expect(countSignificant('cpf', '123.4')).toBe(4);
    expect(countSignificant('phone', '(11) 9')).toBe(3);
    expect(countSignificant('date', '31/0')).toBe(3);
    expect(countSignificant('cep', '89010-')).toBe(5);
  });

  it('conta também as letras no CNPJ', () => {
    expect(countSignificant('cnpj', '12.ab')).toBe(4);
    expect(countSignificant('cnpj', '12.AB')).toBe(4);
  });

  it('não conta letras onde só cabem dígitos', () => {
    expect(countSignificant('cpf', '12a')).toBe(2);
  });
});

describe('caretAfter', () => {
  it('põe o cursor logo depois do caractere significativo pedido', () => {
    expect(caretAfter('cpf', '123.456', 3)).toBe(3);
    expect(caretAfter('cpf', '123.456', 4)).toBe(5);
    expect(caretAfter('cpf', '123.456', 6)).toBe(7);
  });

  it('põe o cursor no começo quando nada vem antes dele', () => {
    expect(caretAfter('cpf', '123.456', 0)).toBe(0);
    expect(caretAfter('phone', '(11) 9', 0)).toBe(0);
  });

  it('pula o parêntese de abertura do telefone', () => {
    expect(caretAfter('phone', '(11) 9', 1)).toBe(2);
    expect(caretAfter('phone', '(11) 9', 3)).toBe(6);
  });

  it('vai ao fim quando o pedido passa do que existe', () => {
    expect(caretAfter('cpf', '123.456', 9)).toBe(7);
    expect(caretAfter('cpf', '', 2)).toBe(0);
  });
});

describe('MASK_INPUTMODE', () => {
  it.each([
    ['cpf', 'numeric'],
    ['cnpj', 'text'],
    ['cep', 'numeric'],
    ['phone', 'tel'],
    ['date', 'numeric'],
  ] as const)('escolhe o teclado do campo %s', (kind, inputmode) => {
    expect(MASK_INPUTMODE[kind]).toBe(inputmode);
  });
});
