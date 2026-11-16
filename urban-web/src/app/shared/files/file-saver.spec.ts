import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { FileSaver } from './file-saver';

describe('FileSaver', () => {
  let create: jest.Mock<(blob: Blob) => string>;
  let revoke: jest.Mock<(url: string) => void>;
  let click: jest.SpiedFunction<HTMLAnchorElement['click']>;

  beforeEach(() => {
    create = jest.fn<(blob: Blob) => string>().mockReturnValue('blob:urban/abc');
    revoke = jest.fn<(url: string) => void>();
    Object.defineProperty(URL, 'createObjectURL', { value: create, configurable: true });
    Object.defineProperty(URL, 'revokeObjectURL', { value: revoke, configurable: true });
    click = jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
  });

  afterEach(() => jest.restoreAllMocks());

  it('baixa o arquivo com o nome dado por um link temporario', () => {
    const blob = new Blob(['%PDF-1.7'], { type: 'application/pdf' });

    TestBed.inject(FileSaver).save(blob, 'fatura-2026-11-101.pdf');

    expect(create).toHaveBeenCalledWith(blob);
    expect(click).toHaveBeenCalledTimes(1);
    const link = click.mock.contexts[0] as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('blob:urban/abc');
    expect(link.download).toBe('fatura-2026-11-101.pdf');
  });

  it('nao deixa o link para tras e libera o endereco temporario depois', () => {
    jest.useFakeTimers();
    try {
      TestBed.inject(FileSaver).save(new Blob(['x']), 'a.pdf');

      expect(document.querySelector('a[download]')).toBeNull();
      expect(revoke).not.toHaveBeenCalled();

      jest.runAllTimers();

      expect(revoke).toHaveBeenCalledWith('blob:urban/abc');
    } finally {
      jest.useRealTimers();
    }
  });
});
