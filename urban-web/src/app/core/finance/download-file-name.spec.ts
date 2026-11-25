import { downloadFileName } from './download-file-name';

const FATURA = 'fatura.pdf';

describe('downloadFileName', () => {
  it('le o nome do filename* quando a API o manda', () => {
    expect(
      downloadFileName(
        "attachment; filename=fatura-2026-11-a-101.pdf; filename*=UTF-8''fatura-2026-11-a-101.pdf",
        FATURA,
      ),
    ).toBe('fatura-2026-11-a-101.pdf');
  });

  it('le o nome simples, com ou sem aspas', () => {
    expect(downloadFileName('attachment; filename=fatura-2026-11-102.pdf', FATURA)).toBe(
      'fatura-2026-11-102.pdf',
    );
    expect(downloadFileName('attachment; filename="fatura-2026-11-102.pdf"', FATURA)).toBe(
      'fatura-2026-11-102.pdf',
    );
  });

  it('decodifica o nome percentual do filename*', () => {
    expect(
      downloadFileName("attachment; filename*=UTF-8''fatura-2026-11-casa%20a.pdf", FATURA),
    ).toBe('fatura-2026-11-casa a.pdf');
  });

  it('usa o nome padrao quando falta o cabecalho ou o nome', () => {
    expect(downloadFileName(null, FATURA)).toBe(FATURA);
    expect(downloadFileName('attachment', FATURA)).toBe(FATURA);
    expect(downloadFileName('attachment; filename=', FATURA)).toBe(FATURA);
    expect(downloadFileName('attachment; filename=..', FATURA)).toBe(FATURA);
  });

  it('o nome padrao e de quem chama: o recibo cai em recibo.pdf, a fatura em fatura.pdf', () => {
    expect(downloadFileName(null, 'recibo.pdf')).toBe('recibo.pdf');
    expect(downloadFileName('attachment; filename=', 'recibo.pdf')).toBe('recibo.pdf');
    expect(downloadFileName(null, FATURA)).toBe(FATURA);
  });

  it('le o nome do recibo que a API sugeriu', () => {
    expect(
      downloadFileName(
        "attachment; filename=recibo-000123.pdf; filename*=UTF-8''recibo-000123.pdf",
        'recibo.pdf',
      ),
    ).toBe('recibo-000123.pdf');
  });

  it('nao deixa o nome sair da pasta de downloads nem trazer caracteres de caminho', () => {
    expect(downloadFileName('attachment; filename="../../etc/passwd"', FATURA)).toBe('passwd');
    expect(downloadFileName('attachment; filename="..\\..\\boot.ini"', FATURA)).toBe('boot.ini');
    expect(downloadFileName("attachment; filename*=UTF-8''%2e%2e%2fsegredo.pdf", FATURA)).toBe(
      'segredo.pdf',
    );
  });

  it('ignora um filename* mal formado e cai no nome simples', () => {
    expect(
      downloadFileName(
        "attachment; filename=fatura-2026-11-101.pdf; filename*=UTF-8''%E0%A4%A",
        FATURA,
      ),
    ).toBe('fatura-2026-11-101.pdf');
  });
});
