/**
 * O nome do arquivo que a API sugeriu em Content-Disposition (`filename*` primeiro, depois `filename`). Só o último trecho
 * do caminho vale, para um nome estranho nunca sair da pasta de downloads; sem nome utilizável, o nome padrão de quem chama
 * (`fatura.pdf`, `recibo.pdf`).
 */
export function downloadFileName(contentDisposition: string | null, fallback: string): string {
  if (contentDisposition === null) {
    return fallback;
  }
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(contentDisposition)?.[1];
  const plain = /filename=("?)([^";]*)\1/i.exec(contentDisposition)?.[2];
  const name = baseName((encoded === undefined ? null : decode(encoded.trim())) ?? plain ?? '');

  return name === '' || /^\.+$/.test(name) ? fallback : name;
}

function decode(value: string): string | null {
  try {
    return decodeURIComponent(value);
  } catch {
    return null;
  }
}

function baseName(path: string): string {
  return (path.split(/[\\/]/).at(-1) ?? '').trim();
}
