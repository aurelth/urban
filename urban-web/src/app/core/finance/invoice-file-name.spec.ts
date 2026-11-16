import { invoiceFileName } from './invoice-file-name';

describe('invoiceFileName', () => {
  it('le o nome do filename* quando a API o manda', () => {
    expect(
      invoiceFileName(
        "attachment; filename=fatura-2026-11-a-101.pdf; filename*=UTF-8''fatura-2026-11-a-101.pdf",
      ),
    ).toBe('fatura-2026-11-a-101.pdf');
  });

  it('le o nome simples, com ou sem aspas', () => {
    expect(invoiceFileName('attachment; filename=fatura-2026-11-102.pdf')).toBe(
      'fatura-2026-11-102.pdf',
    );
    expect(invoiceFileName('attachment; filename="fatura-2026-11-102.pdf"')).toBe(
      'fatura-2026-11-102.pdf',
    );
  });

  it('decodifica o nome percentual do filename*', () => {
    expect(invoiceFileName("attachment; filename*=UTF-8''fatura-2026-11-casa%20a.pdf")).toBe(
      'fatura-2026-11-casa a.pdf',
    );
  });

  it('usa o nome padrao quando falta o cabecalho ou o nome', () => {
    expect(invoiceFileName(null)).toBe('fatura.pdf');
    expect(invoiceFileName('attachment')).toBe('fatura.pdf');
    expect(invoiceFileName('attachment; filename=')).toBe('fatura.pdf');
  });

  it('nao deixa o nome sair da pasta de downloads nem trazer caracteres de caminho', () => {
    expect(invoiceFileName('attachment; filename="../../etc/passwd"')).toBe('passwd');
    expect(invoiceFileName('attachment; filename="..\\..\\boot.ini"')).toBe('boot.ini');
    expect(invoiceFileName("attachment; filename*=UTF-8''%2e%2e%2fsegredo.pdf")).toBe(
      'segredo.pdf',
    );
  });

  it('ignora um filename* mal formado e cai no nome simples', () => {
    expect(
      invoiceFileName("attachment; filename=fatura-2026-11-101.pdf; filename*=UTF-8''%E0%A4%A"),
    ).toBe('fatura-2026-11-101.pdf');
  });
});
