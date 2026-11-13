import { fileOfSize } from '../../../testing/contact.fixtures';
import {
  ATTACHMENT_ACCEPT,
  ATTACHMENT_LIMITS,
  REPLY_BODY_LIMITS,
  checkAttachments,
  formatFileSize,
} from './reply-attachments';

const MB = 1024 * 1024;

describe('limites da resposta', () => {
  it('espelham os da API', () => {
    expect(REPLY_BODY_LIMITS).toEqual({ min: 5, max: 5000 });
    expect(ATTACHMENT_LIMITS).toEqual({
      maxFiles: 3,
      maxFileBytes: 5 * MB,
      maxTotalBytes: 10 * MB,
    });
  });

  it('o seletor de arquivos só oferece os tipos aceitos', () => {
    expect(ATTACHMENT_ACCEPT).toBe('.pdf,.png,.jpg,.jpeg,.docx,.xlsx,.csv,.txt');
  });
});

describe('checkAttachments', () => {
  it('acrescenta os arquivos escolhidos depois dos que já estavam, sem avisos', () => {
    const before = fileOfSize('a.pdf', 100);
    const picked = [fileOfSize('b.png', 200, 'image/png'), fileOfSize('c.txt', 300, 'text/plain')];

    const result = checkAttachments([before], picked);

    expect(result.files.map((file) => file.name)).toEqual(['a.pdf', 'b.png', 'c.txt']);
    expect(result.problems).toEqual([]);
  });

  it('não altera a lista recebida', () => {
    const current = [fileOfSize('a.pdf', 100)];

    checkAttachments(current, [fileOfSize('b.pdf', 100)]);

    expect(current).toHaveLength(1);
  });

  it.each(['relatorio.pdf', 'FOTO.JPG', 'foto.jpeg', 'planilha.XLSX', 'texto.docx', 'dados.csv'])(
    'aceita %s, qualquer que seja a caixa da extensão',
    (name) => {
      expect(checkAttachments([], [fileOfSize(name, 10)]).problems).toEqual([]);
    },
  );

  it.each(['programa.exe', 'LEIAME', 'arquivo.', 'pagina.html', 'compactado.zip', '.pdf.exe'])(
    'recusa %s por causa do tipo e diz quais são aceitos',
    (name) => {
      const result = checkAttachments([], [fileOfSize(name, 10)]);

      expect(result.files).toEqual([]);
      expect(result.problems).toEqual([`“${name}”: tipo de arquivo não aceito`]);
    },
  );

  it('recusa arquivo vazio', () => {
    const result = checkAttachments([], [fileOfSize('vazio.pdf', 0)]);

    expect(result.files).toEqual([]);
    expect(result.problems).toEqual(['“vazio.pdf”: o arquivo está vazio']);
  });

  it('aceita exatamente 5 MB e recusa um byte a mais', () => {
    const result = checkAttachments(
      [],
      [fileOfSize('limite.pdf', 5 * MB), fileOfSize('passou.pdf', 5 * MB + 1)],
    );

    expect(result.files.map((file) => file.name)).toEqual(['limite.pdf']);
    expect(result.problems).toEqual(['“passou.pdf”: passa de 5 MB']);
  });

  it('aceita até 3 arquivos e avisa uma vez só dos que passam disso', () => {
    const picked = ['a', 'b', 'c', 'd', 'e'].map((name) => fileOfSize(`${name}.pdf`, 10));

    const result = checkAttachments([], picked);

    expect(result.files.map((file) => file.name)).toEqual(['a.pdf', 'b.pdf', 'c.pdf']);
    expect(result.problems).toEqual(['Anexe no máximo 3 arquivos']);
  });

  it('conta os arquivos que já estavam na lista', () => {
    const current = [fileOfSize('a.pdf', 10), fileOfSize('b.pdf', 10), fileOfSize('c.pdf', 10)];

    const result = checkAttachments(current, [fileOfSize('d.pdf', 10)]);

    expect(result.files).toEqual(current);
    expect(result.problems).toEqual(['Anexe no máximo 3 arquivos']);
  });

  it('aceita 10 MB no total e recusa o arquivo que faria passar disso', () => {
    const result = checkAttachments(
      [fileOfSize('a.pdf', 5 * MB), fileOfSize('b.pdf', 5 * MB)],
      [fileOfSize('c.pdf', 1)],
    );

    expect(result.files.map((file) => file.name)).toEqual(['a.pdf', 'b.pdf']);
    expect(result.problems).toEqual(['Os anexos somam mais de 10 MB']);
  });

  it('guarda os bons e avisa de cada recusado, na ordem em que vieram', () => {
    const result = checkAttachments(
      [],
      [
        fileOfSize('bom.pdf', 10),
        fileOfSize('ruim.exe', 10),
        fileOfSize('vazio.txt', 0),
        fileOfSize('outro.png', 20, 'image/png'),
      ],
    );

    expect(result.files.map((file) => file.name)).toEqual(['bom.pdf', 'outro.png']);
    expect(result.problems).toEqual([
      '“ruim.exe”: tipo de arquivo não aceito',
      '“vazio.txt”: o arquivo está vazio',
    ]);
  });
});

describe('formatFileSize', () => {
  it.each([
    [0, '0 B'],
    [850, '850 B'],
    [1024, '1 KB'],
    [1536, '1,5 KB'],
    [200 * 1024, '200 KB'],
    [MB, '1 MB'],
    [2.5 * MB, '2,5 MB'],
    [5 * MB, '5 MB'],
  ])('%s bytes aparecem como %s', (bytes, text) => {
    expect(formatFileSize(bytes)).toBe(text);
  });
});
